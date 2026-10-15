using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A carousel/slider control styled after DaisyUI's Carousel component.
    /// Supports prev/next navigation with animated transitions.
    /// Ported from Flowery.Uno with Avalonia-specific adaptations.
    /// </summary>
    public class DaisyCarousel : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyCarousel);

        private const double BaseTextFontSize = 14.0;

        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private Button? _previousButton;
        private Button? _nextButton;
        private Panel? _slideContainer; // Holds slides
        private readonly ContentPresenter _slidePresenter = new();
        private readonly ContentPresenter _previousSlidePresenter = new();
        private readonly Grid _transitionHost = new() { ClipToBounds = true };

        private readonly List<Control> _items = [];
        private readonly FlowerySlideshowController _slideshowController;


        private int _slideGeneration;

        public event EventHandler<FlowerySlideChangedEventArgs>? SlideChanged;

        public DaisyCarousel()
        {
            _transitionHost.Children.Add(_previousSlidePresenter);
            _transitionHost.Children.Add(_slidePresenter);
            DaisyAccessibility.RegisterMotionHandler(this, static control => ((DaisyCarousel)control).ShowCurrentSlide(false));
            _slideshowController = new FlowerySlideshowController(
                index => SelectedIndex = index,
                () => _items.Count,
                () => SelectedIndex);
            _slideshowController.WrapAround = WrapAround;
        }

        #region Dependency Properties

        public static readonly StyledProperty<int> SelectedIndexProperty =
            AvaloniaProperty.Register<DaisyCarousel, int>(nameof(SelectedIndex), 0, coerce: CoerceSelectedIndex);

        public int SelectedIndex
        {
            get => GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        private static int CoerceSelectedIndex(AvaloniaObject sender, int value)
        {
            if (sender is DaisyCarousel c && c.ItemCount > 0)
            {
                return Math.Max(0, Math.Min(value, c.ItemCount - 1));
            }
            return 0;
        }

        public static readonly StyledProperty<bool> ShowNavigationProperty =
            AvaloniaProperty.Register<DaisyCarousel, bool>(nameof(ShowNavigation), true);

        public bool ShowNavigation
        {
            get => GetValue(ShowNavigationProperty);
            set => SetValue(ShowNavigationProperty, value);
        }

        public static readonly StyledProperty<bool> WrapAroundProperty =
            AvaloniaProperty.Register<DaisyCarousel, bool>(nameof(WrapAround), false);

        public bool WrapAround
        {
            get => GetValue(WrapAroundProperty);
            set => SetValue(WrapAroundProperty, value);
        }

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyCarousel, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly StyledProperty<double> SwipeThresholdProperty =
            AvaloniaProperty.Register<DaisyCarousel, double>(nameof(SwipeThreshold), 50.0);

        public double SwipeThreshold
        {
            get => GetValue(SwipeThresholdProperty);
            set => SetValue(SwipeThresholdProperty, value);
        }

        public static readonly StyledProperty<FlowerySlideshowMode> ModeProperty =
            AvaloniaProperty.Register<DaisyCarousel, FlowerySlideshowMode>(nameof(Mode), FlowerySlideshowMode.Manual);

        public FlowerySlideshowMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly StyledProperty<double> SlideIntervalProperty =
            AvaloniaProperty.Register<DaisyCarousel, double>(nameof(SlideInterval), 3.0);

        public double SlideInterval
        {
            get => GetValue(SlideIntervalProperty);
            set => SetValue(SlideIntervalProperty, value);
        }

        public static readonly StyledProperty<FlowerySlideEffect> SlideEffectProperty =
            AvaloniaProperty.Register<DaisyCarousel, FlowerySlideEffect>(nameof(SlideEffect), FlowerySlideEffect.None);

        public FlowerySlideEffect SlideEffect
        {
            get => GetValue(SlideEffectProperty);
            set => SetValue(SlideEffectProperty, value);
        }

        public static readonly StyledProperty<FlowerySlideTransition> SlideTransitionProperty =
            AvaloniaProperty.Register<DaisyCarousel, FlowerySlideTransition>(nameof(SlideTransition), FlowerySlideTransition.None);

        public FlowerySlideTransition SlideTransition
        {
            get => GetValue(SlideTransitionProperty);
            set => SetValue(SlideTransitionProperty, value);
        }

        public static readonly StyledProperty<double> TransitionDurationProperty =
           AvaloniaProperty.Register<DaisyCarousel, double>(nameof(TransitionDuration), 0.4);

        public double TransitionDuration
        {
            get => GetValue(TransitionDurationProperty);
            set => SetValue(TransitionDurationProperty, value);
        }

        public int ItemCount => _items.Count;

        #endregion

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            StopSlideAnimation();

            if (_previousButton != null) _previousButton.Click -= OnPreviousClick;
            if (_nextButton != null) _nextButton.Click -= OnNextClick;
            _slideContainer?.Children.Clear();

            _previousButton = e.NameScope.Find<Button>("PART_PreviousButton");
            _nextButton = e.NameScope.Find<Button>("PART_NextButton");
            _slideContainer = e.NameScope.Find<Panel>("PART_SlideContainer");
            UpdateNavigationAutomationProperties();

            if (_previousButton != null) _previousButton.Click += OnPreviousClick;
            if (_nextButton != null) _nextButton.Click += OnNextClick;

            UpdateButtonVisibility();
            ShowCurrentSlide(false);
            _slideshowController.Start();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (_slideContainer != null)
            {
                ShowCurrentSlide(false);
                _slideshowController.Start();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _slideshowController.Stop();
            StopSlideAnimation();
            _previousSlidePresenter.Content = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == AutomationProperties.HelpTextProperty
                || change.Property == AutomationProperties.LabeledByProperty
                || change.Property == AutomationProperties.AutomationIdProperty
                || change.Property == AutomationProperties.IsRequiredForFormProperty)
            {
                UpdateNavigationAutomationProperties();
            }

            if (change.Property == SelectedIndexProperty)
            {
                var oldIndex = (int)change.OldValue!;
                var newIndex = (int)change.NewValue!;

                if (oldIndex != newIndex)
                {
                    NavigateToSlide(newIndex, oldIndex);
                }

                UpdateButtonVisibility();
                SlideChanged?.Invoke(this, new FlowerySlideChangedEventArgs(oldIndex, newIndex));
            }
            else if (change.Property == ModeProperty)
            {
                _slideshowController.Mode = Mode;
                UpdateButtonVisibility();
            }
            else if (change.Property == SlideIntervalProperty)
            {
                _slideshowController.Interval = SlideInterval;
            }
            else if (change.Property == ContentProperty)
            {
                RefreshItems();
            }
            else if (change.Property == OrientationProperty || change.Property == ShowNavigationProperty || change.Property == WrapAroundProperty)
            {
                if (change.Property == WrapAroundProperty)
                {
                    _slideshowController.WrapAround = WrapAround;
                }
                UpdateButtonVisibility();
            }
        }

        private void UpdateNavigationAutomationProperties()
        {
            if (_previousButton != null)
            {
                DaisyAccessibility.ApplyAutomationProperties(
                    this,
                    _previousButton,
                    FloweryLocalization.GetStringInternal(
                        "Accessibility_PreviousSlide",
                        "Previous slide"),
                    automationIdSuffix: "previous");
            }

            if (_nextButton != null)
            {
                DaisyAccessibility.ApplyAutomationProperties(
                    this,
                    _nextButton,
                    FloweryLocalization.GetStringInternal(
                        "Accessibility_NextSlide",
                        "Next slide"),
                    automationIdSuffix: "next");
            }
        }

        private void RefreshItems()
        {
            _slidePresenter.Content = null;
            _items.Clear();
            if (Content is Panel panel)
            {
                var children = new List<Control>(panel.Children);
                panel.Children.Clear();
                _items.AddRange(children);
            }
            else if (Content is Control c)
            {
                _items.Add(c);
            }

            // Ensure SelectedIndex is valid
            if (SelectedIndex >= _items.Count && _items.Count > 0)
                SelectedIndex = 0;

            ShowCurrentSlide(false);
            UpdateButtonVisibility();
        }

        private void UpdateButtonVisibility()
        {
            if (_previousButton == null || _nextButton == null) return;

            bool showNav = ShowNavigation;

            // Adjust chevron icons/orientation
            // Adjust chevron icons/orientation
            if (Orientation == Orientation.Horizontal)
            {
                if (_previousButton.Content is PathIcon prevIcon)
                    prevIcon.Data = StreamGeometry.Parse(FloweryCarouselPaths.Left);
                if (_nextButton.Content is PathIcon nextIcon)
                    nextIcon.Data = StreamGeometry.Parse(FloweryCarouselPaths.Right);

                _previousButton.HorizontalAlignment = HorizontalAlignment.Left;
                _previousButton.VerticalAlignment = VerticalAlignment.Center;

                _nextButton.HorizontalAlignment = HorizontalAlignment.Right;
                _nextButton.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                // Vertical
                if (_previousButton.Content is PathIcon prevIcon)
                    prevIcon.Data = StreamGeometry.Parse(FloweryCarouselPaths.Up);
                if (_nextButton.Content is PathIcon nextIcon)
                    nextIcon.Data = StreamGeometry.Parse(FloweryCarouselPaths.Down);

                _previousButton.HorizontalAlignment = HorizontalAlignment.Center;
                _previousButton.VerticalAlignment = VerticalAlignment.Top;

                _nextButton.HorizontalAlignment = HorizontalAlignment.Center;
                _nextButton.VerticalAlignment = VerticalAlignment.Bottom;
            }

            // Simple visibility logic
            if (!showNav || ItemCount <= 1)
            {
                _previousButton.IsVisible = false;
                _nextButton.IsVisible = false;
            }
            else
            {
                if (WrapAround || Mode == FlowerySlideshowMode.Kiosk || Mode == FlowerySlideshowMode.Random)
                {
                    _previousButton.IsVisible = true;
                    _nextButton.IsVisible = true;
                }
                else
                {
                    _previousButton.IsVisible = SelectedIndex > 0;
                    _nextButton.IsVisible = SelectedIndex < ItemCount - 1;
                }
            }
        }

        private void OnPreviousClick(object? sender, RoutedEventArgs e)
        {
            if (ItemCount == 0) return;
            int newIndex = SelectedIndex - 1;
            if (newIndex < 0)
            {
                if (WrapAround) newIndex = ItemCount - 1;
                else return;
            }
            SelectedIndex = newIndex;
        }

        private void OnNextClick(object? sender, RoutedEventArgs e)
        {
            if (ItemCount == 0) return;
            int newIndex = SelectedIndex + 1;
            if (newIndex >= ItemCount)
            {
                if (WrapAround) newIndex = 0;
                else return;
            }
            SelectedIndex = newIndex;
        }

        private void NavigateToSlide(int newIndex, int oldIndex)
        {
            if (_slideContainer == null) return;
            if (newIndex < 0 || newIndex >= _items.Count) return;

            var oldItem = oldIndex >= 0 && oldIndex < _items.Count ? _items[oldIndex] : null;

            ShowCurrentSlide(true, oldItem);
        }

        private void StopSlideAnimation()
        {
            _slideGeneration++;
            FloweryAnimationHelpers.StopAnimations(_slidePresenter);
            FloweryAnimationHelpers.StopAnimations(_previousSlidePresenter);
            FloweryAnimationHelpers.ResetTransform(_slidePresenter);
            FloweryAnimationHelpers.ResetTransform(_previousSlidePresenter);
        }

        private void ShowCurrentSlide(bool animate, Control? oldItem = null)
        {
            StopSlideAnimation();
            _slidePresenter.Content = null;
            _previousSlidePresenter.Content = null;
            if (_slideContainer == null) return;
            if (_items.Count == 0)
            {
                _slideContainer.Children.Clear();
                return;
            }
            _slidePresenter.Content = _items[SelectedIndex];
            if (!_slideContainer.Children.Contains(_transitionHost))
            {
                _slideContainer.Children.Clear();
                _slideContainer.Children.Add(_transitionHost);
            }
            var generation = _slideGeneration;
            void Complete()
            {
                if (generation != _slideGeneration) return;
                FloweryAnimationHelpers.StopAnimations(_previousSlidePresenter);
                _previousSlidePresenter.Content = null;
                _previousSlidePresenter.IsVisible = false;
                if (TopLevel.GetTopLevel(this) != null && !DaisyAccessibility.IsReducedMotion)
                    FloweryAnimationHelpers.ApplySlideEffect(_slidePresenter, SlideEffect, TimeSpan.FromSeconds(SlideInterval));
            }
            if (!animate || oldItem == null || ReferenceEquals(oldItem, _slidePresenter.Content)
                || SlideTransition == FlowerySlideTransition.None || DaisyAccessibility.IsReducedMotion
                || TopLevel.GetTopLevel(this) == null)
            {
                Complete();
                return;
            }
            _previousSlidePresenter.Content = oldItem;
            _previousSlidePresenter.IsVisible = true;
            FlowerySlideTransitionHelpers.ApplyTransition(_transitionHost, _previousSlidePresenter, _slidePresenter,
                SlideTransition, new FlowerySlideTransitionParams { Duration = TimeSpan.FromSeconds(Math.Max(0, TransitionDuration)) }, Complete);
        }
    }
}
