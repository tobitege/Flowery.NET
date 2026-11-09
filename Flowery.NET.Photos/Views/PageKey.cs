using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Flowery.NET.Photos.Views;

/// <summary>
/// A key that moves through a page of thumbnails or cards: an arrow key, Home or End, and the paging keys, which are
/// Page Up, Page Down and Ctrl with Home or End.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Ctrl">Whether Ctrl is down.</param>
internal readonly record struct PageKey(Key Key, bool Ctrl)
{
    public bool IsPaging => Key is Key.PageUp or Key.PageDown || Ctrl && Key is Key.Home or Key.End;

    /// <summary>For Ctrl with Home, End, Page Up or Page Down: -1 for the start of the page, 1 for its end, else 0.</summary>
    public int Edge => !Ctrl ? 0 : Key is Key.Home or Key.PageUp ? -1 : Key is Key.End or Key.PageDown ? 1 : 0;

    /// <summary>
    /// The key of a key press taken at the window, or null when it is none of these or another control owns it. On
    /// the page, text boxes, sliders and lists below it keep their keys. Outside the page, only a button, such as one of
    /// the side bar, lends the page its paging keys; any other control, such as the gallery's timeline, keeps its keys.
    /// Without any focus the keys go to the window itself, and so to the page.
    /// </summary>
    public static PageKey? Of(KeyEventArgs e, object? window, Control page)
    {
        if (e.Handled || e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return null;
        var key = new PageKey(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Control));
        if (!key.IsPaging && e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)) return null;
        if (e.Source == window) return key;
        var ancestors = (e.Source as Visual)?.GetSelfAndVisualAncestors().ToList() ?? [];
        if (!ancestors.Contains(page)) return key.IsPaging && e.Source is Button ? key : null;
        return ancestors.TakeWhile(visual => visual != page).Any(visual => visual is TextBox or RangeBase or SelectingItemsControl)
            ? null : key;
    }
}
