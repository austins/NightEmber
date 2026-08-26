using NightEmber.Models;

namespace NightEmber.Tests.Unit.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void Default_WhenAccessed_ReturnsSafeDefaultsAndASeparateInstance()
    {
        // Act
        var first = AppSettings.Default;
        var second = AppSettings.Default;

        // Assert
        first.Should().BeEquivalentTo(
            new AppSettings
            {
                Temperature = 3400,
                Brightness = 100,
                Mode = ScheduleMode.Sunset,
                CustomOn = "21:00",
                CustomOff = "07:00",
                FadeMs = 300
            });
        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void Clone_PopulatedSettings_CopiesEveryValueToASeparateInstance()
    {
        // Arrange
        var settings = new AppSettings
        {
            Temperature = 2200,
            Brightness = 75,
            Mode = ScheduleMode.Custom,
            CustomOn = "18:45",
            CustomOff = "06:15",
            FadeMs = 1200
        };

        // Act
        var clone = settings.Clone();

        // Assert
        clone.Should().BeEquivalentTo(settings);
        clone.Should().NotBeSameAs(settings);
    }

    [Theory]
    [InlineData(1200, 50, 0)]
    [InlineData(6500, 100, 5000)]
    public void Normalize_BoundaryValues_PreservesValues(int temperature, int brightness, int fadeMs)
    {
        // Arrange
        var settings = new AppSettings
        {
            Temperature = temperature,
            Brightness = brightness,
            Mode = ScheduleMode.Manual,
            CustomOn = "00:00",
            CustomOff = "23:59",
            FadeMs = fadeMs
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.Should().BeEquivalentTo(settings);
    }

    [Theory]
    [InlineData(1199, 49, -1)]
    [InlineData(6501, 101, 5001)]
    [InlineData(int.MinValue, int.MinValue, int.MinValue)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    public void Normalize_OutOfRangeNumericValues_UsesDefaults(int temperature, int brightness, int fadeMs)
    {
        // Arrange
        var settings = new AppSettings
        {
            Temperature = temperature,
            Brightness = brightness,
            FadeMs = fadeMs
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.Temperature.Should().Be(3400);
        normalized.Brightness.Should().Be(100);
        normalized.FadeMs.Should().Be(300);
    }

    [Fact]
    public void Normalize_InvalidModeAndTimes_UsesDefaults()
    {
        // Arrange
        var settings = new AppSettings
        {
            Mode = (ScheduleMode)999,
            CustomOn = "24:00",
            CustomOff = "not a time"
        };

        // Act
        var normalized = settings.Normalize();

        // Assert
        normalized.Mode.Should().Be(ScheduleMode.Sunset);
        normalized.CustomOn.Should().Be("21:00");
        normalized.CustomOff.Should().Be("07:00");
    }

    [Theory]
    [InlineData("00:00", 0, 0)]
    [InlineData("07:05", 7, 5)]
    [InlineData("12:30", 12, 30)]
    [InlineData("23:59", 23, 59)]
    public void TryParseTime_ValidPortableTime_ReturnsTime(string value, int hours, int minutes)
    {
        // Act
        var parsed = AppSettings.TryParseTime(value, out var result);

        // Assert
        parsed.Should().BeTrue();
        result.Should().Be(new TimeSpan(hours, minutes, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7:00")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("00:00:00")]
    [InlineData(" 07:00")]
    [InlineData("07:00 ")]
    public void TryParseTime_InvalidPortableTime_ReturnsFalse(string? value)
    {
        // Act
        var parsed = AppSettings.TryParseTime(value, out var result);

        // Assert
        parsed.Should().BeFalse();
        result.Should().Be(default);
    }

    [Theory]
    [InlineData(0, 0, "00:00")]
    [InlineData(7, 5, "07:05")]
    [InlineData(23, 59, "23:59")]
    [InlineData(24, 0, "00:00")]
    [InlineData(-1, 0, "23:00")]
    public void FormatTime_TimeSpan_FormatsAndWrapsTimeOfDay(int hours, int minutes, string expected)
    {
        // Arrange
        var time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);

        // Act
        var result = AppSettings.FormatTime(time);

        // Assert
        result.Should().Be(expected);
    }
}
