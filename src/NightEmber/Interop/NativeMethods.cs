using NightEmber.Display;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NightEmber.Interop;

/// <summary>
/// Declares the Windows interop functions and native layouts used for displays and tray icons.
/// </summary>
internal static partial class NativeMethods
{
    private const int GammaRampElementCount = GammaRampBuilder.RampLength * GammaRampBuilder.ChannelCount;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal delegate bool MonitorEnumCallback(nint monitor, nint deviceContext, nint rectangle, nint data);

    /// <summary>
    /// Invokes a callback for each monitor intersecting the supplied clipping region.
    /// </summary>
    /// <param name="deviceContext">The clipping device context, or zero to enumerate across the virtual screen.</param>
    /// <param name="clipRectangle">A pointer to a clipping rectangle, or zero for no additional clipping.</param>
    /// <param name="callback">The callback, which returns false to stop enumeration.</param>
    /// <param name="data">Application data passed unchanged to the callback.</param>
    /// <returns><see langword="true" /> when enumeration completes successfully.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumCallback callback,
        nint data);

    /// <summary>
    /// Reads a monitor's bounds, work area, flags, and GDI device name.
    /// </summary>
    /// <param name="monitor">The monitor handle supplied by monitor enumeration.</param>
    /// <param name="info">The output structure, with its size initialized before the call.</param>
    /// <returns><see langword="true" /> when monitor information was retrieved.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    /// <summary>
    /// Creates a GDI device context for a device.
    /// </summary>
    /// <param name="driver">The driver name; use "DISPLAY" for display devices.</param>
    /// <param name="device">The device name, or null for the default device.</param>
    /// <param name="output">The output device parameter, which should be null.</param>
    /// <param name="initData">A pointer to device initialization data, or zero for defaults.</param>
    /// <returns>A device-context handle to release with <see cref="DeleteDc" />, or zero on failure.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport(
        "gdi32.dll",
        EntryPoint = "CreateDCW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateDc(string? driver, string? device, string? output, nint initData);

    /// <summary>
    /// Deletes a GDI device context created by <see cref="CreateDc" />.
    /// </summary>
    /// <param name="deviceContext">The device-context handle to release.</param>
    /// <returns><see langword="true" /> when the context was deleted.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDc(nint deviceContext);

    /// <summary>
    /// Sets the gamma ramp for a display device context.
    /// </summary>
    /// <param name="deviceContext">The display device-context handle.</param>
    /// <param name="ramp">The 768 values, ordered as 256 red, 256 green, and 256 blue entries.</param>
    /// <returns><see langword="true" /> when the native write succeeds.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetDeviceGammaRamp(
        nint deviceContext,
        [In] [MarshalUsing(ConstantElementCount = GammaRampElementCount)] ushort[] ramp);

    /// <summary>
    /// Reads the gamma ramp for a display device context.
    /// </summary>
    /// <param name="deviceContext">The display device-context handle.</param>
    /// <param name="ramp">A buffer for 256 red, 256 green, and 256 blue entries, in that order.</param>
    /// <returns><see langword="true" /> when the buffer contains a successfully read ramp.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetDeviceGammaRamp(
        nint deviceContext,
        [Out] [MarshalUsing(ConstantElementCount = GammaRampElementCount)] ushort[] ramp);

    /// <summary>
    /// Destroys an owned native icon and releases its resources.
    /// </summary>
    /// <param name="icon">The handle of a nonshared icon owned by the caller.</param>
    /// <returns><see langword="true" /> when the icon was destroyed.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    /// <summary>
    /// Retrieves the screen-pixel bounds of a notification-area icon.
    /// </summary>
    /// <param name="identifier">The initialized shell icon identifier.</param>
    /// <param name="rectangle">The icon's bounds when the lookup succeeds.</param>
    /// <returns>An HRESULT indicating success or failure.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    public static partial int ShellNotifyIconGetRect(in NotifyIconIdentifier identifier, out NativeRectangle rectangle);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NotifyIconIdentifier
    {
        public uint Size;
        public nint Window;
        public uint IconId;
        public Guid Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public uint Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;
        public MonitorDeviceName DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // MONITORINFOEXW contains 32 inline UTF-16 code units, not a string pointer.
    [InlineArray(32)]
    internal struct MonitorDeviceName
    {
#pragma warning disable S1144 // The compiler uses this field as the inline array's backing storage.
        private ushort _element0;
#pragma warning restore S1144
    }
}
