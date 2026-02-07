using System;
using System.Collections.Generic;
using Avalonia.Animation;
using Avalonia.Controls;
using Flowery.Enums;

namespace Flowery.Helpers
{
    /// <summary>
    /// Static helper class for creating slide transition animations between Controls in a Grid container.
    /// Transitions animate the change from one slide to another.
    /// </summary>
    public static class FlowerySlideTransitionHelpers
    {
        private static readonly Random _random = new();

        // Track active animations to prevent overlapping
        private static readonly Dictionary<Control, Animation> _activeTransitions = [];

        #region Public API

        /// <summary>
        /// Applies a slide transition between two controls within a Grid container.
        /// </summary>
        /// <param name="container">The parent container (Grid) holding both controls.</param>
        /// <param name="oldElement">The control transitioning out (can be null).</param>
        /// <param name="newElement">The control transitioning in.</param>
        /// <param name="transition">The type of transition to apply.</param>
        /// <param name="transitionParams">Optional parameters for the transition.</param>
        /// <param name="onComplete">Callback when transition completes.</param>
        /// <returns>The resolved transition that was applied.</returns>
        public static FlowerySlideTransition ApplyTransition(
            Grid container,
            Control? oldElement,
            Control newElement,
            FlowerySlideTransition transition,
            FlowerySlideTransitionParams? transitionParams = null,
            Action? onComplete = null)
        {
            var resolvedTransition = transition;
            var @params = transitionParams ?? new FlowerySlideTransitionParams();

            if (transition == FlowerySlideTransition.Random)
            {
                resolvedTransition = FlowerySlideTransitionParser.PickRandom(FloweryTransitionTier.Transform);
            }

            if (resolvedTransition == FlowerySlideTransition.None)
            {
                if (oldElement != null) oldElement.IsVisible = false;
                newElement.IsVisible = true;
                onComplete?.Invoke();
                return resolvedTransition;
            }

            // Ensure transforms are ready
            FloweryAnimationHelpers.EnsureTransformBuilder(newElement);
            if (oldElement != null) FloweryAnimationHelpers.EnsureTransformBuilder(oldElement);

            // Prepare elements
            newElement.IsVisible = true;
            newElement.Opacity = 1;
            if (oldElement != null) oldElement.IsVisible = true;

            var duration = @params.Duration;
            var easing = @params.EasingMode;

            var width = container.Bounds.Width > 0 ? container.Bounds.Width : 400;
            var height = container.Bounds.Height > 0 ? container.Bounds.Height : 300;

            // Apply transition core logic
            switch (resolvedTransition)
            {
                case FlowerySlideTransition.Fade:
                    ApplyFadeTransition(oldElement, newElement, duration, easing, onComplete);
                    break;

                case FlowerySlideTransition.SlideLeft:
                    ApplySlideTransition(oldElement, newElement, duration, easing, -width, 0, width, 0, onComplete);
                    break;

                case FlowerySlideTransition.SlideRight:
                    ApplySlideTransition(oldElement, newElement, duration, easing, width, 0, -width, 0, onComplete);
                    break;

                case FlowerySlideTransition.SlideUp:
                    ApplySlideTransition(oldElement, newElement, duration, easing, 0, -height, 0, height, onComplete);
                    break;

                case FlowerySlideTransition.SlideDown:
                    ApplySlideTransition(oldElement, newElement, duration, easing, 0, height, 0, -height, onComplete);
                    break;

                case FlowerySlideTransition.PushLeft:
                    ApplyPushTransition(oldElement, newElement, duration, easing, -width, 0, onComplete);
                    break;

                case FlowerySlideTransition.PushRight:
                    ApplyPushTransition(oldElement, newElement, duration, easing, width, 0, onComplete);
                    break;

                case FlowerySlideTransition.PushUp:
                    ApplyPushTransition(oldElement, newElement, duration, easing, 0, -height, onComplete);
                    break;

                case FlowerySlideTransition.PushDown:
                    ApplyPushTransition(oldElement, newElement, duration, easing, 0, height, onComplete);
                    break;

                case FlowerySlideTransition.ZoomIn:
                    ApplyZoomTransition(oldElement, newElement, duration, easing, @params.ZoomScale, 1.0, onComplete);
                    break;

                case FlowerySlideTransition.ZoomOut:
                    ApplyZoomTransition(oldElement, newElement, duration, easing, 1.0, @params.ZoomScale, onComplete);
                    break;

                default:
                    // Fallback to crossfade
                    ApplyFadeTransition(oldElement, newElement, duration, easing, onComplete);
                    break;
            }

            return resolvedTransition;
        }

        #endregion

        #region Transition Implementations

        private static void ApplyFadeTransition(Control? oldElement, Control newElement, TimeSpan duration, EasingMode easingMode, Action? onComplete)
        {
            var easing = FloweryAnimationHelpers.GetEasing(easingMode);
            newElement.Opacity = 0;
            FloweryAnimationHelpers.ApplyFadeAnimation(newElement, 0, 1, duration, onComplete, easing);

            if (oldElement != null)
            {
                FloweryAnimationHelpers.ApplyFadeAnimation(oldElement, 1, 0, duration, () => {
                    oldElement.IsVisible = false;
                }, easing);
            }
        }

        private static void ApplySlideTransition(Control? oldElement, Control newElement, TimeSpan duration, EasingMode easingMode, double oldExitX, double oldExitY, double newEnterX, double newEnterY, Action? onComplete)
        {
            var easing = FloweryAnimationHelpers.GetEasing(easingMode);
            newElement.Opacity = 0;
            FloweryAnimationHelpers.ApplyCombinedAnimation(newElement, 1.0, 1.0, newEnterX, 0, newEnterY, 0, duration, onComplete, easing);
            FloweryAnimationHelpers.ApplyFadeAnimation(newElement, 0, 1, duration, null, easing);

            if (oldElement != null)
            {
                FloweryAnimationHelpers.ApplyCombinedAnimation(oldElement, 1.0, 1.0, 0, oldExitX, 0, oldExitY, duration, () => {
                    oldElement.IsVisible = false;
                }, easing);
                FloweryAnimationHelpers.ApplyFadeAnimation(oldElement, 1, 0, duration, null, easing);
            }
        }

        private static void ApplyPushTransition(Control? oldElement, Control newElement, TimeSpan duration, EasingMode easingMode, double pushX, double pushY, Action? onComplete)
        {
            var easing = FloweryAnimationHelpers.GetEasing(easingMode);
            FloweryAnimationHelpers.ApplyCombinedAnimation(newElement, 1.0, 1.0, -pushX, 0, -pushY, 0, duration, onComplete, easing);

            if (oldElement != null)
            {
                FloweryAnimationHelpers.ApplyCombinedAnimation(oldElement, 1.0, 1.0, 0, pushX, 0, pushY, duration, () => {
                    oldElement.IsVisible = false;
                }, easing);
            }
        }

        private static void ApplyZoomTransition(Control? oldElement, Control newElement, TimeSpan duration, EasingMode easingMode, double startScale, double endScale, Action? onComplete)
        {
            var easing = FloweryAnimationHelpers.GetEasing(easingMode);
            if (startScale != 1.0 || endScale == 1.0) // Zoom In logic
            {
                newElement.Opacity = 0;
                FloweryAnimationHelpers.ApplyZoomAnimation(newElement, startScale, 1.0, duration, onComplete, easing);
                FloweryAnimationHelpers.ApplyFadeAnimation(newElement, 0, 1, duration, null, easing);

                if (oldElement != null)
                {
                    FloweryAnimationHelpers.ApplyFadeAnimation(oldElement, 1, 0, duration, () => {
                        oldElement.IsVisible = false;
                    }, easing);
                }
            }
            else // Zoom Out logic
            {
                newElement.Opacity = 0;
                FloweryAnimationHelpers.ApplyFadeAnimation(newElement, 0, 1, duration, onComplete, easing);

                if (oldElement != null)
                {
                    FloweryAnimationHelpers.ApplyZoomAnimation(oldElement, 1.0, endScale, duration, () => {
                        oldElement.IsVisible = false;
                    }, easing);
                    FloweryAnimationHelpers.ApplyFadeAnimation(oldElement, 1, 0, duration, null, easing);
                }
            }
        }
        #endregion
    }
}
