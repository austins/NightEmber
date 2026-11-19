using Avalonia;
using Avalonia.Media.Imaging;
using NightEmber.TrayIcon;
using System.Runtime.InteropServices;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayIconFactoryTests
{
    private const int BytesPerPixel = 4;

    [Theory]
    [InlineData(true, 255, 168, 60)]
    [InlineData(false, 150, 160, 175)]
    public async Task Render_State_ReturnsTransparentMoonWithExpectedColor(bool isOn, int red, int green, int blue)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Act
            using var bitmap = TrayIconFactory.Render(isOn);
            var pixels = ReadPixels(bitmap);

            // Assert
            bitmap.PixelSize.Should().Be(new PixelSize(32, 32));
            PixelAt(pixels, bitmap.PixelSize.Width, 7, 16).Should().Equal((byte)blue, (byte)green, (byte)red, 255);
            PixelAt(pixels, bitmap.PixelSize.Width, 0, 0)[3].Should().Be(0);
            PixelAt(pixels, bitmap.PixelSize.Width, 25, 12)[3].Should().Be(0);
        });
    }

    [Fact]
    public async Task Create_State_ReturnsIndependentIcons()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Act
            var on = TrayIconFactory.Create(true);
            var off = TrayIconFactory.Create(false);

            // Assert
            on.Should().NotBeNull();
            off.Should().NotBeSameAs(on);
        });
    }

    private static byte[] ReadPixels(RenderTargetBitmap bitmap)
    {
        var stride = bitmap.PixelSize.Width * BytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelSize.Height];
        var buffer = Marshal.AllocHGlobal(pixels.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), buffer, pixels.Length, stride);
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return pixels;
    }

    // Bgra8888 order: blue, green, red, alpha.
    private static byte[] PixelAt(byte[] pixels, int width, int x, int y)
    {
        return pixels[((y * width + x) * BytesPerPixel)..][..BytesPerPixel];
    }
}
