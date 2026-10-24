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
    /// background and the control's own tint layer on top, and moves the text color toward white or black until it
    /// has WCAG contrast against that color (4.5, or 3 for large text). A foreground set on the control itself is
    /// kept as it is.
    /// </summary>
    internal sealed class GlassTextContrast : IDisposable
    {
        private readonly TemplatedControl _owner;
        private readonly DaisyGlass _glass;
        private readonly Border? _tint;
        private readonly Action<IBrush?> _apply;
        private bool _enabled;
        private IBrush? _lastSource;
        private Color _lastColor;
        private IBrush? _lastBrush;

        public GlassTextContrast(TemplatedControl owner, DaisyGlass glass, Border? tint, bool enabled, Action<IBrush?> apply)
        {
            _owner = owner;
            _glass = glass;
            _tint = tint;
            _apply = apply;
            _enabled = enabled;
            _glass.PropertyChanged += OnPartChanged;
            if (_tint != null)
                _tint.PropertyChanged += OnPartChanged;
            Update();
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
            _glass.MeasureBackdropColor = false;
        }

        /// <summary>
        /// Recomputes the text color; the owner calls it when its foreground, font size or font weight changes.
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

            var ratio = IsLargeText() ? 3.0 : 4.5;
            var color = FloweryColorHelpers.EnsureContrast(solid.Color, surface, ratio);
            if (color == solid.Color)
            {
                _apply(foreground);
                return;
            }
            // An adjusted color that still has the contrast stays, so small changes in the measured backdrop do not
            // redraw the text, which would redraw the glass and could measure it again.
            if (_lastBrush != null && ReferenceEquals(_lastSource, foreground)
                && FloweryColorHelpers.GetContrastRatio(Color.FromRgb(_lastColor.R, _lastColor.G, _lastColor.B), surface) >= ratio)
            {
                _apply(_lastBrush);
                return;
            }
            if (!ReferenceEquals(_lastSource, foreground) || _lastColor != color || _lastBrush == null)
            {
                _lastSource = foreground;
                _lastColor = color;
                _lastBrush = new SolidColorBrush(color, solid.Opacity);
            }
            _apply(_lastBrush);
        }

        private void OnPartChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            var property = e.Property;
            if (property == DaisyGlass.BackdropColorProperty || property == DaisyGlass.GlassTintProperty
                || property == DaisyGlass.GlassTintOpacityProperty || property == DaisyGlass.EffectiveGlassFillProperty
                || property == DaisyGlass.GlassFillOpacityProperty || property == DaisyGlass.GlassBackgroundProperty
                || property == DaisyGlass.GlassBackgroundOpacityProperty
                || (sender == _tint && (property == Border.BackgroundProperty || property == Visual.OpacityProperty)))
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
