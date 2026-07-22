using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Flowery.Controls
{
    /// <summary>
    /// A Table control styled after DaisyUI's Table component.
    /// Can contain DaisyTableHead, DaisyTableBody, and DaisyTableFoot sections.
    /// </summary>
    public class DaisyTable : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTable);

        /// <summary>
        /// Gets or sets the size of the table (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyTable, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show zebra stripe rows.
        /// </summary>
        public static readonly StyledProperty<bool> ZebraProperty =
            AvaloniaProperty.Register<DaisyTable, bool>(nameof(Zebra), false);

        public bool Zebra
        {
            get => GetValue(ZebraProperty);
            set => SetValue(ZebraProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to pin (make sticky) header and footer rows.
        /// </summary>
        public static readonly StyledProperty<bool> PinRowsProperty =
            AvaloniaProperty.Register<DaisyTable, bool>(nameof(PinRows), false);

        public bool PinRows
        {
            get => GetValue(PinRowsProperty);
            set => SetValue(PinRowsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to pin (make sticky) th columns.
        /// </summary>
        public static readonly StyledProperty<bool> PinColsProperty =
            AvaloniaProperty.Register<DaisyTable, bool>(nameof(PinCols), false);

        public bool PinCols
        {
            get => GetValue(PinColsProperty);
            set => SetValue(PinColsProperty, value);
        }
    }

    /// <summary>
    /// Table header section (thead equivalent).
    /// </summary>
    public class DaisyTableHead : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTableHead);
    }

    /// <summary>
    /// Table body section (tbody equivalent).
    /// </summary>
    public class DaisyTableBody : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTableBody);

        static DaisyTableBody()
        {
            ItemCountProperty.Changed.AddClassHandler<DaisyTableBody>((x, _) => x.UpdateZebraStripes());
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateZebraStripes();
        }

        private void UpdateZebraStripes()
        {
            var parentTable = this.FindAncestorOfType<DaisyTable>();
            if (parentTable == null || !parentTable.Zebra)
                return;

            var zebraBrush = this.FindResource("DaisyBase200Brush") as IBrush;
            if (zebraBrush == null)
                return;

            int index = 0;
            foreach (var item in Items)
            {
                if (item is DaisyTableRow row)
                {
                    if (!row.IsActive)
                    {
                        row.Background = (index % 2 == 1) ? zebraBrush : Brushes.Transparent;
                    }
                }
                index++;
            }
        }
    }

    /// <summary>
    /// Table footer section (tfoot equivalent).
    /// </summary>
    public class DaisyTableFoot : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTableFoot);
    }

    /// <summary>
    /// A table row (tr equivalent).
    /// </summary>
    public class DaisyTableRow : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTableRow);

        /// <summary>
        /// Gets or sets whether this row is active/selected.
        /// </summary>
        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<DaisyTableRow, bool>(nameof(IsActive), false);

        public bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this row highlights on hover.
        /// </summary>
        public static readonly StyledProperty<bool> HighlightOnHoverProperty =
            AvaloniaProperty.Register<DaisyTableRow, bool>(nameof(HighlightOnHover), false);

        public bool HighlightOnHover
        {
            get => GetValue(HighlightOnHoverProperty);
            set => SetValue(HighlightOnHoverProperty, value);
        }
    }

    /// <summary>
    /// A table header cell (th equivalent).
    /// </summary>
    public class DaisyTableHeaderCell : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTableHeaderCell);

        /// <summary>
        /// Gets or sets the width of this column.
        /// </summary>
        public static readonly StyledProperty<GridLength> ColumnWidthProperty =
            AvaloniaProperty.Register<DaisyTableHeaderCell, GridLength>(nameof(ColumnWidth), new GridLength(1, GridUnitType.Star));

        public GridLength ColumnWidth
        {
            get => GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }
    }

    /// <summary>
    /// A table data cell (td equivalent).
    /// </summary>
    public class DaisyTableCell : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTableCell);
    }

    /// <summary>
    /// Custom panel for table rows that arranges cells in columns.
    /// </summary>
    public class DaisyTableRowPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var children = Children;
            double totalWidth = 0;
            double maxHeight = 0;
            int starCount = 0;
            double fixedWidth = 0;

            // First pass: measure fixed-width children and count star columns
            foreach (var child in children)
            {
                var columnWidth = GetColumnWidth(child);
                if (columnWidth.IsStar)
                {
                    starCount++;
                }
                else if (columnWidth.IsAbsolute)
                {
                    fixedWidth += columnWidth.Value;
                }
                else // Auto
                {
                    child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                    fixedWidth += child.DesiredSize.Width;
                }
            }

            // Calculate star width
            double remainingWidth = Math.Max(0, availableSize.Width - fixedWidth);
            double starWidth = starCount > 0 ? remainingWidth / starCount : 0;

            // Second pass: measure all children with correct widths
            foreach (var child in children)
            {
                var columnWidth = GetColumnWidth(child);
                double childWidth;

                if (columnWidth.IsStar)
                {
                    childWidth = starWidth * columnWidth.Value;
                }
                else if (columnWidth.IsAbsolute)
                {
                    childWidth = columnWidth.Value;
                }
                else
                {
                    childWidth = child.DesiredSize.Width;
                }

                child.Measure(new Size(childWidth, availableSize.Height));
                totalWidth += childWidth;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            return new Size(
                double.IsInfinity(availableSize.Width) ? totalWidth : availableSize.Width,
                maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = Children;
            int starCount = 0;
            double fixedWidth = 0;

            // First pass: calculate fixed and star widths
            foreach (var child in children)
            {
                var columnWidth = GetColumnWidth(child);
                if (columnWidth.IsStar)
                {
                    starCount++;
                }
                else if (columnWidth.IsAbsolute)
                {
                    fixedWidth += columnWidth.Value;
                }
                else
                {
                    fixedWidth += child.DesiredSize.Width;
                }
            }

            double remainingWidth = Math.Max(0, finalSize.Width - fixedWidth);
            double starWidth = starCount > 0 ? remainingWidth / starCount : 0;

            // Arrange children
            double x = 0;
            foreach (var child in children)
            {
                var columnWidth = GetColumnWidth(child);
                double childWidth;

                if (columnWidth.IsStar)
                {
                    childWidth = starWidth * columnWidth.Value;
                }
                else if (columnWidth.IsAbsolute)
                {
                    childWidth = columnWidth.Value;
                }
                else
                {
                    childWidth = child.DesiredSize.Width;
                }

                child.Arrange(new Rect(x, 0, childWidth, finalSize.Height));
                x += childWidth;
            }

            return finalSize;
        }

        private GridLength GetColumnWidth(Control child)
        {
            if (child is DaisyTableHeaderCell headerCell)
                return headerCell.ColumnWidth;
            return new GridLength(1, GridUnitType.Star);
        }
    }

    /// <summary>
    /// Converter for table cell padding based on table size.
    /// </summary>
    public class TableSizeToPaddingConverter : IValueConverter
    {
        public static readonly TableSizeToPaddingConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DaisySize size)
            {
                return size switch
                {
                    DaisySize.ExtraSmall => new Thickness(8, 4),
                    DaisySize.Small => new Thickness(12, 6),
                    DaisySize.Medium => new Thickness(16, 12),
                    DaisySize.Large => new Thickness(20, 16),
                    DaisySize.ExtraLarge => new Thickness(24, 20),
                    _ => new Thickness(16, 12)
                };
            }
            return new Thickness(16, 12);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter for table font size based on table size.
    /// </summary>
    public class TableSizeToFontSizeConverter : IValueConverter
    {
        public static readonly TableSizeToFontSizeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DaisySize size)
            {
                return size switch
                {
                    DaisySize.ExtraSmall => 11.0,
                    DaisySize.Small => 12.0,
                    DaisySize.Medium => 14.0,
                    DaisySize.Large => 16.0,
                    DaisySize.ExtraLarge => 18.0,
                    _ => 14.0
                };
            }
            return 14.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
