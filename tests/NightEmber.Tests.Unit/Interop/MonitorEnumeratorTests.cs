using NightEmber.Interop;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NightEmber.Tests.Unit.Interop;

public sealed class MonitorEnumeratorTests
{
    [Fact]
    public void GetDeviceNames_MultipleMonitors_ReturnsNamesInEnumerationOrder()
    {
        // Arrange
        var sizes = new List<uint>();

        // Act
        var result = MonitorEnumerator.GetDeviceNames(
            static callback => callback(1, 0, 0, 0) && callback(2, 0, 0, 0),
            (monitor, ref info) =>
            {
                sizes.Add(info.Size);
                var name = monitor == 1 ? @"\\.\DISPLAY1" : @"\\.\DISPLAY2";
                WriteDeviceName(ref info, name);
                return true;
            });

        // Assert
        sizes.Should().Equal(104, 104);
        result.Should().Equal(@"\\.\DISPLAY1", @"\\.\DISPLAY2");
    }

    [Fact]
    public void GetDeviceNames_MaximumTerminatedName_PreservesAllCharacters()
    {
        // Arrange
        var name = new string('a', 31);

        // Act
        var result = MonitorEnumerator.GetDeviceNames(
            static callback => callback(1, 0, 0, 0),
            (_, ref info) =>
            {
                WriteDeviceName(ref info, name);
                return true;
            });

        // Assert
        result.Should().Equal(name);
    }

    [Fact]
    public void GetDeviceNames_DataAfterTerminator_IgnoresUnusedBuffer()
    {
        // Act
        var result = MonitorEnumerator.GetDeviceNames(
            static callback => callback(1, 0, 0, 0),
            static (_, ref info) =>
            {
                WriteDeviceName(ref info, "\\\\.\\DISPLAY1\0unused");
                return true;
            });

        // Assert
        result.Should().Equal(@"\\.\DISPLAY1");
    }

    [Fact]
    public void GetDeviceNames_EnumerationFailure_ThrowsNativeError()
    {
        // Act
        var action = () => MonitorEnumerator.GetDeviceNames(
            static _ =>
            {
                Marshal.SetLastPInvokeError(5);
                return false;
            },
            static (_, ref _) => throw new InvalidOperationException());

        // Assert
        action.Should().Throw<Win32Exception>().Which.NativeErrorCode.Should().Be(5);
    }

    [Fact]
    public void GetDeviceNames_InfoFailure_PreservesErrorAcrossNativeCallback()
    {
        // Arrange
        bool? callbackResult = null;

        // Act
        var action = () => MonitorEnumerator.GetDeviceNames(
            callback =>
            {
                var result = callback(1, 0, 0, 0);
                callbackResult = result;
                Marshal.SetLastPInvokeError(0);
                return result;
            },
            static (_, ref _) =>
            {
                Marshal.SetLastPInvokeError(6);
                return false;
            });

        // Assert
        action.Should().Throw<Win32Exception>().Which.NativeErrorCode.Should().Be(6);
        callbackResult.Should().BeFalse();
    }

    [Fact]
    public void GetDeviceNames_CallbackThrows_RethrowsOutsideNativeCallback()
    {
        // Arrange
        var error = new InvalidOperationException("Monitor information failed.");
        bool? callbackResult = null;

        // Act
        var action = () => MonitorEnumerator.GetDeviceNames(
            callback =>
            {
                callbackResult = callback(1, 0, 0, 0);
                return false;
            },
            (_, ref _) => throw error);

        // Assert
        action.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(error);
        callbackResult.Should().BeFalse();
    }

    [Fact]
    public void GetDeviceNames_NoMonitors_ThrowsInsteadOfReturningEmptyList()
    {
        // Act
        var action = () => MonitorEnumerator.GetDeviceNames(
            static _ => true,
            static (_, ref _) => throw new InvalidOperationException());

        // Assert
        action.Should().Throw<Win32Exception>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345678901234567890123456789012")]
    public void GetDeviceNames_InvalidName_ThrowsInsteadOfReturningPartialList(string name)
    {
        // Act
        var action = () => MonitorEnumerator.GetDeviceNames(
            static callback => callback(1, 0, 0, 0) && callback(2, 0, 0, 0),
            (monitor, ref info) =>
            {
                WriteDeviceName(ref info, monitor == 1 ? @"\\.\DISPLAY1" : name);
                return true;
            });

        // Assert
        action.Should().Throw<Win32Exception>();
    }

    private static void WriteDeviceName(ref NativeMethods.MonitorInfo info, string name)
    {
        for (var index = 0; index < name.Length; index++)
        {
            info.DeviceName[index] = name[index];
        }
    }
}
