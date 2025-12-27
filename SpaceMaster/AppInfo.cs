using System.Reflection;

namespace SpaceMaster;

public static class AppInfo
{
    public static string Version
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" : "v1.0.0.0";
        }
    }

    public static string BuildNumber
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.Revision.ToString() ?? "0";
        }
    }
}
