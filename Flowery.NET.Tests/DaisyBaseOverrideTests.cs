using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Flowery.Controls;
using Flowery.Helpers;
using Xunit;
using static Flowery.NET.Tests.TestControls;

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyThemeManager.BaseOverride, which changes the base of any theme at run time, and FindContrastGaps.
/// </summary>
public class DaisyBaseOverrideTests
{
    [AvaloniaFact]
    public void A_Base_Override_Takes_Base100_And_The_Other_Base_Colors_Keep_Their_Distance()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            var original = Bases();

            var light = Color.Parse("#E0F2FE");
            DaisyThemeManager.BaseOverride = light;
            var changed = Bases();
            Assert.Equal(light, changed.Base100);
            Assert.Equal(light, Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBase100Brush")).Color);
            Assert.False(DaisyThemeManager.IsCurrentThemeDark);
            // A SolidColorBrush like those of the palettes, which some controls read their colors from.
            Assert.IsType<SolidColorBrush>(Resource("DaisyBase300Brush"));
            Assert.IsType<SolidColorBrush>(Resource("DaisyPrimaryForegroundBrush"));

            // The same contrast ratio and the same direction: darker, like in Light.
            AssertKeeps(original.Base200, original.Base100, changed.Base200, changed.Base100);
            AssertKeeps(original.Base300, original.Base100, changed.Base300, changed.Base100);
            AssertKeeps(original.Content, original.Base100, changed.Content, changed.Base100);
            Assert.Equal(changed.Content, Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBaseContentBrush")).Color);

            DaisyThemeManager.BaseOverride = null;
            Assert.Equal(original, Bases());
        }
        finally
        {
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Text_Colors_Of_The_Accents_Reach_4_5_And_The_Accent_Surfaces_Stay()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            var primary = ThemeColor("DaisyPrimaryColor");
            var content = ThemeColor("DaisyPrimaryContentColor");

            DaisyThemeManager.BaseOverride = Colors.White;
            foreach (var key in new[] { "DaisyPrimaryForegroundBrush", "DaisySecondaryForegroundBrush", "DaisyAccentForegroundBrush", "DaisyButtonPrimaryForegroundBrush" })
            {
                var text = Assert.IsAssignableFrom<ISolidColorBrush>(Resource(key)).Color;
                Assert.True(FloweryColorHelpers.GetContrastRatio(text, Colors.White) >= 4.5, $"{key} {text}");
            }
            Assert.True(FloweryColorHelpers.GetContrastRatio(ThemeColor("DaisyPrimaryForegroundColor"), Colors.White) >= 4.5);
            Assert.Equal(primary, ThemeColor("DaisyPrimaryColor"));
            Assert.Equal(content, ThemeColor("DaisyPrimaryContentColor"));
        }
        finally
        {
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Theme_Swap_Still_Reaches_The_Other_Theme_Under_A_Base_Of_The_Other_Kind()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var swap = new DaisyThemeSwap { LightTheme = "Light", DarkTheme = "Dark" };
        var window = new Window { Width = 200, Height = 100, Content = swap };
        window.Show();
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            // A light base makes Dark look light, but the swap still goes to Light and back.
            DaisyThemeManager.BaseOverride = Colors.White;
            swap.PointerClick();
            Assert.Equal("Light", DaisyThemeManager.CurrentThemeName);
            swap.PointerClick();
            Assert.Equal("Dark", DaisyThemeManager.CurrentThemeName);
        }
        finally
        {
            window.Close();
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    private readonly record struct BaseColors(Color Base100, Color Base200, Color Base300, Color Content);

    private static BaseColors Bases() => new(
        ThemeColor("DaisyBase100Color"), ThemeColor("DaisyBase200Color"), ThemeColor("DaisyBase300Color"), ThemeColor("DaisyBaseContentColor"));

    // The derived color has the contrast ratio of the original to its base (at least 4.5 for text), on the same side of
    // the base relative to the text.
    private static void AssertKeeps(Color original, Color originalBase, Color derived, Color derivedBase)
    {
        var ratio = FloweryColorHelpers.GetContrastRatio(original, originalBase);
        var derivedRatio = FloweryColorHelpers.GetContrastRatio(derived, derivedBase);
        Assert.True(Math.Abs(ratio - derivedRatio) < 0.08, $"{derived} has {derivedRatio:0.00}:1 to {derivedBase}, {original} had {ratio:0.00}:1.");
        Assert.Equal(Luminance(original) > Luminance(originalBase), Luminance(derived) > Luminance(derivedBase));
    }

    private static double Luminance(Color color) => FloweryColorHelpers.GetRelativeLuminance(color);
}
