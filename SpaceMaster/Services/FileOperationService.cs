namespace SpaceMaster.Services;

/// <summary>
/// 文件操作进度
/// </summary>
public class FileOperationProgress
{
    public long TotalBytes { get; set; }
    public long CopiedBytes { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public double Percentage => TotalBytes > 0 ? (double)CopiedBytes / TotalBytes * 100 : 0;
}

/// <summary>
/// 进度追踪器
/// </summary>
internal class ProgressTracker(long totalBytes, IProgress<FileOperationProgress>? progress)
{
    public long CopiedBytes { get; private set; }

    public void AddBytes(long bytes, string currentFile)
    {
        CopiedBytes += bytes;
        progress?.Report(new FileOperationProgress
        {
            TotalBytes = totalBytes,
            CopiedBytes = CopiedBytes,
            CurrentFile = currentFile
        });
    }
}

/// <summary>
/// 文件操作服务
/// </summary>
public class FileOperationService
{
    private const int BufferSize = 81920; // 80KB buffer

    /// <summary>
    /// 计算文件或目录的总大小
    /// </summary>
    public long CalculateSize(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path).Length;
        }

        if (Directory.Exists(path))
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }

        return 0;
    }

    /// <summary>
    /// 复制文件或目录到目标位置
    /// </summary>
    public async Task CopyAsync(
        string sourcePath,
        string targetPath,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalBytes = CalculateSize(sourcePath);
        var tracker = new ProgressTracker(totalBytes, progress);

        if (File.Exists(sourcePath))
        {
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            await CopyFileAsync(sourcePath, targetPath, tracker, cancellationToken);
        }
        else if (Directory.Exists(sourcePath))
        {
            await CopyDirectoryAsync(sourcePath, targetPath, tracker, cancellationToken);
        }
    }

    private async Task CopyFileAsync(
        string sourceFile,
        string targetFile,
        ProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        await using var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        int bytesRead;
        var fileName = Path.GetFileName(sourceFile);

        while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
        {
            await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            tracker.AddBytes(bytesRead, fileName);
        }

        // 保留原始文件属性
        File.SetAttributes(targetFile, File.GetAttributes(sourceFile));
        File.SetCreationTime(targetFile, File.GetCreationTime(sourceFile));
        File.SetLastWriteTime(targetFile, File.GetLastWriteTime(sourceFile));
    }

    private async Task CopyDirectoryAsync(
        string sourceDir,
        string targetDir,
        ProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDir);

        // 复制所有文件
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            await CopyFileAsync(file, targetFile, tracker, cancellationToken);
        }

        // 递归复制子目录
        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, targetSubDir, tracker, cancellationToken);
        }

        // 保留目录属性
        var dirInfo = new DirectoryInfo(sourceDir);
        var targetDirInfo = new DirectoryInfo(targetDir);
        targetDirInfo.Attributes = dirInfo.Attributes;
        targetDirInfo.CreationTime = dirInfo.CreationTime;
        targetDirInfo.LastWriteTime = dirInfo.LastWriteTime;
    }

    /// <summary>
    /// 删除文件或目录
    /// </summary>
    public void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
