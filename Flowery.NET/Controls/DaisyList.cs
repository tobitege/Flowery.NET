using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A vertical list layout to display information in rows.
    /// Similar to daisyUI's list component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyList : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyList);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }
    }

    /// <summary>
    /// A row item inside DaisyList. Uses a horizontal grid layout.
    /// By default, the second child fills the remaining space.
    /// </summary>
    public class DaisyListRow : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyListRow);

        /// <summary>
        /// Gets or sets the index of the child that should grow to fill remaining space.
        /// Default is 1 (second child, zero-based index).
        /// Set to -1 to disable auto-grow, or use GrowIndex on a specific item.
        /// </summary>
        public static readonly StyledProperty<int> GrowColumnProperty =
            AvaloniaProperty.Register<DaisyListRow, int>(nameof(GrowColumn), 1);

        public int GrowColumn
        {
            get => GetValue(GrowColumnProperty);
            set => SetValue(GrowColumnProperty, value);
        }

        /// <summary>
        /// Gets or sets the spacing between items in the row.
        /// </summary>
        public static readonly StyledProperty<double> SpacingProperty =
            AvaloniaProperty.Register<DaisyListRow, double>(nameof(Spacing), 12);

        public double Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }
    }

    /// <summary>
    /// A column item within a DaisyListRow.
    /// </summary>
    public class DaisyListColumn : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyListColumn);

        /// <summary>
        /// When true, this column will grow to fill available space.
        /// Overrides the parent DaisyListRow.GrowColumn setting.
        /// </summary>
        public static readonly StyledProperty<bool> GrowProperty =
            AvaloniaProperty.Register<DaisyListColumn, bool>(nameof(Grow), false);

        public bool Grow
        {
            get => GetValue(GrowProperty);
            set => SetValue(GrowProperty, value);
        }

        /// <summary>
        /// When true, this column will wrap to a new line.
        /// </summary>
        public static readonly StyledProperty<bool> WrapProperty =
            AvaloniaProperty.Register<DaisyListColumn, bool>(nameof(Wrap), false);

        public bool Wrap
        {
            get => GetValue(WrapProperty);
            set => SetValue(WrapProperty, value);
        }
    }

    /// <summary>
    /// Custom panel for DaisyListRow that handles grow and wrap behavior.
    /// </summary>
    public class DaisyListRowPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var spacing = GetSpacing();
            var children = Children;
            var mainRowChildren = new List<Control>();
            var wrapChildren = new List<Control>();

            // Separate main row children from wrap children
            foreach (var child in children)
            {
                if (child is DaisyListColumn col && col.Wrap)
                    wrapChildren.Add(child);
                else
                    mainRowChildren.Add(child);
            }

            // Measure non-growing children first to calculate remaining space
            double fixedWidth = 0;
            int growChildIndex = GetGrowChildIndex(mainRowChildren);
            Control? growChild = null;

            for (int i = 0; i < mainRowChildren.Count; i++)
            {
                var child = mainRowChildren[i];
                bool isGrow = IsGrowChild(child, i, growChildIndex);

                if (!isGrow)
                {
                    child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                    fixedWidth += child.DesiredSize.Width;
                }
                else
                {
                    growChild = child;
                }
            }

            // Add spacing
            if (mainRowChildren.Count > 1)
                fixedWidth += spacing * (mainRowChildren.Count - 1);

            // Measure grow child with remaining width
            double remainingWidth = Math.Max(0, availableSize.Width - fixedWidth);
            if (growChild != null)
            {
                growChild.Measure(new Size(remainingWidth, availableSize.Height));
            }

            // Calculate main row size
            double totalWidth = 0;
            double maxHeight = 0;
            for (int i = 0; i < mainRowChildren.Count; i++)
            {
                var child = mainRowChildren[i];
                totalWidth += child.DesiredSize.Width;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }
            if (mainRowChildren.Count > 1)
                totalWidth += spacing * (mainRowChildren.Count - 1);

            // Measure and add wrap children
            double wrapHeight = 0;
            foreach (var wrapChild in wrapChildren)
            {
                wrapChild.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                wrapHeight += wrapChild.DesiredSize.Height;
            }

            if (wrapChildren.Count > 0)
                wrapHeight += spacing * wrapChildren.Count;

            return new Size(
                double.IsInfinity(availableSize.Width) ? totalWidth : availableSize.Width,
                maxHeight + wrapHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var spacing = GetSpacing();
            var children = Children;
            var mainRowChildren = new List<Control>();
            var wrapChildren = new List<Control>();

            // Separate main row children from wrap children
            foreach (var child in children)
            {
                if (child is DaisyListColumn col && col.Wrap)
                    wrapChildren.Add(child);
                else
                    mainRowChildren.Add(child);
            }

            // Calculate fixed width of non-growing children
            double fixedWidth = 0;
            int growChildIndex = GetGrowChildIndex(mainRowChildren);

            for (int i = 0; i < mainRowChildren.Count; i++)
            {
                var child = mainRowChildren[i];
                bool isGrow = IsGrowChild(child, i, growChildIndex);

                if (!isGrow)
                {
                    fixedWidth += child.DesiredSize.Width;
                }
            }

            // Add spacing
            if (mainRowChildren.Count > 1)
                fixedWidth += spacing * (mainRowChildren.Count - 1);

            double remainingWidth = Math.Max(0, finalSize.Width - fixedWidth);

            // Calculate main row height
            double mainRowHeight = 0;
            foreach (var child in mainRowChildren)
            {
                mainRowHeight = Math.Max(mainRowHeight, child.DesiredSize.Height);
            }

            // Arrange main row children
            double x = 0;
            for (int i = 0; i < mainRowChildren.Count; i++)
            {
                var child = mainRowChildren[i];
                bool isGrow = IsGrowChild(child, i, growChildIndex);
                double childWidth = isGrow ? remainingWidth : child.DesiredSize.Width;

                double y = (mainRowHeight - child.DesiredSize.Height) / 2; // Center vertically
                child.Arrange(new Rect(x, y, childWidth, child.DesiredSize.Height));

                x += childWidth + spacing;
            }

            // Arrange wrap children below main row
            double wrapY = mainRowHeight + spacing;
            foreach (var wrapChild in wrapChildren)
            {
                wrapChild.Arrange(new Rect(0, wrapY, finalSize.Width, wrapChild.DesiredSize.Height));
                wrapY += wrapChild.DesiredSize.Height + spacing;
            }

            return finalSize;
        }

        private double GetSpacing()
        {
            var parent = this.FindAncestorOfType<DaisyListRow>();
            return parent?.Spacing ?? 12;
        }

        private int GetGrowChildIndex(List<Control> mainRowChildren)
        {
            // First check if any child has explicit Grow=true
            for (int i = 0; i < mainRowChildren.Count; i++)
            {
                if (mainRowChildren[i] is DaisyListColumn col && col.Grow)
                    return i;
            }

            // Fall back to parent's GrowColumn
            var parent = this.FindAncestorOfType<DaisyListRow>();
            return parent?.GrowColumn ?? 1;
        }

        private bool IsGrowChild(Control child, int index, int growChildIndex)
        {
            if (child is DaisyListColumn col && col.Grow)
                return true;

            return index == growChildIndex;
        }
    }
}
