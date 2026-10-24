using System;
using Avalonia;
using Avalonia.Controls;
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
    /// <see cref="DaisyGlassBackground.FrameRate"/> times per second while it is attached. With reduced motion it draws a still frame; with high contrast it stops redrawing, since the contrast
    /// surface covers it.
    /// </summary>
    public abstract class DaisyGlassAnimationLayer : Control
    {
        private bool _frameRequested;
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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            DaisyAccessibility.PreferencesChanged += OnPreferencesChanged;
            RequestFrame();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DaisyAccessibility.PreferencesChanged -= OnPreferencesChanged;
            _lastFrame = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnPreferencesChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
            RequestFrame();
        }

        private void RequestFrame()
        {
            if (_frameRequested || DaisyAccessibility.IsReducedMotion || TopLevel.GetTopLevel(this) is not { } topLevel)
                return;
            _frameRequested = true;
            topLevel.RequestAnimationFrame(OnFrame);
        }

        private void OnFrame(TimeSpan now)
        {
            _frameRequested = false;
            if (TopLevel.GetTopLevel(this) == null || DaisyAccessibility.IsReducedMotion)
            {
                _lastFrame = null;
                return;
            }

            // A long gap, such as after the window was hidden, does not make the animation jump.
            var elapsed = _lastFrame is { } last ? Math.Min((now - last).TotalSeconds, 0.1) : 0;
            _lastFrame = now;
            Time += elapsed * Math.Max(0, Source.Speed);
            if (!DaisyAccessibility.IsHighContrast && IsEffectivelyVisible && elapsed > 0 && Source.Speed > 0 &&
                IsRedrawFrame(ref _sinceRedraw, elapsed, Source.FrameRate))
                InvalidateVisual();
            RequestFrame();
        }

        // A frame redraws when it is nearer to the next redraw time than the frame after it. The time past that point
        // carries over, so 30 per second on a 144 Hz display alternates between every fourth and fifth frame.
        private static bool IsRedrawFrame(ref double sinceRedraw, double elapsed, double frameRate)
        {
            var interval = frameRate > 0 ? 1 / frameRate : 0;
            sinceRedraw += elapsed;
            if (sinceRedraw < interval - elapsed / 2)
                return false;
            sinceRedraw = Math.Max(sinceRedraw - interval, -interval);
            return true;
        }
    }
}
