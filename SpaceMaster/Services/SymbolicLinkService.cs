using System.Runtime.InteropServices;

namespace SpaceMaster.Services;

/// <summary>
/// 符号链接服务
/// </summary>
public partial class SymbolicLinkService
{
    [LibraryImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

    private const int SYMBOLIC_LINK_FLAG_FILE = 0x0;
    private const int SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
    private const int SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

    /// <summary>
    /// 创建符号链接
    /// </summary>
    /// <param name="linkPath">链接路径（原始位置）</param>
    /// <param name="targetPath">目标路径（实际文件位置）</param>
    /// <param name="isDirectory">是否为目录</param>
    public bool CreateLink(string linkPath, string targetPath, bool isDirectory)
    {
        int flags = isDirectory ? SYMBOLIC_LINK_FLAG_DIRECTORY : SYMBOLIC_LINK_FLAG_FILE;
        flags |= SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;

        bool result = CreateSymbolicLink(linkPath, targetPath, flags);

        if (!result)
        {
            // 如果失败，尝试不使用ALLOW_UNPRIVILEGED_CREATE标志
            flags = isDirectory ? SYMBOLIC_LINK_FLAG_DIRECTORY : SYMBOLIC_LINK_FLAG_FILE;
            result = CreateSymbolicLink(linkPath, targetPath, flags);
        }

        return result;
    }

    /// <summary>
    /// 检查路径是否为符号链接
    /// </summary>
    public bool IsSymbolicLink(string path)
    {
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }

        if (Directory.Exists(path))
        {
            var info = new DirectoryInfo(path);
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }

        return false;
    }

    /// <summary>
    /// 删除符号链接（不删除目标文件）
    /// </summary>
    public bool RemoveLink(string linkPath)
    {
        if (!IsSymbolicLink(linkPath))
            return false;

        if (File.Exists(linkPath))
        {
            File.Delete(linkPath);
            return true;
        }

        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath, false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查文件或目录是否被占用
    /// </summary>
    public bool IsPathInUse(string path)
    {
        if (File.Exists(path))
        {
            return IsFileInUse(path);
        }

        if (Directory.Exists(path))
        {
            return IsDirectoryInUse(path);
        }

        return false;
    }

    private bool IsFileInUse(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private bool IsDirectoryInUse(string directoryPath)
    {
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            if (IsFileInUse(file))
                return true;
        }
        return false;
    }
}
