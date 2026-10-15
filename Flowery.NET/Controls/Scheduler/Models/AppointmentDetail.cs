// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// A labelled detail row shown in the hover popup (e.g. a participant role: label "Technician",
/// value "John Doe"). Host-defined and domain-agnostic: the control just renders the rows.
/// Bind to <see cref="AppointmentModel.Details"/> from a custom template, or let the default
/// popup render them.
/// </summary>
public sealed class AppointmentDetail
{
    public AppointmentDetail() { }

    public AppointmentDetail(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>The row's caption, e.g. "Contact".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The row's value, e.g. the participant's name.</summary>
    public string Value { get; set; } = string.Empty;
}
