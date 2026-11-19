using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NightEmber.Core.Services;
using NightEmber.Services;

namespace NightEmber;

/// <summary>
/// Provides the Night Ember application entry point and lifecycle coordination.
/// </summary>
public sealed partial class App : Application, IDisposable
{
    private SingleInstanceService? _singleInstance;
    private WatchdogService? _watchdog;
    private AppController? _controller;

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;

        _watchdog?.SignalOrderlyExit();
        _watchdog?.Dispose();
        _watchdog = null;

        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        // Headless test sessions create this application without a desktop lifetime and skip startup.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Start(desktop);
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

            // The main loop has not started yet, so shut down from inside it.
            Dispatcher.UIThread.Post(() => desktop.Shutdown());
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
