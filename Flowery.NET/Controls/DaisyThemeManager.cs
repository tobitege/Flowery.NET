using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Flowery.Helpers;
using Flowery.Services;
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

        private static readonly HashSet<string> InternalThemeNames =
            new(StringComparer.OrdinalIgnoreCase);

        private static ResourceDictionary? _currentPalette;
        private static ResourceDictionary? _contrastPalette;
        private static bool _contrastDark;
        private static string? _currentThemeName;
        private static string _baseThemeName = "Dark";
        private static HashSet<string> _excludedThemes = new(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _preferredThemes = new(StringComparer.OrdinalIgnoreCase);
        internal sealed record ThemeEntry(string Name, string DisplayName, bool Preferred, ProductPalette? ProductPalette);
        private static readonly Dictionary<string, ThemeEntry> AddedThemes = new(StringComparer.OrdinalIgnoreCase);
        internal static IReadOnlyDictionary<string, ThemeEntry> ThemeOverrides => AddedThemes;

        /// <summary>
        /// Adds or updates a theme in both dropdowns globally. Local dropdown overrides take
        /// precedence. Product themes are registered without applying their palette.
        /// Call before creating controls or on the UI thread at runtime.
        /// </summary>
        /// <param name="originalName">Original registered or product theme name, matched case-insensitively.</param>
        /// <param name="displayName">Default text displayed by the dropdowns.</param>
        /// <param name="preferred">Whether to place the theme in the preferred group, sorted by display name.</param>
        public static void AddTheme(string originalName, string displayName, bool preferred)
        {
            var entry = CreateThemeEntry(originalName, displayName, preferred);
            if (AddedThemes.TryGetValue(entry.Name, out var existing) && existing == entry) return;
            AddedThemes[entry.Name] = entry;
            if (GetThemeInfo(entry.Name) == null && entry.ProductPalette is { } product)
                EnsureProductThemeRegistered(product);
            else
                NotifyThemeListChanged();
        }

        /// <summary>
        /// Removes global display and ordering overrides. Registered themes remain registered.
        /// Call on the UI thread once controls exist. Returns true when an override was removed.
        /// </summary>
        public static bool RemoveThemeOverride(string originalName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
            if (!AddedThemes.Remove(originalName)) return false;
            NotifyThemeListChanged();
            return true;
        }

        internal static ThemeEntry CreateThemeEntry(string originalName, string displayName, bool preferred)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            var registered = GetThemeInfo(originalName);
            var product = ProductPaletteFactory.FindByName(originalName);
            var name = registered?.Name ?? product?.Name
                ?? throw new ArgumentException($"Unknown theme '{originalName}'.", nameof(originalName));
            return new ThemeEntry(name, displayName, preferred, product);
        }

        /// <summary>
        /// Theme names hidden from both theme dropdowns. Assign before constructing the UI,
        /// or replace on the UI thread at runtime. The getter returns a read-only snapshot.
        /// Names are case-insensitive and may refer to themes not yet registered.
        /// This does not prevent <see cref="ApplyTheme"/> from applying a hidden theme.
        /// </summary>
        public static IReadOnlyCollection<string> ExcludedThemes
        {
            get => _excludedThemes.ToList().AsReadOnly();
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in value)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(name);
                    names.Add(name);
                }

                if (_excludedThemes.SetEquals(names)) return;
                _excludedThemes = names;
                NotifyThemeListChanged();
            }
        }

        /// <summary>
        /// Theme names shown in the preferred group, sorted by display text.
        /// An empty collection clears these defaults. Explicit AddTheme flags override them.
        /// Names are case-insensitive; the getter returns a read-only snapshot.
        /// Exclusion takes precedence. This neither selects nor applies a theme.
        /// Set before constructing the UI or on the UI thread at runtime.
        /// </summary>
        public static IReadOnlyCollection<string> PreferredThemes
        {
            get => new ReadOnlyCollection<string>([.. _preferredThemes]);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in value)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(name);
                    names.Add(name);
                }

                if (_preferredThemes.SetEquals(names)) return;
                _preferredThemes = names;
                NotifyThemeListChanged();
            }
        }

        internal static bool IsThemePreferred(string themeName) => _preferredThemes.Contains(themeName);

        /// <summary>
        /// Hides a theme from both dropdowns, including themes registered later.
        /// Call on the UI thread once the UI exists. Returns true if the exclusion changed.
        /// </summary>
        public static bool HideTheme(string themeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(themeName);
            if (!_excludedThemes.Add(themeName)) return false;
            NotifyThemeListChanged();
            return true;
        }

        /// <summary>
        /// Removes a theme exclusion. Call on the UI thread once the UI exists.
        /// Returns true if the exclusion changed; this does not register unknown themes.
        /// </summary>
        public static bool ShowTheme(string themeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(themeName);
            if (!_excludedThemes.Remove(themeName)) return false;
            NotifyThemeListChanged();
            return true;
        }

        /// <summary>
        /// Whether a name is eligible for display, independently of theme registration.
        /// </summary>
        public static bool IsThemeVisible(string themeName) =>
            !string.IsNullOrWhiteSpace(themeName) && !_excludedThemes.Contains(themeName);

        private static void NotifyThemeListChanged()
        {
            RebuildThemeList();
            AvailableThemesChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// When true, ApplyTheme calls only update internal state without actually applying the theme.
        /// </summary>
        public static bool SuppressThemeApplication { get; set; }
        private static bool _themeApplicationPending;

        /// <summary>
        /// When true, <see cref="RegisterTheme"/> also raises <see cref="AvailableThemesChanged"/>
        /// for built-in themes. Default is false.
        /// </summary>
        public static bool NotifyForInternalThemesChanged { get; set; }

        /// <summary>
        /// Optional custom theme applicator. When set, this delegate is called
        /// instead of the default MergedDictionaries approach.
        /// </summary>
        public static Func<string, bool>? CustomThemeApplicator { get; set; }

        /// <summary>
        /// Visible registered themes, with the preferred theme first and the rest sorted by name.
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
                InternalThemeNames.Add(info.Name);
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
        /// Event raised when <see cref="RegisterTheme"/> adds a new theme name.
        /// Built-in themes raise this event only when <see cref="NotifyForInternalThemesChanged"/> is true.
        /// Changes to exclusions or the preferred theme always raise this event.
        /// </summary>
        public static event EventHandler? AvailableThemesChanged;

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

            var isNewTheme = !ThemesByName.ContainsKey(info.Name);
            var isInternalTheme = InternalThemeNames.Contains(info.Name);
            ThemesByName[info.Name] = new ThemeDefinition(info, paletteFactory);
            RebuildThemeList();

            var notifyInternal = isInternalTheme && NotifyForInternalThemesChanged;
            var notifyNew = isNewTheme && !isInternalTheme;
            if (notifyInternal || notifyNew)
            {
                AvailableThemesChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        internal static void EnsureProductThemeRegistered(ProductPalette palette)
        {
            if (GetThemeInfo(palette.Name) != null)
                return;

            var precompiled = ProductPalettes.Get(palette.Name);
            if (precompiled != null)
            {
                var info = new DaisyThemeInfo(palette.Name, FloweryColorHelpers.IsDark(precompiled.Base100));
                RegisterTheme(info, () => DaisyPaletteFactory.Create(precompiled));
                return;
            }

            var fallbackInfo = new DaisyThemeInfo(palette.Name, FloweryColorHelpers.IsDark(palette.Background));
            RegisterTheme(fallbackInfo, () => ProductPaletteFactory.CreateResourceDictionary(palette));
        }

        private static void RebuildThemeList()
        {
            var list = ThemeListResolver.AvailableThemes(ThemesByName.Values.Select(static definition => definition.Info));
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
        /// Gets the palette factory registered for a theme, or null if the theme is unknown.
        /// </summary>
        public static Func<ResourceDictionary>? GetPaletteFactory(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return null;

            return ThemesByName.TryGetValue(themeName, out var def) ? def.PaletteFactory : null;
        }

        /// <summary>
        /// Creates a palette <see cref="ResourceDictionary"/> from the factory registered for a theme.
        /// </summary>
        /// <param name="themeName">The registered theme name.</param>
        /// <param name="palette">The created palette when the method returns true.</param>
        /// <returns>True when a palette was created; otherwise false.</returns>
        public static bool TryCreatePalette(string themeName, out ResourceDictionary? palette)
        {
            palette = null;
            var factory = GetPaletteFactory(themeName);
            if (factory == null)
                return false;

            try
            {
                palette = factory();
                return palette != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryCreatePalette: failed for '{themeName}': {ex.Message}");
                palette = null;
                return false;
            }
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
            if (!_themeApplicationPending && string.Equals(_currentThemeName, def.Info.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            // When suppressed, only update internal state without applying
            if (SuppressThemeApplication)
            {
                _currentThemeName = def.Info.Name;
                _themeApplicationPending = true;
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
                _themeApplicationPending = false;

                // Set light/dark variant for system controls
                app.RequestedThemeVariant = def.Info.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

                DaisyAccessibility.RefreshContrastPalette();
                FloweryEventRaiser.Raise(ThemeChanged, null, def.Info.Name, nameof(ThemeChanged));

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
            if (!_themeApplicationPending && string.Equals(_currentThemeName, themeName, StringComparison.OrdinalIgnoreCase))
                return;

            _currentThemeName = themeName;
            _themeApplicationPending = false;
            DaisyAccessibility.RefreshContrastPalette();
            FloweryEventRaiser.Raise(ThemeChanged, null, themeName, nameof(ThemeChanged));
        }

        internal static void ApplyContrastResources(bool enabled, bool dark)
        {
            if (Application.Current is not { } app) return;
            var dictionaries = app.Resources.MergedDictionaries;
            if (DaisyAccessibility.CustomContrastApplicator?.Invoke(enabled) == true || !enabled)
            {
                if (_contrastPalette != null) dictionaries.Remove(_contrastPalette);
                _contrastPalette = null;
                return;
            }
            if (_contrastPalette == null || _contrastDark != dark)
            {
                if (_contrastPalette != null) dictionaries.Remove(_contrastPalette);
                _contrastPalette = CreateContrastPalette(dark);
                _contrastDark = dark;
            }
            if (dictionaries.LastOrDefault() == _contrastPalette) return;
            dictionaries.Remove(_contrastPalette);
            dictionaries.Add(_contrastPalette);
        }

        private static ResourceDictionary CreateContrastPalette(bool dark)
        {
            var palette = new ResourceDictionary();
            var background = dark ? Colors.Black : Colors.White;
            var foreground = dark ? Colors.White : Colors.Black;
            var accent = dark ? Colors.Yellow : Colors.Blue;
            foreach (var name in new[] { "Base100", "Base200", "Base300" }) AddContrastColor(palette, name, background);
            AddContrastColor(palette, "Base300", foreground);
            foreach (var name in new[] { "BaseContent", "BaseContentMuted", "BaseContentSubtle", "BaseContentDisabled" })
                AddContrastColor(palette, name, foreground);
            foreach (var name in new[] { "Primary", "Secondary", "Accent", "Neutral", "Info", "Success", "Warning", "Error" })
            {
                AddContrastColor(palette, name, accent);
                AddContrastColor(palette, name + "Focus", accent);
                AddContrastColor(palette, name + "Foreground", accent);
                AddContrastColor(palette, name + "Content", background);
                AddContrastColor(palette, "Button" + name + "Foreground", accent);
                AddContrastColor(palette, "Button" + name + "Soft", background);
                AddContrastColor(palette, "Button" + name + "SoftHover", background);
                AddContrastColor(palette, "Button" + name + "SoftBorder", accent);
                AddContrastColor(palette, "Button" + name + "Disabled", foreground);
                palette["DaisyButton" + name + "DisabledOpacity"] = 1d;
            }
            AddContrastColor(palette, "TreeSelectedForeground", foreground);
            return palette;
        }

        private static void AddContrastColor(ResourceDictionary palette, string name, Color color)
        {
            palette["Daisy" + name + "Color"] = color;
            palette["Daisy" + name + "Brush"] = new SolidColorBrush(color);
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
