using System;
using System.Globalization;

namespace Flowery.Extensions;

/// <summary>
/// Extension methods for converting decimal values to various string notations.
/// These are static counterparts to the instance methods on DaisyNumericUpDown,
/// useful for ViewModel conversions where access to the control is not available.
/// </summary>
public static class DecimalExtensions
{
    #region To String Methods

    /// <summary>
    /// Converts a decimal value to a hexadecimal string (e.g., "0xFF" or "FF").
    /// </summary>
    /// <param name="value">The decimal value to convert</param>
    /// <param name="includePrefix">Include "0x" prefix (default: true)</param>
    /// <param name="uppercase">Use uppercase letters (default: true)</param>
    /// <returns>Hex string representation</returns>
    public static string ToHexString(this decimal value, bool includePrefix = true, bool uppercase = true)
    {
        var format = uppercase ? "X" : "x";
        var hex = ((long)value).ToString(format);
        return includePrefix ? "0x" + hex : hex;
    }

    /// <summary>
    /// Converts a decimal value to a binary string (e.g., "0b1010" or "1010").
    /// </summary>
    /// <param name="value">The decimal value to convert</param>
    /// <param name="includePrefix">Include "0b" prefix (default: true)</param>
    /// <returns>Binary string representation</returns>
    public static string ToBinaryString(this decimal value, bool includePrefix = true)
    {
        var binary = Convert.ToString((long)value, 2);
        return includePrefix ? "0b" + binary : binary;
    }

    /// <summary>
    /// Converts a decimal value to an octal string (e.g., "0o755" or "755").
    /// </summary>
    /// <param name="value">The decimal value to convert</param>
    /// <param name="includePrefix">Include "0o" prefix (default: true)</param>
    /// <returns>Octal string representation</returns>
    public static string ToOctalString(this decimal value, bool includePrefix = true)
    {
        var octal = Convert.ToString((long)value, 8);
        return includePrefix ? "0o" + octal : octal;
    }

    /// <summary>
    /// Converts a decimal value to a color hex string (e.g., "#FF5733" or "FF5733").
    /// The result is always padded to 6 digits.
    /// </summary>
    /// <param name="value">The decimal value to convert (0-16777215)</param>
    /// <param name="includePrefix">Include "#" prefix (default: true)</param>
    /// <param name="uppercase">Use uppercase letters (default: true)</param>
    /// <returns>Color hex string padded to 6 digits</returns>
    public static string ToColorHexString(this decimal value, bool includePrefix = true, bool uppercase = true)
    {
        var format = uppercase ? "X" : "x";
        var hex = ((long)value).ToString(format).PadLeft(6, '0');
        return includePrefix ? "#" + hex : hex;
    }

    /// <summary>
    /// Converts a decimal value to an IPv4 address string (e.g., "192.168.1.1").
    /// </summary>
    /// <param name="value">The decimal value to convert (0-4294967295)</param>
    /// <returns>IPv4 address string</returns>
    public static string ToIPAddressString(this decimal value)
    {
        var ipValue = (uint)Math.Max(0, Math.Min(uint.MaxValue, value));
        return $"{(ipValue >> 24) & 0xFF}.{(ipValue >> 16) & 0xFF}.{(ipValue >> 8) & 0xFF}.{ipValue & 0xFF}";
    }

    #endregion

    #region From String Methods

    /// <summary>
    /// Parses a hexadecimal string to a decimal value.
    /// Accepts formats: "0xFF", "0xff", "FF", "ff"
    /// </summary>
    /// <param name="hex">The hex string to parse</param>
    /// <returns>Decimal value</returns>
    /// <exception cref="FormatException">If the string is not a valid hex number</exception>
    public static decimal FromHexString(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex string cannot be null or empty", nameof(hex));

        hex = hex.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        if (hex.Length == 0)
            throw new ArgumentException("Hex string contains no digits", nameof(hex));

        return Convert.ToInt64(hex, 16);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string to a decimal value.
    /// </summary>
    /// <param name="hex">The hex string to parse</param>
    /// <param name="result">The resulting decimal value</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryFromHexString(string? hex, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        try
        {
            result = FromHexString(hex!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a binary string to a decimal value.
    /// Accepts formats: "0b1010", "1010"
    /// </summary>
    /// <param name="binary">The binary string to parse</param>
    /// <returns>Decimal value</returns>
    /// <exception cref="FormatException">If the string is not a valid binary number</exception>
    public static decimal FromBinaryString(string binary)
    {
        if (string.IsNullOrWhiteSpace(binary))
            throw new ArgumentException("Binary string cannot be null or empty", nameof(binary));

        binary = binary.Trim();
        if (binary.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            binary = binary.Substring(2);

        if (binary.Length == 0)
            throw new ArgumentException("Binary string contains no digits", nameof(binary));

        return Convert.ToInt64(binary, 2);
    }

    /// <summary>
    /// Attempts to parse a binary string to a decimal value.
    /// </summary>
    /// <param name="binary">The binary string to parse</param>
    /// <param name="result">The resulting decimal value</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryFromBinaryString(string? binary, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(binary))
            return false;

        try
        {
            result = FromBinaryString(binary!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses an octal string to a decimal value.
    /// Accepts formats: "0o755", "755"
    /// </summary>
    /// <param name="octal">The octal string to parse</param>
    /// <returns>Decimal value</returns>
    /// <exception cref="FormatException">If the string is not a valid octal number</exception>
    public static decimal FromOctalString(string octal)
    {
        if (string.IsNullOrWhiteSpace(octal))
            throw new ArgumentException("Octal string cannot be null or empty", nameof(octal));

        octal = octal.Trim();
        if (octal.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            octal = octal.Substring(2);

        if (octal.Length == 0)
            throw new ArgumentException("Octal string contains no digits", nameof(octal));

        return Convert.ToInt64(octal, 8);
    }

    /// <summary>
    /// Attempts to parse an octal string to a decimal value.
    /// </summary>
    /// <param name="octal">The octal string to parse</param>
    /// <param name="result">The resulting decimal value</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryFromOctalString(string? octal, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(octal))
            return false;

        try
        {
            result = FromOctalString(octal!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a color hex string to a decimal value.
    /// Accepts formats: "#FF5733", "FF5733", "#ABC" (short form), "ABC"
    /// </summary>
    /// <param name="colorHex">The color hex string to parse</param>
    /// <returns>Decimal value (0-16777215)</returns>
    /// <exception cref="FormatException">If the string is not a valid color hex</exception>
    public static decimal FromColorHexString(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            throw new ArgumentException("Color hex string cannot be null or empty", nameof(colorHex));

        colorHex = colorHex.Trim().TrimStart('#');

        // Handle short form (#RGB -> #RRGGBB)
        if (colorHex.Length == 3)
        {
            colorHex = $"{colorHex[0]}{colorHex[0]}{colorHex[1]}{colorHex[1]}{colorHex[2]}{colorHex[2]}";
        }

        if (colorHex.Length != 6)
            throw new FormatException($"Invalid color hex format: '{colorHex}'. Expected 6 characters (RRGGBB).");

        return Convert.ToInt64(colorHex, 16);
    }

    /// <summary>
    /// Attempts to parse a color hex string to a decimal value.
    /// </summary>
    /// <param name="colorHex">The color hex string to parse</param>
    /// <param name="result">The resulting decimal value</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryFromColorHexString(string? colorHex, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(colorHex))
            return false;

        try
        {
            result = FromColorHexString(colorHex!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses an IPv4 address string to a decimal value.
    /// Accepts format: "192.168.1.1"
    /// </summary>
    /// <param name="ipAddress">The IPv4 address string to parse</param>
    /// <returns>Decimal value (0-4294967295)</returns>
    /// <exception cref="FormatException">If the string is not a valid IPv4 address</exception>
    public static decimal FromIPAddressString(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new ArgumentException("IP address string cannot be null or empty", nameof(ipAddress));

        var parts = ipAddress.Trim().Split('.');
        if (parts.Length != 4)
            throw new FormatException($"Invalid IPv4 format: '{ipAddress}'. Expected 4 octets.");

        uint result = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!byte.TryParse(parts[i], out byte octet))
                throw new FormatException($"Invalid octet value: '{parts[i]}'. Expected 0-255.");

            result = (result << 8) | octet;
        }

        return result;
    }

    /// <summary>
    /// Attempts to parse an IPv4 address string to a decimal value.
    /// </summary>
    /// <param name="ipAddress">The IPv4 address string to parse</param>
    /// <param name="result">The resulting decimal value</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryFromIPAddressString(string? ipAddress, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        try
        {
            result = FromIPAddressString(ipAddress!);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
