// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System.Collections.Generic;
using Avalonia.Media;
namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// A schedulable resource — a lane in Planner or Timeline and a grouping key for
/// appointments. Domain-agnostic: a host maps its own people / rooms / equipment onto this.
/// </summary>
public sealed class ResourceModel
{
    /// <summary>
    /// Host-defined identity, matched against <see cref="AppointmentModel.ResourceIds"/> by
    /// value equality. Any value type works (int, Guid, string). Ids must be unique across all kinds.
    /// </summary>
    public object Id { get; set; } = 0;

    /// <summary>Display name for the resource header.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Resource category used by occupancy queries. Defaults to <see cref="ResourceKind.Unspecified"/>.</summary>
    public ResourceKind Kind { get; set; }

    /// <summary>Host-defined grouping criteria, for example { "Department": "Development" }.</summary>
    public IDictionary<string, object> GroupValues { get; set; } = new Dictionary<string, object>();

    /// <summary>Lane accent color (header chip + default event color). Null = theme default.</summary>
    public Color? Color { get; set; }

    /// <summary>
    /// IANA time zone id (e.g. <c>"America/New_York"</c>). Planner and Timeline read timed appointments as UTC,
    /// draws them in this zone, and reports gesture times back as UTC. Occupancy queries interpret
    /// their date boundaries in this zone. Null (the default) uses the times as given.
    /// All-day appointments use their dates without conversion. See <see cref="AppointmentModel"/>.
    /// </summary>
    public string? TimeZoneId { get; set; }
}
