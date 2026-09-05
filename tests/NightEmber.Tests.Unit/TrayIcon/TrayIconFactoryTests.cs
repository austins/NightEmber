using NightEmber.TrayIcon;
using System.Drawing;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayIconFactoryTests
{
    [Theory]
    [InlineData(true, 255, 168, 60)]
    [InlineData(false, 150, 160, 175)]
    public void Create_State_ReturnsTransparentMoonWithExpectedColor(bool isOn, int red, int green, int blue)
    {
        // Act
        using var icon = TrayIconFactory.Create(isOn);
        using var bitmap = icon.ToBitmap();

        // Assert
        icon.Size.Should().Be(new Size(32, 32));
        icon.Handle.Should().NotBe(nint.Zero);
        bitmap.GetPixel(7, 16).ToArgb().Should().Be(Color.FromArgb(red, green, blue).ToArgb());
        bitmap.GetPixel(0, 0).A.Should().Be(0);
        bitmap.GetPixel(25, 12).A.Should().Be(0);
    }

    [Fact]
    public void Create_MultipleIcons_HaveIndependentLifetimes()
    {
        // Arrange
        using var second = TrayIconFactory.Create(true);
        nint firstHandle;

        // Act
        using (var first = TrayIconFactory.Create(true))
        {
            firstHandle = first.Handle;
        }

        using var bitmap = second.ToBitmap();

        // Assert
        firstHandle.Should().NotBe(second.Handle);
        bitmap.GetPixel(7, 16).ToArgb().Should().Be(Color.FromArgb(255, 168, 60).ToArgb());
    }
}
