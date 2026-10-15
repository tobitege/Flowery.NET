using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Flowery.Controls.Scheduler.Models;
using Flowery.Localization;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    public static readonly StyledProperty<CalendarWeekRule> WeekRuleProperty =
        AvaloniaProperty.Register<DaisyScheduler, CalendarWeekRule>(nameof(WeekRule), CalendarWeekRule.FirstFourDayWeek,
            validate: static value => Enum.IsDefined(value));

    /// <summary>First week of a week-numbering year. Combined with FirstDayOfWeek; defaults to ISO rules.</summary>
    public CalendarWeekRule WeekRule
    {
        get => GetValue(WeekRuleProperty);
        set => SetValue(WeekRuleProperty, value);
    }

    public static readonly StyledProperty<SchedulerDateTimeFormat> DateTimeFormatProperty =
        AvaloniaProperty.Register<DaisyScheduler, SchedulerDateTimeFormat>(nameof(DateTimeFormat), SchedulerDateTimeFormat.Locale,
            validate: static value => Enum.IsDefined(value));

    public SchedulerDateTimeFormat DateTimeFormat
    {
        get => GetValue(DateTimeFormatProperty);
        set => SetValue(DateTimeFormatProperty, value);
    }

    public static readonly StyledProperty<SchedulerClockFormat> ClockFormatProperty =
        AvaloniaProperty.Register<DaisyScheduler, SchedulerClockFormat>(nameof(ClockFormat), SchedulerClockFormat.Locale,
            validate: static value => Enum.IsDefined(value));

    public SchedulerClockFormat ClockFormat
    {
        get => GetValue(ClockFormatProperty);
        set => SetValue(ClockFormatProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<Avalonia.Media.Color>?> AppointmentColorsProperty =
        AvaloniaProperty.Register<DaisyScheduler, IReadOnlyList<Avalonia.Media.Color>?>(nameof(AppointmentColors), null);

    /// <summary>Optional host editor palette. Null or empty hides the palette in the Gallery editor.
    /// A null appointment BackColor always means the theme default.</summary>
    public IReadOnlyList<Avalonia.Media.Color>? AppointmentColors
    {
        get => GetValue(AppointmentColorsProperty);
        set => SetValue(AppointmentColorsProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> TimeSlotStartProperty =
        AvaloniaProperty.Register<DaisyScheduler, TimeSpan>(nameof(TimeSlotStart), TimeSpan.FromHours(7),
            validate: static value => value >= TimeSpan.Zero && value < TimeSpan.FromDays(1));

    /// <summary>First time offered to host editors by GetTimeSlots(). Does not restrict appointment data.</summary>
    public TimeSpan TimeSlotStart
    {
        get => GetValue(TimeSlotStartProperty);
        set => SetValue(TimeSlotStartProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> TimeSlotEndProperty =
        AvaloniaProperty.Register<DaisyScheduler, TimeSpan>(nameof(TimeSlotEnd), TimeSpan.FromHours(17),
            validate: static value => value >= TimeSpan.Zero && value < TimeSpan.FromDays(1));

    /// <summary>Last offered time, included if it lies on the configured interval.</summary>
    public TimeSpan TimeSlotEnd
    {
        get => GetValue(TimeSlotEndProperty);
        set => SetValue(TimeSlotEndProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> TimeSlotIntervalProperty =
        AvaloniaProperty.Register<DaisyScheduler, TimeSpan>(nameof(TimeSlotInterval), TimeSpan.FromMinutes(30),
            validate: static value => value >= TimeSpan.FromMinutes(1) && value <= TimeSpan.FromDays(1));

    /// <summary>Editor time step. Independent of TimeIncrement, which controls grid snapping.</summary>
    public TimeSpan TimeSlotInterval
    {
        get => GetValue(TimeSlotIntervalProperty);
        set => SetValue(TimeSlotIntervalProperty, value);
    }

    /// <summary>Culture for date/time display and host editor parsing. UI translations use FloweryLocalization.</summary>
    public CultureInfo FormattingCulture => DateTimeFormat switch
    {
        SchedulerDateTimeFormat.US => CultureInfo.GetCultureInfo("en-US"),
        SchedulerDateTimeFormat.UK => CultureInfo.GetCultureInfo("en-GB"),
        SchedulerDateTimeFormat.European => CultureInfo.GetCultureInfo("de-DE"),
        _ => FloweryLocalization.CurrentCulture
    };

    private string TimePattern => ClockFormat switch
    {
        SchedulerClockFormat.TwelveHour => "h:mm tt",
        SchedulerClockFormat.TwentyFourHour => "HH:mm",
        _ => FormattingCulture.DateTimeFormat.ShortTimePattern
    };

    /// <summary>Formats a time with this scheduler's clock and regional overrides.</summary>
    public string FormatTime(DateTime time) => time.ToString(TimePattern, FormattingCulture);

    /// <summary>Formats an editor date using the selected regional convention.</summary>
    public string FormatDate(DateTime date) => date.ToString("d", FormattingCulture);

    /// <summary>Returns the inclusive editor time sequence. Does not round or change appointments.</summary>
    /// <exception cref="InvalidOperationException">TimeSlotEnd precedes TimeSlotStart.</exception>
    public IReadOnlyList<TimeSpan> GetTimeSlots()
    {
        if (TimeSlotEnd < TimeSlotStart)
            throw new InvalidOperationException("TimeSlotEnd must not precede TimeSlotStart.");
        var slots = new List<TimeSpan>();
        for (var time = TimeSlotStart; time <= TimeSlotEnd; time += TimeSlotInterval)
            slots.Add(time);
        return slots.ToArray();
    }
}
