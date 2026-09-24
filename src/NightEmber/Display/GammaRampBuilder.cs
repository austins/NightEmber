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
    private const double DriverMinimumMultiplier = 0.5;

    private const double RampStep = ushort.MaxValue / (RampLength - 1.0);

    /// <summary>
    /// Creates a Windows gamma ramp for the specified channel and brightness multipliers.
    /// </summary>
    /// <param name="red">The normalized red-channel multiplier.</param>
    /// <param name="green">The normalized green-channel multiplier.</param>
    /// <param name="blue">The normalized blue-channel multiplier.</param>
    /// <param name="brightness">The normalized brightness multiplier.</param>
    /// <param name="clampToDriverFloor">
    /// Whether to keep every channel multiplier at or above the minimum accepted reliably by display drivers.
    /// Brightness then dims only the range above that floor.
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
    /// Whether to keep every channel multiplier at or above the minimum accepted reliably by display drivers.
    /// Brightness then dims only the range above that floor.
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

        var minimum = clampToDriverFloor ? DriverMinimumMultiplier : 0;
        red = Dim(red, brightness, minimum);
        green = Dim(green, brightness, minimum);
        blue = Dim(blue, brightness, minimum);

        for (var index = 0; index < RampLength; index++)
        {
            var linear = index * RampStep;
            ramp[index] = ToUShort(linear * red);
            ramp[RampLength + index] = ToUShort(linear * green);
            ramp[2 * RampLength + index] = ToUShort(linear * blue);
        }
    }

    private static double Dim(double channel, double brightness, double floor)
    {
        channel = Math.Clamp(channel, floor, 1);
        brightness = Math.Clamp(brightness, floor, 1);

        // Dimming scales only the headroom above the driver floor, so channels that differ
        // before dimming stay distinguishable instead of collapsing onto the floor together.
        return floor + (channel - floor) * (brightness - floor) / (1 - floor);
    }

    private static ushort ToUShort(double value)
    {
        return (ushort)Math.Round(Math.Clamp(value, ushort.MinValue, ushort.MaxValue));
    }
}
