using SpaceMaster.Models;

namespace SpaceMaster.Services;

/// <summary>
/// 磁盘信息服务
/// </summary>
public class DriveService
{
    /// <summary>
    /// 获取所有可用的固定磁盘
    /// </summary>
    public List<DriveInfoModel> GetAvailableDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => new DriveInfoModel
            {
                Name = d.Name.TrimEnd('\\'),
                Label = d.VolumeLabel,
                TotalSize = d.TotalSize,
                FreeSpace = d.AvailableFreeSpace
            })
            .ToList();
    }

    /// <summary>
    /// 获取除指定磁盘外的其他可用磁盘
    /// </summary>
    public List<DriveInfoModel> GetOtherDrives(string excludeDrive)
    {
        var normalizedExclude = excludeDrive.TrimEnd('\\', ':').ToUpperInvariant();

        return GetAvailableDrives()
            .Where(d => !d.Name.TrimEnd('\\', ':').Equals(normalizedExclude, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// 检查目标磁盘是否有足够空间
    /// </summary>
    public bool HasEnoughSpace(string targetDrive, long requiredBytes)
    {
        var drive = GetAvailableDrives()
            .FirstOrDefault(d => d.Name.StartsWith(targetDrive, StringComparison.OrdinalIgnoreCase));

        return drive != null && drive.FreeSpace >= requiredBytes;
    }

    /// <summary>
    /// 从路径中提取盘符
    /// </summary>
    public static string GetDriveLetter(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 2)
            return string.Empty;

        return path[..1].ToUpperInvariant();
    }
}
