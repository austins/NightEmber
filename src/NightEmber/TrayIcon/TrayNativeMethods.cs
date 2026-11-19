using System.Runtime.InteropServices;

namespace NightEmber.TrayIcon;

/// <summary>
/// Declares the Windows interop functions and native layouts used for tray icons.
/// </summary>
internal static partial class TrayNativeMethods
{
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
    internal struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
