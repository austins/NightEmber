namespace NightEmber.Display;

/// <summary>
/// Converts color temperatures into normalized red, green, and blue multipliers.
/// </summary>
internal static class ColorTemperature
{
    /// <summary>
    /// The temperature in Kelvin at which the conversion produces an identity ramp.
    /// </summary>
    /// <remarks>
    /// Matches the sRGB D65 white point and the warmest-to-neutral upper bound in the user interface.
    /// </remarks>
    public const int NeutralKelvin = 6500;

    private const int MinimumKelvin = 1000;
    private const int MaximumKelvin = 40000;
    private const int KelvinScale = 100;
    private const int MaximumChannelValue = 255;
    private const int RedGreenThreshold = 66;
    private const int BlueThreshold = 19;
    private const double RedPowerCoefficient = 329.698727446;
    private const int RedPowerOffset = 60;
    private const double RedPowerExponent = -0.1332047592;
    private const double GreenLogCoefficient = 99.4708025861;
    private const double GreenLogAdjustment = 161.1195681661;
    private const double GreenPowerCoefficient = 288.1221695283;
    private const int GreenPowerOffset = 60;
    private const double GreenPowerExponent = -0.0755148492;
    private const double BlueLogCoefficient = 138.5177312231;
    private const int BlueLogOffset = 10;
    private const double BlueLogAdjustment = 305.0447927307;

    private static readonly RgbMultipliers NeutralCurve = EvaluateCurve(NeutralKelvin);

    /// <summary>
    /// Converts a color temperature to gamma-ramp channel multipliers.
    /// </summary>
    /// <param name="kelvin">The requested color temperature in Kelvin.</param>
    /// <returns>Normalized channel multipliers in the range zero through one.</returns>
    /// <remarks>
    /// Uses Tanner Helland's empirical curve fit to black-body radiation:
    /// <see href="https://tannerhelland.com/2012/09/18/convert-temperature-rgb-algorithm-code.html" />.
    /// The fitted coefficients retain their published precision. The curve is normalized so
    /// <see cref="NeutralKelvin" /> maps exactly to white; hotter inputs saturate at white.
    /// Inputs outside the supported mathematical range are clamped before evaluation.
    /// </remarks>
    public static RgbMultipliers ToRgb(double kelvin)
    {
        var curve = EvaluateCurve(kelvin);
        return new RgbMultipliers(
            Math.Min(curve.Red / NeutralCurve.Red, 1),
            Math.Min(curve.Green / NeutralCurve.Green, 1),
            Math.Min(curve.Blue / NeutralCurve.Blue, 1));
    }

    private static RgbMultipliers EvaluateCurve(double kelvin)
    {
        var temperature = Math.Clamp(kelvin, MinimumKelvin, MaximumKelvin) / KelvinScale;

        var red = temperature <= RedGreenThreshold
            ? MaximumChannelValue
            : RedPowerCoefficient * Math.Pow(temperature - RedPowerOffset, RedPowerExponent);

        var green = temperature <= RedGreenThreshold
            ? GreenLogCoefficient * Math.Log(temperature) - GreenLogAdjustment
            : GreenPowerCoefficient * Math.Pow(temperature - GreenPowerOffset, GreenPowerExponent);

        var blue = temperature >= RedGreenThreshold ? MaximumChannelValue :
            temperature <= BlueThreshold ? 0 :
            BlueLogCoefficient * Math.Log(temperature - BlueLogOffset) - BlueLogAdjustment;

        return new RgbMultipliers(
            Math.Clamp(red, 0, MaximumChannelValue) / MaximumChannelValue,
            Math.Clamp(green, 0, MaximumChannelValue) / MaximumChannelValue,
            Math.Clamp(blue, 0, MaximumChannelValue) / MaximumChannelValue);
    }
}

/// <summary>
/// Contains normalized gamma-ramp multipliers for the red, green, and blue channels.
/// </summary>
/// <param name="Red">The red-channel multiplier.</param>
/// <param name="Green">The green-channel multiplier.</param>
/// <param name="Blue">The blue-channel multiplier.</param>
internal readonly record struct RgbMultipliers(double Red, double Green, double Blue);
