namespace NightEmber.Services;

/// <summary>
/// Converts color temperatures into normalized red, green, and blue multipliers.
/// </summary>
internal static class ColorTemperature
{
    /// <summary>
    /// The temperature in Kelvin at which the conversion curve produces an identity ramp.
    /// </summary>
    /// <remarks>The user interface supports temperatures up to 6500 K.</remarks>
    public const double NeutralKelvin = 6600;

    /// <summary>
    /// Converts a color temperature to gamma-ramp channel multipliers.
    /// </summary>
    /// <param name="kelvin">The requested color temperature in Kelvin.</param>
    /// <returns>Normalized channel multipliers in the range zero through one.</returns>
    /// <remarks>
    /// Uses a curve fit to black-body radiation. Inputs outside the supported
    /// mathematical range are clamped before evaluation.
    /// </remarks>
    public static RgbMultipliers ToRgb(double kelvin)
    {
        var temperature = Math.Clamp(kelvin, 1000, 40000) / 100.0;

        var red = temperature <= 66 ? 255 : 329.698727446 * Math.Pow(temperature - 60, -0.1332047592);

        var green = temperature <= 66
            ? 99.4708025861 * Math.Log(temperature) - 161.1195681661
            : 288.1221695283 * Math.Pow(temperature - 60, -0.0755148492);

        var blue = temperature >= 66 ? 255 :
            temperature <= 19 ? 0 : 138.5177312231 * Math.Log(temperature - 10) - 305.0447927307;

        return new RgbMultipliers(
            Math.Clamp(red, 0, 255) / 255.0,
            Math.Clamp(green, 0, 255) / 255.0,
            Math.Clamp(blue, 0, 255) / 255.0);
    }
}

/// <summary>
/// Contains normalized gamma-ramp multipliers for the red, green, and blue channels.
/// </summary>
/// <param name="Red">The red-channel multiplier.</param>
/// <param name="Green">The green-channel multiplier.</param>
/// <param name="Blue">The blue-channel multiplier.</param>
internal readonly record struct RgbMultipliers(double Red, double Green, double Blue);
