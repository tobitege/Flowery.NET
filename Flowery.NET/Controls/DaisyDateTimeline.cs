using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Defines the strategy for disabling dates in the timeline.
    /// </summary>
    public enum DateDisableStrategy
    {
        /// <summary>No dates are disabled.</summary>
        None,
        /// <summary>Disable dates before today.</summary>
        BeforeToday,
        /// <summary>Disable dates after today.</summary>
        AfterToday,
        /// <summary>Disable dates before a specific date (use DisableBeforeDate property).</summary>
        BeforeDate,
        /// <summary>Disable dates after a specific date (use DisableAfterDate property).</summary>
        AfterDate,
        /// <summary>Disable all dates.</summary>
        All
    }

    /// <summary>
    /// Defines which elements to show in each date item.
    /// </summary>
    [Flags]
    public enum DateElementDisplay
    {
        /// <summary>Show day name (e.g., Mon, Tue).</summary>
        DayName = 1,
        /// <summary>Show day number (e.g., 1, 2, 31).</summary>
        DayNumber = 2,
        /// <summary>Show month name (e.g., Jan, Dec).</summary>
        MonthName = 4,
        /// <summary>Default layout: Day name, day number, month name.</summary>
        Default = DayName | DayNumber | MonthName,
        /// <summary>Compact layout: Day name and number only.</summary>
        Compact = DayName | DayNumber,
        /// <summary>Number only.</summary>
        NumberOnly = DayNumber
    }

    /// <summary>
    /// Defines how the selected date is positioned after selection or initial load.
    /// </summary>
    public enum DateSelectionMode
    {
        /// <summary>No automatic scrolling.</summary>
        None,
        /// <summary>Always scroll to show selected date at the start.</summary>
        AlwaysFirst,
        /// <summary>Scroll to center the selected date.</summary>
        AutoCenter
    }

    /// <summary>
    /// Defines the visual style of the date timeline header.
    /// </summary>
    public enum DateTimelineHeaderType
    {
        /// <summary>No header.</summary>
        None,
        /// <summary>Simple header showing month and year.</summary>
        MonthYear,
        /// <summary>Header with navigation arrows to switch months.</summary>
        Switcher
    }

    /// <summary>
    /// Defines the layout orientation of date items.
    /// </summary>
    public enum DateItemLayout
    {
        /// <summary>Vertical layout: Month on top, day number in center, day name at bottom (default, portrait-style).</summary>
        Vertical,
        /// <summary>Horizontal layout: Day name and day number side-by-side (landscape-style, like "MON 19").</summary>
        Horizontal
    }

    /// <summary>
    /// Represents a marked date with an optional tooltip text.
    /// </summary>
    public class DateMarker
    {
        /// <summary>
        /// Gets or sets the date to mark.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Gets or sets the tooltip/description text for this marker (max ~100 chars recommended).
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new DateMarker for the specified date.
        /// </summary>
        public DateMarker(DateTime date, string text = "")
        {
            Date = date.Date; // Normalize to date only
            Text = text;
        }

        /// <summary>
        /// Creates a new DateMarker with default values.
        /// </summary>
        public DateMarker() { }
    }

    /// <summary>
    /// A horizontal scrollable date timeline picker inspired by easy_date_timeline.
    /// Displays dates in a horizontal strip with customizable appearance.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDateTimeline : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDateTimeline);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        private ScrollViewer? _scrollViewer;
        private ItemsControl? _itemsControl;
        
        // Drag-to-scroll state
        private bool _isDragging;
        private bool _hasDragged; // True if mouse actually moved during drag (vs just click)
        private Point _dragStartPoint;
        private Vector _dragStartOffset;
        private const double DragThreshold = 5.0; // Pixels of movement before considered a drag

        #region Styled Properties

        /// <summary>
        /// Defines the <see cref="FirstDate"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTime> FirstDateProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateTime>(nameof(FirstDate), DateTime.Today.AddMonths(-1));

        /// <summary>
        /// Defines the <see cref="LastDate"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTime> LastDateProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateTime>(nameof(LastDate), DateTime.Today.AddMonths(3));

        /// <summary>
        /// Defines the <see cref="SelectedDate"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTime?> SelectedDateProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateTime?>(nameof(SelectedDate), DateTime.Today);

        /// <summary>
        /// Defines the <see cref="ItemWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ItemWidthProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, double>(nameof(ItemWidth), 64);

        /// <summary>
        /// Defines the <see cref="ItemSpacing"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ItemSpacingProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, double>(nameof(ItemSpacing), 8);

        /// <summary>
        /// Defines the <see cref="DisplayElements"/> property.
        /// </summary>
        public static readonly StyledProperty<DateElementDisplay> DisplayElementsProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateElementDisplay>(nameof(DisplayElements), DateElementDisplay.Default);

        /// <summary>
        /// Defines the <see cref="DisableStrategy"/> property.
        /// </summary>
        public static readonly StyledProperty<DateDisableStrategy> DisableStrategyProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateDisableStrategy>(nameof(DisableStrategy), DateDisableStrategy.None);

        /// <summary>
        /// Defines the <see cref="DisableBeforeDate"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTime?> DisableBeforeDateProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateTime?>(nameof(DisableBeforeDate));

        /// <summary>
        /// Defines the <see cref="DisableAfterDate"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTime?> DisableAfterDateProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateTime?>(nameof(DisableAfterDate));

        /// <summary>
        /// Defines the <see cref="SelectionMode"/> property.
        /// </summary>
        public static readonly StyledProperty<DateSelectionMode> SelectionModeProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateSelectionMode>(nameof(SelectionMode), DateSelectionMode.AutoCenter);

        /// <summary>
        /// Defines the <see cref="HeaderType"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTimelineHeaderType> HeaderTypeProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateTimelineHeaderType>(nameof(HeaderType), DateTimelineHeaderType.MonthYear);

        /// <summary>
        /// Defines the <see cref="Locale"/> property.
        /// </summary>
        public static readonly StyledProperty<CultureInfo> LocaleProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, CultureInfo>(nameof(Locale), CultureInfo.CurrentCulture);

        /// <summary>
        /// Defines the <see cref="ShowTodayHighlight"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowTodayHighlightProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, bool>(nameof(ShowTodayHighlight), true);

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Defines the <see cref="ItemLayout"/> property.
        /// </summary>
        public static readonly StyledProperty<DateItemLayout> ItemLayoutProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, DateItemLayout>(nameof(ItemLayout), DateItemLayout.Vertical);

        /// <summary>
        /// Defines the <see cref="HeaderText"/> property (read-only, computed from SelectedDate).
        /// </summary>
        public static readonly DirectProperty<DaisyDateTimeline, string> HeaderTextProperty =
            AvaloniaProperty.RegisterDirect<DaisyDateTimeline, string>(
                nameof(HeaderText),
                o => o.HeaderText);

        /// <summary>
        /// Defines the <see cref="MarkedDates"/> property.
        /// </summary>
        public static readonly StyledProperty<IList<DateMarker>?> MarkedDatesProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, IList<DateMarker>?>(nameof(MarkedDates));

        /// <summary>
        /// Defines the <see cref="ScrollBarVisibility"/> property.
        /// </summary>
        public static readonly StyledProperty<ScrollBarVisibility> ScrollBarVisibilityProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, ScrollBarVisibility>(nameof(ScrollBarVisibility), ScrollBarVisibility.Hidden);

        /// <summary>
        /// Defines the <see cref="AutoWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoWidthProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, bool>(nameof(AutoWidth), false);

        /// <summary>
        /// Defines the <see cref="VisibleDaysCount"/> property.
        /// </summary>
        public static readonly StyledProperty<int> VisibleDaysCountProperty =
            AvaloniaProperty.Register<DaisyDateTimeline, int>(nameof(VisibleDaysCount), 7);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the first date to display in the timeline.
        /// </summary>
        public DateTime FirstDate
        {
            get => GetValue(FirstDateProperty);
            set => SetValue(FirstDateProperty, value);
        }

        /// <summary>
        /// Gets or sets the last date to display in the timeline.
        /// </summary>
        public DateTime LastDate
        {
            get => GetValue(LastDateProperty);
            set => SetValue(LastDateProperty, value);
        }

        /// <summary>
        /// Gets or sets the currently selected date.
        /// </summary>
        public DateTime? SelectedDate
        {
            get => GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        /// <summary>
        /// Gets or sets the width of each date item.
        /// </summary>
        public double ItemWidth
        {
            get => GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets the spacing between date items.
        /// </summary>
        public double ItemSpacing
        {
            get => GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        /// <summary>
        /// Gets or sets which date elements to display in each item.
        /// </summary>
        public DateElementDisplay DisplayElements
        {
            get => GetValue(DisplayElementsProperty);
            set => SetValue(DisplayElementsProperty, value);
        }

        /// <summary>
        /// Gets or sets the strategy for disabling dates.
        /// </summary>
        public DateDisableStrategy DisableStrategy
        {
            get => GetValue(DisableStrategyProperty);
            set => SetValue(DisableStrategyProperty, value);
        }

        /// <summary>
        /// Gets or sets the date before which all dates are disabled (when DisableStrategy is BeforeDate).
        /// </summary>
        public DateTime? DisableBeforeDate
        {
            get => GetValue(DisableBeforeDateProperty);
            set => SetValue(DisableBeforeDateProperty, value);
        }

        /// <summary>
        /// Gets or sets the date after which all dates are disabled (when DisableStrategy is AfterDate).
        /// </summary>
        public DateTime? DisableAfterDate
        {
            get => GetValue(DisableAfterDateProperty);
            set => SetValue(DisableAfterDateProperty, value);
        }

        /// <summary>
        /// Gets or sets how the selected date is positioned after selection.
        /// </summary>
        public DateSelectionMode SelectionMode
        {
            get => GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        /// <summary>
        /// Gets or sets the header display type.
        /// </summary>
        public DateTimelineHeaderType HeaderType
        {
            get => GetValue(HeaderTypeProperty);
            set => SetValue(HeaderTypeProperty, value);
        }

        /// <summary>
        /// Gets or sets the culture for formatting dates.
        /// </summary>
        public CultureInfo Locale
        {
            get => GetValue(LocaleProperty);
            set => SetValue(LocaleProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to visually highlight today's date.
        /// </summary>
        public bool ShowTodayHighlight
        {
            get => GetValue(ShowTodayHighlightProperty);
            set => SetValue(ShowTodayHighlightProperty, value);
        }

        /// <summary>
        /// Gets or sets the size of the date items.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the layout orientation of date items (Vertical or Horizontal).
        /// </summary>
        public DateItemLayout ItemLayout
        {
            get => GetValue(ItemLayoutProperty);
            set => SetValue(ItemLayoutProperty, value);
        }

        /// <summary>
        /// Gets or sets the collection of marked dates. Each marker has a date and optional tooltip text.
        /// </summary>
        public IList<DateMarker>? MarkedDates
        {
            get => GetValue(MarkedDatesProperty);
            set => SetValue(MarkedDatesProperty, value);
        }

        /// <summary>
        /// Gets or sets the scrollbar visibility mode. Default is Hidden (mouse wheel/drag still work).
        /// </summary>
        public ScrollBarVisibility ScrollBarVisibility
        {
            get => GetValue(ScrollBarVisibilityProperty);
            set => SetValue(ScrollBarVisibilityProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to automatically calculate width based on VisibleDaysCount.
        /// When true, the control's width is set to show exactly VisibleDaysCount items.
        /// </summary>
        public bool AutoWidth
        {
            get => GetValue(AutoWidthProperty);
            set => SetValue(AutoWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets the number of visible date items when AutoWidth is true. Default is 5.
        /// </summary>
        public int VisibleDaysCount
        {
            get => GetValue(VisibleDaysCountProperty);
            set => SetValue(VisibleDaysCountProperty, value);
        }

        private string _headerText = string.Empty;
        /// <summary>
        /// Gets the header text showing the current month and year.
        /// </summary>
        public string HeaderText
        {
            get => _headerText;
            private set => SetAndRaise(HeaderTextProperty, ref _headerText, value);
        }

        #endregion

        #region Date Formatting Helpers

        /// <summary>
        /// Gets the selected date as a long date string (e.g., "Wednesday, December 11, 2025").
        /// Returns empty string if no date is selected.
        /// </summary>
        public string SelectedDateLong => SelectedDate?.ToString("D", Locale) ?? string.Empty;

        /// <summary>
        /// Gets the selected date as a short date string (e.g., "12/11/2025").
        /// Returns empty string if no date is selected.
        /// </summary>
        public string SelectedDateShort => SelectedDate?.ToString("d", Locale) ?? string.Empty;

        /// <summary>
        /// Gets the selected date in ISO 8601 format (e.g., "2025-12-11").
        /// Returns empty string if no date is selected.
        /// </summary>
        public string SelectedDateIso => SelectedDate?.ToString("yyyy-MM-dd") ?? string.Empty;

        /// <summary>
        /// Gets the selected date as month and day (e.g., "December 11").
        /// Returns empty string if no date is selected.
        /// </summary>
        public string SelectedDateMonthDay => SelectedDate?.ToString("MMMM d", Locale) ?? string.Empty;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the selected date changes (from any source: click, keyboard, programmatic).
        /// </summary>
        public event EventHandler<DateTime>? DateChanged;

        /// <summary>
        /// Occurs when a date is clicked with the mouse. Provides the clicked date.
        /// </summary>
        public event EventHandler<DateTime>? DateClicked;

        /// <summary>
        /// Occurs when Enter or Space is pressed on the selected date (confirmation action).
        /// Provides the confirmed date.
        /// </summary>
        public event EventHandler<DateTime>? DateConfirmed;

        /// <summary>
        /// Occurs when Escape is pressed. By default, scrolls to today if in range.
        /// </summary>
        public event EventHandler? EscapePressed;

        #endregion

        public DaisyDateTimeline()
        {
            UpdateHeaderText();
            // Enable keyboard focus
            Focusable = true;
        }

        /// <summary>
        /// Overrides measure to apply auto-calculated width when AutoWidth is true.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (AutoWidth && VisibleDaysCount > 0)
            {
                // Calculate width to show exactly VisibleDaysCount items
                var calculatedWidth = (VisibleDaysCount * ItemWidth) + ((VisibleDaysCount - 1) * ItemSpacing);
                
                // Apply MinWidth/MaxWidth constraints if set
                if (MinWidth > 0)
                    calculatedWidth = Math.Max(MinWidth, calculatedWidth);
                if (MaxWidth > 0 && MaxWidth < double.MaxValue)
                    calculatedWidth = Math.Min(MaxWidth, calculatedWidth);

                Width = calculatedWidth;
            }
            return base.MeasureOverride(availableSize);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            _itemsControl = e.NameScope.Find<ItemsControl>("PART_ItemsControl");

            // Find navigation buttons if header is switcher type (using DaisyButton)
            var prevButton = e.NameScope.Find<DaisyButton>("PART_PreviousButton");
            var nextButton = e.NameScope.Find<DaisyButton>("PART_NextButton");

            if (prevButton != null)
            {
                prevButton.Click += OnPreviousMonthClick;
            }
            if (nextButton != null)
            {
                nextButton.Click += OnNextMonthClick;
            }

            GenerateDateItems();
            
            // Defer scroll to after layout is complete (viewport width is 0 during OnApplyTemplate)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrollToSelectedDate(), 
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FirstDateProperty ||
                change.Property == LastDateProperty ||
                change.Property == DisplayElementsProperty ||
                change.Property == DisableStrategyProperty ||
                change.Property == DisableBeforeDateProperty ||
                change.Property == DisableAfterDateProperty ||
                change.Property == ShowTodayHighlightProperty ||
                change.Property == LocaleProperty ||
                change.Property == ItemWidthProperty ||
                change.Property == ItemSpacingProperty ||
                change.Property == SizeProperty ||
                change.Property == ItemLayoutProperty ||
                change.Property == MarkedDatesProperty)
            {
                GenerateDateItems();
                // Defer scroll recalculation after layout updates
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrollToSelectedDate(), 
                    Avalonia.Threading.DispatcherPriority.Loaded);
            }

            if (change.Property == SelectedDateProperty)
            {
                UpdateHeaderText();
                UpdateSelectedStates();
                ScrollToSelectedDate();
                
                var newDate = (DateTime?)change.NewValue;
                if (newDate.HasValue)
                {
                    DateChanged?.Invoke(this, newDate.Value);
                }
            }
        }

        private void UpdateHeaderText()
        {
            var date = SelectedDate ?? DateTime.Today;
            HeaderText = date.ToString("MMMM yyyy", Locale);
        }

        private void GenerateDateItems()
        {
            if (_itemsControl == null) return;

            var items = new List<DaisyDateTimelineItem>();
            var current = FirstDate.Date;
            var end = LastDate.Date;
            var today = DateTime.Today;

            // Build a dictionary for quick marker lookup
            var markerLookup = new Dictionary<DateTime, string>();
            if (MarkedDates != null)
            {
                foreach (var marker in MarkedDates)
                {
                    markerLookup[marker.Date.Date] = marker.Text;
                }
            }

            while (current <= end)
            {
                // Check if this date has a marker
                markerLookup.TryGetValue(current, out var markerText);

                var item = new DaisyDateTimelineItem
                {
                    Date = current,
                    IsToday = current == today,
                    IsDisabled = IsDateDisabled(current),
                    IsSelected = SelectedDate.HasValue && current == SelectedDate.Value.Date,
                    DayName = current.ToString("ddd", Locale).ToUpperInvariant(),
                    DayNumber = current.Day.ToString(),
                    MonthName = current.ToString("MMM", Locale).ToUpperInvariant(),
                    ShowDayName = DisplayElements.HasFlag(DateElementDisplay.DayName),
                    ShowDayNumber = DisplayElements.HasFlag(DateElementDisplay.DayNumber),
                    ShowMonthName = DisplayElements.HasFlag(DateElementDisplay.MonthName),
                    ShowTodayHighlight = ShowTodayHighlight,
                    Layout = ItemLayout,
                    IsMarked = markerText != null,
                    MarkerText = markerText ?? string.Empty,
                    Width = ItemWidth,
                    Margin = new Thickness(0, 0, ItemSpacing, 0)
                };

                item.Tapped += OnDateItemTapped;
                items.Add(item);
                current = current.AddDays(1);
            }

            // Remove right margin from last item
            if (items.Count > 0)
            {
                items[items.Count - 1].Margin = new Thickness(0);
            }

            _itemsControl.ItemsSource = items;
        }

        private bool IsDateDisabled(DateTime date)
        {
            return DisableStrategy switch
            {
                DateDisableStrategy.None => false,
                DateDisableStrategy.BeforeToday => date < DateTime.Today,
                DateDisableStrategy.AfterToday => date > DateTime.Today,
                DateDisableStrategy.BeforeDate => DisableBeforeDate.HasValue && date < DisableBeforeDate.Value.Date,
                DateDisableStrategy.AfterDate => DisableAfterDate.HasValue && date > DisableAfterDate.Value.Date,
                DateDisableStrategy.All => true,
                _ => false
            };
        }

        private void OnDateItemTapped(object? sender, TappedEventArgs e)
        {
            if (sender is DaisyDateTimelineItem item && !item.IsDisabled)
            {
                SelectedDate = item.Date;
                DateClicked?.Invoke(this, item.Date);
            }
        }

        private void UpdateSelectedStates()
        {
            if (_itemsControl?.ItemsSource is not IEnumerable<DaisyDateTimelineItem> items) return;

            foreach (var item in items)
            {
                item.IsSelected = SelectedDate.HasValue && item.Date == SelectedDate.Value.Date;
            }
        }

        private void ScrollToSelectedDate()
        {
            if (_scrollViewer == null || 
                _itemsControl?.ItemsSource is not IEnumerable<DaisyDateTimelineItem> items ||
                !SelectedDate.HasValue ||
                SelectionMode == DateSelectionMode.None)
            {
                return;
            }

            var selectedDate = SelectedDate.Value.Date;
            var dayOffset = (selectedDate - FirstDate.Date).Days;
            
            if (dayOffset < 0) return;

            var itemTotalWidth = ItemWidth + ItemSpacing;
            var targetOffset = dayOffset * itemTotalWidth;

            switch (SelectionMode)
            {
                case DateSelectionMode.AlwaysFirst:
                    _scrollViewer.Offset = new Vector(targetOffset, 0);
                    break;

                case DateSelectionMode.AutoCenter:
                    var viewportCenter = _scrollViewer.Viewport.Width / 2;
                    var centerOffset = targetOffset - viewportCenter + ItemWidth / 2;
                    _scrollViewer.Offset = new Vector(Math.Max(0, centerOffset), 0);
                    break;
            }
        }

        private void OnPreviousMonthClick(object? sender, RoutedEventArgs e)
        {
            var current = SelectedDate ?? DateTime.Today;
            var newDate = current.AddMonths(-1);
            
            // Clamp to valid range
            if (newDate >= FirstDate)
            {
                SelectedDate = newDate;
            }
            else
            {
                SelectedDate = FirstDate;
            }
        }

        private void OnNextMonthClick(object? sender, RoutedEventArgs e)
        {
            var current = SelectedDate ?? DateTime.Today;
            var newDate = current.AddMonths(1);
            
            // Clamp to valid range
            if (newDate <= LastDate)
            {
                SelectedDate = newDate;
            }
            else
            {
                SelectedDate = LastDate;
            }
        }

        /// <summary>
        /// Scrolls to and selects the specified date if it's within the valid range.
        /// </summary>
        /// <param name="date">The date to navigate to.</param>
        public void GoToDate(DateTime date)
        {
            if (date >= FirstDate && date <= LastDate)
            {
                SelectedDate = date;
            }
        }

        /// <summary>
        /// Scrolls to and selects today's date if it's within the valid range.
        /// </summary>
        public void GoToToday()
        {
            GoToDate(DateTime.Today);
        }

        /// <summary>
        /// Handles keyboard navigation for the timeline.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled) return;

            switch (e.Key)
            {
                case Key.Left:
                    // Move to previous day
                    NavigateByDays(-1);
                    e.Handled = true;
                    break;

                case Key.Right:
                    // Move to next day
                    NavigateByDays(1);
                    e.Handled = true;
                    break;

                case Key.Up:
                    // Move to previous week
                    NavigateByDays(-7);
                    e.Handled = true;
                    break;

                case Key.Down:
                    // Move to next week
                    NavigateByDays(7);
                    e.Handled = true;
                    break;

                case Key.Home:
                    // Go to first date in range
                    SelectFirstAvailableDate();
                    e.Handled = true;
                    break;

                case Key.End:
                    // Go to last date in range
                    SelectLastAvailableDate();
                    e.Handled = true;
                    break;

                case Key.PageUp:
                    // Move to previous month
                    OnPreviousMonthClick(null, null!);
                    e.Handled = true;
                    break;

                case Key.PageDown:
                    // Move to next month
                    OnNextMonthClick(null, null!);
                    e.Handled = true;
                    break;

                case Key.Enter:
                case Key.Space:
                    // Confirm selection - fires DateConfirmed event
                    if (SelectedDate.HasValue)
                    {
                        DateConfirmed?.Invoke(this, SelectedDate.Value);
                    }
                    e.Handled = true;
                    break;

                case Key.Escape:
                    // Scroll to today and fire EscapePressed event
                    var today = DateTime.Today;
                    if (today >= FirstDate && today <= LastDate)
                    {
                        GoToToday();
                    }
                    EscapePressed?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
            }
        }

        private void NavigateByDays(int days)
        {
            var current = SelectedDate ?? DateTime.Today;
            var newDate = current.AddDays(days);

            // Skip disabled dates in the direction of navigation
            while (newDate >= FirstDate && newDate <= LastDate && IsDateDisabled(newDate))
            {
                newDate = newDate.AddDays(days > 0 ? 1 : -1);
            }

            // Clamp to valid range
            if (newDate >= FirstDate && newDate <= LastDate && !IsDateDisabled(newDate))
            {
                SelectedDate = newDate;
            }
        }

        private void SelectFirstAvailableDate()
        {
            var date = FirstDate;
            while (date <= LastDate && IsDateDisabled(date))
            {
                date = date.AddDays(1);
            }
            if (date <= LastDate)
            {
                SelectedDate = date;
            }
        }

        private void SelectLastAvailableDate()
        {
            var date = LastDate;
            while (date >= FirstDate && IsDateDisabled(date))
            {
                date = date.AddDays(-1);
            }
            if (date >= FirstDate)
            {
                SelectedDate = date;
            }
        }

        #region Mouse Scrolling

        /// <summary>
        /// Handles mouse wheel to scroll horizontally.
        /// </summary>
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (_scrollViewer != null && !e.Handled)
            {
                // Scroll horizontally based on wheel delta
                // Positive delta (scroll up) = scroll left (earlier dates)
                // Negative delta (scroll down) = scroll right (later dates)
                var delta = e.Delta.Y * 50; // Adjust scroll speed
                var newOffset = _scrollViewer.Offset.X + delta;
                newOffset = Math.Max(0, Math.Min(newOffset, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width));
                _scrollViewer.Offset = new Vector(newOffset, 0);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Starts drag-to-scroll on pointer press.
        /// </summary>
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_scrollViewer != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _hasDragged = false; // Reset - will be set to true if mouse moves
                _dragStartPoint = e.GetPosition(this);
                _dragStartOffset = _scrollViewer.Offset;
                // Don't capture or handle yet - let clicks pass through to items
            }
        }

        /// <summary>
        /// Scrolls during drag.
        /// </summary>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDragging && _scrollViewer != null)
            {
                var currentPoint = e.GetPosition(this);
                var deltaX = Math.Abs(currentPoint.X - _dragStartPoint.X);
                
                // Only start actual dragging after threshold is exceeded
                if (!_hasDragged && deltaX > DragThreshold)
                {
                    _hasDragged = true;
                    e.Pointer.Capture(this);
                    Cursor = new Cursor(StandardCursorType.SizeWestEast);
                }
                
                if (_hasDragged)
                {
                    var delta = _dragStartPoint.X - currentPoint.X;
                    var newOffset = _dragStartOffset.X + delta;
                    newOffset = Math.Max(0, Math.Min(newOffset, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width));
                    _scrollViewer.Offset = new Vector(newOffset, 0);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Ends drag-to-scroll on pointer release.
        /// </summary>
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isDragging)
            {
                var wasDragging = _hasDragged;
                _isDragging = false;
                _hasDragged = false;
                
                if (wasDragging)
                {
                    e.Pointer.Capture(null);
                    Cursor = Cursor.Default;
                    e.Handled = true;
                }
                // If not actually dragged, let the click event pass through to items
            }
        }

        /// <summary>
        /// Cancels drag if pointer leaves the control.
        /// </summary>
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            
            if (_isDragging)
            {
                _isDragging = false;
                _hasDragged = false;
                Cursor = Cursor.Default;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a single date item in the DaisyDateTimeline.
    /// </summary>
    public class DaisyDateTimelineItem : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDateTimelineItem);

        #region Styled Properties

        /// <summary>
        /// Defines the <see cref="Date"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTime> DateProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, DateTime>(nameof(Date));

        /// <summary>
        /// Defines the <see cref="IsSelected"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(IsSelected));

        /// <summary>
        /// Defines the <see cref="IsToday"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsTodayProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(IsToday));

        /// <summary>
        /// Defines the <see cref="IsDisabled"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsDisabledProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(IsDisabled));

        /// <summary>
        /// Defines the <see cref="DayName"/> property.
        /// </summary>
        public static readonly StyledProperty<string> DayNameProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, string>(nameof(DayName), string.Empty);

        /// <summary>
        /// Defines the <see cref="DayNumber"/> property.
        /// </summary>
        public static readonly StyledProperty<string> DayNumberProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, string>(nameof(DayNumber), string.Empty);

        /// <summary>
        /// Defines the <see cref="MonthName"/> property.
        /// </summary>
        public static readonly StyledProperty<string> MonthNameProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, string>(nameof(MonthName), string.Empty);

        /// <summary>
        /// Defines the <see cref="ShowDayName"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowDayNameProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(ShowDayName), true);

        /// <summary>
        /// Defines the <see cref="ShowDayNumber"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowDayNumberProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(ShowDayNumber), true);

        /// <summary>
        /// Defines the <see cref="ShowMonthName"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowMonthNameProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(ShowMonthName), true);

        /// <summary>
        /// Defines the <see cref="ShowTodayHighlight"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowTodayHighlightProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(ShowTodayHighlight), true);

        /// <summary>
        /// Defines the <see cref="Layout"/> property.
        /// </summary>
        public static readonly StyledProperty<DateItemLayout> LayoutProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, DateItemLayout>(nameof(Layout), DateItemLayout.Vertical);

        /// <summary>
        /// Defines the <see cref="IsMarked"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsMarkedProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, bool>(nameof(IsMarked), false);

        /// <summary>
        /// Defines the <see cref="MarkerText"/> property.
        /// </summary>
        public static readonly StyledProperty<string> MarkerTextProperty =
            AvaloniaProperty.Register<DaisyDateTimelineItem, string>(nameof(MarkerText), string.Empty);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the date this item represents.
        /// </summary>
        public DateTime Date
        {
            get => GetValue(DateProperty);
            set => SetValue(DateProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this date is selected.
        /// </summary>
        public bool IsSelected
        {
            get => GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this is today's date.
        /// </summary>
        public bool IsToday
        {
            get => GetValue(IsTodayProperty);
            set => SetValue(IsTodayProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this date is disabled.
        /// </summary>
        public bool IsDisabled
        {
            get => GetValue(IsDisabledProperty);
            set => SetValue(IsDisabledProperty, value);
        }

        /// <summary>
        /// Gets or sets the day name (e.g., "Mon").
        /// </summary>
        public string DayName
        {
            get => GetValue(DayNameProperty);
            set => SetValue(DayNameProperty, value);
        }

        /// <summary>
        /// Gets or sets the day number (e.g., "15").
        /// </summary>
        public string DayNumber
        {
            get => GetValue(DayNumberProperty);
            set => SetValue(DayNumberProperty, value);
        }

        /// <summary>
        /// Gets or sets the month name (e.g., "Jan").
        /// </summary>
        public string MonthName
        {
            get => GetValue(MonthNameProperty);
            set => SetValue(MonthNameProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the day name.
        /// </summary>
        public bool ShowDayName
        {
            get => GetValue(ShowDayNameProperty);
            set => SetValue(ShowDayNameProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the day number.
        /// </summary>
        public bool ShowDayNumber
        {
            get => GetValue(ShowDayNumberProperty);
            set => SetValue(ShowDayNumberProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the month name.
        /// </summary>
        public bool ShowMonthName
        {
            get => GetValue(ShowMonthNameProperty);
            set => SetValue(ShowMonthNameProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show today highlight even when not selected.
        /// </summary>
        public bool ShowTodayHighlight
        {
            get => GetValue(ShowTodayHighlightProperty);
            set => SetValue(ShowTodayHighlightProperty, value);
        }

        /// <summary>
        /// Gets or sets the layout orientation (Vertical or Horizontal).
        /// </summary>
        public DateItemLayout Layout
        {
            get => GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        /// <summary>
        /// Gets or sets whether this date has a marker indicator.
        /// </summary>
        public bool IsMarked
        {
            get => GetValue(IsMarkedProperty);
            set => SetValue(IsMarkedProperty, value);
        }

        /// <summary>
        /// Gets or sets the marker tooltip text.
        /// </summary>
        public string MarkerText
        {
            get => GetValue(MarkerTextProperty);
            set => SetValue(MarkerTextProperty, value);
        }

        #endregion

        public DaisyDateTimelineItem()
        {
            // Enable pointer events
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsDisabledProperty)
            {
                Cursor = IsDisabled ? Cursor.Default : new Cursor(StandardCursorType.Hand);
                Opacity = IsDisabled ? 0.4 : 1.0;
            }
        }
    }
}
