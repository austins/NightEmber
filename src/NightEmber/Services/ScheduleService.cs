using NightEmber.Models;

namespace NightEmber.Services;

/// <summary>
/// Evaluates schedule state and computes upcoming schedule boundaries.
/// </summary>
internal static class ScheduleService
{
    /// <summary>
    /// Determines whether the tint should be active at a specified local time.
    /// </summary>
    /// <param name="settings">The schedule settings to evaluate.</param>
    /// <param name="now">The local date and time to evaluate.</param>
    /// <returns><see langword="true" /> when the configured schedule is active.</returns>
    public static bool ShouldBeOn(AppSettings settings, DateTime now)
    {
        return ShouldBeOn(settings, now, SolarCalculator.GetLocalTimeZoneLocation(), TimeZoneInfo.Local);
    }

    public static bool ShouldBeOn(AppSettings settings, DateTime now, SolarLocation location, TimeZoneInfo timeZone)
    {
        return settings.Mode switch
        {
            ScheduleMode.Manual => false,
            ScheduleMode.Custom => IsWithinCustomWindow(settings, now),
            ScheduleMode.Sunset => IsOutsideDaylight(now, location, timeZone),
            _ => false
        };
    }

    /// <summary>
    /// Finds the next time the configured schedule changes state.
    /// </summary>
    /// <param name="settings">The schedule settings to evaluate.</param>
    /// <param name="now">The current local date and time.</param>
    /// <returns>The next boundary, or <see langword="null" /> when no boundary exists.</returns>
    public static DateTime? GetNextChange(AppSettings settings, DateTime now)
    {
        return GetNextChange(settings, now, SolarCalculator.GetLocalTimeZoneLocation(), TimeZoneInfo.Local);
    }

    public static DateTime? GetNextChange(
        AppSettings settings,
        DateTime now,
        SolarLocation location,
        TimeZoneInfo timeZone)
    {
        return settings.Mode switch
        {
            ScheduleMode.Manual => null,
            ScheduleMode.Custom => GetNextCustomChange(settings, now),
            ScheduleMode.Sunset => GetNextSolarChange(now, location, timeZone),
            _ => null
        };
    }

    /// <summary>
    /// Gets sunrise and sunset estimates for a local calendar date.
    /// </summary>
    /// <param name="date">The local date to calculate.</param>
    /// <returns>The solar estimate and the source used to approximate location.</returns>
    public static SolarSummary GetSolarSummary(DateTime date)
    {
        var location = SolarCalculator.GetLocalTimeZoneLocation();
        var times = SolarCalculator.GetSunTimes(location, date);
        return new SolarSummary(times.Sunrise, times.Sunset, location.Source);
    }

    private static bool IsWithinCustomWindow(AppSettings settings, DateTime now)
    {
        if (!AppSettings.TryParseTime(settings.CustomOn, out var on)
            || !AppSettings.TryParseTime(settings.CustomOff, out var off)
            || on == off)
        {
            return false;
        }

        var current = now.TimeOfDay;
        return on > off ? current >= on || current < off : current >= on && current < off;
    }

    private static DateTime? GetNextCustomChange(AppSettings settings, DateTime now)
    {
        if (!AppSettings.TryParseTime(settings.CustomOn, out var on)
            || !AppSettings.TryParseTime(settings.CustomOff, out var off)
            || on == off)
        {
            return null;
        }

        var nextOn = now.Date.Add(on);
        var nextOff = now.Date.Add(off);
        if (nextOn <= now)
        {
            nextOn = nextOn.AddDays(1);
        }

        if (nextOff <= now)
        {
            nextOff = nextOff.AddDays(1);
        }

        return nextOn < nextOff ? nextOn : nextOff;
    }

    private static bool IsOutsideDaylight(DateTime now, SolarLocation location, TimeZoneInfo timeZone)
    {
        var sun = SolarCalculator.GetSunTimes(location, now, timeZone);
        return sun.Sunrise is not null && sun.Sunset is not null && (now < sun.Sunrise || now >= sun.Sunset);
    }

    private static DateTime? GetNextSolarChange(DateTime now, SolarLocation location, TimeZoneInfo timeZone)
    {
        var today = SolarCalculator.GetSunTimes(location, now, timeZone);
        if (today.Sunrise is null || today.Sunset is null)
        {
            return null;
        }

        if (now < today.Sunrise)
        {
            return today.Sunrise;
        }

        if (now < today.Sunset)
        {
            return today.Sunset;
        }

        return SolarCalculator.GetSunTimes(location, now.Date.AddDays(1), timeZone).Sunrise;
    }
}

/// <summary>
/// Describes estimated solar events and the location source used to calculate them.
/// </summary>
/// <param name="Sunrise">The estimated local sunrise, if one occurs.</param>
/// <param name="Sunset">The estimated local sunset, if one occurs.</param>
/// <param name="Source">A description of the location approximation.</param>
internal readonly record struct SolarSummary(DateTime? Sunrise, DateTime? Sunset, string Source);
