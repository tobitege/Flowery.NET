using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Utilities;
using Xunit;

namespace Flowery.NET.Tests;

public sealed partial class SchedulerTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupMoveUsesSelectedVisibleResourceLanesAfterHostRefresh(bool allDay)
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        scope.Scheduler.SchedulerResources = new[]
        {
            new ResourceModel { Id = "alex", Kind = ResourceKind.Person },
            new ResourceModel { Id = "bailey", Kind = ResourceKind.Person },
            new ResourceModel { Id = "a", Kind = ResourceKind.Room },
            new ResourceModel { Id = "equipment", Kind = ResourceKind.Equipment },
            new ResourceModel { Id = "b", Kind = ResourceKind.Room },
            new ResourceModel { Id = "c", Kind = ResourceKind.Room }
        };
        var first = new AppointmentModel { Id = 1, Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { "alex", "a", "b" } };
        var second = new AppointmentModel { Id = 2, Start = Monday.AddHours(11), End = Monday.AddHours(12), ResourceIds = { "alex", "a", "b" } };
        if (allDay)
        {
            second.IsAllDay = true;
            second.Start = Monday;
            second.End = Monday.AddDays(1);
        }
        scope.Scheduler.Appointments = new[] { first, second };
        scope.Layout();
        SelectFrame(scope, scope.Frames(first).First());
        SelectFrame(scope, scope.Frames(second).Last(), multiple: true);
        // Replacement instances must retain which of their two room columns was selected.
        first = first.Clone();
        second = second.Clone();
        scope.Scheduler.Appointments = new[] { first, second };
        scope.Layout();
        Assert.Equal(2, scope.Scheduler.SelectedAppointments.Count);
        AppointmentsMoveArgs? request = null;
        scope.Scheduler.AppointmentsMoveRequested += (_, args) => request = args;
        var source = scope.Frames(first).First();
        var target = scope.Frames(first).Last();
        var origin = scope.Point(source, source.Bounds.Width / 2, source.Bounds.Height / 3);
        var destination = scope.Point(target, target.Bounds.Width / 2, target.Bounds.Height / 3);
        DragPointer(scope, origin, destination);
        Assert.NotNull(request);
        var movedFirst = Assert.Single(request.Items, item => ReferenceEquals(item.Appointment, first));
        var movedSecond = Assert.Single(request.Items, item => ReferenceEquals(item.Appointment, second));
        Assert.Equal("a", movedFirst.FromResourceId);
        Assert.Equal("b", movedFirst.ToResourceId);
        Assert.Equal("b", movedSecond.FromResourceId);
        Assert.Equal("c", movedSecond.ToResourceId);
        Assert.Equal(first.Start, movedFirst.NewStart);
        Assert.Equal(second.Start, movedSecond.NewStart);
        Assert.Equal(new object[] { "alex", "a", "b" }, first.ResourceIds);
        Assert.Equal(new object[] { "alex", "a", "b" }, second.ResourceIds);
    }

    [AvaloniaFact]
    public void DailyMoveRejectsMidnightCrossingAndStillAcceptsAValidMove()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        scope.Appointment.Start = Monday.AddHours(22);
        scope.Appointment.End = Monday.AddDays(4).AddHours(23);
        scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        frame.BringIntoView();
        scope.Layout();
        AppointmentMoveArgs? request = null;
        scope.Scheduler.AppointmentMoveRequested += (_, args) => request = args;
        var origin = scope.Point(frame, frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        DragPointer(scope, origin, origin + new Vector(0, 48));
        Assert.Null(request);
        Assert.Equal(5, AppointmentOccurrences.InRange(scope.Appointment, Monday, Monday.AddDays(7)).Count());
        origin = scope.Point(frame, frame.Bounds.Width / 2, frame.Bounds.Height / 4);
        DragPointer(scope, origin, origin - new Vector(0, 48));
        Assert.NotNull(request);
        Assert.Equal(Monday.AddHours(21), request.NewStart);
        Assert.Equal(Monday.AddDays(4).AddHours(22), request.NewEnd);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupEditsRejectAnInvalidDailyWindowAtomically(bool resize)
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        scope.Appointment.End = Monday.AddHours(12);
        var daily = new AppointmentModel
        {
            Id = 2, SpanMode = AppointmentSpanMode.Daily,
            Start = Monday.AddHours(resize ? 9.5 : 22),
            End = Monday.AddDays(4).AddHours(resize ? 10 : 23)
        };
        scope.Scheduler.Appointments = new[] { scope.Appointment, daily };
        scope.Layout();
        SelectFrame(scope, scope.Frame(daily));
        SelectFrame(scope, scope.Frame(scope.Appointment), multiple: true);
        var requests = 0;
        scope.Scheduler.AppointmentsMoveRequested += (_, _) => requests++;
        scope.Scheduler.AppointmentsResizeRequested += (_, _) => requests++;
        var frame = scope.Frame(scope.Appointment);
        var origin = scope.Point(frame, frame.Bounds.Width / 2, resize ? frame.Bounds.Height - 2 : frame.Bounds.Height / 3);
        DragPointer(scope, origin, origin + new Vector(0, resize ? -24 : 48));
        Assert.Equal(0, requests);
        Assert.Equal(5, AppointmentOccurrences.InRange(daily, Monday, Monday.AddDays(7)).Count());
        Assert.Equal(Monday.AddHours(9), scope.Appointment.Start);
        Assert.Equal(Monday.AddHours(12), scope.Appointment.End);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Planner)]
    [InlineData(SchedulerView.Timeline)]
    public void SpringGapClickAndExternalDropAreRejectedWithoutBreakingLaterInput(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var date = new DateTime(2026, 3, 29);
        scope.Scheduler.SelectedDate = new DateTimeOffset(date);
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, TimeZoneId = "Europe/Berlin" } };
        scope.Scheduler.Appointments = Array.Empty<AppointmentModel>();
        scope.Scheduler.SingleClickCreates = true;
        scope.Layout();
        var lane = scope.Scheduler.GetVisualDescendants().OfType<Canvas>().Single(DragDrop.GetAllowDrop);
        // Keep both slots visible so scrolling cannot turn separate slot clicks into a double-click.
        var scroll = lane.GetVisualAncestors().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(view == SchedulerView.Planner ? scroll.Extent.Width : 0, 0);
        scope.Layout();
        var selections = 0;
        var creations = new List<AppointmentCreateArgs>();
        var drops = new List<ExternalDropArgs>();
        scope.Scheduler.EmptySlotSelected += (_, _) => selections++;
        scope.Scheduler.AppointmentCreateRequested += (_, args) => creations.Add(args);
        scope.Scheduler.ExternalAppointmentDropped += (_, args) => drops.Add(args);
        var gap = HorizontalSlotPoint(scope, lane, date.AddHours(2.5));
        scope.Window.MouseDown(gap, MouseButton.Left);
        scope.Window.MouseUp(gap, MouseButton.Left);
        Assert.Equal(0, selections);
        Assert.Empty(creations);
        using var data = ExternalDropHandler.CreateDataTransfer(new Droppable());
        scope.Window.DragDrop(gap, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        scope.Window.DragDrop(gap, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        Assert.Empty(drops);
        var valid = HorizontalSlotPoint(scope, lane, date.AddHours(1.5));
        scope.Window.MouseDown(valid, MouseButton.Left);
        scope.Window.MouseUp(valid, MouseButton.Left);
        Assert.Equal(1, selections);
        Assert.Equal(date.AddMinutes(30), Assert.Single(creations).Start);
        scope.Window.DragDrop(valid, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        scope.Window.DragDrop(valid, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        Assert.Equal(date.AddMinutes(30), Assert.Single(drops).Start);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpringGapMoveAndResizeDoNotPublishChanges(bool resize)
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var date = new DateTime(2026, 3, 29);
        scope.Scheduler.SelectedDate = new DateTimeOffset(date);
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, TimeZoneId = "Europe/Berlin" } };
        scope.Appointment.Start = date;
        scope.Appointment.End = date.AddMinutes(30);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        if (resize)
            scope.Scheduler.SelectedAppointment = scope.Appointment;
        scope.Layout();
        var requests = 0;
        scope.Scheduler.AppointmentMoveRequested += (_, _) => requests++;
        scope.Scheduler.AppointmentResizeRequested += (_, _) => requests++;
        var frame = scope.Frame(scope.Appointment);
        frame.BringIntoView();
        scope.Layout();
        var lane = Assert.IsType<Canvas>(frame.Parent);
        var origin = scope.Point(frame, resize ? frame.Bounds.Width - 2 : frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        DragPointer(scope, origin, origin + new Vector(lane.Width / 1440 * (resize ? 60 : 90), 0));
        Assert.Equal(0, requests);
        Assert.Equal(date, scope.Appointment.Start);
        Assert.Equal(date.AddMinutes(30), scope.Appointment.End);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Day)]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Planner)]
    [InlineData(SchedulerView.Timeline)]
    public void HideIdleResourcesUsesTheDisplayedTimeBasis(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, TimeZoneId = "America/New_York" } };
        scope.Appointment.Start = Monday.AddHours(1);
        scope.Appointment.End = Monday.AddHours(2);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var horizontal = view is SchedulerView.Planner or SchedulerView.Timeline;
        Assert.Equal(horizontal ? 0 : 1, scope.Frames(scope.Appointment).Count());
        scope.Scheduler.HideIdleResources = true;
        scope.Layout();
        Assert.Equal(horizontal ? 0 : 1, scope.Frames(scope.Appointment).Count());
        // Public occupancy queries retain their resource-local contract in every view.
        Assert.Empty(scope.Scheduler.GetOccupiedResources(2026, 42));
        scope.Appointment.IsCancelled = true;
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        Assert.Empty(scope.Frames(scope.Appointment));
    }

    private static void SelectFrame(SchedulerScope scope, Border frame, bool multiple = false)
    {
        frame.BringIntoView();
        scope.Layout();
        var point = scope.Point(frame, frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        var modifiers = multiple ? RawInputModifiers.Control : RawInputModifiers.None;
        scope.Window.MouseDown(point, MouseButton.Left, modifiers);
        scope.Window.MouseUp(point, MouseButton.Left, modifiers);
        scope.Layout();
    }

    private static void DragPointer(SchedulerScope scope, Point origin, Point destination)
    {
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(destination);
        scope.Window.MouseUp(destination, MouseButton.Left);
        scope.Layout();
    }

    private static Point HorizontalSlotPoint(SchedulerScope scope, Canvas lane, DateTime time)
    {
        var origin = scope.Scheduler.SelectedDate.Date;
        var days = scope.Scheduler.CurrentView == SchedulerView.Planner ? 7 : 1;
        if (days == 7)
            origin = origin.AddDays(-((7 + (int)origin.DayOfWeek - (int)scope.Scheduler.FirstDayOfWeek) % 7));
        var x = ((time - origin).TotalMinutes + 1) * lane.Width / (days * 1440);
        lane.BringIntoView(new Rect(x - 4, 0, 8, lane.Height));
        scope.Layout();
        return scope.Point(lane, x, lane.Height / 2);
    }
}
