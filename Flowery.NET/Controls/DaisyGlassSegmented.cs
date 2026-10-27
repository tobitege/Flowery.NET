using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// A segmented control on a glass track. The items share the width of the track, and a pill slides to the selected
    /// item with a spring, squashing slightly while it moves. Selection and keyboard work as on a <see cref="ListBox"/>;
    /// one item is always selected. The glass properties match those of <see cref="DaisyGlass"/> and apply to the track.
    /// </summary>
    public class DaisyGlassSegmented : ListBox
    {
        protected override Type StyleKeyOverride => typeof(DaisyGlassSegmented);

        // How long the pill stays squashed after it starts to move, and how much.
        private static readonly TimeSpan SquashDuration = TimeSpan.FromMilliseconds(160);
        private const double SquashScaleX = 1.08;
        private const double SquashScaleY = 0.9;

        private IDisposable? _surfaceBindings;
        private Border? _indicator;
        private Point? _indicatorOrigin;
        private bool _animateIndicator;
        private DispatcherTimer? _squashTimer;

        static DaisyGlassSegmented()
        {
            SelectionModeProperty.OverrideDefaultValue<DaisyGlassSegmented>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _surfaceBindings?.Dispose();
            _surfaceBindings = e.NameScope.Find<DaisyGlass>("PART_Glass") is { } glass ? DaisyGlass.BindSurface(glass, this) : null;
            _indicator = e.NameScope.Find<Border>("PART_Indicator");
            _indicatorOrigin = null;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // A squash cut short would stay: the pill is not placed again while its item keeps its position.
            EndSquash();
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SelectedIndexProperty)
            {
                // Arranging places the pill once the containers have their bounds.
                _animateIndicator = _indicatorOrigin != null;
                InvalidateArrange();
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            PlaceIndicator();
            return size;
        }

        /// <summary>
        /// Moves the pill over the selected item: with the spring after a selection change, at once after a resize or
        /// when it appears.
        /// </summary>
        private void PlaceIndicator()
        {
            if (_indicator?.GetVisualParent() is not Visual layer)
                return;

            var animate = _animateIndicator;
            _animateIndicator = false;

            if (SelectedIndex < 0 || ContainerFromIndex(SelectedIndex) is not { } container
                || container.TranslatePoint(default, layer) is not { } origin)
            {
                _indicator.IsVisible = false;
                _indicatorOrigin = null;
                return;
            }

            _indicator.IsVisible = true;
            _indicator.Width = container.Bounds.Width;
            _indicator.Height = container.Bounds.Height;
            if (_indicatorOrigin == origin)
                return;

            _indicatorOrigin = origin;
            if (animate && DaisyAccessibility.GetMotionEnabled(this))
            {
                _indicator.RenderTransform = CreateTransform(origin, SquashScaleX, SquashScaleY);
                StartSquash();
            }
            else
            {
                StopSquash();
                // A local null hides the transitions of the template; clearing it brings them back.
                _indicator.SetValue(Animatable.TransitionsProperty, null);
                _indicator.RenderTransform = CreateTransform(origin, 1, 1);
                _indicator.ClearValue(Animatable.TransitionsProperty);
            }
        }

        private void StartSquash()
        {
            if (_squashTimer == null)
            {
                _squashTimer = new DispatcherTimer { Interval = SquashDuration };
                _squashTimer.Tick += OnSquashTimerTick;
            }
            _squashTimer.Stop();
            _squashTimer.Start();
        }

        private void StopSquash() => _squashTimer?.Stop();

        private void OnSquashTimerTick(object? sender, EventArgs e) => EndSquash();

        private void EndSquash()
        {
            if (_squashTimer is not { IsEnabled: true })
                return;
            StopSquash();
            if (_indicator != null && _indicatorOrigin is { } origin)
                _indicator.RenderTransform = CreateTransform(origin, 1, 1);
        }

        // The scale comes first, around the center of the pill, so it does not scale the translation.
        private static ITransform CreateTransform(Point origin, double scaleX, double scaleY)
        {
            var builder = TransformOperations.CreateBuilder(2);
            builder.AppendScale(scaleX, scaleY);
            builder.AppendTranslate(origin.X, origin.Y);
            return builder.Build();
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyGlassSegmented, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the items: their padding and font size.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IndicatorBrush"/> property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
            AvaloniaProperty.Register<DaisyGlassSegmented, IBrush?>(nameof(IndicatorBrush));

        /// <summary>
        /// Gets or sets the fill of the pill that marks the selected item.
        /// </summary>
        public IBrush? IndicatorBrush
        {
            get => GetValue(IndicatorBrushProperty);
            set => SetValue(IndicatorBrushProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IndicatorBorderBrush"/> property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> IndicatorBorderBrushProperty =
            AvaloniaProperty.Register<DaisyGlassSegmented, IBrush?>(nameof(IndicatorBorderBrush));

        /// <summary>
        /// Gets or sets the outline of the pill that marks the selected item.
        /// </summary>
        public IBrush? IndicatorBorderBrush
        {
            get => GetValue(IndicatorBorderBrushProperty);
            set => SetValue(IndicatorBorderBrushProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="EnableBackdropBlur"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> EnableBackdropBlurProperty =
            DaisyGlass.EnableBackdropBlurProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.BlurModeProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassBlurProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassSaturationProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassOpacityProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassTintProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassTintOpacityProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassReflectDegreeProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassReflectOpacityProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassDepthProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassCurvatureProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassBendProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassDispersionProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassFillProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassGradientProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassFillOpacityProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassBackgroundProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassBackgroundOpacityProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassPatternProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassPatternColorProperty.AddOwner<DaisyGlassSegmented>();

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
            DaisyGlass.GlassPatternOpacityProperty.AddOwner<DaisyGlassSegmented>();

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
