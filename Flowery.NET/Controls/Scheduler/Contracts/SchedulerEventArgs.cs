// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System;
using System.Collections.Generic;
using Avalonia;
using Flowery.Controls.Scheduler.Models;

namespace Flowery.Controls.Scheduler.Contracts;

/// <summary>
/// Raised when the user drags an appointment to a new time and/or resource. The control does
/// not mutate the appointment — the host applies the change and refreshes if it accepts it.
/// </summary>
public sealed class AppointmentMoveArgs : EventArgs
{
    /// <summary>The host's own appointment instance (from <c>DaisyScheduler.Appointments</c>), in every view.</summary>
    public required AppointmentModel Appointment { get; init; }

    /// <summary>Proposed new start, on the same basis as <see cref="AppointmentModel.Start"/>: wall-clock,
    /// or UTC when the target is a Planner lane whose resource sets <see cref="ResourceModel.TimeZoneId"/>.</summary>
    public required DateTime NewStart { get; init; }

    /// <summary>Proposed new end, preserving the original duration.</summary>
    public required DateTime NewEnd { get; init; }

    /// <summary>The resource lane the drag started in (null in non-resource views).</summary>
    public object? FromResourceId { get; init; }

    /// <summary>The resource lane the drag ended in (equals <see cref="FromResourceId"/> for an in-lane move).</summary>
    public object? ToResourceId { get; init; }

    /// <summary>True when Ctrl was held (copy drag): the host should duplicate the appointment at the
    /// new position and leave the original in place, rather than relocating it.</summary>
    public bool IsCopy { get; init; }

    /// <summary>True when Shift was held at drop. The control ascribes no meaning to it; a host may use
    /// it to skip a confirmation step and commit the change immediately.</summary>
    public bool WithShift { get; init; }
}

/// <summary>Raised when the user drags an appointment's top or bottom edge (left or right edge in the
/// Planner) to change its duration. Times are on the same basis as <see cref="AppointmentModel.Start"/>.</summary>
public sealed class AppointmentResizeArgs : EventArgs
{
    /// <summary>The host's own appointment instance (from <c>DaisyScheduler.Appointments</c>), in every view.</summary>
    public required AppointmentModel Appointment { get; init; }

    /// <summary>Proposed new start (changed when the top edge is dragged).</summary>
    public required DateTime NewStart { get; init; }

    /// <summary>Proposed new end (changed when the bottom edge is dragged).</summary>
    public required DateTime NewEnd { get; init; }

    /// <summary>True when Shift was held at drop. The control ascribes no meaning to it; a host may use
    /// it to skip a confirmation step and commit the change immediately.</summary>
    public bool WithShift { get; init; }
}

/// <summary>One appointment's resolved target in a multi-selection move (see <see cref="AppointmentsMoveArgs"/>).
/// Each item may move between a different resource lane while the group shares one (time, lane) delta.</summary>
public sealed class AppointmentMoveItem
{
    public required AppointmentModel Appointment { get; init; }
    public required DateTime NewStart { get; init; }
    public required DateTime NewEnd { get; init; }
    public object? FromResourceId { get; init; }
    public object? ToResourceId { get; init; }
}

/// <summary>Raised when the user drags a multi-selection: every selected item lands at its own position
/// plus the shared (time, lane) delta. The host applies the per-item resource change and persists atomically.</summary>
public sealed class AppointmentsMoveArgs : EventArgs
{
    public required IReadOnlyList<AppointmentMoveItem> Items { get; init; }

    /// <summary>True when Ctrl was held (copy drag): the host should duplicate each appointment at its
    /// new position and leave the originals in place, rather than relocating them.</summary>
    public bool IsCopy { get; init; }

    /// <summary>True when Shift was held at drop. The control ascribes no meaning to it; a host may use
    /// it to skip a confirmation step and commit the change immediately.</summary>
    public bool WithShift { get; init; }
}

/// <summary>One appointment's resolved target in a multi-selection resize (see <see cref="AppointmentsResizeArgs"/>).</summary>
public sealed class AppointmentResizeItem
{
    public required AppointmentModel Appointment { get; init; }
    public required DateTime NewStart { get; init; }
    public required DateTime NewEnd { get; init; }
}

/// <summary>Raised when the user resizes a multi-selection: the same edge-delta is applied to every
/// selected appointment.</summary>
public sealed class AppointmentsResizeArgs : EventArgs
{
    public required IReadOnlyList<AppointmentResizeItem> Items { get; init; }

    /// <summary>True when Shift was held at drop. The control ascribes no meaning to it; a host may use
    /// it to skip a confirmation step and commit the change immediately.</summary>
    public bool WithShift { get; init; }
}

/// <summary>Raised when the user deletes the current (single or multi) selection.</summary>
public sealed class AppointmentsDeleteArgs : EventArgs
{
    public required IReadOnlyList<AppointmentModel> Appointments { get; init; }

    /// <summary>True when Shift was held during the delete. The control ascribes no meaning to it; a host
    /// may use it to skip its delete-confirmation prompt (mirrors <see cref="AppointmentsMoveArgs.WithShift"/>).</summary>
    public bool WithShift { get; init; }
}

/// <summary>Raised when an external <see cref="ISchedulerDroppable"/> item is dropped on a time slot.</summary>
public sealed class ExternalDropArgs : EventArgs
{
    public required ISchedulerDroppable Item { get; init; }

    /// <summary>The snapped slot start the item was dropped on.</summary>
    public required DateTime Start { get; init; }

    /// <summary>Slot start + the item's <see cref="ISchedulerDroppable.DefaultDuration"/>.</summary>
    public required DateTime End { get; init; }

    /// <summary>The resource lane the slot belongs to (null in non-resource views).</summary>
    public object? ResourceId { get; init; }
}

/// <summary>
/// Raised when the user double-clicks (double-taps) a non-locked appointment, in any view, to ask to edit
/// it. The control selects the appointment first. It ships no editor: the host shows its own, and a
/// request no handler acts on does nothing.
/// </summary>
public sealed class AppointmentEditRequestedArgs : EventArgs
{
    /// <summary>The host's own appointment instance (from <c>DaisyScheduler.Appointments</c>).</summary>
    public required AppointmentModel Appointment { get; init; }

    /// <summary>Not read by the control. Lets several subscribers coordinate: a handler that opened an
    /// editor sets it so later handlers can skip the request.</summary>
    public bool Handled { get; set; }
}

/// <summary>A click/tap on an appointment, as a view reports it to <c>DaisyScheduler</c>.
/// <see cref="WithCtrl"/> is true when the Ctrl/Cmd modifier was held (multi-select toggle); a plain click
/// single-selects, and a second click on the same appointment within the double-click window is an edit
/// request. <c>DaisyScheduler</c> raises no event with this type.</summary>
public sealed class AppointmentClickArgs : EventArgs
{
    public required AppointmentModel Appointment { get; init; }

    /// <summary>True when Ctrl/Cmd was held — toggle this appointment in/out of the selection set.</summary>
    public bool WithCtrl { get; init; }
}

/// <summary>
/// Raised when the user right-clicks (or otherwise requests a context menu on) an appointment. The
/// control selects the appointment (visual) and reports it here; the host decides what menu to show —
/// the library is domain-agnostic and builds none itself. Anchor a host menu at <see cref="Position"/>.
/// </summary>
public sealed class AppointmentContextRequestedArgs : EventArgs
{
    /// <summary>The host's own appointment instance (from <c>DaisyScheduler.Appointments</c>).</summary>
    public required AppointmentModel Appointment { get; init; }

    /// <summary>The click position, relative to the <see cref="Avalonia.Controls.TopLevel"/> content, so the
    /// host can anchor its context menu at the click. Null when unknown (e.g. the overlay isn't yet in a
    /// live tree). Transform it into the target's space before showing.</summary>
    public Point? Position { get; init; }
}

/// <summary>Raised when the user double-clicks or drag-selects an empty slot to create an appointment.</summary>
public sealed class AppointmentCreateArgs : EventArgs
{
    public required DateTime Start { get; init; }

    public required DateTime End { get; init; }

    /// <summary>The resource lane the slot belongs to (null in non-resource views).</summary>
    public object? ResourceId { get; init; }

    /// <summary>True when the create came from the all-day row (Day/Week) or a Month day cell. The range then
    /// covers whole days: <see cref="Start"/> is midnight and <see cref="End"/> is midnight of the next day.</summary>
    public bool IsAllDay { get; init; }

    /// <summary>The click position, relative to the <see cref="Avalonia.Controls.TopLevel"/> content, so the
    /// host can anchor a popup (e.g. an event-type picker) at the click. Null when unknown (e.g. a
    /// synthesized create). Transform it into the target's space before showing.</summary>
    public Point? Position { get; init; }
}

/// <summary>
/// Raised when the user single-clicks an empty slot: a Day/Week time slot or all-day cell, a Planner lane,
/// or a Month day cell. A single click does not create; it reports the clicked lane and time so the host can
/// track a "selected resource" for its new-appointment panel. A double-click raises
/// <c>DaisyScheduler.AppointmentCreateRequested</c> instead (or the single click does, when
/// <c>DaisyScheduler.SingleClickCreates</c> is true).
/// </summary>
public sealed class EmptySlotSelectedArgs : EventArgs
{
    /// <summary>The snapped slot start the user clicked (midnight of the day for an all-day or Month cell).</summary>
    public required DateTime Start { get; init; }

    /// <summary>The resource lane the slot belongs to (null in non-resource views).</summary>
    public object? ResourceId { get; init; }

    /// <summary>True when the click was on the all-day row (Day/Week) or a Month day cell.</summary>
    public bool IsAllDay { get; init; }

    /// <summary>The click position, relative to the <see cref="Avalonia.Controls.TopLevel"/> content (so the
    /// host can anchor a popup at the click). Null when unknown.</summary>
    public Point? Position { get; init; }
}
