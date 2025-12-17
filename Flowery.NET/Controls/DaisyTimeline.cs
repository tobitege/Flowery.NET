using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum TimelineItemPosition
    {
        Start,
        End
    }

    /// <summary>
    /// A timeline control styled after DaisyUI's Timeline component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyTimeline : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTimeline);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyTimeline, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public static readonly StyledProperty<bool> IsCompactProperty =
            AvaloniaProperty.Register<DaisyTimeline, bool>(nameof(IsCompact));

        public static readonly StyledProperty<bool> SnapIconProperty =
            AvaloniaProperty.Register<DaisyTimeline, bool>(nameof(SnapIcon));

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public bool IsCompact
        {
            get => GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        public bool SnapIcon
        {
            get => GetValue(SnapIconProperty);
            set => SetValue(SnapIconProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemCountProperty ||
                change.Property == OrientationProperty ||
                change.Property == IsCompactProperty ||
                change.Property == SnapIconProperty)
            {
                UpdateItemStates();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            UpdateItemStates();
        }

        private void UpdateItemStates()
        {
            int count = ItemCount;
            for (int i = 0; i < count; i++)
            {
                var container = ContainerFromIndex(i);
                if (container is DaisyTimelineItem item)
                {
                    item.SetCurrentValue(DaisyTimelineItem.IsFirstProperty, i == 0);
                    item.SetCurrentValue(DaisyTimelineItem.IsLastProperty, i == count - 1);
                    item.SetCurrentValue(DaisyTimelineItem.IndexProperty, i);
                    item.SetCurrentValue(DaisyTimelineItem.OrientationProperty, Orientation);
                    item.SetCurrentValue(DaisyTimelineItem.IsCompactProperty, IsCompact);
                    item.SetCurrentValue(DaisyTimelineItem.SnapIconProperty, SnapIcon);
                }
            }
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new DaisyTimelineItem();
        }

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return item is not DaisyTimelineItem;
        }

        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);

            if (container is DaisyTimelineItem timelineItem)
            {
                int count = ItemCount;
                timelineItem.SetCurrentValue(DaisyTimelineItem.IsFirstProperty, index == 0);
                timelineItem.SetCurrentValue(DaisyTimelineItem.IsLastProperty, index == count - 1);
                timelineItem.SetCurrentValue(DaisyTimelineItem.IndexProperty, index);
                timelineItem.SetCurrentValue(DaisyTimelineItem.OrientationProperty, Orientation);
                timelineItem.SetCurrentValue(DaisyTimelineItem.IsCompactProperty, IsCompact);
                timelineItem.SetCurrentValue(DaisyTimelineItem.SnapIconProperty, SnapIcon);
            }
        }
    }

    public class DaisyTimelineItem : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTimelineItem);

        public static readonly StyledProperty<object?> StartContentProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, object?>(nameof(StartContent));

        public static readonly StyledProperty<object?> MiddleContentProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, object?>(nameof(MiddleContent));

        public static readonly StyledProperty<object?> EndContentProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, object?>(nameof(EndContent));

        public static readonly StyledProperty<bool> HasStartLineProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(HasStartLine));

        public static readonly StyledProperty<bool> HasEndLineProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(HasEndLine));

        public static readonly StyledProperty<bool> IsBoxedProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(IsBoxed));

        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(IsActive));

        public static readonly StyledProperty<bool> IsFirstProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(IsFirst));

        public static readonly StyledProperty<bool> IsLastProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(IsLast));

        public static readonly StyledProperty<int> IndexProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, int>(nameof(Index));

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public static readonly StyledProperty<bool> IsCompactProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(IsCompact));

        public static readonly StyledProperty<bool> SnapIconProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, bool>(nameof(SnapIcon));

        public static readonly StyledProperty<TimelineItemPosition> PositionProperty =
            AvaloniaProperty.Register<DaisyTimelineItem, TimelineItemPosition>(nameof(Position), TimelineItemPosition.End);

        public object? StartContent
        {
            get => GetValue(StartContentProperty);
            set => SetValue(StartContentProperty, value);
        }

        public object? MiddleContent
        {
            get => GetValue(MiddleContentProperty);
            set => SetValue(MiddleContentProperty, value);
        }

        public object? EndContent
        {
            get => GetValue(EndContentProperty);
            set => SetValue(EndContentProperty, value);
        }

        public bool HasStartLine
        {
            get => GetValue(HasStartLineProperty);
            set => SetValue(HasStartLineProperty, value);
        }

        public bool HasEndLine
        {
            get => GetValue(HasEndLineProperty);
            set => SetValue(HasEndLineProperty, value);
        }

        public bool IsBoxed
        {
            get => GetValue(IsBoxedProperty);
            set => SetValue(IsBoxedProperty, value);
        }

        public bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public bool IsFirst
        {
            get => GetValue(IsFirstProperty);
            set => SetValue(IsFirstProperty, value);
        }

        public bool IsLast
        {
            get => GetValue(IsLastProperty);
            set => SetValue(IsLastProperty, value);
        }

        public int Index
        {
            get => GetValue(IndexProperty);
            set => SetValue(IndexProperty, value);
        }

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public bool IsCompact
        {
            get => GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        public bool SnapIcon
        {
            get => GetValue(SnapIconProperty);
            set => SetValue(SnapIconProperty, value);
        }

        public TimelineItemPosition Position
        {
            get => GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }
    }
}
