using Avalonia;
using Avalonia.Controls;

namespace Flowery.Capture.Internals;

/// <summary>
/// Helper for clipping capture regions to viewport and screen bounds.
/// </summary>
internal static class RegionClipper
{
    /// <summary>
    /// Clips a capture region to the visible viewport of a ScrollViewer.
    /// </summary>
    public static PixelRect ClipToViewport(PixelRect region, ScrollViewer scrollViewer)
    {
        var viewport = CoordinateHelper.GetViewportBounds(scrollViewer);
        return Intersect(region, viewport);
    }

    /// <summary>
    /// Clips a capture region to the screen's working area (excludes taskbar).
    /// </summary>
    public static PixelRect ClipToScreen(PixelRect region, PixelRect workingArea)
    {
        return Intersect(region, workingArea);
    }

    /// <summary>
    /// Clips a capture region to both viewport and screen bounds.
    /// </summary>
    public static PixelRect ClipToVisibleArea(
        PixelRect region,
        ScrollViewer? scrollViewer,
        PixelRect? workingArea)
    {
        var result = region;

        if (scrollViewer != null)
            result = ClipToViewport(result, scrollViewer);

        if (workingArea.HasValue)
            result = ClipToScreen(result, workingArea.Value);

        return result;
    }

    /// <summary>
    /// Computes the intersection of two pixel rectangles.
    /// </summary>
    private static PixelRect Intersect(PixelRect a, PixelRect b)
    {
        var x = System.Math.Max(a.X, b.X);
        var y = System.Math.Max(a.Y, b.Y);
        var right = System.Math.Min(a.Right, b.Right);
        var bottom = System.Math.Min(a.Bottom, b.Bottom);

        var width = right - x;
        var height = bottom - y;

        if (width <= 0 || height <= 0)
            return default;

        return new PixelRect(x, y, width, height);
    }
}
