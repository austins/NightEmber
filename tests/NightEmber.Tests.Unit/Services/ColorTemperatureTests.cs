using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class ColorTemperatureTests
{
    [Theory]
    [InlineData(1000, 1, 0.2663545845, 0)]
    [InlineData(3400, 1, 0.7437268370, 0.5300863277)]
    [InlineData(6500, 1, 0.9965101328, 0.9805565033)]
    [InlineData(6600, 1, 1, 1)]
    [InlineData(40000, 0.5948014943, 0.7275657511, 1)]
    public void ToRgb_KnownTemperature_ReturnsExpectedMultipliers(
        double kelvin,
        double red,
        double green,
        double blue)
    {
        // Act
        var result = ColorTemperature.ToRgb(kelvin);

        // Assert
        result.Red.Should().BeApproximately(red, 0.0000001);
        result.Green.Should().BeApproximately(green, 0.0000001);
        result.Blue.Should().BeApproximately(blue, 0.0000001);
    }

    [Theory]
    [InlineData(-1000, 1000)]
    [InlineData(0, 1000)]
    [InlineData(999, 1000)]
    [InlineData(40001, 40000)]
    [InlineData(double.NegativeInfinity, 1000)]
    [InlineData(double.PositiveInfinity, 40000)]
    public void ToRgb_OutOfRangeTemperature_ClampsToSupportedRange(double input, double clamped)
    {
        // Act
        var result = ColorTemperature.ToRgb(input);
        var expected = ColorTemperature.ToRgb(clamped);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1200)]
    [InlineData(3400)]
    [InlineData(6500)]
    [InlineData(6600)]
    [InlineData(10000)]
    [InlineData(40000)]
    public void ToRgb_SupportedTemperature_ReturnsNormalizedChannels(double kelvin)
    {
        // Act
        var result = ColorTemperature.ToRgb(kelvin);

        // Assert
        result.Red.Should().BeInRange(0, 1);
        result.Green.Should().BeInRange(0, 1);
        result.Blue.Should().BeInRange(0, 1);
    }
}
