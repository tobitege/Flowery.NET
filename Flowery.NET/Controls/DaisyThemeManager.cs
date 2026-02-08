using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// Information about a DaisyUI theme.
    /// </summary>
    public class DaisyThemeInfo(string name, bool isDark)
    {
        public string Name { get; } = name;
        public bool IsDark { get; } = isDark;
    }

    /// <summary>
    /// Centralized theme manager for DaisyUI themes (flowery-net).
    /// Swaps a single palette ResourceDictionary into Application.Resources.MergedDictionaries
    /// and raises ThemeChanged.
    /// </summary>
    public static class DaisyThemeManager
    {
        private sealed class ThemeDefinition(DaisyThemeInfo info, Func<ResourceDictionary> paletteFactory)
        {
            public DaisyThemeInfo Info { get; } = info;
            public Func<ResourceDictionary> PaletteFactory { get; } = paletteFactory;
        }

        private static readonly Dictionary<string, ThemeDefinition> ThemesByName =
            new(StringComparer.OrdinalIgnoreCase);

        private static ResourceDictionary? _currentPalette;
        private static string? _currentThemeName;
        private static string _baseThemeName = "Dark";

        /// <summary>
        /// When true, ApplyTheme calls only update internal state without actually applying the theme.
        /// </summary>
        public static bool SuppressThemeApplication { get; set; }

        /// <summary>
        /// Optional custom theme applicator. When set, this delegate is called
        /// instead of the default MergedDictionaries approach.
        /// </summary>
        public static Func<string, bool>? CustomThemeApplicator { get; set; }

        /// <summary>
        /// All available DaisyUI themes.
        /// </summary>
        public static ReadOnlyCollection<DaisyThemeInfo> AvailableThemes { get; private set; } =
            new([]);

        static DaisyThemeManager()
        {
            // Register all standard DaisyUI themes
            var standardThemes = new[]
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
                new DaisyThemeInfo("Smooth", true),
                new DaisyThemeInfo("Sunset", true),
                new DaisyThemeInfo("Synthwave", true),
                new DaisyThemeInfo("Valentine", false),
                new DaisyThemeInfo("Winter", false),
                new DaisyThemeInfo("Wireframe", false)
            };

            foreach (var info in standardThemes)
            {
                RegisterTheme(info, () => LoadAxamlPalette(info.Name));
            }
        }

        private static ResourceDictionary LoadAxamlPalette(string themeName)
        {
            var uri = new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{themeName}.axaml");
            return (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
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
        /// Gets whether the current theme is dark.
        /// </summary>
        public static bool IsCurrentThemeDark
        {
            get
            {
                var currentTheme = _currentThemeName;
                return currentTheme != null && IsDarkTheme(currentTheme);
            }
        }

        /// <summary>
        /// Gets or sets the base/default theme name (used by theme controllers as the "unchecked" theme).
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
        /// Registers a theme with a palette factory. Call this to add more DaisyUI themes over time.
        /// </summary>
        public static void RegisterTheme(DaisyThemeInfo info, Func<ResourceDictionary> paletteFactory)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            if (paletteFactory == null) throw new ArgumentNullException(nameof(paletteFactory));
            if (string.IsNullOrWhiteSpace(info.Name))
                throw new ArgumentException("Theme name cannot be empty.", nameof(info));

            ThemesByName[info.Name] = new ThemeDefinition(info, paletteFactory);
            RebuildThemeList();
        }

        private static void RebuildThemeList()
        {
            var list = ThemesByName.Values.Select(d => d.Info).OrderBy(i => i.Name).ToList();
            AvailableThemes = new ReadOnlyCollection<DaisyThemeInfo>(list);
        }

        /// <summary>
        /// Gets theme info by name, or null if not found.
        /// </summary>
        public static DaisyThemeInfo? GetThemeInfo(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return null;

            return ThemesByName.TryGetValue(themeName, out var def) ? def.Info : null;
        }

        /// <summary>
        /// Apply a theme by name.
        /// </summary>
        public static bool ApplyTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return false;

            if (!ThemesByName.TryGetValue(themeName, out var def))
            {
                // Try to find case-insensitive match if not found directly
                var match = ThemesByName.Keys.FirstOrDefault(k => k.Equals(themeName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    def = ThemesByName[match];
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ApplyTheme: theme not registered: '{themeName}'");
                    return false;
                }
            }

            // Skip if already applied
            if (string.Equals(_currentThemeName, def.Info.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            // When suppressed, only update internal state without applying
            if (SuppressThemeApplication)
            {
                _currentThemeName = def.Info.Name;
                return true;
            }

            // Use custom applicator if set
            if (CustomThemeApplicator != null)
            {
                var result = CustomThemeApplicator(themeName);
                if (result)
                {
                    SetCurrentTheme(def.Info.Name);
                }
                return result;
            }

            var app = Application.Current;
            if (app == null) return false;

            try
            {
                var newPalette = def.PaletteFactory();

                if (_currentPalette != null && app.Resources.MergedDictionaries.Contains(_currentPalette))
                {
                    app.Resources.MergedDictionaries.Remove(_currentPalette);
                }

                app.Resources.MergedDictionaries.Add(newPalette);
                _currentPalette = newPalette;
                _currentThemeName = def.Info.Name;

                // Set light/dark variant for system controls
                app.RequestedThemeVariant = def.Info.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

                ThemeChanged?.Invoke(null, def.Info.Name);

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
