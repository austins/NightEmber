using System.Globalization;

namespace NightEmber.Controls;

internal static class TimeFormat
{
    /// <summary>
    /// Formats a date and time using a culture-specific custom time token.
    /// </summary>
    /// <param name="value">The date and time to format.</param>
    /// <param name="token">The custom date and time format token.</param>
    /// <param name="culture">The culture that supplies formatting conventions.</param>
    /// <returns>The formatted token value.</returns>
    public static string FormatToken(DateTime value, string token, CultureInfo culture)
    {
        // A one-character format is otherwise interpreted as a standard DateTime format.
        var format = token.Length == 1 ? $"%{token}" : token;

        return value.ToString(format, culture);
    }

    /// <summary>
    /// Finds the first unquoted and unescaped run of the requested token characters.
    /// </summary>
    /// <param name="pattern">The custom date and time format pattern to inspect.</param>
    /// <param name="tokenCharacters">The token characters to find.</param>
    /// <returns>The matching token run, or <see langword="null" /> when none is present.</returns>
    public static string? FindToken(string pattern, params char[] tokenCharacters)
    {
        var quoted = false;
        var escaped = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var character = pattern[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character is '\'' or '"')
            {
                quoted = !quoted;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (quoted || !tokenCharacters.Contains(character))
            {
                continue;
            }

            var length = 1;
            while (i + length < pattern.Length && pattern[i + length] == character)
            {
                length++;
            }

            return pattern.Substring(i, length);
        }

        return null;
    }
}
