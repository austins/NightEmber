using Avalonia;
using Avalonia.Controls;
using NightEmber.Core.Services;
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
        // UI-thread exceptions that Avalonia does not handle also surface here before the process ends.
        AppDomain.CurrentDomain.UnhandledException += static (_, e) => CrashLog.Write(e.ExceptionObject);

        // The watchdog runs for the whole session, so it dispatches before any Avalonia type
        // is referenced to avoid loading the UI stack into the companion process.
        return WatchdogService.IsWatchdogCommand(args)
            ? WatchdogService.RunFromCommandLine(args)
            : RunApplication(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    // The XAML previewer finds this method by name.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseWin32()
            // The settings window is simple enough for software rendering, which lets the publish drop ANGLE.
            .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
            .UseSkia()
            .UseHarfBuzz();
    }
}
