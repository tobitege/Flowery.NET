using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Helpers;
using Xunit;

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
    public void A_Dark_Base_Turns_A_Light_Theme_Dark_With_Light_Text_And_Lighter_Borders()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var app = Application.Current!;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);

            DaisyThemeManager.BaseOverride = Color.Parse("#101828");
            Assert.True(DaisyThemeManager.IsCurrentThemeDark);
            Assert.True(DaisyThemeManager.IsDarkTheme("Light"));
            Assert.False(DaisyThemeManager.IsDarkTheme("Cupcake"));
            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);

            // Base300 lies toward the text in Light, so on a dark base it is lighter, as in the dark themes.
            var bases = Bases();
            Assert.True(Luminance(bases.Content) > Luminance(bases.Base100));
            Assert.True(Luminance(bases.Base300) > Luminance(bases.Base100));
            Assert.True(FloweryColorHelpers.GetContrastRatio(bases.Content, bases.Base100) >= 4.5);

            DaisyThemeManager.BaseOverride = null;
            Assert.False(DaisyThemeManager.IsCurrentThemeDark);
            Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);
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
    public void A_Theme_Switch_Keeps_The_Override_And_Computes_It_Again()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var changes = 0;
        var themeChanges = new List<string>();
        void CountOverride(object? sender, EventArgs e) => changes++;
        void CountTheme(object? sender, string name) => themeChanges.Add(name);
        DaisyThemeManager.BaseOverrideChanged += CountOverride;
        DaisyThemeManager.ThemeChanged += CountTheme;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            themeChanges.Clear();
            var navy = Color.Parse("#0B1B5E");
            DaisyThemeManager.BaseOverride = navy;
            Assert.Equal(1, changes);
            // Code that read the colors of the theme reads them again, as after a theme switch.
            Assert.Equal(["Dark"], themeChanges);

            Assert.True(DaisyThemeManager.ApplyTheme("Corporate"));
            Assert.Equal(navy, DaisyThemeManager.BaseOverride);
            var bases = Bases();
            Assert.Equal(navy, bases.Base100);
            Assert.True(DaisyThemeManager.IsCurrentThemeDark);
            Assert.True(Luminance(bases.Content) > Luminance(bases.Base100));

            // An alpha would make the base see-through, so the override is opaque.
            DaisyThemeManager.BaseOverride = Color.Parse("#800B1B5E");
            Assert.Equal(navy, DaisyThemeManager.BaseOverride);
            Assert.Equal(1, changes);
        }
        finally
        {
            DaisyThemeManager.BaseOverrideChanged -= CountOverride;
            DaisyThemeManager.ThemeChanged -= CountTheme;
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Glass_Tint_Follows_The_New_Base_Also_For_A_Theme_With_Its_Own_Tint()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            var themeBase = ThemeColor("DaisyBase100Color");
            var themeTint = ThemeColor("DaisyGlassTintColor");

            var teal = Color.Parse("#0F3D3E");
            DaisyThemeManager.BaseOverride = teal;
            var tint = ThemeColor("DaisyGlassTintColor");
            // Whole RGB values shift the hue of a dark color a little.
            Assert.InRange(Math.Abs(new HslHue(teal).Hue - new HslHue(tint).Hue), 0, 4);
            AssertKeeps(themeTint, themeBase, tint, teal);

            // In the glass mode a theme without a tint of its own takes the new base as tint.
            Assert.True(DaisyThemeManager.ApplyTheme("Dracula"));
            DaisyGlassSurface.GlobalEnabled = true;
            Assert.Equal(teal, ThemeColor("DaisyGlassTintColor"));
        }
        finally
        {
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Backdrop_Of_A_Theme_Gives_Way_To_A_Base_Of_The_Other_Kind()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));

            // A dark base keeps the dark wave of Glassy.
            DaisyThemeManager.BaseOverride = Color.Parse("#111111");
            Assert.IsType<DrawingBrush>(Resource("DaisyBackdropBrush"));

            // On a light base the page text turns dark, which the dark wave would swallow.
            var light = Color.Parse("#FFF7ED");
            DaisyThemeManager.BaseOverride = light;
            Assert.Equal(light, Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBackdropBrush")).Color);
            DaisyGlassSurface.GlobalEnabled = true;
            Assert.Equal(light, Assert.IsAssignableFrom<ISolidColorBrush>(
                Assert.IsType<DrawingGroup>(Assert.IsType<DrawingBrush>(Resource("DaisyBackdropBrush")).Drawing).Children
                    .OfType<GeometryDrawing>().First().Brush).Color);
        }
        finally
        {
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Contrast_Gaps_Name_The_Accent_Surfaces_Too_Pale_For_A_Very_Light_Base()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            Assert.DoesNotContain(DaisyThemeManager.FindContrastGaps(), gap => gap.Foreground == "DaisyBaseContentColor");

            DaisyThemeManager.BaseOverride = Colors.White;
            var gaps = DaisyThemeManager.FindContrastGaps();
            var warning = Assert.Single(gaps, gap => gap.Foreground == "DaisyWarningColor" && gap.Background == "DaisyBase100Color");
            Assert.Equal(3, warning.Required);
            Assert.True(warning.Ratio < 3);
            Assert.Equal(FloweryColorHelpers.GetContrastRatio(ThemeColor("DaisyWarningColor"), Colors.White), warning.Ratio, 3);
            // The base text and the text colors of the accents were moved, so they are no gap.
            Assert.DoesNotContain(gaps, gap => gap.Foreground is "DaisyBaseContentColor" or "DaisyPrimaryForegroundBrush");
        }
        finally
        {
            DaisyThemeManager.BaseOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void The_Sidebar_Shows_The_Base_And_Gives_The_Theme_Base_Back()
    {
        var sidebar = new FloweryComponentSidebar();
        var window = new Window { Width = 300, Height = 400, Content = sidebar };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal("Theme", sidebar.BaseColorText);
            Assert.False(sidebar.ResetBaseColorCommand.CanExecute(null));

            DaisyThemeManager.BaseOverride = Color.Parse("#0B1B5E");
            Assert.Equal("#0B1B5E", sidebar.BaseColorText);
            Assert.True(sidebar.ResetBaseColorCommand.CanExecute(null));

            sidebar.ResetBaseColorCommand.Execute(null);
            Assert.Null(DaisyThemeManager.BaseOverride);
            Assert.Equal("Theme", sidebar.BaseColorText);
        }
        finally
        {
            window.Close();
            DaisyThemeManager.BaseOverride = null;
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

    private readonly record struct HslHue(double Hue)
    {
        public HslHue(Color color) : this(new Flowery.Controls.ColorPicker.HslColor(color).H)
        {
        }
    }

    private static Color ThemeColor(string key) => Assert.IsType<Color>(Resource(key));

    private static object? Resource(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) ? value : null;
}
