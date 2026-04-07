using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Flowery.Effects
{
    /// <summary>
    /// Replays a reveal animation when an element becomes visible in a ScrollViewer.
    /// Works in conjunction with RevealBehavior.
    /// </summary>
    public static class ScrollRevealBehavior
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(ScrollRevealBehavior), false);

        private static readonly AttachedProperty<bool> WasTriggeredProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("WasTriggered", typeof(ScrollRevealBehavior), false);

        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static bool GetWasTriggered(Control element) => element.GetValue(WasTriggeredProperty);
        private static void SetWasTriggered(Control element, bool value) => element.SetValue(WasTriggeredProperty, value);

        static ScrollRevealBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(Control element, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                element.AttachedToVisualTree += (s, ev) => AttachToScroll(element);
                element.DetachedFromVisualTree += (s, ev) => DetachFromScroll(element);

                if (TopLevel.GetTopLevel(element) != null)
                {
                    AttachToScroll(element);
                }
            }
            else
            {
                DetachFromScroll(element);
                SetWasTriggered(element, false);
            }
        }

        private static void AttachToScroll(Control element)
        {
            // Hide element initially if RevealBehavior is configured
            if (RevealBehavior.GetIsEnabled(element) && RevealBehavior.GetManualTriggerOnly(element))
            {
                element.Opacity = 0;
            }

            var scrollViewer = element.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += (s, e) => CheckVisibility(element, scrollViewer);
                // Small delay before first check to ensure layout is ready
                Avalonia.Threading.Dispatcher.UIThread.Post(() => CheckVisibility(element, scrollViewer), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private static void DetachFromScroll(Control element)
        {
            // Reset triggered state on detach so it can re-trigger next time it's shown if desired
            // though typically we only want once per "session"
        }

        private static void CheckVisibility(Control element, ScrollViewer scrollViewer)
        {
            if (!GetIsEnabled(element) || GetWasTriggered(element)) return;

            var elementBounds = element.Bounds;
            var transform = element.TransformToVisual(scrollViewer);
            if (transform == null) return;

            var relativePos = new Rect(transform.Value.Transform(elementBounds.Position), elementBounds.Size);
            var viewport = new Rect(0, 0, scrollViewer.Bounds.Width, scrollViewer.Bounds.Height);

            if (viewport.Intersects(relativePos))
            {
                SetWasTriggered(element, true);

                // Trigger RevealBehavior if present
                if (RevealBehavior.GetIsEnabled(element))
                {
                    RevealBehavior.TriggerReveal(element);
                }
            }
        }
    }
}
