using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// A DaisyRange with a thin track and a pill thumb that turns into a glass lens while it is pressed: the opaque
    /// thumb grows and clears, so the track shows through it. The pseudo-class <c>:lens</c> marks that state. The glass
    /// properties match those of <see cref="DaisyGlass"/> and apply to the lens.
    /// </summary>
    [PseudoClasses(LensPseudoClass)]
    public class DaisyGlassRange : DaisyRange
    {
        private const string LensPseudoClass = ":lens";

        protected override Type StyleKeyOverride => typeof(DaisyGlassRange);

        private IDisposable? _surfaceBindings;
        private Thumb? _thumb;
        private Control? _lens;
        private Control? _progress;
        private RepeatButton[] _trackButtons = [];

        public DaisyGlassRange()
        {
            // The slider marks its own presses as handled; the lens follows them anyway.
            AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnAnyPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerCaptureLostEvent, OnAnyPointerCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _surfaceBindings?.Dispose();
            _surfaceBindings = e.NameScope.Find<DaisyGlass>("PART_Glass") is { } glass ? DaisyGlass.BindSurface(glass, this) : null;

            if (_thumb != null)
            {
                _thumb.PropertyChanged -= OnThumbPropertyChanged;
                _thumb.DragCompleted -= OnThumbDragCompleted;
            }
            _thumb = e.NameScope.Find<Thumb>("PART_Thumb");
            _lens = e.NameScope.Find<Control>("PART_Lens");
            _progress = e.NameScope.Find<Control>("PART_Progress");

            // A press on the track captures the pointer in one of its buttons; losing that capture ends the lens too.
            foreach (var button in _trackButtons)
                button.PointerCaptureLost -= OnAnyPointerCaptureLost;
            _trackButtons = [.. new[] { "PART_DecreaseButton", "PART_IncreaseButton" }
                .Select(name => e.NameScope.Find<RepeatButton>(name)).OfType<RepeatButton>()];
            foreach (var button in _trackButtons)
                button.PointerCaptureLost += OnAnyPointerCaptureLost;
            if (_thumb != null)
            {
                _thumb.PropertyChanged += OnThumbPropertyChanged;
                // Also raised when the thumb loses the pointer without a release.
                _thumb.DragCompleted += OnThumbDragCompleted;
            }
        }

        // The thumb only takes the pointer; the lens is drawn outside the track, where it can lie over the track and
        // the progress and still sit below the thumb. It follows the thumb, and the progress ends under its middle.
        private void OnThumbPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != BoundsProperty || _thumb == null)
                return;

            var bounds = _thumb.Bounds;
            if (_lens != null)
            {
                Canvas.SetLeft(_lens, bounds.X);
                Canvas.SetTop(_lens, bounds.Y);
                _lens.Width = bounds.Width;
                _lens.Height = bounds.Height;
            }
            if (_progress != null)
                _progress.Width = bounds.X + bounds.Width / 2;
        }

        private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (IsEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                PseudoClasses.Set(LensPseudoClass, true);
        }

        private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e) => PseudoClasses.Set(LensPseudoClass, false);

        private void OnAnyPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => PseudoClasses.Set(LensPseudoClass, false);

        private void OnThumbDragCompleted(object? sender, VectorEventArgs e) => PseudoClasses.Set(LensPseudoClass, false);

        /// <summary>
        /// Defines the <see cref="EnableBackdropBlur"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> EnableBackdropBlurProperty =
            DaisyGlass.EnableBackdropBlurProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.BlurModeProperty.AddOwner<DaisyGlassRange>();

        /// <summary>
        /// Gets or sets how the backdrop is blurred. SkiaSharp blurs the live backdrop on every frame;
        /// BitmapCapture blurs a snapshot that is taken again when the lens moves or resizes.
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
            DaisyGlass.GlassBlurProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassSaturationProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassOpacityProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassTintProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassTintOpacityProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassReflectDegreeProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassReflectOpacityProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassDepthProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassCurvatureProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassBendProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassDispersionProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassFillProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassGradientProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassFillOpacityProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassBackgroundProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassBackgroundOpacityProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassPatternProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassPatternColorProperty.AddOwner<DaisyGlassRange>();

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
            DaisyGlass.GlassPatternOpacityProperty.AddOwner<DaisyGlassRange>();

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
