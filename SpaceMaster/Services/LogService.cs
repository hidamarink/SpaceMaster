namespace SpaceMaster.Services;

/// <summary>
/// 日志服务
/// </summary>
public static class LogService
{
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly object LockObject = new();

    static LogService()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    private static string GetLogFilePath()
    {
        return Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
    }

    public static void Info(string message)
    {
        WriteLog("INFO", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        var fullMessage = ex != null
            ? $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}"
            : message;

        WriteLog("ERROR", fullMessage);
    }

    public static void Warn(string message)
    {
        WriteLog("WARN", message);
    }

    private static void WriteLog(string level, string message)
    {
        var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

        lock (LockObject)
        {
            try
            {
                File.AppendAllText(GetLogFilePath(), logLine + Environment.NewLine);
            }
            catch
            {
                // 日志写入失败时静默处理
            }
        }
    }
}
