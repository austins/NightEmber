using NightEmber.Models;
using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class ScheduleServiceTests
{
    private static readonly SolarLocation Equator = new(0, 0, "Test");

    [Fact]
    public void ShouldBeOn_ManualMode_IsAlwaysOff()
    {
        // Arrange
        var settings = new AppSettings { Mode = ScheduleMode.Manual };
        var now = LocalDateTime(2026, 3, 20, 22);

        // Act
        var isOn = ScheduleService.ShouldBeOn(settings, now);
        var nextChange = ScheduleService.GetNextChange(settings, now);

        // Assert
        isOn.Should().BeFalse();
        nextChange.Should().BeNull();
    }

    [Fact]
    public void ScheduleMethods_UnknownMode_HasNoSchedule()
    {
        // Arrange
        var settings = new AppSettings { Mode = (ScheduleMode)999 };
        var now = LocalDateTime(2026, 3, 20, 22);

        // Act
        var isOn = ScheduleService.ShouldBeOn(settings, now);
        var nextChange = ScheduleService.GetNextChange(settings, now);

        // Assert
        isOn.Should().BeFalse();
        nextChange.Should().BeNull();
    }

    [Theory]
    [InlineData(20, 59, false)]
    [InlineData(21, 0, true)]
    [InlineData(23, 59, true)]
    [InlineData(0, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(12, 0, false)]
    public void ShouldBeOn_OvernightCustomWindow_UsesInclusiveStartExclusiveEnd(
        int hour,
        int minute,
        bool expected)
    {
        // Arrange
        var settings = CustomSettings("21:00", "07:00");
        var now = LocalDateTime(2026, 4, 10, hour, minute);

        // Act
        var result = ScheduleService.ShouldBeOn(settings, now);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(7, 59, false)]
    [InlineData(8, 0, true)]
    [InlineData(11, 59, true)]
    [InlineData(12, 0, false)]
    [InlineData(20, 0, false)]
    public void ShouldBeOn_SameDayCustomWindow_UsesInclusiveStartExclusiveEnd(
        int hour,
        int minute,
        bool expected)
    {
        // Arrange
        var settings = CustomSettings("08:00", "12:00");
        var now = LocalDateTime(2026, 4, 10, hour, minute);

        // Act
        var result = ScheduleService.ShouldBeOn(settings, now);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("invalid", "07:00")]
    [InlineData("21:00", "invalid")]
    [InlineData("07:00", "07:00")]
    public void CustomSchedule_InvalidOrEqualBoundaries_HasNoSchedule(string on, string off)
    {
        // Arrange
        var settings = CustomSettings(on, off);
        var now = LocalDateTime(2026, 4, 10, 22);

        // Act
        var isOn = ScheduleService.ShouldBeOn(settings, now);
        var nextChange = ScheduleService.GetNextChange(settings, now);

        // Assert
        isOn.Should().BeFalse();
        nextChange.Should().BeNull();
    }

    [Theory]
    [InlineData(6, 0, 7, 0, 0)]
    [InlineData(7, 0, 21, 0, 0)]
    [InlineData(20, 59, 21, 0, 0)]
    [InlineData(21, 0, 7, 0, 1)]
    [InlineData(23, 0, 7, 0, 1)]
    public void GetNextChange_OvernightCustomWindow_ReturnsNextBoundary(
        int nowHour,
        int nowMinute,
        int expectedHour,
        int expectedMinute,
        int expectedDayOffset)
    {
        // Arrange
        var settings = CustomSettings("21:00", "07:00");
        var now = LocalDateTime(2026, 4, 10, nowHour, nowMinute);
        var expected = now.Date
            .AddDays(expectedDayOffset)
            .AddHours(expectedHour)
            .AddMinutes(expectedMinute);

        // Act
        var next = ScheduleService.GetNextChange(settings, now);

        // Assert
        next.Should().Be(expected);
    }

    [Theory]
    [InlineData(6, 0, 8, 0, 0)]
    [InlineData(8, 0, 12, 0, 0)]
    [InlineData(9, 0, 12, 0, 0)]
    [InlineData(12, 0, 8, 0, 1)]
    [InlineData(13, 0, 8, 0, 1)]
    public void GetNextChange_SameDayCustomWindow_ReturnsNextBoundary(
        int nowHour,
        int nowMinute,
        int expectedHour,
        int expectedMinute,
        int expectedDayOffset)
    {
        // Arrange
        var settings = CustomSettings("08:00", "12:00");
        var now = LocalDateTime(2026, 4, 10, nowHour, nowMinute);
        var expected = now.Date
            .AddDays(expectedDayOffset)
            .AddHours(expectedHour)
            .AddMinutes(expectedMinute);

        // Act
        var next = ScheduleService.GetNextChange(settings, now);

        // Assert
        next.Should().Be(expected);
    }

    [Fact]
    public void SolarSchedule_AtCalculatedSunriseAndSunset_Transitions()
    {
        // Arrange
        var settings = new AppSettings { Mode = ScheduleMode.Sunset };
        var date = LocalDateTime(2026, 3, 20);
        var sun = SolarCalculator.GetSunTimes(Equator, date, TimeZoneInfo.Utc);
        var sunrise = sun.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var sunset = sun.Sunset ?? throw new InvalidOperationException("Expected sunset.");

        // Act
        var beforeSunrise = ScheduleService.ShouldBeOn(
            settings,
            sunrise.AddTicks(-1),
            Equator,
            TimeZoneInfo.Utc);
        var atSunrise = ScheduleService.ShouldBeOn(settings, sunrise, Equator, TimeZoneInfo.Utc);
        var beforeSunset = ScheduleService.ShouldBeOn(
            settings,
            sunset.AddTicks(-1),
            Equator,
            TimeZoneInfo.Utc);
        var atSunset = ScheduleService.ShouldBeOn(settings, sunset, Equator, TimeZoneInfo.Utc);

        // Assert
        beforeSunrise.Should().BeTrue();
        atSunrise.Should().BeFalse();
        beforeSunset.Should().BeFalse();
        atSunset.Should().BeTrue();
    }

    [Fact]
    public void GetNextChange_SolarSchedule_ReturnsTodayThenTomorrowBoundaries()
    {
        // Arrange
        var settings = new AppSettings { Mode = ScheduleMode.Sunset };
        var date = LocalDateTime(2026, 3, 20);
        var today = SolarCalculator.GetSunTimes(Equator, date, TimeZoneInfo.Utc);
        var sunrise = today.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var sunset = today.Sunset ?? throw new InvalidOperationException("Expected sunset.");
        var tomorrowSunrise = SolarCalculator.GetSunTimes(Equator, date.AddDays(1), TimeZoneInfo.Utc).Sunrise;

        // Act
        var beforeSunrise = ScheduleService.GetNextChange(
            settings,
            sunrise.AddMinutes(-1),
            Equator,
            TimeZoneInfo.Utc);
        var afterSunrise = ScheduleService.GetNextChange(
            settings,
            sunrise.AddMinutes(1),
            Equator,
            TimeZoneInfo.Utc);
        var afterSunset = ScheduleService.GetNextChange(
            settings,
            sunset.AddMinutes(1),
            Equator,
            TimeZoneInfo.Utc);

        // Assert
        beforeSunrise.Should().Be(sunrise);
        afterSunrise.Should().Be(sunset);
        afterSunset.Should().Be(tomorrowSunrise);
    }

    [Fact]
    public void SolarSchedule_PolarDateWithoutCrossings_HasNoSchedule()
    {
        // Arrange
        var settings = new AppSettings { Mode = ScheduleMode.Sunset };
        var location = new SolarLocation(90, 0, "North Pole");
        var now = LocalDateTime(2026, 6, 21, 12);

        // Act
        var isOn = ScheduleService.ShouldBeOn(settings, now, location, TimeZoneInfo.Utc);
        var nextChange = ScheduleService.GetNextChange(settings, now, location, TimeZoneInfo.Utc);

        // Assert
        isOn.Should().BeFalse();
        nextChange.Should().BeNull();
    }

    [Fact]
    public void SolarSchedule_PolarNightWithoutCrossings_HasNoSchedule()
    {
        // Arrange
        var settings = new AppSettings { Mode = ScheduleMode.Sunset };
        var location = new SolarLocation(90, 0, "North Pole");
        var now = LocalDateTime(2026, 12, 21, 12);

        // Act
        var isOn = ScheduleService.ShouldBeOn(settings, now, location, TimeZoneInfo.Utc);
        var nextChange = ScheduleService.GetNextChange(settings, now, location, TimeZoneInfo.Utc);

        // Assert
        isOn.Should().BeFalse();
        nextChange.Should().BeNull();
    }

    private static AppSettings CustomSettings(string on, string off)
    {
        return new AppSettings { Mode = ScheduleMode.Custom, CustomOn = on, CustomOff = off };
    }

    private static DateTime LocalDateTime(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }
}
