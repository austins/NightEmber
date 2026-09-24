using NightEmber.Models;
using System.Globalization;

namespace NightEmber.TrayIcon;

/// <summary>
/// Formats the tray tooltip that describes the current tint state and the next schedule change.
/// </summary>
internal static class TrayTooltip
{
    /// <summary>
    /// Formats the two-line tray tooltip.
    /// </summary>
    /// <param name="isOn">Whether the tint is active.</param>
    /// <param name="temperature">The configured color temperature in Kelvin.</param>
    /// <param name="hasManualOverride">Whether a manual tray toggle currently overrides the schedule.</param>
    /// <param name="mode">The configured schedule mode.</param>
    /// <param name="nextChange">The next schedule boundary, or <see langword="null" /> when none exists.</param>
    /// <param name="culture">The culture used to format the next-change time.</param>
    /// <returns>The tooltip text.</returns>
    public static string Format(
        bool isOn,
        int temperature,
        bool hasManualOverride,
        ScheduleMode mode,
        DateTime? nextChange,
        CultureInfo culture)
    {
        var state = isOn ? $"Night Ember on - {temperature}K" : "Night Ember off";
        var time = nextChange?.ToString(culture.DateTimeFormat.ShortTimePattern, culture) ?? string.Empty;
        var verb = isOn ? "off" : "on";

        string reason;
        if (hasManualOverride)
        {
            reason = nextChange is null ? "Manual" : $"Manual until {time}";
        }
        else
        {
            reason = mode switch
            {
                ScheduleMode.Sunset when nextChange is not null => $"Sun schedule: {verb} {time}",
                ScheduleMode.Sunset => "Sunset to sunrise",
                ScheduleMode.Custom when nextChange is not null => $"Set hours: {verb} {time}",
                ScheduleMode.Custom => "Set hours",
                _ => "Manual only"
            };
        }

        return $"{state}\n{reason}";
    }
}
