using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class TrayIconServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Night Ember off")]
    public void TrimTooltip_ShortText_ReturnsOriginalText(string tooltip)
    {
        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().Be(tooltip);
    }

    [Fact]
    public void TrimTooltip_LongText_TrimsToShellLimit()
    {
        // Arrange
        var tooltip = new string('a', 100);

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().HaveLength(63);
    }

    [Fact]
    public void TrimTooltip_SurrogatePairAtLimit_DoesNotSplitThePair()
    {
        // Arrange
        var tooltip = new string('a', 62) + "\U0001F319" + new string('b', 20);

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().HaveLength(62);
        char.IsSurrogate(result[^1]).Should().BeFalse();
    }
}
