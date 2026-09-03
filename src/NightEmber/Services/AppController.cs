using Microsoft.Win32;
using NightEmber.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NightEmber.Services;

/// <summary>
/// Coordinates settings, scheduling, gamma updates, tray behavior, and application lifecycle.
/// </summary>
internal sealed class AppController : IDisposable
{
    private const int FadeIntervalMilliseconds = 20;
    private const double GammaComparisonTolerance = 0.01;
    private const int NeutralBrightnessPercent = 100;
    private const int GammaErrorThrottleMinutes = 1;
    private const int SchedulePollIntervalSeconds = 20;
    private const int DriftCheckIntervalSeconds = 3;
    private const int DisplayReapplyDelayMilliseconds = 1200;

    private readonly Dispatcher _dispatcher;
    private readonly Action _shutdown;
    private readonly MessageThrottle _gammaErrorThrottle = new(TimeSpan.FromMinutes(GammaErrorThrottleMinutes));

    private readonly SettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly GammaService _gammaService = new();
    private readonly DispatcherTimer _scheduleTimer;
    private readonly DispatcherTimer _driftTimer;
    private readonly DispatcherTimer _reapplyTimer;
    private DispatcherTimer? _fadeTimer;
    private TrayIconService? _tray;
    private MainWindow? _window;
    private AppSettings _settings = AppSettings.Default;
    private bool _isOn;
    private bool? _manualOverride;
    private bool? _lastScheduledState;
    private bool _previewing;
    private int _exiting;
    private bool _disposed;
    private bool _neutralRestored;
    private double _currentKelvin = ColorTemperature.NeutralKelvin;
    private double _currentBrightness = NeutralBrightnessPercent;
    private int _previewTemperature = ColorTemperature.NeutralKelvin;
    private int _previewBrightness = NeutralBrightnessPercent;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Volatile.Write(ref _exiting, 1);
        UnsubscribeSystemEvents();
        StopTimers();
        if (!_neutralRestored)
        {
            TryResetGamma();
        }

        _window?.Close();
        _tray?.Dispose();
        _gammaService.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppController" /> class.
    /// </summary>
    /// <param name="dispatcher">The WPF dispatcher that owns UI-bound operations.</param>
    /// <param name="shutdown">The callback that requests application shutdown.</param>
    public AppController(Dispatcher dispatcher, Action shutdown)
    {
        _dispatcher = dispatcher;
        _shutdown = shutdown;

        _scheduleTimer = CreateTimer(
            TimeSpan.FromSeconds(SchedulePollIntervalSeconds),
            (_, _) => UpdateSchedule(false));

        _driftTimer = CreateTimer(TimeSpan.FromSeconds(DriftCheckIntervalSeconds), (_, _) => RepairGammaDrift());

        _reapplyTimer = CreateTimer(
            TimeSpan.FromMilliseconds(DisplayReapplyDelayMilliseconds),
            (_, _) => ReapplyDisplays());

        _reapplyTimer.Stop();
    }

    /// <summary>
    /// Gets an independent copy of the active settings.
    /// </summary>
    public AppSettings CurrentSettings => _settings.Clone();

    /// <summary>
    /// Gets a value indicating whether sign-in startup is enabled.
    /// </summary>
    public bool IsStartupEnabled => _startupService.IsEnabled;

    /// <summary>
    /// Loads configuration and starts the tray, display, scheduling, and recovery services.
    /// </summary>
    /// <param name="hidden">
    /// <see langword="true" /> to suppress the first-run settings window; otherwise, <see langword="false" />.
    /// </param>
    public void Initialize(bool hidden)
    {
        var loadResult = _settingsService.Load();
        _settings = loadResult.Settings;

        _tray?.Dispose();
        _tray = new TrayIconService(Toggle, ShowSettings, Exit);
        SubscribeSystemEvents();

        try
        {
            _gammaService.OpenDisplays();
        }
        catch (Exception exception) when (IsDisplayException(exception))
        {
            ReportGammaError(exception.Message);
        }

        // Begin from a known neutral ramp, then let the normal state-change path
        // animate into the scheduled night state using the configured transition.
        TryResetGamma();
        UpdateSchedule(false);
        _scheduleTimer.Start();
        _driftTimer.Start();

        if (loadResult.IsFirstRun)
        {
            try
            {
                _settingsService.Save(_settings);
            }
            catch (Exception exception) when (IsSettingsException(exception))
            {
                loadResult = loadResult with
                {
                    Warning = $"Default settings could not be saved beside NightEmber.exe.\n\n{exception.Message}"
                };
            }
        }

        if (!hidden && loadResult.IsFirstRun)
        {
            ShowSettings();
        }

        if (loadResult.Warning is not null)
        {
            System.Windows.MessageBox.Show(
                loadResult.Warning,
                "Night Ember",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Opens, restores, or activates the settings window.
    /// </summary>
    public void ShowSettings()
    {
        if (IsExiting)
        {
            return;
        }

        if (_window is not null)
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }

            _window.Show();
            _window.Activate();
            return;
        }

        _window = new MainWindow(this);
        _window.Closed += (_, _) =>
        {
            _window = null;
            if (Debugger.IsAttached)
            {
                Exit();
            }
        };
        _window.Show();
        _window.Activate();
    }

    /// <summary>
    /// Gets today's estimated sunrise and sunset information.
    /// </summary>
    /// <returns>The current solar estimate and its location source.</returns>
    public static SolarSummary GetSolarSummary()
    {
        return ScheduleService.GetSolarSummary(DateTime.Now);
    }

    /// <summary>
    /// Temporarily applies unsaved temperature and brightness values.
    /// </summary>
    /// <param name="temperature">The preview color temperature in Kelvin.</param>
    /// <param name="brightness">The preview software brightness percentage.</param>
    /// <remarks>
    /// A preview owns the gamma ramp until <see cref="CancelPreview" /> or
    /// <see cref="SaveSettings" /> restores normal scheduling.
    /// </remarks>
    public void Preview(int temperature, int brightness)
    {
        if (IsExiting)
        {
            return;
        }

        _previewTemperature = temperature;
        _previewBrightness = brightness;
        _previewing = true;
        StopFade();

        if (GammaStatesMatch(_currentKelvin, _currentBrightness, temperature, brightness))
        {
            return;
        }

        ApplyGamma(temperature, brightness);
    }

    /// <summary>
    /// Ends a live preview and restores the active saved state.
    /// </summary>
    public void CancelPreview()
    {
        if (!_previewing || IsExiting)
        {
            return;
        }

        _previewing = false;
        UpdateSchedule(true);
    }

    /// <summary>
    /// Validates, persists, and applies settings, then updates sign-in startup.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="enableStartup">Whether to launch Night Ember at sign-in.</param>
    /// <returns>
    /// A warning when settings were saved but startup could not be updated;
    /// otherwise, <see langword="null" />.
    /// </returns>
    public string? SaveSettings(AppSettings settings, bool enableStartup)
    {
        var normalized = settings.Normalize();
        _settingsService.Save(normalized);

        _settings = normalized;
        _manualOverride = null;
        _previewing = false;
        UpdateSchedule(true);

        try
        {
            _startupService.SetEnabled(enableStartup);
            return null;
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or System.Security.SecurityException
                                              or InvalidOperationException
                                              or System.Runtime.InteropServices.COMException)
        {
            return $"Settings were saved, but sign-in startup could not be updated.\n\n{exception.Message}";
        }
    }

    public static (bool DesiredState, bool? ManualOverride) ResolveScheduleState(
        bool? manualOverride,
        bool? lastScheduledState,
        bool scheduledState)
    {
        if (manualOverride is not null && lastScheduledState is not null && scheduledState != lastScheduledState)
        {
            manualOverride = null;
        }

        return (manualOverride ?? scheduledState, manualOverride);
    }

    /// <summary>
    /// Calculates clamped fade progress from monotonic elapsed time.
    /// </summary>
    /// <param name="elapsed">The elapsed fade time.</param>
    /// <param name="duration">The configured fade duration.</param>
    /// <returns>A progress value from zero through one.</returns>
    public static double CalculateFadeProgress(TimeSpan elapsed, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 1;
        }

        return Math.Clamp(elapsed / duration, 0, 1);
    }

    public static (double Kelvin, double Brightness) InterpolateGammaState(
        double startKelvin,
        double startBrightness,
        double targetKelvin,
        double targetBrightness,
        double progress)
    {
        progress = Math.Clamp(progress, 0, 1);

        return (startKelvin + (targetKelvin - startKelvin) * progress,
            startBrightness + (targetBrightness - startBrightness) * progress);
    }

    /// <summary>
    /// Determines whether two gamma states are equivalent within the application tolerance.
    /// </summary>
    /// <param name="firstKelvin">The first color temperature in Kelvin.</param>
    /// <param name="firstBrightness">The first brightness percentage.</param>
    /// <param name="secondKelvin">The second color temperature in Kelvin.</param>
    /// <param name="secondBrightness">The second brightness percentage.</param>
    /// <returns><see langword="true" /> when both values are within tolerance; otherwise, <see langword="false" />.</returns>
    public static bool GammaStatesMatch(
        double firstKelvin,
        double firstBrightness,
        double secondKelvin,
        double secondBrightness)
    {
        return Math.Abs(firstKelvin - secondKelvin) < GammaComparisonTolerance
               && Math.Abs(firstBrightness - secondBrightness) < GammaComparisonTolerance;
    }

    private void Exit()
    {
        if (IsExiting)
        {
            return;
        }

        Volatile.Write(ref _exiting, 1);
        StopTimers();
        TryResetGamma();
        _shutdown();
    }

    private DispatcherTimer CreateTimer(TimeSpan interval, EventHandler handler)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = interval };
        timer.Tick += handler;
        return timer;
    }

    private void Toggle()
    {
        _manualOverride = !_isOn;
        _previewing = false;
        SetTintState(_manualOverride.Value, true);
    }

    private void UpdateSchedule(bool force)
    {
        if (_previewing || IsExiting)
        {
            return;
        }

        var scheduled = ScheduleService.ShouldBeOn(_settings, DateTime.Now);
        var resolution = ResolveScheduleState(_manualOverride, _lastScheduledState, scheduled);
        _manualOverride = resolution.ManualOverride;
        _lastScheduledState = scheduled;
        if (force || resolution.DesiredState != _isOn)
        {
            SetTintState(resolution.DesiredState, !force);
        }
        else
        {
            UpdateTray();
        }
    }

    private void SetTintState(bool isOn, bool fade)
    {
        StopFade();
        _isOn = isOn;
        UpdateTray();

        var targetKelvin = isOn ? _settings.Temperature : ColorTemperature.NeutralKelvin;
        var targetBrightness = isOn ? (double)_settings.Brightness : NeutralBrightnessPercent;
        if (!fade
            || _settings.FadeMs <= 0
            || GammaStatesMatch(_currentKelvin, _currentBrightness, targetKelvin, targetBrightness))
        {
            ApplyGamma(targetKelvin, targetBrightness);
            return;
        }

        var startKelvin = _currentKelvin;
        var startBrightness = _currentBrightness;
        var fadeDuration = TimeSpan.FromMilliseconds(_settings.FadeMs);
        var fadeStopwatch = Stopwatch.StartNew();

        _fadeTimer = CreateTimer(
            TimeSpan.FromMilliseconds(FadeIntervalMilliseconds),
            (_, _) =>
            {
                var progress = CalculateFadeProgress(fadeStopwatch.Elapsed, fadeDuration);
                if (progress >= 1)
                {
                    ApplyGamma(targetKelvin, targetBrightness);
                    StopFade();
                    return;
                }

                var state = InterpolateGammaState(
                    startKelvin,
                    startBrightness,
                    targetKelvin,
                    targetBrightness,
                    progress);

                ApplyGamma(state.Kelvin, state.Brightness);
            });
        _fadeTimer.Start();
    }

    private void ApplyGamma(double kelvin, double brightness)
    {
        try
        {
            if (!_gammaService.Apply(kelvin, brightness))
            {
                ReportGammaError("The display driver rejected the requested gamma ramp.");
                return;
            }

            _currentKelvin = kelvin;
            _currentBrightness = brightness;
            _neutralRestored = false;
        }
        catch (Exception exception) when (IsDisplayException(exception))
        {
            ReportGammaError(exception.Message);
            StopFade();
        }
    }

    private void RepairGammaDrift()
    {
        if (!_isOn || _previewing || _fadeTimer is not null || IsExiting)
        {
            return;
        }

        try
        {
            _gammaService.RepairDrift();
        }
        catch (Exception exception) when (IsDisplayException(exception))
        {
            ReportGammaError(exception.Message);
        }
    }

    private void ScheduleDisplayReapply()
    {
        if (IsExiting)
        {
            return;
        }

#pragma warning disable VSTHRD001, VSTHRD110 // WPF dispatcher scheduling is intentionally fire-and-forget.
        _dispatcher.BeginInvoke(() =>
        {
            _reapplyTimer.Stop();
            _reapplyTimer.Start();
        });
#pragma warning restore VSTHRD001, VSTHRD110
    }

    private void ReapplyDisplays()
    {
        _reapplyTimer.Stop();
        if (IsExiting)
        {
            return;
        }

        try
        {
            _gammaService.OpenDisplays();

            if (_previewing)
            {
                ApplyGamma(_previewTemperature, _previewBrightness);
            }
            else
            {
                UpdateSchedule(true);
            }
        }
        catch (Exception exception) when (IsDisplayException(exception))
        {
            ReportGammaError(exception.Message);
        }
    }

    private void UpdateTray()
    {
        if (_tray is null)
        {
            return;
        }

        var state = _isOn ? $"Night Ember on - {_settings.Temperature}K" : "Night Ember off";
        var next = ScheduleService.GetNextChange(_settings, DateTime.Now);
        var time = next?.ToString(
                       CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern,
                       CultureInfo.CurrentCulture)
                   ?? string.Empty;
        var verb = _isOn ? "off" : "on";

        string reason;
        if (_manualOverride is not null)
        {
            reason = next is null ? "Manual" : $"Manual until {time}";
        }
        else
        {
            reason = _settings.Mode switch
            {
                ScheduleMode.Sunset when next is not null => $"Sun schedule: {verb} {time}",
                ScheduleMode.Sunset => "Sunset to sunrise",
                ScheduleMode.Custom when next is not null => $"Set hours: {verb} {time}",
                ScheduleMode.Custom => "Set hours",
                _ => "Manual only"
            };
        }

        var tooltip = $"{state}\n{reason}";
        _tray.Update(_isOn, tooltip);
    }

    private void ReportGammaError(string message)
    {
        if (_tray is null || !_gammaErrorThrottle.ShouldAllow(message, DateTime.UtcNow))
        {
            return;
        }

        _tray.ShowError(message);
    }

    private void StopFade()
    {
        _fadeTimer?.Stop();
        _fadeTimer = null;
    }

    private void StopTimers()
    {
        _scheduleTimer.Stop();
        _driftTimer.Stop();
        _reapplyTimer.Stop();
        StopFade();
    }

    private void TryResetGamma()
    {
        try
        {
            if (!_gammaService.Reset())
            {
                GammaService.ResetAllDisplays();
            }
        }
        catch (Exception exception) when (IsDisplayException(exception))
        {
            GammaService.ResetAllDisplays();
        }
        finally
        {
            // Repeated ramp writes make some drivers re-composite the desktop,
            // so shutdown paths skip a reset that already happened.
            _neutralRestored = true;
        }
    }

    private void SubscribeSystemEvents()
    {
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    private void UnsubscribeSystemEvents()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.SessionEnding -= OnSessionEnding;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        ScheduleDisplayReapply();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            ScheduleDisplayReapply();
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            ScheduleDisplayReapply();
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            ResetForSessionEnding();
            return;
        }

#pragma warning disable VSTHRD001 // Session-ending cleanup must synchronously reach the WPF dispatcher.
        try
        {
            _dispatcher.Invoke(ResetForSessionEnding);
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException)
        {
            // The dispatcher is already shutting down, so restore the displays directly.
            GammaService.ResetAllDisplays();
        }
#pragma warning restore VSTHRD001
    }

    private void ResetForSessionEnding()
    {
        Volatile.Write(ref _exiting, 1);
        StopTimers();
        TryResetGamma();
    }

    private static bool IsDisplayException(Exception exception)
    {
        return exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ObjectDisposedException;
    }

    private bool IsExiting => Volatile.Read(ref _exiting) != 0;

    private static bool IsSettingsException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    }
}
