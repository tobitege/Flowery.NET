using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Theming;

public class Given_ThemeSwitching
{
    private static readonly (DaisyButtonVariant Variant, string ColorKey)[] ColoredButtons =
    [
        (DaisyButtonVariant.Primary, "DaisyPrimaryColor"),
        (DaisyButtonVariant.Secondary, "DaisySecondaryColor"),
        (DaisyButtonVariant.Accent, "DaisyAccentColor")
    ];

    [Fact]
    public async Task When_Switching_Through_All_Themes_The_Controls_Render_The_New_Palette()
    {
        var buttons = ColoredButtons
            .Select(entry => new DaisyButton { Variant = entry.Variant, Content = entry.Variant.ToString(), Width = 160, Height = 48 })
            .ToArray();
        var panel = new StackPanel
        {
            Spacing = 8,
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.AddRange(buttons);
        // Representative controls that follow the palette; they only need to render without exceptions.
        panel.Children.AddRange(
        [
            new DaisyCard { Content = new TextBlock { Text = "Card" } },
            new DaisyInput { PlaceholderText = "Input" },
            new DaisyBadge { Content = "Badge", Variant = DaisyBadgeVariant.Secondary },
            new DaisyToggle { IsChecked = true },
            new DaisyAlert { Content = "Alert", Variant = DaisyAlertVariant.Info },
            new DaisyProgress { Value = 60 }
        ]);
        await RuntimeTestHelpers.AttachAsync(panel);

        var themes = DaisyThemeManager.AvailableThemes.Select(theme => theme.Name).ToList();
        Assert.NotEmpty(themes);
        var failures = new StringBuilder();
        foreach (var theme in themes)
        {
            try
            {
                if (!DaisyThemeManager.ApplyTheme(theme))
                {
                    failures.AppendLine($"{theme}: the theme could not be applied.");
                    continue;
                }

                await RuntimeTestHelpers.WaitForRenderAsync();
                for (var index = 0; index < buttons.Length; index++)
                {
                    var button = buttons[index];
                    var expected = RuntimeTestHelpers.GetThemeColor(button, ColoredButtons[index].ColorKey);
                    // Sample the fill between the rounded corner and the label.
                    var actual = await RuntimeTestHelpers.WaitForPixelAsync(button, 12, (int)(button.Bounds.Height / 2), expected);
                    if (!RuntimeTestHelpers.IsClose(actual, expected))
                    {
                        failures.AppendLine($"{theme}: the {ColoredButtons[index].Variant} button fill is {actual} instead of {expected}.");
                        using var _ = RuntimeTestHelpers.Render(panel, $"theme-{theme}");
                    }
                }

                using var __ = RuntimeTestHelpers.Render(panel);
            }
            catch (Exception exception)
            {
                failures.AppendLine($"{theme}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        Assert.True(failures.Length == 0, failures.ToString());
    }
}
