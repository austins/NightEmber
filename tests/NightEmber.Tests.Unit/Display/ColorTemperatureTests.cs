using NightEmber.Display;

namespace NightEmber.Tests.Unit.Display;

public sealed class ColorTemperatureTests
{
    [Theory]
    [InlineData(1000, 1, 0.2672873820, 0)]
    [InlineData(3400, 1, 0.7463314345, 0.5405974320)]
    [InlineData(6500, 1, 1, 1)]
    [InlineData(6600, 1, 1, 1)]
    [InlineData(40000, 0.5948014943, 0.7301137511, 1)]
    public void ToRgb_KnownTemperature_ReturnsExpectedMultipliers(double kelvin, double red, double green, double blue)
    {
        // Act
        var result = ColorTemperature.ToRgb(kelvin);

        // Assert
        result.Red.Should().BeApproximately(red, 0.0000001);
        result.Green.Should().BeApproximately(green, 0.0000001);
        result.Blue.Should().BeApproximately(blue, 0.0000001);
    }

    [Fact]
    public void ToRgb_NeutralTemperature_ReturnsIdentityMultipliers()
    {
        // Act
        var result = ColorTemperature.ToRgb(ColorTemperature.NeutralKelvin);

        // Assert
        result.Should().Be(new RgbMultipliers(1, 1, 1));
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
    [InlineData(1900)]
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
