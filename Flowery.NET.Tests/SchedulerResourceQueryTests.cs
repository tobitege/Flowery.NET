using System.Globalization;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Xunit;

namespace Flowery.NET.Tests;

public sealed class SchedulerResourceQueryTests
{
    private static readonly DateTime Monday = new(2026, 10, 12);

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2015)]
    [InlineData(9998)]
    public void DefaultWeekRulesMatchIsoIncludingWeekYearBoundaries(int year)
    {
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1 } } };
        foreach (var week in new[] { 1, 2, ISOWeek.GetWeeksInYear(year) })
        {
            var start = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
            scheduler.Appointments = new[] { Booking(start, start.AddHours(1)) };
            Assert.Single(scheduler.GetOccupiedResources(year, week));
            scheduler.Appointments = new[] { Booking(start.AddDays(7), start.AddDays(7).AddHours(1)) };
            Assert.Empty(scheduler.GetOccupiedResources(year, week));
        }
    }

    [AvaloniaTheory]
    [InlineData(DayOfWeek.Sunday, CalendarWeekRule.FirstDay, 2025, 12, 28)]
    public void WeekOneUsesConfiguredStartAndRule(DayOfWeek firstDay, CalendarWeekRule rule, int year, int month, int day)
    {
        var first = new DateTime(year, month, day);
        var scheduler = new DaisyScheduler { FirstDayOfWeek = firstDay, WeekRule = rule, SchedulerResources = new[] { new ResourceModel { Id = 1 } } };
        scheduler.Appointments = new[] { Booking(first, first.AddMinutes(30)) };
        Assert.Single(scheduler.GetOccupiedResources(2026, 1));
        scheduler.Appointments = new[] { Booking(first.AddDays(-1), first) };
        Assert.Empty(scheduler.GetOccupiedResources(2026, 1));
        scheduler.Appointments = new[] { Booking(first.AddDays(7), first.AddDays(8)) };
        Assert.Empty(scheduler.GetOccupiedResources(2026, 1));
    }

    [AvaloniaTheory]
    [InlineData(2025, 53)]
    [InlineData(9999, 52)]
    public void InvalidOrUnrepresentableWeeksAreRejected(int year, int week)
    {
        var scheduler = new DaisyScheduler();
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.GetOccupiedResources(year, week));
    }

    [AvaloniaFact]
    public void RangeIsHalfOpenAndCancelledOrInvalidBookingsDoNotOccupyResources()
    {
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1 } } };
        foreach (var appointment in new[]
        {
            Booking(Monday.AddHours(-1), Monday), Booking(Monday.AddDays(7), Monday.AddDays(8)),
            Booking(Monday, Monday), Booking(Monday.AddHours(2), Monday.AddHours(1)),
            new AppointmentModel { Start = Monday.AddHours(1), End = Monday.AddHours(2), IsCancelled = true, ResourceIds = { 1 } }
        })
        {
            scheduler.Appointments = new[] { appointment };
            Assert.Empty(scheduler.GetOccupiedResources(2026, 42));
        }
        var booked = Booking(Monday.AddHours(-1), Monday.AddMinutes(1));
        booked.IsLocked = booked.IsComplete = booked.IsTentative = true;
        scheduler.Appointments = new[] { booked };
        Assert.Single(scheduler.GetOccupiedResources(2026, 42));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.GetOccupiedResources(Monday, Monday));
    }

    [AvaloniaFact]
    public void FallBackBookingCountsEvenWhenLocalClockEndPrecedesStart()
    {
        var scheduler = new DaisyScheduler
        {
            SchedulerResources = new[] { new ResourceModel { Id = 1, TimeZoneId = "America/New_York" } },
            Appointments = new[] { Booking(new DateTime(2026, 11, 1, 5, 50, 0), new DateTime(2026, 11, 1, 6, 10, 0)) }
        };
        Assert.Single(scheduler.GetOccupiedResources(2026, 44));
    }

    [AvaloniaFact]
    public void ConflictQueryChecksResourcesDailyWindowsAndExcludesOwnIdentity()
    {
        var daily = Booking(Monday.AddHours(9), Monday.AddDays(4).AddHours(12));
        daily.Id = "daily";
        daily.SpanMode = AppointmentSpanMode.Daily;
        var otherRoom = Booking(Monday.AddHours(9), Monday.AddHours(17));
        otherRoom.ResourceIds.Clear();
        otherRoom.ResourceIds.Add(2);
        var scheduler = new DaisyScheduler { Appointments = new[] { daily, otherRoom } };
        var proposed = Booking(Monday.AddDays(2).AddHours(11), Monday.AddDays(2).AddHours(13));
        Assert.Same(daily, Assert.Single(scheduler.GetConflictingAppointments(proposed)));
        proposed.Start = proposed.End;
        proposed.End = proposed.Start.AddHours(1);
        Assert.Empty(scheduler.GetConflictingAppointments(proposed));
        Assert.Empty(scheduler.GetConflictingAppointments(daily.Clone()));
        proposed.Start = Monday.AddHours(10);
        proposed.End = Monday.AddHours(11);
        daily.IsCancelled = true;
        Assert.Empty(scheduler.GetConflictingAppointments(proposed));
    }

    private static AppointmentModel Booking(DateTime start, DateTime end) => new() { Start = start, End = end, ResourceIds = { 1 } };
}
