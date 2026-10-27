using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// An animated background for the glass of <see cref="DaisyGlass"/> and the glass controls, drawn over the blurred
    /// backdrop at <see cref="DaisyGlass.GlassBackgroundOpacity"/>. A background object may be shared; each glass
    /// creates its own layer from it with <see cref="CreateLayer"/>.
    /// </summary>
    public abstract class DaisyGlassBackground : AvaloniaObject
    {
        /// <summary>
        /// Defines the <see cref="Effect"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyGlassAnimation> EffectProperty =
            AvaloniaProperty.Register<DaisyGlassBackground, DaisyGlassAnimation>(nameof(Effect));

        /// <summary>
        /// Gets or sets the animated effect.
        /// </summary>
        public DaisyGlassAnimation Effect
        {
            get => GetValue(EffectProperty);
            set => SetValue(EffectProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Speed"/> property.
        /// </summary>
        public static readonly StyledProperty<double> SpeedProperty =
            AvaloniaProperty.Register<DaisyGlassBackground, double>(nameof(Speed), 1.0);

        /// <summary>
        /// Gets or sets the animation speed; 1 is the normal speed and 0 stops the animation.
        /// </summary>
        public double Speed
        {
            get => GetValue(SpeedProperty);
            set => SetValue(SpeedProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="FrameRate"/> property.
        /// </summary>
        public static readonly StyledProperty<double> FrameRateProperty =
            AvaloniaProperty.Register<DaisyGlassBackground, double>(nameof(FrameRate), 30.0);

        /// <summary>
        /// Gets or sets how many times per second the background redraws at most (default 30); 0 redraws on every
        /// display frame. The rate holds on any refresh rate, such as 60 or 144 Hz. Each redraw also redraws the glass
        /// with its blur, so a lower rate costs less. The animation keeps its speed.
        /// </summary>
        public double FrameRate
        {
            get => GetValue(FrameRateProperty);
            set => SetValue(FrameRateProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="EffectColor"/> property.
        /// </summary>
        public static readonly StyledProperty<Color?> EffectColorProperty =
            AvaloniaProperty.Register<DaisyGlassBackground, Color?>(nameof(EffectColor));

        /// <summary>
        /// Gets or sets the color of the <see cref="DaisyGlassAnimation.LiquidColor"/> effect; <c>null</c> (the default)
        /// takes the accent color of the system, and the primary color of the theme where the system has none.
        /// </summary>
        public Color? EffectColor
        {
            get => GetValue(EffectColorProperty);
            set => SetValue(EffectColorProperty, value);
        }

        /// <summary>
        /// Returns the color the <see cref="DaisyGlassAnimation.LiquidColor"/> effect draws with: <see cref="EffectColor"/>, else
        /// the accent color of the system, else the primary color of the theme.
        /// </summary>
        public Color GetEffectColor() => EffectColor ?? AccentColor();

        // The accent color is read once and again after a change of the system colors or of the theme, not on every frame.
        private static Color? _accentColor;
        private static IPlatformSettings? _watchedSettings;
        private static bool _watchingThemes;

        /// <summary>
        /// Fired when the accent color of the system or the theme changes, which <see cref="AccentColor"/> follows.
        /// </summary>
        internal static event EventHandler? AccentColorChanged;

        /// <summary>
        /// The accent color of the system, or the primary color of the theme where the system has none.
        /// </summary>
        public static Color AccentColor()
        {
            WatchAccentColor();
            if (_accentColor is { } cached)
                return cached;
            var app = Application.Current;
            var color = ResolveAccentColor(app?.PlatformSettings?.GetColorValues().AccentColor1, app);
            if (app != null)
                _accentColor = color;
            return color;
        }

        /// <summary>
        /// The accent color of the system when it has one (an alpha above 0), else <c>DaisyPrimaryColor</c> of
        /// <paramref name="resources"/>, else a plain blue.
        /// </summary>
        internal static Color ResolveAccentColor(Color? systemAccent, IResourceHost? resources)
        {
            if (systemAccent is { A: > 0 } accent)
                return accent;
            var variant = (resources as IThemeVariantHost)?.ActualThemeVariant;
            if (resources != null && resources.TryFindResource("DaisyPrimaryColor", variant, out var primary) && primary is Color color)
                return color;
            return Colors.DodgerBlue;
        }

        internal static void WatchAccentColor()
        {
            if (!_watchingThemes)
            {
                _watchingThemes = true;
                DaisyThemeManager.ThemeChanged += (_, _) => OnAccentColorChanged();
            }
            var settings = Application.Current?.PlatformSettings;
            if (ReferenceEquals(settings, _watchedSettings))
                return;
            if (_watchedSettings != null)
                _watchedSettings.ColorValuesChanged -= OnColorValuesChanged;
            _watchedSettings = settings;
            if (settings != null)
                settings.ColorValuesChanged += OnColorValuesChanged;
        }

        private static void OnColorValuesChanged(object? sender, PlatformColorValues e) => OnAccentColorChanged();

        private static void OnAccentColorChanged()
        {
            _accentColor = null;
            AccentColorChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Creates the layer that draws this background for one glass.
        /// </summary>
        public abstract DaisyGlassAnimationLayer CreateLayer();
    }

    /// <summary>
    /// A layer that draws one animated <see cref="DaisyGlassBackground"/> and redraws itself
    /// <see cref="DaisyGlassBackground.FrameRate"/> times per second while it is attached and on screen; between two redraws
    /// it waits instead of asking for every display frame. With reduced
    /// motion it draws a still frame; with high contrast it stops redrawing, since the contrast surface covers it.
    /// While it cannot move (a speed of 0, reduced motion or high contrast) or is scrolled out of view it asks for no
    /// frames; while it is hidden, such as on a page that is not shown, it looks again a few times per second.
    /// </summary>
    public abstract class DaisyGlassAnimationLayer : Control
    {
        private static readonly TimeSpan HiddenCheckInterval = TimeSpan.FromMilliseconds(250);

        // The longest animation step of one frame, in seconds, unless the frame rate asks for longer steps.
        private const double MaxFrameStep = 0.1;

        // A display frame at 60 Hz, assumed before the first frame tells the real frame time.
        private const double MinFrameTime = 1.0 / 60;

        // Shorter waits than this are not worth a timer; the layer asks for the next display frame instead.
        private const double MinSleep = 0.012;

        // The longest single wait, in seconds; a longer interval is waited out in steps.
        private const double MaxSleep = 1;

        private bool _attached;
        private bool _frameRequested;
        private IDisposable? _wait;
        private bool _hiddenCheckQueued;
        private bool _inViewport = true;
        private TimeSpan? _lastFrame;
        private double _sinceRedraw;

        protected DaisyGlassAnimationLayer(DaisyGlassBackground source)
        {
            Source = source;
            IsHitTestVisible = false;
            // Glass surfaces of the same size start at different phases, so they do not move in step.
            Time = Random.Shared.NextDouble() * 60;
        }

        /// <summary>
        /// Gets the background that this layer draws.
        /// </summary>
        protected DaisyGlassBackground Source { get; }

        /// <summary>
        /// Gets the animation time in seconds, scaled by <see cref="DaisyGlassBackground.Speed"/>.
        /// </summary>
        protected double Time { get; private set; }

        private bool CanMove => Source.Speed > 0 && !DaisyAccessibility.IsReducedMotion && !DaisyAccessibility.IsHighContrast;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _attached = true;
            DaisyAccessibility.PreferencesChanged += OnPreferencesChanged;
            Source.PropertyChanged += OnSourceChanged;
            EffectiveViewportChanged += OnEffectiveViewportChanged;
            DaisyGlassBackground.WatchAccentColor();
            DaisyGlassBackground.AccentColorChanged += OnAccentColorChanged;
            RequestFrame();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _attached = false;
            DaisyAccessibility.PreferencesChanged -= OnPreferencesChanged;
            Source.PropertyChanged -= OnSourceChanged;
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
            DaisyGlassBackground.AccentColorChanged -= OnAccentColorChanged;
            StopWaiting();
            _lastFrame = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnPreferencesChanged(object? sender, EventArgs e)
        {
            StopWaiting();
            InvalidateVisual();
            RequestFrame();
        }

        // A new effect shows at once, also on a still frame; a speed above 0 starts the frames again, and a new frame
        // rate applies without finishing the wait for the old one.
        private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            StopWaiting();
            InvalidateVisual();
            RequestFrame();
        }

        // A new accent color of the system, or a new theme for its fallback, shows at once on a background that follows
        // it, also on a still frame.
        private void OnAccentColorChanged(object? sender, EventArgs e)
        {
            if (Source is { Effect: DaisyGlassAnimation.LiquidColor, EffectColor: null })
                InvalidateVisual();
        }

        private void StopWaiting()
        {
            _wait?.Dispose();
            _wait = null;
        }

        private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            _inViewport = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
            RequestFrame();
        }

        private void RequestFrame()
        {
            // A pending frame or wait carries on as it is: dropping the time of the last frame would make the next one
            // count no time, so the animation would stall while something keeps asking.
            if (_wait != null || _frameRequested)
                return;
            if (!_attached || !CanMove || !_inViewport || TopLevel.GetTopLevel(this) is not { } topLevel)
            {
                _lastFrame = null;
                return;
            }
            if (!IsEffectivelyVisible)
            {
                CheckWhenHidden();
                return;
            }
            _frameRequested = true;
            topLevel.RequestAnimationFrame(OnFrame);
        }

        // Nothing reports when an ancestor shows the layer again, so a hidden layer looks again now and then.
        private void CheckWhenHidden()
        {
            _lastFrame = null;
            if (_hiddenCheckQueued)
                return;
            _hiddenCheckQueued = true;
            DispatcherTimer.RunOnce(() =>
            {
                _hiddenCheckQueued = false;
                RequestFrame();
            }, HiddenCheckInterval);
        }

        private void OnFrame(TimeSpan now)
        {
            _frameRequested = false;
            if (!_attached || !CanMove || !_inViewport || !IsEffectivelyVisible)
            {
                RequestFrame();
                return;
            }

            // A long gap, such as after the window was hidden, does not make the animation jump. The layer itself waits up
            // to one redraw interval between frames, which is no gap.
            var frameRate = Source.FrameRate;
            var maxStep = frameRate > 0 ? Math.Max(MaxFrameStep, 2 / frameRate) : MaxFrameStep;
            var elapsed = _lastFrame is { } last ? Math.Min((now - last).TotalSeconds, maxStep) : 0;
            _lastFrame = now;
            Time += elapsed * Source.Speed;
            if (elapsed > 0 && IsRedrawFrame(ref _sinceRedraw, elapsed, frameRate))
                InvalidateVisual();
            WaitForNextRedraw(elapsed, frameRate);
        }

        // Between two redraws the layer does not ask for every display frame, which would wake the UI thread at the
        // display rate for nothing. It sleeps until about one display frame before the next redraw is due and asks for
        // frames from there, so the redraw still lands on a display frame.
        private void WaitForNextRedraw(double frameTime, double frameRate)
        {
            var untilRedraw = frameRate > 0 ? 1 / frameRate - _sinceRedraw : 0;
            var sleep = untilRedraw - Math.Max(frameTime, MinFrameTime) * 1.5;
            if (sleep < MinSleep)
            {
                RequestFrame();
                return;
            }

            // A long interval is waited out in steps, so a very low rate cannot overflow the timer.
            _wait = DispatcherTimer.RunOnce(() =>
            {
                _wait = null;
                RequestFrame();
            }, TimeSpan.FromSeconds(Math.Min(sleep, MaxSleep)));
        }

        // A frame redraws when it is nearer to the next redraw time than the frame after it. The time past that point
        // carries over, so 30 per second on a 144 Hz display alternates between every fourth and fifth frame. Without a
        // rate every frame redraws and nothing carries over.
        private static bool IsRedrawFrame(ref double sinceRedraw, double elapsed, double frameRate)
        {
            if (frameRate <= 0)
            {
                sinceRedraw = 0;
                return true;
            }
            var interval = 1 / frameRate;
            sinceRedraw += elapsed;
            if (sinceRedraw < interval - elapsed / 2)
                return false;
            // The carry-over stays within one interval: a rate above the display rate, or dropped frames, would otherwise
            // pile it up, and after a lower rate is set the layer would redraw on every frame until it drained.
            sinceRedraw = Math.Clamp(sinceRedraw - interval, -interval, interval);
            return true;
        }
    }
}
