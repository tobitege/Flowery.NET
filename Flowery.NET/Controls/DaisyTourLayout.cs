using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Controls;

internal static class DaisyTourLayout
{
    /// <summary>
    /// The part of the window content that its display's work area shows, in content DIP. A window that
    /// extends below the taskbar or off the screen must not receive the card or annotations there.
    /// </summary>
    internal static Rect VisibleArea(TopLevel root) =>
        VisibleArea(new Rect(0, 0, root.ClientSize.Width, root.ClientSize.Height), root as Window,
            root.InsetsManager?.SafeAreaPadding ?? default);

    /// <param name="content">The content of the view in DIP.</param>
    /// <param name="window">The window that shows the view, or null for a view without one.</param>
    /// <param name="safeArea">The safe-area padding of a view without a window in DIP; a window does not use it.</param>
    internal static Rect VisibleArea(Rect content, Window? window, Thickness safeArea)
    {
        // A view without a window, such as on a phone, can reach under the system bars and display cutouts.
        if (window is null)
            return SafeArea(content, safeArea);
        if (window.Screens.ScreenFromWindow(window) is not { } display)
            return content;
        // Screen positions and the working area are in desktop pixels: physical pixels on Windows, points on macOS.
        var origin = window.PointToScreen(new Point(0, 0));
        return VisibleArea(content, origin.X, origin.Y, display.WorkingArea, window.DesktopScaling);
    }

    /// <summary>The content without the insets that system bars and display cutouts cover.</summary>
    /// <param name="content">The view content in DIP.</param>
    /// <param name="insets">The safe-area padding of the view in DIP.</param>
    internal static Rect SafeArea(Rect content, Thickness insets)
    {
        var safe = content.Deflate(insets);
        // Insets that leave nothing keep the full content area, like a window outside its display.
        return safe is { Width: > 0, Height: > 0 } ? safe : content;
    }

    /// <param name="content">The window content in DIP.</param>
    /// <param name="clientX">The content's left edge on the screen in desktop pixels.</param>
    /// <param name="clientY">The content's top edge on the screen in desktop pixels.</param>
    /// <param name="workArea">The display's work area in desktop pixels.</param>
    /// <param name="scale">The window's desktop scaling: desktop pixels per DIP.</param>
    internal static Rect VisibleArea(Rect content, int clientX, int clientY, PixelRect workArea, double scale)
    {
        if (!(scale > 0))
            return content;
        var visible = new Rect((workArea.X - clientX) / scale, (workArea.Y - clientY) / scale, workArea.Width / scale, workArea.Height / scale);
        visible = visible.Intersect(content);
        // A window that is mostly outside its display keeps its full content area.
        return visible is not { Width: > 0, Height: > 0 } || visible.Width < content.Width / 4 || visible.Height < content.Height / 4 ? content : visible;
    }

    internal static string Describe(Control? target, Control host)
    {
        if (target is null) return "No target was supplied.";
        var details = new List<string>();
        for (Visual? current = target; current is not null; current = current.GetVisualParent())
        {
            if (current is Control element)
                details.Add($"{element.GetType().Name}({element.Name}): {(element.IsVisible ? "Visible" : "Collapsed")}, opacity={element.Opacity}, size={element.Bounds.Width}x{element.Bounds.Height}");
        }
        return $"Same root={TopLevel.GetTopLevel(target) == TopLevel.GetTopLevel(host)}; connected={TopLevel.GetTopLevel(target) is not null}. {string.Join("; ", details)}";
    }

    internal static bool TryBounds(Control target, Control host, out Rect bounds)
    {
        bounds = default;
        if (TopLevel.GetTopLevel(target) is not { } root || root != TopLevel.GetTopLevel(host))
            return false;
        for (Visual? element = target; element is not null; element = element.GetVisualParent())
        {
            if (!element.IsVisible || element.Opacity <= 0)
                return false;
        }
        if (target.Bounds.Width <= 0 || target.Bounds.Height <= 0)
            return false;
        if (target.TransformToVisual(host) is not { } transform)
            return false;
        bounds = new Rect(0, 0, target.Bounds.Width, target.Bounds.Height).TransformToAABB(transform);
        return double.IsFinite(bounds.X) && double.IsFinite(bounds.Y);
    }

    internal static Rect VisibleBounds(Control target, Control host, Rect bounds)
    {
        bounds = bounds.Intersect(new Rect(0, 0, host.Bounds.Width, host.Bounds.Height));
        for (Visual? element = target; element is not null; element = element.GetVisualParent())
        {
            if (element is ScrollViewer viewer && viewer.TransformToVisual(host) is { } transform)
            {
                var viewport = new Rect(0, 0, Math.Min(viewer.Bounds.Width, viewer.Viewport.Width), Math.Min(viewer.Bounds.Height, viewer.Viewport.Height))
                    .TransformToAABB(transform);
                bounds = bounds.Intersect(viewport);
            }
        }
        return bounds;
    }

    internal static async Task SettleAsync(Control host, CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Layout is forced explicitly; low-priority queue work can starve during active rendering.
        // Avalonia's UpdateLayout runs the layout pass of the host's whole top level.
        Dispatcher.UIThread.Post(() =>
        {
            host.UpdateLayout();
            completion.TrySetResult();
        }, DispatcherPriority.Normal);
        await completion.Task.WaitAsync(token);
    }

    // Loaded handlers can still resize controls created during preparation, and a scroll
    // viewer that has not rendered yet applies an accepted offset to its content transform
    // only in the next frame. Bounds are measured after that frame.
    private static async Task NextFrameAsync(Control host, CancellationToken token)
    {
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (TopLevel.GetTopLevel(host) is { } root) root.RequestAnimationFrame(_ => rendered.TrySetResult());
        else rendered.TrySetResult();
        await rendered.Task.WaitAsync(token);
    }

    /// <param name="frame">Space around each target, such as its highlight frame, that must become visible too.</param>
    internal static async Task PrepareTargetsAsync(IReadOnlyList<Control> targets, Control host,
        CancellationToken token, double frame = 0)
    {
        await SettleAsync(host, token);
        if (targets.Any(target => !target.IsLoaded))
        {
            await NextFrameAsync(host, token);
            await SettleAsync(host, token);
        }
        // Innermost scroll containers are adjusted first. Shared containers use the
        // union, so preparing a connector cannot scroll its first endpoint away.
        var viewers = new List<ScrollViewer>();
        foreach (var target in targets)
        {
            for (Visual? current = target; current is not null; current = current.GetVisualParent())
            {
                if (current is ScrollViewer viewer && !viewers.Contains(viewer))
                    viewers.Add(viewer);
            }
        }
        foreach (var viewer in viewers.OrderByDescending(Depth))
        {
            Rect? union = null;
            Rect? primary = null;
            foreach (var target in targets)
            {
                if (!IsDescendant(target, viewer) || !TryBounds(target, host, out _) || target.TransformToVisual(viewer) is not { } transform)
                    continue;
                var rect = new Rect(-frame, -frame, target.Bounds.Width + 2 * frame, target.Bounds.Height + 2 * frame).TransformToAABB(transform);
                if (ReferenceEquals(target, targets[0])) primary = rect;
                if (union is { } previous) union = previous.Union(rect);
                else union = rect;
            }
            if (union is not { } area)
                continue;
            // When all targets cannot fit, the step target (listed first) takes precedence.
            if (primary is { } main && (area.Width > viewer.Viewport.Width || area.Height > viewer.Viewport.Height))
                area = main;
            var x = ScrollDelta(area.X, area.Width, viewer.Viewport.Width);
            var y = ScrollDelta(area.Y, area.Height, viewer.Viewport.Height);
            var horizontal = Math.Clamp(viewer.Offset.X + x, 0, Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width));
            var vertical = Math.Clamp(viewer.Offset.Y + y, 0, Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height));
            var changed = horizontal != viewer.Offset.X || vertical != viewer.Offset.Y;
            if (changed)
            {
                viewer.Offset = new Vector(horizontal, vertical);
                await NextFrameAsync(host, token);
            }
            await SettleAsync(host, token);
        }
    }

    private static double ScrollDelta(double start, double length, double viewport) =>
        start < 0 || length > viewport ? start : Math.Max(0, start + length - viewport);

    private static int Depth(Visual element)
    {
        var depth = 0;
        for (var current = element.GetVisualParent(); current is not null; current = current.GetVisualParent()) depth++;
        return depth;
    }

    internal static bool IsDescendant(Visual element, Visual ancestor)
    {
        for (Visual? current = element; current is not null; current = current.GetVisualParent())
        {
            if (current.Equals(ancestor)) return true;
        }
        return false;
    }

    internal static Rect Place(Rect target, Size card, Size viewport, DaisyTourPlacement preferred)
    {
        const double margin = 12;
        var candidates = SideCandidates(target, card);
        foreach (var side in SideOrder(preferred))
        {
            if (candidates.TryGetValue(side, out var rect) && InView(rect, viewport))
                return rect;
        }
        var fallback = candidates[preferred == DaisyTourPlacement.Auto ? DaisyTourPlacement.Bottom : preferred];
        return new Rect(Math.Clamp(fallback.X, margin, Math.Max(margin, viewport.Width - card.Width - margin)),
            Math.Clamp(fallback.Y, margin, Math.Max(margin, viewport.Height - card.Height - margin)), card.Width, card.Height);
    }

    private static Dictionary<DaisyTourPlacement, Rect> SideCandidates(Rect target, Size card)
    {
        const double gap = 32;
        var centeredX = target.X + (target.Width - card.Width) / 2;
        var centeredY = target.Y + (target.Height - card.Height) / 2;
        return new Dictionary<DaisyTourPlacement, Rect>
        {
            [DaisyTourPlacement.Bottom] = new(centeredX, target.Bottom + gap, card.Width, card.Height),
            [DaisyTourPlacement.Top] = new(centeredX, target.Top - card.Height - gap, card.Width, card.Height),
            [DaisyTourPlacement.Right] = new(target.Right + gap, centeredY, card.Width, card.Height),
            [DaisyTourPlacement.Left] = new(target.Left - card.Width - gap, centeredY, card.Width, card.Height)
        };
    }

    private static IEnumerable<DaisyTourPlacement> SideOrder(DaisyTourPlacement preferred) =>
        new[] { preferred, DaisyTourPlacement.Bottom, DaisyTourPlacement.Top, DaisyTourPlacement.Right, DaisyTourPlacement.Left }.Distinct();

    private static bool InView(Rect rect, Size viewport)
    {
        const double margin = 12;
        return rect.X >= margin && rect.Y >= margin && rect.Right <= viewport.Width - margin && rect.Bottom <= viewport.Height - margin;
    }

    internal static bool Covers(Rect card, IEnumerable<Rect> obstacles)
    {
        foreach (var obstacle in obstacles)
        {
            if (Overlap(card, obstacle) > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Moves a card off connector courses; the obstacles are the highlighted targets, the courses, and the
    /// areas their route limits keep clear. A fixed position slides along its own edge. Otherwise a previous
    /// position that is still clear is kept; else the card goes beside the target: on a clear side, then
    /// moved along or away from a side, then at the nearest clear spot in view. Without a highlighted target,
    /// the nearest clear spot from the center is used. Null when nothing is clear.
    /// </summary>
    internal static Rect? AvoidObstacles(DaisyTourCardPosition position, DaisyTourPlacement preferred, Rect? target,
        Size card, Size viewport, IReadOnlyList<Rect> obstacles, Rect? previous = null)
    {
        if (position is not (DaisyTourCardPosition.Default or DaisyTourCardPosition.Target) &&
            SlideStationary(position, card, viewport, obstacles) is { } stationary)
            return stationary;
        if (previous is { } kept && InView(kept, viewport) && !Covers(kept, obstacles))
            return kept;
        if (target is not { } anchor)
            return Clear(StationaryRect(DaisyTourCardPosition.Center, card, viewport), viewport, obstacles, true, true, combined: true);
        var candidates = SideCandidates(anchor, card);
        var sides = SideOrder(preferred).Where(candidates.ContainsKey).ToList();
        foreach (var side in sides)
        {
            if (InView(candidates[side], viewport) && !Covers(candidates[side], obstacles))
                return candidates[side];
        }
        foreach (var side in sides)
        {
            if (Clear(candidates[side], viewport, obstacles, true, true, combined: true, side) is { } moved)
                return moved;
        }
        var start = candidates[preferred == DaisyTourPlacement.Auto ? DaisyTourPlacement.Bottom : preferred];
        return Clear(start, viewport, obstacles, true, true, combined: true);
    }

    // The nearest position clear of all obstacles. Candidate coordinates are the start, the view edges,
    // and the positions just before and after each obstacle; without combined moves, only one axis changes.
    // A side keeps the card on that side of its start: Top moves it only up, Left only left, and so on.
    private static Rect? Clear(Rect start, Size viewport, IReadOnlyList<Rect> obstacles, bool horizontal, bool vertical,
        bool combined = false, DaisyTourPlacement side = DaisyTourPlacement.Auto)
    {
        const double margin = 12;
        const double gap = 12;
        var xs = new List<double> { start.X };
        var ys = new List<double> { start.Y };
        foreach (var obstacle in obstacles)
        {
            if (horizontal) { xs.Add(obstacle.Left - gap - start.Width); xs.Add(obstacle.Right + gap); }
            if (vertical) { ys.Add(obstacle.Top - gap - start.Height); ys.Add(obstacle.Bottom + gap); }
        }
        if (horizontal) { xs.Add(margin); xs.Add(viewport.Width - start.Width - margin); }
        if (vertical) { ys.Add(margin); ys.Add(viewport.Height - start.Height - margin); }
        var positions = new List<Point>();
        foreach (var x in xs)
        {
            if (!combined) { positions.Add(new Point(x, start.Y)); continue; }
            foreach (var y in ys) positions.Add(new Point(x, y));
        }
        if (!combined)
        {
            foreach (var y in ys) positions.Add(new Point(start.X, y));
        }
        Rect? best = null;
        var distance = double.PositiveInfinity;
        foreach (var position in positions)
        {
            var moved = Math.Abs(position.X - start.X) + Math.Abs(position.Y - start.Y);
            if (moved >= distance || side switch
                {
                    DaisyTourPlacement.Top => position.Y > start.Y,
                    DaisyTourPlacement.Bottom => position.Y < start.Y,
                    DaisyTourPlacement.Left => position.X > start.X,
                    DaisyTourPlacement.Right => position.X < start.X,
                    _ => false
                }) continue;
            var candidate = new Rect(position.X, position.Y, start.Width, start.Height);
            if (!InView(candidate, viewport) || Covers(candidate, obstacles)) continue;
            best = candidate;
            distance = moved;
        }
        return best;
    }

    /// <summary>
    /// Places the card at a fixed window position; null requests target-relative placement. A card that
    /// would cover the highlighted targets slides along its own edge until they are clear: Left and Right
    /// vertically, Top and Bottom horizontally, corners and Center along either axis. Without room on that
    /// edge, the card goes beside the target.
    /// </summary>
    /// <param name="highlighted">The highlighted area the card keeps clear of; null when nothing is highlighted.</param>
    internal static Rect? PlaceStationary(DaisyTourCardPosition position, Size card, Size viewport, Rect? highlighted) =>
        position is DaisyTourCardPosition.Default or DaisyTourCardPosition.Target
            ? null
            : SlideStationary(position, card, viewport, highlighted is { } area ? [area] : []);

    // The smallest slide along the position's own edge that keeps the card in view and clear.
    private static Rect? SlideStationary(DaisyTourCardPosition position, Size card, Size viewport, IReadOnlyList<Rect> obstacles) =>
        Clear(StationaryRect(position, card, viewport), viewport, obstacles,
            position is not (DaisyTourCardPosition.Left or DaisyTourCardPosition.Right),
            position is not (DaisyTourCardPosition.Top or DaisyTourCardPosition.Bottom));

    private static Rect StationaryRect(DaisyTourCardPosition position, Size card, Size viewport)
    {
        const double margin = 12;
        var x = position switch
        {
            DaisyTourCardPosition.TopLeft or DaisyTourCardPosition.Left or DaisyTourCardPosition.BottomLeft => margin,
            DaisyTourCardPosition.TopRight or DaisyTourCardPosition.Right or DaisyTourCardPosition.BottomRight => viewport.Width - card.Width - margin,
            _ => (viewport.Width - card.Width) / 2
        };
        var y = position switch
        {
            DaisyTourCardPosition.TopLeft or DaisyTourCardPosition.Top or DaisyTourCardPosition.TopRight => margin,
            DaisyTourCardPosition.BottomLeft or DaisyTourCardPosition.Bottom or DaisyTourCardPosition.BottomRight => viewport.Height - card.Height - margin,
            _ => (viewport.Height - card.Height) / 2
        };
        return new Rect(Math.Max(margin, x), Math.Max(margin, y), card.Width, card.Height);
    }

    // Directions clockwise from east in 45° steps; positions above the target come first.
    private static readonly int[] AnnotationOrder = [6, 7, 5, 0, 4, 2, 1, 3];

    internal static Rect PlaceAnnotation(Rect target, Rect card, Size viewport, Size surface) =>
        PlaceAnnotation(target, card, viewport, surface, [], -1, false, out _);

    /// <summary>
    /// Places a surface beside the target in one of eight directions. A direction is valid when the
    /// surface stays inside the viewport and covers neither the target, the card, nor another highlight.
    /// The previous direction is kept while it remains valid. Direction is -1 when none is valid.
    /// </summary>
    internal static Rect PlaceAnnotation(Rect target, Rect card, Size viewport, Size surface,
        IReadOnlyList<Rect> highlights, int previous, bool rightToLeft, out int direction)
    {
        const double margin = 12;
        var order = AnnotationOrder.Select(index => rightToLeft ? (12 - index) % 8 : index);
        foreach (var index in previous >= 0 ? order.Prepend(previous) : order)
        {
            var candidate = AnnotationCandidate(target, surface, index);
            if (candidate.X >= margin && candidate.Y >= margin && candidate.Right <= viewport.Width - margin &&
                candidate.Bottom <= viewport.Height - margin && Overlap(candidate, card) == 0 &&
                highlights.All(highlight => Overlap(candidate, highlight) == 0))
            {
                direction = index;
                return candidate;
            }
        }
        // No direction fits: keep the surface in the viewport and cover as little as possible.
        direction = -1;
        return order.Select(index => AnnotationCandidate(target, surface, index))
            .Select(candidate => new Rect(
                Math.Clamp(candidate.X, margin, Math.Max(margin, viewport.Width - surface.Width - margin)),
                Math.Clamp(candidate.Y, margin, Math.Max(margin, viewport.Height - surface.Height - margin)),
                surface.Width, surface.Height))
            .OrderBy(candidate => Overlap(candidate, target) * 4 + Overlap(candidate, card) * 2 +
                highlights.Sum(highlight => Overlap(candidate, highlight)))
            .First();
    }

    private static Rect AnnotationCandidate(Rect target, Size surface, int direction)
    {
        const double gap = 28;
        var (dx, dy) = direction switch
        {
            0 => (1, 0), 1 => (1, 1), 2 => (0, 1), 3 => (-1, 1), 4 => (-1, 0), 5 => (-1, -1), 6 => (0, -1), _ => (1, -1)
        };
        // Diagonal positions start at the corner; the tail spans both axes there.
        var offset = dx != 0 && dy != 0 ? gap / 2 : gap;
        var x = dx > 0 ? target.Right + offset : dx < 0 ? target.Left - offset - surface.Width : target.X + (target.Width - surface.Width) / 2;
        var y = dy > 0 ? target.Bottom + offset : dy < 0 ? target.Top - offset - surface.Height : target.Y + (target.Height - surface.Height) / 2;
        return new Rect(x, y, surface.Width, surface.Height);
    }

    private static double Overlap(Rect first, Rect second)
    {
        first = first.Intersect(second);
        return first is not { Width: > 0, Height: > 0 } ? 0 : first.Width * first.Height;
    }
}
