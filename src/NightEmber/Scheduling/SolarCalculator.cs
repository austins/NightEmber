namespace NightEmber.Scheduling;

/// <summary>
/// Calculates local sunrise and sunset times using an offline solar algorithm.
/// </summary>
/// <remarks>
/// Windows time-zone metadata contains UTC offsets and daylight-saving rules,
/// but no geographic coordinates. Night Ember therefore uses representative
/// latitudes and derives longitude from the base UTC offset to provide a
/// permission-free estimate.
/// These coordinates are not the user's actual location. A single time zone can
/// cover a large area, the latitude table is not exhaustive, and an unknown zone
/// falls back to latitude zero at the equator. Accurate solar times require
/// explicit Windows location permission or user-provided coordinates.
/// </remarks>
internal static class SolarCalculator
{
    // Official sunrise/sunset zenith: 90 degrees plus atmospheric refraction
    // and the apparent radius of the sun near the horizon.
    private const double Zenith = 90.833;
    private const int DegreesPerHour = 15;
    private const int HoursPerDay = 24;
    private const int DegreesPerCircle = 360;
    private const int MaximumLongitudeDegrees = DegreesPerCircle / 2;
    private const int DegreesPerQuadrant = 90;
    private const int SunriseBaseHour = 6;
    private const int SunsetBaseHour = 18;

    // Coefficients from the U.S. Naval Observatory's Almanac for Computers
    // sunrise/sunset algorithm, as transcribed at:
    // https://edwilliams.org/sunrise_sunset_algorithm.htm
    private const double MeanAnomalyRate = 0.9856;
    private const double MeanAnomalyAdjustment = 3.289;
    private const double LongitudePrimaryCorrection = 1.916;
    private const double LongitudeSecondaryCorrection = 0.020;
    private const double LongitudeOffset = 282.634;
    private const double RightAscensionFactor = 0.91764;
    private const double DeclinationFactor = 0.39782;
    private const double LocalMeanTimeRate = 0.06571;
    private const double LocalMeanTimeOffset = 6.622;

    private static readonly IReadOnlyDictionary<string, double> TimeZoneLatitudes =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["Eastern Standard Time"] = 40.0,
            ["Central Standard Time"] = 41.9,
            ["Mountain Standard Time"] = 39.7,
            ["US Mountain Standard Time"] = 33.4,
            ["Pacific Standard Time"] = 37.8,
            ["Alaskan Standard Time"] = 61.2,
            ["Hawaiian Standard Time"] = 21.3,
            ["Atlantic Standard Time"] = 44.6,
            ["Newfoundland Standard Time"] = 47.6,
            ["GMT Standard Time"] = 51.5,
            ["Greenwich Standard Time"] = 53.3,
            ["W. Europe Standard Time"] = 52.4,
            ["Central Europe Standard Time"] = 50.1,
            ["Central European Standard Time"] = 52.2,
            ["Romance Standard Time"] = 48.9,
            ["E. Europe Standard Time"] = 44.4,
            ["FLE Standard Time"] = 60.2,
            ["GTB Standard Time"] = 38.0,
            ["Russian Standard Time"] = 55.8,
            ["Turkey Standard Time"] = 41.0,
            ["Israel Standard Time"] = 31.8,
            ["Arabian Standard Time"] = 25.3,
            ["India Standard Time"] = 28.6,
            ["China Standard Time"] = 31.2,
            ["Singapore Standard Time"] = 1.35,
            ["Tokyo Standard Time"] = 35.7,
            ["Korea Standard Time"] = 37.6,
            ["AUS Eastern Standard Time"] = -33.9,
            ["E. Australia Standard Time"] = -27.5,
            ["AUS Central Standard Time"] = -34.9,
            ["Cen. Australia Standard Time"] = -34.9,
            ["W. Australia Standard Time"] = -31.95,
            ["Tasmania Standard Time"] = -42.9,
            ["Lord Howe Standard Time"] = -31.55,
            ["New Zealand Standard Time"] = -41.3,
            ["Norfolk Standard Time"] = -29.0,
            ["Fiji Standard Time"] = -18.1,
            ["Tonga Standard Time"] = -21.1,
            ["Samoa Standard Time"] = -13.8,
            ["Easter Island Standard Time"] = -27.1,
            ["SA Pacific Standard Time"] = 4.7,
            ["E. South America Standard Time"] = -23.5,
            ["Central Brazilian Standard Time"] = -15.8,
            ["Bahia Standard Time"] = -12.9,
            ["Tocantins Standard Time"] = -10.2,
            ["Argentina Standard Time"] = -34.6,
            ["Pacific SA Standard Time"] = -33.4,
            ["Paraguay Standard Time"] = -25.3,
            ["Montevideo Standard Time"] = -34.9,
            ["Magallanes Standard Time"] = -53.2,
            ["South Africa Standard Time"] = -26.2,
            ["Egypt Standard Time"] = 30.0,
            ["Morocco Standard Time"] = 33.6
        };

    /// <summary>
    /// Produces an approximate location from the active Windows time zone.
    /// </summary>
    /// <returns>The representative latitude, derived longitude, and source label.</returns>
    /// <remarks>
    /// An unrecognized time zone uses latitude zero and is labeled as an equatorial estimate.
    /// </remarks>
    public static SolarLocation GetLocalTimeZoneLocation()
    {
        return GetTimeZoneLocation(TimeZoneInfo.Local);
    }

    /// <summary>
    /// Calculates sunrise and sunset for a location and local calendar date.
    /// </summary>
    /// <param name="location">The coordinates used for the calculation.</param>
    /// <param name="date">The local calendar date to calculate.</param>
    /// <returns>
    /// Sunrise and sunset values, or <see langword="null" /> values for polar days
    /// where the sun does not cross the horizon.
    /// </returns>
    public static SunTimes GetSunTimes(SolarLocation location, DateTime date)
    {
        return GetSunTimes(location, date, TimeZoneInfo.Local);
    }

    /// <summary>
    /// Calculates sunrise and sunset for a location and calendar date in an explicit time zone.
    /// </summary>
    /// <param name="location">The coordinates used for the calculation.</param>
    /// <param name="date">The calendar date in the supplied time zone.</param>
    /// <param name="timeZone">The time zone used to express the calculated event times.</param>
    /// <returns>Sunrise and sunset values, with null for any event where the sun does not cross the horizon.</returns>
    public static SunTimes GetSunTimes(SolarLocation location, DateTime date, TimeZoneInfo timeZone)
    {
        var sunrise = GetTime(location, date, SunriseBaseHour, true, timeZone);
        var sunset = GetTime(location, date, SunsetBaseHour, false, timeZone);

        return new SunTimes(sunrise, sunset);
    }

    /// <summary>
    /// Produces an approximate location from a time zone's representative latitude and standard UTC offset.
    /// </summary>
    /// <param name="timeZone">The time zone whose location should be estimated.</param>
    /// <returns>The representative latitude, derived longitude, and source label.</returns>
    /// <remarks>Unrecognized time zones use latitude zero and are labeled as equatorial estimates.</remarks>
    public static SolarLocation GetTimeZoneLocation(TimeZoneInfo timeZone)
    {
        var longitude = Math.Clamp(
            timeZone.BaseUtcOffset.TotalHours * DegreesPerHour,
            -MaximumLongitudeDegrees,
            MaximumLongitudeDegrees);

        var hasRepresentativeLatitude = TimeZoneLatitudes.TryGetValue(timeZone.Id, out var latitude);
        var source = hasRepresentativeLatitude ? timeZone.Id : $"{timeZone.Id} (equatorial estimate)";

        return new SolarLocation(latitude, longitude, source);
    }

    private static DateTime? GetTime(
        SolarLocation location,
        DateTime date,
        double baseHour,
        bool rising,
        TimeZoneInfo timeZone)
    {
        const double radians = Math.PI / 180.0;
        const double degrees = 180.0 / Math.PI;

        var longitudeHour = location.Longitude / DegreesPerHour;
        var approximateDay = date.DayOfYear + (baseHour - longitudeHour) / HoursPerDay;
        var meanAnomaly = MeanAnomalyRate * approximateDay - MeanAnomalyAdjustment;

        var trueLongitude = meanAnomaly
                            + LongitudePrimaryCorrection * Math.Sin(radians * meanAnomaly)
                            + LongitudeSecondaryCorrection * Math.Sin(radians * 2 * meanAnomaly)
                            + LongitudeOffset;

        trueLongitude = Normalize(trueLongitude, DegreesPerCircle);

        var rightAscension = degrees * Math.Atan(RightAscensionFactor * Math.Tan(radians * trueLongitude));
        rightAscension = Normalize(rightAscension, DegreesPerCircle);
        var longitudeQuadrant = Math.Floor(trueLongitude / DegreesPerQuadrant) * DegreesPerQuadrant;
        var rightAscensionQuadrant = Math.Floor(rightAscension / DegreesPerQuadrant) * DegreesPerQuadrant;
        rightAscension = (rightAscension + longitudeQuadrant - rightAscensionQuadrant) / DegreesPerHour;

        var sinDeclination = DeclinationFactor * Math.Sin(radians * trueLongitude);
        var cosDeclination = Math.Cos(Math.Asin(sinDeclination));

        var cosHourAngle = (Math.Cos(radians * Zenith) - sinDeclination * Math.Sin(radians * location.Latitude))
                           / (cosDeclination * Math.Cos(radians * location.Latitude));

        if (cosHourAngle is > 1 or < -1)
        {
            return null;
        }

        var hourAngle = rising
            ? DegreesPerCircle - degrees * Math.Acos(cosHourAngle)
            : degrees * Math.Acos(cosHourAngle);

        hourAngle /= DegreesPerHour;

        var localMeanTime = hourAngle + rightAscension - LocalMeanTimeRate * approximateDay - LocalMeanTimeOffset;
        var universalTime = Normalize(localMeanTime - longitudeHour, HoursPerDay);
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcDate.AddHours(universalTime), timeZone);

        while (local.Date < date.Date)
        {
            local = local.AddDays(1);
        }

        while (local.Date > date.Date)
        {
            local = local.AddDays(-1);
        }

        return local;
    }

    private static double Normalize(double value, double maximum)
    {
        return (value % maximum + maximum) % maximum;
    }
}

/// <summary>
/// Represents the coordinates and source label used for solar calculations.
/// </summary>
/// <param name="Latitude">Latitude in degrees.</param>
/// <param name="Longitude">Longitude in degrees.</param>
/// <param name="Source">A human-readable description of the coordinate source.</param>
internal readonly record struct SolarLocation(double Latitude, double Longitude, string Source);

/// <summary>
/// Contains calculated local sunrise and sunset values.
/// </summary>
/// <param name="Sunrise">The local sunrise, if one occurs.</param>
/// <param name="Sunset">The local sunset, if one occurs.</param>
internal readonly record struct SunTimes(DateTime? Sunrise, DateTime? Sunset);
