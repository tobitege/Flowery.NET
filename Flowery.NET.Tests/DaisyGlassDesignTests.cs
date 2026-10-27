using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
using DaisyBadgeVariant = Flowery.Controls.DaisyBadgeVariant;
using DaisySize = Flowery.Controls.DaisySize;

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyGlassDesign and DaisyGlassDesigner, which describe a glass look for every control on glass.
/// </summary>
public class DaisyGlassDesignTests
{
    [AvaloniaFact]
    public void The_Defaults_Of_A_Design_Are_Those_Of_The_Glass_Button_Theme()
    {
        var button = new DaisyButton { Content = "Get started", Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonTheme") };
        var window = Show(button);
        try
        {
            Assert.Equal(new DaisyGlassDesign().ToJson(), DaisyGlassDesign.FromControl(button).ToJson());
            Assert.Equal(
                "<controls:DaisyButton Theme=\"{StaticResource DaisyGlassButtonTheme}\"\n" +
                "                      controls:DaisyGlassSurface.IsEnabled=\"True\"\n" +
                "                      controls:DaisyGlassSurface.Tint=\"#FFFFFFFF\" />",
                new DaisyGlassDesign().ToXaml().ReplaceLineEndings("\n"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DaisyGlassFillKind.Linear, "button")]
    [InlineData(DaisyGlassFillKind.Radial, "badge")]
    [InlineData(DaisyGlassFillKind.Conic, "input")]
    [InlineData(DaisyGlassFillKind.Preset, "card")]
    [InlineData(DaisyGlassFillKind.Linear, "menu")]
    public void A_Design_Applied_To_Any_Control_Reads_Back_Unchanged(DaisyGlassFillKind fill, string kind)
    {
        Control control = kind switch
        {
            "button" => new DaisyButton { Content = "OK", Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonTheme") },
            "badge" => new DaisyBadge { Content = "New", Variant = DaisyBadgeVariant.Primary, Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassBadgeTheme") },
            "input" => new DaisyInput { Label = "Email" },
            "card" => new DaisyCard { Width = 200, Height = 120 },
            _ => new DaisyMenu { Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassMenuTheme"), Items = { new ListBoxItem { Content = "Pin" } } }
        };
        var design = Custom(fill);
        var window = Show(control);
        try
        {
            design.ApplyTo(control);
            Dispatcher.UIThread.RunJobs();
            var glass = DaisyGlassSurface.GetGlass(control);
            Assert.NotNull(glass);
            Assert.Equal(42, glass.GlassBlur);
            Assert.Equal(design.ToJson(), DaisyGlassDesign.FromControl(control).ToJson());
        }
        finally
        {
            window.Close();
        }
    }

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
    [InlineData("{ \"tint\": \"not a color\" }")]
    [InlineData("null")]
    [InlineData("{ \"fillStops\": [null] }")]
    [InlineData("{ \"blurMode\": 99 }")]
    [InlineData("{ \"blur\": -5 }")]
    [InlineData("{ \"opacity\": 2 }")]
    public void Json_That_Is_No_Design_Fails_With_A_Json_Exception(string json)
    {
        Assert.Throws<JsonException>(() => DaisyGlassDesign.FromJson(json));
    }

    [AvaloniaTheory]
    [InlineData(DaisyGlassFillKind.Linear)]
    [InlineData(DaisyGlassFillKind.Radial)]
    [InlineData(DaisyGlassFillKind.Conic)]
    [InlineData(DaisyGlassFillKind.Preset)]
    public void The_Xaml_Of_A_Design_Loads_As_The_Same_Look(DaisyGlassFillKind fill)
    {
        var design = Custom(fill);
        var xaml = design.ToXaml().Replace("<controls:DaisyButton ",
            "<controls:DaisyButton xmlns=\"https://github.com/avaloniaui\" " +
            "xmlns:controls=\"clr-namespace:Flowery.Controls;assembly=Flowery.NET\" ");

        var button = (DaisyButton)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml);
        var window = Show(button);
        try
        {
            Assert.Equal(design.ToJson(), DaisyGlassDesign.FromControl(button).ToJson());
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Code_Of_A_Glass_Button_Lists_Only_What_Differs_From_Its_Theme()
    {
        var design = new DaisyGlassDesign { Blur = 44, Pattern = DaisyCardPattern.Dots };

        Assert.Equal(
            "<controls:DaisyButton Theme=\"{StaticResource DaisyGlassButtonTheme}\"\n" +
            "                      controls:DaisyGlassSurface.IsEnabled=\"True\"\n" +
            "                      controls:DaisyGlassSurface.Blur=\"44\"\n" +
            "                      controls:DaisyGlassSurface.Tint=\"#FFFFFFFF\"\n" +
            "                      controls:DaisyGlassSurface.Pattern=\"Dots\" />",
            design.ToXaml().ReplaceLineEndings("\n"));
        Assert.Equal(
            "// using Avalonia;\n// using Avalonia.Media;\n// using Avalonia.Styling;\n// using Flowery.Controls;\n// using Flowery.Enums;\n" +
            "var button = new DaisyButton { Theme = (ControlTheme)Application.Current!.FindResource(\"DaisyGlassButtonTheme\")! };\n" +
            "DaisyGlassSurface.SetIsEnabled(button, true);\nDaisyGlassSurface.SetBlur(button, 44);\n" +
            "DaisyGlassSurface.SetTint(button, Color.Parse(\"#FFFFFFFF\"));\n" +
            "DaisyGlassSurface.SetPattern(button, DaisyCardPattern.Dots);",
            design.ToCSharp().ReplaceLineEndings("\n"));
    }

    [AvaloniaFact]
    public void The_Exported_Glass_Button_Keeps_The_Tint_Of_The_Preview_Under_Glassy()
    {
        // The glass button theme leaves the tint to the color theme, which is navy in Glassy; the design sets white.
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            var design = new DaisyGlassDesign();
            var xaml = design.ToXaml().Replace("<controls:DaisyButton ",
                "<controls:DaisyButton xmlns=\"https://github.com/avaloniaui\" " +
                "xmlns:controls=\"clr-namespace:Flowery.Controls;assembly=Flowery.NET\" ");
            var button = (DaisyButton)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml);
            var window = Show(button);
            try
            {
                Assert.Equal(Colors.White, DaisyGlassSurface.GetGlass(button)!.GlassTint);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaTheory]
    [InlineData("#FF0EA5E9")]
    [InlineData(null)]
    public void The_Color_Of_A_Liquid_Background_Goes_Through_Json_Xaml_And_The_Control(string? color)
    {
        var design = new DaisyGlassDesign
        {
            BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
            BackgroundEffect = DaisyGlassAnimation.LiquidColor,
            BackgroundColor = color is null ? null : Color.Parse(color)
        };
        Assert.Equal(design.BackgroundColor, DaisyGlassDesign.FromJson(design.ToJson()).BackgroundColor);
        Assert.Equal(color is not null, design.ToXaml().Contains("EffectColor=\"#FF0EA5E9\""));
        Assert.Equal(color is not null, design.ToCSharp().Contains("EffectColor = Color.Parse(\"#FF0EA5E9\")"));

        var xaml = design.ToXaml().Replace("<controls:DaisyButton ",
            "<controls:DaisyButton xmlns=\"https://github.com/avaloniaui\" " +
            "xmlns:controls=\"clr-namespace:Flowery.Controls;assembly=Flowery.NET\" ");
        var button = (DaisyButton)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml);
        var window = Show(button);
        try
        {
            Assert.Equal(design.BackgroundColor, DaisyGlassSurface.GetBackground(button)!.EffectColor);
            Assert.Equal(design.ToJson(), DaisyGlassDesign.FromControl(button).ToJson());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Liquid_Color_Effect_Offers_The_Accent_Color_Or_A_Picked_One()
    {
        var designer = new DaisyGlassDesigner
        {
            Design = new DaisyGlassDesign { BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient, BackgroundEffect = DaisyGlassAnimation.LiquidColor }
        };
        var window = Show(designer, 1100, 1300);
        try
        {
            designer.GetVisualDescendants().OfType<RadioButton>()
                .Single(tab => Avalonia.Automation.AutomationProperties.GetAutomationId(tab) == "GlassDesigner_TabMotion").IsChecked = true;
            window.UpdateLayout();
            var accent = designer.GetVisualDescendants().OfType<RadioButton>().Single(chip => chip.GroupName?.StartsWith("gbd-effect-color-") == true);
            Assert.True(accent.IsChecked);

            designer.Design = new DaisyGlassDesign
            {
                BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient, BackgroundEffect = DaisyGlassAnimation.LiquidColor,
                BackgroundColor = Colors.Teal
            };
            window.UpdateLayout();
            accent = designer.GetVisualDescendants().OfType<RadioButton>().Single(chip => chip.GroupName?.StartsWith("gbd-effect-color-") == true);
            Assert.False(accent.IsChecked);
            accent.IsChecked = true;
            Assert.Null(designer.Design.BackgroundColor);
        }
        finally
        {
            window.Close();
        }
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
    public void The_Help_Button_Explains_In_A_Glass_Dialog_How_To_Use_A_Design()
    {
        var designer = new DaisyGlassDesigner();
        var window = Show(designer, 1100, 1300);
        try
        {
            var help = designer.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.IconSymbol == DaisyIconSymbol.Help);
            help.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, help));
            Dispatcher.UIThread.RunJobs();

            var overlay = OverlayLayer.GetOverlayLayer(designer)!;
            var modal = Assert.Single(overlay.Children.OfType<DaisyModal>());
            Assert.True(modal.IsOpen);
            Assert.Same(Application.Current!.FindResource("DaisyGlassModalTheme"), modal.Theme);
            Assert.Contains(modal.GetLogicalDescendants().OfType<SelectableTextBlock>(), block => block.Text!.Contains("DaisyGlassDesign.FromJson"));

            OverlayButton(overlay, "GlassDesigner_HelpClose").PointerClick();
            Assert.Empty(overlay.Children.OfType<DaisyModal>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Palette_Sets_The_Fill_Colors_Which_Stay_Editable()
    {
        var designer = new DaisyGlassDesigner();
        var window = Show(designer, 1100, 1300);
        try
        {
            designer.GetVisualDescendants().OfType<RadioButton>()
                .Single(tab => Avalonia.Automation.AutomationProperties.GetAutomationId(tab) == "GlassDesigner_TabFill").IsChecked = true;
            window.UpdateLayout();
            var ember = designer.GetVisualDescendants().OfType<Button>().Single(tile => ToolTip.GetTip(tile) as string == "Ember");
            ember.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, ember));
            window.UpdateLayout();

            var design = designer.Design;
            Assert.Equal(DaisyGlassFillKind.Linear, design.FillKind);
            Assert.Equal([Color.Parse("#F97316"), Color.Parse("#DC2626"), Color.Parse("#7F1D1D")], design.FillStops.Select(stop => stop.Color));
            Assert.Equal([0, 0.5, 1], design.FillStops.Select(stop => stop.Offset));
            // The colors show as swatches to edit, and the text of the preview stays at hand above the tabs.
            Assert.Contains(designer.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("gbd-swatch"));
            var text = designer.GetVisualDescendants().OfType<TextBox>().First(box => box.Text == designer.PreviewText);
            text.Text = "Save";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Save", designer.PreviewButton.Content);
            designer.PreviewText = "Load";
            Assert.Equal("Load", text.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Preset_Shows_On_The_Button_It_Was_Made_For()
    {
        var designer = new DaisyGlassDesigner();
        var window = Show(designer, 1100, 1300);
        try
        {
            designer.UpdateLayout();
            var neon = designer.GetVisualDescendants().OfType<Button>()
                .Single(tile => tile.Classes.Contains("gbd-tile") && ToolTip.GetTip(tile) as string == "Neon");
            // The tile shows the preset on its own button, shape included.
            var sunset = designer.GetVisualDescendants().OfType<Button>()
                .Single(tile => tile.Classes.Contains("gbd-tile") && ToolTip.GetTip(tile) as string == "Sunset");
            Assert.Equal(DaisyButtonShape.Wide, sunset.GetVisualDescendants().OfType<DaisyButton>().Single().Shape);
            neon.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, neon));

            Assert.Equal(DaisyButtonVariant.Accent, designer.PreviewVariant);
            Assert.Equal(DaisyButtonStyle.Outline, designer.PreviewButtonStyle);
            Assert.Equal(0.9, designer.Design.Curvature);
            // The preview options stay out of the design.
            Assert.DoesNotContain("Accent", designer.Json);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Code_For_Another_Control_Holds_The_Whole_Design()
    {
        // Without the glass button theme, nothing else gives the glass the values equal to the defaults.
        var design = Custom(DaisyGlassFillKind.Linear);
        design.TintOpacity = new DaisyGlassDesign().TintOpacity;
        var code = design.ToCSharp("card");
        Assert.Contains("DaisyGlassSurface.SetTintOpacity(card, 0.15);", code);
        Assert.Contains("DaisyGlassSurface.SetEnableBackdropBlur(card, true);", code);

        var xaml = design.ToXaml(element: "controls:DaisyCard").Replace("<controls:DaisyCard ",
            "<controls:DaisyCard xmlns=\"https://github.com/avaloniaui\" " +
            "xmlns:controls=\"clr-namespace:Flowery.Controls;assembly=Flowery.NET\" Width=\"200\" Height=\"120\" ");
        var card = (DaisyCard)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml);
        var window = Show(card);
        try
        {
            Assert.Equal(design.ToJson(), DaisyGlassDesign.FromControl(card).ToJson());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Editing_A_Control_Not_On_Glass_Gives_It_The_Whole_Design()
    {
        var input = new DaisyInput { Label = "Email" };
        var window = Show(input, 1100, 1300);
        try
        {
            var overlay = OverlayLayer.GetOverlayLayer(input)!;
            var edit = DaisyGlassDesigner.EditAsync(input);
            Dispatcher.UIThread.RunJobs();
            OverlayButton(overlay, "GlassDesigner_Apply").PointerClick();
            Assert.True(await edit);

            var glass = DaisyGlassSurface.GetGlass(input);
            Assert.NotNull(glass);
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(30, glass.GlassBlur);
            Assert.Equal(0.15, glass.GlassTintOpacity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Fill_Or_Background_The_Design_Cannot_Describe_Stays_On_The_Control()
    {
        var fill = new SolidColorBrush(Colors.Teal);
        var background = new DaisyGlassGradientBackground { Effect = DaisyGlassAnimation.Aurora };
        var button = new DaisyButton { Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonTheme") };
        DaisyGlassSurface.SetFill(button, fill);
        var design = DaisyGlassDesign.FromControl(button);
        Assert.Equal(DaisyGlassFillKind.Keep, design.FillKind);

        design.Blur = 40;
        design.ApplyTo(button);
        Assert.Equal(40, DaisyGlassSurface.GetBlur(button));
        Assert.Same(fill, DaisyGlassSurface.GetFill(button));

        DaisyGlassSurface.SetBackground(button, background);
        DaisyGlassDesign.FromControl(button).ApplyTo(button);
        Assert.Same(fill, DaisyGlassSurface.GetFill(button));
    }

    [AvaloniaFact]
    public void The_Preview_Options_Change_Only_The_Preview()
    {
        var designer = new DaisyGlassDesigner();
        var window = Show(designer, 1100, 1300);
        try
        {
            var xaml = designer.Xaml;
            designer.PreviewText = "Save";
            designer.PreviewVariant = DaisyButtonVariant.Primary;
            designer.PreviewSize = DaisySize.Large;
            Assert.Equal("Save", designer.PreviewButton.Content);
            Assert.Equal(DaisyButtonVariant.Primary, designer.PreviewButton.Variant);
            Assert.Equal(DaisySize.Large, designer.PreviewButton.Size);
            Assert.Equal(xaml, designer.Xaml);
            Assert.NotNull(DaisyGlassSurface.GetGlass(designer.PreviewButton));

            var source = new DaisyButton { Content = "Delete", Variant = DaisyButtonVariant.Error, Shape = DaisyButtonShape.Wide };
            DaisyGlassSurface.SetBlur(source, 12);
            designer.LoadFrom(source);
            Assert.Equal("Delete", designer.PreviewText);
            Assert.Equal(DaisyButtonVariant.Error, designer.PreviewVariant);
            Assert.Equal(DaisyButtonShape.Wide, designer.PreviewShape);
            Assert.Equal(12, designer.Design.Blur);
            Assert.Equal(12, DaisyGlassSurface.GetGlass(designer.PreviewButton)!.GlassBlur);

            // A square or circle preview grows until the whole text fits inside it.
            var wide = designer.PreviewButton.Bounds.Width;
            foreach (var shape in new[] { DaisyButtonShape.Square, DaisyButtonShape.Circle })
            {
                designer.PreviewShape = shape;
                window.UpdateLayout();
                var bounds = designer.PreviewButton.Bounds;
                Assert.Equal("Delete", designer.PreviewButton.Content);
                Assert.Equal(bounds.Height, bounds.Width);
                var text = designer.PreviewButton.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == "Delete");
                Assert.True(bounds.Width > text.Bounds.Width + 8, $"{shape}: {bounds.Width} for text {text.Bounds.Width}");
            }
            designer.PreviewSize = DaisySize.Small;
            window.UpdateLayout();
            Assert.True(designer.PreviewButton.Bounds.Width < wide + 40);
            designer.PreviewShape = DaisyButtonShape.Default;
            window.UpdateLayout();
            Assert.Equal(28, designer.PreviewButton.Bounds.Height);

            // A block button takes the width of the stage.
            designer.PreviewShape = DaisyButtonShape.Block;
            window.UpdateLayout();
            Assert.True(designer.PreviewButton.Bounds.Width > 3 * wide, $"{designer.PreviewButton.Bounds.Width}");
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

    private static Window Show(Control content, double width = 400, double height = 300)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static Button OverlayButton(OverlayLayer overlay, string key) =>
        overlay.GetVisualDescendants().OfType<Button>().Single(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == key);
}
