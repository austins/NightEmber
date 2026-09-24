using NightEmber.Services;
using System.Runtime.CompilerServices;

namespace NightEmber;

/// <summary>
/// Provides the process entry point for the application and its display cleanup watchdog.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Dispatcher exceptions that WPF does not handle also surface here before the process ends.
        AppDomain.CurrentDomain.UnhandledException += static (_, e) => CrashLog.Write(e.ExceptionObject);

        // The watchdog runs for the whole session, so it dispatches before any WPF type
        // is referenced to avoid loading the presentation stack into the companion process.
        return WatchdogService.IsWatchdogCommand(args)
            ? WatchdogService.RunFromCommandLine(args)
            : RunApplication();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication()
    {
        using var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
