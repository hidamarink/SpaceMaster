using Microsoft.Data.Sqlite;
using SpaceMaster.Models;

namespace SpaceMaster.Data;

/// <summary>
/// SQLite数据库上下文
/// </summary>
public class DatabaseContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SpaceMaster");
    private static readonly string DbPath = Path.Combine(DataDir, "spacemaster.db");

    public DatabaseContext()
    {
        Directory.CreateDirectory(DataDir);
        _connection = new SqliteConnection($"Data Source={DbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS MigrationRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourcePath TEXT NOT NULL,
                TargetPath TEXT NOT NULL,
                SourceDrive TEXT NOT NULL,
                TargetDrive TEXT NOT NULL,
                Size INTEGER NOT NULL,
                IsDirectory INTEGER NOT NULL,
                MigratedAt TEXT NOT NULL,
                Status INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_source_drive ON MigrationRecords(SourceDrive);
            CREATE INDEX IF NOT EXISTS idx_target_drive ON MigrationRecords(TargetDrive);
            CREATE INDEX IF NOT EXISTS idx_status ON MigrationRecords(Status);
            """;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public long InsertRecord(MigrationRecord record)
    {
        const string sql = """
            INSERT INTO MigrationRecords (SourcePath, TargetPath, SourceDrive, TargetDrive, Size, IsDirectory, MigratedAt, Status)
            VALUES (@SourcePath, @TargetPath, @SourceDrive, @TargetDrive, @Size, @IsDirectory, @MigratedAt, @Status);
            SELECT last_insert_rowid();
            """;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@SourcePath", record.SourcePath);
        cmd.Parameters.AddWithValue("@TargetPath", record.TargetPath);
        cmd.Parameters.AddWithValue("@SourceDrive", record.SourceDrive);
        cmd.Parameters.AddWithValue("@TargetDrive", record.TargetDrive);
        cmd.Parameters.AddWithValue("@Size", record.Size);
        cmd.Parameters.AddWithValue("@IsDirectory", record.IsDirectory ? 1 : 0);
        cmd.Parameters.AddWithValue("@MigratedAt", record.MigratedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@Status", (int)record.Status);

        return (long)cmd.ExecuteScalar()!;
    }

    public void UpdateRecordStatus(long id, MigrationStatus status)
    {
        const string sql = "UPDATE MigrationRecords SET Status = @Status WHERE Id = @Id";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Status", (int)status);
        cmd.ExecuteNonQuery();
    }

    public List<MigrationRecord> GetRecords(string? sourceDriveFilter = null, string? targetDriveFilter = null)
    {
        var sql = "SELECT * FROM MigrationRecords WHERE 1=1";

        if (!string.IsNullOrEmpty(sourceDriveFilter))
            sql += " AND SourceDrive = @SourceDrive";
        if (!string.IsNullOrEmpty(targetDriveFilter))
            sql += " AND TargetDrive = @TargetDrive";

        sql += " ORDER BY MigratedAt DESC";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        if (!string.IsNullOrEmpty(sourceDriveFilter))
            cmd.Parameters.AddWithValue("@SourceDrive", sourceDriveFilter);
        if (!string.IsNullOrEmpty(targetDriveFilter))
            cmd.Parameters.AddWithValue("@TargetDrive", targetDriveFilter);

        var records = new List<MigrationRecord>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            records.Add(new MigrationRecord
            {
                Id = reader.GetInt64(0),
                SourcePath = reader.GetString(1),
                TargetPath = reader.GetString(2),
                SourceDrive = reader.GetString(3),
                TargetDrive = reader.GetString(4),
                Size = reader.GetInt64(5),
                IsDirectory = reader.GetInt64(6) == 1,
                MigratedAt = DateTime.Parse(reader.GetString(7)),
                Status = (MigrationStatus)reader.GetInt64(8)
            });
        }

        return records;
    }

    public MigrationRecord? GetRecordById(long id)
    {
        const string sql = "SELECT * FROM MigrationRecords WHERE Id = @Id";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new MigrationRecord
            {
                Id = reader.GetInt64(0),
                SourcePath = reader.GetString(1),
                TargetPath = reader.GetString(2),
                SourceDrive = reader.GetString(3),
                TargetDrive = reader.GetString(4),
                Size = reader.GetInt64(5),
                IsDirectory = reader.GetInt64(6) == 1,
                MigratedAt = DateTime.Parse(reader.GetString(7)),
                Status = (MigrationStatus)reader.GetInt64(8)
            };
        }

        return null;
    }

    public List<string> GetDistinctSourceDrives()
    {
        const string sql = "SELECT DISTINCT SourceDrive FROM MigrationRecords ORDER BY SourceDrive";
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        var drives = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            drives.Add(reader.GetString(0));
        }
        return drives;
    }

    public List<string> GetDistinctTargetDrives()
    {
        const string sql = "SELECT DISTINCT TargetDrive FROM MigrationRecords ORDER BY TargetDrive";
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        var drives = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            drives.Add(reader.GetString(0));
        }
        return drives;
    }

    public void DeleteRecord(long id)
    {
        const string sql = "DELETE FROM MigrationRecords WHERE Id = @Id";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
