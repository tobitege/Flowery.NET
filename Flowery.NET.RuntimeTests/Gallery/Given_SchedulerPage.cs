using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Examples;
using Flowery.NET.Gallery.Localization;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

public class Given_SchedulerPage
{
    [Fact]
    public async Task When_An_Appointment_Is_Created_The_Editor_Lies_Over_The_Scheduler()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new SchedulerExamples());
        var scheduler = page.FindControl<DaisyScheduler>("DemoScheduler")!;
        var editor = page.FindControl<Border>("EditorPanel")!;
        var width = scheduler.Bounds.Width;

        var create = scheduler.GetVisualDescendants().OfType<Button>()
            .Single(static button => button.Tag is SchedulerToolbarButton { Id: "new" });
        create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RuntimeTestHelpers.WaitForRenderAsync();

        Assert.True(editor.IsVisible);
        // The scheduler keeps its width; the editor covers its right side.
        Assert.Equal(width, scheduler.Bounds.Width);
        Assert.Equal(scheduler.Bounds.Right, editor.Bounds.Right, 1);
        Assert.True(editor.Bounds.Left > scheduler.Bounds.Left);
    }

    [Fact]
    public async Task When_The_Scheduler_Is_Pressed_An_Unchanged_Editor_Closes()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new SchedulerExamples());
        var scheduler = page.FindControl<DaisyScheduler>("DemoScheduler")!;
        var editor = page.FindControl<Border>("EditorPanel")!;
        var subject = page.FindControl<TextBox>("SubjectInput")!;

        await OpenNewAsync(scheduler);
        Press(page, scheduler);
        Assert.False(editor.IsVisible);

        // With a change the editor stays open; Escape too leaves it open.
        await OpenNewAsync(scheduler);
        subject.Text = "Lunch";
        Press(page, scheduler);
        subject.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.True(editor.IsVisible);

        // Escape closes the editor once the change is undone.
        subject.Text = string.Empty;
        subject.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.False(editor.IsVisible);
    }

    [Fact]
    public async Task When_The_Page_Explains_Touch_It_Shows_The_Touch_Help()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new SchedulerExamples(touchHelp: true));
        var help = page.FindControl<TextBlock>("StatusText")!;
        Assert.Equal(GalleryLocalization.GetString("Scheduler_TouchHelp"), help.Text);

        var desktop = await RuntimeTestHelpers.AttachAsync(new SchedulerExamples(touchHelp: false));
        Assert.Equal(GalleryLocalization.GetString("Scheduler_Help"), desktop.FindControl<TextBlock>("StatusText")!.Text);
    }

    [Fact]
    public async Task When_The_Page_Is_Phone_Wide_The_Scheduler_Takes_Most_Of_Its_Width()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new SchedulerExamples { Width = 400, HorizontalAlignment = HorizontalAlignment.Left });
        var scheduler = page.FindControl<DaisyScheduler>("DemoScheduler")!;
        Assert.Equal(400 - 16, scheduler.Bounds.Width, 1);

        page.Width = 1000;
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.Equal(1000 - 48, scheduler.Bounds.Width, 1);
    }

    [Fact]
    public async Task When_A_Page_Has_A_Collapsible_Intro_The_Title_Bar_Folds_It()
    {
        var mainView = await RuntimeTestHelpers.AttachAsync(new MainView(collapsesIntros: true));
        var header = mainView.FindControl<Border>("HeaderBorder")!;
        var bar = mainView.FindControl<Border>("CategoryTitleBar")!;
        var menu = mainView.FindControl<Button>("CategoryMenuButton")!;

        await NavigateAsync(mainView, "Sidebar_Scheduler");
        var intro = VisiblePage(mainView).FindControl<Control>("IntroPanel")!;
        Assert.False(header.IsVisible);
        Assert.False(intro.IsVisible);
        Assert.Equal(bar.Padding.Top, bar.Padding.Bottom);

        Tap(mainView, bar);
        Assert.True(intro.IsVisible);
        // A tap on the menu button opens the sidebar only.
        Tap(mainView, menu);
        Assert.True(intro.IsVisible);
        Tap(mainView, bar);
        Assert.False(intro.IsVisible);

        // A page without an intro shows the header again; the intro keeps its state for the next visit.
        await NavigateAsync(mainView, "Sidebar_Actions");
        Assert.True(header.IsVisible);
        await NavigateAsync(mainView, "Sidebar_Scheduler");
        Assert.False(header.IsVisible);
        Assert.False(intro.IsVisible);
    }

    private static async Task OpenNewAsync(DaisyScheduler scheduler)
    {
        scheduler.GetVisualDescendants().OfType<Button>().Single(static button => button.Tag is SchedulerToolbarButton { Id: "new" })
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RuntimeTestHelpers.WaitForRenderAsync();
    }

    private static void Press(Visual root, Interactive target) =>
        target.RaiseEvent(new PointerPressedEventArgs(target, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true), root, default, 0,
            PointerPointProperties.None, KeyModifiers.None));

    private static async Task NavigateAsync(MainView mainView, string tabHeader)
    {
        var sidebar = mainView.FindControl<FloweryComponentSidebar>("ComponentSidebar")!;
        var category = sidebar.Categories!.First(category => category.Items.Any(item => item.TabHeader == tabHeader));
        var item = category.Items.First(item => item.TabHeader == tabHeader);
        sidebar.SelectedItem = item;
        sidebar.RaiseEvent(new SidebarItemSelectedEventArgs(FloweryComponentSidebar.ItemSelectedEvent, item, category));
        await RuntimeTestHelpers.WaitForRenderAsync();
    }

    private static Control VisiblePage(MainView mainView) =>
        mainView.FindControl<Panel>("MainContentHost")!.Children.Single(static child => child.IsVisible);

    private static void Tap(Visual root, Interactive target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var release = new PointerEventArgs(InputElement.PointerReleasedEvent, target, pointer, root, default, 0,
            PointerPointProperties.None, KeyModifiers.None);
        target.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, release));
    }
}
