using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Flowery.Theming
{
    /// <summary>
    /// Converts colors between different color spaces.
    /// Primarily used to convert DaisyUI's OKLCH colors to hex format.
    /// </summary>
    public static class ColorConverter
    {
        /// <summary>
        /// Convert an OKLCH color string to hex format.
        /// </summary>
        /// <param name="oklchValue">OKLCH value string, e.g., "65.69% 0.196 275.75" or "0.6569 0.196 275.75"</param>
        /// <returns>Hex color string, e.g., "#5B21B6"</returns>
        public static string OklchToHex(string oklchValue)
        {
            var (l, c, h) = ParseOklch(oklchValue);
            var (r, g, b) = OklchToRgb(l, c, h);
            return RgbToHex(r, g, b);
        }

        /// <summary>
        /// Parse OKLCH string into L, C, H components.
        /// Supports formats: "65.69% 0.196 275.75" or "0.6569 0.196 275.75"
        /// </summary>
        private static (double L, double C, double H) ParseOklch(string value)
        {
            var parts = value.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                throw new ArgumentException($"Invalid OKLCH format: '{value}'. Expected 3 components.");

            // Parse L (lightness) - may be percentage or decimal
            var lStr = parts[0].Trim();
            double l;
            if (lStr.EndsWith("%"))
            {
                l = double.Parse(lStr.TrimEnd('%'), CultureInfo.InvariantCulture) / 100.0;
            }
            else
            {
                l = double.Parse(lStr, CultureInfo.InvariantCulture);
                if (l > 1.0) l /= 100.0; // Assume percentage if > 1
            }

            // Parse C (chroma)
            var c = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);

            // Parse H (hue) - degrees
            var hStr = parts[2].Trim();
            var h = double.Parse(hStr.Replace("deg", ""), CultureInfo.InvariantCulture);

            return (l, c, h);
        }

        /// <summary>
        /// Convert OKLCH to RGB (0-255 range).
        /// </summary>
        private static (int R, int G, int B) OklchToRgb(double l, double c, double h)
        {
            // Convert OKLCH to OKLab
            var hRad = h * Math.PI / 180.0;
            var labA = c * Math.Cos(hRad);
            var labB = c * Math.Sin(hRad);

            // Convert OKLab to linear RGB
            var (linR, linG, linB) = OklabToLinearRgb(l, labA, labB);

            // Convert linear RGB to sRGB
            var r = LinearToSrgb(linR);
            var g = LinearToSrgb(linG);
            var b = LinearToSrgb(linB);

            // Clamp and convert to 0-255
            return (
                Clamp((int)Math.Round(r * 255)),
                Clamp((int)Math.Round(g * 255)),
                Clamp((int)Math.Round(b * 255))
            );
        }

        /// <summary>
        /// Convert OKLab to linear RGB.
        /// </summary>
        private static (double R, double G, double B) OklabToLinearRgb(double l, double a, double b)
        {
            // OKLab to LMS
            var l_ = l + 0.3963377774 * a + 0.2158037573 * b;
            var m_ = l - 0.1055613458 * a - 0.0638541728 * b;
            var s_ = l - 0.0894841775 * a - 1.2914855480 * b;

            var lCubed = l_ * l_ * l_;
            var mCubed = m_ * m_ * m_;
            var sCubed = s_ * s_ * s_;

            // LMS to linear RGB
            var r = +4.0767416621 * lCubed - 3.3077115913 * mCubed + 0.2309699292 * sCubed;
            var g = -1.2684380046 * lCubed + 2.6097574011 * mCubed - 0.3413193965 * sCubed;
            var bOut = -0.0041960863 * lCubed - 0.7034186147 * mCubed + 1.7076147010 * sCubed;

            return (r, g, bOut);
        }

        /// <summary>
        /// Convert linear RGB component to sRGB (gamma correction).
        /// </summary>
        private static double LinearToSrgb(double linear)
        {
            if (linear <= 0.0031308)
                return 12.92 * linear;
            return 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
        }

        /// <summary>
        /// Clamp integer to 0-255 range.
        /// </summary>
        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return value;
        }

        /// <summary>
        /// Convert RGB components to hex string.
        /// </summary>
        private static string RgbToHex(int r, int g, int b)
        {
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>
        /// Parse a hex color string to RGB components.
        /// </summary>
        /// <param name="hex">Hex color string, e.g., "#5B21B6" or "5B21B6"</param>
        /// <returns>RGB tuple (0-255 range)</returns>
        public static (int R, int G, int B) HexToRgb(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }
            if (hex.Length != 6)
                throw new ArgumentException($"Invalid hex color: '{hex}'");

            return (
                int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
                int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber)
            );
        }
    }
}
