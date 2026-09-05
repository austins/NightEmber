using NightEmber.Interop;
using System.Runtime.InteropServices;

namespace NightEmber.Tests.Unit.Interop;

public sealed class NativeMethodsTests
{
    [Fact]
    public void MonitorInfo_NativeLayout_MatchesMonitorInfoExW()
    {
        // Act
        var size = Marshal.SizeOf<NativeMethods.MonitorInfo>();
        var monitorOffset = Marshal.OffsetOf<NativeMethods.MonitorInfo>(nameof(NativeMethods.MonitorInfo.MonitorArea));
        var workOffset = Marshal.OffsetOf<NativeMethods.MonitorInfo>(nameof(NativeMethods.MonitorInfo.WorkArea));
        var flagsOffset = Marshal.OffsetOf<NativeMethods.MonitorInfo>(nameof(NativeMethods.MonitorInfo.Flags));
        var nameOffset = Marshal.OffsetOf<NativeMethods.MonitorInfo>(nameof(NativeMethods.MonitorInfo.DeviceName));

        // Assert
        size.Should().Be(104);
        monitorOffset.Should().Be(4);
        workOffset.Should().Be(20);
        flagsOffset.Should().Be(36);
        nameOffset.Should().Be(40);
    }

    [Fact]
    public void NativeRectangle_NativeLayout_MatchesRect()
    {
        // Act
        var size = Marshal.SizeOf<NativeMethods.NativeRectangle>();
        var leftOffset = Marshal.OffsetOf<NativeMethods.NativeRectangle>(nameof(NativeMethods.NativeRectangle.Left));
        var topOffset = Marshal.OffsetOf<NativeMethods.NativeRectangle>(nameof(NativeMethods.NativeRectangle.Top));
        var rightOffset = Marshal.OffsetOf<NativeMethods.NativeRectangle>(nameof(NativeMethods.NativeRectangle.Right));
        var bottomOffset =
            Marshal.OffsetOf<NativeMethods.NativeRectangle>(nameof(NativeMethods.NativeRectangle.Bottom));

        // Assert
        size.Should().Be(16);
        leftOffset.Should().Be(nint.Zero);
        topOffset.Should().Be(4);
        rightOffset.Should().Be(8);
        bottomOffset.Should().Be(12);
    }
}
