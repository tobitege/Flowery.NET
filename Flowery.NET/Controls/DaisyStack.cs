using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Controls
{
    /// <summary>
    /// DaisyStack displays children in a stacked arrangement with optional navigation.
    /// When ShowNavigation is false (default), all children are visible with layered offsets.
    /// When ShowNavigation is true, only one item is visible at a time with prev/next arrows.
    /// </summary>
    public class DaisyStack : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyStack);

        private Button? _previousButton;
        private Button? _nextButton;
        private TextBlock? _counterText;
        private TextBlock? _counterTopText;
        private TextBlock? _counterStartText;
        private TextBlock? _counterEndText;

        private bool _isAnimating;

        #region Styled Properties

        /// <summary>
        /// Gets or sets whether navigation arrows are shown.
        /// When true, only one item is visible at a time.
        /// </summary>
        public static readonly StyledProperty<bool> ShowNavigationProperty =
            AvaloniaProperty.Register<DaisyStack, bool>(nameof(ShowNavigation), false);

        public bool ShowNavigation
        {
            get => GetValue(ShowNavigationProperty);
            set => SetValue(ShowNavigationProperty, value);
        }

        /// <summary>
        /// Gets or sets the navigation arrow placement (Horizontal or Vertical).
        /// </summary>
        public static readonly StyledProperty<DaisyStackNavigation> NavigationPlacementProperty =
            AvaloniaProperty.Register<DaisyStack, DaisyStackNavigation>(nameof(NavigationPlacement),
                DaisyStackNavigation.Horizontal);

        public DaisyStackNavigation NavigationPlacement
        {
            get => GetValue(NavigationPlacementProperty);
            set => SetValue(NavigationPlacementProperty, value);
        }

        /// <summary>
        /// Gets or sets the transition duration for navigation animations.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> TransitionDurationProperty =
            AvaloniaProperty.Register<DaisyStack, TimeSpan>(nameof(TransitionDuration), TimeSpan.FromMilliseconds(300));

        public TimeSpan TransitionDuration
        {
            get => GetValue(TransitionDurationProperty);
            set => SetValue(TransitionDurationProperty, value);
        }

        /// <summary>
        /// Gets or sets the color for navigation buttons.
        /// </summary>
        public static readonly StyledProperty<DaisyColor> NavigationColorProperty =
            AvaloniaProperty.Register<DaisyStack, DaisyColor>(nameof(NavigationColor), DaisyColor.Default);

        public DaisyColor NavigationColor
        {
            get => GetValue(NavigationColorProperty);
            set => SetValue(NavigationColorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the counter label ("1 / 5") is shown.
        /// </summary>
        public static readonly StyledProperty<bool> ShowCounterProperty =
            AvaloniaProperty.Register<DaisyStack, bool>(nameof(ShowCounter), false);

        public bool ShowCounter
        {
            get => GetValue(ShowCounterProperty);
            set => SetValue(ShowCounterProperty, value);
        }

        /// <summary>
        /// Gets or sets the counter placement (Top, Bottom, Start, End).
        /// </summary>
        public static readonly StyledProperty<DaisyPlacement> CounterPlacementProperty =
            AvaloniaProperty.Register<DaisyStack, DaisyPlacement>(nameof(CounterPlacement), DaisyPlacement.Bottom);

        public DaisyPlacement CounterPlacement
        {
            get => GetValue(CounterPlacementProperty);
            set => SetValue(CounterPlacementProperty, value);
        }

        /// <summary>
        /// Gets or sets the currently selected item index (0-based).
        /// </summary>
        public static readonly StyledProperty<int> SelectedIndexProperty =
            AvaloniaProperty.Register<DaisyStack, int>(nameof(SelectedIndex), 0);

        public int SelectedIndex
        {
            get => GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, CoerceIndex(value));
        }

        /// <summary>
        /// Gets or sets the opacity of non-active items in static (non-navigation) mode.
        /// </summary>
        public static readonly StyledProperty<double> StackOpacityProperty =
            AvaloniaProperty.Register<DaisyStack, double>(nameof(StackOpacity), 0.6);

        public double StackOpacity
        {
            get => GetValue(StackOpacityProperty);
            set => SetValue(StackOpacityProperty, value);
        }

        #endregion

        // Prevent ItemsControl from wrapping items - use items directly
        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return false; // Items are their own containers
        }

        static DaisyStack()
        {
            ShowNavigationProperty.Changed.AddClassHandler<DaisyStack>((x, _) => x.UpdateItemVisibility());
            SelectedIndexProperty.Changed.AddClassHandler<DaisyStack>((x, e) => x.OnSelectedIndexChanged(e));
            ItemCountProperty.Changed.AddClassHandler<DaisyStack>((x, _) => x.OnItemCountChanged());
        }

        private int CoerceIndex(int value)
        {
            var count = ItemCount;
            if (count == 0) return 0;
            if (value < 0) return count - 1;
            if (value >= count) return 0;
            return value;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_previousButton != null)
                _previousButton.Click -= OnPreviousClick;
            if (_nextButton != null)
                _nextButton.Click -= OnNextClick;

            _previousButton = e.NameScope.Find<Button>("PART_PreviousButton");
            _nextButton = e.NameScope.Find<Button>("PART_NextButton");
            _counterText = e.NameScope.Find<TextBlock>("PART_Counter");
            _counterTopText = e.NameScope.Find<TextBlock>("PART_CounterTop");
            _counterStartText = e.NameScope.Find<TextBlock>("PART_CounterStart");
            _counterEndText = e.NameScope.Find<TextBlock>("PART_CounterEnd");


            if (_previousButton != null)
                _previousButton.Click += OnPreviousClick;
            if (_nextButton != null)
                _nextButton.Click += OnNextClick;

            Dispatcher.UIThread.Post(() =>
            {
                UpdateItemVisibility();
                UpdateCounter();
            }, DispatcherPriority.Loaded);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ShowCounterProperty || change.Property == CounterPlacementProperty)
            {
                UpdateCounter();
            }
            else if (change.Property == StackOpacityProperty)
            {
                UpdateItemVisibility();
            }
        }

        private void OnItemCountChanged()
        {
            SelectedIndex = CoerceIndex(SelectedIndex);
            UpdateItemVisibility();
            UpdateCounter();
        }

        private List<Control> GetStackItems()
        {
            var items = new List<Control>();

            // Find the OverlayPanel in the visual tree
            var panel = this.GetVisualDescendants().OfType<OverlayPanel>().FirstOrDefault();
            if (panel != null)
            {
                foreach (var child in panel.Children)
                {
                    if (child != null)
                        items.Add(child);
                }
            }

            return items;
        }

        private void UpdateItemVisibility()
        {
            var items = GetStackItems();
            if (items.Count == 0) return;

            if (ShowNavigation)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.IsVisible = i == SelectedIndex;
                    item.Opacity = 1;
                    item.RenderTransform = null;
                    item.ZIndex = i == SelectedIndex ? 10 : 0;
                }
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.IsVisible = true;

                    if (i == 0)
                    {
                        item.ZIndex = 10;
                        item.Opacity = 1;
                        item.RenderTransform = null;
                    }
                    else if (i == 1)
                    {
                        item.ZIndex = 5;
                        item.Opacity = Math.Min(1.0, StackOpacity + 0.2);
                        item.RenderTransform = new TransformGroup
                        {
                            Children = { new TranslateTransform(4, 4), new ScaleTransform(0.95, 0.95) }
                        };
                    }
                    else if (i == 2)
                    {
                        item.ZIndex = 1;
                        item.Opacity = StackOpacity;
                        item.RenderTransform = new TransformGroup
                        {
                            Children = { new TranslateTransform(8, 8), new ScaleTransform(0.9, 0.9) }
                        };
                    }
                    else
                    {
                        item.ZIndex = -i;
                        item.Opacity = Math.Max(0.3, StackOpacity - 0.1 * (i - 2));
                        var offset = 8 + (i - 2) * 4;
                        var scale = Math.Max(0.8, 0.9 - 0.05 * (i - 2));
                        item.RenderTransform = new TransformGroup
                        {
                            Children = { new TranslateTransform(offset, offset), new ScaleTransform(scale, scale) }
                        };
                    }
                }
            }
        }

        private void UpdateCounter()
        {
            var counterValue = $"{SelectedIndex + 1} / {ItemCount}";
            if (_counterText != null) _counterText.Text = counterValue;
            if (_counterTopText != null) _counterTopText.Text = counterValue;
            if (_counterStartText != null) _counterStartText.Text = counterValue;
            if (_counterEndText != null) _counterEndText.Text = counterValue;
        }

        private void OnSelectedIndexChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!ShowNavigation || _isAnimating) return;

            var oldIndex = (int)(e.OldValue ?? 0);
            var newIndex = (int)(e.NewValue ?? 0);

            if (oldIndex != newIndex)
            {
                _ = AnimateTransitionAsync(oldIndex, newIndex);
            }

            UpdateCounter();
        }

        private async Task AnimateTransitionAsync(int fromIndex, int toIndex)
        {
            var items = GetStackItems();
            if (items.Count == 0 || fromIndex < 0 || fromIndex >= items.Count ||
                toIndex < 0 || toIndex >= items.Count)
            {
                UpdateItemVisibility();
                return;
            }

            _isAnimating = true;

            var fromItem = items[fromIndex];
            var toItem = items[toIndex];

            var isForward = toIndex > fromIndex || (fromIndex == items.Count - 1 && toIndex == 0);
            if (fromIndex == 0 && toIndex == items.Count - 1) isForward = false;

            var isHorizontal = NavigationPlacement == DaisyStackNavigation.Horizontal;
            var direction = isForward ? 1 : -1;
            var distance = isHorizontal ? Bounds.Width : Bounds.Height;
            if (distance <= 0) distance = 200;

            toItem.IsVisible = true;
            toItem.Opacity = 0;

            var translateFrom = new TranslateTransform();
            var translateTo = new TranslateTransform();
            fromItem.RenderTransform = translateFrom;
            toItem.RenderTransform = translateTo;

            if (isHorizontal)
                translateTo.X = direction * distance;
            else
                translateTo.Y = direction * distance;

            var cts = new CancellationTokenSource();
            var tasks = new List<Task>();

            var outAnim = new Animation
            {
                Duration = TransitionDuration,
                FillMode = FillMode.Forward, // Hold end state to prevent flicker
                Easing = new CubicEaseInOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                            new Setter(isHorizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty, 0.0)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                            new Setter(isHorizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty,
                                (-direction * distance))
                        }
                    }
                }
            };

            var inAnim = new Animation
            {
                Duration = TransitionDuration,
                FillMode = FillMode.Forward, // Hold end state to prevent flicker
                Easing = new CubicEaseInOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                            new Setter(isHorizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty,
                                (direction * distance))
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                            new Setter(isHorizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty, 0.0)
                        }
                    }
                }
            };

            tasks.Add(outAnim.RunAsync(fromItem, cts.Token));
            tasks.Add(inAnim.RunAsync(toItem, cts.Token));

            await Task.WhenAll(tasks);

            // Manual safe cleanup slightly different for prev/next to prevent flicker
            fromItem.IsVisible = false; // Ensure it's hidden while still holding animation state

            // Clean up everything properly
            UpdateItemVisibility();

            _isAnimating = false;
        }

        private void OnPreviousClick(object? sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            Previous();
        }

        private void OnNextClick(object? sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;
            Next();
        }

        /// <summary>
        /// Navigates to the next item in the stack.
        /// </summary>
        public void Next()
        {
            if (ItemCount == 0) return;
            SelectedIndex = CoerceIndex(SelectedIndex + 1);
        }

        /// <summary>
        /// Navigates to the previous item in the stack.
        /// </summary>
        public void Previous()
        {
            if (ItemCount == 0) return;
            SelectedIndex = CoerceIndex(SelectedIndex - 1);
        }
    }
}
