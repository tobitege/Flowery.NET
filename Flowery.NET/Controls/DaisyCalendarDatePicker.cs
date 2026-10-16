using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Flowery.Controls
{
    /// <summary>
    /// Event data for <see cref="DaisyCalendarDatePicker.DateChanged"/>.
    /// </summary>
    public class DaisyCalendarDatePickerDateChangedEventArgs : EventArgs
    {
        public DaisyCalendarDatePickerDateChangedEventArgs(DateTimeOffset? oldDate, DateTimeOffset? newDate)
        {
            OldDate = oldDate;
            NewDate = newDate;
        }

        /// <summary>
        /// Gets the previously selected date.
        /// </summary>
        public DateTimeOffset? OldDate { get; }

        /// <summary>
        /// Gets the newly selected date.
        /// </summary>
        public DateTimeOffset? NewDate { get; }
    }

    /// <summary>
    /// A CalendarDatePicker styled to match Daisy input surfaces, with the native calendar popup.
    /// </summary>
    /// <remarks>
    /// <see cref="Date"/> mirrors the inherited <see cref="CalendarDatePicker.SelectedDate"/> as a
    /// <see cref="DateTimeOffset"/>, matching the Flowery.WinUI and Flowery.Uno API.
    /// </remarks>
    public class DaisyCalendarDatePicker : CalendarDatePicker
    {
        protected override Type StyleKeyOverride => typeof(DaisyCalendarDatePicker);

        private bool _isSyncingDate;
        private Calendar? _calendar;

        /// <summary>
        /// Defines the <see cref="Date"/> property.
        /// </summary>
        public static readonly StyledProperty<DateTimeOffset?> DateProperty =
            AvaloniaProperty.Register<DaisyCalendarDatePicker, DateTimeOffset?>(nameof(Date));

        /// <summary>
        /// Gets or sets the selected date.
        /// </summary>
        public DateTimeOffset? Date
        {
            get => GetValue(DateProperty);
            set => SetValue(DateProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyInputVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyCalendarDatePicker, DaisyInputVariant>(nameof(Variant), DaisyInputVariant.Bordered);

        /// <summary>
        /// Gets or sets the visual variant of the input surface.
        /// </summary>
        public DaisyInputVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyCalendarDatePicker, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the date picker.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="BorderRingBrush"/> property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> BorderRingBrushProperty =
            AvaloniaProperty.Register<DaisyCalendarDatePicker, IBrush?>(nameof(BorderRingBrush));

        /// <summary>
        /// Gets or sets the border brush while focused. Overrides the variant's focus color when set.
        /// </summary>
        public IBrush? BorderRingBrush
        {
            get => GetValue(BorderRingBrushProperty);
            set => SetValue(BorderRingBrushProperty, value);
        }

        /// <summary>
        /// Occurs when the selected date changes.
        /// </summary>
        public event EventHandler<DaisyCalendarDatePickerDateChangedEventArgs>? DateChanged;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // The popup calendar takes the Daisy calendar styles and follows Size through these classes.
            _calendar = e.NameScope.Find<Calendar>("PART_Calendar");
            _calendar?.Classes.Add("daisy-calendar");
            ApplyCalendarSize();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedDateProperty)
            {
                var oldDate = ToDateTimeOffset(change.GetOldValue<DateTime?>());
                var newDate = ToDateTimeOffset(change.GetNewValue<DateTime?>());

                if (!_isSyncingDate)
                {
                    _isSyncingDate = true;
                    try
                    {
                        SetCurrentValue(DateProperty, newDate);
                    }
                    finally
                    {
                        _isSyncingDate = false;
                    }
                }

                DateChanged?.Invoke(this, new DaisyCalendarDatePickerDateChangedEventArgs(oldDate, newDate));
            }
            else if (change.Property == DateProperty)
            {
                if (_isSyncingDate)
                    return;

                _isSyncingDate = true;
                try
                {
                    SetCurrentValue(SelectedDateProperty, change.GetNewValue<DateTimeOffset?>()?.Date);
                }
                finally
                {
                    _isSyncingDate = false;
                }
            }
            else if (change.Property == BorderRingBrushProperty)
            {
                PseudoClasses.Set(":ring", BorderRingBrush != null);
            }
            else if (change.Property == SizeProperty)
            {
                ApplyCalendarSize();
            }
        }

        private void ApplyCalendarSize()
        {
            if (_calendar == null)
                return;

            _calendar.Classes.Set("xs", Size == DaisySize.ExtraSmall);
            _calendar.Classes.Set("sm", Size == DaisySize.Small);
            _calendar.Classes.Set("lg", Size == DaisySize.Large);
            _calendar.Classes.Set("xl", Size == DaisySize.ExtraLarge);
        }

        private static DateTimeOffset? ToDateTimeOffset(DateTime? date) =>
            date is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified)) : null;
    }
}
