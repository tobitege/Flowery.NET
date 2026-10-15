namespace Flowery.Controls.Scheduler.Models;

public enum AppointmentSpanMode
{
    /// <summary>One continuous interval from Start through End (exclusive).</summary>
    Continuous,
    /// <summary>Repeat Start.TimeOfDay through End.TimeOfDay on every date from Start.Date through End.Date.
    /// The end time must follow the start time. All-day ranges retain their exclusive end date.</summary>
    Daily
}
