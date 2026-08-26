namespace NightEmber.Tests.Unit;

public sealed class MainWindowTests
{
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
