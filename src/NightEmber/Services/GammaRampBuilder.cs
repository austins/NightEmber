namespace NightEmber.Services;

internal static class GammaRampBuilder
{
    public const int RampLength = 256;

    public static ushort[] Build(double red, double green, double blue, double brightness, bool clampToDriverFloor)
    {
        red *= brightness;
        green *= brightness;
        blue *= brightness;

        var minimum = clampToDriverFloor ? 0.5 : 0;
        red = Math.Clamp(red, minimum, 1);
        green = Math.Clamp(green, minimum, 1);
        blue = Math.Clamp(blue, minimum, 1);

        var ramp = new ushort[RampLength * 3];
        for (var index = 0; index < RampLength; index++)
        {
            var linear = index * 257.0;
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
