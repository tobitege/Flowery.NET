using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// A DaisyToggle on a glass track. Switched on, the track fills with the variant color (Success for the Default
    /// variant); the white knob stretches while it is pressed. The glass properties match those of
    /// <see cref="DaisyGlass"/> and apply to the track.
    /// </summary>
    public class DaisyGlassToggle : DaisyToggle
    {
        protected override Type StyleKeyOverride => typeof(DaisyGlassToggle);

        private IDisposable? _surfaceBindings;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _surfaceBindings?.Dispose();
            _surfaceBindings = e.NameScope.Find<DaisyGlass>("PART_Glass") is { } glass ? DaisyGlass.BindSurface(glass, this) : null;
        }

        /// <summary>
        /// Defines the <see cref="EnableBackdropBlur"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> EnableBackdropBlurProperty =
            DaisyGlass.EnableBackdropBlurProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets whether the glass blurs the backdrop. When false, the glass is simulated with overlays.
        /// </summary>
        public bool EnableBackdropBlur
        {
            get => GetValue(EnableBackdropBlurProperty);
            set => SetValue(EnableBackdropBlurProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="BlurMode"/> property.
        /// </summary>
        public static readonly StyledProperty<GlassBlurMode> BlurModeProperty =
            DaisyGlass.BlurModeProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets how the backdrop is blurred. SkiaSharp blurs the live backdrop on every frame;
        /// BitmapCapture blurs a snapshot that is taken again when the track moves or resizes.
        /// </summary>
        public GlassBlurMode BlurMode
        {
            get => GetValue(BlurModeProperty);
            set => SetValue(BlurModeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassBlur"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassBlurProperty =
            DaisyGlass.GlassBlurProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the blur amount of the backdrop.
        /// </summary>
        public double GlassBlur
        {
            get => GetValue(GlassBlurProperty);
            set => SetValue(GlassBlurProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassSaturation"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassSaturationProperty =
            DaisyGlass.GlassSaturationProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the saturation of the blurred backdrop (0.0 = grayscale, 1.0 = normal).
        /// </summary>
        public double GlassSaturation
        {
            get => GetValue(GlassSaturationProperty);
            set => SetValue(GlassSaturationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassOpacity"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassOpacityProperty =
            DaisyGlass.GlassOpacityProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the opacity of the simulated glass, used when the backdrop is not blurred.
        /// </summary>
        public double GlassOpacity
        {
            get => GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassTint"/> property.
        /// </summary>
        public static readonly StyledProperty<Color> GlassTintProperty =
            DaisyGlass.GlassTintProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the tint color of the glass.
        /// </summary>
        public Color GlassTint
        {
            get => GetValue(GlassTintProperty);
            set => SetValue(GlassTintProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassTintOpacity"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassTintOpacityProperty =
            DaisyGlass.GlassTintOpacityProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the opacity of the glass tint over the blurred backdrop.
        /// </summary>
        public double GlassTintOpacity
        {
            get => GetValue(GlassTintOpacityProperty);
            set => SetValue(GlassTintOpacityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassReflectDegree"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassReflectDegreeProperty =
            DaisyGlass.GlassReflectDegreeProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the angle of the glass reflection in degrees.
        /// </summary>
        public double GlassReflectDegree
        {
            get => GetValue(GlassReflectDegreeProperty);
            set => SetValue(GlassReflectDegreeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassReflectOpacity"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassReflectOpacityProperty =
            DaisyGlass.GlassReflectOpacityProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the opacity of the glass reflection.
        /// </summary>
        public double GlassReflectOpacity
        {
            get => GetValue(GlassReflectOpacityProperty);
            set => SetValue(GlassReflectOpacityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassDepth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassDepthProperty =
            DaisyGlass.GlassDepthProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets how far the liquid-glass refraction reaches inward from the edge.
        /// </summary>
        public double GlassDepth
        {
            get => GetValue(GlassDepthProperty);
            set => SetValue(GlassDepthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassCurvature"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassCurvatureProperty =
            DaisyGlass.GlassCurvatureProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the convex lens curvature of the liquid-glass highlight.
        /// </summary>
        public double GlassCurvature
        {
            get => GetValue(GlassCurvatureProperty);
            set => SetValue(GlassCurvatureProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassBend"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassBendProperty =
            DaisyGlass.GlassBendProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the inner rim bend of the liquid-glass meniscus.
        /// </summary>
        public double GlassBend
        {
            get => GetValue(GlassBendProperty);
            set => SetValue(GlassBendProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassDispersion"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassDispersionProperty =
            DaisyGlass.GlassDispersionProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the chromatic edge split of the liquid-glass rim.
        /// </summary>
        public double GlassDispersion
        {
            get => GetValue(GlassDispersionProperty);
            set => SetValue(GlassDispersionProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassFill"/> property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> GlassFillProperty =
            DaisyGlass.GlassFillProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets a brush, such as a gradient, painted over the blurred backdrop. It takes precedence over <see cref="GlassGradient"/>.
        /// </summary>
        public IBrush? GlassFill
        {
            get => GetValue(GlassFillProperty);
            set => SetValue(GlassFillProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassGradient"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyGlassGradient> GlassGradientProperty =
            DaisyGlass.GlassGradientProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets a gradient preset painted over the blurred backdrop when <see cref="GlassFill"/> is not set.
        /// </summary>
        public DaisyGlassGradient GlassGradient
        {
            get => GetValue(GlassGradientProperty);
            set => SetValue(GlassGradientProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassFillOpacity"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassFillOpacityProperty =
            DaisyGlass.GlassFillOpacityProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the opacity of the fill over the blurred backdrop.
        /// </summary>
        public double GlassFillOpacity
        {
            get => GetValue(GlassFillOpacityProperty);
            set => SetValue(GlassFillOpacityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassBackground"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyGlassBackground?> GlassBackgroundProperty =
            DaisyGlass.GlassBackgroundProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets an animated background drawn over the fill and below the pattern.
        /// </summary>
        public DaisyGlassBackground? GlassBackground
        {
            get => GetValue(GlassBackgroundProperty);
            set => SetValue(GlassBackgroundProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassBackgroundOpacity"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassBackgroundOpacityProperty =
            DaisyGlass.GlassBackgroundOpacityProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the opacity of the animated background.
        /// </summary>
        public double GlassBackgroundOpacity
        {
            get => GetValue(GlassBackgroundOpacityProperty);
            set => SetValue(GlassBackgroundOpacityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassPattern"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyCardPattern> GlassPatternProperty =
            DaisyGlass.GlassPatternProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets a pattern drawn over the fill, using the same patterns as <see cref="DaisyPatternedCard"/>.
        /// </summary>
        public DaisyCardPattern GlassPattern
        {
            get => GetValue(GlassPatternProperty);
            set => SetValue(GlassPatternProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassPatternColor"/> property.
        /// </summary>
        public static readonly StyledProperty<Color> GlassPatternColorProperty =
            DaisyGlass.GlassPatternColorProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the color of the glass pattern.
        /// </summary>
        public Color GlassPatternColor
        {
            get => GetValue(GlassPatternColorProperty);
            set => SetValue(GlassPatternColorProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassPatternOpacity"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassPatternOpacityProperty =
            DaisyGlass.GlassPatternOpacityProperty.AddOwner<DaisyGlassToggle>();

        /// <summary>
        /// Gets or sets the opacity of the glass pattern.
        /// </summary>
        public double GlassPatternOpacity
        {
            get => GetValue(GlassPatternOpacityProperty);
            set => SetValue(GlassPatternOpacityProperty, value);
        }
    }
}
