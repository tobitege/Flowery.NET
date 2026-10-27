using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Flowery.Enums;

namespace Flowery.Controls
{
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
        /// <item>while <see cref="DaisyGlassSurface.GlobalEnabled"/> is on, <c>DaisyGlassTintColor</c> is <c>Base100</c>
        /// of the active theme, so the glass takes the hue of the theme and the text on it keeps its contrast. A palette
        /// that sets its own tint, such as Glassy, keeps it;</item>
        /// <item>the backdrop of <see cref="BackdropOverride"/>, or, in the glass mode, glows in the primary and secondary
        /// color on <c>Base100</c> for a theme without a backdrop of its own.</item>
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

            // A tint of the app or its palette stays, also one applied by a CustomThemeApplicator or included in the app
            // itself; the default tint lives with the styles of DaisyUITheme, not in the app resources.
            if (glassMode && !app.Resources.TryGetResource(GlassTintKey, variant, out _)
                && TryGetPaletteColor(app, "DaisyBase100Color", variant, out var tintBase))
                derived[GlassTintKey] = tintBase;

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
            else if (glassMode && !app.Resources.TryGetResource(BackdropBrushKey, variant, out _)
                     && !app.Resources.TryGetResource(BackdropEffectKey, variant, out _)
                     && TryGetPaletteColor(app, "DaisyBase100Color", variant, out var base100)
                     && TryGetPaletteColor(app, "DaisyPrimaryColor", variant, out var primary)
                     && TryGetPaletteColor(app, "DaisySecondaryColor", variant, out var secondary))
            {
                derived[BackdropBrushKey] = CreateGlowBackdrop(base100, primary, secondary);
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
