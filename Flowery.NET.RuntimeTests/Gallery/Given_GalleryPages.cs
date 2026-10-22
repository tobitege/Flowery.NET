using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Gallery;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

public class Given_GalleryPages
{
    private readonly StringBuilder _failures = new();
    private string _currentItem = string.Empty;

    [Fact]
    public async Task When_Selecting_Every_Sidebar_Item_Its_Page_Opens_Without_Exceptions()
    {
        var mainView = await RuntimeTestHelpers.AttachAsync(new MainView());
        var sidebar = mainView.FindControl<FloweryComponentSidebar>("ComponentSidebar");
        var contentHost = mainView.FindControl<Panel>("MainContentHost");
        var title = mainView.FindControl<TextBlock>("CategoryTitle");
        Assert.NotNull(sidebar?.Categories);
        Assert.NotNull(contentHost);
        Assert.NotNull(title);

        var visitedPages = new HashSet<Control>();
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        try
        {
            foreach (var category in sidebar.Categories)
            foreach (var item in category.Items)
            {
                _currentItem = $"{category.Name}/{item.Id}";
                try
                {
                    // The sidebar raises this event when an item is clicked; MainView navigates in its handler.
                    sidebar.SelectedItem = item;
                    sidebar.RaiseEvent(new SidebarItemSelectedEventArgs(FloweryComponentSidebar.ItemSelectedEvent, item, category));
                    await RuntimeTestHelpers.WaitForRenderAsync();

                    var pages = contentHost.Children.Where(child => child.IsVisible).ToList();
                    if (pages.Count != 1)
                        _failures.AppendLine($"{_currentItem}: {pages.Count} visible pages instead of one.");
                    else if (pages[0].Bounds.Width <= 0 || pages[0].Bounds.Height <= 0)
                        _failures.AppendLine($"{_currentItem}: the page {pages[0].GetType().Name} has no size.");
                    else
                        visitedPages.Add(pages[0]);
                    if (item.TabHeader != "Sidebar_Home" && title.Text != FloweryLocalization.GetString(item.TabHeader))
                        _failures.AppendLine($"{_currentItem}: the title is '{title.Text}'.");
                }
                catch (Exception exception)
                {
                    _failures.AppendLine($"{_currentItem}: {exception.GetType().Name} - {exception.Message}");
                }
            }
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
        }

        Assert.True(_failures.Length == 0, _failures.ToString());
        // Every sidebar area opens its own page.
        var areas = sidebar.Categories.SelectMany(category => category.Items).Select(item => item.TabHeader).Distinct().Count();
        Assert.True(visitedPages.Count >= areas, $"{visitedPages.Count} pages opened for {areas} sidebar areas.");
    }

    // The runner fails the test as well; this names the sidebar item that was open.
    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e) =>
        _failures.AppendLine($"{_currentItem}: unhandled {e.Exception.GetType().Name} - {e.Exception.Message}");
}
