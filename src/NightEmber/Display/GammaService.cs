namespace NightEmber.Display;

/// <summary>
/// Owns display device contexts and applies GDI gamma ramps to every active monitor.
/// </summary>
internal sealed class GammaService : IGammaService
{
    private const int PercentageScale = 100;
    private const int RampMidpointIndex = GammaRampBuilder.RampLength / 2;

    // Driver rounding can alter ramp values slightly; a reset changes the midpoint
    // by thousands, so one 8-bit step is a conservative drift threshold.
    private const int DriftTolerance = 256;

    private readonly List<DisplayContext> _displays = [];
    private readonly IGammaDeviceApi _devices;
    private readonly ushort[] _rampBuffer = new ushort[GammaRampBuilder.RampElementCount];
    private readonly ushort[] _readbackBuffer = new ushort[GammaRampBuilder.RampElementCount];
    private ushort? _expectedBlue;
    private double _lastKelvin = ColorTemperature.NeutralKelvin;
    private double _lastBrightness = 100;
    private bool _disposed;

    /// <summary>
    /// Initializes a gamma service backed by the native Windows display APIs.
    /// </summary>
    public GammaService()
        : this(new GammaDeviceApi())
    {
    }

    internal GammaService(IGammaDeviceApi devices)
    {
        _devices = devices;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseDisplays();
        _disposed = true;
    }

    public void OpenDisplays()
    {
        ThrowIfDisposed();
        CloseDisplays();

        var lastError = 0;
        foreach (var deviceName in _devices.GetDeviceNames())
        {
            var deviceContext = _devices.Open(deviceName);
            if (deviceContext == 0)
            {
                lastError = _devices.GetLastError();
                continue;
            }

            _displays.Add(new DisplayContext(deviceContext));
        }

        if (_displays.Count == 0)
        {
            const string message = "Windows did not provide a display device context.";
            throw lastError == 0
                ? new System.ComponentModel.Win32Exception(message)
                : new System.ComponentModel.Win32Exception(lastError, message);
        }
    }

    public bool Apply(double kelvin, double brightnessPercent)
    {
        ThrowIfDisposed();
        EnsureDisplays();

        var rgb = ColorTemperature.ToRgb(kelvin);
        var brightness = Math.Clamp(brightnessPercent / PercentageScale, GammaRampBuilder.DriverMinimumMultiplier, 1.0);
        GammaRampBuilder.BuildInto(_rampBuffer, rgb.Red, rgb.Green, rgb.Blue, brightness, true);

        if (ApplyToAll(_rampBuffer))
        {
            RememberRamp(_rampBuffer, kelvin, brightnessPercent);
            return true;
        }

        OpenDisplays();

        var applied = ApplyToAll(_rampBuffer);
        if (applied)
        {
            RememberRamp(_rampBuffer, kelvin, brightnessPercent);
        }

        return applied;
    }

    public bool Reset()
    {
        ThrowIfDisposed();
        EnsureDisplays();

        GammaRampBuilder.BuildInto(_rampBuffer, 1, 1, 1, 1, false);
        var applied = ApplyToAll(_rampBuffer);
        if (applied)
        {
            RememberRamp(_rampBuffer, ColorTemperature.NeutralKelvin, 100);
        }

        return applied;
    }

    public void RepairDrift()
    {
        ThrowIfDisposed();
        if (_expectedBlue is null || _displays.Count == 0)
        {
            return;
        }

        foreach (var display in _displays)
        {
            if (!_devices.Read(display.Handle, _readbackBuffer))
            {
                continue;
            }

            var actualBlue = _readbackBuffer[2 * GammaRampBuilder.RampLength + RampMidpointIndex];
            if (HasDrifted(_expectedBlue.Value, actualBlue))
            {
                Apply(_lastKelvin, _lastBrightness);
                return;
            }
        }
    }

    /// <summary>
    /// Opens each current display independently and attempts to restore an identity ramp.
    /// </summary>
    /// <remarks>
    /// This static recovery path is usable by the watchdog after the main process,
    /// and therefore its tracked display handles, no longer exist.
    /// </remarks>
    public static void ResetAllDisplays()
    {
        ResetAllDisplays(new GammaDeviceApi());
    }

    /// <summary>
    /// Determines whether a blue-channel midpoint differs by more than the drift tolerance.
    /// </summary>
    /// <param name="expected">The midpoint of the last successfully applied ramp.</param>
    /// <param name="actual">The midpoint read from the display.</param>
    /// <returns><see langword="true" /> when the absolute difference exceeds 256 ramp units.</returns>
    public static bool HasDrifted(ushort expected, ushort actual)
    {
        return Math.Abs(actual - expected) > DriftTolerance;
    }

    void IGammaService.ResetAll()
    {
        ResetAllDisplays(_devices);
    }

    internal static void ResetAllDisplays(IGammaDeviceApi devices)
    {
        var ramp = GammaRampBuilder.Build(1, 1, 1, 1, false);
        foreach (var deviceName in devices.GetDeviceNames())
        {
            var deviceContext = devices.Open(deviceName);
            if (deviceContext == 0)
            {
                continue;
            }

            try
            {
                // Some display drivers intermittently ignore the first identity ramp,
                // so the watchdog recovery path repeats the reset once.
                devices.Write(deviceContext, ramp);
                devices.Write(deviceContext, ramp);
            }
            finally
            {
                devices.Close(deviceContext);
            }
        }
    }

    private void EnsureDisplays()
    {
        if (_displays.Count == 0)
        {
            OpenDisplays();
        }
    }

    private bool ApplyToAll(ushort[] ramp)
    {
        var allApplied = _displays.Count > 0;
        foreach (var displayHandle in _displays.Select(static display => display.Handle))
        {
            var result = _devices.Write(displayHandle, ramp);
            if (!result)
            {
                result = _devices.Write(displayHandle, ramp);
            }

            allApplied &= result;
        }

        return allApplied;
    }

    private void RememberRamp(ushort[] ramp, double kelvin, double brightnessPercent)
    {
        _expectedBlue = ramp[2 * GammaRampBuilder.RampLength + RampMidpointIndex];
        _lastKelvin = kelvin;
        _lastBrightness = brightnessPercent;
    }

    private void CloseDisplays()
    {
        foreach (var display in _displays)
        {
            _devices.Close(display.Handle);
        }

        _displays.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record DisplayContext(nint Handle);
}

/// <summary>
/// Manages display gamma ramps, drift repair, and recovery to a neutral display state.
/// </summary>
internal interface IGammaService : IDisposable
{
    /// <summary>
    /// Rebuilds the device-context collection from the currently connected displays.
    /// </summary>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// Thrown when Windows provides no usable display device contexts.
    /// </exception>
    public void OpenDisplays();

    /// <summary>
    /// Applies a color temperature and software brightness to every display.
    /// </summary>
    /// <param name="kelvin">The requested color temperature in Kelvin.</param>
    /// <param name="brightnessPercent">The requested software brightness percentage.</param>
    /// <returns><see langword="true" /> when every display accepted the ramp.</returns>
    /// <remarks>
    /// Stale display handles are rebuilt and retried once. Generated channel multipliers
    /// are clamped to a driver-safe floor of half of linear.
    /// </remarks>
    public bool Apply(double kelvin, double brightnessPercent);

    /// <summary>
    /// Applies an identity ramp to every tracked display.
    /// </summary>
    /// <returns><see langword="true" /> when every display accepted the reset.</returns>
    public bool Reset();

    /// <summary>
    /// Detects whether another component replaced the active ramp and reapplies it.
    /// </summary>
    /// <remarks>
    /// Only the midpoint of the blue channel is read because a reset changes it by
    /// thousands of units while ordinary driver rounding differs by only a few.
    /// </remarks>
    public void RepairDrift();

    /// <summary>
    /// Opens each current display independently and attempts to restore an identity ramp.
    /// </summary>
    /// <remarks>
    /// Recovery does not depend on tracked display handles. It writes the identity ramp
    /// twice per usable display and releases each newly opened handle afterward.
    /// </remarks>
    public void ResetAll();
}
