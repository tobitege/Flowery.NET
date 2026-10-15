using System.Globalization;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Xunit;

namespace Flowery.NET.Tests;

public sealed class SchedulerResourceQueryTests
{
    private static readonly DateTime Monday = new(2026, 10, 12);

    [AvaloniaFact]
    public void KindQueryUsesSharedIdsHostOrderAndOriginalModelsWithoutChangingInput()
    {
        var person = new ResourceModel { Id = "p", Kind = ResourceKind.Person };
        var roomB = new ResourceModel { Id = "b", Kind = ResourceKind.Room };
        var roomA = new ResourceModel { Id = "a", Kind = ResourceKind.Room };
        var idle = new ResourceModel { Id = "idle", Kind = ResourceKind.Room };
        var appointment = new AppointmentModel { Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { "p", "a", "b" } };
        var scheduler = new DaisyScheduler
        {
            SchedulerResources = new[] { person, roomB, roomA, idle, roomA }, Appointments = new[] { appointment },
            CurrentView = SchedulerView.Month, SelectedDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), HideIdleResources = true
        };
        var resources = scheduler.SchedulerResources;
        var appointments = scheduler.Appointments;
        var result = scheduler.GetOccupiedResources(2026, 42, ResourceKind.Room);
        Assert.Equal(new[] { roomB, roomA }, result);
        Assert.Same(roomB, result[0]);
        Assert.Equal(new[] { person }, scheduler.GetOccupiedResources(2026, 42, ResourceKind.Person));
        Assert.Equal(3, scheduler.GetOccupiedResources(2026, 42).Count);
        Assert.Same(resources, scheduler.SchedulerResources);
        Assert.Same(appointments, scheduler.Appointments);
        Assert.Equal(Monday.AddHours(9), appointment.Start);
        Assert.Equal(3, appointment.ResourceIds.Count);
        roomA.Kind = ResourceKind.Equipment;
        Assert.Equal(new[] { roomB }, scheduler.GetOccupiedResources(2026, 42, ResourceKind.Room));
        Assert.Equal(ResourceKind.Unspecified, new ResourceModel().Kind);
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2015)]
    [InlineData(2020)]
    [InlineData(2026)]
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
    [InlineData(DayOfWeek.Sunday, CalendarWeekRule.FirstFullWeek, 2026, 1, 4)]
    [InlineData(DayOfWeek.Monday, CalendarWeekRule.FirstFourDayWeek, 2025, 12, 29)]
    [InlineData(DayOfWeek.Saturday, CalendarWeekRule.FirstFourDayWeek, 2026, 1, 3)]
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
    [InlineData(-1, 1)]
    [InlineData(10000, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 54)]
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
    public void ZonedWeekBoundariesUseUtcWhileAllDayDatesFloat()
    {
        var newYork = new ResourceModel { Id = 1, TimeZoneId = "America/New_York" };
        var wallClock = new ResourceModel { Id = 2 };
        var appointment = Booking(Monday.AddHours(1), Monday.AddHours(2));
        appointment.ResourceIds.Add(2);
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { newYork, wallClock }, Appointments = new[] { appointment } };
        Assert.Equal(new[] { wallClock }, scheduler.GetOccupiedResources(2026, 42));
        Assert.Equal(new[] { newYork }, scheduler.GetOccupiedResources(2026, 41));
        appointment.IsAllDay = true;
        appointment.End = Monday.AddDays(1).AddHours(2);
        Assert.Equal(2, scheduler.GetOccupiedResources(2026, 42).Count);
        Assert.Empty(scheduler.GetOccupiedResources(2026, 41));
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
    public void DailySpansLeaveEachAfternoonFreeAndCountOnce()
    {
        var appointment = Booking(Monday.AddHours(9), Monday.AddDays(4).AddHours(12));
        appointment.SpanMode = AppointmentSpanMode.Daily;
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1 } }, Appointments = new[] { appointment } };
        Assert.Single(scheduler.GetOccupiedResources(2026, 42));
        Assert.Empty(scheduler.GetOccupiedResources(Monday.AddDays(2).AddHours(13), Monday.AddDays(2).AddHours(15)));
        Assert.Single(scheduler.GetOccupiedResources(Monday.AddDays(4).AddHours(11), Monday.AddDays(4).AddHours(12)));
        Assert.Empty(scheduler.GetOccupiedResources(Monday.AddDays(5), Monday.AddDays(6)));
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

    [AvaloniaFact]
    public void AllDayConflictUsesResourceLocalDatesAndCanBeQueriedInBothDirections()
    {
        var allDay = Booking(Monday, Monday.AddDays(1));
        allDay.IsAllDay = true;
        var previousLocalDay = Booking(Monday.AddHours(1), Monday.AddHours(2));
        var sameLocalDay = Booking(Monday.AddHours(5), Monday.AddHours(6));
        var scheduler = new DaisyScheduler { SchedulerResources = new[] { new ResourceModel { Id = 1, TimeZoneId = "America/New_York" } }, Appointments = new[] { allDay } };
        Assert.Empty(scheduler.GetConflictingAppointments(previousLocalDay));
        Assert.Same(allDay, Assert.Single(scheduler.GetConflictingAppointments(sameLocalDay)));
        scheduler.Appointments = new[] { sameLocalDay };
        Assert.Same(sameLocalDay, Assert.Single(scheduler.GetConflictingAppointments(allDay)));
    }

    [AvaloniaFact]
    public void EmptyInputsAndUnassignedDefaultCalendarAreSupported()
    {
        var scheduler = new DaisyScheduler();
        Assert.Empty(scheduler.GetOccupiedResources(2026, 42));
        var first = Booking(Monday, Monday.AddHours(1));
        var second = Booking(Monday, Monday.AddHours(2));
        first.ResourceIds.Clear();
        second.ResourceIds.Clear();
        scheduler.Appointments = new[] { first };
        Assert.Same(first, Assert.Single(scheduler.GetConflictingAppointments(second)));
        Assert.Empty(scheduler.GetOccupiedResources(2026, 42));
    }

    private static AppointmentModel Booking(DateTime start, DateTime end) => new() { Start = start, End = end, ResourceIds = { 1 } };
}
