// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System.Collections.Generic;
using System.Windows.Input;

namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// A host-supplied custom button for the <c>DaisyScheduler</c> toolbar. The generic control knows
/// nothing about what the button does — it renders the glyph/label, applies the tooltip, and on click
/// runs <see cref="Command"/> (if set) and raises <c>DaisyScheduler.ToolbarButtonClicked</c>. This
/// is the extension point that lets an app graft features (e.g. "open the map") onto the bar without
/// the library taking a dependency on them.
/// </summary>
public sealed class SchedulerToolbarButton
{
    /// <summary>Stable identifier the host can switch on in the click handler. Not shown to the user.</summary>
    public string? Id { get; init; }

    /// <summary>Optional icon glyph from the symbol theme font (e.g. ""). Rendered as a FontIcon.</summary>
    public string? Glyph { get; init; }

    /// <summary>Optional raw image bytes (PNG/JPG/…) rendered as the button icon. Takes precedence over
    /// <see cref="Glyph"/> — e.g. an adornment template's image.</summary>
    public byte[]? Image { get; init; }

    /// <summary>Optional caption shown beside the glyph (or alone, when no glyph is set).</summary>
    public string? Label { get; init; }

    /// <summary>Hover tooltip. Recommended when the button is glyph-only.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Optional command invoked on click, with <see cref="CommandParameter"/>. The
    /// <c>ToolbarButtonClicked</c> event fires regardless, so a code-behind host can skip this.</summary>
    public ICommand? Command { get; init; }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get; init; }

    /// <summary>Whether the rendered button is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Render as a toggle: <see cref="IsChecked"/> shows it pressed (accent fill). Clicking still
    /// raises the click event — the host owns the state and re-supplies the button with the new value.</summary>
    public bool IsToggle { get; init; }

    /// <summary>The pressed state of an <see cref="IsToggle"/> button.</summary>
    public bool IsChecked { get; init; }

    /// <summary>
    /// When set, the button opens a menu of these entries instead of acting itself, and picking one raises
    /// <c>ToolbarButtonClicked</c> (and runs its <see cref="Command"/>) with THAT entry. Each entry uses
    /// <see cref="Id"/>, <see cref="Label"/>, <see cref="Glyph"/> and <see cref="IsEnabled"/>. In the overflow
    /// menu the button becomes a submenu of the same entries.
    /// </summary>
    public IReadOnlyList<SchedulerToolbarButton>? MenuItems { get; init; }
}
