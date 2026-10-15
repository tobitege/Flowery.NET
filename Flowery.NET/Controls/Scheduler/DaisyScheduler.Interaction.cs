// Avalonia adaptation of Xtreme.Components Scheduler (MIT).
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Utilities;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private sealed record AppointmentTarget(AppointmentModel Appointment, Lane? Lane, AppointmentModel Displayed, object? ResourceId);
    private sealed record DayTarget(DateTime Date, ResourceModel? Resource);
    private enum GestureKind { Create, Move, ResizeStart, ResizeEnd }

    private sealed class Gesture
    {
        public required IPointer Pointer { get; init; }
        public required Lane Source { get; init; }
        public required Point PressPoint { get; init; }
        public required DateTime PressTime { get; init; }
        public required GestureKind Kind { get; init; }
        public required int ClickCount { get; init; }
        public AppointmentModel? Appointment { get; init; }
        public required DateTime OriginalStart { get; init; }
        public required DateTime OriginalEnd { get; init; }
        public Lane? Target { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool Started { get; set; }
        public Border? Ghost { get; set; }
    }

    private Gesture? _gesture;

    private void OnAppointmentPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: AppointmentTarget target } frame)
            return;
        var appointment = target.Appointment;
        var point = e.GetCurrentPoint(frame);
        var selectedBefore = _selection.Any(selected => Same(selected, appointment));
        if (point.Properties.IsRightButtonPressed)
        {
            if (!appointment.IsLocked)
                RememberSelectionResource(target);
            if (!appointment.IsLocked && !selectedBefore)
                SelectAppointment(appointment, false);
            AppointmentContextRequested?.Invoke(this, new AppointmentContextRequestedArgs { Appointment = appointment, Position = e.GetPosition(TopLevel.GetTopLevel(this)) });
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
            return;
        e.Handled = true;
        if (appointment.IsLocked)
            return;
        RememberSelectionResource(target);
        var multiple = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (multiple || !selectedBefore)
            SelectAppointment(appointment, multiple);
        else
            Focus(NavigationMethod.Pointer);
        if (e.ClickCount == 2 && !multiple)
        {
            AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = appointment });
            return;
        }
        if (IsReadOnly || target.Lane is not { } lane || multiple && selectedBefore)
            return;
        var kind = GestureKind.Move;
        if (selectedBefore)
        {
            var axis = lane.Horizontal ? point.Position.X : point.Position.Y;
            var length = lane.Horizontal ? frame.Bounds.Width : frame.Bounds.Height;
            if (axis < 5)
                kind = GestureKind.ResizeStart;
            else if (axis > length - 5)
                kind = GestureKind.ResizeEnd;
        }
        BeginGesture(e, frame, lane, kind, appointment, target.Displayed);
    }

    private void OnAppointmentKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Border { Tag: AppointmentTarget { Appointment.IsLocked: false } target } && e.Key == Key.Enter)
        {
            RememberSelectionResource(target);
            SelectAppointment(target.Appointment, false);
            AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = target.Appointment });
            e.Handled = true;
        }
    }

    private void RememberSelectionResource(AppointmentTarget target)
    {
        if (target.ResourceId is { } resourceId)
            _selectionResourceIds[target.Appointment] = resourceId;
        else
            _selectionResourceIds.Remove(target.Appointment);
    }

    private void AttachEmptyDay(Control control, DateTime date, ResourceModel? resource)
    {
        control.Tag = new DayTarget(date, resource);
        control.PointerPressed += OnEmptyDayPressed;
    }

    private void OnEmptyDayPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsReadOnly || sender is not Control { Tag: DayTarget target } control || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            return;
        SelectSlot(target.Date, target.Resource, true, e.ClickCount, e.GetPosition(TopLevel.GetTopLevel(this)));
        e.Handled = true;
    }

    private void AttachLane(Lane lane)
    {
        lane.Surface.Tag = lane;
        lane.Surface.PointerPressed += OnLanePressed;
        DragDrop.SetAllowDrop(lane.Surface, true);
        lane.Surface.AddHandler(DragDrop.DragOverEvent, OnLaneDragOver);
        lane.Surface.AddHandler(DragDrop.DropEvent, OnLaneDrop);
    }

    private void OnLanePressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsReadOnly || sender is not Canvas { Tag: Lane lane } canvas || !e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            return;
        Focus(NavigationMethod.Pointer);
        BeginGesture(e, canvas, lane, GestureKind.Create, null);
        e.Handled = true;
    }

    private DateTime SlotTime(Lane lane, Point position)
    {
        var axis = lane.Horizontal ? position.X : position.Y;
        var limit = lane.DayCount * 1440;
        var minutes = Math.Clamp(Math.Floor(axis / lane.PixelsPerMinute / SnapMinutes) * SnapMinutes, 0, limit - SnapMinutes);
        return lane.Origin.AddMinutes(minutes);
    }

    private void BeginGesture(PointerPressedEventArgs e, Control capture, Lane lane, GestureKind kind, AppointmentModel? appointment, AppointmentModel? displayed = null)
    {
        CancelGesture();
        var slot = SlotTime(lane, e.GetPosition(lane.Surface));
        var start = displayed?.Start ?? (appointment is null ? slot : ShownTime(appointment.Start, lane));
        var end = displayed?.End ?? (appointment is null ? slot.AddMinutes(SnapMinutes) : ShownTime(appointment.End, lane));
        _gesture = new Gesture
        {
            Pointer = e.Pointer, Source = lane, Target = lane, PressPoint = e.GetPosition(this),
            PressTime = slot, Kind = kind, ClickCount = e.ClickCount, Appointment = appointment,
            OriginalStart = start, OriginalEnd = end, Start = start, End = end
        };
        e.Pointer.Capture(capture);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_gesture is not { } gesture || gesture.Pointer != e.Pointer || IsReadOnly)
            return;
        var distance = e.GetPosition(this) - gesture.PressPoint;
        if (!gesture.Started && distance.X * distance.X + distance.Y * distance.Y < 16)
            return;
        gesture.Started = true;
        var target = gesture.Source;
        if (gesture.Kind == GestureKind.Move)
            target = _lanes.FirstOrDefault(lane => lane.Horizontal == gesture.Source.Horizontal && new Rect(lane.Surface.Bounds.Size).Contains(e.GetPosition(lane.Surface))) ?? gesture.Source;
        var slot = SlotTime(target, e.GetPosition(target.Surface));
        var delta = slot - gesture.PressTime;
        gesture.Target = target;
        switch (gesture.Kind)
        {
            case GestureKind.Create:
                gesture.Start = slot < gesture.PressTime ? slot : gesture.PressTime;
                gesture.End = (slot > gesture.PressTime ? slot : gesture.PressTime).AddMinutes(SnapMinutes);
                break;
            case GestureKind.Move:
                gesture.Start = gesture.OriginalStart + delta;
                gesture.End = gesture.Start + (gesture.OriginalEnd - gesture.OriginalStart);
                break;
            case GestureKind.ResizeStart:
                gesture.Start = gesture.OriginalStart + delta;
                if (gesture.Start > gesture.End.AddMinutes(-SnapMinutes))
                    gesture.Start = gesture.End.AddMinutes(-SnapMinutes);
                break;
            case GestureKind.ResizeEnd:
                gesture.End = gesture.OriginalEnd + delta;
                if (gesture.End < gesture.Start.AddMinutes(SnapMinutes))
                    gesture.End = gesture.Start.AddMinutes(SnapMinutes);
                break;
        }
        ShowGhost(gesture);
        e.Handled = true;
    }

    private void ShowGhost(Gesture gesture)
    {
        var lane = gesture.Target ?? gesture.Source;
        if (gesture.Ghost is null)
        {
            gesture.Ghost = new Border
            {
                Background = Brush("DaisyBase200Brush"), BorderBrush = Brush("DaisyPrimaryBrush"),
                BorderThickness = new Thickness(SelectionThickness), CornerRadius = new CornerRadius(EventCornerRadius),
                Opacity = 0.85, IsHitTestVisible = false, ZIndex = 100
            };
        }
        if (!ReferenceEquals(gesture.Ghost.Parent, lane.Surface))
        {
            (gesture.Ghost.Parent as Panel)?.Children.Remove(gesture.Ghost);
            lane.Surface.Children.Add(gesture.Ghost);
        }
        gesture.Ghost.Child = Label(FormatTime(gesture.Start) + " – " + FormatTime(gesture.End));
        var begin = (gesture.Start - lane.Origin).TotalMinutes * lane.PixelsPerMinute;
        var length = Math.Max(ChipHeight, (gesture.End - gesture.Start).TotalMinutes * lane.PixelsPerMinute);
        gesture.Ghost.Width = lane.Horizontal ? length : lane.Surface.Width - 4;
        gesture.Ghost.Height = lane.Horizontal ? ChipHeight : length;
        Canvas.SetLeft(gesture.Ghost, lane.Horizontal ? begin : 2);
        Canvas.SetTop(gesture.Ghost, lane.Horizontal ? 4 : begin);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_gesture is not { } gesture || gesture.Pointer != e.Pointer)
            return;
        CancelGesture();
        if (IsReadOnly)
            return;
        e.Handled = true;
        var lane = gesture.Target ?? gesture.Source;
        if (!gesture.Started)
        {
            if (gesture.Kind == GestureKind.Create && TryStoredTime(gesture.PressTime, lane, out var slot))
                SelectSlot(slot, lane.Resource, false, gesture.ClickCount, e.GetPosition(TopLevel.GetTopLevel(this)));
            return;
        }
        if (!TryStoredTime(gesture.Start, lane, out var start) || !TryStoredTime(gesture.End, lane, out var end) || end <= start)
            return;
        if (gesture.Kind == GestureKind.Create)
            AppointmentCreateRequested?.Invoke(this, new AppointmentCreateArgs { Start = start, End = end, ResourceId = lane.Resource?.Id, Position = e.GetPosition(TopLevel.GetTopLevel(this)) });
        else if (gesture.Appointment is { } appointment)
        {
            if (appointment.SpanMode == AppointmentSpanMode.Daily)
            {
                if (!TryStoredTime(gesture.OriginalStart, gesture.Source, out var originalStart) ||
                    !TryStoredTime(gesture.OriginalEnd, gesture.Source, out var originalEnd))
                    return;
                start = appointment.Start + (start - originalStart);
                end = appointment.End + (end - originalEnd);
            }
            RaiseEditGesture(gesture, appointment, lane, start, end, e.KeyModifiers);
        }
    }

    private void RaiseEditGesture(Gesture gesture, AppointmentModel appointment, Lane lane, DateTime start, DateTime end, KeyModifiers modifiers)
    {
        if (!IsValidGestureRange(appointment, start, end, gesture.Kind != GestureKind.Move))
            return;
        var withShift = modifiers.HasFlag(KeyModifiers.Shift);
        var copy = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var multi = !lane.Horizontal && _selection.Count > 1;
        if (gesture.Kind == GestureKind.Move)
        {
            var delta = start - appointment.Start;
            if (multi)
            {
                var items = _selection.Where(static selected => !selected.IsLocked)
                    .Select(selected => GroupMoveItem(selected, gesture.Source, lane, delta)).ToArray();
                if (items.All(item => IsValidGestureRange(item.Appointment, item.NewStart, item.NewEnd, false)))
                    AppointmentsMoveRequested?.Invoke(this, new AppointmentsMoveArgs { Items = items, IsCopy = copy, WithShift = withShift });
            }
            else
                AppointmentMoveRequested?.Invoke(this, new AppointmentMoveArgs
                {
                    Appointment = appointment, NewStart = start, NewEnd = end,
                    FromResourceId = gesture.Source.Resource?.Id, ToResourceId = lane.Resource?.Id, IsCopy = copy, WithShift = withShift
                });
        }
        else if (multi)
        {
            var startDelta = start - appointment.Start;
            var endDelta = end - appointment.End;
            var items = _selection.Where(static selected => !selected.IsLocked).Select(selected => new AppointmentResizeItem
            {
                Appointment = selected,
                NewStart = selected.Start + startDelta,
                NewEnd = selected.End + endDelta
            }).ToArray();
            if (items.All(item => IsValidGestureRange(item.Appointment, item.NewStart, item.NewEnd, true)))
                AppointmentsResizeRequested?.Invoke(this, new AppointmentsResizeArgs { Items = items, WithShift = withShift });
        }
        else
            AppointmentResizeRequested?.Invoke(this, new AppointmentResizeArgs { Appointment = appointment, NewStart = start, NewEnd = end, WithShift = withShift });
    }

    private bool IsValidGestureRange(AppointmentModel appointment, DateTime start, DateTime end, bool resizing)
    {
        var duration = appointment is { SpanMode: AppointmentSpanMode.Daily, IsAllDay: false }
            ? end.TimeOfDay - start.TimeOfDay : end - start;
        return end > start && duration > TimeSpan.Zero &&
            (!resizing || duration >= TimeSpan.FromMinutes(SnapMinutes));
    }

    private AppointmentMoveItem GroupMoveItem(AppointmentModel appointment, Lane source, Lane target, TimeSpan delta)
    {
        var resources = _lanes.Select(static lane => lane.Resource).OfType<ResourceModel>()
            .DistinctBy(static resource => resource.Id).ToArray();
        var sourceIndex = Array.FindIndex(resources, resource => Equals(resource.Id, source.Resource?.Id));
        var targetIndex = Array.FindIndex(resources, resource => Equals(resource.Id, target.Resource?.Id));
        var itemIndex = _selectionResourceIds.TryGetValue(appointment, out var selectedResourceId)
            ? Array.FindIndex(resources, resource => Equals(resource.Id, selectedResourceId) && appointment.ResourceIds.Contains(resource.Id))
            : -1;
        if (itemIndex < 0)
            itemIndex = Array.FindIndex(resources, resource => appointment.ResourceIds.Contains(resource.Id));
        var movedIndex = itemIndex + targetIndex - sourceIndex;
        var from = itemIndex >= 0 ? resources[itemIndex].Id : null;
        var to = itemIndex >= 0 && sourceIndex >= 0 && targetIndex >= 0 && movedIndex >= 0 && movedIndex < resources.Length ? resources[movedIndex].Id : from;
        return new AppointmentMoveItem
        {
            Appointment = appointment, NewStart = appointment.Start + delta, NewEnd = appointment.End + delta,
            FromResourceId = from, ToResourceId = to
        };
    }

    private DateTime ShownTime(DateTime time, Lane lane) => lane.Horizontal ? TimeZoneConverter.ToZone(time, lane.Resource?.TimeZoneId) : time;
    private static bool TryStoredTime(DateTime time, Lane lane, out DateTime stored)
    {
        stored = time;
        return !lane.Horizontal || TimeZoneConverter.TryFromZone(time, lane.Resource?.TimeZoneId, out stored);
    }

    private void SelectSlot(DateTime start, ResourceModel? resource, bool allDay, int clicks, Point position)
    {
        _selection.Clear();
        CommitSelection();
        var slot = new EmptySlotSelectedArgs { Start = start, ResourceId = resource?.Id, IsAllDay = allDay, Position = position };
        EmptySlotSelected?.Invoke(this, slot);
        if (SingleClickCreates ? clicks == 1 : clicks == 2)
            AppointmentCreateRequested?.Invoke(this, new AppointmentCreateArgs
            {
                Start = start, End = allDay ? start.AddDays(1) : start.AddHours(1),
                ResourceId = resource?.Id, IsAllDay = allDay, Position = position
            });
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (_gesture?.Pointer == e.Pointer)
            CancelGesture();
        base.OnPointerCaptureLost(e);
    }

    private void CancelGesture()
    {
        var gesture = _gesture;
        _gesture = null;
        if (gesture?.Ghost?.Parent is Panel panel)
            panel.Children.Remove(gesture.Ghost);
        gesture?.Pointer.Capture(null);
    }

    private void OnLaneDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = !IsReadOnly && sender is Canvas { Tag: Lane lane } &&
            e.DataTransfer.TryGetValue(ExternalDropHandler.Format) is not null &&
            TryStoredTime(SlotTime(lane, e.GetPosition(lane.Surface)), lane, out _)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnLaneDrop(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || sender is not Canvas { Tag: Lane lane } || e.DataTransfer.TryGetValue(ExternalDropHandler.Format) is not { } item)
            return;
        e.Handled = true;
        if (!TryStoredTime(SlotTime(lane, e.GetPosition(lane.Surface)), lane, out var start))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        ExternalAppointmentDropped?.Invoke(this, new ExternalDropArgs { Item = item, Start = start, End = start + item.DefaultDuration, ResourceId = lane.Resource?.Id });
    }
}
