using NightEmber.Display;

namespace NightEmber.Tests.Unit.Display;

public sealed class GammaRampBuilderTests
{
    [Fact]
    public void Build_IdentityMultipliers_ReturnsThreeIdentityChannels()
    {
        // Act
        var ramp = GammaRampBuilder.Build(1, 1, 1, 1, false);

        // Assert
        ramp.Should().HaveCount(GammaRampBuilder.RampLength * 3);
        for (var index = 0; index < GammaRampBuilder.RampLength; index++)
        {
            var expected = (ushort)(index * 257);
            ramp[index].Should().Be(expected);
            ramp[GammaRampBuilder.RampLength + index].Should().Be(expected);
            ramp[2 * GammaRampBuilder.RampLength + index].Should().Be(expected);
        }
    }

    [Fact]
    public void Build_DifferentChannelMultipliers_ScalesEachChannel()
    {
        // Act
        var ramp = GammaRampBuilder.Build(0.25, 0.5, 0.75, 0.8, false);

        // Assert
        ramp[255].Should().Be(13107);
        ramp[GammaRampBuilder.RampLength + 255].Should().Be(26214);
        ramp[2 * GammaRampBuilder.RampLength + 255].Should().Be(39321);
    }

    [Fact]
    public void Build_DriverFloorEnabled_ClampsCombinedMultiplierToOneHalf()
    {
        // Act
        var ramp = GammaRampBuilder.Build(0, 0.25, 0.5, 0.5, true);

        // Assert
        ramp[255].Should().Be(32767);
        ramp[GammaRampBuilder.RampLength + 255].Should().Be(32767);
        ramp[2 * GammaRampBuilder.RampLength + 255].Should().Be(32767);
    }

    [Fact]
    public void Build_DriverFloorDisabled_AllowsZeroAndLowValues()
    {
        // Act
        var ramp = GammaRampBuilder.Build(0, 0.25, 0.5, 0.5, false);

        // Assert
        ramp[255].Should().Be(0);
        ramp[GammaRampBuilder.RampLength + 255].Should().Be(8191);
        ramp[2 * GammaRampBuilder.RampLength + 255].Should().Be(16383);
    }

    [Fact]
    public void Build_ValuesAboveOne_ClampsWithoutOverflow()
    {
        // Act
        var ramp = GammaRampBuilder.Build(2, double.PositiveInfinity, 4, 2, false);

        // Assert
        ramp[255].Should().Be(ushort.MaxValue);
        ramp[GammaRampBuilder.RampLength + 255].Should().Be(ushort.MaxValue);
        ramp[2 * GammaRampBuilder.RampLength + 255].Should().Be(ushort.MaxValue);
    }

    [Fact]
    public void BuildInto_PreviouslyUsedBuffer_OverwritesEveryRampEntry()
    {
        // Arrange
        var ramp = new ushort[GammaRampBuilder.RampElementCount];
        Array.Fill(ramp, ushort.MaxValue);

        // Act
        GammaRampBuilder.BuildInto(ramp, 0.25, 0.5, 0.75, 0.8, false);

        // Assert
        ramp.Should().NotContain(ushort.MaxValue);
        ramp[255].Should().Be(13107);
        ramp[GammaRampBuilder.RampLength + 255].Should().Be(26214);
        ramp[2 * GammaRampBuilder.RampLength + 255].Should().Be(39321);
    }

    [Fact]
    public void Build_UndersizedBuffer_Throws()
    {
        // Arrange
        var ramp = new ushort[GammaRampBuilder.RampElementCount - 1];

        // Act
        var action = () => GammaRampBuilder.BuildInto(ramp, 1, 1, 1, 1, false);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("ramp");
    }
}
