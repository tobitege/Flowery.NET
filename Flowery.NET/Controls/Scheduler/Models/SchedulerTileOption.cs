// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// A host-supplied on/off entry in the toolbar's "Visible tiles" menu (see <c>DaisyScheduler.SetTileOptions</c>):
/// the generic hook that lets an app expose which data tiles are available and let the user toggle each. The
/// library knows nothing about what a tile means; it renders the <see cref="Label"/> and reports toggles by
/// <see cref="Key"/> (<c>DaisyScheduler.TileOptionToggled</c>). It does not filter tiles itself.
/// </summary>
public sealed class SchedulerTileOption
{
    /// <summary>Stable identifier the host switches on when a toggle changes (e.g. "customer-balance").</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Display label for the toggle.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Whether the tile is currently enabled (drives the menu entry's check mark).</summary>
    public bool IsOn { get; init; }
}
