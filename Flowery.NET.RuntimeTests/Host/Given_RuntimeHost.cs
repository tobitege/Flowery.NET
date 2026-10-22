using Avalonia.Layout;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Host;

public class Given_RuntimeHost
{
    [Fact]
    public void When_The_Run_Starts_The_Window_Hosts_The_Test_Panel()
    {
        var window = RuntimeTestHelpers.GetWindow();

        Assert.True(window.IsVisible);
        Assert.Same(RuntimeTestHelpers.GetHostPanel(), window.Content);
        Assert.Equal(RuntimeTestRunner.BaseTheme, DaisyThemeManager.CurrentThemeName);
    }

    [Fact]
    public async Task When_A_Primary_Button_Is_Rendered_Its_Fill_Uses_The_Primary_Color()
    {
        var button = await RuntimeTestHelpers.AttachAsync(new DaisyButton
        {
            Variant = DaisyButtonVariant.Primary,
            Content = "Runtime",
            Width = 160,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        var expected = RuntimeTestHelpers.GetThemeColor(button, "DaisyPrimaryColor");

        using var pixels = RuntimeTestHelpers.Render(button, nameof(When_A_Primary_Button_Is_Rendered_Its_Fill_Uses_The_Primary_Color));
        // Sample the fill between the rounded corner and the label.
        var actual = pixels.GetPixel(12, pixels.Height / 2);

        Assert.True(RuntimeTestHelpers.IsClose(actual, expected), $"Expected the primary color {expected}, but the button fill is {actual}.");
    }
}
