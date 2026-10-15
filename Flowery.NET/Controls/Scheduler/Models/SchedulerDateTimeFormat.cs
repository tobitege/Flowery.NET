namespace Flowery.Controls.Scheduler.Models;

/// <summary>Date and time conventions, independent of the UI language.</summary>
public enum SchedulerDateTimeFormat
{
    Locale,
    US,
    UK,
    European
}

/// <summary>Overrides the clock used by the selected date/time convention.</summary>
public enum SchedulerClockFormat
{
    Locale,
    TwelveHour,
    TwentyFourHour
}
