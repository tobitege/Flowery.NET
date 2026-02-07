using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Flowery.Helpers;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Pagination item that represents a single page button in the pagination control.
    /// </summary>
    public class DaisyPaginationItem : RepeatButton
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

        /// <summary>
        /// Defines the <see cref="IsEllipsis"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsEllipsisProperty =
            AvaloniaProperty.Register<DaisyPaginationItem, bool>(nameof(IsEllipsis));

        /// <summary>
        /// Gets or sets whether this pagination item is an ellipsis placeholder.
        /// </summary>
        public bool IsEllipsis
        {
            get => GetValue(IsEllipsisProperty);
            set => SetValue(IsEllipsisProperty, value);
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
        private readonly ObservableCollection<DaisyPaginationItem> _generatedItems = new();
        private readonly DaisyControlLifecycle _lifecycle;
        private bool _isLoaded;
        private bool _suppressItemsSourceUpdate;
        private bool _useGeneratedItems;
        private DaisyPaginationItem? _firstButton;
        private DaisyPaginationItem? _jumpBackButton;
        private DaisyPaginationItem? _prevButton;
        private DaisyPaginationItem? _nextButton;
        private DaisyPaginationItem? _jumpForwardButton;
        private DaisyPaginationItem? _lastButton;

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
            if (obj is DaisyPagination pagination)
            {
                var clamped = Math.Max(1, value);
                return Math.Min(clamped, pagination.TotalPages);
            }

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
        /// Defines the <see cref="TotalPages"/> property.
        /// </summary>
        public static readonly StyledProperty<int> TotalPagesProperty =
            AvaloniaProperty.Register<DaisyPagination, int>(nameof(TotalPages), 1, coerce: CoerceTotalPages);

        private static int CoerceTotalPages(AvaloniaObject obj, int value)
        {
            return value < 1 ? 1 : value;
        }

        /// <summary>
        /// Gets or sets the total number of pages.
        /// </summary>
        public int TotalPages
        {
            get => GetValue(TotalPagesProperty);
            set => SetValue(TotalPagesProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MaxVisiblePages"/> property.
        /// </summary>
        public static readonly StyledProperty<int> MaxVisiblePagesProperty =
            AvaloniaProperty.Register<DaisyPagination, int>(nameof(MaxVisiblePages), 7, coerce: CoerceMaxVisiblePages);

        private static int CoerceMaxVisiblePages(AvaloniaObject obj, int value)
        {
            return Math.Max(5, value);
        }

        /// <summary>
        /// Gets or sets the maximum number of page buttons to display (excluding navigation buttons).
        /// When TotalPages exceeds this, ellipsis will be shown.
        /// </summary>
        public int MaxVisiblePages
        {
            get => GetValue(MaxVisiblePagesProperty);
            set => SetValue(MaxVisiblePagesProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="CenterVisiblePages"/> property.
        /// </summary>
        public static readonly StyledProperty<int> CenterVisiblePagesProperty =
            AvaloniaProperty.Register<DaisyPagination, int>(nameof(CenterVisiblePages), 5, coerce: CoerceCenterVisiblePages);

        private static int CoerceCenterVisiblePages(AvaloniaObject obj, int value)
        {
            var clamped = Math.Max(1, value);
            return clamped % 2 == 0 ? clamped + 1 : clamped;
        }

        /// <summary>
        /// Gets or sets the number of visible page buttons in the center block.
        /// When set, this overrides MaxVisiblePages (effective total = CenterVisiblePages + 2).
        /// If both CenterVisiblePages and MaxVisiblePages are set, MaxVisiblePages acts as a hard cap.
        /// </summary>
        public int CenterVisiblePages
        {
            get => GetValue(CenterVisiblePagesProperty);
            set => SetValue(CenterVisiblePagesProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowPrevNext"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowPrevNextProperty =
            AvaloniaProperty.Register<DaisyPagination, bool>(nameof(ShowPrevNext), true);

        /// <summary>
        /// Gets or sets whether to show Previous/Next buttons (single chevron).
        /// </summary>
        public bool ShowPrevNext
        {
            get => GetValue(ShowPrevNextProperty);
            set => SetValue(ShowPrevNextProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowFirstLast"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowFirstLastProperty =
            AvaloniaProperty.Register<DaisyPagination, bool>(nameof(ShowFirstLast), false);

        /// <summary>
        /// Gets or sets whether to show First/Last page buttons.
        /// </summary>
        public bool ShowFirstLast
        {
            get => GetValue(ShowFirstLastProperty);
            set => SetValue(ShowFirstLastProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowJumpButtons"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowJumpButtonsProperty =
            AvaloniaProperty.Register<DaisyPagination, bool>(nameof(ShowJumpButtons), false);

        /// <summary>
        /// Gets or sets whether to show double-chevron jump buttons (jump multiple pages).
        /// </summary>
        public bool ShowJumpButtons
        {
            get => GetValue(ShowJumpButtonsProperty);
            set => SetValue(ShowJumpButtonsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="JumpStep"/> property.
        /// </summary>
        public static readonly StyledProperty<int> JumpStepProperty =
            AvaloniaProperty.Register<DaisyPagination, int>(nameof(JumpStep), 10, coerce: CoerceJumpStep);

        private static int CoerceJumpStep(AvaloniaObject obj, int value)
        {
            return Math.Max(2, value);
        }

        /// <summary>
        /// Gets or sets how many pages to jump when using jump buttons.
        /// </summary>
        public int JumpStep
        {
            get => GetValue(JumpStepProperty);
            set => SetValue(JumpStepProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="EllipsisText"/> property.
        /// </summary>
        public static readonly StyledProperty<string> EllipsisTextProperty =
            AvaloniaProperty.Register<DaisyPagination, string>(nameof(EllipsisText), "...");

        /// <summary>
        /// Gets or sets the text displayed for ellipsis.
        /// </summary>
        public string EllipsisText
        {
            get => GetValue(EllipsisTextProperty);
            set => SetValue(EllipsisTextProperty, value);
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
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll,
                () => Size,
                s => Size = s,
                handleLifecycleEvents: false);

            AddHandler(Button.ClickEvent, OnItemClicked);
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _isLoaded = true;
            _lifecycle.HandleLoaded();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _isLoaded = false;
            _lifecycle.HandleUnloaded();
        }

        private void OnItemClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (e.Source is DaisyPaginationItem item && item.PageNumber.HasValue && !item.IsActive)
            {
                CurrentPage = item.PageNumber.Value;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemsSourceProperty || change.Property == ItemCountProperty)
            {
                EnsureItemsSource();
                if (change.Property == ItemCountProperty && !_useGeneratedItems)
                    UpdateActiveStates();
            }

            if (change.Property == CurrentPageProperty)
            {
                if (_useGeneratedItems)
                {
                    RebuildPages();
                }
                else
                {
                    UpdateActiveStates();
                }

                if (_isLoaded)
                    PageChanged?.Invoke(this, CurrentPage);
            }
            else if (change.Property == TotalPagesProperty)
            {
                if (CurrentPage > TotalPages)
                {
                    SetCurrentValue(CurrentPageProperty, TotalPages);
                }

                if (_useGeneratedItems)
                    RebuildPages();
            }
            else if (change.Property == MaxVisiblePagesProperty ||
                     change.Property == CenterVisiblePagesProperty ||
                     change.Property == ShowPrevNextProperty ||
                     change.Property == ShowFirstLastProperty ||
                     change.Property == ShowJumpButtonsProperty ||
                     change.Property == JumpStepProperty ||
                     change.Property == EllipsisTextProperty)
            {
                if (_useGeneratedItems)
                    RebuildPages();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ApplyAll();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (e.Handled)
                return;

            var delta = e.Delta;
            if (delta.X == 0 && delta.Y == 0)
                return;

            var primary = Math.Abs(delta.Y) >= Math.Abs(delta.X) ? delta.Y : delta.X;
            if (primary < 0)
            {
                MoveToPreviousPage();
            }
            else
            {
                MoveToNextPage();
            }

            e.Handled = true;
        }


        private void ApplyAll()
        {
            EnsureItemsSource();

            if (_useGeneratedItems)
                RebuildPages();
            else
                UpdateActiveStates();
        }

        private void EnsureItemsSource()
        {
            if (_suppressItemsSourceUpdate)
                return;

            if (ItemsSource != null)
            {
                _useGeneratedItems = ReferenceEquals(ItemsSource, _generatedItems);
                return;
            }

            if (ItemCount > 0)
            {
                _useGeneratedItems = false;
                return;
            }

            _suppressItemsSourceUpdate = true;
            ItemsSource = _generatedItems;
            _suppressItemsSourceUpdate = false;
            _useGeneratedItems = true;
        }

        private void UpdateActiveStates()
        {
            if (ItemCount == 0) return;

            foreach (var item in Items.OfType<DaisyPaginationItem>())
            {
                item.IsActive = item.PageNumber.HasValue && item.PageNumber.Value == CurrentPage;
            }
        }


        private void MoveToPreviousPage()
        {
            MovePageByDelta(-1);
        }

        private void MoveToNextPage()
        {
            MovePageByDelta(1);
        }

        private void MovePageByDelta(int delta)
        {
            if (delta == 0 || TotalPages <= 1)
                return;

            var target = ClampPage(CurrentPage + delta, 1, TotalPages);
            CurrentPage = target;
        }

        private static int ClampPage(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private void RebuildPages()
        {
            var prefixButtons = BuildPrefixButtons();
            var suffixButtons = BuildSuffixButtons();
            var pageButtons = BuildPageButtons();

            if (!CanUpdateInPlace(prefixButtons, suffixButtons))
            {
                _generatedItems.Clear();
                foreach (var button in prefixButtons)
                    _generatedItems.Add(button);
                foreach (var button in pageButtons)
                    _generatedItems.Add(button);
                foreach (var button in suffixButtons)
                    _generatedItems.Add(button);
            }
            else
            {
                ReplacePageButtons(prefixButtons.Count, suffixButtons.Count, pageButtons);
            }

            UpdateActiveStates();
        }

        private List<DaisyPaginationItem> BuildPrefixButtons()
        {
            var prefix = new List<DaisyPaginationItem>();

            if (ShowFirstLast)
            {
                prefix.Add(EnsureNavButton(ref _firstButton, "DaisyIconPageFirst", () => CurrentPage = 1));
            }

            if (ShowJumpButtons)
            {
                prefix.Add(EnsureNavButton(ref _jumpBackButton, "DaisyIconChevronDoubleLeft", () => CurrentPage = CalculatePreviousJumpTarget()));
            }

            if (ShowPrevNext)
            {
                prefix.Add(EnsureNavButton(ref _prevButton, "DaisyIconChevronLeft", () =>
                {
                    if (CurrentPage > 1)
                        CurrentPage--;
                }));
            }

            return prefix;
        }

        private List<DaisyPaginationItem> BuildSuffixButtons()
        {
            var suffix = new List<DaisyPaginationItem>();

            if (ShowPrevNext)
            {
                suffix.Add(EnsureNavButton(ref _nextButton, "DaisyIconChevronRight", () =>
                {
                    if (CurrentPage < TotalPages)
                        CurrentPage++;
                }));
            }

            if (ShowJumpButtons)
            {
                suffix.Add(EnsureNavButton(ref _jumpForwardButton, "DaisyIconChevronDoubleRight", () => CurrentPage = CalculateNextJumpTarget()));
            }

            if (ShowFirstLast)
            {
                suffix.Add(EnsureNavButton(ref _lastButton, "DaisyIconPageLast", () => CurrentPage = TotalPages));
            }

            return suffix;
        }

        private List<DaisyPaginationItem> BuildPageButtons()
        {
            var result = new List<DaisyPaginationItem>();
            var pagesToShow = CalculateVisiblePages();
            int? lastPage = null;

            foreach (var pageNum in pagesToShow)
            {
                if (lastPage.HasValue && pageNum - lastPage.Value > 1)
                {
                    result.Add(CreateEllipsisButton());
                }

                result.Add(CreatePageButton(pageNum));
                lastPage = pageNum;
            }

            return result;
        }

        private DaisyPaginationItem EnsureNavButton(ref DaisyPaginationItem? buttonField, string iconKey, Action onClick)
        {
            if (buttonField != null)
                return buttonField;

            var button = CreateIconButton(iconKey);
            button.Click += (_, _) => onClick();
            buttonField = button;
            return button;
        }

        private bool CanUpdateInPlace(IReadOnlyList<DaisyPaginationItem> prefixButtons, IReadOnlyList<DaisyPaginationItem> suffixButtons)
        {
            if (_generatedItems.Count < prefixButtons.Count + suffixButtons.Count)
                return false;

            for (int i = 0; i < prefixButtons.Count; i++)
            {
                if (!ReferenceEquals(_generatedItems[i], prefixButtons[i]))
                    return false;
            }

            for (int i = 0; i < suffixButtons.Count; i++)
            {
                var index = _generatedItems.Count - suffixButtons.Count + i;
                if (index < 0 || !ReferenceEquals(_generatedItems[index], suffixButtons[i]))
                    return false;
            }

            return true;
        }

        private void ReplacePageButtons(int prefixCount, int suffixCount, IReadOnlyList<DaisyPaginationItem> pageButtons)
        {
            var start = prefixCount;
            var end = _generatedItems.Count - suffixCount;
            if (end < start)
                end = start;

            for (var i = end - 1; i >= start; i--)
            {
                _generatedItems.RemoveAt(i);
            }

            for (var i = 0; i < pageButtons.Count; i++)
            {
                _generatedItems.Insert(start + i, pageButtons[i]);
            }
        }

        private List<int> CalculateVisiblePages()
        {
            var result = new List<int>();
            var maxVisiblePages = GetEffectiveMaxVisiblePages();

            if (TotalPages <= maxVisiblePages)
            {
                for (int i = 1; i <= TotalPages; i++)
                    result.Add(i);
                return result;
            }

            result.Add(1);

            int middleSlots = maxVisiblePages - 2;
            int halfMiddle = middleSlots / 2;

            int rangeStart = CurrentPage - halfMiddle;
            int rangeEnd = CurrentPage + halfMiddle;

            if (rangeStart <= 2)
            {
                rangeStart = 2;
                rangeEnd = rangeStart + middleSlots - 1;
            }

            if (rangeEnd >= TotalPages - 1)
            {
                rangeEnd = TotalPages - 1;
                rangeStart = rangeEnd - middleSlots + 1;
            }

            rangeStart = Math.Max(2, rangeStart);
            rangeEnd = Math.Min(TotalPages - 1, rangeEnd);

            for (int i = rangeStart; i <= rangeEnd; i++)
            {
                if (i > 1 && i < TotalPages)
                    result.Add(i);
            }

            if (TotalPages > 1)
                result.Add(TotalPages);

            result.Sort();
            return result;
        }

        private int GetEffectiveMaxVisiblePages()
        {
            var hasCenter = IsSet(CenterVisiblePagesProperty);
            var hasMax = IsSet(MaxVisiblePagesProperty);
            var normalizedMax = NormalizeMaxVisiblePages(MaxVisiblePages);

            if (hasCenter)
            {
                var center = CenterVisiblePages;
                if (hasMax)
                {
                    var maxCenter = Math.Max(1, normalizedMax - 2);
                    center = Math.Min(center, maxCenter);
                    if (center % 2 == 0)
                        center = Math.Max(1, center - 1);
                }

                return center + 2;
            }

            return normalizedMax;
        }

        private static int NormalizeMaxVisiblePages(int value)
        {
            var clamped = Math.Max(5, value);
            if (clamped % 2 == 0)
                clamped -= 1;
            return clamped;
        }

        private int CalculatePreviousJumpTarget()
        {
            int currentMultiple = (CurrentPage / JumpStep) * JumpStep;
            if (currentMultiple == CurrentPage)
                return Math.Max(1, currentMultiple - JumpStep);

            return Math.Max(1, currentMultiple);
        }

        private int CalculateNextJumpTarget()
        {
            int nextMultiple = ((CurrentPage / JumpStep) + 1) * JumpStep;
            return Math.Min(TotalPages, nextMultiple);
        }

        private DaisyPaginationItem CreatePageButton(int pageNumber)
        {
            return new DaisyPaginationItem
            {
                Content = pageNumber.ToString(),
                PageNumber = pageNumber,
                IsEllipsis = false,
                IsActive = pageNumber == CurrentPage,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
        }

        private DaisyPaginationItem CreateEllipsisButton()
        {
            return new DaisyPaginationItem
            {
                Content = EllipsisText,
                PageNumber = null,
                IsEllipsis = true,
                IsEnabled = false,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
        }

        private DaisyPaginationItem CreateIconButton(string iconKey)
        {
            var button = new DaisyPaginationItem
            {
                PageNumber = null,
                IsEllipsis = false,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            button.Content = CreateNavIcon(button, iconKey);
            return button;
        }

        private static Control CreateNavIcon(DaisyPaginationItem source, string iconKey)
        {
            var geometry = FloweryPathHelpers.GetIconGeometry(iconKey);
            var path = new Path
            {
                Data = geometry,
                Stretch = Stretch.Uniform
            };

            path.Bind(Shape.FillProperty, new Binding
            {
                Source = source,
                Path = nameof(TemplatedControl.Foreground)
            });

            return new Viewbox
            {
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = path
            };
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
                    paginationItem.IsEllipsis = false;
                    paginationItem.IsEnabled = true;
                }
                else if (item is string str)
                {
                    paginationItem.Content = str;
                    if (int.TryParse(str, out var parsed))
                    {
                        paginationItem.PageNumber = parsed;
                        paginationItem.IsActive = parsed == CurrentPage;
                        paginationItem.IsEllipsis = false;
                        paginationItem.IsEnabled = true;
                    }
                    else
                    {
                        paginationItem.PageNumber = null;
                        paginationItem.IsEllipsis = string.Equals(str, EllipsisText, StringComparison.Ordinal) || str == "...";
                        paginationItem.IsEnabled = !paginationItem.IsEllipsis;
                    }
                }
            }
        }
    }
}
