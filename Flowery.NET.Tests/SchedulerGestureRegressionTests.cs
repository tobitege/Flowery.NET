using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
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
        // Keep both slots visible so scrolling cannot turn separate slot clicks into a double-click. The date is the
        // last day of its Planner week.
        var scroll = lane.GetVisualAncestors().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(view == SchedulerView.Planner ? lane.Bounds.Width * 6 / 7 : 0, 0);
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
        var lane = Assert.IsAssignableFrom<Canvas>(frame.Parent);
        var origin = scope.Point(frame, resize ? frame.Bounds.Width - 2 : frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        DragPointer(scope, origin, origin + new Vector(lane.Bounds.Width / 1440 * (resize ? 60 : 90), 0));
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

    [AvaloniaTheory]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Timeline)]
    public void OnTouchATapOpensAnAppointmentAHeldOneMovesWithTheFingerAndEverySwipeScrolls(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        // Three hours long, so a drag in the middle stays clear of the resize edges.
        scope.Appointment.End = scope.Appointment.Start.AddHours(3);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var moves = 0;
        var edits = 0;
        scope.Scheduler.AppointmentMoveRequested += (_, _) => moves++;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        var direction = view == SchedulerView.Week ? new Vector(0, 1) : new Vector(1, 0);
        var frame = scope.Frame(scope.Appointment);
        var lane = Assert.IsAssignableFrom<Canvas>(frame.Parent);

        // As in the apps of the platforms: a tap opens the appointment.
        Assert.Equal(0, TouchDrag(scope, frame, frame.Bounds.Center - frame.Bounds.Position, direction, 0));
        Assert.Equal(1, edits);
        Assert.Null(scope.Scheduler.SelectedAppointment);

        // Holding it selects it, and the finger then moves it while the view stays.
        frame = scope.Frame(scope.Appointment);
        Assert.Equal(0, TouchDrag(scope, frame, frame.Bounds.Center - frame.Bounds.Position, direction, hold: true));
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.Equal(1, moves);

        // A swipe without a hold scrolls the whole way, over the appointment and over a free slot, and opens nothing.
        scope.Scheduler.SelectedAppointment = null;
        scope.Layout();
        frame = scope.Frame(scope.Appointment);
        Assert.True(TouchDrag(scope, frame, frame.Bounds.Center - frame.Bounds.Position, direction) > 60, "swipe over an appointment");
        Assert.Null(scope.Scheduler.SelectedAppointment);
        var free = frame.Bounds.Center + direction * (view == SchedulerView.Week ? frame.Bounds.Height : frame.Bounds.Width);
        Assert.True(TouchDrag(scope, lane, free, direction) > 60, "swipe over a free slot");
        Assert.Equal((1, 1), (moves, edits));
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Timeline)]
    public void OnTouchTheEdgeOfAHeldAppointmentIsWideEnoughToResizeItWhereAMouseMovesIt(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var moves = 0;
        var resizes = 0;
        scope.Scheduler.AppointmentMoveRequested += (_, _) => moves++;
        scope.Scheduler.AppointmentResizeRequested += (_, _) => resizes++;
        var direction = view == SchedulerView.Week ? new Vector(0, 1) : new Vector(1, 0);
        var frame = scope.Frame(scope.Appointment);
        // 8 pixels inside the end: within the edge for a finger, beyond it for a mouse.
        var nearEnd = frame.Bounds.Center - frame.Bounds.Position + direction * ((view == SchedulerView.Week ? frame.Bounds.Height : frame.Bounds.Width) / 2 - 8);

        TouchDrag(scope, frame, nearEnd, direction, hold: true);
        Assert.Equal((0, 1), (moves, resizes));

        frame = scope.Frame(scope.Appointment);
        frame.BringIntoView();
        scope.Layout();
        var origin = scope.Point(frame, nearEnd.X, nearEnd.Y);
        DragPointer(scope, origin, origin + direction * 80);
        Assert.Equal((1, 1), (moves, resizes));
    }

    [AvaloniaFact]
    public void OnTouchAHoldSelectsTapsThenToggleTheSelectionAndALockedAppointmentStaysClosedAndUnselected()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var second = new AppointmentModel { Id = 2, Subject = "Second", Start = Monday.AddDays(1).AddHours(9), End = Monday.AddDays(1).AddHours(10), ResourceIds = { 1 } };
        var locked = new AppointmentModel { Id = 3, Subject = "Locked", Start = Monday.AddDays(2).AddHours(9), End = Monday.AddDays(2).AddHours(10), ResourceIds = { 1 }, IsLocked = true };
        scope.Scheduler.Appointments = new[] { scope.Appointment, second, locked };
        scope.Layout();
        var edits = 0;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        int[] Selected() => scope.Scheduler.SelectedAppointments.Select(static appointment => (int)appointment.Id!).Order().ToArray();

        // Holding selects; while appointments are selected, a tap adds one to the selection or takes it out.
        TouchHold(scope, scope.Frame(scope.Appointment));
        Assert.Equal([1], Selected());
        TouchTap(scope, scope.Frame(second));
        Assert.Equal([1, 2], Selected());
        TouchTap(scope, scope.Frame(scope.Appointment));
        Assert.Equal([2], Selected());
        TouchTap(scope, scope.Frame(second));
        Assert.Empty(Selected());
        Assert.Equal(0, edits);

        // Without a selection a tap opens again.
        TouchTap(scope, scope.Frame(second));
        Assert.Equal(1, edits);

        TouchTap(scope, scope.Frame(locked));
        TouchHold(scope, scope.Frame(locked));
        Assert.Equal(1, edits);
        Assert.Empty(Selected());
    }

    [AvaloniaFact]
    public void OnTouchATapThatStopsAScrollingViewOpensNothing()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var edits = 0;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        var frame = scope.Frame(scope.Appointment);
        frame.BringIntoView();
        scope.Layout();
        var presenter = frame.GetVisualAncestors().OfType<ScrollContentPresenter>().First();
        var starting = (ScrollGestureInertiaStartingEventArgs)Activator.CreateInstance(typeof(ScrollGestureInertiaStartingEventArgs),
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, [1, new Vector(0, 500)], null)!;

        presenter.RaiseEvent(starting);
        TouchTap(scope, frame);
        Assert.Equal(0, edits);

        presenter.RaiseEvent(new ScrollGestureEndedEventArgs(1));
        TouchTap(scope, scope.Frame(scope.Appointment));
        Assert.Equal(1, edits);
    }

    [AvaloniaFact]
    public void OnTouchASwipeOverAFreeSlotNeverCreatesAnAppointmentAndADoubleTapDoes()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var creates = 0;
        scope.Scheduler.AppointmentCreateRequested += (_, _) => creates++;
        var lane = Assert.IsAssignableFrom<Canvas>(scope.Frame(scope.Appointment).Parent);
        var scroll = lane.GetVisualAncestors().OfType<ScrollViewer>().First();
        scroll.Offset = default;
        scope.Layout();

        // At the top the view cannot scroll down any further, and a week does not scroll sideways.
        var top = new Point(lane.Bounds.Width / 2, 40);
        TouchDrag(scope, lane, top, new Vector(0, 1));
        TouchDrag(scope, lane, top, new Vector(1, 0));
        Assert.Equal(0, creates);

        // Away from the swipes, which counted as presses at the same spot.
        var free = top + new Vector(0, 120);
        TouchTap(scope, lane, free);
        TouchTap(scope, lane, free);
        Assert.Equal(1, creates);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Week)]
    public void OnTouchSwipesOverAFreeDayNeverCreateAnAppointmentAndADoubleTapThereDoes(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var creates = new List<bool>();
        scope.Scheduler.AppointmentCreateRequested += (_, args) => creates.Add(args.IsAllDay);
        // A month day or the all-day row of a week day, pressed below what it shows.
        var day = scope.Scheduler.GetVisualDescendants().OfType<Control>()
            .First(control => control.Tag?.GetType().Name == "DayTarget" && control.Bounds.Height > 0);
        var free = new Point(day.Bounds.Width / 2, day.Bounds.Height - 3);

        TouchDrag(scope, day, free, new Vector(0, 1), pixels: 40);
        TouchDrag(scope, day, free, new Vector(0, 1), pixels: 40);
        Assert.Empty(creates);

        // The swipes counted as presses at the same spot; the taps after them still need two.
        TouchTap(scope, day, free);
        Assert.Empty(creates);
        TouchTap(scope, day, free);
        Assert.Equal([true], creates);
    }

    [AvaloniaFact]
    public void OnTouchSwipesOverAnAppointmentOpenNothingAndATapAfterThemDoes()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var edits = 0;
        scope.Scheduler.AppointmentEditRequested += (_, _) => edits++;
        var frame = scope.Frame(scope.Appointment);
        var middle = frame.Bounds.Center - frame.Bounds.Position;

        // A week does not scroll sideways, so the swipes start where the tap comes.
        TouchDrag(scope, frame, middle, new Vector(1, 0), pixels: 40);
        TouchDrag(scope, frame, middle, new Vector(1, 0), pixels: 40);
        Assert.Equal(0, edits);
        Assert.Null(scope.Scheduler.SelectedAppointment);
        TouchTap(scope, scope.Frame(scope.Appointment), middle);
        Assert.Equal(1, edits);
    }

    // Holds a touch contact on the middle of the target longer than the hold of the platform, then lifts it.
    private static void TouchHold(SchedulerScope scope, Control target)
    {
        var point = target.Bounds.Center - target.Bounds.Position;
        target.BringIntoView(new Rect(point, new Size(1, 1)));
        scope.Layout();
        var origin = scope.Point(target, point.X, point.Y);
        var touch = scope.Window.TouchBegin(origin);
        WaitForHold(scope);
        scope.Window.TouchEnd(touch, origin);
        scope.Layout();
    }

    private static void WaitForHold(SchedulerScope scope)
    {
        Thread.Sleep(Application.Current!.PlatformSettings!.HoldWaitDuration + TimeSpan.FromMilliseconds(150));
        // The dispatcher looks for due timers only after it ran a job.
        Dispatcher.UIThread.Post(static () => { });
        scope.Layout();
        scope.Layout();
    }

    // Taps a touch contact on the middle of the target, or on a point of it.
    private static void TouchTap(SchedulerScope scope, Control target, RawInputModifiers modifiers = RawInputModifiers.None) =>
        TouchTap(scope, target, target.Bounds.Center - target.Bounds.Position, modifiers);

    private static void TouchTap(SchedulerScope scope, Control target, Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        target.BringIntoView(new Rect(point, new Size(1, 1)));
        scope.Layout();
        var origin = scope.Point(target, point.X, point.Y);
        var touch = scope.Window.TouchBegin(origin, modifiers);
        scope.Window.TouchEnd(touch, origin, modifiers);
        scope.Layout();
    }

    // Drags a touch contact from a point of the target by the given pixels (none is a tap), after holding it first if
    // asked; returns how far the view scrolled against the drag.
    private static double TouchDrag(SchedulerScope scope, Control target, Point point, Vector direction, int pixels = 80, bool hold = false)
    {
        // With room around the point, so the contact stays inside the view.
        target.BringIntoView(new Rect(point, new Size(1, 1)).Inflate(120));
        scope.Layout();
        var scroll = target.GetVisualAncestors().OfType<ScrollViewer>().First();
        var before = scroll.Offset;
        var origin = scope.Point(target, point.X, point.Y);
        var touch = scope.Window.TouchBegin(origin);
        if (hold)
            WaitForHold(scope);
        for (var step = 1; step <= pixels / 10; step++)
            scope.Window.TouchMove(touch, origin + direction * (step * 10));
        scope.Window.TouchEnd(touch, origin + direction * pixels);
        scope.Layout();
        var scrolled = before - scroll.Offset;
        return scrolled.X * direction.X + scrolled.Y * direction.Y;
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
        var x = ((time - origin).TotalMinutes + 1) * lane.Bounds.Width / (days * 1440);
        lane.BringIntoView(new Rect(x - 4, 0, 8, lane.Height));
        scope.Layout();
        return scope.Point(lane, x, lane.Height / 2);
    }
}
