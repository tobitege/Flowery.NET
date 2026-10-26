using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Flowery.Controls;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Examples;
using Flowery.NET.Kanban.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

public class Given_KanbanPage
{
    [Fact]
    public async Task When_The_Page_Has_A_Collapsible_Intro_The_Title_Bar_Folds_The_Description_And_The_Features()
    {
        var mainView = await RuntimeTestHelpers.AttachAsync(new MainView(collapsesIntros: true));
        var bar = mainView.FindControl<Border>("CategoryTitleBar")!;

        await NavigateAsync(mainView, "Sidebar_FlowKanban");
        var page = VisiblePage(mainView);
        var description = page.FindControl<Control>("DescriptionText")!;
        var features = page.FindControl<Control>("FeaturesCollapse")!;
        var board = page.FindControl<FlowKanban>("DemoKanban")!;
        Assert.False(mainView.FindControl<Border>("HeaderBorder")!.IsVisible);
        Assert.False(description.IsVisible);
        Assert.False(features.IsVisible);
        var foldedHeight = board.Bounds.Height;

        Tap(mainView, bar);
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.True(description.IsVisible);
        Assert.True(features.IsVisible);
        Assert.True(board.Bounds.Height < foldedHeight, "the folded intro gives its height to the board");
    }

    [Fact]
    public async Task When_The_Page_Is_Phone_Wide_The_Board_Takes_Most_Of_Its_Width()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new FlowKanbanExample { Width = 400, HorizontalAlignment = HorizontalAlignment.Left });
        var frame = (Border)page.FindControl<FlowKanban>("DemoKanban")!.Parent!;
        Assert.Equal(400 - 16, frame.Bounds.Width, 1);

        page.Width = 1000;
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.Equal(1000 - 48, frame.Bounds.Width, 1);
    }

    [Fact]
    public async Task When_The_Page_Explains_Touch_The_Features_Name_The_Touch_Gestures()
    {
        var touch = await RuntimeTestHelpers.AttachAsync(new FlowKanbanExample(touchHelp: true));
        var touchFeatures = touch.FindControl<TextBlock>("FeaturesText")!.Text!;
        Assert.Contains("Tap a card to select or unselect it, double-tap it to edit", touchFeatures);
        Assert.Contains("Drag a selected card with your finger", touchFeatures);
        Assert.DoesNotContain("Click", touchFeatures);

        var desktop = await RuntimeTestHelpers.AttachAsync(new FlowKanbanExample(touchHelp: false));
        Assert.Contains("Double-click any card", desktop.FindControl<TextBlock>("FeaturesText")!.Text);
    }

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
