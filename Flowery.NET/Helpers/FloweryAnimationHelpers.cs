using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Enums;

namespace Flowery.Helpers
{
    /// <summary>
    /// Static helper class for creating reusable transform-based animations in Avalonia.
    /// Provides common animation patterns like zoom, pan, pulse, and fade effects
    /// that can be used across multiple controls.
    /// </summary>
    public static class FloweryAnimationHelpers
    {
        private static readonly Random _random = new();
        private static readonly ConditionalWeakTable<Control, PanAndZoomState> _PanAndZoomStates = new();
        private static readonly ConditionalWeakTable<Control, PanAndZoomSizingSnapshot> _PanAndZoomSizingSnapshots = new();

        #region High-Level Slide Effects

        /// <summary>
        /// Applies a slide effect to an element. This is the high-level method that controls can call
        /// to get cinematic effects like Pan And Zoom, pan, zoom, etc. Returns the actual effect that was applied.
        /// </summary>
        /// <param name="target">The control to animate.</param>
        /// <param name="effect">The slide effect to apply.</param>
        /// <param name="duration">Animation duration.</param>
        /// <param name="params">Effect parameters (intensity, distance, etc.). Uses defaults if null.</param>
        /// <param name="onComplete">Optional callback when animation completes.</param>
        public static FlowerySlideEffect ApplySlideEffect(
            Control target,
            FlowerySlideEffect effect,
            TimeSpan duration,
            FlowerySlideEffectParams? @params = null,
            Action? onComplete = null)
        {
            @params ??= new FlowerySlideEffectParams();

            // Ensure transform is set up
            EnsureTransformBuilder(target);

            // Pan And Zoom: combine zoom + pan for true documentary feel
            if (effect == FlowerySlideEffect.PanAndZoom)
            {
                ApplyPanAndZoomEffect(target, duration, @params);
                return FlowerySlideEffect.PanAndZoom;
            }

            // Breath: keyframe-based breathing zoom
            if (effect == FlowerySlideEffect.Breath)
            {
                ApplyBreathEffect(target, duration, @params, onComplete);
                return FlowerySlideEffect.Breath;
            }

            // Throw: dramatic parallax fly-through
            if (effect == FlowerySlideEffect.Throw)
            {
                ApplyThrowEffect(target, duration, @params, onComplete);
                return FlowerySlideEffect.Throw;
            }

            // Drift: random pan direction
            if (effect == FlowerySlideEffect.Drift)
            {
                FlowerySlideEffect[] panEffects = new[] {
                    FlowerySlideEffect.PanLeft,
                    FlowerySlideEffect.PanRight,
                    FlowerySlideEffect.PanUp,
                    FlowerySlideEffect.PanDown
                };
                effect = panEffects[_random.Next(panEffects.Length)];
            }

            // Apply individual effects
            switch (effect)
            {
                case FlowerySlideEffect.ZoomIn:
                    ApplyZoomAnimation(target, 1.0, 1.0 + @params.ZoomIntensity, duration, onComplete);
                    break;

                case FlowerySlideEffect.ZoomOut:
                    ApplyZoomAnimation(target, 1.0 + @params.ZoomIntensity, 1.0, duration, onComplete);
                    break;

                case FlowerySlideEffect.PanLeft:
                    ApplyPanAnimation(target, -@params.PanDistance, 0, duration, onComplete);
                    ApplyZoomAnimation(target, 1.0, 1.0 + (@params.ZoomIntensity * @params.SubtleZoomRatio), duration, null);
                    break;

                case FlowerySlideEffect.PanRight:
                    ApplyPanAnimation(target, @params.PanDistance, 0, duration, onComplete);
                    ApplyZoomAnimation(target, 1.0, 1.0 + (@params.ZoomIntensity * @params.SubtleZoomRatio), duration, null);
                    break;

                case FlowerySlideEffect.PanUp:
                    ApplyPanAnimation(target, 0, -@params.PanDistance * @params.VerticalPanRatio, duration, onComplete);
                    ApplyZoomAnimation(target, 1.0, 1.0 + (@params.ZoomIntensity * @params.SubtleZoomRatio), duration, null);
                    break;

                case FlowerySlideEffect.PanDown:
                    ApplyPanAnimation(target, 0, @params.PanDistance * @params.VerticalPanRatio, duration, onComplete);
                    ApplyZoomAnimation(target, 1.0, 1.0 + (@params.ZoomIntensity * @params.SubtleZoomRatio), duration, null);
                    break;

                case FlowerySlideEffect.Pulse:
                    ApplyPulseAnimation(target, @params.PulseIntensity, duration, onComplete);
                    break;

                case FlowerySlideEffect.None:
                default:
                    onComplete?.Invoke();
                    break;
            }

            return effect;
        }

        #region Pan And Zoom Effect

        /// <summary>
        /// Tracks state for the Pan And Zoom looping effect, allowing direction alternation.
        /// </summary>
        private sealed class PanAndZoomState
        {
            public bool LastZoomIn { get; set; } = true;
            public bool LastPanHorizontal { get; set; } = true;
            public bool IsActive { get; set; }
            public DispatcherTimer? AnimationTimer { get; set; }

            // Track last pan position as NORMALIZED 0..1 coordinates
            public double LastPanX01 { get; set; } = 0.5;
            public double LastPanY01 { get; set; } = 0.5;
            public Control? SizingTarget { get; set; }
            public bool HasSizingSnapshot { get; set; }
            public double OriginalWidth { get; set; }
            public double OriginalHeight { get; set; }
            public Thickness OriginalMargin { get; set; }
            public HorizontalAlignment OriginalHorizontalAlignment { get; set; }
            public VerticalAlignment OriginalVerticalAlignment { get; set; }
            public bool OriginalIsHitTestVisible { get; set; }

            public void Deactivate()
            {
                IsActive = false;
                AnimationTimer?.Stop();
                AnimationTimer = null;
            }
        }

        private sealed class PanAndZoomSizingSnapshot
        {
            public double Width { get; init; }
            public double Height { get; init; }
            public Thickness Margin { get; init; }
            public HorizontalAlignment HorizontalAlignment { get; init; }
            public VerticalAlignment VerticalAlignment { get; init; }
            public bool IsHitTestVisible { get; init; }
        }

        /// <summary>
        /// Applies a simplified Pan And Zoom effect.
        /// Uses axis-aligned motion (horizontal or vertical), smart portrait handling, and alternating zoom.
        /// </summary>
        private static void ApplyPanAndZoomEffect(
            Control target,
            TimeSpan duration,
            FlowerySlideEffectParams @params)
        {
            // Get or create state for looping
            if (!_PanAndZoomStates.TryGetValue(target, out PanAndZoomState? state))
            {
                state = new PanAndZoomState();
                _PanAndZoomStates.Remove(target);
                _PanAndZoomStates.Add(target, state);
            }

            state.IsActive = true;

            // Configure and start first cycle
            ConfigurePanAndZoomCycle(target, duration, @params, state);
        }

        private static void ConfigurePanAndZoomCycle(
            Control target,
            TimeSpan duration,
            FlowerySlideEffectParams @params,
            PanAndZoomState state)
        {
            state.AnimationTimer?.Stop();

            double zoomIntensity = Math.Max(0.0, @params.PanAndZoomZoom);
            double viewportWidth = target.Bounds.Width;
            double viewportHeight = target.Bounds.Height;

            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                return;
            }

            // Determine zoom direction
            bool zoomIn = state.LastZoomIn;
            bool panHorizontal = state.LastPanHorizontal;

            // Calculate scale values
            double zoomRatio = Math.Min(0.5, zoomIntensity);
            double minScale = 1.0;
            double maxScale = 1.0 + zoomRatio;

            double startScale, endScale;
            if (zoomIn)
            {
                startScale = minScale;
                endScale = maxScale;
            }
            else
            {
                startScale = maxScale;
                endScale = minScale;
            }

            // Calculate pan values
            double panDistance = @params.PanDistance;
            double startTx = 0, startTy = 0, endTx = 0, endTy = 0;

            if (panHorizontal)
            {
                double direction = _random.Next(2) == 0 ? 1.0 : -1.0;
                startTx = state.LastPanX01 > 0.5 ? panDistance : -panDistance;
                endTx = startTx * -1;
            }
            else
            {
                double direction = _random.Next(2) == 0 ? 1.0 : -1.0;
                startTy = state.LastPanY01 > 0.5 ? panDistance * @params.VerticalPanRatio : -panDistance * @params.VerticalPanRatio;
                endTy = startTy * -1;
            }

            // Store end positions for next cycle
            state.LastPanX01 = panHorizontal ? (endTx > 0 ? 0.75 : 0.25) : 0.5;
            state.LastPanY01 = panHorizontal ? 0.5 : (endTy > 0 ? 0.75 : 0.25);

            // Apply combined transform animation
            ApplyCombinedAnimation(target, startScale, endScale, startTx, endTx, startTy, endTy, duration, () =>
            {
                if (!state.IsActive)
                    return;

                // Alternate for next cycle
                if (!@params.PanAndZoomLockZoom)
                {
                    state.LastZoomIn = !state.LastZoomIn;
                }
                state.LastPanHorizontal = _random.Next(2) == 0;

                // Schedule next cycle
                state.AnimationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50)
                };
                state.AnimationTimer.Tick += (s, e) =>
                {
                    state.AnimationTimer?.Stop();
                    if (state.IsActive)
                    {
                        ConfigurePanAndZoomCycle(target, duration, @params, state);
                    }
                };
                state.AnimationTimer.Start();
            });
        }

        internal static void StopPanAndZoom(Control target, bool preserveSizing = false)
        {
            if (_PanAndZoomStates.TryGetValue(target, out PanAndZoomState? state))
            {
                state.Deactivate();
                if (!preserveSizing)
                {
                    RestorePanAndZoomSizing(state);
                }
                _PanAndZoomStates.Remove(target);
            }
        }

        private static void RestorePanAndZoomSizing(PanAndZoomState state)
        {
            if (!state.HasSizingSnapshot || state.SizingTarget is not Control target)
            {
                return;
            }

            target.Width = state.OriginalWidth;
            target.Height = state.OriginalHeight;
            target.Margin = state.OriginalMargin;
            target.HorizontalAlignment = state.OriginalHorizontalAlignment;
            target.VerticalAlignment = state.OriginalVerticalAlignment;
            target.IsHitTestVisible = state.OriginalIsHitTestVisible;

            state.HasSizingSnapshot = false;
            state.SizingTarget = null;
        }

        #endregion

        #region Breath Effect

        /// <summary>
        /// Applies a breathing zoom effect: zooms in to peak, then settles back slightly.
        /// </summary>
        private static void ApplyBreathEffect(
            Control target,
            TimeSpan duration,
            FlowerySlideEffectParams @params,
            Action? onComplete)
        {
            double intensity = @params.BreathIntensity;
            bool zoomIn = _random.Next(2) == 0;

            double startScale, peakScale, endScale;
            if (zoomIn)
            {
                startScale = 1.0;
                peakScale = 1.0 + intensity;
                endScale = 1.0 + (intensity * 0.33);
            }
            else
            {
                startScale = 1.0 + intensity;
                peakScale = 1.0;
                endScale = 1.0 + (intensity * 0.66);
            }

            // Two-phase animation: start->peak, then peak->end
            TimeSpan halfDuration = TimeSpan.FromSeconds(duration.TotalSeconds * 0.5);

            ApplyZoomAnimation(target, startScale, peakScale, halfDuration, () =>
            {
                ApplyZoomAnimation(target, peakScale, endScale, halfDuration, onComplete);
            });
        }

        #endregion

        #region Throw Effect

        private static bool _lastThrowDirectionPositive = true;

        /// <summary>
        /// Applies a dramatic fly-through effect: image scales up at center while panning across.
        /// </summary>
        private static void ApplyThrowEffect(
            Control target,
            TimeSpan duration,
            FlowerySlideEffectParams @params,
            Action? onComplete)
        {
            _lastThrowDirectionPositive = !_lastThrowDirectionPositive;
            bool direction = _lastThrowDirectionPositive;

            double viewportWidth = target.Bounds.Width;
            double viewportHeight = target.Bounds.Height;
            if (viewportWidth <= 0) viewportWidth = 400;
            if (viewportHeight <= 0) viewportHeight = 300;

            bool isWiderThanTall = viewportWidth > viewportHeight;
            double centerScale = Math.Max(1.25, @params.ThrowScale);
            double edgeScale = 1.0;

            double panDistance = @params.PanDistance;
            double startPan = direction ? panDistance : -panDistance;
            double endPan = direction ? -panDistance : panDistance;

            TimeSpan halfDuration = TimeSpan.FromSeconds(duration.TotalSeconds * 0.5);

            // First half: edge->center scale, start pan
            if (isWiderThanTall)
            {
                ApplyCombinedAnimation(target, edgeScale, centerScale, startPan, 0, 0, 0, halfDuration, () =>
                {
                    // Second half: center->edge scale, complete pan
                    ApplyCombinedAnimation(target, centerScale, edgeScale, 0, endPan, 0, 0, halfDuration, onComplete);
                });
            }
            else
            {
                ApplyCombinedAnimation(target, edgeScale, centerScale, 0, 0, startPan, 0, halfDuration, () =>
                {
                    ApplyCombinedAnimation(target, centerScale, edgeScale, 0, 0, 0, endPan, halfDuration, onComplete);
                });
            }
        }

        #endregion

        #endregion

        #region Transform Preparation

        /// <summary>
        /// Ensures the element has a TransformOperations builder set up for animation.
        /// Sets RenderTransformOrigin to center for symmetric transformations.
        /// In C# this uses RelativePoint(0.5, 0.5, RelativeUnit.Relative); in AXAML use
        /// "50%,50%" or omit RenderTransformOrigin to keep Avalonia's default center origin.
        /// Do not copy the C# numeric form into AXAML, because "0.5,0.5" is not a centered
        /// relative point there and can make rotations pivot near the top-left corner.
        /// </summary>
        public static void EnsureTransformBuilder(Control element)
        {
            element.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            if (element.RenderTransform == null)
            {
                element.RenderTransform = new ScaleTransform(1, 1);
            }
        }

        /// <summary>
        /// Resets a control's transform to its default state (no transformation).
        /// </summary>
        public static void ResetTransform(Control element)
        {
            element.RenderTransform = new ScaleTransform(1, 1);
        }

        #endregion

        #region Easing Helpers

        /// <summary>
        /// Maps Flowery EasingMode to Avalonia Easing instance.
        /// </summary>
        public static Easing GetEasing(EasingMode mode)
        {
            return mode switch
            {
                EasingMode.EaseIn => new QuadraticEaseIn(),
                EasingMode.EaseOut => new QuadraticEaseOut(),
                EasingMode.EaseInOut => new QuadraticEaseInOut(),
                _ => new QuadraticEaseInOut()
            };
        }

        #endregion

        #region Animation Helpers

        /// <summary>
        /// Applies a zoom (scale) animation to a control.
        /// </summary>
        public static void ApplyZoomAnimation(
            Control target,
            double fromScale,
            double toScale,
            TimeSpan duration,
            Action? onComplete,
            Easing? easing = null)
        {
            RunTimedAnimation(
                duration,
                easing ?? new QuadraticEaseInOut(),
                progress =>
                {
                    var scale = Interpolate(fromScale, toScale, progress);
                    target.RenderTransform = new ScaleTransform(scale, scale);
                },
                onComplete);
        }

        /// <summary>
        /// Applies a pan (translate) animation to a control.
        /// </summary>
        public static void ApplyPanAnimation(
            Control target,
            double translateX,
            double translateY,
            TimeSpan duration,
            Action? onComplete,
            Easing? easing = null)
        {
            RunTimedAnimation(
                duration,
                easing ?? new QuadraticEaseInOut(),
                progress =>
                {
                    target.RenderTransform = new TranslateTransform(
                        Interpolate(0, translateX, progress),
                        Interpolate(0, translateY, progress));
                },
                onComplete);
        }

        /// <summary>
        /// Applies a combined scale and translate animation.
        /// </summary>
        public static void ApplyCombinedAnimation(
            Control target,
            double fromScale,
            double toScale,
            double fromTranslateX,
            double toTranslateX,
            double fromTranslateY,
            double toTranslateY,
            TimeSpan duration,
            Action? onComplete,
            Easing? easing = null)
        {
            RunTimedAnimation(
                duration,
                easing ?? new QuadraticEaseInOut(),
                progress =>
                {
                    var transform = new TransformGroup();
                    var scale = Interpolate(fromScale, toScale, progress);
                    transform.Children.Add(new ScaleTransform(scale, scale));
                    transform.Children.Add(new TranslateTransform(
                        Interpolate(fromTranslateX, toTranslateX, progress),
                        Interpolate(fromTranslateY, toTranslateY, progress)));
                    target.RenderTransform = transform;
                },
                onComplete);
        }

        /// <summary>
        /// Applies a pulse (breathing) animation that scales up then back down.
        /// </summary>
        public static void ApplyPulseAnimation(
            Control target,
            double intensity,
            TimeSpan duration,
            Action? onComplete,
            Easing? easing = null)
        {
            RunTimedAnimation(
                duration,
                easing ?? new QuadraticEaseInOut(),
                progress =>
                {
                    var localProgress = progress <= 0.5
                        ? progress * 2
                        : (1 - progress) * 2;
                    var scale = Interpolate(1.0, 1.0 + intensity, localProgress);
                    target.RenderTransform = new ScaleTransform(scale, scale);
                },
                onComplete);
        }

        /// <summary>
        /// Applies a fade animation to a control's opacity.
        /// </summary>
        public static void ApplyFadeAnimation(
            Control target,
            double fromOpacity,
            double toOpacity,
            TimeSpan duration,
            Action? onComplete,
            Easing? easing = null)
        {
            RunTimedAnimation(
                duration,
                easing ?? new QuadraticEaseInOut(),
                progress =>
                {
                    target.Opacity = Interpolate(fromOpacity, toOpacity, progress);
                },
                onComplete);
        }

        /// <summary>
        /// Creates a fade-in animation (0 to 1).
        /// </summary>
        public static void ApplyFadeInAnimation(Control target, TimeSpan duration, Action? onComplete = null)
        {
            ApplyFadeAnimation(target, 0.0, 1.0, duration, onComplete);
        }

        /// <summary>
        /// Creates a fade-out animation (1 to 0).
        /// </summary>
        public static void ApplyFadeOutAnimation(Control target, TimeSpan duration, Action? onComplete = null)
        {
            ApplyFadeAnimation(target, 1.0, 0.0, duration, onComplete);
        }

        /// <summary>
        /// Applies a rotation animation.
        /// </summary>
        public static void ApplyRotationAnimation(
            Control target,
            double fromDegrees,
            double toDegrees,
            TimeSpan duration,
            Action? onComplete)
        {
            RunTimedAnimation(
                duration,
                new QuadraticEaseInOut(),
                progress =>
                {
                    target.RenderTransform = new RotateTransform(Interpolate(fromDegrees, toDegrees, progress));
                },
                onComplete);
        }

        #endregion

        #region Animation Runner

        private static void RunTimedAnimation(
            TimeSpan duration,
            Easing easing,
            Action<double> applyFrame,
            Action? onComplete)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var durationMilliseconds = Math.Max(1.0, duration.TotalMilliseconds);
                var startedAt = DateTime.UtcNow;

                void ApplyProgress(double progress)
                {
                    applyFrame(easing.Ease(Math.Max(0.0, Math.Min(1.0, progress))));
                }

                ApplyProgress(0.0);

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                timer.Tick += (_, _) =>
                {
                    var elapsedMilliseconds = (DateTime.UtcNow - startedAt).TotalMilliseconds;
                    var progress = elapsedMilliseconds / durationMilliseconds;

                    if (progress >= 1.0)
                    {
                        ApplyProgress(1.0);
                        timer.Stop();
                        onComplete?.Invoke();
                        return;
                    }

                    ApplyProgress(progress);
                };
                timer.Start();
            });
        }

        private static double Interpolate(double from, double to, double progress)
        {
            return from + ((to - from) * progress);
        }

        #endregion
    }
}
