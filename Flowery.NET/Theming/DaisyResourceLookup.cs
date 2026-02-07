using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Flowery.Controls;

namespace Flowery.Theming
{
    /// <summary>
    /// Centralized helpers for Daisy palette detection and lookup.
    /// </summary>
    public static class DaisyResourceLookup
    {
        /// <summary>
        /// All known Daisy palette names used for background/content brush pairs.
        /// </summary>
        private static readonly string[] PaletteNames =
        {
            "Primary", "Secondary", "Accent", "Neutral",
            "Info", "Success", "Warning", "Error",
            "Base100", "Base200", "Base300"
        };

        /// <summary>
        /// Cache of color values to palette names. Invalidated on theme change.
        /// </summary>
        private static readonly Dictionary<Color, string> ColorToPaletteCache = new();
        private static string? _cachedThemeName;

        /// <summary>
        /// Gets the Daisy palette name (e.g., "Primary", "Secondary") for a given color.
        /// Returns null if the color doesn't match any known palette brush.
        /// </summary>
        /// <param name="color">The color to match against current theme palette brushes.</param>
        /// <returns>The palette name if found, or null if no match.</returns>
        public static string? GetPaletteNameForColor(Color color)
        {
            var currentTheme = DaisyThemeManager.CurrentThemeName;
            if (!string.Equals(_cachedThemeName, currentTheme, StringComparison.Ordinal))
            {
                ColorToPaletteCache.Clear();
                _cachedThemeName = currentTheme;
            }

            if (ColorToPaletteCache.TryGetValue(color, out var cached))
            {
                return cached;
            }

            foreach (var name in PaletteNames)
            {
                var brush = GetBrush($"Daisy{name}Brush");
                if (brush is ISolidColorBrush solid && solid.Color == color)
                {
                    ColorToPaletteCache[color] = name;
                    return name;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the background and content brushes for a given palette name.
        /// </summary>
        /// <param name="paletteName">The palette name (e.g., "Primary", "Secondary", "Base200").</param>
        /// <returns>A tuple containing the background brush and content brush for the palette.</returns>
        public static (IBrush? background, IBrush? content) GetPaletteBrushes(string? paletteName)
        {
            if (string.IsNullOrWhiteSpace(paletteName))
            {
                return (null, null);
            }

            var palette = paletteName!;
            var background = GetBrush($"Daisy{palette}Brush");
            var contentKey = palette.StartsWith("Base", StringComparison.OrdinalIgnoreCase)
                ? "DaisyBaseContentBrush"
                : $"Daisy{palette}ContentBrush";
            var content = GetBrush(contentKey);

            return (background, content);
        }

        /// <summary>
        /// Gets the background and content brushes for a given color by detecting its palette.
        /// </summary>
        /// <param name="color">The color to match against current theme palette brushes.</param>
        /// <returns>A tuple containing the background brush and content brush, or (null, null) if not found.</returns>
        public static (IBrush? background, IBrush? content) GetPaletteBrushesForColor(Color color)
        {
            var paletteName = GetPaletteNameForColor(color);
            return GetPaletteBrushes(paletteName);
        }

        public static IBrush? GetBrush(string key)
        {
            var app = Application.Current;
            if (app == null)
            {
                return null;
            }

            if (app.TryGetResource(key, null, out var value) && value is IBrush brush)
            {
                return brush;
            }

            return null;
        }
    }
}
