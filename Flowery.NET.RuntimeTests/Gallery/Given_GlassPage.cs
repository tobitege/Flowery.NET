using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Flowery.Controls;
using Flowery.NET.Gallery.Examples;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>The Glass page of the Gallery: the moving ball behind the glass button and the Glass Interface demo.</summary>
public class Given_GlassPage
{
    [Fact]
    public async Task When_The_Page_Is_Hidden_The_Ball_Stops_Asking_And_Moves_On_When_It_Is_Shown()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new GlassExamples());
        var transform = Assert.IsType<TranslateTransform>(page.FindControl<Border>("GlassDemoBall")!.RenderTransform);
        await RuntimeTestHelpers.WaitUntilAsync(() => transform.X != 0, "the ball moves");

        // The Gallery keeps a page it showed once and only hides it. Regression: the hidden ball looked again four times
        // a second for as long as the page stayed hidden.
        page.IsVisible = false;
        await RuntimeTestHelpers.WaitForRenderAsync();
        await RuntimeTestHelpers.WaitForRenderAsync();
        var stopped = transform.X;
        var runs = page.BallLoopRuns;
        await Task.Delay(600);
        Assert.Equal(runs, page.BallLoopRuns);
        Assert.Equal(stopped, transform.X);

        page.IsVisible = true;
        await RuntimeTestHelpers.WaitUntilAsync(() => transform.X != stopped, "the shown ball moves on");
    }

    [Fact]
    public async Task When_A_Menu_Item_Is_Chosen_The_Menu_Closes_And_Keeps_No_Selection()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new GlassExamples());
        var popup = page.FindControl<Popup>("GlassInterfaceMenuPopup")!;
        var menu = page.FindControl<DaisyMenu>("GlassInterfaceMenu")!;

        page.FindControl<Button>("GlassInterfaceMoreButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.True(popup.IsOpen);

        // The arrow keys only move through the menu.
        menu.SelectedIndex = 1;
        menu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
        Assert.True(popup.IsOpen);

        menu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.False(popup.IsOpen);
        Assert.Null(menu.SelectedItem);
    }

    [Fact]
    public async Task When_The_Glass_Interface_Switches_To_Light_The_Sky_The_Stage_And_The_Menu_Follow()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new GlassExamples());
        var toggle = page.FindControl<DaisyToggle>("GlassInterfaceDarkToggle")!;
        var stage = page.FindControl<Border>("GlassInterfaceStage")!;
        var menu = page.FindControl<DaisyMenu>("GlassInterfaceMenu")!;
        var darkSky = page.FindControl<Viewbox>("GlassInterfaceDarkSky")!;
        var lightSky = page.FindControl<Viewbox>("GlassInterfaceLightSky")!;
        Assert.True(toggle.IsChecked);

        // The skies fade into each other.
        toggle.IsChecked = false;
        await RuntimeTestHelpers.WaitUntilAsync(() => (darkSky.Opacity, lightSky.Opacity) == (0, 1), "the light sky is shown");
        Assert.True(stage.Classes.Contains("light"));
        Assert.True(menu.Classes.Contains("light"));

        toggle.IsChecked = true;
        await RuntimeTestHelpers.WaitUntilAsync(() => (darkSky.Opacity, lightSky.Opacity) == (1, 0), "the dark sky is shown");
        Assert.False(stage.Classes.Contains("light"));
        Assert.False(menu.Classes.Contains("light"));
    }
}
