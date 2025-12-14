using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.VisualTree;

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

        public static readonly AttachedProperty<RevealMode> ModeProperty =
            AvaloniaProperty.RegisterAttached<Visual, RevealMode>(
                "Mode", typeof(RevealBehavior), RevealMode.FadeReveal);

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
                "Easing", typeof(RevealBehavior), new QuadraticEaseOut());

        #endregion

        #region Getters/Setters

        public static bool GetIsEnabled(Visual element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Visual element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static RevealMode GetMode(Visual element) => element.GetValue(ModeProperty);
        public static void SetMode(Visual element, RevealMode value) => element.SetValue(ModeProperty, value);

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

                // If already attached to visual tree, start animation immediately
                if (element.GetVisualRoot() != null)
                {
                    StartRevealAnimation(element);
                }
            }
            else
            {
                element.AttachedToVisualTree -= OnAttachedToVisualTree;
            }
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Visual element) return;
            StartRevealAnimation(element);
        }

        private static async void StartRevealAnimation(Visual element)
        {
            var mode = GetMode(element);
            var duration = GetDuration(element);
            var direction = GetDirection(element);
            var distance = GetDistance(element);
            var easing = GetEasing(element);

            // Determine animation behavior based on mode
            var hasTranslate = mode is RevealMode.FadeReveal or RevealMode.SlideIn or RevealMode.ScaleSlide;
            var hasScale = mode is RevealMode.Scale or RevealMode.ScaleSlide;
            var hasFade = mode is RevealMode.FadeReveal or RevealMode.FadeOnly or RevealMode.Scale or RevealMode.ScaleSlide;

            // Calculate start offset based on direction (for translate modes)
            var (startX, startY) = hasTranslate ? direction switch
            {
                RevealDirection.Top => (0.0, -distance),
                RevealDirection.Bottom => (0.0, distance),
                RevealDirection.Left => (-distance, 0.0),
                RevealDirection.Right => (distance, 0.0),
                _ => (0.0, distance)
            } : (0.0, 0.0);

            // Set up transforms
            TranslateTransform? translateTransform = null;
            ScaleTransform? scaleTransform = null;

            if (hasTranslate && hasScale)
            {
                // Use TransformGroup for combined transforms
                translateTransform = new TranslateTransform { X = startX, Y = startY };
                scaleTransform = new ScaleTransform { ScaleX = 0.8, ScaleY = 0.8 };
                element.RenderTransform = new TransformGroup
                {
                    Children = { scaleTransform, translateTransform }
                };
                element.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }
            else if (hasTranslate)
            {
                translateTransform = new TranslateTransform { X = startX, Y = startY };
                element.RenderTransform = translateTransform;
            }
            else if (hasScale)
            {
                scaleTransform = new ScaleTransform { ScaleX = 0.8, ScaleY = 0.8 };
                element.RenderTransform = scaleTransform;
                element.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }

            // Set initial opacity
            if (hasFade)
            {
                element.Opacity = 0;
            }

            // Wait for layout to complete - use 2 frames to ensure rendering
            await Task.Delay(32);

            // Time-based animation for accurate timing
            var startTime = DateTime.UtcNow;
            var endTime = startTime + duration;

            while (DateTime.UtcNow < endTime)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var rawT = Math.Min(1.0, elapsed / duration.TotalMilliseconds);
                var easedT = easing.Ease(rawT);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (hasFade)
                    {
                        element.Opacity = easedT;
                    }
                    if (translateTransform != null)
                    {
                        translateTransform.X = AnimationHelper.Lerp(startX, 0, easedT);
                        translateTransform.Y = AnimationHelper.Lerp(startY, 0, easedT);
                    }
                    if (scaleTransform != null)
                    {
                        var scale = AnimationHelper.Lerp(0.8, 1.0, easedT);
                        scaleTransform.ScaleX = scale;
                        scaleTransform.ScaleY = scale;
                    }
                });

                await Task.Delay(16); // ~60fps
            }

            // Ensure final state
            element.Opacity = 1;
            if (translateTransform != null)
            {
                translateTransform.X = 0;
                translateTransform.Y = 0;
            }
            if (scaleTransform != null)
            {
                scaleTransform.ScaleX = 1;
                scaleTransform.ScaleY = 1;
            }
        }
    }

    /// <summary>
    /// Animation mode for reveal effect.
    /// </summary>
    public enum RevealMode
    {
        /// <summary>
        /// Fades in opacity while sliding into position (default).
        /// </summary>
        FadeReveal,

        /// <summary>
        /// Slides in from off-screen while staying fully visible (no fade).
        /// </summary>
        SlideIn,

        /// <summary>
        /// Pure fade-in with no movement.
        /// </summary>
        FadeOnly,

        /// <summary>
        /// Scales up from center while fading in.
        /// </summary>
        Scale,

        /// <summary>
        /// Scales up while sliding into position with fade.
        /// </summary>
        ScaleSlide
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
