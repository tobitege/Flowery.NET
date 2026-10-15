// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using Avalonia.Media;

namespace Flowery.Controls.Scheduler.Models;

/// <summary>Where a decoration sits on the appointment block.</summary>
/// <remarks>
/// <para><b>Adornments</b> are pinned overlays: every value anchors the badge's center on that side (or
/// corner, with <see cref="DecorationAlignment"/>) and it may overhang the block. The edge values and the
/// <c>Float*</c> values render the same way; neither reserves space.</para>
/// <para><b>Tiles</b> flow in strips that never cover the subject: <see cref="Left"/>/<see cref="FloatLeft"/>
/// = a strip at the left, <see cref="Right"/>/<see cref="FloatRight"/> = a strip at the right,
/// <see cref="Top"/> = a row above the subject, <see cref="Bottom"/>/<see cref="FloatCenter"/> = a row below
/// it. Chips and Planner bars are one line high, so Top and Bottom tiles join the right strip there.</para>
/// </remarks>
public enum DecorationPosition
{
    Left,
    Top,
    Right,
    Bottom,
    FloatLeft,
    FloatCenter,
    FloatRight,
}

/// <summary>Alignment of a decoration along its edge or strip (near = top/left, far = bottom/right).</summary>
public enum DecorationAlignment
{
    Near,
    Middle,
    Far,
}

/// <summary>
/// A small badge on an appointment block — the domain-agnostic primitive behind the host's <c>Tile</c> and
/// <c>Adornment</c> features. A decoration shows its <see cref="Image"/> when set; otherwise a pill with its
/// <see cref="Glyph"/> and/or <see cref="Text"/> side by side. Every member applies to both
/// <see cref="AppointmentModel.Adornments"/> (pinned overlays) and <see cref="AppointmentModel.Tiles"/>
/// (flowing strips); <see cref="Position"/> and <see cref="Alignment"/> describe where each kind goes (see
/// <see cref="DecorationPosition"/>). Custom templates can lay them out however they like via the
/// appointment context.
/// </summary>
public sealed class AppointmentDecoration
{
    /// <summary>Text label (shown after the <see cref="Glyph"/> when both are set; ignored when an <see cref="Image"/> is set).</summary>
    public string? Text { get; set; }

    /// <summary>Icon-font glyph (a Segoe Fluent / MDL2 codepoint), shown before the <see cref="Text"/>. Ignored when an <see cref="Image"/> is set.</summary>
    public string? Glyph { get; set; }

    /// <summary>Glyph font. Null = the symbol theme font (Segoe Fluent Icons / MDL2 Assets).</summary>
    public FontFamily? GlyphFontFamily { get; set; }

    /// <summary>Raw image bytes (PNG/JPG/…). Takes precedence over <see cref="Glyph"/>/<see cref="Text"/>.</summary>
    public byte[]? Image { get; set; }

    /// <summary>Where the decoration sits on the block: an adornment's anchor, or a tile's strip (see
    /// <see cref="DecorationPosition"/>). Defaults to <see cref="DecorationPosition.FloatLeft"/>: the top-left
    /// corner for an adornment, the left strip for a tile.</summary>
    public DecorationPosition Position { get; set; } = DecorationPosition.FloatLeft;

    /// <summary>Alignment along the anchored edge (adornment) or along the strip (tile).</summary>
    public DecorationAlignment Alignment { get; set; } = DecorationAlignment.Near;

    /// <summary>Horizontal pixel nudge from the anchor / strip slot (positive = right).</summary>
    public int OffsetX { get; set; }

    /// <summary>Vertical pixel nudge from the anchor / strip slot (positive = down).</summary>
    public int OffsetY { get; set; }

    /// <summary>Pill fill. Null = transparent for an adornment, the neutral gray pill for a tile.</summary>
    public Color? BackColor { get; set; }

    /// <summary>Text/glyph color. Null selects readable text for the badge fill; transparent adornments use the appointment foreground.</summary>
    public Color? ForeColor { get; set; }

    /// <summary>Pill border color. Null = no border.</summary>
    public Color? BorderColor { get; set; }

    /// <summary>Optional explicit font size in DIPs. Null uses the scheduler's secondary design token.</summary>
    public double? FontSize { get; set; }

    /// <summary>Whether the text renders bold.</summary>
    public bool Bold { get; set; }

    /// <summary>Optional tooltip shown on hover.</summary>
    public string? Tooltip { get; set; }
}
