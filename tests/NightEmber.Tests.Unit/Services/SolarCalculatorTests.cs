using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class SolarCalculatorTests
{
    [Fact]
    public void GetTimeZoneLocation_KnownZone_UsesRepresentativeLatitudeAndOffsetLongitude()
    {
        // Arrange
        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Eastern Standard Time",
            TimeSpan.FromHours(-5),
            "Eastern Standard Time",
            "Eastern Standard Time");

        // Act
        var result = SolarCalculator.GetTimeZoneLocation(timeZone);

        // Assert
        result.Latitude.Should().Be(40);
        result.Longitude.Should().Be(-75);
        result.Source.Should().Be("Eastern Standard Time");
    }

    [Fact]
    public void GetTimeZoneLocation_UnknownZone_UsesClampedEquatorialEstimate()
    {
        // Arrange
        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Unknown Test Zone",
            TimeSpan.FromHours(14),
            "Unknown Test Zone",
            "Unknown Test Zone");

        // Act
        var result = SolarCalculator.GetTimeZoneLocation(timeZone);

        // Assert
        result.Latitude.Should().Be(0);
        result.Longitude.Should().Be(180);
        result.Source.Should().Be("Unknown Test Zone (equatorial estimate)");
    }

    [Fact]
    public void GetSunTimes_EquatorAtEquinox_ReturnsExpectedApproximateTimes()
    {
        // Arrange
        var date = CalendarDate(2026, 3, 20);

        // Act
        var result = SolarCalculator.GetSunTimes(new SolarLocation(0, 0, "Equator"), date, TimeZoneInfo.Utc);
        var sunrise = result.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var sunset = result.Sunset ?? throw new InvalidOperationException("Expected sunset.");

        // Assert
        sunrise.Date.Should().Be(date);
        sunset.Date.Should().Be(date);
        sunrise.TimeOfDay.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(5));
        sunrise.TimeOfDay.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(7));
        sunset.TimeOfDay.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(17));
        sunset.TimeOfDay.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(19));
        sunset.Should().BeAfter(sunrise);
    }

    [Fact]
    public void GetSunTimes_NorthernLatitude_HasLongerSummerDayThanWinterDay()
    {
        // Arrange
        var location = new SolarLocation(40, -75, "Test");
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");

        // Act
        var summer = SolarCalculator.GetSunTimes(location, CalendarDate(2026, 6, 21), timeZone);
        var winter = SolarCalculator.GetSunTimes(location, CalendarDate(2026, 12, 21), timeZone);
        var summerDayLength = DayLength(summer);
        var winterDayLength = DayLength(winter);

        // Assert
        summerDayLength.Should().BeGreaterThan(winterDayLength);
    }

    [Fact]
    public void GetSunTimes_SouthernLatitude_HasLongerDecemberDayThanJuneDay()
    {
        // Arrange
        var location = new SolarLocation(-34, 151, "Test");
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+10", TimeSpan.FromHours(10), "UTC+10", "UTC+10");

        // Act
        var june = SolarCalculator.GetSunTimes(location, CalendarDate(2026, 6, 21), timeZone);
        var december = SolarCalculator.GetSunTimes(location, CalendarDate(2026, 12, 21), timeZone);
        var juneDayLength = DayLength(june);
        var decemberDayLength = DayLength(december);

        // Assert
        decemberDayLength.Should().BeGreaterThan(juneDayLength);
    }

    [Theory]
    [InlineData(90, 6, 21)]
    [InlineData(90, 12, 21)]
    [InlineData(-90, 6, 21)]
    [InlineData(-90, 12, 21)]
    public void GetSunTimes_PolarDateWithoutHorizonCrossing_ReturnsNulls(
        double latitude,
        int month,
        int day)
    {
        // Act
        var result = SolarCalculator.GetSunTimes(
            new SolarLocation(latitude, 0, "Pole"),
            CalendarDate(2026, month, day),
            TimeZoneInfo.Utc);

        // Assert
        result.Sunrise.Should().BeNull();
        result.Sunset.Should().BeNull();
    }

    [Fact]
    public void GetSunTimes_AlignedLongitudeAndUtcOffset_PreservesApproximateWallClockTime()
    {
        // Arrange
        var date = CalendarDate(2026, 4, 15);
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+2",
            TimeSpan.FromHours(2),
            "UTC+2",
            "UTC+2");

        // Act
        var utcResult = SolarCalculator.GetSunTimes(new SolarLocation(0, 0, "UTC"), date, TimeZoneInfo.Utc);
        var shiftedResult = SolarCalculator.GetSunTimes(new SolarLocation(0, 30, "UTC+2"), date, plusTwo);
        var utcSunrise = utcResult.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var shiftedSunrise = shiftedResult.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var utcSunset = utcResult.Sunset ?? throw new InvalidOperationException("Expected sunset.");
        var shiftedSunset = shiftedResult.Sunset ?? throw new InvalidOperationException("Expected sunset.");
        var sunriseDifference = (shiftedSunrise.TimeOfDay - utcSunrise.TimeOfDay).Duration();
        var sunsetDifference = (shiftedSunset.TimeOfDay - utcSunset.TimeOfDay).Duration();

        // Assert
        sunriseDifference.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        sunsetDifference.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GetSunTimes_RequestedTimeZone_ChangesWallClockResultWithoutChangingDate()
    {
        // Arrange
        var date = CalendarDate(2026, 3, 20);
        var location = new SolarLocation(0, 0, "Test");
        var plusFive = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+5",
            TimeSpan.FromHours(5),
            "UTC+5",
            "UTC+5");

        // Act
        var utcResult = SolarCalculator.GetSunTimes(location, date, TimeZoneInfo.Utc);
        var shiftedResult = SolarCalculator.GetSunTimes(location, date, plusFive);
        var utcSunrise = utcResult.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var shiftedSunrise = shiftedResult.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var difference = shiftedSunrise - utcSunrise;

        // Assert
        shiftedSunrise.Date.Should().Be(date);
        difference.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void GetSunTimes_DaylightSavingZone_AppliesOffsetForCalculatedDate()
    {
        // Arrange
        var location = new SolarLocation(40, -75, "Test");
        var standardZone = TimeZoneInfo.CreateCustomTimeZone(
            "Fixed UTC-5",
            TimeSpan.FromHours(-5),
            "Fixed UTC-5",
            "Fixed UTC-5");
        var daylightTransition = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified),
            3,
            2,
            DayOfWeek.Sunday);
        var standardTransition = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified),
            11,
            1,
            DayOfWeek.Sunday);
        var adjustmentRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            CalendarDate(2020, 1, 1),
            CalendarDate(2030, 12, 31),
            TimeSpan.FromHours(1),
            daylightTransition,
            standardTransition);
        var daylightZone = TimeZoneInfo.CreateCustomTimeZone(
            "Test Eastern",
            TimeSpan.FromHours(-5),
            "Test Eastern",
            "Test Eastern Standard",
            "Test Eastern Daylight",
            [adjustmentRule]);
        var summerDate = CalendarDate(2026, 6, 21);
        var winterDate = CalendarDate(2026, 1, 15);

        // Act
        var summerStandard = SolarCalculator.GetSunTimes(location, summerDate, standardZone);
        var summerDaylight = SolarCalculator.GetSunTimes(location, summerDate, daylightZone);
        var winterStandard = SolarCalculator.GetSunTimes(location, winterDate, standardZone);
        var winterDaylight = SolarCalculator.GetSunTimes(location, winterDate, daylightZone);

        // Assert
        summerDaylight.Sunrise.Should().Be(summerStandard.Sunrise?.AddHours(1));
        summerDaylight.Sunset.Should().Be(summerStandard.Sunset?.AddHours(1));
        winterDaylight.Should().Be(winterStandard);
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(180)]
    public void GetSunTimes_DateLineLongitude_NormalizesResultToRequestedDate(double longitude)
    {
        // Arrange
        var date = CalendarDate(2026, 1, 1);

        // Act
        var result = SolarCalculator.GetSunTimes(
            new SolarLocation(0, longitude, "Date line"),
            date,
            TimeZoneInfo.Utc);

        // Assert
        result.Sunrise.Should().NotBeNull();
        result.Sunset.Should().NotBeNull();
        result.Sunrise.Value.Date.Should().Be(date);
        result.Sunset.Value.Date.Should().Be(date);
    }

    private static TimeSpan DayLength(SunTimes times)
    {
        var sunrise = times.Sunrise ?? throw new InvalidOperationException("Expected sunrise.");
        var sunset = times.Sunset ?? throw new InvalidOperationException("Expected sunset.");
        return sunset - sunrise;
    }

    private static DateTime CalendarDate(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }
}
