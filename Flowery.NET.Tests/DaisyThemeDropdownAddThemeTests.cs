using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyThemeDropdownAddThemeTests
{
    [AvaloniaFact]
    public void Selection_Uses_Original_Name_And_Renders_The_Alias()
    {
        using var scope = new ThemeScope();
        var combo = new DaisyThemeDropdown();
        combo.AddTheme("TheaterCinema", "DarkAndRed", true);
        scope.Show(combo);
        var entry = StandardItems(combo).Single(static item => item.Name == "TheaterCinema");
        var expectedPrimary = Assert.IsAssignableFrom<ISolidColorBrush>(entry.Primary).Color;
        combo.SelectedItem = entry;
        Dispatcher.UIThread.RunJobs();
        scope.Window.UpdateLayout();

        Assert.Equal("TheaterCinema", DaisyThemeManager.CurrentThemeName);
        Assert.Equal("TheaterCinema", combo.SelectedTheme);
        var selected = Assert.IsType<ThemePreviewInfo>(combo.SelectedItem);
        Assert.Same(StandardItems(combo).Single(static item => item.Name == "TheaterCinema"), selected);
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Null(DaisyThemeManager.GetThemeInfo("DarkAndRed"));
        Assert.True(DaisyThemeManager.TryCreatePalette("TheaterCinema", out var palette));
        Assert.NotNull(palette);
        Assert.True(palette.TryGetResource("DaisyPrimaryBrush", null, out var primary));
        Assert.Equal(expectedPrimary, Assert.IsAssignableFrom<ISolidColorBrush>(primary).Color);
        Assert.Contains(combo.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "DarkAndRed");

        combo.AddTheme("TheaterCinema", "Renamed cinema", false);
        Dispatcher.UIThread.RunJobs();
        scope.Window.UpdateLayout();
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Equal("TheaterCinema", combo.SelectedTheme);
        Assert.Contains(combo.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "Renamed cinema");
    }

    private static ThemePreviewInfo[] StandardItems(DaisyThemeDropdown combo) =>
        [.. Assert.IsAssignableFrom<IEnumerable<ThemePreviewInfo>>(combo.ItemsSource)];

    private sealed class ThemeScope() : ThemeTestScope(600, 350)
    {
        public void Show(params Control[] controls)
        {
            if (controls.Length == 1)
                Window.Content = controls[0];
            else
            {
                var panel = new StackPanel();
                panel.Children.AddRange(controls);
                Window.Content = panel;
            }
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
