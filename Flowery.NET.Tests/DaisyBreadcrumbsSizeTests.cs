using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyBreadcrumbsSizeTests
{
    [AvaloniaFact]
    public void Items_And_Separators_Follow_Size()
    {
        var breadcrumbs = new DaisyBreadcrumbs
        {
            Items =
            {
                new DaisyBreadcrumbItem { Content = "Home" },
                new DaisyBreadcrumbItem { Content = "Docs" }
            }
        };
        var window = new Window { Width = 400, Height = 200, Content = breadcrumbs };
        try
        {
            window.Show();
            Layout(window);
            AssertFont(breadcrumbs, "DaisySizeMediumFontSize");

            breadcrumbs.Size = DaisySize.ExtraLarge;
            Layout(window);
            AssertFont(breadcrumbs, "DaisySizeExtraLargeFontSize");

            breadcrumbs.Size = DaisySize.ExtraSmall;
            Layout(window);
            AssertFont(breadcrumbs, "DaisySizeExtraSmallFontSize");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertFont(DaisyBreadcrumbs breadcrumbs, string tokenKey)
    {
        Assert.True(breadcrumbs.TryFindResource(tokenKey, out var token));
        var expected = Assert.IsType<double>(token);
        Assert.Equal(expected, breadcrumbs.FontSize);
        var separator = breadcrumbs.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_Separator" && text.IsVisible);
        Assert.Equal(expected, separator.FontSize);
        var label = breadcrumbs.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Docs");
        Assert.Equal(expected, label.FontSize);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
