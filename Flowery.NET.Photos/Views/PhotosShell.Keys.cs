using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Flowery.NET.Photos.Views;

/// <summary>
/// The keyboard on the album and folder pages and on the home page, as in the gallery: the arrow keys go from card to
/// card, Page Up and Page Down a screen up or down, Home and End to the start and end of the row, and Ctrl with Home,
/// End, Page Up or Page Down to the first and last card. The page keys also work while the side bar has the focus.
/// </summary>
/// <remarks>
/// The album and folder cards sit in a list of rows like the gallery's thumbnails, with the same faults before: a move
/// down handed the focus to the search box, and no key paged. The home page leaves its arrow keys to the window's
/// directional focus moves (XYFocus in the XAML) and pages here; before, Page Down left the focus on a card above the
/// screen, and with the focus in the side bar it did nothing.
/// </remarks>
public partial class PhotosShell
{
    private TopLevel? _topLevel;

    private void AttachKeys()
    {
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnPageKeyDown, RoutingStrategies.Tunnel);
    }

    private void DetachKeys()
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnPageKeyDown);
        _topLevel = null;
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (Viewer.IsVisible) return;
        if (CollectionsScroll.IsVisible && PageKey.Of(e, sender, Collections) is { } card) e.Handled = MoveInCollections(card);
        else if (HomeScroll.IsVisible && PageKey.Of(e, sender, HomeScroll) is { } home) e.Handled = MoveOnHome(home);
    }

    private IInputElement? FocusedElement => _topLevel?.FocusManager?.GetFocusedElement();

    // The cards are rows of equal columns; the focus goes by the index of a card among them all.
    private bool MoveInCollections(PageKey key)
    {
        if (Collections.ItemsSource is not CollectionRow[] { Length: > 0 } rows
            || Collections.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroller) return false;
        var columns = rows[0].Entries.Length;
        var count = rows.Sum(row => row.Entries.Length);
        if (CurrentCard(scroller, columns) is not var (index, focused)) return false;
        var (row, column) = (index / columns, index % columns);
        var rowHeight = Collections.ContainerFromIndex(row)?.Bounds.Height ?? scroller.Viewport.Height;
        var rowsPerPage = Math.Max(1, (int)(scroller.Viewport.Height / Math.Max(1, rowHeight)));
        // Without a focused card an arrow key, Home or End first focuses the one on the screen.
        var target = !focused && !key.IsPaging ? index : key switch
        {
            { Edge: < 0 } => 0,
            { Edge: > 0 } => count - 1,
            { Key: Key.Home } => row * columns,
            { Key: Key.End } => Math.Min(count, (row + 1) * columns) - 1,
            { Key: Key.Left } => Math.Max(0, index - 1),
            { Key: Key.Right } => Math.Min(count - 1, index + 1),
            { Key: Key.Up } => index >= columns ? index - columns : index,
            // Below a row that the last row does not fill, the last card.
            { Key: Key.Down } => row < rows.Length - 1 ? Math.Min(count - 1, index + columns) : index,
            { Key: Key.PageUp } => Math.Max(0, row - rowsPerPage) * columns + column,
            { Key: Key.PageDown } => Math.Min(count - 1, Math.Min(rows.Length - 1, row + rowsPerPage) * columns + column),
            _ => index
        };
        var top = key.IsPaging && key.Edge == 0 ? PageScroll.TopOf(scroller, Collections, row) : null;
        PageScroll.Show<PhotoCard>(scroller, Collections, target / columns, target % columns, top, key.Edge);
        return true;
    }

    // The focused card's index, or else that of the first card on the screen.
    private (int Index, bool Focused)? CurrentCard(ScrollViewer scroller, int columns)
    {
        if (FocusedElement is PhotoCard focused && IndexOf(focused, columns) is { } index) return (index, true);
        foreach (var card in Collections.GetVisualDescendants().OfType<PhotoCard>())
        {
            if (card.TranslatePoint(default, scroller) is { } point && point.Y >= -1 && point.Y < scroller.Viewport.Height
                && IndexOf(card, columns) is { } shown) return (shown, false);
        }
        return null;
    }

    private int? IndexOf(PhotoCard card, int columns) =>
        card.FindAncestorOfType<ListBoxItem>() is { } container && card.Parent is Panel panel && Collections.IndexFromContainer(container) is >= 0 and var row
            ? row * columns + panel.Children.IndexOf(card) : null;

    // On the home page the arrow keys move between the cards and buttons by themselves once one has the focus; the
    // page keys scroll and take the focus along to the card or button at the same place on the screen.
    private bool MoveOnHome(PageKey key)
    {
        var targets = Home.GetVisualDescendants().OfType<Control>()
            .Where(control => control is PhotoCard or Button && control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled)
            .ToList();
        if (targets.Count == 0) return false;
        var focused = FocusedElement as Control;
        if (!key.IsPaging)
        {
            // The window's own moves take it from a focused card or button; without one they would find nothing to start from.
            if (focused != null) return false;
            FocusAndReveal(FirstOnScreen(targets, HomeScroll) ?? targets[0]);
            return true;
        }
        var before = HomeScroll.Offset.Y;
        var from = focused != null && targets.Contains(focused) && focused.TranslatePoint(default, HomeScroll) is { } point ? point : default;
        if (key.Edge != 0) PageScroll.ScrollToEdge(HomeScroll, key.Edge);
        else HomeScroll.Offset = new Avalonia.Vector(0, before + (key.Key == Key.PageDown ? 1 : -1) * HomeScroll.Viewport.Height);
        HomeScroll.UpdateLayout();
        // At the top or bottom already, the first or last card or button.
        var edge = key.Edge != 0 ? key.Edge : Math.Abs(HomeScroll.Offset.Y - before) < 1 ? (key.Key == Key.PageDown ? 1 : -1) : 0;
        FocusAndReveal(edge < 0 ? targets[0] : edge > 0 ? targets[^1] : NearestOnScreen(targets, HomeScroll, from) ?? targets[0]);
        return true;
    }

    private static bool IsOnScreen(Control target, ScrollViewer scroller) => target.TranslatePoint(default, scroller) is { } top
        && top.Y >= -1 && top.Y + target.Bounds.Height <= scroller.Viewport.Height + 1;

    private static Control? FirstOnScreen(List<Control> targets, ScrollViewer scroller)
    {
        foreach (var target in targets)
            if (IsOnScreen(target, scroller)) return target;
        return null;
    }

    // The target on the screen nearest to a point, lines apart counting more than places along a line.
    private static Control? NearestOnScreen(List<Control> targets, ScrollViewer scroller, Point from)
    {
        Control? nearest = null;
        var best = double.MaxValue;
        foreach (var target in targets)
        {
            if (!IsOnScreen(target, scroller) || target.TranslatePoint(default, scroller) is not { } top) continue;
            var distance = Math.Abs(top.Y - from.Y) * 4 + Math.Abs(top.X - from.X);
            if (distance < best) (nearest, best) = (target, distance);
        }
        return nearest;
    }

    private static void FocusAndReveal(Control target)
    {
        target.Focus(NavigationMethod.Directional);
        target.BringIntoView();
    }
}
