using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Flowery.Controls
{
    public class DaisyAvatarGroup : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAvatarGroup);

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

        protected override Size MeasureOverride(Size availableSize)
        {
            var children = Children;
            double maxWidth = 0;
            double maxHeight = 0;

            foreach (var child in children)
            {
                child.Measure(availableSize);
                maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            double totalWidth = children.Count > 0
                ? maxWidth + (children.Count - 1) * (maxWidth - Overlap)
                : 0;

            return new Size(totalWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = Children;
            double x = 0;
            double childWidth = 0;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                childWidth = child.DesiredSize.Width;
                double childHeight = child.DesiredSize.Height;

                child.Arrange(new Rect(x, 0, childWidth, childHeight));
                child.ZIndex = children.Count - i;
                x += childWidth - Overlap;
            }

            double totalWidth = children.Count > 0
                ? x + Overlap
                : 0;

            return new Size(totalWidth, finalSize.Height);
        }
    }
}
