using NightEmber.Models;

namespace NightEmber.Tests.Unit;

public sealed class MainWindowTests
{
    [Theory]
    [InlineData(3400, 58, 3400)]
    [InlineData(3400, 58.4, 3400)]
    [InlineData(3400, 58.5, 3400)]
    [InlineData(3400, 58.6, 3373)]
    [InlineData(6500, -1, 6500)]
    [InlineData(1200, 101, 1200)]
    public void BuildSettings_Strength_PreservesUnchangedTemperatureOrRoundsAndClamps(
        int original,
        double strength,
        int expected)
    {
        // Arrange
        var off = TimeSpan.FromHours(7);

        // Act
        var settings = MainWindow.BuildSettings(original, strength, 80, ScheduleMode.Manual, TimeSpan.Zero, off, 300);

        // Assert
        settings.Temperature.Should().Be(expected);
    }

    [Theory]
    [InlineData((int)ScheduleMode.Manual, 80.5, 80)]
    [InlineData((int)ScheduleMode.Custom, 81.5, 82)]
    [InlineData((int)ScheduleMode.Sunset, 99.6, 100)]
    public void BuildSettings_SaveValues_PreservesModeFadeAndPortableTimes(
        int mode,
        double brightness,
        int expectedBrightness)
    {
        // Arrange
        var customOn = new TimeSpan(21, 35, 0);
        var customOff = new TimeSpan(7, 5, 0);

        // Act
        var settings = MainWindow.BuildSettings(3400, 58, brightness, (ScheduleMode)mode, customOn, customOff, 1234);

        // Assert
        settings.Brightness.Should().Be(expectedBrightness);
        settings.Mode.Should().Be((ScheduleMode)mode);
        settings.CustomOn.Should().Be("21:35");
        settings.CustomOff.Should().Be("07:05");
        settings.FadeMs.Should().Be(1234);
    }

    [Theory]
    [InlineData(6500, 0)]
    [InlineData(3400, 58)]
    [InlineData(1200, 100)]
    public void TemperatureToStrength_KnownTemperature_ReturnsExpectedStrength(int temperature, int expected)
    {
        // Act
        var result = MainWindow.TemperatureToStrength(temperature);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, 6500)]
    [InlineData(0, 6500)]
    [InlineData(1, 6447)]
    [InlineData(50, 3850)]
    [InlineData(99, 1253)]
    [InlineData(100, 1200)]
    [InlineData(101, 1200)]
    public void StrengthToTemperature_Strength_ClampsAndConvertsTemperature(int strength, int expected)
    {
        // Act
        var result = MainWindow.StrengthToTemperature(strength);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 21, 7, true)]
    [InlineData(true, 0, 1, true)]
    [InlineData(true, 7, 7, false)]
    [InlineData(false, 7, 7, true)]
    public void IsCustomWindowValid_ScheduleTimes_DetectsZeroLengthWindow(
        bool isCustomMode,
        int onHours,
        int offHours,
        bool expected)
    {
        // Act
        var result = MainWindow.IsCustomWindowValid(
            isCustomMode,
            TimeSpan.FromHours(onHours),
            TimeSpan.FromHours(offHours));

        // Assert
        result.Should().Be(expected);
    }
}
