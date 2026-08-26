using System.Runtime.InteropServices;

namespace NightEmber.Services;

/// <summary>
/// Owns display device contexts and applies GDI gamma ramps to every active monitor.
/// </summary>
internal sealed class GammaService : IDisposable
{
    private readonly List<DisplayContext> _displays = [];
    private ushort? _expectedBlue;
    private double _lastKelvin = ColorTemperature.NeutralKelvin;
    private double _lastBrightness = 100;
    private bool _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseDisplays();
        _disposed = true;
    }

    /// <summary>
    /// Rebuilds the device-context collection from the currently connected displays.
    /// </summary>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// Thrown when Windows provides no usable display device contexts.
    /// </exception>
    public void OpenDisplays()
    {
        ThrowIfDisposed();
        CloseDisplays();

        var lastError = 0;
        foreach (var deviceName in Screen.AllScreens.Select(static screen => screen.DeviceName))
        {
            var deviceContext = NativeMethods.CreateDc("DISPLAY", deviceName, null, 0);
            if (deviceContext == 0)
            {
                lastError = Marshal.GetLastPInvokeError();
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

    /// <summary>
    /// Applies a color temperature and software brightness to every display.
    /// </summary>
    /// <param name="kelvin">The requested color temperature in Kelvin.</param>
    /// <param name="brightnessPercent">The requested software brightness percentage.</param>
    /// <returns><see langword="true" /> when every display accepted the ramp.</returns>
    /// <remarks>
    /// Stale display handles are rebuilt and retried once. Windows rejects ramps
    /// below half of linear, so generated channel multipliers are clamped to that floor.
    /// </remarks>
    public bool Apply(double kelvin, double brightnessPercent)
    {
        ThrowIfDisposed();
        EnsureDisplays();

        var rgb = ColorTemperature.ToRgb(kelvin);
        var brightness = Math.Clamp(brightnessPercent / 100.0, 0.5, 1.0);
        var ramp = GammaRampBuilder.Build(rgb.Red, rgb.Green, rgb.Blue, brightness, true);

        if (ApplyToAll(ramp))
        {
            RememberRamp(ramp, kelvin, brightnessPercent);
            return true;
        }

        OpenDisplays();
        var applied = ApplyToAll(ramp);
        if (applied)
        {
            RememberRamp(ramp, kelvin, brightnessPercent);
        }

        return applied;
    }

    /// <summary>
    /// Applies an identity ramp to every tracked display.
    /// </summary>
    /// <returns><see langword="true" /> when every display accepted the reset.</returns>
    public bool Reset()
    {
        ThrowIfDisposed();
        EnsureDisplays();

        var ramp = GammaRampBuilder.Build(1, 1, 1, 1, false);
        var applied = ApplyToAll(ramp);
        if (applied)
        {
            RememberRamp(ramp, ColorTemperature.NeutralKelvin, 100);
        }

        return applied;
    }

    /// <summary>
    /// Detects whether another component replaced the active ramp and reapplies it.
    /// </summary>
    /// <remarks>
    /// Only the midpoint of the blue channel is read because a reset changes it by
    /// thousands of units while ordinary driver rounding differs by only a few.
    /// </remarks>
    public void RepairDrift()
    {
        ThrowIfDisposed();
        if (_expectedBlue is null || _displays.Count == 0)
        {
            return;
        }

        var readback = new ushort[GammaRampBuilder.RampLength * 3];
        foreach (var display in _displays)
        {
            if (!NativeMethods.GetDeviceGammaRamp(display.Handle, readback))
            {
                continue;
            }

            var actualBlue = readback[2 * GammaRampBuilder.RampLength + 128];
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
        var ramp = GammaRampBuilder.Build(1, 1, 1, 1, false);
        foreach (var deviceName in Screen.AllScreens.Select(static screen => screen.DeviceName))
        {
            var deviceContext = NativeMethods.CreateDc("DISPLAY", deviceName, null, 0);
            if (deviceContext == 0)
            {
                continue;
            }

            try
            {
                // Some display drivers intermittently ignore the first identity ramp,
                // so the watchdog recovery path repeats the reset once.
                NativeMethods.SetDeviceGammaRamp(deviceContext, ramp);
                NativeMethods.SetDeviceGammaRamp(deviceContext, ramp);
            }
            finally
            {
                NativeMethods.DeleteDc(deviceContext);
            }
        }
    }

    public static bool HasDrifted(ushort expected, ushort actual)
    {
        return Math.Abs(actual - expected) > 256;
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
            var result = NativeMethods.SetDeviceGammaRamp(displayHandle, ramp);
            if (!result)
            {
                result = NativeMethods.SetDeviceGammaRamp(displayHandle, ramp);
            }

            allApplied &= result;
        }

        return allApplied;
    }

    private void RememberRamp(ushort[] ramp, double kelvin, double brightnessPercent)
    {
        _expectedBlue = ramp[2 * GammaRampBuilder.RampLength + 128];
        _lastKelvin = kelvin;
        _lastBrightness = brightnessPercent;
    }

    private void CloseDisplays()
    {
        foreach (var display in _displays)
        {
            NativeMethods.DeleteDc(display.Handle);
        }

        _displays.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record DisplayContext(nint Handle);
}
