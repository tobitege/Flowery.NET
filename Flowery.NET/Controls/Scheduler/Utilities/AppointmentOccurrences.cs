using System;
using System.Collections.Generic;
using Flowery.Controls.Scheduler.Models;

namespace Flowery.Controls.Scheduler.Utilities;

/// <summary>Expands the built-in daily span in the appointment's storage time basis. Does not parse RRULE strings.</summary>
public static class AppointmentOccurrences
{
    public static IEnumerable<AppointmentModel> InRange(AppointmentModel appointment, DateTime start, DateTime end)
    {
        if (appointment.IsAllDay && (appointment.Start.TimeOfDay != TimeSpan.Zero || appointment.End.TimeOfDay != TimeSpan.Zero))
        {
            appointment = appointment.Clone();
            appointment.Start = appointment.Start.Date;
            appointment.End = appointment.End.Date;
        }
        if (appointment.End <= appointment.Start || end <= start)
            yield break;
        if (appointment.SpanMode != AppointmentSpanMode.Daily)
        {
            if (appointment.Start < end && appointment.End > start)
                yield return appointment;
            yield break;
        }
        if (!appointment.IsAllDay && appointment.End.TimeOfDay <= appointment.Start.TimeOfDay)
            yield break;
        var day = appointment.Start.Date > start.Date ? appointment.Start.Date : start.Date;
        var last = appointment.IsAllDay ? appointment.End.Date.AddDays(-1) : appointment.End.Date;
        if (last > end.Date)
            last = end.Date;
        for (; day <= last;)
        {
            var occurrence = appointment.Clone();
            occurrence.Start = day + (appointment.IsAllDay ? TimeSpan.Zero : appointment.Start.TimeOfDay);
            occurrence.End = appointment.IsAllDay ? day.AddDays(1) : day + appointment.End.TimeOfDay;
            if (occurrence.Start < end && occurrence.End > start)
                yield return occurrence;
            if (day == DateTime.MaxValue.Date)
                yield break;
            day = day.AddDays(1);
        }
    }
}
