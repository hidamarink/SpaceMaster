namespace SpaceMaster.Models;

/// <summary>
/// 迁移记录实体
/// </summary>
public class MigrationRecord
{
    public long Id { get; set; }

    /// <summary>
    /// 源路径（原始位置）
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// 目标路径（迁移后的实际位置）
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// 源磁盘盘符（如 C）
    /// </summary>
    public string SourceDrive { get; set; } = string.Empty;

    /// <summary>
    /// 目标磁盘盘符（如 D）
    /// </summary>
    public string TargetDrive { get; set; } = string.Empty;

    /// <summary>
    /// 文件/文件夹大小（字节）
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// 迁移时间
    /// </summary>
    public DateTime MigratedAt { get; set; }

    /// <summary>
    /// 状态：Active=已迁移, Restored=已还原
    /// </summary>
    public MigrationStatus Status { get; set; }

    /// <summary>
    /// 运行时属性：符号链接是否有效
    /// </summary>
    public bool IsValid { get; set; } = true;
}

public enum MigrationStatus
{
    Active = 0,
    Restored = 1
}
