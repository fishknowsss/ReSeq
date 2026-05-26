using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReSeq.Core.Services;
using ReSeq.Services;
using ReSeq.ViewModels;

namespace ReSeq;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFileSystemService, PhysicalFileSystemService>();
        services.AddSingleton<VideoScanner>();
        services.AddSingleton<RenamePlanner>();
        services.AddSingleton<SafeRenameExecutor>();
        services.AddSingleton<ShellThumbnailService>();
        services.AddSingleton<ViewModelFactory>();
        services.AddSingleton<WorkspaceViewModel>();
        services.AddSingleton<RenamePlanViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception);
        TryReportToMainWindow(e.Exception);
        e.Handled = true;
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception);
        e.SetObserved();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogUnhandledException(exception);
        }
    }

    private static void TryReportToMainWindow(Exception exception)
    {
        if (Current?.MainWindow?.DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ReportError("运行异常已拦截", exception);
        }
    }

    private static void LogUnhandledException(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReSeq",
                "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // The exception logger must never become the reason the app exits.
        }
    }
}
