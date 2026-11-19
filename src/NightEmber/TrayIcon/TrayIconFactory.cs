using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace NightEmber.TrayIcon;

internal static class TrayIconFactory
{
    private const int IconSize = 32;

    /// <summary>
    /// Creates a crescent-moon icon for the requested application state.
    /// </summary>
    /// <param name="isOn">Whether to use the warm active-state color.</param>
    /// <returns>An icon that the platform copies from the rendered image.</returns>
    public static WindowIcon Create(bool isOn)
    {
        using var bitmap = Render(isOn);
        return new WindowIcon(bitmap);
    }

    /// <summary>
    /// Renders the crescent-moon image for the requested application state.
    /// </summary>
    /// <param name="isOn">Whether to use the warm active-state color.</param>
    /// <returns>A caller-owned transparent bitmap with the moon drawn in the state color.</returns>
    internal static RenderTargetBitmap Render(bool isOn)
    {
        var color = isOn ? Color.FromRgb(255, 168, 60) : Color.FromRgb(150, 160, 175);
        var moon = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new EllipseGeometry(new Rect(3, 3, 26, 26)),
            new EllipseGeometry(new Rect(12, -1, 26, 26)));

        var bitmap = new RenderTargetBitmap(new PixelSize(IconSize, IconSize));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawGeometry(new ImmutableSolidColorBrush(color), null, moon);
        }

        return bitmap;
    }
}
