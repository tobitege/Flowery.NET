using System;
using System.Globalization;
using System.Linq;

namespace Flowery.Helpers;

/// <summary>Maps displayed digits to their place value in the underlying number.</summary>
internal static class NumberFlowDigitMap
{
    public static bool TryGetDigit(decimal value, string format, CultureInfo culture, int index,
        out int digit, out decimal placeValue)
    {
        digit = 0;
        placeValue = 0;
        if (index < 0) return false;

        var numberFormat = culture.NumberFormat;
        var separator = numberFormat.NumberDecimalSeparator;
        var scale = 0;
        var standard = format.Length > 0 && char.IsAsciiLetter(format[0])
            && format.Skip(1).All(char.IsAsciiDigit);
        if (standard)
        {
            switch (char.ToUpperInvariant(format[0]))
            {
                case 'C': separator = numberFormat.CurrencyDecimalSeparator; break;
                case 'P': separator = numberFormat.PercentDecimalSeparator; scale = 2; break;
                case 'N': case 'F': case 'G': break;
                default: return false;
            }
        }
        else if (!TryGetCustomScale(format, value, out scale))
        {
            return false;
        }

        var text = value.ToString(format, culture);
        if (standard && (text.Contains('E') || text.Contains('e'))) return false;
        var decimalPosition = text.IndexOf(separator, StringComparison.Ordinal);
        var fractionalDigits = decimalPosition < 0 ? 0
            : text.AsSpan(decimalPosition + separator.Length).CountDigits();
        var digits = text.Where(char.IsAsciiDigit).ToArray();
        if (index >= digits.Length) return false;
        digit = digits[digits.Length - 1 - index] - '0';

        var exponent = index - fractionalDigits - scale;
        if (exponent is < -28 or > 28) return false;
        placeValue = 1;
        for (var i = 0; i < Math.Abs(exponent); i++)
            placeValue = exponent < 0 ? placeValue / 10 : placeValue * 10;
        return true;
    }

    private static int CountDigits(this ReadOnlySpan<char> text)
    {
        var count = 0;
        foreach (var character in text)
            if (char.IsAsciiDigit(character)) count++;
        return count;
    }

    private static bool TryGetCustomScale(string format, decimal value, out int scale)
    {
        scale = 0;
        var sections = new System.Collections.Generic.List<string>();
        var start = 0;
        var quote = '\0';
        for (var i = 0; i < format.Length; i++)
        {
            var character = format[i];
            if (character == '\\') { i++; continue; }
            if (quote != '\0') { if (character == quote) quote = '\0'; continue; }
            if (character is '\'' or '"') { quote = character; continue; }
            if (character != ';') continue;
            sections.Add(format[start..i]);
            start = i + 1;
        }
        sections.Add(format[start..]);
        var section = sections[value < 0 && sections.Count > 1 ? 1
            : value == 0 && sections.Count > 2 ? 2 : 0];
        quote = '\0';
        var lastIntegerDigit = -1;
        var decimalPosition = section.Length;
        for (var i = 0; i < section.Length; i++)
        {
            var character = section[i];
            if (character == '\\')
            {
                if (++i < section.Length && char.IsAsciiDigit(section[i])) return false;
                continue;
            }
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                else if (char.IsAsciiDigit(character)) return false;
                continue;
            }
            if (character is '\'' or '"') { quote = character; continue; }
            if (character is 'E' or 'e' || char.IsAsciiDigit(character) && character != '0') return false;
            if (character == '%') scale += 2;
            if (character == '‰') scale += 3;
            if (character == '.' && decimalPosition == section.Length) decimalPosition = i;
            if (character is '0' or '#' && i < decimalPosition) lastIntegerDigit = i;
        }
        if (lastIntegerDigit < 0) return false;
        for (var i = lastIntegerDigit + 1; i < decimalPosition && section[i] == ','; i++) scale -= 3;
        return true;
    }
}
