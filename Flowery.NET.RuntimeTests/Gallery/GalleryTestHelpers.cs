using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flowery.Controls;
using Flowery.NET.Gallery;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>Navigation in the Gallery's main view and touch taps, which the Gallery page tests share.</summary>
internal static class GalleryTestHelpers
{
    /// <summary>
    /// Selects the first sidebar item that <paramref name="match"/> accepts, as a click does: the sidebar raises its
    /// ItemSelected event and MainView navigates in its handler. Returns the page that is shown then.
    /// </summary>
    internal static async Task<Control> NavigateAsync(MainView mainView, Func<SidebarItem, bool> match)
    {
        var sidebar = mainView.FindControl<FloweryComponentSidebar>("ComponentSidebar")!;
        var category = sidebar.Categories!.First(category => category.Items.Any(match));
        var item = category.Items.First(match);
        sidebar.SelectedItem = item;
        sidebar.RaiseEvent(new SidebarItemSelectedEventArgs(FloweryComponentSidebar.ItemSelectedEvent, item, category));
        await RuntimeTestHelpers.WaitForRenderAsync();
        return VisiblePage(mainView);
    }

    internal static Control VisiblePage(MainView mainView) =>
        mainView.FindControl<Panel>("MainContentHost")!.Children.Single(static child => child.IsVisible);

    /// <summary>Raises a touch tap on <paramref name="target"/>, as the gesture recognizer does after a short touch.</summary>
    internal static void Tap(Visual root, Interactive target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var release = new PointerEventArgs(InputElement.PointerReleasedEvent, target, pointer, root, default, 0,
            PointerPointProperties.None, KeyModifiers.None);
        target.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, release));
    }
}
