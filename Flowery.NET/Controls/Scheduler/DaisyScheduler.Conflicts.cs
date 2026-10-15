using System;
using System.Collections.Generic;
using System.Linq;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Utilities;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    /// <summary>Returns original, non-cancelled appointments that overlap a candidate on shared resources.
    /// Two unassigned appointments share the default calendar. The candidate's own id is excluded.
    /// Overlaps are allowed; the host decides how to warn or confirm. Daily spans are expanded; RRULE is not.</summary>
    public IReadOnlyList<AppointmentModel> GetConflictingAppointments(AppointmentModel candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.IsCancelled || candidate.End <= candidate.Start)
            return Array.Empty<AppointmentModel>();
        var result = new List<AppointmentModel>();
        foreach (var existing in Appointments ?? Array.Empty<AppointmentModel>())
        {
            if (existing.IsCancelled || Same(existing, candidate) ||
                !(existing.ResourceIds.Count == 0 && candidate.ResourceIds.Count == 0 ||
                  existing.ResourceIds.Intersect(candidate.ResourceIds).Any()))
                continue;
            var sharedIds = candidate.ResourceIds.Intersect(existing.ResourceIds).ToArray();
            var zones = sharedIds.Select(id => SchedulerResources?.FirstOrDefault(resource => Equals(resource.Id, id))?.TimeZoneId)
                .DefaultIfEmpty(null).Distinct();
            var found = false;
            foreach (var zone in zones)
            {
                if (!AppointmentsOverlap(candidate, existing, zone))
                    continue;
                found = true;
                break;
            }
            if (found && !result.Any(item => Same(item, existing)))
                result.Add(existing);
        }
        return result.ToArray();
    }

    private static bool AppointmentsOverlap(AppointmentModel candidate, AppointmentModel existing, string? zone)
    {
        var existingStart = existing.IsAllDay ? TimeZoneConverter.FromZone(existing.Start.Date, zone) : existing.Start;
        var existingEnd = existing.IsAllDay ? TimeZoneConverter.FromZone(existing.End.Date, zone) : existing.End;
        var from = candidate.IsAllDay ? TimeZoneConverter.ToZone(existingStart, zone) : existingStart;
        var through = candidate.IsAllDay ? TimeZoneConverter.ToZone(existingEnd, zone) : existingEnd;
        foreach (var occurrence in AppointmentOccurrences.InRange(candidate, from, through))
        {
            var start = candidate.IsAllDay ? TimeZoneConverter.FromZone(occurrence.Start.Date, zone) : occurrence.Start;
            var end = candidate.IsAllDay ? TimeZoneConverter.FromZone(occurrence.End.Date, zone) : occurrence.End;
            var existingFrom = existing.IsAllDay ? TimeZoneConverter.ToZone(start, zone) : start;
            var existingThrough = existing.IsAllDay ? TimeZoneConverter.ToZone(end, zone) : end;
            foreach (var other in AppointmentOccurrences.InRange(existing, existingFrom, existingThrough))
            {
                var otherStart = existing.IsAllDay ? TimeZoneConverter.FromZone(other.Start.Date, zone) : other.Start;
                var otherEnd = existing.IsAllDay ? TimeZoneConverter.FromZone(other.End.Date, zone) : other.End;
                if (start < otherEnd && end > otherStart)
                    return true;
            }
        }
        return false;
    }
}
