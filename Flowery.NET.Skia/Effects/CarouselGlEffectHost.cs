using Avalonia;

namespace Flowery.Effects
{
    public sealed class CarouselGlEffectHost : CarouselGlNativeHost
    {
        // new: the name of the WinUI property hides Visual.Effect, the render effect of Avalonia.
        public static readonly new StyledProperty<CarouselGlEffectKind> EffectProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, CarouselGlEffectKind>(
                nameof(Effect),
                CarouselGlEffectKind.None);

        public static readonly StyledProperty<double> IntervalSecondsProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(
                nameof(IntervalSeconds),
                0d);

        public static readonly StyledProperty<double> TransitionSecondsProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(
                nameof(TransitionSeconds),
                2.4d);

        public static readonly StyledProperty<CarouselGlEffectKind> OverlayEffectProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, CarouselGlEffectKind>(
                nameof(OverlayEffect),
                CarouselGlEffectKind.None);

        public static readonly StyledProperty<int> TransitionSliceCountProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, int>(
                nameof(TransitionSliceCount),
                8);

        public static readonly StyledProperty<bool> TransitionStaggerSlicesProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, bool>(
                nameof(TransitionStaggerSlices),
                true);

        public static readonly StyledProperty<double> TransitionSliceStaggerMsProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(
                nameof(TransitionSliceStaggerMs),
                50.0);

        public static readonly StyledProperty<int> TransitionPixelateSizeProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, int>(
                nameof(TransitionPixelateSize),
                20);

        public static readonly StyledProperty<double> TransitionDissolveDensityProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(
                nameof(TransitionDissolveDensity),
                0.5);

        public static readonly StyledProperty<double> TransitionFlipAngleProperty =
            AvaloniaProperty.Register<CarouselGlEffectHost, double>(
                nameof(TransitionFlipAngle),
                90.0);

        // The property metadata callbacks of WinUI: class handlers of the changes.
        static CarouselGlEffectHost()
        {
            EffectProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            IntervalSecondsProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionSecondsProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            OverlayEffectProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionSliceCountProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionStaggerSlicesProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionSliceStaggerMsProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionPixelateSizeProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionDissolveDensityProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionFlipAngleProperty.Changed.AddClassHandler<CarouselGlEffectHost>((sender, args) => OnParametersChanged(sender, args));
        }

        public new CarouselGlEffectKind Effect
        {
            get => GetValue(EffectProperty);
            set => SetValue(EffectProperty, value);
        }

        public double IntervalSeconds
        {
            get => GetValue(IntervalSecondsProperty);
            set => SetValue(IntervalSecondsProperty, value);
        }

        public double TransitionSeconds
        {
            get => GetValue(TransitionSecondsProperty);
            set => SetValue(TransitionSecondsProperty, value);
        }

        public CarouselGlEffectKind OverlayEffect
        {
            get => GetValue(OverlayEffectProperty);
            set => SetValue(OverlayEffectProperty, value);
        }

        public int TransitionSliceCount
        {
            get => GetValue(TransitionSliceCountProperty);
            set => SetValue(TransitionSliceCountProperty, value);
        }

        public bool TransitionStaggerSlices
        {
            get => GetValue(TransitionStaggerSlicesProperty);
            set => SetValue(TransitionStaggerSlicesProperty, value);
        }

        public double TransitionSliceStaggerMs
        {
            get => GetValue(TransitionSliceStaggerMsProperty);
            set => SetValue(TransitionSliceStaggerMsProperty, value);
        }

        public int TransitionPixelateSize
        {
            get => GetValue(TransitionPixelateSizeProperty);
            set => SetValue(TransitionPixelateSizeProperty, value);
        }

        public double TransitionDissolveDensity
        {
            get => GetValue(TransitionDissolveDensityProperty);
            set => SetValue(TransitionDissolveDensityProperty, value);
        }

        public double TransitionFlipAngle
        {
            get => GetValue(TransitionFlipAngleProperty);
            set => SetValue(TransitionFlipAngleProperty, value);
        }

        private static void OnParametersChanged(CarouselGlEffectHost sender, AvaloniaPropertyChangedEventArgs args)
        {
            sender.RefreshRenderer();
        }

        // EngineScript: the engine object that ports flowery-carousel-gl.js.
        protected override CarouselGlEngine CreateEngine() => new FloweryCarouselGL();

        protected override CarouselGlConfiguration? CreateConfiguration()
        {
            var effect = Effect == CarouselGlEffectKind.None ? OverlayEffect : Effect;
            if (effect == CarouselGlEffectKind.None) return null;
            if (!CarouselGlEffectRegistry.TryGet(effect, out var definition) || definition is null)
                throw new InvalidOperationException("GL effect is not registered: " + effect);
            string? overlayVariant = null;
            if (definition.Mode is not "effect" and not "text"
                && CarouselGlEffectRegistry.TryGet(OverlayEffect, out var overlay)
                && overlay is { Mode: "effect" }) overlayVariant = overlay.Variant;
            return new CarouselGlConfiguration("effects", definition.Mode, definition.Variant, overlayVariant,
                definition.Assets, IntervalSeconds, TransitionSeconds,
                TransitionSliceCount, TransitionStaggerSlices, TransitionSliceStaggerMs,
                TransitionPixelateSize, TransitionDissolveDensity, TransitionFlipAngle);
        }
    }
}
