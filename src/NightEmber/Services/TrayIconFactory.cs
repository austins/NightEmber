using System.Drawing.Drawing2D;

namespace NightEmber.Services;

internal static class TrayIconFactory
{
    /// <summary>
    /// Creates a crescent-moon icon for the requested application state.
    /// </summary>
    /// <param name="isOn">Whether to use the warm active-state color.</param>
    /// <returns>A caller-owned icon with an independent native handle.</returns>
    public static Icon Create(bool isOn)
    {
        const int iconSize = 32;
        const int scale = 4;
        const int canvasSize = iconSize * scale;
        var color = isOn ? Color.FromArgb(255, 168, 60) : Color.FromArgb(150, 160, 175);

        using var canvas = new Bitmap(canvasSize, canvasSize);
        using (var graphics = Graphics.FromImage(canvas))
        using (SolidBrush brush = new(color))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            graphics.FillEllipse(brush, 3 * scale, 3 * scale, 26 * scale, 26 * scale);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            using var transparent = new SolidBrush(Color.Transparent);
            graphics.FillEllipse(transparent, 12 * scale, -1 * scale, 26 * scale, 26 * scale);
        }

        using var result = new Bitmap(iconSize, iconSize);
        using (var graphics = Graphics.FromImage(result))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImage(canvas, 0, 0, iconSize, iconSize);
        }

        var nativeIcon = result.GetHicon();
        try
        {
            using var handleIcon = Icon.FromHandle(nativeIcon);
            return (Icon)handleIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(nativeIcon);
        }
    }
}
