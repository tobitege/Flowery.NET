using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Metadata;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Pagination item that represents a single page button in the pagination control.
    /// </summary>
    public class DaisyPaginationItem : Button
    {
        protected override Type StyleKeyOverride => typeof(DaisyPaginationItem);

        /// <summary>
        /// Defines the <see cref="IsActive"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<DaisyPaginationItem, bool>(nameof(IsActive));

        /// <summary>
        /// Gets or sets whether this pagination item is the currently active/selected page.
        /// </summary>
        public bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="PageNumber"/> property.
        /// </summary>
        public static readonly StyledProperty<int?> PageNumberProperty =
            AvaloniaProperty.Register<DaisyPaginationItem, int?>(nameof(PageNumber));

        /// <summary>
        /// Gets or sets the page number this item represents. Null for non-numeric items like prev/next.
        /// </summary>
        public int? PageNumber
        {
            get => GetValue(PageNumberProperty);
            set => SetValue(PageNumberProperty, value);
        }
    }

    /// <summary>
    /// A pagination control styled after DaisyUI's Pagination component.
    /// Uses the join pattern to group page buttons together.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyPagination : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyPagination);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyPagination, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of pagination buttons (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Orientation"/> property.
        /// </summary>
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyPagination, Orientation>(nameof(Orientation), Orientation.Horizontal);

        /// <summary>
        /// Gets or sets the orientation of the pagination buttons.
        /// </summary>
        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="CurrentPage"/> property.
        /// </summary>
        public static readonly StyledProperty<int> CurrentPageProperty =
            AvaloniaProperty.Register<DaisyPagination, int>(nameof(CurrentPage), 1, coerce: CoerceCurrentPage);

        private static int CoerceCurrentPage(AvaloniaObject obj, int value)
        {
            return value < 1 ? 1 : value;
        }

        /// <summary>
        /// Gets or sets the currently selected page number.
        /// </summary>
        public int CurrentPage
        {
            get => GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ButtonStyle"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonStyle> ButtonStyleProperty =
            AvaloniaProperty.Register<DaisyPagination, DaisyButtonStyle>(nameof(ButtonStyle), DaisyButtonStyle.Default);

        /// <summary>
        /// Gets or sets the button style for pagination items (Default, Outline, etc.).
        /// </summary>
        public DaisyButtonStyle ButtonStyle
        {
            get => GetValue(ButtonStyleProperty);
            set => SetValue(ButtonStyleProperty, value);
        }

        /// <summary>
        /// Event raised when the current page changes.
        /// </summary>
        public event EventHandler<int>? PageChanged;

        public DaisyPagination()
        {
            AddHandler(Button.ClickEvent, OnItemClicked);
        }

        private void OnItemClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (e.Source is DaisyPaginationItem item && item.PageNumber.HasValue && !item.IsActive)
            {
                CurrentPage = item.PageNumber.Value;
                PageChanged?.Invoke(this, CurrentPage);
                UpdateActiveStates();
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == CurrentPageProperty)
            {
                UpdateActiveStates();
            }
            else if (change.Property == ItemCountProperty)
            {
                UpdateActiveStates();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            UpdateActiveStates();
        }

        private void UpdateActiveStates()
        {
            if (ItemCount == 0) return;

            foreach (var item in Items.OfType<DaisyPaginationItem>())
            {
                item.IsActive = item.PageNumber.HasValue && item.PageNumber.Value == CurrentPage;
            }
        }

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return item is not DaisyPaginationItem;
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new DaisyPaginationItem();
        }

        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);

            if (container is DaisyPaginationItem paginationItem)
            {
                if (item is int pageNum)
                {
                    paginationItem.Content = pageNum.ToString();
                    paginationItem.PageNumber = pageNum;
                    paginationItem.IsActive = pageNum == CurrentPage;
                }
                else if (item is string str)
                {
                    paginationItem.Content = str;
                    if (int.TryParse(str, out var parsed))
                    {
                        paginationItem.PageNumber = parsed;
                        paginationItem.IsActive = parsed == CurrentPage;
                    }
                    else
                    {
                        paginationItem.PageNumber = null;
                        paginationItem.IsEnabled = str != "...";
                    }
                }
            }
        }
    }
}
