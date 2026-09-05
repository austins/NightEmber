using NightEmber.Controls;
using System.Globalization;

namespace NightEmber.Tests.Unit.Controls;

public sealed class TimeFormatTests
{
    [Theory]
    [InlineData("H", "7")]
    [InlineData("HH", "07")]
    [InlineData("h", "7")]
    [InlineData("hh", "07")]
    [InlineData("m", "5")]
    [InlineData("mm", "05")]
    [InlineData("t", "A")]
    [InlineData("tt", "AM")]
    public void FormatToken_CustomTimeToken_ReturnsExpectedText(string token, string expected)
    {
        // Arrange
        var value = new DateTime(2026, 8, 24, 7, 5, 0, DateTimeKind.Unspecified);

        // Act
        var result = TimeFormat.FormatToken(value, token, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("H:mm", "H")]
    [InlineData("HH:mm", "HH")]
    [InlineData("'H' HH:mm", "HH")]
    [InlineData("\"H\" h:mm", "h")]
    [InlineData(@"\H h:mm", "h")]
    [InlineData("'h\"' HH:mm", "HH")]
    [InlineData("\"h'\" HH:mm", "HH")]
    [InlineData(@"'h\'H' HH:mm", "HH")]
    public void FindToken_HourPattern_IgnoresQuotedAndEscapedCharacters(string pattern, string expected)
    {
        // Act
        var result = TimeFormat.FindToken(pattern, 'H', 'h');

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FindToken_PatternContainingOnlyLiteralTokens_ReturnsNull()
    {
        // Arrange
        const string pattern = "'H' \"h\" \\H";

        // Act
        var result = TimeFormat.FindToken(pattern, 'H', 'h');

        // Assert
        result.Should().BeNull();
    }
}
