// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System;
using System.Collections.Generic;
using Avalonia.Media;
namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// A single schedulable item rendered by the <c>DaisyScheduler</c>. This is the
/// control's own domain-agnostic model — it has no knowledge of any host application's
/// data types. Hosts map their own entities onto this model.
/// </summary>
/// <remarks>
/// <para>
/// <b>Time contract.</b> <see cref="Start"/> / <see cref="End"/> are <em>wall-clock</em> times, drawn
/// exactly as given: Day, Week, Month, Agenda and every Planner or Timeline lane without a time zone apply no
/// conversion, and the times they report back in event args are on the same basis. Pass the times you
/// want the viewer to see (typically the viewer's local time).
/// </para>
/// <para>
/// A Planner or Timeline lane whose resource sets <see cref="ResourceModel.TimeZoneId"/> reads
/// <see cref="Start"/> / <see cref="End"/> as <em>UTC instants</em>, draws them in the
/// resource's zone, and converts gesture times back to UTC. Occupancy queries use this same UTC
/// convention and evaluate their boundaries in the resource's local time. Only use
/// <see cref="ResourceModel.TimeZoneId"/> when the host stores UTC for that resource's appointments.
/// The CLR <see cref="DateTime.Kind"/> is never inspected.
/// </para>
/// <para>
/// All-day events (<see cref="IsAllDay"/>) are floating: their date components are used as-is and they
/// are never shifted, in any view.
/// </para>
/// </remarks>
public sealed class AppointmentModel
{
    /// <summary>
    /// Host-defined identity. Any value type works (int, Guid, string); the control only
    /// uses it for equality/lookup via <see cref="object.Equals(object?)"/>. May be null
    /// for not-yet-persisted appointments (e.g. a drag-create ghost).
    /// </summary>
    public object? Id { get; set; }

    /// <summary>Title shown on the event block.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Optional longer text (tooltip / detail row).</summary>
    public string? Description { get; set; }

    /// <summary>Start, as a wall-clock time (UTC only in a zoned Planner or Timeline lane). See the remarks on <see cref="AppointmentModel"/>.</summary>
    public DateTime Start { get; set; }

    /// <summary>End, on the same basis as <see cref="Start"/>. Must be &gt;= <see cref="Start"/>.</summary>
    public DateTime End { get; set; }

    /// <summary>When true the event renders in the all-day banner and is not timezone-shifted.</summary>
    public bool IsAllDay { get; set; }

    /// <summary>Continuous interval or daily time window across the Start/End dates. Daily uses the stored clock times.</summary>
    public AppointmentSpanMode SpanMode { get; set; }

    /// <summary>
    /// Resource ids this appointment belongs to. An event spanning multiple resources
    /// renders once per matching resource lane (Planner or Timeline) / column. Compared against
    /// <see cref="ResourceModel.Id"/> by value equality. Empty = unassigned.
    /// </summary>
    public IList<object> ResourceIds { get; set; } = new List<object>();

    /// <summary>Accent/stripe color for the event block (e.g. a category color). Null = theme default.</summary>
    public Color? BarColor { get; set; }

    /// <summary>Fill/background color for the event block. Null = theme default.</summary>
    public Color? BackColor { get; set; }

    /// <summary>
    /// Optional recurrence rule (RFC 5545 RRULE or host-defined). When non-null the block
    /// shows a recurrence indicator. Expansion of occurrences is a host/view concern.
    /// </summary>
    public string? RecurrenceRule { get; set; }

    /// <summary>Optional recurrence exception dates (host-defined serialization).</summary>
    public string? RecurrenceException { get; set; }

    /// <summary>True for daily spans or appointments with a recurrence rule.</summary>
    public bool IsRecurring => SpanMode == AppointmentSpanMode.Daily || !string.IsNullOrWhiteSpace(RecurrenceRule);

    /// <summary>Duration of the appointment.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Locked appointments show a lock glyph in the default template (host-defined meaning).</summary>
    public bool IsLocked { get; set; }

    /// <summary>Completed appointments show a check glyph in the default template.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Canceled appointments show a cancel glyph in the default template.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Tentative appointments show a question-mark glyph in the default template.</summary>
    public bool IsTentative { get; set; }

    /// <summary>Confirmed appointments show a confirmed glyph in the default template.</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// Extra host-defined status glyphs rendered alongside the standard ones (in the default
    /// template's indicator strip, and available to custom templates via the appointment context).
    /// </summary>
    public IList<AppointmentIndicator> Indicators { get; set; } = new List<AppointmentIndicator>();

    /// <summary>
    /// Host-defined labelled detail rows shown in the hover popup (e.g. participant roles).
    /// The control is domain-agnostic — it just renders them. Empty = none shown.
    /// </summary>
    public IList<AppointmentDetail> Details { get; set; } = new List<AppointmentDetail>();

    /// <summary>
    /// User/template-defined badges decorating the block (the host's "adornment" feature) — small
    /// positioned text/glyph/image tiles. Rendered natively by the default templates at each
    /// decoration's <see cref="AppointmentDecoration.Position"/>. Empty = none.
    /// </summary>
    public IList<AppointmentDecoration> Adornments { get; set; } = new List<AppointmentDecoration>();

    /// <summary>
    /// Data-driven content tiles decorating the block (the host's "tile" feature — e.g. report fields).
    /// Structurally identical to <see cref="Adornments"/>; kept as a distinct collection so the two host
    /// features stay separable. Rendered natively by the default templates. Empty = none.
    /// </summary>
    public IList<AppointmentDecoration> Tiles { get; set; } = new List<AppointmentDecoration>();

    /// <summary>
    /// Optional secondary/internal note shown in the hover popup below the main <see cref="Description"/>
    /// note (host-defined meaning, e.g. an internal memo). Null/blank = not shown.
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// Opaque host payload (the originating entity, a view-model, etc.). The control never reads
    /// it; it's carried through so custom templates / renderers can bind to app-specific data.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// A copy of this appointment with every property carried over. The collections
    /// (<see cref="ResourceIds"/>, <see cref="Indicators"/>, <see cref="Details"/>, <see cref="Adornments"/>,
    /// <see cref="Tiles"/>) are new lists holding the same items, so adding to or removing from the copy's
    /// lists leaves this instance alone; the items themselves and <see cref="Tag"/> are shared.
    /// </summary>
    public AppointmentModel Clone()
    {
        var copy = (AppointmentModel)MemberwiseClone();
        copy.ResourceIds = new List<object>(ResourceIds);
        copy.Indicators = new List<AppointmentIndicator>(Indicators);
        copy.Details = new List<AppointmentDetail>(Details);
        copy.Adornments = new List<AppointmentDecoration>(Adornments);
        copy.Tiles = new List<AppointmentDecoration>(Tiles);
        return copy;
    }
}
