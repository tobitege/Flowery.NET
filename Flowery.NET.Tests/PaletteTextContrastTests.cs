using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Flowery.Controls;
using Flowery.Helpers;
using Xunit;
using static Flowery.NET.Tests.TestControls;

namespace Flowery.NET.Tests;

/// <summary>
/// The muted text that DaisyThemeManager derives where a palette leaves it below 4.5:1, and the text colors on the accent
/// colors, which every palette keeps as it has them.
/// </summary>
public class PaletteTextContrastTests
{
    private const double TextContrast = 4.5;

    private static readonly string[] AccentNames = ["Primary", "Secondary", "Accent", "Neutral", "Info", "Success", "Warning", "Error"];

    [AvaloniaFact]
    public void Every_Palette_Keeps_Its_Text_Colors_On_Its_Accent_Colors()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            var themes = BuiltInThemes();
            Assert.Equal(37, themes.Count);
            foreach (var theme in themes)
            {
                var palette = DaisyThemeManager.GetPaletteFactory(theme)!();
                Assert.True(DaisyThemeManager.ApplyTheme(theme));
                foreach (var name in AccentNames)
                {
                    var content = ThemeColor($"Daisy{name}ContentColor");
                    Assert.Equal(PaletteColor(palette, $"Daisy{name}ContentColor"), content);
                    Assert.Equal(content, Assert.IsAssignableFrom<ISolidColorBrush>(Resource($"Daisy{name}ContentBrush")).Color);
                }
            }
        }
        finally
        {
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Muted_Text_Of_Every_Palette_Is_Readable_And_Stays_At_Seventy_Percent_Where_That_Is_Enough()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var raised = new List<string>();
        try
        {
            foreach (var theme in BuiltInThemes())
            {
                var palette = DaisyThemeManager.GetPaletteFactory(theme)!();
                Assert.True(DaisyThemeManager.ApplyTheme(theme));
                var content = PaletteColor(palette, "DaisyBaseContentColor");
                Color[] surfaces = [PaletteColor(palette, "DaisyBase100Color"), PaletteColor(palette, "DaisyBase200Color")];
                var muted = MutedBrush();
                Assert.Equal(content, muted.Color);
                if (surfaces.All(surface => Readable(content, DaisyThemeManager.MutedTextOpacity, surface)))
                {
                    Assert.Equal(DaisyThemeManager.MutedTextOpacity, muted.Opacity);
                    continue;
                }
                AssertJustReadable(theme, muted, surfaces);
                raised.Add($"{theme} {muted.Opacity:0.00}");
            }
            Assert.NotEmpty(raised);
        }
        finally
        {
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
            TestContext.Current.TestOutputHelper?.WriteLine(string.Join(Environment.NewLine, raised));
        }
    }

    [AvaloniaFact]
    public void The_Muted_Text_Follows_A_Base_Override_And_Gives_Way_To_High_Contrast()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var previousContrast = DaisyAccessibility.HighContrastOverride;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            Assert.Equal(DaisyThemeManager.MutedTextOpacity, MutedBrush().Opacity);

            // A grey base leaves the base text at the least contrast it may have, where 70 percent is too faint.
            DaisyThemeManager.BaseOverride = Color.Parse("#8A8A8A");
            var muted = MutedBrush();
            Assert.Equal(ThemeColor("DaisyBaseContentColor"), muted.Color);
            AssertJustReadable("Light on grey", muted, [ThemeColor("DaisyBase100Color"), ThemeColor("DaisyBase200Color")]);

            DaisyAccessibility.HighContrastOverride = true;
            Assert.Equal(1, MutedBrush().Opacity);
            Assert.Equal(ThemeColor("DaisyBaseContentColor"), MutedBrush().Color);
            DaisyAccessibility.HighContrastOverride = false;

            DaisyThemeManager.BaseOverride = null;
            Assert.Equal(DaisyThemeManager.MutedTextOpacity, MutedBrush().Opacity);
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = previousContrast;
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    // Readable at its opacity, and not one step below it: the opacity rose only as far as needed.
    private static void AssertJustReadable(string theme, ISolidColorBrush muted, Color[] surfaces)
    {
        Assert.True(muted.Opacity is > DaisyThemeManager.MutedTextOpacity and <= 1, $"{theme}: opacity {muted.Opacity}");
        Assert.All(surfaces, surface => Assert.True(Readable(muted.Color, muted.Opacity, surface) || muted.Opacity == 1,
            $"{theme}: muted text at {muted.Opacity} on {surface}"));
        Assert.Contains(surfaces, surface => !Readable(muted.Color, Math.Round(muted.Opacity - 0.01, 2), surface));
    }

    private static bool Readable(Color text, double opacity, Color surface) =>
        FloweryColorHelpers.GetContrastRatio(FloweryColorHelpers.Composite(surface, text, opacity), surface) >= TextContrast;

    // The themes of the library, one per palette file, not the product palettes or themes a test registered.
    private static List<string> BuiltInThemes()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !Directory.Exists(Path.Combine(folder.FullName, "Flowery.NET", "Themes", "Palettes")))
            folder = folder.Parent;
        Assert.NotNull(folder);
        var themes = Directory.GetFiles(Path.Combine(folder.FullName, "Flowery.NET", "Themes", "Palettes"), "Daisy*.axaml")
            .Select(static file => Path.GetFileNameWithoutExtension(file)["Daisy".Length..]).Order().ToList();
        Assert.All(themes, static theme => Assert.NotNull(DaisyThemeManager.GetPaletteFactory(theme)));
        return themes;
    }

    private static ISolidColorBrush MutedBrush() => Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBaseContentMutedBrush"));

    private static Color PaletteColor(ResourceDictionary palette, string key) =>
        palette.TryGetResource(key, ThemeVariant.Default, out var value) ? Assert.IsType<Color>(value) : ThemeColor(key);
}
