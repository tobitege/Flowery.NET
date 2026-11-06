using Avalonia;

namespace Flowery.Effects
{
    /// <summary>
    /// Draws a registered carousel effect (<see cref="CarouselGlEffectRegistry"/>) with SkSL shaders: a mask, flip,
    /// transition or text fill between the images of its definition, or a continuous effect on one image. Every property
    /// change configures the renderer again.
    /// </summary>
    public sealed class CarouselGlEffectHost : CarouselGlNativeHost
    {
        /// <summary>Defines the <see cref="EffectKind"/> property.</summary>
        public static readonly StyledProperty<CarouselGlEffectKind> EffectKindProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, CarouselGlEffectKind>(nameof(EffectKind));

        /// <summary>Defines the <see cref="IntervalSeconds"/> property.</summary>
        public static readonly StyledProperty<double> IntervalSecondsProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(nameof(IntervalSeconds));

        /// <summary>Defines the <see cref="TransitionSeconds"/> property.</summary>
        public static readonly StyledProperty<double> TransitionSecondsProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(nameof(TransitionSeconds), 2.4d);

        /// <summary>Defines the <see cref="OverlayEffect"/> property.</summary>
        public static readonly StyledProperty<CarouselGlEffectKind> OverlayEffectProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, CarouselGlEffectKind>(nameof(OverlayEffect));

        /// <summary>Defines the <see cref="TransitionSliceCount"/> property.</summary>
        public static readonly StyledProperty<int> TransitionSliceCountProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, int>(nameof(TransitionSliceCount), 8);

        /// <summary>Defines the <see cref="TransitionStaggerSlices"/> property.</summary>
        public static readonly StyledProperty<bool> TransitionStaggerSlicesProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, bool>(nameof(TransitionStaggerSlices), true);

        /// <summary>Defines the <see cref="TransitionSliceStaggerMs"/> property.</summary>
        public static readonly StyledProperty<double> TransitionSliceStaggerMsProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(nameof(TransitionSliceStaggerMs), 50.0);

        /// <summary>Defines the <see cref="TransitionPixelateSize"/> property.</summary>
        public static readonly StyledProperty<int> TransitionPixelateSizeProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, int>(nameof(TransitionPixelateSize), 20);

        /// <summary>Defines the <see cref="TransitionDissolveDensity"/> property.</summary>
        public static readonly StyledProperty<double> TransitionDissolveDensityProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(nameof(TransitionDissolveDensity), 0.5);

        /// <summary>Defines the <see cref="TransitionFlipAngle"/> property.</summary>
        public static readonly StyledProperty<double> TransitionFlipAngleProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(nameof(TransitionFlipAngle), 180.0);

        // The property metadata callbacks of WinUI: class handlers of the changes.
        static CarouselGlEffectHost()
        {
            EffectKindProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            IntervalSecondsProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionSecondsProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            OverlayEffectProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionSliceCountProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionStaggerSlicesProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionSliceStaggerMsProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionPixelateSizeProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionDissolveDensityProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
            TransitionFlipAngleProperty.Changed.AddClassHandler<CarouselGlEffectHost>(OnParametersChanged);
        }

        /// <summary>
        /// Gets or sets the registered effect to draw; <see cref="CarouselGlEffectKind.None"/> draws
        /// <see cref="OverlayEffect"/> alone, or nothing.
        /// </summary>
        public CarouselGlEffectKind EffectKind
        {
            get => GetValue(EffectKindProperty);
            set => SetValue(EffectKindProperty, value);
        }

        /// <summary>Gets or sets the hold time between two transitions in seconds; 0 holds none.</summary>
        public double IntervalSeconds
        {
            get => GetValue(IntervalSecondsProperty);
            set => SetValue(IntervalSecondsProperty, value);
        }

        /// <summary>Gets or sets the transition duration in seconds; 0 takes the default of the effect.</summary>
        public double TransitionSeconds
        {
            get => GetValue(TransitionSecondsProperty);
            set => SetValue(TransitionSecondsProperty, value);
        }

        /// <summary>
        /// Gets or sets a continuous effect, such as <see cref="CarouselGlEffectKind.EffectDrift"/>, that moves the images of a
        /// mask, flip or transition.
        /// </summary>
        public CarouselGlEffectKind OverlayEffect
        {
            get => GetValue(OverlayEffectProperty);
            set => SetValue(OverlayEffectProperty, value);
        }

        /// <summary>Gets or sets the tiles or slices of a mask; below 2 takes the default of the mask.</summary>
        public int TransitionSliceCount
        {
            get => GetValue(TransitionSliceCountProperty);
            set => SetValue(TransitionSliceCountProperty, value);
        }

        /// <summary>Gets or sets whether the slices of a mask start one after another.</summary>
        public bool TransitionStaggerSlices
        {
            get => GetValue(TransitionStaggerSlicesProperty);
            set => SetValue(TransitionStaggerSlicesProperty, value);
        }

        /// <summary>Gets or sets the delay between two slices in milliseconds.</summary>
        public double TransitionSliceStaggerMs
        {
            get => GetValue(TransitionSliceStaggerMsProperty);
            set => SetValue(TransitionSliceStaggerMsProperty, value);
        }

        /// <summary>Gets or sets the block size of the pixelate mask in pixels.</summary>
        public int TransitionPixelateSize
        {
            get => GetValue(TransitionPixelateSizeProperty);
            set => SetValue(TransitionPixelateSizeProperty, value);
        }

        /// <summary>Gets or sets the grain of the dissolve mask, from 0 to 1.</summary>
        public double TransitionDissolveDensity
        {
            get => GetValue(TransitionDissolveDensityProperty);
            set => SetValue(TransitionDissolveDensityProperty, value);
        }

        /// <summary>
        /// Gets or sets the angle a flip turns, from 30 to 180 degrees; at 180 it ends on the back image. Cubes turn at
        /// least 180 degrees.
        /// </summary>
        public double TransitionFlipAngle
        {
            get => GetValue(TransitionFlipAngleProperty);
            set => SetValue(TransitionFlipAngleProperty, value);
        }

        private static void OnParametersChanged(CarouselGlEffectHost sender, AvaloniaPropertyChangedEventArgs args) =>
            sender.RefreshRenderer();

        /// <inheritdoc/>
        // EngineScript: the engine object that ports flowery-carousel-gl.js.
        protected override CarouselGlEngine CreateEngine() => new FloweryCarouselGL();

        /// <inheritdoc/>
        protected override CarouselGlConfiguration? CreateConfiguration()
        {
            var effect = EffectKind == CarouselGlEffectKind.None ? OverlayEffect : EffectKind;
            if (effect == CarouselGlEffectKind.None) return null;
            if (!CarouselGlEffectRegistry.TryGet(effect, out var definition))
                throw new InvalidOperationException("GL effect is not registered: " + effect);
            string? overlayVariant = null;
            if (definition.Mode is not (CarouselGlMode.Effect or CarouselGlMode.Text)
                && CarouselGlEffectRegistry.TryGet(OverlayEffect, out var overlay)
                && overlay.Mode == CarouselGlMode.Effect) overlayVariant = overlay.Variant;
            return new CarouselGlConfiguration(definition.Mode, definition.Variant, overlayVariant,
                definition.Assets, IntervalSeconds, TransitionSeconds,
                TransitionSliceCount, TransitionStaggerSlices, TransitionSliceStaggerMs,
                TransitionPixelateSize, TransitionDissolveDensity, TransitionFlipAngle);
        }
    }
}
