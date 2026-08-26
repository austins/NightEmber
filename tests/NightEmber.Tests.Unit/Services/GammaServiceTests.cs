using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class GammaServiceTests
{
    [Theory]
    [InlineData(30000, 30000, false)]
    [InlineData(30000, 29745, false)]
    [InlineData(30000, 29744, false)]
    [InlineData(30000, 29743, true)]
    [InlineData(30000, 30255, false)]
    [InlineData(30000, 30256, false)]
    [InlineData(30000, 30257, true)]
    public void HasDrifted_ExpectedAndActualBlueValues_UsesExclusiveThreshold(
        int expected,
        int actual,
        bool hasDrifted)
    {
        // Act
        var result = GammaService.HasDrifted((ushort)expected, (ushort)actual);

        // Assert
        result.Should().Be(hasDrifted);
    }
}
