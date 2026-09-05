using NightEmber.Interop;
using System.Runtime.InteropServices;

namespace NightEmber.Display;

/// <summary>
/// Provides native monitor enumeration and GDI gamma-ramp operations.
/// </summary>
internal sealed class GammaDeviceApi : IGammaDeviceApi
{
    public IEnumerable<string> GetDeviceNames()
    {
        return MonitorEnumerator.GetDeviceNames();
    }

    public nint Open(string deviceName)
    {
        return NativeMethods.CreateDc("DISPLAY", deviceName, null, 0);
    }

    public int GetLastError()
    {
        return Marshal.GetLastPInvokeError();
    }

    public bool Write(nint handle, ushort[] ramp)
    {
        return NativeMethods.SetDeviceGammaRamp(handle, ramp);
    }

    public bool Read(nint handle, ushort[] ramp)
    {
        return NativeMethods.GetDeviceGammaRamp(handle, ramp);
    }

    public void Close(nint handle)
    {
        NativeMethods.DeleteDc(handle);
    }
}

/// <summary>
/// Abstracts display discovery, device-context ownership, and gamma-ramp access.
/// </summary>
internal interface IGammaDeviceApi
{
    /// <summary>
    /// Retrieves the GDI device names of the currently connected display monitors.
    /// </summary>
    /// <returns>The display names accepted by <see cref="Open" />.</returns>
    public IEnumerable<string> GetDeviceNames();

    /// <summary>
    /// Opens a device context for a display.
    /// </summary>
    /// <param name="deviceName">The GDI display device name.</param>
    /// <returns>A handle to close with <see cref="Close" />, or zero when opening fails.</returns>
    public nint Open(string deviceName);

    /// <summary>
    /// Gets the native error code captured by the last interop call on the current thread.
    /// </summary>
    /// <returns>The captured Windows error code.</returns>
    public int GetLastError();

    /// <summary>
    /// Writes a gamma ramp to a display device context.
    /// </summary>
    /// <param name="handle">An open display device-context handle.</param>
    /// <param name="ramp">The 768 values, ordered as 256 red, 256 green, and 256 blue entries.</param>
    /// <returns><see langword="true" /> when the native write succeeds.</returns>
    public bool Write(nint handle, ushort[] ramp);

    /// <summary>
    /// Reads a display's current gamma ramp into the supplied buffer.
    /// </summary>
    /// <param name="handle">An open display device-context handle.</param>
    /// <param name="ramp">A buffer for 256 red, 256 green, and 256 blue entries, in that order.</param>
    /// <returns><see langword="true" /> when the buffer contains a successfully read ramp.</returns>
    public bool Read(nint handle, ushort[] ramp);

    /// <summary>
    /// Releases a display device context opened by <see cref="Open" />.
    /// </summary>
    /// <param name="handle">The device-context handle to release.</param>
    public void Close(nint handle);
}
