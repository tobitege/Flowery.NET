using System;
using Avalonia.Media;
using Flowery.Controls;

namespace Flowery.Helpers
{
    /// <summary>
    /// Shadow math for neumorphic effects (top-left light source).
    /// </summary>
    internal static class DaisyNeumorphicHelper
    {
        /// <summary>
        /// Derives shadow offset and blur from the elevation. Explicit offset/blur overrides are used as-is;
        /// Flat mode uses 60% of both.
        /// </summary>
        internal static (double Offset, double Blur) GetShadowMetrics(
            double elevation,
            DaisyNeumorphicMode mode,
            double baseOffset,
            bool hasOffsetOverride,
            double baseBlur,
            bool hasBlurOverride)
        {
            var offset = hasOffsetOverride ? baseOffset : Math.Max(baseOffset, elevation * 0.35);
            var blur = hasBlurOverride ? baseBlur : Math.Max(baseBlur, offset * 1.5);

            if (mode == DaisyNeumorphicMode.Flat)
            {
                offset *= 0.6;
                blur *= 0.6;
            }

            return (offset, blur);
        }

        /// <summary>
        /// Builds the box shadows for a mode:
        /// Raised = dark bottom-right + light top-left, Inset = the same pair as inner shadows,
        /// Flat = dark bottom-right only, None = no shadow.
        /// </summary>
        internal static BoxShadows CreateShadows(
            DaisyNeumorphicMode mode,
            double intensity,
            double offset,
            double blur,
            Color darkColor,
            Color lightColor)
        {
            if (mode == DaisyNeumorphicMode.None)
                return default;

            var isInset = mode == DaisyNeumorphicMode.Inset;
            var dark = new BoxShadow
            {
                OffsetX = offset,
                OffsetY = offset,
                Blur = blur,
                Color = ApplyIntensity(darkColor, intensity),
                IsInset = isInset
            };

            if (mode == DaisyNeumorphicMode.Flat)
                return new BoxShadows(dark);

            var light = new BoxShadow
            {
                OffsetX = -offset,
                OffsetY = -offset,
                Blur = blur,
                Color = ApplyIntensity(lightColor, intensity),
                IsInset = isInset
            };

            return new BoxShadows(dark, [light]);
        }

        internal static Color ApplyIntensity(Color color, double intensity)
        {
            var alpha = (byte)Math.Clamp(color.A * intensity, 0, 255);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        /// <summary>
        /// Darkens a color by a factor (0.0 = unchanged, 1.0 = black). The result is fully opaque.
        /// </summary>
        internal static Color Darken(Color color, double factor)
        {
            var keep = 1.0 - Math.Clamp(factor, 0.0, 1.0);
            return Color.FromRgb(
                (byte)Math.Round(color.R * keep),
                (byte)Math.Round(color.G * keep),
                (byte)Math.Round(color.B * keep));
        }
    }
}
