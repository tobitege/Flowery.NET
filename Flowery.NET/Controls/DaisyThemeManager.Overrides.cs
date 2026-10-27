using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.Controls
{
    /// <summary>
    /// A color pair of the active theme below the contrast WCAG asks for, which
    /// <see cref="DaisyThemeManager.FindContrastGaps"/> reports.
    /// </summary>
    /// <param name="Foreground">The resource key of the text or surface color, such as <c>DaisyWarningColor</c>.</param>
    /// <param name="Background">The resource key of the color it lies on, such as <c>DaisyBase100Color</c>.</param>
    /// <param name="Ratio">The contrast ratio of the pair.</param>
    /// <param name="Required">The ratio WCAG asks for: 4.5 for text, 3 for the surface of a control.</param>
    public sealed record DaisyContrastGap(string Foreground, string Background, double Ratio, double Required);

    public static partial class DaisyThemeManager
    {
        private const string BackdropBrushKey = "DaisyBackdropBrush";
        private const string BackdropEffectKey = "DaisyBackdropEffect";
        private const string EffectPrefix = "effect:";
        private const string ImagePrefix = "image:";

        // Big photos are decoded at most this wide; the brush scales the image to fill the backdrop.
        private const int MaxBackdropImageWidth = 3840;

        // Right after the palette: what the library derives from it, the glass tint of the glass mode and the backdrop
        // that the theme leaves to the library or that BackdropOverride chose. Built again on every theme switch and
        // every change of the glass mode or an override.
        private static ResourceDictionary? _derivedPalette;

        private static string? _backdropOverride;
        private static IBrush? _backdropOverrideBrush;
        private static DaisyGlassBackground? _backdropOverrideEffect;
        private static Color? _baseOverride;

        // WCAG: text needs 4.5:1; the surfaces of controls, such as a button in an accent color, 3:1 against the page.
        private const double TextContrast = 4.5;
        private const double SurfaceContrast = 3;

        private static readonly string[] AccentNames = ["Primary", "Secondary", "Accent", "Neutral", "Info", "Success", "Warning", "Error"];

        // The text colors of the accents, for text in an accent color on the base, and the accent each falls back to.
        private static readonly (string Key, string Accent)[] AccentTextBrushes =
        [
            ("DaisyPrimaryForegroundBrush", "DaisyPrimaryColor"),
            ("DaisySecondaryForegroundBrush", "DaisySecondaryColor"),
            ("DaisyAccentForegroundBrush", "DaisyAccentColor"),
            ("DaisyButtonPrimaryForegroundBrush", "DaisyPrimaryColor")
        ];

        /// <summary>
        /// Gets or sets a base color that replaces <c>Base100</c> of every theme at run time and stays across theme
        /// switches, which spares palettes that differ only in their base colors. <c>null</c> keeps the theme as
        /// defined. While it is set:
        /// <list type="bullet">
        /// <item><c>Base100</c> takes the color, without its alpha. Light or dark follows from its luminance, and with it
        /// <see cref="IsCurrentThemeDark"/>, <see cref="IsDarkTheme"/> of the current theme and
        /// <c>Application.RequestedThemeVariant</c>.</item>
        /// <item><c>Base200</c>, <c>Base300</c> and <c>BaseContent</c> keep the contrast ratio of the theme to
        /// <c>Base100</c>, measured on its own palette, and their side of it: toward the text or away from it.
        /// <c>BaseContent</c> keeps at least 4.5:1. The brushes derived from them, such as the muted text, follow.</item>
        /// <item>The text colors of the accents (<c>DaisyPrimaryForegroundBrush</c> and the others) move until they
        /// reach 4.5:1 against the new base. The accent surfaces and the text on them stay as they are, so a theme
        /// keeps its character.</item>
        /// <item>The glass tint follows: in the glass mode it is the new base, and a theme with a tint of its own,
        /// such as Glassy, gets one in the hue of the new base.</item>
        /// </list>
        /// A change raises <see cref="BaseOverrideChanged"/> and <see cref="ThemeChanged"/> with the current theme,
        /// since its colors change. <see cref="FindContrastGaps"/> lists the gaps that remain, such as accent surfaces
        /// too pale for a very light base.
        /// </summary>
        public static Color? BaseOverride
        {
            get => _baseOverride;
            set
            {
                var color = value is { } given ? Color.FromRgb(given.R, given.G, given.B) : (Color?)null;
                if (color == _baseOverride)
                    return;
                _baseOverride = color;
                if (Application.Current is { } app && _currentThemeName != null && GetThemeInfo(_currentThemeName) is { } info)
                    app.RequestedThemeVariant = IsDarkWithOverride(info.IsDark) ? ThemeVariant.Dark : ThemeVariant.Light;
                RefreshDerivedResources();
                BaseOverrideChanged?.Invoke(null, EventArgs.Empty);
                if (_currentThemeName is { } name)
                    Services.FloweryEventRaiser.Raise(ThemeChanged, null, name, nameof(ThemeChanged));
            }
        }

        /// <summary>
        /// Raised when <see cref="BaseOverride"/> changes.
        /// </summary>
        public static event EventHandler? BaseOverrideChanged;

        // Dark when white text has more contrast on it than black text.
        private static bool IsDarkBase(Color color) =>
            FloweryColorHelpers.GetContrastRatio(Colors.White, color) >= FloweryColorHelpers.GetContrastRatio(Colors.Black, color);

        private static bool IsDarkWithOverride(bool themeIsDark) => _baseOverride is { } color ? IsDarkBase(color) : themeIsDark;

        /// <summary>
        /// Lists the color pairs of the active theme, as it is drawn now, that stay below the contrast WCAG asks for:
        /// <c>BaseContent</c> on <c>Base100</c>, <c>Base200</c> and <c>Base300</c>, and the text colors of the accents on
        /// <c>Base100</c>, at 4.5:1; the accent surfaces against <c>Base100</c> at 3:1; and the text on each accent
        /// surface (<c>DaisyPrimaryContentColor</c> on <c>DaisyPrimaryColor</c> and the others) at 4.5:1. With
        /// <see cref="BaseOverride"/> it shows what the override leaves, since it moves the base text and the text colors
        /// of the accents but not the accent surfaces. Many themes have a few gaps of their own, such as a yellow warning
        /// on white.
        /// </summary>
        public static IReadOnlyList<DaisyContrastGap> FindContrastGaps()
        {
            var gaps = new List<DaisyContrastGap>();
            if (Application.Current is not { } app)
                return gaps;
            var variant = app.ActualThemeVariant;
            Color? Find(string key) => app.TryFindResource(key, variant, out var value)
                ? value switch { Color color => color, ISolidColorBrush brush => brush.Color, _ => null }
                : null;
            void Check(string foreground, string background, double required)
            {
                if (Find(foreground) is not { } front || Find(background) is not { } back)
                    return;
                // A translucent color counts as it is drawn on its background.
                var ratio = FloweryColorHelpers.GetContrastRatio(FloweryColorHelpers.Composite(back, front, 1), back);
                if (ratio < required - 0.005)
                    gaps.Add(new DaisyContrastGap(foreground, background, ratio, required));
            }

            foreach (var surface in new[] { "DaisyBase100Color", "DaisyBase200Color", "DaisyBase300Color" })
                Check("DaisyBaseContentColor", surface, TextContrast);
            foreach (var (key, _) in AccentTextBrushes)
                Check(key, "DaisyBase100Color", TextContrast);
            foreach (var name in AccentNames)
            {
                Check($"Daisy{name}Color", "DaisyBase100Color", SurfaceContrast);
                Check($"Daisy{name}ContentColor", $"Daisy{name}Color", TextContrast);
            }
            return gaps;
        }

        /// <summary>
        /// Gets or sets a backdrop chosen at run time, which replaces <c>DaisyBackdropBrush</c> and
        /// <c>DaisyBackdropEffect</c> of every theme and stays across theme switches. It is a short text that an app can
        /// save next to the theme name and set again at startup; the library stores nothing itself:
        /// <list type="bullet">
        /// <item>a color, such as <c>#0B1B5E</c>;</item>
        /// <item><c>image:</c> with a file path or an <c>avares://</c> URI, such as <c>image:C:\Pictures\bg.jpg</c>, which
        /// the library loads and scales to fill the backdrop (an image that cannot be loaded leaves the backdrop of the
        /// theme);</item>
        /// <item><c>effect:</c> with a <see cref="DaisyGlassAnimation"/>, such as <c>effect:Aurora</c>, drawn with shaders
        /// while Flowery.NET.Skia is registered and with gradients otherwise.</item>
        /// </list>
        /// <c>null</c> keeps the backdrop of the theme. The getter returns the text in its normal form, such as
        /// <c>#0B1B5E</c> or <c>effect:LiquidGold</c>. Set it after the application has started, since loading an image
        /// needs the platform.
        /// </summary>
        /// <exception cref="FormatException">The text is in none of these forms.</exception>
        public static string? BackdropOverride
        {
            get => _backdropOverride;
            set
            {
                var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (string.Equals(text, _backdropOverride, StringComparison.Ordinal))
                    return;
                (_backdropOverride, _backdropOverrideBrush, _backdropOverrideEffect) =
                    text == null ? (null, null, null) : ParseBackdrop(text);
                RefreshDerivedResources();
                BackdropOverrideChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raised when <see cref="BackdropOverride"/> changes.
        /// </summary>
        public static event EventHandler? BackdropOverrideChanged;

        private static (string Text, IBrush? Brush, DaisyGlassBackground? Effect) ParseBackdrop(string text)
        {
            if (text.StartsWith(EffectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var name = text[EffectPrefix.Length..].Trim();
                // A number would parse as any value of the enum.
                if (name.Length == 0 || char.IsDigit(name[0]) || !Enum.TryParse<DaisyGlassAnimation>(name, true, out var animation)
                    || !Enum.IsDefined(animation))
                {
                    throw new FormatException(
                        $"'{name}' is no backdrop effect; use one of {string.Join(", ", Enum.GetNames<DaisyGlassAnimation>())}.");
                }
                // A calm effect behind the whole window: slower than on a glass and redrawn less often, which costs less.
                var effect = DaisyGlassLiveRenderer.Current?.CreateShaderBackground() ?? new DaisyGlassGradientBackground();
                effect.Effect = animation;
                effect.Speed = 0.4;
                effect.FrameRate = 20;
                return (EffectPrefix + animation, null, effect);
            }
            if (text.StartsWith(ImagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var source = text[ImagePrefix.Length..].Trim();
                if (source.Length == 0)
                    throw new FormatException("An image backdrop needs a file path or an avares:// URI after 'image:'.");
                return (ImagePrefix + source, LoadBackdropImage(source), null);
            }
            if (Color.TryParse(text, out var color))
                return (color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}",
                    new ImmutableSolidColorBrush(color), null);
            throw new FormatException(
                $"'{text}' is no backdrop: use a color such as #0B1B5E, image: with a file path or URI, or effect: with an effect name.");
        }

        private static IBrush? LoadBackdropImage(string source)
        {
            try
            {
                using var stream = source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
                    ? AssetLoader.Open(new Uri(source))
                    : File.OpenRead(source.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(source).LocalPath : source);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                memory.Position = 0;
                var bitmap = new Bitmap(memory);
                if (bitmap.PixelSize.Width > MaxBackdropImageWidth)
                {
                    bitmap.Dispose();
                    memory.Position = 0;
                    bitmap = Bitmap.DecodeToWidth(memory, MaxBackdropImageWidth);
                }
                return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            }
            // A missing file, a URI the app does not have or data that is no image: the theme keeps its own backdrop.
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BackdropOverride: the image '{source}' could not be loaded: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Puts what the library derives from the palette right after it, and in any case before the contrast palette:
        /// <list type="bullet">
        /// <item>the base colors of <see cref="BaseOverride"/>;</item>
        /// <item>while <see cref="DaisyGlassSurface.GlobalEnabled"/> is on, <c>DaisyGlassTintColor</c> is <c>Base100</c>
        /// of the active theme, so the glass takes the hue of the theme and the text on it keeps its contrast. A palette
        /// that sets its own tint, such as Glassy, keeps it, moved to the hue of a base override;</item>
        /// <item>the backdrop of <see cref="BackdropOverride"/>, or, in the glass mode, glows in the primary and secondary
        /// color on <c>Base100</c> for a theme without a backdrop of its own. A backdrop of the theme made for a dark base
        /// gives way under a light base override, and the other way round.</item>
        /// </list>
        /// </summary>
        internal static void RefreshDerivedResources()
        {
            DaisyGlassSurface.ApplyGlobalStyles();
            if (Application.Current is not { } app) return;
            var dictionaries = app.Resources.MergedDictionaries;
            if (_derivedPalette != null) dictionaries.Remove(_derivedPalette);
            _derivedPalette = null;

            var variant = app.ActualThemeVariant;
            var glassMode = DaisyGlassSurface.GlobalEnabled;
            var derived = new ResourceDictionary();

            // Base100 as it is drawn: the override or that of the palette.
            var hasBase = TryGetPaletteColor(app, "DaisyBase100Color", variant, out var base100);
            var themeBase100 = base100;
            if (hasBase && _baseOverride is { } baseOverride)
            {
                AddBaseOverride(app, variant, derived, base100, baseOverride);
                base100 = baseOverride;
            }

            // A tint of the app or its palette stays, also one applied by a CustomThemeApplicator or included in the app
            // itself; the default tint lives with the styles of DaisyUITheme, not in the app resources. Under a base
            // override, the tint of the palette moves with the base.
            var ownTint = app.Resources.TryGetResource(GlassTintKey, variant, out _);
            if (ownTint && _baseOverride != null)
            {
                if (_currentPalette?.TryGetResource(GlassTintKey, variant, out var tint) == true && tint is Color paletteTint
                    && TryGetPaletteColor(app, "DaisyBase100Color", variant, out var themeBase)
                    && TryGetPaletteColor(app, "DaisyBaseContentColor", variant, out var themeContent))
                    derived[GlassTintKey] = FollowBase(paletteTint, themeBase, themeContent, base100);
            }
            else if (glassMode && !ownTint && hasBase)
            {
                derived[GlassTintKey] = base100;
            }

            // A backdrop of the theme made for a base of the other kind, such as the dark wave of Glassy under a light base
            // override, would leave the text on it unreadable, so it gives way as if the theme had none.
            var hasThemeBackdrop = app.Resources.TryGetResource(BackdropBrushKey, variant, out _)
                || app.Resources.TryGetResource(BackdropEffectKey, variant, out _);
            var themeBackdropFits = _baseOverride is not { } newBase || IsDarkBase(newBase) == IsDarkBase(themeBase100);
            if (_backdropOverride != null)
            {
                if (_backdropOverrideBrush != null)
                {
                    derived[BackdropBrushKey] = _backdropOverrideBrush;
                    derived[BackdropEffectKey] = null;
                }
                else if (_backdropOverrideEffect != null)
                {
                    derived[BackdropEffectKey] = _backdropOverrideEffect;
                }
            }
            else if (hasBase && (!hasThemeBackdrop || !themeBackdropFits))
            {
                if (glassMode && TryGetPaletteColor(app, "DaisyPrimaryColor", variant, out var primary)
                    && TryGetPaletteColor(app, "DaisySecondaryColor", variant, out var secondary))
                    derived[BackdropBrushKey] = CreateGlowBackdrop(base100, primary, secondary);
                else if (hasThemeBackdrop)
                    derived[BackdropBrushKey] = new ImmutableSolidColorBrush(base100);
                if (hasThemeBackdrop)
                    derived[BackdropEffectKey] = null;
            }

            if (derived.Count == 0)
                return;
            _derivedPalette = derived;
            // Right after the palette, and in any case before the contrast palette.
            var index = _currentPalette != null ? dictionaries.IndexOf(_currentPalette) + 1 : 0;
            if (index <= 0)
                index = _contrastPalette != null && dictionaries.Contains(_contrastPalette)
                    ? dictionaries.IndexOf(_contrastPalette)
                    : dictionaries.Count;
            dictionaries.Insert(index, derived);
        }

        // The base colors and the text colors of the accents for a new Base100. A fixed rule would not fit: Base300, the
        // border and hover color, is 1.3 to 1.7:1 darker in the light themes, 3.0 to 3.6:1 lighter in most dark themes and
        // only 1.2 to 1.5:1 lighter in some, so each color keeps the contrast its own theme gives it.
        private static void AddBaseOverride(Application app, ThemeVariant variant, ResourceDictionary derived, Color themeBase, Color newBase)
        {
            var themeContent = TryGetPaletteColor(app, "DaisyBaseContentColor", variant, out var content)
                ? content
                : IsDarkBase(themeBase) ? Colors.White : Colors.Black;
            AddBaseColor(derived, "Base100", newBase);
            foreach (var name in new[] { "Base200", "Base300" })
            {
                if (TryGetPaletteColor(app, $"Daisy{name}Color", variant, out var color))
                    AddBaseColor(derived, name, FollowBase(color, themeBase, themeContent, newBase));
            }

            // The text keeps its hue while it stays on its side; on a base of the other kind it takes the hue of the base,
            // since a strong hue of the old text would not fit there.
            var dark = IsDarkBase(newBase);
            var ratio = Math.Max(TextContrast, FloweryColorHelpers.GetContrastRatio(themeContent, themeBase));
            AddBaseColor(derived, "BaseContent",
                WithContrast(dark == IsDarkBase(themeBase) ? themeContent : newBase, newBase, ratio, lighter: dark));

            foreach (var (key, accent) in AccentTextBrushes)
            {
                if (TryGetPaletteBrushColor(app, key, variant, out var text) || TryGetPaletteColor(app, accent, variant, out text))
                    derived[key] = new ImmutableSolidColorBrush(FloweryColorHelpers.EnsureContrast(text, newBase, TextContrast));
            }
            // A few palettes name the primary text color as a color too.
            if (_currentPalette?.TryGetResource("DaisyPrimaryForegroundColor", variant, out var primaryText) == true && primaryText is Color primary)
                derived["DaisyPrimaryForegroundColor"] = FloweryColorHelpers.EnsureContrast(primary, newBase, TextContrast);
        }

        // A palette brushes its colors with StaticResource, so a new color needs a new brush as well.
        private static void AddBaseColor(ResourceDictionary derived, string name, Color color)
        {
            derived[$"Daisy{name}Color"] = color;
            derived[$"Daisy{name}Brush"] = new ImmutableSolidColorBrush(color);
        }

        // The color that keeps the contrast ratio of color to the old base, and its side: toward the text of the theme or
        // away from it, with the hue of the new base.
        private static Color FollowBase(Color color, Color themeBase, Color themeContent, Color newBase)
        {
            var luminance = FloweryColorHelpers.GetRelativeLuminance(themeBase);
            var towardText = FloweryColorHelpers.GetRelativeLuminance(color) > luminance
                == FloweryColorHelpers.GetRelativeLuminance(themeContent) > luminance;
            // On a dark base the text is light, so toward the text is lighter.
            var lighter = towardText == IsDarkBase(newBase);
            return WithContrast(newBase, newBase, FloweryColorHelpers.GetContrastRatio(color, themeBase), lighter);
        }

        // The color with the hue and saturation of seed that has the given contrast ratio to background, lighter or
        // darker than it; black or white where the ratio cannot be reached.
        private static Color WithContrast(Color seed, Color background, double ratio, bool lighter)
        {
            var luminance = FloweryColorHelpers.GetRelativeLuminance(background);
            var target = Math.Clamp(lighter ? ratio * (luminance + 0.05) - 0.05 : (luminance + 0.05) / ratio - 0.05, 0, 1);
            var hsl = new ColorPicker.HslColor(seed);
            // The luminance grows with the lightness at a fixed hue and saturation, so halving finds the lightness.
            double low = 0, high = 1;
            for (var i = 0; i < 24; i++)
            {
                var middle = (low + high) / 2;
                if (FloweryColorHelpers.GetRelativeLuminance(new ColorPicker.HslColor(hsl.H, hsl.S, middle)) < target)
                    low = middle;
                else
                    high = middle;
            }
            // The side that reaches the ratio, so whole RGB values do not round it below.
            return new ColorPicker.HslColor(hsl.H, hsl.S, lighter ? high : low).ToRgbColor();
        }

        private static bool TryGetPaletteBrushColor(Application app, string key, ThemeVariant variant, out Color color)
        {
            object? value = null;
            var found = _currentPalette?.TryGetResource(key, variant, out value) == true || app.TryFindResource(key, variant, out value);
            color = found && value is ISolidColorBrush brush ? brush.Color : default;
            return found && value is ISolidColorBrush;
        }

        // A color of the palette itself, not of the contrast palette over it or of what the library derives from it.
        private static bool TryGetPaletteColor(Application app, string key, ThemeVariant variant, out Color color)
        {
            object? value = null;
            if (_currentPalette?.TryGetResource(key, variant, out value) != true && !app.TryFindResource(key, variant, out value))
            {
                color = default;
                return false;
            }
            color = value is Color found ? found : default;
            return value is Color;
        }

        // The glows of the Gallery header as a backdrop: the primary color at the upper right and the secondary color at
        // the lower left, on Base100. Every layer covers the whole drawing, so the glows never change its size.
        private static IBrush CreateGlowBackdrop(Color base100, Color primary, Color secondary)
        {
            var area = new RectangleGeometry(new Rect(0, 0, 1600, 900));
            return new DrawingBrush
            {
                Stretch = Stretch.UniformToFill,
                Drawing = new DrawingGroup
                {
                    ClipGeometry = area,
                    Children =
                    {
                        new GeometryDrawing { Geometry = area, Brush = new ImmutableSolidColorBrush(base100) },
                        new GeometryDrawing { Geometry = area, Brush = Glow(primary, 0.55, new RelativePoint(0.82, 0.1, RelativeUnit.Relative), 0.42, 0.6) },
                        new GeometryDrawing { Geometry = area, Brush = Glow(secondary, 0.45, new RelativePoint(0.2, 0.95, RelativeUnit.Relative), 0.38, 0.55) }
                    }
                }
            };
        }

        private static IBrush Glow(Color color, double opacity, RelativePoint center, double radiusX, double radiusY) =>
            new RadialGradientBrush
            {
                Center = center,
                GradientOrigin = center,
                RadiusX = new RelativeScalar(radiusX, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(radiusY, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb((byte)Math.Round(255 * opacity), color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                }
            };
    }
}
