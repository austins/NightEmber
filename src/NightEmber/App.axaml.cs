using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using NightEmber.Core.Services;
using NightEmber.Services;

namespace NightEmber;

/// <summary>
/// Provides the Night Ember application entry point and lifecycle coordination.
/// </summary>
public sealed partial class App : Application, IDisposable
{
    private static readonly string[] AccentButtonForegroundKeys =
    [
        "AccentButtonForeground",
        "AccentButtonForegroundPointerOver",
        "AccentButtonForegroundPressed"
    ];

    private SingleInstanceService? _singleInstance;
    private WatchdogService? _watchdog;
    private AppController? _controller;

    public void Dispose()
    {
        _controller?.Dispose();
        if (_controller?.NeutralRestoreFailed != true)
        {
            // Without the signal, the watchdog treats this like a crash and retries the restore itself.
            _watchdog?.SignalOrderlyExit();
        }

        _controller = null;

        _watchdog?.Dispose();
        _watchdog = null;

        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (PlatformSettings is { } settings)
        {
            UpdateAccentButtonForeground(settings.GetColorValues());
            settings.ColorValuesChanged += (_, values) => UpdateAccentButtonForeground(values);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        // Headless test sessions create this application without a desktop lifetime and skip startup.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Start can show a modal warning, so run it inside the main loop rather than before it.
            Dispatcher.UIThread.Post(() => Start(desktop));
        }
    }

    /// <summary>
    /// Picks the text color for accent buttons in high contrast.
    /// </summary>
    /// <param name="values">The current platform colors.</param>
    /// <returns>Black or white, whichever reads better on the system accent color, or null outside high contrast.</returns>
    /// <remarks>
    /// The Fluent theme always draws accent button text in white, which is unreadable on the pale accent
    /// that Windows reports in high contrast.
    /// </remarks>
    internal static Color? GetHighContrastAccentForeground(PlatformColorValues values)
    {
        if (values.ContrastPreference != ColorContrastPreference.High)
        {
            return null;
        }

        var accent = values.AccentColor1;
        var luminance = (0.2126 * accent.R) + (0.7152 * accent.G) + (0.0722 * accent.B);
        return luminance > 127.5 ? Colors.Black : Colors.White;
    }

    private void UpdateAccentButtonForeground(PlatformColorValues values)
    {
        var color = GetHighContrastAccentForeground(values);
        foreach (var key in AccentButtonForegroundKeys)
        {
            if (color is { } foreground)
            {
                Resources[key] = new SolidColorBrush(foreground);
            }
            else
            {
                Resources.Remove(key);
            }
        }
    }

    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.Exit += (_, _) => Dispose();

        var hidden = desktop.Args?.Contains("--hidden", StringComparer.OrdinalIgnoreCase) ?? false;
        _singleInstance?.Dispose();
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsPrimaryInstance)
        {
            if (!hidden)
            {
                SingleInstanceService.SignalPrimaryInstance();
            }

            desktop.Shutdown();
            return;
        }

        _watchdog?.Dispose();
        _watchdog = new WatchdogService();
        try
        {
            _watchdog.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.ShowWarning($"Night Ember could not start its display cleanup watchdog.\n\n{ex.Message}");
        }

        _controller?.Dispose();
        var controller = new AppController(Dispatcher.UIThread, () => desktop.Shutdown());
        _controller = controller;

        _singleInstance.Listen(() => Dispatcher.UIThread.Post(controller.ShowSettings));

        controller.Initialize(hidden);
    }
}
