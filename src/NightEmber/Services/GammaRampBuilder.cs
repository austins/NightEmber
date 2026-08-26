namespace NightEmber.Services;

internal static class GammaRampBuilder
{
    /// <summary>
    /// The number of entries in each Windows gamma-ramp color channel.
    /// </summary>
    public const int RampLength = 256;

    /// <summary>
    /// The number of color channels in a Windows gamma ramp.
    /// </summary>
    public const int ChannelCount = 3;

    /// <summary>
    /// The lowest channel multiplier accepted reliably by Windows display drivers.
    /// </summary>
    public const double DriverMinimumMultiplier = 0.5;

    private const double RampStep = ushort.MaxValue / (RampLength - 1.0);

    public static ushort[] Build(double red, double green, double blue, double brightness, bool clampToDriverFloor)
    {
        red *= brightness;
        green *= brightness;
        blue *= brightness;

        var minimum = clampToDriverFloor ? DriverMinimumMultiplier : 0;
        red = Math.Clamp(red, minimum, 1);
        green = Math.Clamp(green, minimum, 1);
        blue = Math.Clamp(blue, minimum, 1);

        var ramp = new ushort[RampLength * ChannelCount];
        for (var index = 0; index < RampLength; index++)
        {
            var linear = index * RampStep;
            ramp[index] = ToUShort(linear * red);
            ramp[RampLength + index] = ToUShort(linear * green);
            ramp[2 * RampLength + index] = ToUShort(linear * blue);
        }

        return ramp;
    }

    private static ushort ToUShort(double value)
    {
        return (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
    }
}
