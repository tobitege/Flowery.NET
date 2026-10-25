// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System;
namespace Flowery.Controls.Scheduler.Utilities;

/// <summary>
/// Formats event times as compactly as possible: a 12-hour clock with no leading zero, minutes
/// shown only when non-zero, and a single lowercase am/pm letter -- e.g. "10a", "12:30p". A range
/// drops the start's am/pm when it matches the end's, so "10:00 AM - 12:30 PM" renders "10a-12:30p"
/// and "9:00 AM - 10:30 AM" renders "9-10:30a".
/// </summary>
/// <remarks>
/// <see cref="RangeFormatter"/> is the swap point: assign your own delegate (e.g. a 24-hour or
/// culture-specific format) and every default event block picks it up. The appointment context
/// exposes the result as <c>CompactTimeRange</c>.
/// </remarks>
public static class SchedulerTimeFormatter
{
    /// <summary>The active range formatter. Defaults to <see cref="CompactRange"/>; reassign to customize.</summary>
    public static Func<DateTime, DateTime, string> RangeFormatter { get; set; } = CompactRange;

    /// <summary>A single time, compact: "10a", "12:30p".</summary>
    public static string Compact(DateTime t) => Core(t) + Suffix(t);

    // The start time of a single-line chip or bar: compact on a 12-hour clock, as the pattern writes it on a 24-hour one.
    internal static string CompactStart(DateTime time, string timePattern, IFormatProvider culture) =>
        timePattern.Contains('H') ? time.ToString(timePattern, culture) : Compact(time);

    /// <summary>A start-end span, compact: "10a-12:30p" (or "9-10:30a" when both share am/pm).</summary>
    public static string CompactRange(DateTime start, DateTime end)
    {
        var sameMeridiem = start.Hour < 12 == end.Hour < 12;
        var startPart = sameMeridiem ? Core(start) : Core(start) + Suffix(start);
        return $"{startPart}-{Compact(end)}";
    }

    // Hour + optional ":mm", no am/pm suffix.
    private static string Core(DateTime t)
    {
        var hour = t.Hour % 12;
        if (hour == 0)
        {
            hour = 12;
        }
        return t.Minute == 0 ? hour.ToString() : $"{hour}:{t.Minute:00}";
    }

    private static string Suffix(DateTime t) => t.Hour < 12 ? "a" : "p";
}
