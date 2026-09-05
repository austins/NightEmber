using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace NightEmber.Interop;

/// <summary>
/// Retrieves GDI display names without creating UI objects, including in the watchdog.
/// </summary>
internal static class MonitorEnumerator
{
    internal delegate bool GetMonitorInfoCallback(nint monitor, ref NativeMethods.MonitorInfo info);

    /// <summary>
    /// Retrieves the GDI device names of all display monitors returned by Windows.
    /// </summary>
    /// <returns>A nonempty list of display names suitable for opening GDI device contexts.</returns>
    /// <exception cref="Win32Exception">
    /// Enumeration fails, monitor information is unavailable, a device name is invalid, or no monitors are returned.
    /// </exception>
    public static IReadOnlyList<string> GetDeviceNames()
    {
        return GetDeviceNames(
            static callback => NativeMethods.EnumDisplayMonitors(0, 0, callback, 0),
            NativeMethods.GetMonitorInfo);
    }

    internal static IReadOnlyList<string> GetDeviceNames(
        Func<NativeMethods.MonitorEnumCallback, bool> enumerate,
        GetMonitorInfoCallback getMonitorInfo)
    {
        var names = new List<string>();
        ExceptionDispatchInfo? callbackError = null;
        var enumerated = enumerate((monitor, _, _, _) =>
        {
            // Never unwind a managed exception through the native enumeration frame.
            try
            {
                var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
                if (!getMonitorInfo(monitor, ref info))
                {
                    throw new Win32Exception(
                        Marshal.GetLastPInvokeError(),
                        "Windows could not read monitor information.");
                }

                var deviceName = MemoryMarshal.Cast<ushort, char>(info.DeviceName);
                var terminator = deviceName.IndexOf('\0');
                if (terminator <= 0)
                {
                    throw new Win32Exception("Windows did not provide a valid monitor device name.");
                }

                names.Add(new string(deviceName[..terminator]));
                return true;
            }
            catch (Exception exception)
            {
                callbackError = ExceptionDispatchInfo.Capture(exception);
                return false;
            }
        });
        var lastError = Marshal.GetLastPInvokeError();
        callbackError?.Throw();
        if (!enumerated)
        {
            throw new Win32Exception(lastError, "Windows could not enumerate display monitors.");
        }

        if (names.Count == 0)
        {
            throw new Win32Exception("Windows did not provide any display monitors.");
        }

        return names;
    }
}
