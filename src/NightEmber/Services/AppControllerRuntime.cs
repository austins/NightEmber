using NightEmber.Models;
using System.Diagnostics;
using System.Windows.Threading;

namespace NightEmber.Services;

/// <summary>
/// Connects the controller to the system clock, settings storage, startup registration, and WPF dispatcher.
/// </summary>
/// <param name="dispatcher">The dispatcher that owns controller callbacks and timers.</param>
internal sealed class AppControllerRuntime(Dispatcher dispatcher) : IAppControllerRuntime
{
    private readonly SettingsService _settings = new();
    private readonly StartupService _startup = new();

    public DateTime Now => DateTime.Now;

    public DateTime UtcNow => DateTime.UtcNow;

    public bool IsStartupEnabled => _startup.IsEnabled;

    public long GetTimestamp()
    {
        return Stopwatch.GetTimestamp();
    }

    public TimeSpan GetElapsedTime(long timestamp)
    {
        return Stopwatch.GetElapsedTime(timestamp);
    }

    public SettingsLoadResult LoadSettings()
    {
        return _settings.Load();
    }

    public void SaveSettings(AppSettings settings)
    {
        _settings.Save(settings);
    }

    public void SetStartupEnabled(bool enabled)
    {
        _startup.SetEnabled(enabled);
    }

    public IControllerTimer CreateTimer(TimeSpan interval, EventHandler handler)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = interval };
        timer.Tick += handler;
        return new ControllerTimer(timer);
    }

    public void Post(Action action)
    {
#pragma warning disable VSTHRD001, VSTHRD110 // WPF dispatcher scheduling is intentionally fire-and-forget.
        dispatcher.BeginInvoke(action);
#pragma warning restore VSTHRD001, VSTHRD110
    }

    public bool CheckAccess()
    {
        return dispatcher.CheckAccess();
    }

    public void Invoke(Action action)
    {
#pragma warning disable VSTHRD001 // Session-ending cleanup must synchronously reach the WPF dispatcher.
        dispatcher.Invoke(action);
#pragma warning restore VSTHRD001
    }

    private sealed class ControllerTimer(DispatcherTimer timer) : IControllerTimer
    {
        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }
    }
}

/// <summary>
/// Supplies external dependencies for controller scheduling, persistence, and UI-thread execution.
/// </summary>
internal interface IAppControllerRuntime
{
    /// <summary>
    /// Gets the current local date and time for schedule evaluation.
    /// </summary>
    public DateTime Now { get; }

    /// <summary>
    /// Gets the current UTC date and time for notification throttling.
    /// </summary>
    public DateTime UtcNow { get; }

    /// <summary>
    /// Gets a value indicating whether launch at sign-in is enabled.
    /// </summary>
    public bool IsStartupEnabled { get; }

    /// <summary>
    /// Captures a monotonic timestamp for measuring elapsed time.
    /// </summary>
    /// <returns>A timestamp to pass to <see cref="GetElapsedTime" />.</returns>
    public long GetTimestamp();

    /// <summary>
    /// Measures the time elapsed since a previously captured monotonic timestamp.
    /// </summary>
    /// <param name="timestamp">A value returned by <see cref="GetTimestamp" />.</param>
    /// <returns>The elapsed duration, unaffected by wall-clock adjustments.</returns>
    public TimeSpan GetElapsedTime(long timestamp);

    /// <summary>
    /// Loads application settings, including first-run status and any recoverable load warning.
    /// </summary>
    /// <returns>The loaded or default settings and their load status.</returns>
    public SettingsLoadResult LoadSettings();

    /// <summary>
    /// Persists application settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public void SaveSettings(AppSettings settings);

    /// <summary>
    /// Enables or disables application launch at sign-in.
    /// </summary>
    /// <param name="enabled">Whether to enable launch at sign-in.</param>
    public void SetStartupEnabled(bool enabled);

    /// <summary>
    /// Creates a stopped, recurring timer whose callbacks execute on the controller's dispatcher.
    /// </summary>
    /// <param name="interval">The interval between timer ticks.</param>
    /// <param name="handler">The callback invoked on each tick.</param>
    /// <returns>A timer controlled through <see cref="IControllerTimer.Start" /> and <see cref="IControllerTimer.Stop" />.</returns>
    public IControllerTimer CreateTimer(TimeSpan interval, EventHandler handler);

    /// <summary>
    /// Queues an action on the controller's dispatcher without waiting for it to execute.
    /// </summary>
    /// <param name="action">The action to queue.</param>
    public void Post(Action action);

    /// <summary>
    /// Determines whether the current thread owns the controller's dispatcher.
    /// </summary>
    /// <returns><see langword="true" /> when dispatcher-bound work can run directly.</returns>
    public bool CheckAccess();

    /// <summary>
    /// Executes an action on the controller's dispatcher and waits for completion.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Invoke(Action action);
}

/// <summary>
/// Controls recurring callbacks scheduled by the controller runtime.
/// </summary>
internal interface IControllerTimer
{
    /// <summary>
    /// Enables timer ticks; starting an already running timer leaves it enabled.
    /// </summary>
    public void Start();

    /// <summary>
    /// Disables timer ticks until <see cref="Start" /> is called again.
    /// </summary>
    public void Stop();
}
