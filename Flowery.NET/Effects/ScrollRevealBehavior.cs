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

        private static readonly AttachedProperty<ScrollSubscription?> SubscriptionProperty =
            AvaloniaProperty.RegisterAttached<Control, ScrollSubscription?>("Subscription", typeof(ScrollRevealBehavior));

        private sealed class ScrollSubscription(Control element, ScrollViewer viewer)
        {
            public ScrollViewer Viewer { get; } = viewer;
            public void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => CheckVisibility(element, Viewer);
        }

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
                element.AttachedToVisualTree += OnAttached;
                element.DetachedFromVisualTree += OnDetached;

                if (TopLevel.GetTopLevel(element) != null)
                {
                    AttachToScroll(element);
                }
            }
            else
            {
                element.AttachedToVisualTree -= OnAttached;
                element.DetachedFromVisualTree -= OnDetached;
                DetachFromScroll(element);
                SetWasTriggered(element, false);
            }
        }

        private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control) AttachToScroll(control);
        }

        private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control) DetachFromScroll(control);
        }

        private static void AttachToScroll(Control element)
        {
            DetachFromScroll(element);
            if (!GetIsEnabled(element) || GetWasTriggered(element)) return;

            var scrollViewer = element.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (scrollViewer != null)
            {
                var subscription = new ScrollSubscription(element, scrollViewer);
                element.SetValue(SubscriptionProperty, subscription);
                scrollViewer.ScrollChanged += subscription.OnScrollChanged;
                // Small delay before first check to ensure layout is ready
                Avalonia.Threading.Dispatcher.UIThread.Post(() => CheckVisibility(element, scrollViewer), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private static void DetachFromScroll(Control element)
        {
            if (element.GetValue(SubscriptionProperty) is { } subscription)
            {
                subscription.Viewer.ScrollChanged -= subscription.OnScrollChanged;
                element.SetValue(SubscriptionProperty, null);
            }
        }

        private static void CheckVisibility(Control element, ScrollViewer scrollViewer)
        {
            if (!GetIsEnabled(element) || GetWasTriggered(element)) return;

            var elementBounds = element.Bounds;
            var transform = element.TransformToVisual(scrollViewer);
            if (transform == null) return;

            var relativePos = new Rect(transform.Value.Transform(new Point()), elementBounds.Size);
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
