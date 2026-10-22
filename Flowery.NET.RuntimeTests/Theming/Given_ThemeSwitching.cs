using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Theming;

public class Given_ThemeSwitching
{
    private const int Tolerance = 2;

    private static readonly (DaisyButtonVariant Variant, string ColorKey)[] ColoredButtons =
    [
        (DaisyButtonVariant.Primary, "DaisyPrimaryColor"),
        (DaisyButtonVariant.Secondary, "DaisySecondaryColor"),
        (DaisyButtonVariant.Accent, "DaisyAccentColor")
    ];

    private readonly StringBuilder _failures = new();
    private readonly DaisyButton[] _buttons = new DaisyButton[ColoredButtons.Length];
    private readonly StackPanel _panel = new()
    {
        Spacing = 8,
        Width = 320,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    [Fact]
    public async Task When_Switching_Through_All_Themes_The_Controls_Render_The_New_Palette()
    {
        for (var index = 0; index < ColoredButtons.Length; index++)
        {
            var variant = ColoredButtons[index].Variant;
            _buttons[index] = new DaisyButton { Variant = variant, Content = variant.ToString(), Width = 160, Height = 48 };
        }

        _panel.Children.AddRange(_buttons);
        // Representative controls that follow the palette; they only need to render without exceptions.
        _panel.Children.AddRange(
        [
            new DaisyCard { Content = new TextBlock { Text = "Card" } },
            new DaisyInput { PlaceholderText = "Input" },
            new DaisyBadge { Content = "Badge", Variant = DaisyBadgeVariant.Secondary },
            new DaisyToggle { IsChecked = true },
            new DaisyAlert { Content = "Alert", Variant = DaisyAlertVariant.Info },
            new DaisyProgress { Value = 60 }
        ]);
        await RuntimeTestHelpers.AttachAsync(_panel);

        var themes = DaisyThemeManager.AvailableThemes.Select(theme => theme.Name).ToList();
        Assert.NotEmpty(themes);
        var palettes = new Dictionary<string, Color[]>(StringComparer.OrdinalIgnoreCase);
        var unobserved = new List<string>();
        Color[]? previous = null;
        foreach (var theme in themes)
        {
            if (await CheckThemeAsync(theme) is not { } palette)
                continue;
            if (previous is not null && IsSamePalette(previous, palette))
                unobserved.Add(theme);
            palettes[theme] = palette;
            previous = palette;
        }

        // A switch between two themes with the same button colors changes no pixel and proves nothing,
        // so these themes are checked again right after a theme with other colors.
        foreach (var theme in unobserved)
        {
            if (FindOtherPalette(palettes, palettes[theme]) is not { } other)
                continue;
            await CheckThemeAsync(other);
            await CheckThemeAsync(theme);
        }

        Assert.True(_failures.Length == 0, _failures.ToString());
    }

    /// <summary>
    /// Applies <paramref name="theme"/> and compares the rendered button fills with its palette.
    /// Returns the palette's button colors, or null when the theme failed.
    /// </summary>
    private async Task<Color[]?> CheckThemeAsync(string theme)
    {
        try
        {
            if (!DaisyThemeManager.ApplyTheme(theme))
            {
                _failures.AppendLine($"{theme}: the theme could not be applied.");
                return null;
            }

            await RuntimeTestHelpers.WaitForRenderAsync();
            var palette = new Color[_buttons.Length];
            for (var index = 0; index < _buttons.Length; index++)
            {
                var button = _buttons[index];
                palette[index] = RuntimeTestHelpers.GetThemeColor(button, ColoredButtons[index].ColorKey);
                // Sample the fill between the rounded corner and the label.
                var actual = await RuntimeTestHelpers.WaitForPixelAsync(button, 12, (int)(button.Bounds.Height / 2), palette[index]);
                if (!RuntimeTestHelpers.IsClose(actual, palette[index], Tolerance))
                {
                    _failures.AppendLine($"{theme}: the {ColoredButtons[index].Variant} button fill is {actual} instead of {palette[index]}.");
                    using var _ = RuntimeTestHelpers.Render(_panel, $"theme-{theme}");
                }
            }

            using var __ = RuntimeTestHelpers.Render(_panel);
            return palette;
        }
        catch (Exception exception)
        {
            _failures.AppendLine($"{theme}: {exception.GetType().Name} - {exception.Message}");
            return null;
        }
    }

    private static string? FindOtherPalette(Dictionary<string, Color[]> palettes, Color[] palette)
    {
        foreach (var (theme, candidate) in palettes)
        {
            if (!IsSamePalette(candidate, palette))
                return theme;
        }

        return null;
    }

    private static bool IsSamePalette(Color[] first, Color[] second)
    {
        for (var index = 0; index < first.Length; index++)
        {
            if (Math.Abs(first[index].R - second[index].R) > Tolerance
                || Math.Abs(first[index].G - second[index].G) > Tolerance
                || Math.Abs(first[index].B - second[index].B) > Tolerance)
            {
                return false;
            }
        }

        return true;
    }
}
