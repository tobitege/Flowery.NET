using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
        /// Creates the layer that draws this background for one glass.
        /// </summary>
        public abstract DaisyGlassAnimationLayer CreateLayer();
    }

    /// <summary>
    /// A layer that draws one animated <see cref="DaisyGlassBackground"/> and redraws itself
    /// <see cref="DaisyGlassBackground.FrameRate"/> times per second while it is attached and on screen. With reduced
    /// motion it draws a still frame; with high contrast it stops redrawing, since the contrast surface covers it.
    /// While it cannot move (a speed of 0, reduced motion or high contrast) or is scrolled out of view it asks for no
    /// frames; while it is hidden, such as on a page that is not shown, it looks again a few times per second.
    /// </summary>
    public abstract class DaisyGlassAnimationLayer : Control
    {
        private static readonly TimeSpan HiddenCheckInterval = TimeSpan.FromMilliseconds(250);

        private bool _attached;
        private bool _frameRequested;
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
            RequestFrame();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _attached = false;
            DaisyAccessibility.PreferencesChanged -= OnPreferencesChanged;
            Source.PropertyChanged -= OnSourceChanged;
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
            _lastFrame = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnPreferencesChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
            RequestFrame();
        }

        // A new effect shows at once, also on a still frame; a speed above 0 starts the frames again.
        private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            InvalidateVisual();
            RequestFrame();
        }

        private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            _inViewport = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
            RequestFrame();
        }

        private void RequestFrame()
        {
            if (_frameRequested || !_attached || !CanMove || !_inViewport || TopLevel.GetTopLevel(this) is not { } topLevel)
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

            // A long gap, such as after the window was hidden, does not make the animation jump.
            var elapsed = _lastFrame is { } last ? Math.Min((now - last).TotalSeconds, 0.1) : 0;
            _lastFrame = now;
            Time += elapsed * Source.Speed;
            if (elapsed > 0 && IsRedrawFrame(ref _sinceRedraw, elapsed, Source.FrameRate))
                InvalidateVisual();
            RequestFrame();
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
            sinceRedraw = Math.Max(sinceRedraw - interval, -interval);
            return true;
        }
    }
}
