using Avalonia.Controls;
using Avalonia.Layout;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Examples;
using Flowery.NET.Kanban.Controls;
using Xunit;
using static Flowery.NET.RuntimeTests.Gallery.GalleryTestHelpers;

namespace Flowery.NET.RuntimeTests.Gallery;

public class Given_KanbanPage
{
    [Fact]
    public async Task When_The_Page_Has_A_Collapsible_Intro_The_Title_Bar_Folds_The_Description_And_The_Features()
    {
        var mainView = await RuntimeTestHelpers.AttachAsync(new MainView(collapsesIntros: true));
        var bar = mainView.FindControl<Border>("CategoryTitleBar")!;

        var page = await NavigateAsync(mainView, static item => item.TabHeader == "Sidebar_FlowKanban");
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
        Assert.Contains("Tap a card to edit", touchFeatures);
        Assert.Contains("Hold a card to select it, and drag it with your finger", touchFeatures);
        Assert.DoesNotContain("Click", touchFeatures);

        var desktop = await RuntimeTestHelpers.AttachAsync(new FlowKanbanExample(touchHelp: false));
        Assert.Contains("Double-click any card", desktop.FindControl<TextBlock>("FeaturesText")!.Text);
    }
}
