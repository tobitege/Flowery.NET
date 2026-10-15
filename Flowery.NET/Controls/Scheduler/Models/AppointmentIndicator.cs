// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using Avalonia.Media;

namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// A small status glyph shown on an appointment block (e.g. an attachment, priority or sync icon).
/// The default templates render these in the block's indicator strip; custom templates can bind to
/// <see cref="AppointmentModel.Indicators"/> and lay them out however they like.
/// </summary>
public sealed class AppointmentIndicator
{
    /// <summary>The icon glyph, e.g. <c>""</c> (a Segoe Fluent / MDL2 codepoint).</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <summary>Glyph color. Null = inherit the block's auto-contrast foreground.</summary>
    public Color? Color { get; set; }

    /// <summary>Optional tooltip shown on hover.</summary>
    public string? Tooltip { get; set; }

    /// <summary>Glyph font. Null = the symbol theme font (Segoe Fluent Icons / MDL2 Assets).</summary>
    public FontFamily? FontFamily { get; set; }
}
