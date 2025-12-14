using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Effects
{
    /// <summary>
    /// Creates a cursor-following element that smoothly tracks mouse position.
    /// Apply to a Panel or container control.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;Panel fx:CursorFollowBehavior.IsEnabled="True"
    ///        fx:CursorFollowBehavior.FollowerSize="20"
    ///        fx:CursorFollowBehavior.FollowerBrush="{DynamicResource DaisyPrimaryBrush}"/&gt;
    /// </code>
    /// </example>
    public static class CursorFollowBehavior
    {
        #region Attached Properties

        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "IsEnabled", typeof(CursorFollowBehavior), false);

        public static readonly AttachedProperty<double> FollowerSizeProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "FollowerSize", typeof(CursorFollowBehavior), 20.0);

        public static readonly AttachedProperty<IBrush?> FollowerBrushProperty =
            AvaloniaProperty.RegisterAttached<Control, IBrush?>(
                "FollowerBrush", typeof(CursorFollowBehavior), null);

        public static readonly AttachedProperty<double> StiffnessProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "Stiffness", typeof(CursorFollowBehavior), 0.15);

        public static readonly AttachedProperty<double> DampingProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "Damping", typeof(CursorFollowBehavior), 0.85);

        // Internal: store follower element
        private static readonly AttachedProperty<Ellipse?> FollowerProperty =
            AvaloniaProperty.RegisterAttached<Control, Ellipse?>(
                "Follower", typeof(CursorFollowBehavior), null);

        // Internal: store timer
        private static readonly AttachedProperty<DispatcherTimer?> TimerProperty =
            AvaloniaProperty.RegisterAttached<Control, DispatcherTimer?>(
                "Timer", typeof(CursorFollowBehavior), null);

        // Internal: current position
        private static readonly AttachedProperty<Point> CurrentPosProperty =
            AvaloniaProperty.RegisterAttached<Control, Point>(
                "CurrentPos", typeof(CursorFollowBehavior), default);

        // Internal: target position
        private static readonly AttachedProperty<Point> TargetPosProperty =
            AvaloniaProperty.RegisterAttached<Control, Point>(
                "TargetPos", typeof(CursorFollowBehavior), default);

        // Internal: velocity
        private static readonly AttachedProperty<Point> VelocityProperty =
            AvaloniaProperty.RegisterAttached<Control, Point>(
                "Velocity", typeof(CursorFollowBehavior), default);

        #endregion

        #region Getters/Setters

        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static double GetFollowerSize(Control element) => element.GetValue(FollowerSizeProperty);
        public static void SetFollowerSize(Control element, double value) => element.SetValue(FollowerSizeProperty, value);

        public static IBrush? GetFollowerBrush(Control element) => element.GetValue(FollowerBrushProperty);
        public static void SetFollowerBrush(Control element, IBrush? value) => element.SetValue(FollowerBrushProperty, value);

        public static double GetStiffness(Control element) => element.GetValue(StiffnessProperty);
        public static void SetStiffness(Control element, double value) => element.SetValue(StiffnessProperty, value);

        public static double GetDamping(Control element) => element.GetValue(DampingProperty);
        public static void SetDamping(Control element, double value) => element.SetValue(DampingProperty, value);

        #endregion

        static CursorFollowBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(Control element, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                element.AttachedToVisualTree += OnAttachedToVisualTree;
                element.DetachedFromVisualTree += OnDetachedFromVisualTree;

                if (element.IsAttachedToVisualTree)
                {
                    SetupFollower(element);
                }
            }
            else
            {
                element.AttachedToVisualTree -= OnAttachedToVisualTree;
                element.DetachedFromVisualTree -= OnDetachedFromVisualTree;
                CleanupFollower(element);
            }
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control)
            {
                SetupFollower(control);
            }
        }

        private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control)
            {
                CleanupFollower(control);
            }
        }

        private static void SetupFollower(Control control)
        {
            // Find or create overlay panel
            var panel = control as Panel;
            if (panel == null)
            {
                // Control is not a panel - can't add follower directly
                // Try to find parent panel or use AdornerLayer
                return;
            }

            var size = GetFollowerSize(control);
            var brush = GetFollowerBrush(control) ?? new SolidColorBrush(Colors.DodgerBlue) { Opacity = 0.5 };

            // Create follower ellipse
            var follower = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                IsHitTestVisible = false,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                RenderTransform = new TranslateTransform(),
                Opacity = 0 // Hidden until mouse enters
            };

            panel.Children.Add(follower);
            control.SetValue(FollowerProperty, follower);

            // Hook pointer events
            control.PointerMoved += OnPointerMoved;
            control.PointerEntered += OnPointerEntered;
            control.PointerExited += OnPointerExited;

            // Start animation timer (60 FPS)
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => UpdateFollowerPosition(control);
            control.SetValue(TimerProperty, timer);
            timer.Start();
        }

        private static void CleanupFollower(Control control)
        {
            control.PointerMoved -= OnPointerMoved;
            control.PointerEntered -= OnPointerEntered;
            control.PointerExited -= OnPointerExited;

            var timer = control.GetValue(TimerProperty);
            if (timer != null)
            {
                timer.Stop();
                control.SetValue(TimerProperty, null);
            }

            var follower = control.GetValue(FollowerProperty);
            if (follower != null && control is Panel panel)
            {
                panel.Children.Remove(follower);
                control.SetValue(FollowerProperty, null);
            }
        }

        private static void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
            {
                var follower = control.GetValue(FollowerProperty);
                if (follower != null)
                {
                    follower.Opacity = 1;
                }
            }
        }

        private static void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
            {
                var follower = control.GetValue(FollowerProperty);
                if (follower != null)
                {
                    follower.Opacity = 0;
                }
            }
        }

        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;

            var pos = e.GetPosition(control);
            var size = GetFollowerSize(control);

            // Offset to center the follower on cursor
            var targetPos = new Point(pos.X - size / 2, pos.Y - size / 2);
            control.SetValue(TargetPosProperty, targetPos);
        }

        private static void UpdateFollowerPosition(Control control)
        {
            var follower = control.GetValue(FollowerProperty);
            if (follower == null) return;

            var currentPos = control.GetValue(CurrentPosProperty);
            var targetPos = control.GetValue(TargetPosProperty);
            var velocity = control.GetValue(VelocityProperty);

            var stiffness = GetStiffness(control);
            var damping = GetDamping(control);

            // Spring physics
            var dx = targetPos.X - currentPos.X;
            var dy = targetPos.Y - currentPos.Y;

            // Apply stiffness to acceleration
            var ax = dx * stiffness;
            var ay = dy * stiffness;

            // Update velocity with damping
            var vx = (velocity.X + ax) * damping;
            var vy = (velocity.Y + ay) * damping;

            // Update position
            var newX = currentPos.X + vx;
            var newY = currentPos.Y + vy;

            var newPos = new Point(newX, newY);
            var newVelocity = new Point(vx, vy);

            control.SetValue(CurrentPosProperty, newPos);
            control.SetValue(VelocityProperty, newVelocity);

            // Apply to transform
            if (follower.RenderTransform is TranslateTransform transform)
            {
                transform.X = newX;
                transform.Y = newY;
            }
        }
    }
}
