using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Flowery.Controls
{
    /// <summary>
    /// Information about a DaisyUI theme.
    /// </summary>
    public class DaisyThemeInfo
    {
        public string Name { get; }
        public bool IsDark { get; }

        public DaisyThemeInfo(string name, bool isDark)
        {
            Name = name;
            IsDark = isDark;
        }
    }

    /// <summary>
    /// Centralized theme manager for DaisyUI themes.
    /// Handles loading and applying theme palettes app-wide.
    /// </summary>
    public static class DaisyThemeManager
    {
        private static ResourceDictionary? _currentPalette;
        private static string? _currentThemeName;
        private static string _baseThemeName = "Dark";
        private static readonly Dictionary<string, DaisyThemeInfo> _themesByName;

        /// <summary>
        /// Optional custom theme applicator. When set, this delegate is called
        /// instead of the default MergedDictionaries approach. The delegate receives
        /// the theme name and should return true if the theme was applied successfully.
        /// </summary>
        /// <remarks>
        /// Use this when your app requires a different theme application strategy,
        /// such as in-place ThemeDictionary updates or persisting theme settings.
        /// After calling, DaisyThemeManager automatically updates internal state
        /// and fires ThemeChanged.
        /// </remarks>
        /// <example>
        /// <code>
        /// // In App.axaml.cs OnFrameworkInitializationCompleted:
        /// DaisyThemeManager.CustomThemeApplicator = themeName =>
        /// {
        ///     // Custom in-place update logic
        ///     var themeInfo = DaisyThemeManager.GetThemeInfo(themeName);
        ///     if (themeInfo == null) return false;
        ///
        ///     // ... apply theme your way ...
        ///     AppSettings.Current.DaisyUiTheme = themeName;
        ///     return true;
        /// };
        /// </code>
        /// </example>
        public static Func<string, bool>? CustomThemeApplicator { get; set; }

        /// <summary>
        /// All available DaisyUI themes.
        /// </summary>
        public static readonly ReadOnlyCollection<DaisyThemeInfo> AvailableThemes;

        static DaisyThemeManager()
        {
            var themes = new List<DaisyThemeInfo>
            {
                new DaisyThemeInfo("Abyss", true),
                new DaisyThemeInfo("Acid", false),
                new DaisyThemeInfo("Aqua", true),
                new DaisyThemeInfo("Autumn", false),
                new DaisyThemeInfo("Black", true),
                new DaisyThemeInfo("Bumblebee", false),
                new DaisyThemeInfo("Business", true),
                new DaisyThemeInfo("Caramellatte", false),
                new DaisyThemeInfo("Cmyk", false),
                new DaisyThemeInfo("Coffee", true),
                new DaisyThemeInfo("Corporate", false),
                new DaisyThemeInfo("Cupcake", false),
                new DaisyThemeInfo("Cyberpunk", false),
                new DaisyThemeInfo("Dark", true),
                new DaisyThemeInfo("Dim", true),
                new DaisyThemeInfo("Dracula", true),
                new DaisyThemeInfo("Emerald", false),
                new DaisyThemeInfo("Fantasy", false),
                new DaisyThemeInfo("Forest", true),
                new DaisyThemeInfo("Garden", false),
                new DaisyThemeInfo("Halloween", true),
                new DaisyThemeInfo("Lemonade", false),
                new DaisyThemeInfo("Light", false),
                new DaisyThemeInfo("Lofi", false),
                new DaisyThemeInfo("Luxury", true),
                new DaisyThemeInfo("Night", true),
                new DaisyThemeInfo("Nord", false),
                new DaisyThemeInfo("Pastel", false),
                new DaisyThemeInfo("Retro", false),
                new DaisyThemeInfo("Silk", false),
                new DaisyThemeInfo("Sunset", true),
                new DaisyThemeInfo("Synthwave", true),
                new DaisyThemeInfo("Valentine", false),
                new DaisyThemeInfo("Winter", false),
                new DaisyThemeInfo("Wireframe", false),
            };

            AvailableThemes = new ReadOnlyCollection<DaisyThemeInfo>(themes);
            _themesByName = new Dictionary<string, DaisyThemeInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var theme in themes)
            {
                _themesByName[theme.Name] = theme;
            }
        }

        /// <summary>
        /// Event raised when the theme changes.
        /// </summary>
        public static event EventHandler<string>? ThemeChanged;

        /// <summary>
        /// Gets the currently active theme name.
        /// </summary>
        public static string? CurrentThemeName => _currentThemeName;

        /// <summary>
        /// Gets or sets the base/default theme name (used by theme controllers as the "unchecked" theme).
        /// Defaults to "Light".
        /// </summary>
        public static string BaseThemeName
        {
            get => _baseThemeName;
            set => _baseThemeName = value ?? "Light";
        }

        /// <summary>
        /// Gets the "alternate" theme - the current theme if it's not the base theme,
        /// otherwise returns "Dark" as a fallback.
        /// </summary>
        public static string AlternateThemeName
        {
            get
            {
                if (_currentThemeName != null &&
                    !string.Equals(_currentThemeName, _baseThemeName, StringComparison.OrdinalIgnoreCase))
                {
                    return _currentThemeName;
                }
                return "Dark";
            }
        }

        /// <summary>
        /// Gets theme info by name, or null if not found.
        /// </summary>
        public static DaisyThemeInfo? GetThemeInfo(string themeName)
        {
            return _themesByName.TryGetValue(themeName, out var info) ? info : null;
        }

        /// <summary>
        /// Apply a theme by name.
        /// </summary>
        /// <param name="themeName">The theme name (e.g., "Light", "Dark", "Synthwave").</param>
        /// <returns>True if the theme was applied successfully.</returns>
        public static bool ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                return false;

            var themeInfo = GetThemeInfo(themeName);
            if (themeInfo == null)
            {
                System.Diagnostics.Debug.WriteLine($"Unknown theme: {themeName}");
                return false;
            }

            // Skip if already applied
            if (string.Equals(_currentThemeName, themeInfo.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            // Use custom applicator if set
            if (CustomThemeApplicator != null)
            {
                var result = CustomThemeApplicator(themeName);
                if (result)
                {
                    SetCurrentTheme(themeInfo.Name);
                }
                return result;
            }

            // Default: use MergedDictionaries approach
            var app = Application.Current;
            if (app == null) return false;

            var paletteUri = new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{themeInfo.Name}.axaml");

            try
            {
                var newPalette = (ResourceDictionary)AvaloniaXamlLoader.Load(paletteUri);

                // Remove old palette if exists
                if (_currentPalette != null && app.Resources.MergedDictionaries.Contains(_currentPalette))
                {
                    app.Resources.MergedDictionaries.Remove(_currentPalette);
                }

                // Add new palette
                app.Resources.MergedDictionaries.Add(newPalette);
                _currentPalette = newPalette;
                _currentThemeName = themeInfo.Name;

                // Set light/dark variant for system controls
                app.RequestedThemeVariant = themeInfo.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

                // Notify listeners
                ThemeChanged?.Invoke(null, themeInfo.Name);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme {themeName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the current theme name and fires the ThemeChanged event.
        /// Used by custom theme applicators to update internal state after applying a theme.
        /// </summary>
        /// <param name="themeName">The theme name that was applied.</param>
        public static void SetCurrentTheme(string themeName)
        {
            if (string.Equals(_currentThemeName, themeName, StringComparison.OrdinalIgnoreCase))
                return;

            _currentThemeName = themeName;
            ThemeChanged?.Invoke(null, themeName);
        }

        /// <summary>
        /// Check if a theme is a dark theme.
        /// </summary>
        public static bool IsDarkTheme(string themeName)
        {
            var info = GetThemeInfo(themeName);
            return info?.IsDark ?? false;
        }
    }
}
