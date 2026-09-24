using NightEmber.Display;

namespace NightEmber.Tests.Unit.Display;

public sealed class GammaTransitionTests
{
    [Theory]
    [InlineData(-100, 400, 0)]
    [InlineData(0, 400, 0)]
    [InlineData(100, 400, 0.25)]
    [InlineData(400, 400, 1)]
    [InlineData(500, 400, 1)]
    [InlineData(0, 0, 1)]
    public void CalculateProgress_ElapsedTime_ReturnsClampedProgress(
        int elapsedMilliseconds,
        int durationMilliseconds,
        double expected)
    {
        // Act
        var result = GammaTransition.CalculateProgress(
            TimeSpan.FromMilliseconds(elapsedMilliseconds),
            TimeSpan.FromMilliseconds(durationMilliseconds));

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-0.5, 6500, 100)]
    [InlineData(0.25, 5725, 95)]
    [InlineData(0.5, 4950, 90)]
    [InlineData(1, 3400, 80)]
    [InlineData(1.25, 3400, 80)]
    public void Interpolate_FadeProgress_InterpolatesAndClampsToEndpoints(
        double progress,
        double expectedKelvin,
        double expectedBrightness)
    {
        // Act
        var result = GammaTransition.Interpolate(6500, 100, 3400, 80, progress);

        // Assert
        result.Kelvin.Should().Be(expectedKelvin);
        result.Brightness.Should().Be(expectedBrightness);
    }

    [Theory]
    [InlineData(3400, 80, 3400, 80, true)]
    [InlineData(3400, 80, 3400.009, 80.009, true)]
    [InlineData(3400, 80, 3400.01, 80, false)]
    [InlineData(3400, 80, 3400, 80.01, false)]
    public void StatesMatch_States_UsesExclusiveTolerance(
        double firstKelvin,
        double firstBrightness,
        double secondKelvin,
        double secondBrightness,
        bool expected)
    {
        // Act
        var result = GammaTransition.StatesMatch(firstKelvin, firstBrightness, secondKelvin, secondBrightness);

        // Assert
        result.Should().Be(expected);
    }
}
