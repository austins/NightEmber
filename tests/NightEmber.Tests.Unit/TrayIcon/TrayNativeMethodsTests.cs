using NightEmber.TrayIcon;
using System.Runtime.InteropServices;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayNativeMethodsTests
{
    [Fact]
    public void NotifyIconIdentifier_NativeLayout_MatchesShellIdentifier()
    {
        // Arrange
        var is64Bit = nint.Size == 8;

        // Act
        var size = Marshal.SizeOf<TrayNativeMethods.NotifyIconIdentifier>();
        var windowOffset = Marshal.OffsetOf<TrayNativeMethods.NotifyIconIdentifier>(
            nameof(TrayNativeMethods.NotifyIconIdentifier.Window));
        var iconOffset = Marshal.OffsetOf<TrayNativeMethods.NotifyIconIdentifier>(
            nameof(TrayNativeMethods.NotifyIconIdentifier.IconId));
        var idOffset = Marshal.OffsetOf<TrayNativeMethods.NotifyIconIdentifier>(
            nameof(TrayNativeMethods.NotifyIconIdentifier.Id));

        // Assert
        size.Should().Be(is64Bit ? 40 : 28);
        windowOffset.Should().Be(is64Bit ? 8 : 4);
        iconOffset.Should().Be(is64Bit ? 16 : 8);
        idOffset.Should().Be(is64Bit ? 20 : 12);
    }

    [Fact]
    public void NativeRectangle_NativeLayout_MatchesRect()
    {
        // Act
        var size = Marshal.SizeOf<TrayNativeMethods.NativeRectangle>();
        var topOffset = Marshal.OffsetOf<TrayNativeMethods.NativeRectangle>(
            nameof(TrayNativeMethods.NativeRectangle.Top));
        var rightOffset = Marshal.OffsetOf<TrayNativeMethods.NativeRectangle>(
            nameof(TrayNativeMethods.NativeRectangle.Right));
        var bottomOffset = Marshal.OffsetOf<TrayNativeMethods.NativeRectangle>(
            nameof(TrayNativeMethods.NativeRectangle.Bottom));

        // Assert
        size.Should().Be(16);
        topOffset.Should().Be(4);
        rightOffset.Should().Be(8);
        bottomOffset.Should().Be(12);
    }
}
