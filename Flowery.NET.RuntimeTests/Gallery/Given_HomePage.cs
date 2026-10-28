using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Examples;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>
/// The home page and the header of the Gallery at the width of a phone, such as the 402 DIPs of an iPhone 17.
/// </summary>
public class Given_HomePage
{
    private const double PhoneWidth = 402;

    [Fact]
    public async Task When_The_Page_Is_As_Wide_As_A_Phone_Nothing_Reaches_Past_Its_Right_Edge()
    {
        var page = new HomePage();
        await RuntimeTestHelpers.AttachAsync(Host(page, PhoneWidth));

        foreach (var name in new[] { "BrowseBtn", "GitHubBtn", "FloweryCard", "AvaloniaCard" })
        {
            var control = page.FindControl<Control>(name);
            Assert.NotNull(control);
            var right = control.TranslatePoint(new Point(control.Bounds.Width, 0), page)?.X;
            Assert.True(right <= PhoneWidth, $"{name} ends at {right}, past the width of the page, {PhoneWidth}.");
        }
    }

    [Fact]
    public async Task When_The_Page_Is_Narrower_Than_Its_Padding_Its_Cards_Keep_A_Width()
    {
        // While the sidebar of the Gallery folds away, the page is this narrow for a moment; a negative card width
        // threw and ended the app.
        var page = new HomePage();
        var host = Host(page, 40);
        await RuntimeTestHelpers.AttachAsync(host);
        var card = page.FindControl<Control>("FloweryCard");
        Assert.NotNull(card);
        Assert.True(card.Width > 0, $"The card is {card.Width} wide on a page of 40.");

        host.Width = PhoneWidth;
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.True(card.Width > 120 && card.Width + card.Margin.Left + card.Margin.Right <= PhoneWidth,
            $"The card is {card.Width} wide on a page of {PhoneWidth}.");
    }

    [Fact]
    public async Task When_The_Gallery_Is_As_Wide_As_A_Phone_Its_Title_Wraps_Instead_Of_Running_Out()
    {
        var mainView = new MainView();
        await RuntimeTestHelpers.AttachAsync(Host(mainView, PhoneWidth));

        var title = mainView.FindControl<TextBlock>("HeaderTitle");
        Assert.NotNull(title);
        Assert.True(title.TextLayout.Width <= title.Bounds.Width + 0.5,
            $"The title needs {title.TextLayout.Width} but has {title.Bounds.Width}.");
        Assert.True(title.TextLayout.TextLines.Count > 1, "The title of the Gallery does not wrap at the width of a phone.");
    }

    private static Border Host(Control content, double width) =>
        new() { Width = width, HorizontalAlignment = HorizontalAlignment.Left, Child = content };
}
