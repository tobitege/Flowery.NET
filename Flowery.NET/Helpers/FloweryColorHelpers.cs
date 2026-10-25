using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Media;

namespace Flowery.Helpers
{
    /// <summary>
    /// Static helper class for color manipulation and parsing.
    /// Provides utilities for parsing hex colors, computing luminance,
    /// darkening/lightening colors, and determining contrast colors.
    /// </summary>
    public static class FloweryColorHelpers
    {
        private static readonly Lazy<Dictionary<string, Color>> NamedColorLookup =
            new(BuildNamedColorLookup, true);

        /// <summary>
        /// Parses a color string to a Color struct.
        /// Supports #RGB, #ARGB, #RRGGBB, #AARRGGBB, and named colors.
        /// </summary>
        /// <param name="input">The color string (with or without # prefix).</param>
        /// <param name="color">The parsed color.</param>
        /// <returns>True if parsed successfully; otherwise false.</returns>
        public static bool TryParseColor(string input, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var value = input.Trim();

            if (TryParseHexColor(value, out color))
            {
                return true;
            }

            if (value.StartsWith("#") && TryParseHexColor(value.Substring(1), out color))
            {
                return true;
            }

            return NamedColorLookup.Value.TryGetValue(value, out color);
        }

        /// <summary>
        /// Parses a color string to a Color struct.
        /// Supports #RGB, #ARGB, #RRGGBB, #AARRGGBB, and named colors.
        /// </summary>
        /// <param name="hex">The color string (with or without # prefix).</param>
        /// <returns>The parsed Color, or transparent black if parsing fails.</returns>
        public static Color ColorFromHex(string hex)
        {
            return TryParseColor(hex, out var color) ? color : Colors.Transparent;
        }

        /// <summary>
        /// Converts a Color to a hex string (format: #RRGGBB or #AARRGGBB if alpha is not 255).
        /// </summary>
        public static string ColorToHex(Color color)
        {
            return ColorToHex(color, color.A != 255);
        }

        /// <summary>
        /// Converts a Color to a hex string with optional alpha.
        /// </summary>
        public static string ColorToHex(Color color, bool includeAlpha)
        {
            return includeAlpha
                ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Computes the perceived brightness of a color (0.0 to 1.0) with the Rec. 601 weights on the gamma-encoded
        /// channels. For WCAG contrast use <see cref="GetRelativeLuminance"/> and <see cref="GetContrastRatio"/>.
        /// </summary>
        public static double GetLuminance(Color color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        }

        /// <summary>
        /// Computes the relative luminance from a hex color string.
        /// </summary>
        public static double GetLuminance(string hex)
        {
            return GetLuminance(ColorFromHex(hex));
        }

        /// <summary>
        /// Computes the CIE76 color difference (Delta E) between two opaque colors in the CIELAB space:
        /// 0 for identical colors, about 2.3 for a just noticeable difference and 100 for black against white.
        /// Unlike a luminance contrast ratio it also counts hue and saturation differences.
        /// </summary>
        public static double GetColorDifference(Color first, Color second)
        {
            var (firstL, firstA, firstB) = ToLab(first);
            var (secondL, secondA, secondB) = ToLab(second);
            var deltaL = firstL - secondL;
            var deltaA = firstA - secondA;
            var deltaB = firstB - secondB;
            return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
        }

        /// <summary>
        /// Computes the WCAG 2 relative luminance of an opaque color: 0 for black and 1 for white.
        /// </summary>
        public static double GetRelativeLuminance(Color color)
        {
            return 0.2126 * ToLinear(color.R) + 0.7152 * ToLinear(color.G) + 0.0722 * ToLinear(color.B);
        }

        /// <summary>
        /// Computes the WCAG 2 contrast ratio between two opaque colors: 1 for equal colors and 21 for black on white.
        /// WCAG asks for 4.5 for normal text and 3 for large or bold text.
        /// </summary>
        public static double GetContrastRatio(Color first, Color second)
        {
            var firstLuminance = GetRelativeLuminance(first);
            var secondLuminance = GetRelativeLuminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
        }

        /// <summary>
        /// Returns <paramref name="text"/> with at least <paramref name="ratio"/> contrast against the opaque
        /// <paramref name="background"/>, as drawn: a translucent text color is checked as it renders over the
        /// background. A color that has the contrast already comes back unchanged; otherwise it is mixed toward white
        /// or black, whichever contrasts more with the background, only as far as needed, so its hue stays. The alpha is
        /// kept while that is enough, otherwise the color becomes opaque. When even opaque white or black cannot reach
        /// the ratio, that color is returned.
        /// </summary>
        public static Color EnsureContrast(Color text, Color background, double ratio)
        {
            var opaqueText = Color.FromRgb(text.R, text.G, text.B);
            var alpha = text.A / 255.0;
            if (RenderedContrast(opaqueText, alpha, background) >= ratio)
                return text;

            var target = GetContrastRatio(Colors.White, background) >= GetContrastRatio(Colors.Black, background)
                ? Colors.White
                : Colors.Black;
            if (RenderedContrast(target, alpha, background) < ratio)
            {
                return text.A < 255
                    ? EnsureContrast(Color.FromRgb(text.R, text.G, text.B), background, ratio)
                    : target;
            }

            // On the way to white or black the contrast may first drop, while the text passes the background's
            // luminance, and then only grows; it stays below the start, and so below the ratio, until it grows. The
            // mixes that reach the ratio therefore form one range up to white or black, whose start halving finds.
            double low = 0, high = 1;
            for (var i = 0; i < 16; i++)
            {
                var middle = (low + high) / 2;
                if (RenderedContrast(Mix(opaqueText, target, middle), alpha, background) >= ratio)
                    high = middle;
                else
                    low = middle;
            }
            var mixed = Mix(opaqueText, target, high);
            return Color.FromArgb(text.A, mixed.R, mixed.G, mixed.B);
        }

        private static double RenderedContrast(Color opaqueText, double alpha, Color background) =>
            GetContrastRatio(Mix(Color.FromRgb(background.R, background.G, background.B), opaqueText, alpha), background);

        /// <summary>
        /// Paints <paramref name="over"/> at <paramref name="opacity"/>, times its own alpha, over the opaque
        /// <paramref name="under"/> and returns the opaque result.
        /// </summary>
        public static Color Composite(Color under, Color over, double opacity)
        {
            var alpha = Math.Clamp(opacity, 0, 1) * over.A / 255.0;
            return Mix(Color.FromRgb(under.R, under.G, under.B), Color.FromRgb(over.R, over.G, over.B), alpha);
        }

        private static Color Mix(Color from, Color to, double amount)
        {
            static byte Channel(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
            return Color.FromRgb(Channel(from.R, to.R, amount), Channel(from.G, to.G, amount), Channel(from.B, to.B, amount));
        }

        private static (double L, double A, double B) ToLab(Color color)
        {
            var r = ToLinear(color.R);
            var g = ToLinear(color.G);
            var b = ToLinear(color.B);

            // sRGB to CIE XYZ, relative to the D65 white point.
            var x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
            var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            var z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;

            var fx = ToLabComponent(x);
            var fy = ToLabComponent(y);
            var fz = ToLabComponent(z);
            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        private static double ToLabComponent(double value)
        {
            return value > 216.0 / 24389.0 ? Math.Cbrt(value) : (24389.0 / 27.0 * value + 16) / 116;
        }

        private static double ToLinear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        /// <summary>
        /// Determines if a color is considered "dark" (luminance below 0.5).
        /// </summary>
        public static bool IsDark(Color color)
        {
            return GetLuminance(color) < 0.5;
        }

        /// <summary>
        /// Determines if a hex color is considered "dark".
        /// </summary>
        public static bool IsDark(string hex)
        {
            return GetLuminance(hex) < 0.5;
        }

        /// <summary>
        /// Returns a contrasting text color (black or white) for the given background.
        /// </summary>
        public static Color GetContrastColor(Color background)
        {
            return IsDark(background) ? Colors.White : Colors.Black;
        }

        /// <summary>
        /// Returns a contrasting text color hex string (#000000 or #FFFFFF) for the given background.
        /// </summary>
        public static string GetContrastColorHex(string backgroundHex)
        {
            return IsDark(backgroundHex) ? "#FFFFFF" : "#000000";
        }

        /// <summary>
        /// Darkens a color by the specified factor (0.0 to 1.0).
        /// A factor of 0.2 makes the color 20% darker.
        /// </summary>
        public static Color Darken(Color color, double factor)
        {
            factor = Clamp(factor, 0.0, 1.0);
            var r = (byte)(color.R * (1 - factor));
            var g = (byte)(color.G * (1 - factor));
            var b = (byte)(color.B * (1 - factor));
            return Color.FromUInt32((uint)((color.A << 24) | (r << 16) | (g << 8) | b));
        }

        /// <summary>
        /// Darkens a hex color by the specified factor and returns the result as a hex string.
        /// </summary>
        public static string DarkenHex(string hex, double factor)
        {
            return ColorToHex(Darken(ColorFromHex(hex), factor));
        }

        /// <summary>
        /// Lightens a color by the specified factor (0.0 to 1.0).
        /// A factor of 0.2 makes the color 20% lighter (towards white).
        /// </summary>
        public static Color Lighten(Color color, double factor)
        {
            factor = Clamp(factor, 0.0, 1.0);
            var r = (byte)(color.R + (255 - color.R) * factor);
            var g = (byte)(color.G + (255 - color.G) * factor);
            var b = (byte)(color.B + (255 - color.B) * factor);
            return Color.FromUInt32((uint)((color.A << 24) | (r << 16) | (g << 8) | b));
        }

        /// <summary>
        /// Lightens a hex color by the specified factor and returns the result as a hex string.
        /// </summary>
        public static string LightenHex(string hex, double factor)
        {
            return ColorToHex(Lighten(ColorFromHex(hex), factor));
        }

        /// <summary>
        /// Adjusts a color's brightness. Positive factor lightens, negative factor darkens.
        /// </summary>
        /// <param name="hex">The hex color string.</param>
        /// <param name="factor">Adjustment factor. Positive = lighten, Negative = darken.</param>
        /// <returns>The adjusted hex color string.</returns>
        public static string AdjustBrightness(string hex, double factor)
        {
            var color = ColorFromHex(hex);
            int r, g, b;

            if (factor >= 0)
            {
                r = (int)(color.R + (255 - color.R) * factor);
                g = (int)(color.G + (255 - color.G) * factor);
                b = (int)(color.B + (255 - color.B) * factor);
            }
            else
            {
                var absFactor = Math.Abs(factor);
                r = (int)(color.R * (1 - absFactor));
                g = (int)(color.G * (1 - absFactor));
                b = (int)(color.B * (1 - absFactor));
            }

            r = Clamp(r, 0, 255);
            g = Clamp(g, 0, 255);
            b = Clamp(b, 0, 255);

            return $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// Blends two colors together by the specified ratio.
        /// </summary>
        /// <param name="color1">The first color.</param>
        /// <param name="color2">The second color.</param>
        /// <param name="ratio">Blend ratio (0.0 = all color1, 1.0 = all color2).</param>
        /// <returns>The blended color.</returns>
        public static Color Blend(Color color1, Color color2, double ratio)
        {
            ratio = Clamp(ratio, 0.0, 1.0);
            var inverseRatio = 1.0 - ratio;

            var a = (byte)(color1.A * inverseRatio + color2.A * ratio);
            var r = (byte)(color1.R * inverseRatio + color2.R * ratio);
            var g = (byte)(color1.G * inverseRatio + color2.G * ratio);
            var b = (byte)(color1.B * inverseRatio + color2.B * ratio);

            return Color.FromUInt32((uint)((a << 24) | (r << 16) | (g << 8) | b));
        }

        /// <summary>
        /// Blends two hex colors together by the specified ratio.
        /// </summary>
        public static string BlendHex(string hex1, string hex2, double ratio)
        {
            return ColorToHex(Blend(ColorFromHex(hex1), ColorFromHex(hex2), ratio));
        }

        /// <summary>
        /// Applies an opacity to a color.
        /// </summary>
        /// <param name="color">The color to modify.</param>
        /// <param name="opacity">The opacity (0.0 to 1.0).</param>
        /// <returns>The color with the new alpha value.</returns>
        public static Color WithOpacity(Color color, double opacity)
        {
            opacity = Clamp(opacity, 0.0, 1.0);
            return Color.FromUInt32((uint)(((byte)(255 * opacity) << 24) | (color.R << 16) | (color.G << 8) | color.B));
        }

        /// <summary>
        /// Applies an opacity to a hex color.
        /// </summary>
        public static string WithOpacityHex(string hex, double opacity)
        {
            return ColorToHex(WithOpacity(ColorFromHex(hex), opacity));
        }

        private static bool TryParseHexColor(string value, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (value.StartsWith("#"))
            {
                value = value.Substring(1);
            }

            if (value.Length is 3 or 4)
            {
                if (!TryParseNibble(value[0], out var n0) ||
                    !TryParseNibble(value[1], out var n1) ||
                    !TryParseNibble(value[2], out var n2))
                {
                    return false;
                }

                byte a = 255;
                byte r;
                byte g;
                byte b;

                if (value.Length == 4)
                {
                    if (!TryParseNibble(value[3], out var n3))
                    {
                        return false;
                    }

                    a = (byte)((n0 << 4) + n0);
                    r = (byte)((n1 << 4) + n1);
                    g = (byte)((n2 << 4) + n2);
                    b = (byte)((n3 << 4) + n3);
                }
                else
                {
                    r = (byte)((n0 << 4) + n0);
                    g = (byte)((n1 << 4) + n1);
                    b = (byte)((n2 << 4) + n2);
                }

                color = Color.FromUInt32((uint)((a << 24) | (r << 16) | (g << 8) | b));
                return true;
            }

            if (value.Length is 6 or 8)
            {
                var hasAlpha = value.Length == 8;
                int start = 0;
                byte a = 255;

                if (hasAlpha)
                {
                    if (!TryParseByte(value, 0, out a))
                    {
                        return false;
                    }
                    start = 2;
                }

                if (!TryParseByte(value, start, out var r) ||
                    !TryParseByte(value, start + 2, out var g) ||
                    !TryParseByte(value, start + 4, out var b))
                {
                    return false;
                }

                color = Color.FromUInt32((uint)((a << 24) | (r << 16) | (g << 8) | b));
                return true;
            }

            return false;
        }

        private static bool TryParseByte(string value, int start, out byte result)
        {
            result = 0;
            if (start + 1 >= value.Length)
            {
                return false;
            }

            if (!TryParseNibble(value[start], out var hi) || !TryParseNibble(value[start + 1], out var lo))
            {
                return false;
            }

            result = (byte)((hi << 4) + lo);
            return true;
        }

        private static bool TryParseNibble(char c, out int value)
        {
            value = 0;
            if (c is >= '0' and <= '9')
            {
                value = c - '0';
                return true;
            }

            if (c is >= 'a' and <= 'f')
            {
                value = c - 'a' + 10;
                return true;
            }

            if (c is >= 'A' and <= 'F')
            {
                value = c - 'A' + 10;
                return true;
            }

            return false;
        }

        private static Dictionary<string, Color> BuildNamedColorLookup()
        {
            var dict = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            var properties = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static);

            foreach (var property in properties)
            {
                if (property.PropertyType != typeof(Color) || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                if (property.GetValue(null) is Color color)
                {
                    dict[property.Name] = color;
                }
            }

            return dict;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
