using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Flowery.NET.Photos.Views;

/// <summary>Scrolling a virtualized list of rows for the keyboard, shared by the gallery and the album and folder pages.</summary>
internal static class PageScroll
{
    /// <summary>The top of a row in the scroller, when the row is realized.</summary>
    public static double? TopOf(ScrollViewer scroller, ListBox list, int row) =>
        list.ContainerFromIndex(row)?.TranslatePoint(default, scroller)?.Y;

    /// <summary>
    /// Scrolls the row into view: to the very top or bottom for edge -1 or 1, to the given top for a page, otherwise
    /// just far enough; then focuses the control of type <typeparamref name="T"/> at the column in it.
    /// </summary>
    public static void Show<T>(ScrollViewer scroller, ListBox list, int row, int column, double? top, int edge) where T : Control
    {
        list.ScrollIntoView(row);
        list.UpdateLayout();
        if (edge != 0) ScrollToEdge(scroller, edge);
        else if (top is { } at && list.ContainerFromIndex(row) is { } container) MoveTo(scroller, container, at);
        list.UpdateLayout();
        list.ContainerFromIndex(row)?.GetVisualDescendants().OfType<T>().ElementAtOrDefault(column)?.Focus(NavigationMethod.Directional);
    }

    /// <summary>Scrolls until the element's top lies at top in the scroller.</summary>
    public static void MoveTo(ScrollViewer scroller, Control element, double top)
    {
        if (element.TranslatePoint(default, scroller) is { } point)
            scroller.Offset = new Avalonia.Vector(0, scroller.Offset.Y + point.Y - top);
    }

    /// <summary>Scrolls to the very top for edge -1 or the very bottom for edge 1.</summary>
    public static void ScrollToEdge(ScrollViewer scroller, int edge)
    {
        if (edge != 0) scroller.Offset = new Avalonia.Vector(0, edge < 0 ? 0 : Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height));
    }
}
