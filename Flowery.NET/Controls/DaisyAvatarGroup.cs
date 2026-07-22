using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Services;
using System;

namespace Flowery.Controls
{
    /// <summary>
    /// A group container for avatars with overlapping display.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyAvatarGroup : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAvatarGroup);

        private const double BaseTextFontSize = 14.0;
        private DaisyAvatar? _overflowAvatar;
        private DaisyAvatarGroupPanel? _panel;

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyAvatarGroup, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _overflowAvatar = e.NameScope.Find<DaisyAvatar>("PART_OverflowAvatar");

            // Try to find the panel after the ItemsPresenter has created it
            var itemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
            if (itemsPresenter != null)
            {
                // We need to wait for the panel to be created
                itemsPresenter.EffectiveViewportChanged += ItemsPresenter_EffectiveViewportChanged;
            }
        }

        private void ItemsPresenter_EffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            if (sender is ItemsPresenter presenter && presenter.Panel is DaisyAvatarGroupPanel panel)
            {
                if (_panel != panel)
                {
                    _panel = panel;
                    // Bind the overflow avatar's position to the panel's calculation
                    if (_overflowAvatar != null)
                    {
                        var binding = new Binding
                        {
                            Source = _panel,
                            Path = "OverflowOffset"
                        };
                        var transform = new TranslateTransform();
                        _overflowAvatar.RenderTransform = transform;
                        transform.Bind(TranslateTransform.XProperty, binding);
                    }
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemCountProperty || change.Property == MaxVisibleProperty)
            {
                UpdateOverflow();
            }
        }

        private void UpdateOverflow()
        {
            var count = ItemCount;
            var max = MaxVisible;

            if (max > 0 && count > max)
            {
                // If we have 10 items and MaxVisible is 3:
                // We show items 0, 1.
                // Item 2 is covered by the placeholder.
                // Placeholder says "+8" (items 2..9).
                OverflowCount = count - max + 1;
            }
            else
            {
                OverflowCount = 0;
            }
        }

        public static readonly StyledProperty<double> OverlapProperty =
            AvaloniaProperty.Register<DaisyAvatarGroup, double>(nameof(Overlap), 24.0);

        public double Overlap
        {
            get => GetValue(OverlapProperty);
            set => SetValue(OverlapProperty, value);
        }

        public static readonly StyledProperty<int> MaxVisibleProperty =
            AvaloniaProperty.Register<DaisyAvatarGroup, int>(nameof(MaxVisible), 0);

        public int MaxVisible
        {
            get => GetValue(MaxVisibleProperty);
            set => SetValue(MaxVisibleProperty, value);
        }

        public static readonly DirectProperty<DaisyAvatarGroup, int> OverflowCountProperty =
            AvaloniaProperty.RegisterDirect<DaisyAvatarGroup, int>(
                nameof(OverflowCount),
                o => o.OverflowCount);

        private int _overflowCount;
        public int OverflowCount
        {
            get => _overflowCount;
            private set => SetAndRaise(OverflowCountProperty, ref _overflowCount, value);
        }
    }

    public class DaisyAvatarGroupPanel : Panel
    {
        public static readonly StyledProperty<double> OverlapProperty =
            AvaloniaProperty.Register<DaisyAvatarGroupPanel, double>(nameof(Overlap), 24.0);

        public double Overlap
        {
            get => GetValue(OverlapProperty);
            set => SetValue(OverlapProperty, value);
        }

        public static readonly StyledProperty<int> MaxVisibleProperty =
            AvaloniaProperty.Register<DaisyAvatarGroupPanel, int>(nameof(MaxVisible), 0);

        public int MaxVisible
        {
            get => GetValue(MaxVisibleProperty);
            set => SetValue(MaxVisibleProperty, value);
        }

        public static readonly DirectProperty<DaisyAvatarGroupPanel, double> OverflowOffsetProperty =
            AvaloniaProperty.RegisterDirect<DaisyAvatarGroupPanel, double>(
                nameof(OverflowOffset),
                o => o.OverflowOffset);

        private double _overflowOffset;
        public double OverflowOffset
        {
            get => _overflowOffset;
            private set => SetAndRaise(OverflowOffsetProperty, ref _overflowOffset, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var children = Children;
            double maxWidth = 0;
            double maxHeight = 0;
            int maxVisible = MaxVisible;

            // Measure all children first to be safe, or just the ones we need?
            // Safer to measure all so they have a DesiredSize, even if hidden.
            foreach (var child in children)
            {
                child.Measure(availableSize);
            }

            // Determine how many we actually show
            // If MaxVisible is set and > 0, we show that many slots.
            // But visually, the last slot is the overflow indicator.
            // So we show MaxVisible items from the layout perspective.
            // The items beyond MaxVisible are hidden.
            int count = children.Count;
            int visibleCount = (maxVisible > 0 && maxVisible < count) ? maxVisible : count;

            // Calculate total width based on visible items
            double totalWidth = 0;
            if (visibleCount > 0)
            {
                // Find max width among visible items
                for (int i = 0; i < visibleCount; i++)
                {
                    var child = children[i];
                    maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                    maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
                }

                totalWidth = maxWidth + (visibleCount - 1) * (maxWidth - Overlap);
            }

            return new Size(totalWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = Children;
            double x = 0;
            double childWidth = 0;
            int maxVisible = MaxVisible;

            // Logic:
            // If MaxVisible > 0 and Count > MaxVisible:
            //   Indices 0 to MaxVisible-2: Normal
            //   Index MaxVisible-1: This is the "victim" slot. We arrange it so the Panel takes space.
            //                       Also we record this X position as OverflowOffset.
            //   Indices >= MaxVisible: Hidden (Arrange 0,0,0,0)

            int count = children.Count;
            bool isOverflowing = maxVisible > 0 && count > maxVisible;
            int limit = isOverflowing ? maxVisible : count;

            for (int i = 0; i < count; i++)
            {
                var child = children[i];
                childWidth = child.DesiredSize.Width; // Assuming mostly uniform width
                double childHeight = child.DesiredSize.Height;

                if (i < limit)
                {
                    child.Arrange(new Rect(x, 0, childWidth, childHeight));
                    child.ZIndex = count - i;

                    if (isOverflowing && i == limit - 1)
                    {
                        OverflowOffset = x;
                    }

                    x += childWidth - Overlap;
                }
                else
                {
                    // Hide extra items
                    child.Arrange(new Rect(0, 0, 0, 0));
                }
            }

            double totalWidth = limit > 0
                ? x + Overlap
                : 0;

            return new Size(totalWidth, finalSize.Height);
        }
    }
}
