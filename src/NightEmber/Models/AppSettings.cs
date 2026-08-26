using System.Globalization;
using System.Text.Json.Serialization;

namespace NightEmber.Models;

/// <summary>
/// Represents the validated, portable Night Ember configuration.
/// </summary>
internal sealed class AppSettings
{
    /// <summary>
    /// The lowest supported color temperature in Kelvin.
    /// </summary>
    public const int MinimumTemperature = 1200;

    /// <summary>
    /// The highest supported color temperature in Kelvin.
    /// </summary>
    public const int MaximumTemperature = 6500;

    /// <summary>
    /// The lowest supported software brightness percentage.
    /// </summary>
    public const int MinimumBrightness = 50;

    /// <summary>
    /// The highest supported software brightness percentage.
    /// </summary>
    public const int MaximumBrightness = 100;

    /// <summary>
    /// The longest supported transition duration in milliseconds.
    /// </summary>
    public const int MaximumFadeMilliseconds = 5000;

    /// <summary>
    /// Gets or initializes the active color temperature in Kelvin.
    /// </summary>
    public int Temperature { get; init; } = 3400;

    /// <summary>
    /// Gets or initializes the software brightness percentage.
    /// </summary>
    public int Brightness { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the active scheduling mode.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ScheduleMode>))]
    public ScheduleMode Mode { get; init; } = ScheduleMode.Sunset;

    /// <summary>
    /// Gets or initializes the custom activation time in invariant <c>HH:mm</c> format.
    /// </summary>
    public string CustomOn { get; init; } = "21:00";

    /// <summary>
    /// Gets or initializes the custom deactivation time in invariant <c>HH:mm</c> format.
    /// </summary>
    public string CustomOff { get; init; } = "07:00";

    /// <summary>
    /// Gets or initializes the transition duration in milliseconds.
    /// </summary>
    public int FadeMs { get; init; } = 300;

    /// <summary>
    /// Gets a new settings instance populated with safe defaults.
    /// </summary>
    [JsonIgnore]
    public static AppSettings Default => new();

    /// <summary>
    /// Creates an independent copy of this settings instance.
    /// </summary>
    /// <returns>A copy containing the same values.</returns>
    public AppSettings Clone()
    {
        return new AppSettings
        {
            Temperature = Temperature,
            Brightness = Brightness,
            Mode = Mode,
            CustomOn = CustomOn,
            CustomOff = CustomOff,
            FadeMs = FadeMs
        };
    }

    /// <summary>
    /// Replaces invalid or out-of-range values with their defaults.
    /// </summary>
    /// <returns>A new normalized settings instance.</returns>
    public AppSettings Normalize()
    {
        var defaults = Default;

        return new AppSettings
        {
            Temperature =
                IsInRange(Temperature, MinimumTemperature, MaximumTemperature) ? Temperature : defaults.Temperature,
            Brightness =
                IsInRange(Brightness, MinimumBrightness, MaximumBrightness) ? Brightness : defaults.Brightness,
            Mode = Enum.IsDefined(Mode) ? Mode : defaults.Mode,
            CustomOn = TryParseTime(CustomOn, out _) ? CustomOn : defaults.CustomOn,
            CustomOff = TryParseTime(CustomOff, out _) ? CustomOff : defaults.CustomOff,
            FadeMs = IsInRange(FadeMs, 0, MaximumFadeMilliseconds) ? FadeMs : defaults.FadeMs
        };
    }

    /// <summary>
    /// Parses a portable 24-hour configuration time.
    /// </summary>
    /// <param name="value">The value in invariant <c>HH:mm</c> format.</param>
    /// <param name="result">The parsed time when successful.</param>
    /// <returns><see langword="true" /> when the value is valid; otherwise, <see langword="false" />.</returns>
    public static bool TryParseTime(string? value, out TimeSpan result)
    {
        return TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Formats a time for portable configuration storage.
    /// </summary>
    /// <param name="value">The time to format.</param>
    /// <returns>The time in invariant <c>HH:mm</c> format.</returns>
    public static string FormatTime(TimeSpan value)
    {
        return DateTime.Today.Add(value).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static bool IsInRange(int value, int minimum, int maximum)
    {
        return value >= minimum && value <= maximum;
    }
}
