using Avalonia;
using Avalonia.Controls;

namespace Flowery.Capture.Internals;

/// <summary>
/// Helper for coordinate conversions between logical and physical pixels.
/// </summary>
internal static class CoordinateHelper
{
    /// <summary>
    /// Gets the screen pixel rectangle for a control, accounting for DPI scaling.
    /// </summary>
    /// <param name="control">The control to get bounds for.</param>
    /// <param name="margin">Optional margin adjustment (negative expands, positive shrinks).</param>
    /// <returns>Screen pixel rectangle, or empty if control is not attached to visual tree.</returns>
    public static PixelRect GetScreenBounds(Control control, Thickness margin = default)
    {
        var topLeft = control.PointToScreen(new Point(margin.Left, margin.Top));
        var bottomRight = control.PointToScreen(new Point(
            control.Bounds.Width - margin.Right,
            control.Bounds.Height - margin.Bottom));

        var x = topLeft.X;
        var y = topLeft.Y;
        var width = bottomRight.X - topLeft.X;
        var height = bottomRight.Y - topLeft.Y;

        // Ensure positive dimensions
        if (width < 0)
        {
            x += width;
            width = -width;
        }
        if (height < 0)
        {
            y += height;
            height = -height;
        }

        return new PixelRect(x, y, width, height);
    }

    /// <summary>
    /// Gets the screen pixel rectangle for a ScrollViewer's viewport.
    /// </summary>
    public static PixelRect GetViewportBounds(ScrollViewer scrollViewer)
    {
        var topLeft = scrollViewer.PointToScreen(new Point(0, 0));
        var bottomRight = scrollViewer.PointToScreen(new Point(
            scrollViewer.Bounds.Width,
            scrollViewer.Bounds.Height));

        return new PixelRect(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
    }
}

