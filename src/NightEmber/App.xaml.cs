using NightEmber.Services;
using System.Windows;

namespace NightEmber;

/// <summary>
/// Provides the Night Ember application entry point and lifecycle coordination.
/// </summary>
public sealed partial class App : IDisposable
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var hidden = e.Args.Contains("--hidden", StringComparer.OrdinalIgnoreCase);
        _singleInstance?.Dispose();
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsPrimaryInstance)
        {
            if (!hidden)
            {
                SingleInstanceService.SignalPrimaryInstance();
            }

            Shutdown();
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
            MessageBox.Show(
                $"Night Ember could not start its display cleanup watchdog.\n\n{ex.Message}",
                "Night Ember",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        _controller?.Dispose();
        var controller = new AppController(Dispatcher, Shutdown);
        _controller = controller;

#pragma warning disable VSTHRD001, VSTHRD110 // WPF owns this dispatcher; activation is intentionally fire-and-forget.
        _singleInstance.Listen(() => Dispatcher.BeginInvoke(controller.ShowSettings));
#pragma warning restore VSTHRD001, VSTHRD110

        controller.Initialize(hidden);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }
}
