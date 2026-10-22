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
        // Buttons whose color did not change in a switch, per theme.
        var unobserved = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        // The pinned base theme is on screen before the first switch.
        var previous = ReadPalette();
        foreach (var theme in themes)
        {
            if (await CheckThemeAsync(theme) is not { } palette)
                continue;
            for (var index = 0; index < palette.Length; index++)
            {
                if (IsSameColor(previous[index], palette[index]))
                    GetOrAdd(unobserved, theme).Add(index);
            }

            palettes[theme] = palette;
            previous = palette;
        }

        // A switch that keeps a button's color changes none of its pixels and proves nothing for that button,
        // so each such button is checked again right after a theme that gives it another color.
        foreach (var (theme, indices) in unobserved)
        {
            while (indices.Count > 0)
            {
                var other = FindOtherColor(palettes, indices[0], palettes[theme][indices[0]]);
                if (other is null)
                {
                    // Every theme gives this button the same color; no switch can show it.
                    indices.RemoveAt(0);
                    continue;
                }

                await CheckThemeAsync(other);
                await CheckThemeAsync(theme);
                for (var position = indices.Count - 1; position >= 0; position--)
                {
                    if (!IsSameColor(palettes[other][indices[position]], palettes[theme][indices[position]]))
                        indices.RemoveAt(position);
                }
            }
        }

        Assert.True(_failures.Length == 0, _failures.ToString());
    }

    private Color[] ReadPalette()
    {
        var palette = new Color[_buttons.Length];
        for (var index = 0; index < _buttons.Length; index++)
            palette[index] = RuntimeTestHelpers.GetThemeColor(_buttons[index], ColoredButtons[index].ColorKey);
        return palette;
    }

    private static List<int> GetOrAdd(Dictionary<string, List<int>> lists, string key)
    {
        if (!lists.TryGetValue(key, out var list))
            lists[key] = list = [];
        return list;
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

    /// <summary>Returns a theme that gives the button at <paramref name="index"/> a color other than <paramref name="color"/>.</summary>
    private static string? FindOtherColor(Dictionary<string, Color[]> palettes, int index, Color color)
    {
        foreach (var (theme, palette) in palettes)
        {
            if (!IsSameColor(palette[index], color))
                return theme;
        }

        return null;
    }

    private static bool IsSameColor(Color first, Color second) =>
        Math.Abs(first.R - second.R) <= Tolerance
        && Math.Abs(first.G - second.G) <= Tolerance
        && Math.Abs(first.B - second.B) <= Tolerance;
}
