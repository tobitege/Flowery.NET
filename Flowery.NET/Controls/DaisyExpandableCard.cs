using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A Card control that expands to reveal additional content.
    /// </summary>
    [TemplatePart("PART_SolidBackground", typeof(Border))]
    [TemplatePart("PART_SolidContent", typeof(ContentPresenter))]
    [TemplatePart("PART_SolidExpandedWrapper", typeof(Border))]
    [TemplatePart("PART_SolidExpandedContent", typeof(ContentPresenter))]
    [TemplatePart("PART_GlassContent", typeof(ContentPresenter))]
    [TemplatePart("PART_GlassExpandedWrapper", typeof(Border))]
    [TemplatePart("PART_GlassExpandedContent", typeof(ContentPresenter))]
    public class DaisyExpandableCard : DaisyCard
    {
        protected override Type StyleKeyOverride => typeof(DaisyExpandableCard);

        private Border? _solidBackground;
        private ContentPresenter? _solidContent;
        private Border? _solidExpandedWrapper;
        private ContentPresenter? _solidExpandedContent;
        private ContentPresenter? _glassContent;
        private Border? _glassExpandedWrapper;
        private ContentPresenter? _glassExpandedContent;

        private CancellationTokenSource? _animationCts;

        public DaisyExpandableCard()
        {
            ToggleCommand = new SimpleCommand(_ => IsExpanded = !IsExpanded);
        }

        /// <summary>
        /// Gets or sets whether the card is currently expanded.
        /// </summary>
        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, bool>(nameof(IsExpanded));

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        /// <summary>
        /// Gets or sets the content to display in the expanded area.
        /// </summary>
        public static readonly StyledProperty<object?> ExpandedContentProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, object?>(nameof(ExpandedContent));

        public object? ExpandedContent
        {
            get => GetValue(ExpandedContentProperty);
            set => SetValue(ExpandedContentProperty, value);
        }

        /// <summary>
        /// Gets or sets the data template for the expanded content.
        /// </summary>
        public static readonly StyledProperty<Avalonia.Controls.Templates.IDataTemplate?> ExpandedContentTemplateProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, Avalonia.Controls.Templates.IDataTemplate?>(nameof(ExpandedContentTemplate));

        public Avalonia.Controls.Templates.IDataTemplate? ExpandedContentTemplate
        {
            get => GetValue(ExpandedContentTemplateProperty);
            set => SetValue(ExpandedContentTemplateProperty, value);
        }

        /// <summary>
        /// Gets or sets the duration of the expand/collapse animation.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(300));

        public TimeSpan AnimationDuration
        {
            get => GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        /// <summary>
        /// When true, the control generates its visual content from convenience properties (Title, Subtitle, etc.).
        /// </summary>
        public static readonly StyledProperty<bool> UseBatteriesIncludedModeProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, bool>(nameof(UseBatteriesIncludedMode), false);

        public bool UseBatteriesIncludedMode
        {
            get => GetValue(UseBatteriesIncludedModeProperty);
            set => SetValue(UseBatteriesIncludedModeProperty, value);
        }

        /// <summary>
        /// The main title displayed on the card.
        /// </summary>
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, string?>(nameof(Title));

        public string? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// The subtitle displayed below the title.
        /// </summary>
        public static readonly StyledProperty<string?> SubtitleProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, string?>(nameof(Subtitle));

        public string? Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        /// <summary>
        /// The starting color of the card's background gradient.
        /// </summary>
        public static readonly StyledProperty<Color> GradientStartProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, Color>(nameof(GradientStart), Color.Parse("#0f172a"));

        public Color GradientStart
        {
            get => GetValue(GradientStartProperty);
            set => SetValue(GradientStartProperty, value);
        }

        /// <summary>
        /// The ending color of the card's background gradient.
        /// </summary>
        public static readonly StyledProperty<Color> GradientEndProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, Color>(nameof(GradientEnd), Color.Parse("#334155"));

        public Color GradientEnd
        {
            get => GetValue(GradientEndProperty);
            set => SetValue(GradientEndProperty, value);
        }

        /// <summary>
        /// The width of the main card content area.
        /// </summary>
        public static readonly StyledProperty<double> CardWidthProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, double>(nameof(CardWidth), 150.0);

        public double CardWidth
        {
            get => GetValue(CardWidthProperty);
            set => SetValue(CardWidthProperty, value);
        }

        /// <summary>
        /// The height of the card.
        /// </summary>
        public static readonly StyledProperty<double> CardHeightProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, double>(nameof(CardHeight), 225.0);

        public double CardHeight
        {
            get => GetValue(CardHeightProperty);
            set => SetValue(CardHeightProperty, value);
        }

        /// <summary>
        /// The main text displayed in the expanded panel.
        /// </summary>
        public static readonly StyledProperty<string?> ExpandedTextProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, string?>(nameof(ExpandedText));

        public string? ExpandedText
        {
            get => GetValue(ExpandedTextProperty);
            set => SetValue(ExpandedTextProperty, value);
        }

        /// <summary>
        /// The subtitle displayed in the expanded panel.
        /// </summary>
        public static readonly StyledProperty<string?> ExpandedSubtitleProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, string?>(nameof(ExpandedSubtitle));

        public string? ExpandedSubtitle
        {
            get => GetValue(ExpandedSubtitleProperty);
            set => SetValue(ExpandedSubtitleProperty, value);
        }

        /// <summary>
        /// The background color of the expanded panel.
        /// </summary>
        public static readonly StyledProperty<Color> ExpandedBackgroundProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, Color>(nameof(ExpandedBackground), Color.Parse("#111827"));

        public Color ExpandedBackground
        {
            get => GetValue(ExpandedBackgroundProperty);
            set => SetValue(ExpandedBackgroundProperty, value);
        }

        /// <summary>
        /// The text displayed on the action button.
        /// </summary>
        public static readonly StyledProperty<string> ActionButtonTextProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, string>(nameof(ActionButtonText), "Play");

        public string ActionButtonText
        {
            get => GetValue(ActionButtonTextProperty);
            set => SetValue(ActionButtonTextProperty, value);
        }

        /// <summary>
        /// The path data for an icon displayed in the top-right corner of the main card.
        /// </summary>
        public static readonly StyledProperty<StreamGeometry?> IconDataProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, StreamGeometry?>(nameof(IconData));

        public StreamGeometry? IconData
        {
            get => GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        /// <summary>
        /// The size (width and height) of the icon.
        /// </summary>
        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<DaisyExpandableCard, double>(nameof(IconSize), 96.0);

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        /// <summary>
        /// Command to toggle the expanded state.
        /// </summary>
        public ICommand ToggleCommand { get; }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _solidBackground = e.NameScope.Find<Border>("PART_SolidBackground");
            _solidContent = e.NameScope.Find<ContentPresenter>("PART_SolidContent");
            _solidExpandedWrapper = e.NameScope.Find<Border>("PART_SolidExpandedWrapper");
            _solidExpandedContent = e.NameScope.Find<ContentPresenter>("PART_SolidExpandedContent");
            _glassContent = e.NameScope.Find<ContentPresenter>("PART_GlassContent");
            _glassExpandedWrapper = e.NameScope.Find<Border>("PART_GlassExpandedWrapper");
            _glassExpandedContent = e.NameScope.Find<ContentPresenter>("PART_GlassExpandedContent");

            if (UseBatteriesIncludedMode)
            {
                BuildBatteriesIncludedContent();
            }

            // Initial state
            UpdateState(false);
        }

        private void BuildBatteriesIncludedContent()
        {
            // Build main card content
            var mainGrid = new Grid
            {
                Height = CardHeight,
                Width = CardWidth
            };

            // Background gradient border
            var gradientBorder = new Border
            {
                CornerRadius = CornerRadius,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop { Color = GradientStart, Offset = 0 },
                        new GradientStop { Color = GradientEnd, Offset = 1 }
                    }
                }
            };
            mainGrid.Children.Add(gradientBorder);

            // Add icon in top-right if IconData is provided
            if (IconData != null)
            {
                try
                {
                    var iconPath = new Avalonia.Controls.Shapes.Path
                    {
                        Data = IconData,
                        Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                        Stretch = Stretch.Uniform,
                        Width = IconSize,
                        Height = IconSize,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 8, 8, 0)
                    };
                    mainGrid.Children.Add(iconPath);
                }
                catch { /* ignore */ }
            }

            // Content stack
            var contentStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12)
            };

            if (!string.IsNullOrEmpty(Title))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = Title,
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                });
            }

            if (!string.IsNullOrEmpty(Subtitle))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = Subtitle,
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 0, 0, 10),
                    Foreground = Brushes.White
                });
            }

            // Action button
            var actionButton = new DaisyButton
            {
                Content = ActionButtonText,
                Variant = DaisyButtonVariant.Primary,
                Padding = new Thickness(8, 4, 8, 4),
                MinHeight = 32,
                Command = ToggleCommand
            };
            contentStack.Children.Add(actionButton);

            mainGrid.Children.Add(contentStack);

            // Set content directly on the presenter to avoid dual-parenting issue
            // (both PART_SolidContent and PART_GlassContent bind to Content property)
            var mainContentPresenter = IsGlass ? _glassContent : _solidContent;
            if (mainContentPresenter != null)
            {
                mainContentPresenter.Content = mainGrid;
            }

            // Build expanded content
            var expandedBorder = new Border
            {
                Width = CardWidth,
                Height = CardHeight,
                Background = new SolidColorBrush(ExpandedBackground),
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(0, 16, 16, 0)
            };

            var expandedStack = new StackPanel
            {
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(ExpandedText))
            {
                expandedStack.Children.Add(new TextBlock
                {
                    Text = ExpandedText,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 10
                });
            }

            if (!string.IsNullOrEmpty(ExpandedSubtitle))
            {
                expandedStack.Children.Add(new TextBlock
                {
                    Text = ExpandedSubtitle,
                    Foreground = Brushes.White,
                    Opacity = 0.7,
                    FontSize = 9
                });
            }

            expandedBorder.Child = expandedStack;

            // Set expanded content directly on the presenter
            var expandedContentPresenter = IsGlass ? _glassExpandedContent : _solidExpandedContent;
            if (expandedContentPresenter != null)
            {
                expandedContentPresenter.Content = expandedBorder;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsExpandedProperty)
            {
                UpdateState(true);
            }
        }

        private void UpdateContentCornerRadius(bool isExpanded)
        {
            if (_solidBackground == null) return;

            var baseRadius = CornerRadius;

            if (isExpanded)
            {
                // Left corners rounded, right corners sharp (to connect with expanded panel)
                _solidBackground.CornerRadius = new CornerRadius(baseRadius.TopLeft, 0, 0, baseRadius.BottomLeft);
            }
            else
            {
                // All corners rounded
                _solidBackground.CornerRadius = baseRadius;
            }
        }

        private void UpdateState(bool animate)
        {
            var isExpanded = IsExpanded;
            var wrapper = IsGlass ? _glassExpandedWrapper : _solidExpandedWrapper;
            var content = IsGlass ? _glassExpandedContent : _solidExpandedContent;

            if (wrapper == null || content == null) return;

            // Update corner radius
            UpdateContentCornerRadius(isExpanded);

            // Cancel any running animation
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            if (!animate)
            {
                // Instant update
                if (isExpanded)
                {
                    content.Measure(Avalonia.Size.Infinity);
                    var measuredWidth = content.DesiredSize.Width;
                    wrapper.Width = measuredWidth > 0 ? measuredWidth : 150; // Fallback
                    wrapper.Opacity = 1;
                }
                else
                {
                    wrapper.Width = 0;
                    wrapper.Opacity = 0;
                }
                return;
            }

            // Animate
            double startWidth = wrapper.Bounds.Width;
            double targetWidth = 0;
            double startOpacity = wrapper.Opacity;
            double targetOpacity = isExpanded ? 1 : 0;

            if (isExpanded)
            {
                // Measure desired width
                // Ensure content has constraint to measure properly
                content.Measure(Avalonia.Size.Infinity);
                targetWidth = content.DesiredSize.Width;

                // Fallback if measurement failed (e.g. not in visual tree properly yet)
                if (targetWidth <= 0 && ExpandedContent is Control c && c.Width > 0)
                    targetWidth = c.Width;
                if (targetWidth <= 0) targetWidth = 150;
            }

            // If start width is NaN (Auto), treat as 0
            if (double.IsNaN(startWidth)) startWidth = 0;

            // Run animation loop
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var duration = AnimationDuration;
                var easing = new CubicEaseOut();
                var startTime = DateTime.Now;

                while (DateTime.Now - startTime < duration)
                {
                    if (token.IsCancellationRequested) return;

                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    var t = Math.Min(1.0, elapsed / duration.TotalMilliseconds);
                    var easedT = easing.Ease(t);

                    var currentWidth = startWidth + (targetWidth - startWidth) * easedT;
                    var currentOpacity = startOpacity + (targetOpacity - startOpacity) * easedT;

                    wrapper.Width = currentWidth;
                    wrapper.Opacity = currentOpacity;

                    await Task.Delay(16); // ~60fps
                }

                // Final state
                if (!token.IsCancellationRequested)
                {
                    wrapper.Width = targetWidth;
                    wrapper.Opacity = targetOpacity;
                }
            });
        }
    }
}
