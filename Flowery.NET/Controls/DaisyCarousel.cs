using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Controls
{
    public class DaisyCarousel : Carousel
    {
        protected override Type StyleKeyOverride => typeof(DaisyCarousel);

        private Button? _previousButton;
        private Button? _nextButton;
        private TransitioningContentControl? _transitionControl;
        private bool _isForward = true;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Unsubscribe from old buttons
            if (_previousButton != null)
                _previousButton.Click -= OnPreviousClick;
            if (_nextButton != null)
                _nextButton.Click -= OnNextClick;

            // Find and subscribe to new buttons
            _previousButton = e.NameScope.Find<Button>("PART_PreviousButton");
            _nextButton = e.NameScope.Find<Button>("PART_NextButton");
            _transitionControl = e.NameScope.Find<TransitioningContentControl>("PART_TransitioningContentControl");

            if (_previousButton != null)
                _previousButton.Click += OnPreviousClick;
            if (_nextButton != null)
                _nextButton.Click += OnNextClick;

            UpdateTransition();
            UpdateButtonVisibility();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Update button visibility when selected index or item count changes
            if (change.Property == SelectedIndexProperty || change.Property == ItemCountProperty)
            {
                UpdateButtonVisibility();
            }
        }

        private void UpdateButtonVisibility()
        {
            var itemCount = ItemCount;
            var selectedIndex = SelectedIndex;

            // Hide Previous button on first slide (or when there are no items)
            if (_previousButton != null)
            {
                _previousButton.IsVisible = selectedIndex > 0 && itemCount > 1;
            }

            // Hide Next button on last slide (or when there are no items)
            if (_nextButton != null)
            {
                _nextButton.IsVisible = selectedIndex < itemCount - 1 && itemCount > 1;
            }
        }

        private void UpdateTransition()
        {
            if (_transitionControl == null) return;

            var slide = new PageSlide(TimeSpan.FromMilliseconds(300), PageSlide.SlideAxis.Horizontal)
            {
                SlideInEasing = new CubicEaseOut(),
                SlideOutEasing = new CubicEaseIn()
            };

            _transitionControl.PageTransition = new DirectionalPageSlide(_isForward);
        }

        private void OnPreviousClick(object? sender, RoutedEventArgs e)
        {
            _isForward = false;
            UpdateTransition();
            Previous();
        }

        private void OnNextClick(object? sender, RoutedEventArgs e)
        {
            _isForward = true;
            UpdateTransition();
            Next();
        }
    }

    public class DirectionalPageSlide : IPageTransition
    {
        private readonly bool _forward;
        private readonly TimeSpan _duration = TimeSpan.FromMilliseconds(300);

        public DirectionalPageSlide(bool forward)
        {
            _forward = forward;
        }

        public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            if (to == null) return;

            var direction = _forward ? 1 : -1;
            var parentBounds = (to.GetVisualParent() as Visual)?.Bounds ?? new Rect(0, 0, 400, 300);
            var width = parentBounds.Width;

            TranslateTransform? fromTransform = null;
            if (from != null)
            {
                fromTransform = new TranslateTransform();
                from.RenderTransform = fromTransform;
            }

            var toTransform = new TranslateTransform { X = direction * width };
            to.RenderTransform = toTransform;
            to.Opacity = 0;

            // Use manual interpolation for transforms (more reliable across platforms)
            var steps = 30;
            var stepDuration = _duration.TotalMilliseconds / steps;
            var easing = new CubicEaseInOut();

            for (int i = 0; i <= steps; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var t = (double)i / steps;
                var easedT = easing.Ease(t);

                // Animate opacity and translation on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (from != null && fromTransform != null)
                    {
                        from.Opacity = 1.0 - easedT;
                        fromTransform.X = -direction * width * easedT;
                    }

                    to.Opacity = easedT;
                    toTransform.X = direction * width * (1.0 - easedT);
                });

                if (i < steps)
                    await Task.Delay((int)stepDuration, cancellationToken);
            }

            // Ensure final state on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (from != null)
                    from.Opacity = 0;
                to.Opacity = 1;
                toTransform.X = 0;
            });
        }
    }
}
