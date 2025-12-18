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
using Avalonia.Threading;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A Card control that expands to reveal additional content.
    /// </summary>
    [TemplatePart("PART_SolidExpandedWrapper", typeof(Border))]
    [TemplatePart("PART_SolidExpandedContent", typeof(ContentPresenter))]
    [TemplatePart("PART_GlassExpandedWrapper", typeof(Border))]
    [TemplatePart("PART_GlassExpandedContent", typeof(ContentPresenter))]
    public class DaisyExpandableCard : DaisyCard
    {
        protected override Type StyleKeyOverride => typeof(DaisyExpandableCard);

        private Border? _solidExpandedWrapper;
        private ContentPresenter? _solidExpandedContent;
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
        /// Command to toggle the expanded state.
        /// </summary>
        public ICommand ToggleCommand { get; }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _solidExpandedWrapper = e.NameScope.Find<Border>("PART_SolidExpandedWrapper");
            _solidExpandedContent = e.NameScope.Find<ContentPresenter>("PART_SolidExpandedContent");
            _glassExpandedWrapper = e.NameScope.Find<Border>("PART_GlassExpandedWrapper");
            _glassExpandedContent = e.NameScope.Find<ContentPresenter>("PART_GlassExpandedContent");

            // Initial state
            UpdateState(false);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsExpandedProperty)
            {
                UpdateState(true);
            }
        }

        private void UpdateState(bool animate)
        {
            var isExpanded = IsExpanded;
            var wrapper = IsGlass ? _glassExpandedWrapper : _solidExpandedWrapper;
            var content = IsGlass ? _glassExpandedContent : _solidExpandedContent;

            if (wrapper == null || content == null) return;

            // Cancel any running animation
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            if (!animate)
            {
                // Instant update
                if (isExpanded)
                {
                    content.Measure(Size.Infinity);
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
                content.Measure(Size.Infinity);
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
                var duration = TimeSpan.FromMilliseconds(300);
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
