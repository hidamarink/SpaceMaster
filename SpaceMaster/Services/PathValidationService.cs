namespace SpaceMaster.Services;

/// <summary>
/// 路径验证结果
/// </summary>
public class PathValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static PathValidationResult Success() => new() { IsValid = true };
    public static PathValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
}

/// <summary>
/// 路径验证服务 - 检查系统目录黑名单
/// </summary>
public class PathValidationService
{
    private static readonly string[] BlacklistedDirectoryPatterns =
    [
        @"\Windows\",
        @"\Windows",
        @"\Program Files\",
        @"\Program Files",
        @"\Program Files (x86)\",
        @"\Program Files (x86)",
        @"\ProgramData\",
        @"\ProgramData",
        @"\Recovery\",
        @"\Recovery",
        @"\$Recycle.Bin\",
        @"\$Recycle.Bin",
        @"\System Volume Information\",
        @"\System Volume Information",
        @"\AppData\Local\Microsoft\",
        @"\AppData\Roaming\Microsoft\"
    ];

    private static readonly string[] BlacklistedFilePatterns =
    [
        "NTUSER.DAT",
        "ntuser.dat",
        "pagefile.sys",
        "swapfile.sys",
        "hiberfil.sys",
        "bootmgr",
        "BOOTNXT"
    ];

    /// <summary>
    /// 验证路径是否可以迁移
    /// </summary>
    public PathValidationResult ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathValidationResult.Failure("路径不能为空");

        if (!Path.IsPathRooted(path))
            return PathValidationResult.Failure("必须是绝对路径");

        // 检查路径是否存在
        if (!File.Exists(path) && !Directory.Exists(path))
            return PathValidationResult.Failure("路径不存在");

        // 检查是否是系统/隐藏文件
        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.System))
            return PathValidationResult.Failure("不能迁移系统文件");

        // 检查黑名单目录
        foreach (var pattern in BlacklistedDirectoryPatterns)
        {
            if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return PathValidationResult.Failure($"不能迁移系统关键目录: {pattern.Trim('\\')}");
        }

        // 检查黑名单文件名
        var fileName = Path.GetFileName(path);
        foreach (var pattern in BlacklistedFilePatterns)
        {
            if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return PathValidationResult.Failure($"不能迁移系统关键文件: {pattern}");
        }

        // 检查是否是根目录
        var root = Path.GetPathRoot(path);
        if (path.TrimEnd('\\').Equals(root?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return PathValidationResult.Failure("不能迁移磁盘根目录");

        return PathValidationResult.Success();
    }

    /// <summary>
    /// 检查路径是否已经是符号链接
    /// </summary>
    public bool IsSymbolicLink(string path)
    {
        var attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
