using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Localization;
using Xunit;
using static Flowery.NET.Tests.TestControls;
using DaisySize = Flowery.Controls.DaisySize;

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyGlassDesign and DaisyGlassDesigner, which describe a glass look for every control on glass.
/// </summary>
public class DaisyGlassDesignTests
{
    [Fact]
    public void A_Design_Survives_Json()
    {
        var design = Custom(DaisyGlassFillKind.Conic);
        var json = design.ToJson();

        Assert.Contains("\"tint\": \"#FF0F172A\"", json);
        Assert.Contains("\"fillKind\": \"Conic\"", json);
        Assert.DoesNotContain("variant", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(json, DaisyGlassDesign.FromJson(json).ToJson());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ \"zoom\": 5 }")]
    public void Json_That_Is_No_Design_Fails_With_A_Json_Exception(string json)
    {
        Assert.Throws<JsonException>(() => DaisyGlassDesign.FromJson(json));
    }

    [AvaloniaFact]
    public async Task A_Design_Saved_To_A_File_Opens_As_The_Same_Design()
    {
        var saved = new DaisyGlassDesigner { Design = Custom(DaisyGlassFillKind.Conic) };
        var window = Show(saved, 1100, 1300);
        try
        {
            // A longer file is replaced, not overwritten in part.
            using var file = new MemoryStream(new byte[4096]);
            await saved.SaveAsync(file);
            Assert.Equal(saved.Json, System.Text.Encoding.UTF8.GetString(file.ToArray()));

            var opened = new DaisyGlassDesigner();
            window.Content = opened;
            file.Position = 0;
            await opened.LoadAsync(file);
            Assert.Equal(saved.Json, opened.Json);

            // A file that is no design leaves the design as it is.
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => opened.LoadAsync(new MemoryStream("{ \"blur\": -5 }"u8.ToArray())));
            Assert.Equal(saved.Json, opened.Json);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Editing_A_Control_Writes_Only_What_Changed()
    {
        var button = new DaisyButton { Content = "Mine", Classes = { "brand" }, Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonTheme") };
        var host = new StackPanel { Children = { button } };
        host.Styles.Add(new Style(static selector => selector.OfType<DaisyButton>().Class("brand"))
        {
            Setters = { new Setter(DaisyGlassSurface.BlurProperty, 12d) }
        });
        var window = Show(host, 1100, 1300);
        try
        {
            var overlay = OverlayLayer.GetOverlayLayer(button)!;
            var edit = DaisyGlassDesigner.EditAsync(button);
            Dispatcher.UIThread.RunJobs();
            var designer = overlay.GetVisualDescendants().OfType<DaisyGlassDesigner>().Single();
            Assert.Equal("Mine", designer.PreviewText);
            OverlayButton(overlay, "GlassDesigner_Apply").PointerClick();
            Assert.True(await edit);

            // Nothing changed, so nothing was written: the style still applies.
            Assert.NotEqual(BindingPriority.LocalValue, button.GetDiagnostic(DaisyGlassSurface.BlurProperty).Priority);
            Assert.NotEqual(BindingPriority.LocalValue, button.GetDiagnostic(DaisyGlassSurface.TintOpacityProperty).Priority);

            edit = DaisyGlassDesigner.EditAsync(button);
            Dispatcher.UIThread.RunJobs();
            designer = overlay.GetVisualDescendants().OfType<DaisyGlassDesigner>().Single();
            var design = designer.Design;
            design.TintOpacity = 0.4;
            designer.Design = design;
            designer.PreviewText = "Not mine";
            OverlayButton(overlay, "GlassDesigner_Apply").PointerClick();
            Assert.True(await edit);
            Assert.Equal(0.4, DaisyGlassSurface.GetTintOpacity(button));
            Assert.Equal("Mine", button.Content);
            Assert.NotEqual(BindingPriority.LocalValue, button.GetDiagnostic(DaisyGlassSurface.BlurProperty).Priority);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Overlay_Returns_The_Design_On_Apply_And_Nothing_On_Cancel_Or_Escape()
    {
        var host = new Border();
        var window = Show(host, 1100, 1300);
        try
        {
            var overlay = OverlayLayer.GetOverlayLayer(host)!;

            var applied = DaisyGlassDesigner.ShowOverlayAsync(host, new DaisyGlassDesign { Blur = 55 });
            Dispatcher.UIThread.RunJobs();
            OverlayButton(overlay, "GlassDesigner_Apply").PointerClick();
            Assert.Equal(55, (await applied)!.Blur);
            Assert.Empty(overlay.Children);

            var cancelled = DaisyGlassDesigner.ShowOverlayAsync(host);
            Dispatcher.UIThread.RunJobs();
            OverlayButton(overlay, "GlassDesigner_Cancel").PointerClick();
            Assert.Null(await cancelled);

            var escaped = DaisyGlassDesigner.ShowOverlayAsync(host);
            Dispatcher.UIThread.RunJobs();
            host.Press(PhysicalKey.Escape);
            Assert.Null(await escaped);
            Assert.Empty(overlay.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Styles_Of_The_Parts_Belong_To_The_Designer_And_Not_To_The_App()
    {
        // Regression: about 45 "DaisyGlassDesigner Border.gbd-root" styles were app styles, matched against every control.
        Assert.DoesNotContain(Selectors(Application.Current!.Styles), static selector => selector.Contains("gbd-", StringComparison.Ordinal));

        using var shown = ShowDesigner(out var designer);
        Assert.Contains(Selectors(designer.Styles), static selector => selector.Contains("gbd-root", StringComparison.Ordinal));
        var root = designer.GetVisualDescendants().OfType<Border>().First(static border => border.Classes.Contains("gbd-root"));
        Assert.Same(shown.Window.FindResource("DaisyBase100Brush"), root.Background);
        Assert.Equal(new CornerRadius(24), root.CornerRadius);

        static IEnumerable<string> Selectors(IEnumerable<IStyle> styles) => styles.SelectMany(static style => style switch
        {
            Style { Selector: { } selector } plain => Selectors(plain.Children).Prepend(selector.ToString()),
            Avalonia.Markup.Xaml.Styling.StyleInclude { Loaded: { } loaded } => Selectors([loaded]),
            Styles nested => Selectors(nested),
            _ => []
        });
    }

    [AvaloniaFact]
    public void Muted_Texts_Use_The_Muted_Brush_And_The_Gradient_Names_Sit_On_A_Dark_Pill()
    {
        // Regression: muted texts were dimmed by opacity below 4.5:1, and the gradient names were white on the bare gradient.
        using var shown = ShowDesigner(out var designer);
        var muted = shown.Window.FindResource("DaisyBaseContentMutedBrush");
        var texts = designer.GetVisualDescendants().OfType<TextBlock>()
            .Where(static text => text.Classes.Contains("gbd-label") || text.Classes.Contains("gbd-hint") || text.Classes.Contains("gbd-subtitle"))
            .ToList();
        Assert.NotEmpty(texts);
        Assert.All(texts, text => Assert.Same(muted, text.Foreground));
        Assert.All(texts, static text => Assert.Equal(1, text.Opacity));

        SelectTab(designer, "Fill");
        Chip(designer, "Preset").IsChecked = true;
        designer.UpdateLayout();
        var pill = Part<Border>(Tiles(designer, "gbd-gradient").First(), "PART_Name");
        Assert.Equal(Color.Parse("#99000000"), Assert.IsAssignableFrom<ISolidColorBrush>(pill.Background).Color);
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(pill.GetVisualDescendants().OfType<TextBlock>().Single().Foreground).Color);
    }

    [AvaloniaFact]
    public void With_High_Contrast_The_Code_Drawer_Stage_Bar_Swatches_And_Error_Alerts_Take_The_Contrast_Palette()
    {
        // Regression: the code drawer, the stage bar, the swatch borders and the overlay kept their fixed colors, and the
        // error text was a fixed light red on the base of the theme, below 4.5:1 on most light themes.
        var previousContrast = DaisyAccessibility.HighContrastOverride;
        DaisyAccessibility.HighContrastOverride = false;
        try
        {
            using var shown = ShowDesigner(out var designer);
            var code = designer.GetVisualDescendants().OfType<Border>().First(static border => border.Classes.Contains("gbd-code"));
            var bar = designer.GetVisualDescendants().OfType<Border>().First(static border => border.Classes.Contains("gbd-stage-bar"));
            var swatch = Part<Border>(Tiles(designer, "gbd-swatch").First(static swatch => swatch.IsChecked != true), "PART_Swatch");
            Assert.Equal(Color.Parse("#0B1020"), SolidColor(code.Background));
            // The file error under the tabs and the import error in the code drawer are alerts on the error surface.
            var alerts = designer.GetLogicalDescendants().OfType<Border>().Where(static border => border.Classes.Contains("gbd-error")).ToList();
            Assert.Equal(2, alerts.Count);
            foreach (var alert in alerts)
            {
                Assert.Same(shown.Window.FindResource("DaisyErrorBrush"), alert.Background);
                Assert.Same(shown.Window.FindResource("DaisyErrorContentBrush"), Assert.IsType<TextBlock>(alert.Child).Foreground);
                Assert.Equal(new CornerRadius(14), alert.CornerRadius);
                Assert.Equal(new Thickness(12, 8), alert.Padding);
            }

            DaisyAccessibility.HighContrastOverride = true;
            Dispatcher.UIThread.RunJobs();
            var background = SolidColor(shown.Window.FindResource("DaisyBase100Brush"));
            var text = SolidColor(shown.Window.FindResource("DaisyBaseContentBrush"));
            Assert.Equal(background, SolidColor(code.Background));
            Assert.Equal(text, SolidColor(code.BorderBrush));
            Assert.Equal(background, SolidColor(bar.Background));
            Assert.Equal(text, SolidColor(swatch.BorderBrush));
            Assert.Equal(text, SolidColor(code.GetLogicalDescendants().OfType<SelectableTextBlock>().First().Foreground));
            foreach (var alert in alerts)
            {
                var (alertText, alertBackground) = (SolidColor(Assert.IsType<TextBlock>(alert.Child).Foreground), SolidColor(alert.Background));
                Assert.True(FloweryColorHelpers.GetContrastRatio(alertText, alertBackground) >= 4.5, $"{alertText} on {alertBackground}.");
            }
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = previousContrast;
        }
    }

    [AvaloniaFact]
    public void The_Fonts_Tiles_And_Swatches_Follow_The_Size_The_Global_Size_And_The_Scale_Factor()
    {
        // Regression: every font and tile size was fixed, so the designer ignored the global size and FloweryScaleManager.
        using var scope = new FlowerySizeManagerPropagationTests.SizeScope();
        var designer = new DaisyGlassDesigner();
        scope.Show(designer);
        (scope.Window.Width, scope.Window.Height) = (1100, 1300);
        scope.Layout();

        double Token(string key) => (double)scope.Window.FindResource(key)!;
        TextBlock Label() => designer.GetVisualDescendants().OfType<TextBlock>().First(static text => text.Classes.Contains("gbd-label"));
        Border Swatch() => designer.GetVisualDescendants().OfType<Border>().First(static border => border.Name == "PART_Swatch");
        Border PresetTile() => designer.GetVisualDescendants().OfType<Border>().First(static border => border.Classes.Contains("gbd-tile-stage"));

        Assert.Equal(DaisySize.Medium, designer.Size);
        Assert.Equal(11, Label().FontSize);
        Assert.Equal(22, Swatch().Width);
        Assert.Equal(150, PresetTile().Width);

        designer.Size = DaisySize.Large;
        scope.Layout();
        var large = Token("DaisySizeLargeFontSize") / Token("DaisySizeMediumFontSize");
        Assert.Equal(11 * Token("DaisySizeLargeTertiaryFontSize") / Token("DaisySizeMediumTertiaryFontSize"), Label().FontSize, 6);
        Assert.Equal(Math.Round(22 * large), Swatch().Width);
        Assert.Equal(Math.Round(150 * large), PresetTile().Width);
        // The stage, the disc behind the preview and the help, which set their sizes in code, follow too.
        var stage = designer.GetVisualDescendants().OfType<Border>().First(static border => border.Classes.Contains("gbd-stage"));
        Assert.Equal(Math.Round(240 * large), stage.Height);
        Assert.Contains(stage.GetVisualDescendants().OfType<Border>(), border => border.RenderTransform is TranslateTransform
            && border.Width == Math.Round(120 * large));
        var help = designer.ShowHelp()!;
        Assert.Equal(20 * Token("DaisySizeLargeHeaderFontSize") / Token("DaisySizeMediumHeaderFontSize"), help.GetLogicalDescendants().OfType<TextBlock>().First().FontSize, 6);
        help.GetLogicalDescendants().OfType<DaisyButton>().Single(static button => AutomationProperties.GetAutomationId(button) == "GlassDesigner_HelpClose")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        scope.Layout();

        // The global size sets the size of a designer that has none of its own.
        designer.ClearValue(DaisyGlassDesigner.SizeProperty);
        FlowerySizeManager.ApplySize(DaisySize.Small);
        scope.Layout();
        Assert.Equal(DaisySize.Small, designer.Size);
        var small = Token("DaisySizeSmallFontSize") / Token("DaisySizeMediumFontSize");
        Assert.Equal(Math.Round(22 * small), Swatch().Width);

        // The scale factor of FloweryScaleManager shrinks the tiles and swatches.
        designer.ApplyScaleFactor(0.5);
        scope.Layout();
        Assert.Equal(Math.Round(22 * small * 0.5), Swatch().Width);
        Assert.Equal(Math.Round(150 * small * 0.5), PresetTile().Width);

        // A size given before the designer is shown applies once it is.
        designer = new DaisyGlassDesigner { Size = DaisySize.Large };
        scope.Window.Content = designer;
        scope.Layout();
        Assert.Equal(Math.Round(22 * large), Swatch().Width);
    }

    [AvaloniaFact]
    public void The_Backdrop_Swatches_Are_Named_In_The_Language_Of_The_App()
    {
        // Regression: the tooltips showed the names of an internal enum, in English in every language.
        using var culture = new CultureScope();
        FloweryLocalization.SetCulture(new CultureInfo("de"));
        using var shown = ShowDesigner(out var designer);
        Assert.Equal(["Polarlicht", "Sonnenuntergang", "Nacht", "Papier"], Tiles(designer, "gbd-swatch").Select(static swatch => ToolTip.GetTip(swatch)));
    }

    [AvaloniaFact]
    public void A_Slider_Writes_Only_Its_Own_Value_And_Keeps_The_Fill_And_Background()
    {
        // Regression: every slider tick applied the whole design, with a new fill brush, effective fill and contrast.
        using var shown = ShowDesigner(out var designer);
        var preview = designer.PreviewButton;
        SelectTab(designer, "Fill");
        Chip(designer, "Linear").IsChecked = true;
        var fill = Assert.IsType<LinearGradientBrush>(DaisyGlassSurface.GetFill(preview));
        var effective = DaisyGlassSurface.GetGlass(preview)!.EffectiveGlassFill;

        SelectTab(designer, "Glass");
        SetSlider(designer, "GlassDesigner_Blur", 50);
        SetSlider(designer, "GlassDesigner_Blur", 51);

        Assert.Equal(51, DaisyGlassSurface.GetGlass(preview)!.GlassBlur);
        Assert.Same(fill, DaisyGlassSurface.GetFill(preview));
        Assert.Same(effective, DaisyGlassSurface.GetGlass(preview)!.EffectiveGlassFill);
    }

    [Fact]
    public void A_Design_Read_From_A_Control_With_Values_Out_Of_Range_Saves_As_Json_That_Reads_Back()
    {
        // Regression: FromControl took any value, and FromJson rejected the file the designer then saved.
        var button = new DaisyButton();
        DaisyGlassSurface.SetZoom(button, 4);
        DaisyGlassSurface.SetBlur(button, -5);
        DaisyGlassSurface.SetTintOpacity(button, 2);
        DaisyGlassSurface.SetSaturation(button, double.NaN);
        DaisyGlassSurface.SetFill(button, new LinearGradientBrush
        {
            GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1.5) }
        });

        var design = DaisyGlassDesign.FromControl(button);
        var read = DaisyGlassDesign.FromJson(design.ToJson());

        Assert.Equal((3, 0, 1, 1), (read.Zoom, read.Blur, read.TintOpacity, read.Saturation));
        // A stop past the end is no fill the design can describe, so it stays on the control.
        Assert.Equal(DaisyGlassFillKind.Keep, design.FillKind);
        Assert.Equal(design.ToJson(), read.ToJson());
    }

    [AvaloniaFact]
    public async Task The_Overlay_Keeps_The_Keyboard_Inside_And_Gives_The_Focus_Back()
    {
        var box = new TextBox();
        var window = Show(box, 1100, 1300);
        try
        {
            box.Focus();
            var overlay = OverlayLayer.GetOverlayLayer(box)!;
            var open = DaisyGlassDesigner.ShowOverlayAsync(box);
            Dispatcher.UIThread.RunJobs();
            var backdrop = (Control)overlay.Children[0];
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(backdrop));
            Assert.False(box.IsFocused);

            OverlayButton(overlay, "GlassDesigner_Cancel").PointerClick();
            Assert.Null(await open);
            Assert.True(box.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    // A new designer in a window of its size, which closes when disposed.
    private static ShownDesigner ShowDesigner(out DaisyGlassDesigner designer) => new(Show(designer = new DaisyGlassDesigner(), 1100, 1300));

    private sealed class ShownDesigner(Window window) : IDisposable
    {
        public Window Window { get; } = window;

        public void Dispose() => Window.Close();
    }

    private static Color SolidColor(object? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void SelectTab(DaisyGlassDesigner designer, string name)
    {
        Tiles(designer, "gbd-tab").Single(tab => AutomationProperties.GetAutomationId(tab) == "GlassDesigner_Tab" + name).IsChecked = true;
        designer.UpdateLayout();
    }

    private static RadioButton Chip(DaisyGlassDesigner designer, string name) =>
        Tiles(designer, "gbd-chip").First(chip => (string?)chip.Content == name);

    private static IEnumerable<RadioButton> Tiles(DaisyGlassDesigner designer, string className) =>
        Tiles<RadioButton>(designer, className);

    // A rebuilt part inside a ScrollViewer joins the visual tree with the next layout pass.
    private static IEnumerable<T> Tiles<T>(DaisyGlassDesigner designer, string className) where T : Control
    {
        designer.UpdateLayout();
        return designer.GetVisualDescendants().OfType<T>().Where(control => control.Classes.Contains(className)).ToList();
    }

    private static void SetSlider(DaisyGlassDesigner designer, string key, double value)
    {
        var field = designer.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("gbd-field") && AutomationProperties.GetAutomationId(border) == key);
        field.GetVisualDescendants().OfType<DaisyRange>().Single().Value = value;
    }

    // A preset fill has no stops of its own, so the design keeps the default stops, as one read from a control would.
    private static DaisyGlassDesign Custom(DaisyGlassFillKind fill) => new()
    {
        BlurMode = GlassBlurMode.BitmapCapture,
        Blur = 42,
        Saturation = 1.4,
        Tint = Color.FromUInt32(0xFF0F172A),
        TintOpacity = 0.3,
        ReflectDegree = 45,
        Dispersion = 0.5,
        FillKind = fill,
        FillPreset = fill == DaisyGlassFillKind.Preset ? DaisyGlassGradient.Holo : DaisyGlassGradient.None,
        FillAngle = fill is DaisyGlassFillKind.Linear or DaisyGlassFillKind.Conic ? 60 : 30,
        FillStops = fill == DaisyGlassFillKind.Preset
            ? new DaisyGlassDesign().FillStops
            :
            [
                new() { Color = Color.FromUInt32(0xFFF9A8D4), Offset = 0 },
                new() { Color = Color.FromUInt32(0x80FDBA74), Offset = 0.4 },
                new() { Color = Color.FromUInt32(0xFF6366F1), Offset = 1 }
            ],
        FillOpacity = 0.7,
        Pattern = DaisyCardPattern.Honeycomb,
        PatternColor = Color.FromUInt32(0xFF111827),
        PatternOpacity = 0.3,
        BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
        BackgroundEffect = DaisyGlassAnimation.LiquidGold,
        BackgroundSpeed = 0.75,
        BackgroundFrameRate = 15,
        BackgroundOpacity = 0.8,
        ColorOpacity = 0.2,
        TextShadowOpacity = 0,
        TextContrast = false
    };

    private static Button OverlayButton(OverlayLayer overlay, string key) =>
        overlay.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == key);
}
