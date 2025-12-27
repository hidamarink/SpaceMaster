using SpaceMaster.Data;
using SpaceMaster.Models;

namespace SpaceMaster.Services;

/// <summary>
/// 迁移结果
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public MigrationRecord? Record { get; set; }

    public static MigrationResult Ok(MigrationRecord record) => new() { Success = true, Record = record };
    public static MigrationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// 迁移服务 - 核心业务逻辑
/// </summary>
public class MigrationService
{
    private readonly PathValidationService _pathValidation = new();
    private readonly DriveService _driveService = new();
    private readonly FileOperationService _fileOperation = new();
    private readonly SymbolicLinkService _symbolicLink = new();

    /// <summary>
    /// 生成目标路径
    /// 格式: {目标盘}:\MovedFiles\{源盘符}\{源相对路径}
    /// </summary>
    public string GenerateTargetPath(string sourcePath, string targetDrive)
    {
        var sourceDrive = DriveService.GetDriveLetter(sourcePath);
        var relativePath = sourcePath.Substring(3); // 去掉 "C:\"

        return Path.Combine($"{targetDrive}:\\", "MovedFiles", sourceDrive, relativePath);
    }

    /// <summary>
    /// 执行迁移
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        string sourcePath,
        string targetDrive,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 验证路径
        var validation = _pathValidation.ValidatePath(sourcePath);
        if (!validation.IsValid)
            return MigrationResult.Fail(validation.ErrorMessage!);

        // 2. 检查是否已经是符号链接
        if (_pathValidation.IsSymbolicLink(sourcePath))
            return MigrationResult.Fail("该路径已经是符号链接，无需迁移");

        // 3. 检查文件占用
        if (_symbolicLink.IsPathInUse(sourcePath))
            return MigrationResult.Fail("文件或目录正在被使用，请关闭相关程序后重试");

        // 4. 计算大小并检查目标空间
        var size = _fileOperation.CalculateSize(sourcePath);
        if (!_driveService.HasEnoughSpace(targetDrive, size))
            return MigrationResult.Fail($"目标磁盘 {targetDrive} 空间不足");

        var targetPath = GenerateTargetPath(sourcePath, targetDrive);
        var isDirectory = Directory.Exists(sourcePath);

        try
        {
            // 5. 复制文件到目标位置
            await _fileOperation.CopyAsync(sourcePath, targetPath, progress, cancellationToken);

            // 6. 删除源文件/目录
            _fileOperation.Delete(sourcePath);

            // 7. 创建符号链接
            if (!_symbolicLink.CreateLink(sourcePath, targetPath, isDirectory))
            {
                // 如果创建链接失败，尝试还原
                await _fileOperation.CopyAsync(targetPath, sourcePath, null, CancellationToken.None);
                _fileOperation.Delete(targetPath);
                return MigrationResult.Fail("创建符号链接失败，请确保以管理员身份运行");
            }

            // 8. 保存记录
            var record = new MigrationRecord
            {
                SourcePath = sourcePath,
                TargetPath = targetPath,
                SourceDrive = DriveService.GetDriveLetter(sourcePath),
                TargetDrive = targetDrive,
                Size = size,
                IsDirectory = isDirectory,
                MigratedAt = DateTime.Now,
                Status = MigrationStatus.Active
            };

            using var db = new DatabaseContext();
            record.Id = db.InsertRecord(record);

            return MigrationResult.Ok(record);
        }
        catch (OperationCanceledException)
        {
            // 取消操作，清理已复制的文件
            if (Directory.Exists(targetPath) || File.Exists(targetPath))
                _fileOperation.Delete(targetPath);

            return MigrationResult.Fail("操作已取消");
        }
        catch (Exception ex)
        {
            return MigrationResult.Fail($"迁移失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 还原迁移
    /// </summary>
    public async Task<MigrationResult> RestoreAsync(
        long recordId,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var db = new DatabaseContext();
        var record = db.GetRecordById(recordId);

        if (record == null)
            return MigrationResult.Fail("找不到迁移记录");

        if (record.Status == MigrationStatus.Restored)
            return MigrationResult.Fail("该记录已经还原");

        try
        {
            // 1. 删除符号链接
            if (!_symbolicLink.RemoveLink(record.SourcePath))
                return MigrationResult.Fail("删除符号链接失败");

            // 2. 复制文件回原位置
            await _fileOperation.CopyAsync(record.TargetPath, record.SourcePath, progress, cancellationToken);

            // 3. 删除目标位置的文件
            _fileOperation.Delete(record.TargetPath);

            // 4. 更新记录状态
            db.UpdateRecordStatus(recordId, MigrationStatus.Restored);
            record.Status = MigrationStatus.Restored;

            return MigrationResult.Ok(record);
        }
        catch (Exception ex)
        {
            return MigrationResult.Fail($"还原失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取迁移记录
    /// </summary>
    public List<MigrationRecord> GetRecords(string? sourceDriveFilter = null, string? targetDriveFilter = null)
    {
        using var db = new DatabaseContext();
        return db.GetRecords(sourceDriveFilter, targetDriveFilter);
    }

    /// <summary>
    /// 获取用于筛选的源磁盘列表
    /// </summary>
    public List<string> GetDistinctSourceDrives()
    {
        using var db = new DatabaseContext();
        return db.GetDistinctSourceDrives();
    }

    /// <summary>
    /// 获取用于筛选的目标磁盘列表
    /// </summary>
    public List<string> GetDistinctTargetDrives()
    {
        using var db = new DatabaseContext();
        return db.GetDistinctTargetDrives();
    }

    /// <summary>
    /// 删除迁移记录
    /// </summary>
    public void DeleteRecord(long recordId)
    {
        using var db = new DatabaseContext();
        db.DeleteRecord(recordId);
    }

    /// <summary>
    /// 重做迁移（将已还原的项目重新迁移）
    /// </summary>
    public async Task<MigrationResult> RemigrateAsync(
        long recordId,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var db = new DatabaseContext();
        var record = db.GetRecordById(recordId);

        if (record == null)
            return MigrationResult.Fail("找不到迁移记录");

        if (record.Status != MigrationStatus.Restored)
            return MigrationResult.Fail("只能重做已还原的记录");

        // 检查源路径是否存在
        if (!File.Exists(record.SourcePath) && !Directory.Exists(record.SourcePath))
            return MigrationResult.Fail("源路径不存在");

        // 检查是否已经是符号链接
        if (_pathValidation.IsSymbolicLink(record.SourcePath))
            return MigrationResult.Fail("该路径已经是符号链接");

        // 检查文件占用
        if (_symbolicLink.IsPathInUse(record.SourcePath))
            return MigrationResult.Fail("文件或目录正在被使用，请关闭相关程序后重试");

        // 检查目标空间
        var size = _fileOperation.CalculateSize(record.SourcePath);
        if (!_driveService.HasEnoughSpace(record.TargetDrive, size))
            return MigrationResult.Fail($"目标磁盘 {record.TargetDrive} 空间不足");

        try
        {
            // 1. 复制文件到目标位置
            await _fileOperation.CopyAsync(record.SourcePath, record.TargetPath, progress, cancellationToken);

            // 2. 删除源文件/目录
            _fileOperation.Delete(record.SourcePath);

            // 3. 创建符号链接
            if (!_symbolicLink.CreateLink(record.SourcePath, record.TargetPath, record.IsDirectory))
            {
                await _fileOperation.CopyAsync(record.TargetPath, record.SourcePath, null, CancellationToken.None);
                _fileOperation.Delete(record.TargetPath);
                return MigrationResult.Fail("创建符号链接失败，请确保以管理员身份运行");
            }

            // 4. 更新记录状态
            db.UpdateRecordStatus(recordId, MigrationStatus.Active);
            record.Status = MigrationStatus.Active;

            return MigrationResult.Ok(record);
        }
        catch (OperationCanceledException)
        {
            if (Directory.Exists(record.TargetPath) || File.Exists(record.TargetPath))
                _fileOperation.Delete(record.TargetPath);

            return MigrationResult.Fail("操作已取消");
        }
        catch (Exception ex)
        {
            return MigrationResult.Fail($"重做失败: {ex.Message}");
        }
    }
}
