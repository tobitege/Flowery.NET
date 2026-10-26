using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flowery.Controls.Scheduler.Models;
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

    [Fact]
    public async Task When_An_On_Screen_Keyboard_Covers_The_View_The_Page_Keeps_Clear_Of_It()
    {
        // As on Android, where the header and the intro of the scheduler page give their height to the scheduler.
        var mainView = await RuntimeTestHelpers.AttachAsync(new MainView(collapsesIntros: true));
        var sidebar = mainView.FindControl<FloweryComponentSidebar>("ComponentSidebar")!;
        var item = sidebar.Categories!.SelectMany(static category => category.Items).First(static item => item.TabHeader == "Sidebar_Scheduler");
        sidebar.RaiseEvent(new SidebarItemSelectedEventArgs(FloweryComponentSidebar.ItemSelectedEvent, item,
            sidebar.Categories!.First(category => category.Items.Contains(item))));
        await RuntimeTestHelpers.WaitForRenderAsync();
        var page = mainView.FindControl<Panel>("MainContentHost")!.Children.Single(static child => child.IsVisible);
        page.FindControl<DaisyScheduler>("DemoScheduler")!.GetVisualDescendants().OfType<Button>()
            .Single(static button => button.Tag is SchedulerToolbarButton { Id: "new" }).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RuntimeTestHelpers.WaitForRenderAsync();
        var description = page.FindControl<TextBox>("DescriptionInput")!;
        description.Focus();
        var topLevel = TopLevel.GetTopLevel(mainView)!;
        var bottom = mainView.TranslatePoint(new Point(0, mainView.Bounds.Height), topLevel)!.Value.Y;
        var keyboard = Math.Round(mainView.Bounds.Height / 2);

        // The keyboard covers the lower half: the view ends above it, and the focused field comes into view.
        mainView.KeepClearOfInputPane(new Rect(0, bottom - keyboard, mainView.Bounds.Width, keyboard));
        await RuntimeTestHelpers.WaitForRenderAsync();
        await RuntimeTestHelpers.WaitForRenderAsync();
        var root = mainView.FindControl<Grid>("LayoutRoot")!;
        Assert.Equal(keyboard - mainView.Padding.Bottom, root.Margin.Bottom, 1);
        var editor = page.FindControl<Border>("EditorPanel")!.GetVisualDescendants().OfType<ScrollViewer>().First();
        var field = description.TranslatePoint(default, editor)!.Value;
        Assert.True(editor.Viewport.Height > description.Bounds.Height && field.Y >= 0 &&
            field.Y + description.Bounds.Height <= editor.Viewport.Height + 1,
            $"The description lies at {field.Y} to {field.Y + description.Bounds.Height} in a viewport of {editor.Viewport.Height}.");

        mainView.KeepClearOfInputPane(default);
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.Equal(0, root.Margin.Bottom);
    }

    [Fact]
    public async Task When_The_MainView_Is_Detached_It_Can_Be_Collected()
    {
        var reference = await AttachAndDetachMainViewAsync();
        for (var attempt = 0; attempt < 5 && reference.IsAlive; attempt++)
        {
            // Rendering releases its references to detached visuals on the next frames.
            await RuntimeTestHelpers.WaitForRenderAsync();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // A static subscription would keep the view and its cached pages alive for the rest of the run.
        Assert.False(reference.IsAlive, "Something keeps the detached MainView alive.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> AttachAndDetachMainViewAsync()
    {
        var mainView = await RuntimeTestHelpers.AttachAsync(new MainView());
        RuntimeTestHelpers.GetHostPanel().Children.Clear();
        return new WeakReference(mainView);
    }

    // The runner fails the test as well; this names the sidebar item that was open.
    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e) =>
        _failures.AppendLine($"{_currentItem}: unhandled {e.Exception.GetType().Name} - {e.Exception.Message}");
}
