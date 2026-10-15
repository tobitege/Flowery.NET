// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System.Collections.Generic;
using System.Linq;
using Flowery.Controls.Scheduler.Models;

namespace Flowery.Controls.Scheduler.Rendering;

/// <summary>Which edge of a block a group of tiles sits on.</summary>
internal enum TileSide
{
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>
/// Where data tiles (<see cref="AppointmentModel.Tiles"/>) go on a block. Tiles are laid out in flowing
/// strips rather than pinned like adornments, so they never cover the subject:
/// <list type="bullet">
/// <item><see cref="DecorationPosition.Left"/> / <see cref="DecorationPosition.FloatLeft"/>: a strip at the
/// left, beside the status icons.</item>
/// <item><see cref="DecorationPosition.Right"/> / <see cref="DecorationPosition.FloatRight"/>: a strip at the right.</item>
/// <item><see cref="DecorationPosition.Top"/>: a row above the subject (timed blocks only).</item>
/// <item><see cref="DecorationPosition.Bottom"/> / <see cref="DecorationPosition.FloatCenter"/>: a row below the
/// subject and note (timed blocks only).</item>
/// </list>
/// Single-line surfaces (chips and Planner bars) have no room above or below the text, so Top and Bottom
/// tiles join the right-hand strip there. <see cref="AppointmentDecoration.Alignment"/> places a tile along
/// its strip (Near = top / left, Middle = center, Far = bottom / right) and the pixel offsets nudge it.
/// </summary>
internal static class TileLayout
{
    /// <summary>The side a tile with <paramref name="position"/> sits on; <paramref name="multiLine"/> is
    /// true for timed (Day/Week) blocks.</summary>
    public static TileSide SideOf(DecorationPosition position, bool multiLine) => position switch
    {
        DecorationPosition.Left or DecorationPosition.FloatLeft => TileSide.Left,
        DecorationPosition.Top => multiLine ? TileSide.Top : TileSide.Right,
        DecorationPosition.Bottom or DecorationPosition.FloatCenter => multiLine ? TileSide.Bottom : TileSide.Right,
        _ => TileSide.Right,
    };

    /// <summary>True when the strip on <paramref name="side"/> stacks vertically (the timed block's gutters).</summary>
    public static bool IsVertical(TileSide side, bool multiLine) =>
        multiLine && side is TileSide.Left or TileSide.Right;

    /// <summary>The tiles on <paramref name="side"/>, grouped by alignment in Near, Middle, Far order, each
    /// group keeping the host's order.</summary>
    public static IReadOnlyList<(DecorationAlignment Alignment, IReadOnlyList<AppointmentDecoration> Tiles)> Groups(
        IEnumerable<AppointmentDecoration> tiles, TileSide side, bool multiLine) =>
        tiles.Where(t => SideOf(t.Position, multiLine) == side)
            .GroupBy(t => t.Alignment)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, (IReadOnlyList<AppointmentDecoration>)g.ToList()))
            .ToList();
}
