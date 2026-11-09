using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flowery.NET.Photos.Models;

namespace Flowery.NET.Photos.Views;

/// <summary>
/// The keyboard in the gallery: the arrow keys go from thumbnail to thumbnail, Page Up and Page Down a screen up or
/// down, Home and End to the start and end of the row, and with Ctrl, Home, End, Page Up and Page Down to the first and
/// last thumbnail. A thumbnail keeps the focus throughout, so no other element takes the keys.
/// </summary>
/// <remarks>
/// The keys are taken at the window, before the list of rows, its scroller and the window's own focus moves see them:
/// the list moved the focus by rows, a move down handed it on to a text box, and a page down scrolled the focused
/// thumbnail out of the list, which left no focus at all. <see cref="PageKey"/> says which keys the gallery takes.
/// </remarks>
public partial class GalleryView
{
    private TopLevel? _topLevel;

    private void AttachKeys()
    {
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void DetachKeys()
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
        _topLevel = null;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsEffectivelyVisible && PageKey.Of(e, sender, Rows) is { } key) e.Handled = Navigate(key);
    }

    // Moves the focus to another thumbnail; false when the gallery shows none.
    private bool Navigate(PageKey key)
    {
        if (Scroller is not { } scroller || CurrentTile(scroller) is not var (row, column, focused)) return false;
        var photos = (GalleryPhotoRow)_rows[row];
        var x = CenterOf(photos, column);
        // Without a focused thumbnail an arrow key, Home or End first focuses the one on the screen.
        (int Row, int Column)? target = !focused && !key.IsPaging ? (row, column) : key switch
        {
            { Edge: < 0 } => (NextPhotoRow(-1, 1)!.Value, 0),
            { Edge: > 0 } => LastTile(),
            { Key: Key.Home } => (row, 0),
            { Key: Key.End } => (row, photos.Tiles.Count - 1),
            { Key: Key.Left } => column > 0 ? (row, column - 1) : NextPhotoRow(row, -1) is { } before ? (before, LastColumn(before)) : null,
            { Key: Key.Right } => column < photos.Tiles.Count - 1 ? (row, column + 1) : NextPhotoRow(row, 1) is { } after ? (after, 0) : null,
            { Key: Key.Up } => NextPhotoRow(row, -1) is { } above ? (above, ColumnAt(above, x)) : null,
            { Key: Key.Down } => NextPhotoRow(row, 1) is { } below ? (below, ColumnAt(below, x)) : null,
            { Key: Key.PageUp } => PageRow(row, -scroller.Viewport.Height) is { } up ? (up, ColumnAt(up, x)) : null,
            { Key: Key.PageDown } => PageRow(row, scroller.Viewport.Height) is { } down ? (down, ColumnAt(down, x)) : null,
            _ => null
        };
        // At the first or last thumbnail the key does nothing, but it stays here.
        var (toRow, toColumn) = target ?? (row, column);
        var top = key.IsPaging && key.Edge == 0 ? PageScroll.TopOf(scroller, Rows, row) : null;
        PageScroll.Show<PhotoTile>(scroller, Rows, toRow, toColumn, top, key.Edge);
        return true;
    }

    // The focused thumbnail, or else the first one on the screen.
    private (int Row, int Column, bool Focused)? CurrentTile(ScrollViewer scroller)
    {
        if (_topLevel?.FocusManager?.GetFocusedElement() is PhotoTile { DataContext: GalleryPhotoRow focused } tile)
        {
            var row = _rows.IndexOf(focused);
            var column = ColumnOf(focused, tile.Item);
            if (row >= 0 && column >= 0) return (row, column, true);
        }
        foreach (var candidate in Rows.GetVisualDescendants().OfType<PhotoTile>())
        {
            if (!candidate.IsEffectivelyVisible || candidate.DataContext is not GalleryPhotoRow shown
                || candidate.TranslatePoint(default, scroller) is not { } point || point.Y < -1 || point.Y >= scroller.Viewport.Height) continue;
            var row = _rows.IndexOf(shown);
            if (row >= 0) return (row, ColumnOf(shown, candidate.Item), false);
        }
        return NextPhotoRow(-1, 1) is { } first ? (first, 0, false) : null;
    }

    private static int ColumnOf(GalleryPhotoRow row, PhotoItem item)
    {
        for (var column = 0; column < row.Tiles.Count; column++)
            if (row.Tiles[column].Item == item) return column;
        return -1;
    }

    private int? NextPhotoRow(int row, int step)
    {
        for (var index = row + step; index >= 0 && index < _rows.Count; index += step)
            if (_rows[index] is GalleryPhotoRow) return index;
        return null;
    }

    private (int Row, int Column)? LastTile() => NextPhotoRow(_rows.Count, -1) is { } last ? (last, LastColumn(last)) : null;

    private int LastColumn(int row) => ((GalleryPhotoRow)_rows[row]).Tiles.Count - 1;

    // The photo row a screen above or below, the nearest one when a date header lies there.
    private int? PageRow(int row, double distance)
    {
        var index = GalleryLayout.FindRowAt(_rows, Math.Max(0, _rows[row].Top + distance));
        if (index < 0 || index == row) return distance < 0 ? NextPhotoRow(row, -1) : NextPhotoRow(row, 1);
        return _rows[index] is GalleryPhotoRow ? index : NextPhotoRow(index, distance < 0 ? -1 : 1) ?? NextPhotoRow(index, distance < 0 ? 1 : -1);
    }

    private static double CenterOf(GalleryPhotoRow row, int column) =>
        row.Tiles.Take(column).Sum(tile => tile.Width + TileGap) + row.Tiles[column].Width / 2;

    // The thumbnail of the row whose center lies nearest to a point across the gallery.
    private int ColumnAt(int row, double x)
    {
        var photos = (GalleryPhotoRow)_rows[row];
        var nearest = 0;
        for (var column = 1; column < photos.Tiles.Count; column++)
            if (Math.Abs(CenterOf(photos, column) - x) < Math.Abs(CenterOf(photos, nearest) - x)) nearest = column;
        return nearest;
    }
}
