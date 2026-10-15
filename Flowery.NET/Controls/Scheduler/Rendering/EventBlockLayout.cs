// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System;
using System.Collections.Generic;
using Flowery.Controls.Scheduler.Models;

namespace Flowery.Controls.Scheduler.Rendering;

/// <summary>The computed placement of one appointment within a lane / column.</summary>
/// <param name="Appointment">The laid-out appointment.</param>
/// <param name="StartMinutes">Minutes from the lane origin (midnight for a day column, range-start for a timeline).</param>
/// <param name="EndMinutes">Minutes from the lane origin of the end.</param>
/// <param name="ColumnIndex">0-based slot within its overlap cluster (a side-by-side column in a day grid; a stacked sub-row in a timeline).</param>
/// <param name="ColumnCount">Number of slots in its overlap cluster (the divisor for width / row height).</param>
public readonly record struct EventLayout(
    AppointmentModel Appointment,
    double StartMinutes,
    double EndMinutes,
    int ColumnIndex,
    int ColumnCount);

/// <summary>
/// Pure overlap-resolution. Splits mutually-overlapping appointments into parallel slots using
/// the standard greedy interval-packing algorithm: events are grouped into maximal overlap
/// clusters, and within a cluster each takes the first slot whose previous event has ended.
/// Used both vertically (day-grid columns) and horizontally (timeline sub-rows).
/// </summary>
/// <remarks>No UI dependencies — deterministic and unit-testable in isolation.</remarks>
public static class EventBlockLayout
{
    /// <summary>Minimum minutes a very short (or zero-length) appointment occupies, so it stays visible.</summary>
    public const double DefaultMinVisibleMinutes = 15;

    /// <summary>
    /// Lay out the timed appointments that intersect <paramref name="day"/> (a date at midnight),
    /// with minutes measured from midnight. Times are clipped to the day. Banner (all-day /
    /// multi-day) events should be filtered out before calling. <paramref name="minVisibleMinutes"/> stretches
    /// shorter events so they get room to render (and overlap-pack as that taller block).
    /// </summary>
    public static IReadOnlyList<EventLayout> Layout(
        DateTime day, IEnumerable<AppointmentModel> appointments, double minVisibleMinutes = DefaultMinVisibleMinutes)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        return LayoutRange(dayStart, dayEnd, appointments, minVisibleMinutes);
    }

    /// <summary>
    /// Lay out the timed appointments that intersect <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>,
    /// with minutes measured from <paramref name="rangeStart"/>. Used by the continuous timeline,
    /// where the range can span many days. Times are clipped to the range.
    /// </summary>
    public static IReadOnlyList<EventLayout> LayoutRange(
        DateTime rangeStart, DateTime rangeEnd, IEnumerable<AppointmentModel> appointments,
        double minVisibleMinutes = DefaultMinVisibleMinutes)
    {
        var totalMinutes = (rangeEnd - rangeStart).TotalMinutes;

        var spans = new List<(AppointmentModel Appt, double Start, double End)>();
        foreach (var a in appointments)
        {
            if (a.End <= rangeStart || a.Start >= rangeEnd)
            {
                continue;
            }

            var s = a.Start < rangeStart ? rangeStart : a.Start;
            var e = a.End > rangeEnd ? rangeEnd : a.End;
            var startMin = (s - rangeStart).TotalMinutes;
            var endMin = (e - rangeStart).TotalMinutes;

            if (endMin - startMin < minVisibleMinutes)
            {
                endMin = Math.Min(startMin + minVisibleMinutes, totalMinutes);
            }

            spans.Add((a, startMin, endMin));
        }

        return Pack(spans);
    }

    private static IReadOnlyList<EventLayout> Pack(List<(AppointmentModel Appt, double Start, double End)> spans)
    {
        if (spans.Count == 0)
        {
            return Array.Empty<EventLayout>();
        }

        // Sort by start, then longer-first so wide blocks anchor the first slots.
        spans.Sort((x, y) =>
        {
            var c = x.Start.CompareTo(y.Start);
            return c != 0 ? c : y.End.CompareTo(x.End);
        });

        var results = new List<EventLayout>(spans.Count);

        var clusterStart = 0;
        var clusterMaxEnd = double.NegativeInfinity;
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i].Start >= clusterMaxEnd && i > clusterStart)
            {
                FlushCluster(spans, clusterStart, i, results);
                clusterStart = i;
                clusterMaxEnd = double.NegativeInfinity;
            }

            clusterMaxEnd = Math.Max(clusterMaxEnd, spans[i].End);
        }
        FlushCluster(spans, clusterStart, spans.Count, results);

        return results;
    }

    private static void FlushCluster(
        List<(AppointmentModel Appt, double Start, double End)> spans,
        int from,
        int toExclusive,
        List<EventLayout> results)
    {
        var columnEnds = new List<double>();
        var assigned = new int[toExclusive - from];

        for (var i = from; i < toExclusive; i++)
        {
            var start = spans[i].Start;

            var placed = -1;
            for (var c = 0; c < columnEnds.Count; c++)
            {
                if (columnEnds[c] <= start)
                {
                    placed = c;
                    break;
                }
            }

            if (placed < 0)
            {
                placed = columnEnds.Count;
                columnEnds.Add(spans[i].End);
            }
            else
            {
                columnEnds[placed] = spans[i].End;
            }

            assigned[i - from] = placed;
        }

        var columnCount = columnEnds.Count;
        for (var i = from; i < toExclusive; i++)
        {
            var (appt, start, end) = spans[i];
            results.Add(new EventLayout(appt, start, end, assigned[i - from], columnCount));
        }
    }
}
