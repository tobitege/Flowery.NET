using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;

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
        private static readonly AttachedProperty<RevealRun?> RunProperty =
            AvaloniaProperty.RegisterAttached<Visual, RevealRun?>("Run", typeof(RevealBehavior));

        private sealed class RevealRun(Visual element)
        {
            public CancellationTokenSource Cancellation { get; } = new();
            public ITransform? OriginalTransform { get; } = element.RenderTransform;
            public RelativePoint OriginalOrigin { get; } = element.RenderTransformOrigin;
            public double OriginalOpacity { get; } = element.Opacity;

            public void Restore(Visual target)
            {
                target.RenderTransform = OriginalTransform;
                target.RenderTransformOrigin = OriginalOrigin;
                target.Opacity = OriginalOpacity;
            }
        }

        private static void StopReveal(Visual element)
        {
            if (element.GetValue(RunProperty) is not { } run) return;
            element.SetValue(RunProperty, null);
            element.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            run.Cancellation.Cancel();
            run.Restore(element);
        }

        private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Visual visual) StopReveal(visual);
        }
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

        /// <summary>
        /// When true, the reveal animation will not auto-trigger on attach.
        /// Use TriggerReveal() to manually start the animation (e.g., from ScrollRevealBehavior).
        /// </summary>
        public static readonly AttachedProperty<bool> ManualTriggerOnlyProperty =
            AvaloniaProperty.RegisterAttached<Visual, bool>(
                "ManualTriggerOnly", typeof(RevealBehavior), false);

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

        public static bool GetManualTriggerOnly(Visual element) => element.GetValue(ManualTriggerOnlyProperty);
        public static void SetManualTriggerOnly(Visual element, bool value) => element.SetValue(ManualTriggerOnlyProperty, value);

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

                // If already attached to visual tree, start animation immediately (unless manual trigger only)
                if (TopLevel.GetTopLevel(element) != null && !GetManualTriggerOnly(element))
                {
                    StartRevealAnimation(element);
                }
            }
            else
            {
                element.AttachedToVisualTree -= OnAttachedToVisualTree;
                StopReveal(element);
            }
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Visual element) return;
            if (GetManualTriggerOnly(element)) return;
            StartRevealAnimation(element);
        }

        /// <summary>
        /// Manually triggers the reveal animation for the specified element.
        /// </summary>
        public static void TriggerReveal(Visual element)
        {
            StartRevealAnimation(element);
        }

        private static async void StartRevealAnimation(Visual element)
        {
            StopReveal(element);
            if (DaisyAccessibility.IsReducedMotion) return;
            if (TopLevel.GetTopLevel(element) == null) return;
            var run = new RevealRun(element);
            element.SetValue(RunProperty, run);
            element.DetachedFromVisualTree += OnDetachedFromVisualTree;
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

            if (element.RenderTransform is Transform effectTransform && !ReferenceEquals(effectTransform, run.OriginalTransform)
                && run.OriginalTransform is { } originalTransform)
            {
                element.RenderTransform = new TransformGroup
                {
                    Children = { new MatrixTransform(originalTransform.Value), effectTransform }
                };
            }
            try
            {
                await AnimationHelper.AnimateAsync(easedT =>
                {
                    if (run.Cancellation.IsCancellationRequested) return;
                    if (hasFade)
                    {
                        element.Opacity = run.OriginalOpacity * easedT;
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
                }, duration, easing, ct: run.Cancellation.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (ReferenceEquals(element.GetValue(RunProperty), run))
                {
                    element.SetValue(RunProperty, null);
                    element.DetachedFromVisualTree -= OnDetachedFromVisualTree;
                    run.Restore(element);
                }
                run.Cancellation.Dispose();
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
