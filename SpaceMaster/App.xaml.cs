using System.Windows;
using System.Windows.Threading;
using SpaceMaster.Services;

namespace SpaceMaster;

public partial class App : Application
{
    public App()
    {
        // 捕获UI线程未处理的异常
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // 捕获非UI线程未处理的异常
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 捕获Task未处理的异常
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        LogService.Info($"========== SpaceMaster {AppInfo.Version} 启动 ==========");
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.Error("UI线程未处理异常", e.Exception);
        MessageBox.Show($"发生错误: {e.Exception.Message}\n\n详情已记录到日志文件。",
            "SpaceMaster 错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogService.Error("非UI线程未处理异常", ex);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogService.Error("Task未处理异常", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Info("应用程序退出");
        base.OnExit(e);
    }
}
