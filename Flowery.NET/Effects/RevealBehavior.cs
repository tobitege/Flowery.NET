using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;

namespace Flowery.Effects
{
    /// <summary>
    /// Reveals an element with fade-in and slide animation when it enters the visual tree.
    /// Works on any Control via attached properties.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;Border fx:RevealBehavior.IsEnabled="True"
    ///         fx:RevealBehavior.Duration="0:0:0.5"
    ///         fx:RevealBehavior.Direction="Bottom"/&gt;
    /// </code>
    /// </example>
    public static class RevealBehavior
    {
        #region Attached Properties

        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Visual, bool>(
                "IsEnabled", typeof(RevealBehavior), false);

        public static readonly AttachedProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.RegisterAttached<Visual, TimeSpan>(
                "Duration", typeof(RevealBehavior), TimeSpan.FromMilliseconds(500));

        public static readonly AttachedProperty<RevealDirection> DirectionProperty =
            AvaloniaProperty.RegisterAttached<Visual, RevealDirection>(
                "Direction", typeof(RevealBehavior), RevealDirection.Bottom);

        public static readonly AttachedProperty<double> DistanceProperty =
            AvaloniaProperty.RegisterAttached<Visual, double>(
                "Distance", typeof(RevealBehavior), 30.0);

        public static readonly AttachedProperty<Easing> EasingProperty =
            AvaloniaProperty.RegisterAttached<Visual, Easing>(
                "Easing", typeof(RevealBehavior), new CubicEaseOut());

        #endregion

        #region Getters/Setters

        public static bool GetIsEnabled(Visual element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Visual element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static TimeSpan GetDuration(Visual element) => element.GetValue(DurationProperty);
        public static void SetDuration(Visual element, TimeSpan value) => element.SetValue(DurationProperty, value);

        public static RevealDirection GetDirection(Visual element) => element.GetValue(DirectionProperty);
        public static void SetDirection(Visual element, RevealDirection value) => element.SetValue(DirectionProperty, value);

        public static double GetDistance(Visual element) => element.GetValue(DistanceProperty);
        public static void SetDistance(Visual element, double value) => element.SetValue(DistanceProperty, value);

        public static Easing GetEasing(Visual element) => element.GetValue(EasingProperty);
        public static void SetEasing(Visual element, Easing value) => element.SetValue(EasingProperty, value);

        #endregion

        static RevealBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Visual>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(Visual element, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                element.AttachedToVisualTree += OnAttachedToVisualTree;
            }
            else
            {
                element.AttachedToVisualTree -= OnAttachedToVisualTree;
            }
        }

        private static async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Visual element) return;

            var duration = GetDuration(element);
            var direction = GetDirection(element);
            var distance = GetDistance(element);
            var easing = GetEasing(element);

            // Calculate start offset based on direction
            var (startX, startY) = direction switch
            {
                RevealDirection.Top => (0.0, -distance),
                RevealDirection.Bottom => (0.0, distance),
                RevealDirection.Left => (-distance, 0.0),
                RevealDirection.Right => (distance, 0.0),
                _ => (0.0, distance)
            };

            // Set initial state
            var transform = new TranslateTransform { X = startX, Y = startY };
            element.RenderTransform = transform;
            element.Opacity = 0;

            // Small delay to ensure layout is complete
            await Task.Delay(16);

            // Animate using WASM-compatible helper
            using var cts = new CancellationTokenSource();

            await AnimationHelper.AnimateAsync(
                t =>
                {
                    element.Opacity = t;
                    transform.X = AnimationHelper.Lerp(startX, 0, t);
                    transform.Y = AnimationHelper.Lerp(startY, 0, t);
                },
                duration,
                easing,
                ct: cts.Token);

            // Ensure final state
            element.Opacity = 1;
            transform.X = 0;
            transform.Y = 0;
        }
    }

    /// <summary>
    /// Direction from which the reveal animation originates.
    /// </summary>
    public enum RevealDirection
    {
        Top,
        Bottom,
        Left,
        Right
    }
}
