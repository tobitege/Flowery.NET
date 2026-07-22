using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Flowery.Theming
{
    /// <summary>
    /// Runtime theme loader that can parse DaisyUI CSS and apply themes dynamically.
    /// This is a helper utility - not wired up to the existing theme system.
    /// </summary>
    public class DaisyThemeLoader
    {
        private readonly Dictionary<string, DaisyUiTheme> _loadedThemes = new Dictionary<string, DaisyUiTheme>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the currently loaded themes.
        /// </summary>
        public IReadOnlyDictionary<string, DaisyUiTheme> LoadedThemes => _loadedThemes;

        /// <summary>
        /// Load a theme from a CSS file.
        /// </summary>
        /// <param name="filePath">Path to the DaisyUI CSS file.</param>
        /// <returns>The parsed theme.</returns>
        public DaisyUiTheme LoadFromFile(string filePath)
        {
            var theme = DaisyUiCssParser.ParseFile(filePath);
            _loadedThemes[theme.Name] = theme;
            return theme;
        }

        /// <summary>
        /// Load a theme from CSS content string.
        /// </summary>
        /// <param name="cssContent">Raw CSS content.</param>
        /// <param name="themeName">Name for the theme.</param>
        /// <returns>The parsed theme.</returns>
        public DaisyUiTheme LoadFromString(string cssContent, string themeName)
        {
            var theme = DaisyUiCssParser.Parse(cssContent, themeName);
            _loadedThemes[theme.Name] = theme;
            return theme;
        }

        /// <summary>
        /// Load all CSS files from a directory as themes.
        /// </summary>
        /// <param name="directoryPath">Directory containing CSS files.</param>
        /// <returns>List of loaded themes.</returns>
        public List<DaisyUiTheme> LoadFromDirectory(string directoryPath)
        {
            var themes = new List<DaisyUiTheme>();
            foreach (var file in Directory.GetFiles(directoryPath, "*.css"))
            {
                try
                {
                    var theme = LoadFromFile(file);
                    themes.Add(theme);
                }
                catch
                {
                    // Skip files that fail to parse
                }
            }
            return themes;
        }

        /// <summary>
        /// Get a loaded theme by name.
        /// </summary>
        /// <param name="themeName">Theme name (case-insensitive).</param>
        /// <returns>The theme, or null if not found.</returns>
        public DaisyUiTheme? GetTheme(string themeName)
        {
            return _loadedThemes.TryGetValue(themeName, out var theme) ? theme : null;
        }

        /// <summary>
        /// Apply a theme's colors to a resource dictionary.
        /// Creates or updates Color and SolidColorBrush resources.
        /// </summary>
        /// <param name="theme">The theme to apply.</param>
        /// <param name="resources">The resource dictionary to update.</param>
        public static void ApplyTheme(DaisyUiTheme theme, IResourceDictionary resources)
        {
            foreach (var kvp in theme.Colors)
            {
                var avaloniaKey = DaisyUiAxamlGenerator.GetAvaloniaKey(kvp.Key);
                var color = Color.Parse(kvp.Value);

                // Update or add Color resource
                var colorKey = avaloniaKey + "Color";
                resources[colorKey] = color;

                // Update or add Brush resource
                var brushKey = avaloniaKey + "Brush";
                resources[brushKey] = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Apply a theme and set the appropriate theme variant (Light/Dark).
        /// This injects the theme colors into the appropriate ThemeDictionary.
        /// </summary>
        /// <param name="theme">The theme to apply.</param>
        public static void ApplyThemeToApplication(DaisyUiTheme theme)
        {
            var app = Application.Current;
            if (app?.Resources == null) return;

            // Determine target theme variant
            var targetVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

            // Try to find and update the theme dictionary
            if (app.Resources.ThemeDictionaries.TryGetValue(targetVariant, out var themeDict) && themeDict is IResourceDictionary dict)
            {
                ApplyTheme(theme, dict);
            }
            else
            {
                // Fallback: apply to main resources
                ApplyTheme(theme, app.Resources);
            }

            // Switch to the theme variant
            app.RequestedThemeVariant = targetVariant;
        }

        /// <summary>
        /// Generate AXAML content for a theme (for saving to disk).
        /// </summary>
        /// <param name="theme">The theme to export.</param>
        /// <returns>AXAML ResourceDictionary content.</returns>
        public static string ExportToAxaml(DaisyUiTheme theme)
        {
            return DaisyUiAxamlGenerator.Generate(theme);
        }

        /// <summary>
        /// Generate combined AXAML with Light/Dark theme dictionaries.
        /// </summary>
        /// <param name="lightTheme">Light theme.</param>
        /// <param name="darkTheme">Dark theme.</param>
        /// <returns>AXAML ResourceDictionary content with ThemeDictionaries.</returns>
        public static string ExportCombinedToAxaml(DaisyUiTheme lightTheme, DaisyUiTheme darkTheme)
        {
            return DaisyUiAxamlGenerator.GenerateCombined(lightTheme, darkTheme);
        }
    }
}
