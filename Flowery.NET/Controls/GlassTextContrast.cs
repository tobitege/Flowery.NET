using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.Controls
{
    /// <summary>
    /// Keeps the text of a glass control readable over whatever lies behind it. The glass measures the color of its
    /// backdrop; this estimates the color right behind the text from it, with the glass tint, fill, animated
    /// background, top shine and the control's own tint layer on top, and moves the text color toward white or black
    /// until it has WCAG contrast against that color (4.5, or 3 for large text). A foreground set on the control itself
    /// is kept as it is.
    /// </summary>
    internal sealed class GlassTextContrast : IDisposable
    {
        // Covers what the estimate leaves out: the liquid-glass highlights of the SkiaSharp mode and the blur, which
        // spreads brighter or darker spots of the backdrop over the average.
        private const double RatioReserve = 0.25;

        private readonly TemplatedControl _owner;
        private readonly DaisyGlass _glass;
        private readonly Border? _tint;
        private readonly Action<IBrush?> _apply;
        private bool _enabled;
        private DaisyGlassBackground? _background;
        private IBrush? _lastSource;
        private Color _lastColor;
        private IBrush? _lastBrush;

        private GlassTextContrast(TemplatedControl owner, DaisyGlass glass, Border? tint, bool enabled, Action<IBrush?> apply)
        {
            _owner = owner;
            _glass = glass;
            _tint = tint;
            _apply = apply;
            _enabled = enabled;
            _glass.PropertyChanged += OnPartChanged;
            if (_tint != null)
                _tint.PropertyChanged += OnPartChanged;
            WatchBackground(_glass.GlassBackground);
            Update();
        }

        /// <summary>
        /// Replaces the contrast of a glass control when its template is applied: disposes <paramref name="previous"/>
        /// and returns the contrast for the new <paramref name="glass"/>, or applies the plain foreground without one.
        /// </summary>
        public static GlassTextContrast? Attach(GlassTextContrast? previous, TemplatedControl owner, DaisyGlass? glass,
            Border? tint, bool enabled, Action<IBrush?> apply)
        {
            previous?.Dispose();
            if (glass != null)
                return new GlassTextContrast(owner, glass, tint, enabled, apply);
            apply(owner.Foreground);
            return null;
        }

        /// <summary>
        /// Follows a property change of the owning control: the switch itself, or the foreground and font that the
        /// contrast depends on.
        /// </summary>
        public static void OnOwnerPropertyChanged(GlassTextContrast? contrast, TemplatedControl owner,
            AvaloniaPropertyChangedEventArgs change, AvaloniaProperty enabledProperty, Action<IBrush?> apply)
        {
            if (change.Property == enabledProperty)
            {
                if (contrast != null)
                    contrast.Enabled = change.GetNewValue<bool>();
            }
            else if (change.Property == TemplatedControl.ForegroundProperty || change.Property == TemplatedControl.FontSizeProperty
                     || change.Property == TemplatedControl.FontWeightProperty)
            {
                if (contrast != null)
                    contrast.Update();
                else
                    apply(owner.Foreground);
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Update();
            }
        }

        public void Dispose()
        {
            _glass.PropertyChanged -= OnPartChanged;
            if (_tint != null)
                _tint.PropertyChanged -= OnPartChanged;
            WatchBackground(null);
            _glass.MeasureBackdropColor = false;
        }

        /// <summary>
        /// Recomputes the text color.
        /// </summary>
        public void Update()
        {
            // Only a solid foreground from the theme is adjusted, so only then the glass measures its backdrop.
            var foreground = _owner.Foreground;
            var adjustable = _enabled && foreground is ISolidColorBrush && !IsSetOnControl();
            _glass.MeasureBackdropColor = adjustable;
            if (!adjustable || foreground is not ISolidColorBrush solid || EstimateSurface() is not { } surface)
            {
                _apply(foreground);
                return;
            }

            // The brush opacity goes into the alpha, so a translucent text is checked as it renders.
            var text = WithOpacity(solid.Color, solid.Opacity);
            var ratio = (IsLargeText() ? 3.0 : 4.5) + RatioReserve;
            var color = FloweryColorHelpers.EnsureContrast(text, surface, ratio);
            if (color == text)
            {
                _apply(foreground);
                return;
            }
            // An adjusted color that still has the contrast stays, so small changes in the measured backdrop do not
            // redraw the text, which would redraw the glass and could measure it again.
            if (_lastBrush != null && ReferenceEquals(_lastSource, foreground)
                && FloweryColorHelpers.EnsureContrast(_lastColor, surface, ratio) == _lastColor)
            {
                _apply(_lastBrush);
                return;
            }
            if (!ReferenceEquals(_lastSource, foreground) || _lastColor != color || _lastBrush == null)
            {
                _lastSource = foreground;
                _lastColor = color;
                _lastBrush = new SolidColorBrush(color);
            }
            _apply(_lastBrush);
        }

        private void OnPartChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            var property = e.Property;
            if (property == DaisyGlass.GlassBackgroundProperty)
                WatchBackground(_glass.GlassBackground);
            if (property == DaisyGlass.BackdropColorProperty || property == DaisyGlass.GlassTintProperty
                || property == DaisyGlass.GlassTintOpacityProperty || property == DaisyGlass.EffectiveGlassFillProperty
                || property == DaisyGlass.GlassFillOpacityProperty || property == DaisyGlass.GlassBackgroundProperty
                || property == DaisyGlass.GlassBackgroundOpacityProperty
                || (sender == _glass && property == Visual.BoundsProperty)
                || (sender == _tint && (property == Border.BackgroundProperty || property == Visual.OpacityProperty)))
                Update();
        }

        // The animated background can change its effect while it is set, which changes the color behind the text.
        private void WatchBackground(DaisyGlassBackground? background)
        {
            if (ReferenceEquals(_background, background))
                return;
            if (_background != null)
                _background.PropertyChanged -= OnBackgroundChanged;
            _background = background;
            if (_background != null)
                _background.PropertyChanged += OnBackgroundChanged;
        }

        private void OnBackgroundChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DaisyGlassBackground.EffectProperty)
                Update();
        }

        private bool IsSetOnControl() =>
            _owner.GetDiagnostic(TemplatedControl.ForegroundProperty).Priority == BindingPriority.LocalValue;

        // WCAG: large text is at least 18 pt (24 px), or 14 pt (about 18.7 px) when bold.
        private bool IsLargeText() =>
            _owner.FontSize >= 24 || (_owner.FontSize >= 18.66 && _owner.FontWeight >= FontWeight.Bold);

        private Color? EstimateSurface()
        {
            if (_glass.BackdropColor is not { } color)
                return null;
            color = FloweryColorHelpers.Composite(color, _glass.GlassTint, _glass.GlassTintOpacity);
            if (AverageColor(_glass.EffectiveGlassFill) is { } fill)
                color = FloweryColorHelpers.Composite(color, fill, _glass.GlassFillOpacity);
            if (_glass.GlassBackground is { } background)
                color = FloweryColorHelpers.Composite(color, AverageColor(background.Effect), _glass.GlassBackgroundOpacity);
            // The text sits in the middle of the glass, where the top shine still adds some white.
            color = FloweryColorHelpers.Composite(color, Colors.White, DaisyGlass.TopShineOpacityAt(_glass.Bounds.Height / 2));
            if (_tint != null && AverageColor(_tint.Background) is { } tint)
                color = FloweryColorHelpers.Composite(color, tint, _tint.Opacity);
            return color;
        }

        // The brush opacity goes into the alpha, which Composite multiplies with the layer opacity.
        private static Color? AverageColor(IBrush? brush)
        {
            switch (brush)
            {
                case ISolidColorBrush solid:
                    return WithOpacity(solid.Color, solid.Opacity);
                case IGradientBrush { GradientStops.Count: > 0 } gradient:
                    var stops = gradient.GradientStops;
                    return WithOpacity(Color.FromArgb(
                        (byte)stops.Average(stop => stop.Color.A), (byte)stops.Average(stop => stop.Color.R),
                        (byte)stops.Average(stop => stop.Color.G), (byte)stops.Average(stop => stop.Color.B)), gradient.Opacity);
                default:
                    return null;
            }
        }

        // The average color of each animated effect over its run, drawn as gradients or shaders.
        private static Color AverageColor(DaisyGlassAnimation effect) => effect switch
        {
            DaisyGlassAnimation.Aurora => Color.FromRgb(25, 70, 80),
            DaisyGlassAnimation.LiquidGold => Color.FromRgb(185, 135, 35),
            _ => Color.FromRgb(150, 120, 190)
        };

        private static Color WithOpacity(Color color, double opacity) =>
            Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);
    }
}
