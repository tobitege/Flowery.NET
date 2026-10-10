using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyMenuTitleTests
{
    [AvaloniaFact]
    public void Title_Is_Distinct_From_Disabled_Items_And_Follows_Size()
    {
        var title = new ListBoxItem { Content = "Section", Classes = { "menu-title" } };
        var disabled = new ListBoxItem { Content = "Disabled", IsEnabled = false };
        var item = new ListBoxItem { Content = "Item" };
        var menu = new DaisyMenu { Items = { title, disabled, item } };
        var window = new Window { Width = 300, Height = 300, Content = menu };
        try
        {
            window.Show();
            Layout(window);
            Assert.Equal(1.0, title.Opacity);
            Assert.Equal(0.5, disabled.Opacity);
            Assert.True(menu.TryFindResource("DaisyPrimaryBrush", menu.ActualThemeVariant, out var primary));
            Assert.Same(primary, title.Foreground);
            Assert.NotEqual(title.Foreground, disabled.Foreground);
            Assert.Equal(new Avalonia.Thickness(0, 0, 0, 1), title.BorderThickness);
            Assert.Equal(Token(menu, "DaisySizeMediumSecondaryFontSize"), title.FontSize);
            Assert.Equal(Token(menu, "DaisyMenuMediumFontSize"), item.FontSize);

            menu.Size = DaisySize.ExtraLarge;
            Layout(window);
            Assert.Equal(Token(menu, "DaisySizeExtraLargeSecondaryFontSize"), title.FontSize);
            Assert.Equal(Token(menu, "DaisyMenuExtraLargeFontSize"), item.FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    private static double Token(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, out var value));
        return Assert.IsType<double>(value);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
