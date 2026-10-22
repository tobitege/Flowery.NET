using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
        Assert.True(button.TryFindResource("DaisyPrimaryColor", button.ActualThemeVariant, out var resource));
        var expected = Assert.IsType<Color>(resource);

        using var pixels = RuntimeTestHelpers.Render(button, nameof(When_A_Primary_Button_Is_Rendered_Its_Fill_Uses_The_Primary_Color));
        // Sample the fill between the rounded corner and the label.
        var actual = pixels.GetPixel(12, pixels.Height / 2);

        Assert.True(Math.Abs(actual.Red - expected.R) <= 2 && Math.Abs(actual.Green - expected.G) <= 2
                    && Math.Abs(actual.Blue - expected.B) <= 2,
            $"Expected the primary color {expected}, but the button fill is {actual}.");
    }
}
