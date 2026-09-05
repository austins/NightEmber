using H.NotifyIcon.Core;
using NightEmber.Interop;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace NightEmber.TrayIcon;

/// <summary>
/// Locates a tray popup at its shell icon rather than at an unrelated mouse position.
/// </summary>
internal static class TrayIconPosition
{
    /// <summary>
    /// Gets the icon's lower-left corner in the coordinates expected by H.NotifyIcon's WPF popup.
    /// </summary>
    /// <param name="id">The shell icon identifier.</param>
    /// <returns>The popup anchor, falling back to the notification area if the icon cannot be located.</returns>
    public static Point GetMenuPosition(Guid id)
    {
        var identifier = new NativeMethods.NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.NotifyIconIdentifier>(),
            Id = id
        };
        var result = NativeMethods.ShellNotifyIconGetRect(in identifier, out var rectangle);
        if (result < 0)
        {
            Trace.TraceWarning(
                "Could not locate the tray icon (HRESULT 0x{0:X8}); using the notification area.",
                result);
        }

        var point = result >= 0 ? new Point(rectangle.Left, rectangle.Bottom) : TrayInfo.GetTrayLocation();

        // Match H.NotifyIcon's physical-pixel to WPF desktop-coordinate conversion.
        using var source = new HwndSource(default);
        var transform = source.CompositionTarget.TransformFromDevice;
        var position = transform.Transform(new System.Windows.Point(point.X, point.Y));
        return new Point((int)position.X, (int)position.Y);
    }
}
