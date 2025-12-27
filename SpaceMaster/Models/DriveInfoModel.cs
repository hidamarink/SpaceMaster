namespace SpaceMaster.Models;

/// <summary>
/// 磁盘信息模型
/// </summary>
public class DriveInfoModel
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public long UsedSpace => TotalSize - FreeSpace;
    public double UsedPercentage => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;

    public string DisplayName => string.IsNullOrEmpty(Label)
        ? $"{Name}"
        : $"{Name} ({Label})";

    public string FreeSpaceFormatted => FormatSize(FreeSpace);
    public string TotalSizeFormatted => FormatSize(TotalSize);

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
