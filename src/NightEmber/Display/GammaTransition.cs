namespace NightEmber.Display;

/// <summary>
/// Calculates timed transitions between color temperature and brightness states.
/// </summary>
internal static class GammaTransition
{
    private const double ComparisonTolerance = 0.01;

    /// <summary>
    /// Calculates clamped fade progress from monotonic elapsed time.
    /// </summary>
    /// <param name="elapsed">The elapsed fade time.</param>
    /// <param name="duration">The configured fade duration.</param>
    /// <returns>A progress value from zero through one.</returns>
    public static double CalculateProgress(TimeSpan elapsed, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 1;
        }

        return Math.Clamp(elapsed / duration, 0, 1);
    }

    /// <summary>
    /// Linearly interpolates temperature and brightness for a fade.
    /// </summary>
    /// <param name="startKelvin">The starting color temperature in Kelvin.</param>
    /// <param name="startBrightness">The starting brightness percentage.</param>
    /// <param name="targetKelvin">The target color temperature in Kelvin.</param>
    /// <param name="targetBrightness">The target brightness percentage.</param>
    /// <param name="progress">The fade progress, clamped to the range from zero through one.</param>
    /// <returns>The interpolated color temperature and brightness percentage.</returns>
    public static (double Kelvin, double Brightness) Interpolate(
        double startKelvin,
        double startBrightness,
        double targetKelvin,
        double targetBrightness,
        double progress)
    {
        progress = Math.Clamp(progress, 0, 1);

        return (startKelvin + (targetKelvin - startKelvin) * progress,
            startBrightness + (targetBrightness - startBrightness) * progress);
    }

    /// <summary>
    /// Determines whether two gamma states are equivalent within the application tolerance.
    /// </summary>
    /// <param name="firstKelvin">The first color temperature in Kelvin.</param>
    /// <param name="firstBrightness">The first brightness percentage.</param>
    /// <param name="secondKelvin">The second color temperature in Kelvin.</param>
    /// <param name="secondBrightness">The second brightness percentage.</param>
    /// <returns><see langword="true" /> when both values are within tolerance; otherwise, <see langword="false" />.</returns>
    public static bool StatesMatch(
        double firstKelvin,
        double firstBrightness,
        double secondKelvin,
        double secondBrightness)
    {
        return Math.Abs(firstKelvin - secondKelvin) < ComparisonTolerance
               && Math.Abs(firstBrightness - secondBrightness) < ComparisonTolerance;
    }
}
