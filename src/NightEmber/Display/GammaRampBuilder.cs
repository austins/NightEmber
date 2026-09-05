namespace NightEmber.Display;

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
    /// The total number of entries in a Windows gamma ramp.
    /// </summary>
    public const int RampElementCount = RampLength * ChannelCount;

    /// <summary>
    /// The lowest channel multiplier accepted reliably by Windows display drivers.
    /// </summary>
    public const double DriverMinimumMultiplier = 0.5;

    private const double RampStep = ushort.MaxValue / (RampLength - 1.0);

    /// <summary>
    /// Creates a Windows gamma ramp for the specified channel and brightness multipliers.
    /// </summary>
    /// <param name="red">The normalized red-channel multiplier.</param>
    /// <param name="green">The normalized green-channel multiplier.</param>
    /// <param name="blue">The normalized blue-channel multiplier.</param>
    /// <param name="brightness">The normalized brightness multiplier.</param>
    /// <param name="clampToDriverFloor">
    /// Whether to clamp each combined multiplier to the minimum accepted reliably by display drivers.
    /// </param>
    /// <returns>A newly allocated gamma ramp.</returns>
    public static ushort[] Build(double red, double green, double blue, double brightness, bool clampToDriverFloor)
    {
        var ramp = new ushort[RampElementCount];
        BuildInto(ramp, red, green, blue, brightness, clampToDriverFloor);
        return ramp;
    }

    /// <summary>
    /// Fills a caller-provided buffer with a Windows gamma ramp.
    /// </summary>
    /// <param name="ramp">The destination buffer to populate.</param>
    /// <param name="red">The normalized red-channel multiplier.</param>
    /// <param name="green">The normalized green-channel multiplier.</param>
    /// <param name="blue">The normalized blue-channel multiplier.</param>
    /// <param name="brightness">The normalized brightness multiplier.</param>
    /// <param name="clampToDriverFloor">
    /// Whether to clamp each combined multiplier to the minimum accepted reliably by display drivers.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="ramp" /> contains fewer than <see cref="RampElementCount" /> entries.
    /// </exception>
    public static void BuildInto(
        Span<ushort> ramp,
        double red,
        double green,
        double blue,
        double brightness,
        bool clampToDriverFloor)
    {
        if (ramp.Length < RampElementCount)
        {
            throw new ArgumentException($"A gamma ramp requires at least {RampElementCount} entries.", nameof(ramp));
        }

        red *= brightness;
        green *= brightness;
        blue *= brightness;

        var minimum = clampToDriverFloor ? DriverMinimumMultiplier : 0;
        red = Math.Clamp(red, minimum, 1);
        green = Math.Clamp(green, minimum, 1);
        blue = Math.Clamp(blue, minimum, 1);

        for (var index = 0; index < RampLength; index++)
        {
            var linear = index * RampStep;
            ramp[index] = ToUShort(linear * red);
            ramp[RampLength + index] = ToUShort(linear * green);
            ramp[2 * RampLength + index] = ToUShort(linear * blue);
        }
    }

    private static ushort ToUShort(double value)
    {
        return (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue);
    }
}
