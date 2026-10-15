using System;
using System.Collections.Generic;
using System.Globalization;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Utilities;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    /// <summary>Returns resources booked during a week using FirstDayOfWeek and WeekRule.</summary>
    /// <param name="year">Gregorian week-numbering year, which can differ from the date's calendar year.</param>
    /// <param name="week">Week number in that week-numbering year.</param>
    /// <param name="kind">Resource category, or null to include every category.</param>
    /// <returns>Original resource models, once per id, in the order of <see cref="SchedulerResources"/>.</returns>
    /// <remarks>Weeks always contain seven days. Week 1 follows WeekRule; the next year's week 1 ends this week-year.
    /// Monday with FirstFourDayWeek is ISO 8601. Sunday with FirstDay starts week 1 on or before January 1.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The year/week is invalid or the full week exceeds the DateTime range.</exception>
    public IReadOnlyList<ResourceModel> GetOccupiedResources(int year, int week, ResourceKind? kind = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        var january = new DateTime(year, 1, 1).Ticks / TimeSpan.TicksPerDay;
        var first = FirstWeekStart(january);
        var next = FirstWeekStart(january + (DateTime.IsLeapYear(year) ? 366 : 365));
        var weeksInYear = (next - first) / 7;
        if (week < 1 || week > weeksInYear)
            throw new ArgumentOutOfRangeException(nameof(week), week, "Invalid week for this week-numbering year.");
        var startDay = first + (week - 1) * 7;
        if (startDay < 0 || startDay + 7 > DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay)
            throw new ArgumentOutOfRangeException(nameof(week), week, "The complete week must fit within the DateTime range.");
        var start = new DateTime(startDay * TimeSpan.TicksPerDay);
        return GetOccupiedResources(start, start.AddDays(7), kind);
    }

    private long FirstWeekStart(long januaryDay)
    {
        // Day zero (0001-01-01) was Monday. Day numbers also allow the boundary after year 9999.
        var offset = (januaryDay + 8 - (int)FirstDayOfWeek) % 7;
        var minimumDays = WeekRule switch
        {
            CalendarWeekRule.FirstDay => 1,
            CalendarWeekRule.FirstFullWeek => 7,
            _ => 4
        };
        return januaryDay - offset + (7 - offset < minimumDays ? 7 : 0);
    }

    /// <summary>Returns resources with a non-cancelled appointment overlapping the requested time range.</summary>
    /// <param name="startInclusive">Inclusive range start, in each resource's local wall-clock time.</param>
    /// <param name="endExclusive">Exclusive range end, in each resource's local wall-clock time.</param>
    /// <param name="kind">Resource category, or null to include every category.</param>
    /// <returns>Original resource models, once per id, in host order. The displayed view does not affect the result.</returns>
    /// <remarks>
    /// Reads the currently assigned Appointments and SchedulerResources without changing either.
    /// Timed appointments use a resource's TimeZoneId in the same way as Planner lanes.
    /// All-day appointments use date components with an exclusive end date. Appointments without
    /// positive duration are ignored. Recurrence rules are not expanded; supply each occurrence.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The end is not after the start.</exception>
    public IReadOnlyList<ResourceModel> GetOccupiedResources(DateTime startInclusive, DateTime endExclusive, ResourceKind? kind = null)
    {
        if (endExclusive <= startInclusive)
            throw new ArgumentOutOfRangeException(nameof(endExclusive), "The end must be after the start.");
        if (Appointments is not { Count: > 0 } appointments || SchedulerResources is not { Count: > 0 } resources)
            return Array.Empty<ResourceModel>();

        var result = new List<ResourceModel>();
        var includedIds = new HashSet<object>();
        foreach (var resource in resources)
        {
            if (kind is { } requestedKind && resource.Kind != requestedKind || includedIds.Contains(resource.Id))
                continue;
            var timedStart = TimeZoneConverter.FromZone(startInclusive, resource.TimeZoneId);
            var timedEnd = TimeZoneConverter.FromZone(endExclusive, resource.TimeZoneId);
            foreach (var appointment in appointments)
            {
                if (appointment.IsCancelled || !appointment.ResourceIds.Contains(resource.Id))
                    continue;
                var rangeStart = appointment.IsAllDay ? startInclusive : timedStart;
                var rangeEnd = appointment.IsAllDay ? endExclusive : timedEnd;
                if (!System.Linq.Enumerable.Any(AppointmentOccurrences.InRange(appointment, rangeStart, rangeEnd)))
                    continue;
                includedIds.Add(resource.Id);
                result.Add(resource);
                break;
            }
        }
        return result.ToArray();
    }
}
