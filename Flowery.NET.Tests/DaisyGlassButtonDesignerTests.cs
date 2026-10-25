using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Skia;
using Xunit;
using DaisySize = Flowery.Controls.DaisySize;

namespace Flowery.NET.Tests;

public class DaisyGlassButtonDesignerTests
{
    [AvaloniaFact]
    public void The_Defaults_Of_A_Design_Are_Those_Of_A_Glass_Button_In_The_Theme()
    {
        var button = new DaisyGlassButton { Content = "Get started" };
        var window = Show(button);
        try
        {
            Assert.Equal(new DaisyGlassButtonDesign().ToJson(), DaisyGlassButtonDesign.FromButton(button).ToJson());
            Assert.Equal("<controls:DaisyGlassButton Content=\"Get started\" />", new DaisyGlassButtonDesign().ToXaml());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DaisyGlassFillKind.Linear)]
    [InlineData(DaisyGlassFillKind.Radial)]
    [InlineData(DaisyGlassFillKind.Conic)]
    [InlineData(DaisyGlassFillKind.Preset)]
    public void A_Design_Applied_To_A_Button_Reads_Back_Unchanged(DaisyGlassFillKind fill)
    {
        var design = Custom(fill);
        var button = new DaisyGlassButton();
        design.ApplyTo(button, includeContent: true);

        Assert.Equal(design.ToJson(), DaisyGlassButtonDesign.FromButton(button).ToJson());
    }

    [Fact]
    public void A_Design_Survives_Json()
    {
        var design = Custom(DaisyGlassFillKind.Conic);
        var json = design.ToJson();

        Assert.Contains("\"glassTint\": \"#FF0F172A\"", json);
        Assert.Contains("\"fillKind\": \"Conic\"", json);
        Assert.Equal(json, DaisyGlassButtonDesign.FromJson(json).ToJson());
    }

    [Fact]
    public void Json_That_Is_No_Design_Fails_With_A_Json_Exception()
    {
        Assert.Throws<JsonException>(() => DaisyGlassButtonDesign.FromJson("{ \"glassTint\": \"not a color\" }"));
        Assert.Throws<JsonException>(() => DaisyGlassButtonDesign.FromJson("null"));
    }

    [Theory]
    [InlineData("{ \"fillStops\": [null] }")]
    [InlineData("{ \"variant\": 99 }")]
    [InlineData("{ \"glassBlur\": -5 }")]
    [InlineData("{ \"glassOpacity\": 2 }")]
    [InlineData("{ \"fillStops\": [{ \"color\": \"#FFFFFFFF\", \"offset\": 3 }] }")]
    public void Json_With_A_Value_No_Button_Takes_Fails_With_A_Json_Exception(string json)
    {
        Assert.Throws<JsonException>(() => DaisyGlassButtonDesign.FromJson(json));
    }

    [AvaloniaFact]
    public void The_Exported_Code_Keeps_Any_Text_And_Names_Every_Type_Unambiguously()
    {
        const string text = "{Hi}\n\"quoted\" \\ & <tag>";
        var design = new DaisyGlassButtonDesign { Content = text, Size = DaisySize.Large, GlassPattern = DaisyCardPattern.Dots };

        var xaml = design.ToXaml().Replace("<controls:DaisyGlassButton ",
            "<controls:DaisyGlassButton xmlns=\"https://github.com/avaloniaui\" " +
            "xmlns:controls=\"clr-namespace:Flowery.Controls;assembly=Flowery.NET\" ");
        var button = (DaisyGlassButton)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml);
        Assert.Equal(text, button.Content);

        var code = design.ToCSharp();
        Assert.Contains("Content = \"{Hi}\\n\\\"quoted\\\" \\\\ & <tag>\"", code);
        // DaisySize exists in Flowery.Controls and Flowery.Enums, which the code needs both.
        Assert.Contains("Size = Flowery.Controls.DaisySize.Large", code);
        Assert.Contains("GlassPattern = DaisyCardPattern.Dots", code);
    }

    [AvaloniaFact]
    public void A_Fill_Or_Background_The_Design_Cannot_Describe_Stays_On_The_Button()
    {
        var solid = new SolidColorBrush(Colors.Teal);
        var background = new OwnBackground();
        var radial = new RadialGradientBrush { Center = RelativePoint.Center, GradientStops = { new GradientStop(Colors.Red, 0) } };
        foreach (IBrush fill in new IBrush[] { solid, radial })
        {
            var button = new DaisyGlassButton { GlassFill = fill, GlassBackground = background, GlassBlur = 5 };
            var design = DaisyGlassButtonDesign.FromButton(button);
            Assert.Equal(DaisyGlassFillKind.Keep, design.FillKind);
            Assert.Equal(DaisyGlassBackgroundRenderer.Keep, design.BackgroundRenderer);

            design.GlassBlur = 40;
            design.ApplyTo(button);
            Assert.Equal(40, button.GlassBlur);
            Assert.Same(fill, button.GlassFill);
            Assert.Same(background, button.GlassBackground);

            // The designer preview starts without a fill and shows the kept one.
            var preview = new DaisyGlassButton();
            design.ApplyTo(preview);
            Assert.Same(fill, preview.GlassFill);
        }
    }

    [AvaloniaTheory]
    [InlineData(DaisyGlassFillKind.Linear)]
    [InlineData(DaisyGlassFillKind.Radial)]
    [InlineData(DaisyGlassFillKind.Conic)]
    [InlineData(DaisyGlassFillKind.Preset)]
    public void The_Xaml_Of_A_Design_Loads_As_The_Same_Button(DaisyGlassFillKind fill)
    {
        var design = Custom(fill);
        var xaml = design.ToXaml().Replace("<controls:DaisyGlassButton ",
            "<controls:DaisyGlassButton xmlns=\"https://github.com/avaloniaui\" " +
            "xmlns:controls=\"clr-namespace:Flowery.Controls;assembly=Flowery.NET\" ");

        // The theme gives the button its defaults, which the XAML leaves out.
        var button = (DaisyGlassButton)Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader.Load(xaml);
        var window = Show(button);
        try
        {
            Assert.Equal(design.ToJson(), DaisyGlassButtonDesign.FromButton(button).ToJson());
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Code_Lists_Only_What_Differs_From_The_Defaults()
    {
        var design = new DaisyGlassButtonDesign { Content = "Go", Variant = DaisyButtonVariant.Primary, GlassBlur = 44 };

        Assert.Equal(
            "<controls:DaisyGlassButton Content=\"Go\"\n" +
            "                           Variant=\"Primary\"\n" +
            "                           GlassBlur=\"44\" />",
            design.ToXaml().ReplaceLineEndings("\n"));
        Assert.Equal(
            "// using Avalonia; using Avalonia.Media; using Flowery.Controls; using Flowery.Enums;\n" +
            "var button = new DaisyGlassButton\n{\n    Content = \"Go\",\n    Variant = DaisyButtonVariant.Primary,\n    GlassBlur = 44\n};",
            design.ToCSharp().ReplaceLineEndings("\n"));
    }

    // An AvaloniaFact runs on the UI thread like the other tests that register a renderer, so they never overlap.
    [AvaloniaFact]
    public void A_Shader_Background_Needs_The_Skia_Renderer()
    {
        var previous = DaisyGlassLiveRenderer.Current;
        try
        {
            var design = new DaisyGlassButtonDesign { BackgroundRenderer = DaisyGlassBackgroundRenderer.Shader, BackgroundSpeed = 0.5 };

            DaisyGlassLiveRenderer.Register(null);
            Assert.False(DaisyGlassButtonDesign.ShaderBackgroundAvailable);
            Assert.IsType<DaisyGlassGradientBackground>(design.CreateBackground());

            FlowerySkia.Register();
            Assert.True(DaisyGlassButtonDesign.ShaderBackgroundAvailable);
            var background = Assert.IsType<DaisyGlassShaderBackground>(design.CreateBackground());
            Assert.Equal(0.5, background.Speed);
            Assert.True(DaisyGlassLiveRenderer.Current!.IsShaderBackground(background));
            Assert.False(DaisyGlassLiveRenderer.Current.IsShaderBackground(new DaisyGlassGradientBackground()));
            Assert.Contains("<skia:DaisyGlassShaderBackground Effect=\"Plasma\" Speed=\"0.5\" />", design.ToXaml());
        }
        finally
        {
            DaisyGlassLiveRenderer.Register(previous);
        }
    }

    [AvaloniaFact]
    public void The_Preview_Shows_The_Design_And_Keeps_Its_Background_While_It_Animates()
    {
        var designer = new DaisyGlassButtonDesigner();
        var window = Show(designer, 1000, 1200);
        try
        {
            designer.Design = new DaisyGlassButtonDesign
            {
                Content = "Hello",
                BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
                BackgroundEffect = DaisyGlassAnimation.Aurora
            };
            var preview = designer.PreviewButton;
            Assert.Equal("Hello", preview.Content);
            var background = Assert.IsType<DaisyGlassGradientBackground>(preview.GlassBackground);

            SelectTab(designer, "Motion");
            SetSlider(designer, "GlassDesigner_Speed", 2);

            Assert.Same(background, preview.GlassBackground);
            Assert.Equal(2, background.Speed);
            Assert.Equal(2, designer.Design.BackgroundSpeed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Tab_Edits_The_Design()
    {
        var designer = new DaisyGlassButtonDesigner();
        var window = Show(designer, 1000, 1200);
        var changes = 0;
        designer.DesignChanged += (_, _) => changes++;
        try
        {
            SelectTab(designer, "Button");
            Chip(designer, "Primary").IsChecked = true;
            Assert.Equal(DaisyButtonVariant.Primary, designer.PreviewButton.Variant);

            SelectTab(designer, "Glass");
            Chip(designer, "BitmapCapture").IsChecked = true;
            SetSlider(designer, "GlassDesigner_Blur", 50);
            Assert.Equal(GlassBlurMode.BitmapCapture, designer.PreviewButton.BlurMode);
            Assert.Equal(50, designer.PreviewButton.GlassBlur);

            SelectTab(designer, "Fill");
            Chip(designer, "Linear").IsChecked = true;
            Assert.IsType<LinearGradientBrush>(designer.PreviewButton.GlassFill);

            SelectTab(designer, "Pattern");
            Tiles(designer, "gbd-pattern").Single(tile => tile.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Dots")).IsChecked = true;
            Assert.Equal(DaisyCardPattern.Dots, designer.PreviewButton.GlassPattern);

            SelectTab(designer, "Motion");
            Chip(designer, "Gradient").IsChecked = true;
            Assert.IsType<DaisyGlassGradientBackground>(designer.PreviewButton.GlassBackground);

            SelectTab(designer, "Presets");
            Assert.NotEmpty(Tiles<Button>(designer, "gbd-tile"));

            Assert.True(changes >= 6);
            Assert.Contains("Variant=\"Primary\"", designer.Xaml);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Language_Relabels_The_Designer_And_Keeps_The_Design_Tab_And_Code()
    {
        var previous = Flowery.Localization.FloweryLocalization.CurrentCulture;
        var designer = new DaisyGlassButtonDesigner { Design = new DaisyGlassButtonDesign { GlassBlur = 55 } };
        var window = Show(designer, 1000, 1200);
        try
        {
            Flowery.Localization.FloweryLocalization.SetCulture("en");
            SelectTab(designer, "Glass");
            designer.GetVisualDescendants().OfType<ToggleButton>().First(toggle => toggle.Classes.Contains("gbd-code-toggle")).IsChecked = true;

            Flowery.Localization.FloweryLocalization.SetCulture("de");
            Dispatcher.UIThread.RunJobs();

            var tab = Tiles(designer, "gbd-tab").Single(button => AutomationProperties.GetAutomationId(button) == "GlassDesigner_TabGlass");
            Assert.Equal("Glas", tab.Content);
            Assert.True(tab.IsChecked);
            Assert.Equal(55, designer.Design.GlassBlur);
            Assert.Equal(55, designer.PreviewButton.GlassBlur);
            Assert.Contains(designer.GetVisualDescendants().OfType<SelectableTextBlock>(), text => text.Text == designer.Xaml);
        }
        finally
        {
            Flowery.Localization.FloweryLocalization.SetCulture(previous);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Keeps_The_Text_And_Import_Loads_A_Design()
    {
        var designer = new DaisyGlassButtonDesigner { Design = new DaisyGlassButtonDesign { Content = "Keep", GlassBlur = 60 } };

        designer.Reset();
        Assert.Equal("Keep", designer.Design.Content);
        Assert.Equal(30, designer.Design.GlassBlur);

        designer.ImportJson(Custom(DaisyGlassFillKind.Linear).ToJson());
        Assert.Equal(DaisyGlassFillKind.Linear, designer.Design.FillKind);
        Assert.Throws<JsonException>(() => designer.ImportJson("[]"));
    }

    [AvaloniaFact]
    public async Task The_Overlay_Returns_The_Design_On_Apply_And_Nothing_On_Cancel_Or_Escape()
    {
        var host = new Border();
        var window = Show(host, 1100, 1300);
        try
        {
            var overlay = OverlayLayer.GetOverlayLayer(host)!;

            var applied = DaisyGlassButtonDesigner.ShowOverlayAsync(host, new DaisyGlassButtonDesign { Content = "Over" });
            Dispatcher.UIThread.RunJobs();
            OverlayButton(overlay, "GlassDesigner_Apply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Over", (await applied)!.Content);
            Assert.Empty(overlay.Children);

            var cancelled = DaisyGlassButtonDesigner.ShowOverlayAsync(host);
            Dispatcher.UIThread.RunJobs();
            OverlayButton(overlay, "GlassDesigner_Cancel").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Null(await cancelled);

            var escaped = DaisyGlassButtonDesigner.ShowOverlayAsync(host);
            Dispatcher.UIThread.RunJobs();
            ((Control)overlay.Children[0]).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Assert.Null(await escaped);
            Assert.Empty(overlay.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Editing_A_Button_Applies_The_Design_It_Started_From()
    {
        var button = new DaisyGlassButton { Content = "Mine", Variant = DaisyButtonVariant.Accent, GlassBlur = 12 };
        var window = Show(button, 1100, 1300);
        try
        {
            var overlay = OverlayLayer.GetOverlayLayer(button)!;
            var edit = DaisyGlassButtonDesigner.EditAsync(button);
            Dispatcher.UIThread.RunJobs();
            var designer = overlay.GetVisualDescendants().OfType<DaisyGlassButtonDesigner>().Single();
            Assert.Equal(DaisyButtonVariant.Accent, designer.Design.Variant);
            Assert.Equal("Mine", designer.Design.Content);

            var design = designer.Design;
            design.GlassBlur = 70;
            designer.Design = design;
            OverlayButton(overlay, "GlassDesigner_Apply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.True(await edit);
            Assert.Equal(70, button.GlassBlur);
            Assert.Equal(DaisyButtonVariant.Accent, button.Variant);
            Assert.Equal("Mine", button.Content);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Editing_A_Button_Edits_Its_Text_But_Leaves_Other_Content()
    {
        var button = new DaisyGlassButton { Content = "Old" };
        var window = Show(button, 1100, 1300);
        try
        {
            var overlay = OverlayLayer.GetOverlayLayer(button)!;
            var edit = DaisyGlassButtonDesigner.EditAsync(button);
            Dispatcher.UIThread.RunJobs();
            var designer = overlay.GetVisualDescendants().OfType<DaisyGlassButtonDesigner>().Single();
            var design = designer.Design;
            design.Content = "New";
            designer.Design = design;
            OverlayButton(overlay, "GlassDesigner_Apply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.True(await edit);
            Assert.Equal("New", button.Content);

            var icon = new TextBlock { Text = "★" };
            button.Content = icon;
            edit = DaisyGlassButtonDesigner.EditAsync(button);
            Dispatcher.UIThread.RunJobs();
            OverlayButton(overlay, "GlassDesigner_Apply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.True(await edit);
            Assert.Same(icon, button.Content);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Closing_The_Window_Ends_An_Open_Overlay_With_Nothing()
    {
        var host = new Border();
        var window = Show(host, 1100, 1300);
        var open = DaisyGlassButtonDesigner.ShowOverlayAsync(host);
        Dispatcher.UIThread.RunJobs();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.True(open.IsCompleted);
        Assert.Null(await open);
    }

    // A background the design cannot describe.
    private sealed class OwnBackground : DaisyGlassBackground
    {
        public override DaisyGlassAnimationLayer CreateLayer() => throw new NotSupportedException();
    }

    // A preset fill has no stops of its own, so the design keeps the default stops, as one read from a button would.
    private static DaisyGlassButtonDesign Custom(DaisyGlassFillKind fill) => new()
    {
        Content = "Custom",
        Variant = DaisyButtonVariant.Secondary,
        ButtonStyle = DaisyButtonStyle.Soft,
        Shape = DaisyButtonShape.Wide,
        Size = DaisySize.Large,
        BlurMode = GlassBlurMode.BitmapCapture,
        GlassBlur = 42,
        GlassSaturation = 1.4,
        GlassTint = Color.FromUInt32(0xFF0F172A),
        GlassTintOpacity = 0.3,
        GlassReflectDegree = 45,
        GlassDispersion = 0.5,
        FillKind = fill,
        FillPreset = fill == DaisyGlassFillKind.Preset ? DaisyGlassGradient.Holo : DaisyGlassGradient.None,
        FillAngle = fill is DaisyGlassFillKind.Linear or DaisyGlassFillKind.Conic ? 60 : 30,
        FillStops = fill == DaisyGlassFillKind.Preset
            ? new DaisyGlassButtonDesign().FillStops
            :
            [
                new() { Color = Color.FromUInt32(0xFFF9A8D4), Offset = 0 },
                new() { Color = Color.FromUInt32(0x80FDBA74), Offset = 0.4 },
                new() { Color = Color.FromUInt32(0xFF6366F1), Offset = 1 }
            ],
        GlassFillOpacity = 0.7,
        GlassPattern = DaisyCardPattern.Honeycomb,
        GlassPatternColor = Color.FromUInt32(0xFF111827),
        GlassPatternOpacity = 0.3,
        BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
        BackgroundEffect = DaisyGlassAnimation.LiquidGold,
        BackgroundSpeed = 0.75,
        BackgroundFrameRate = 15,
        GlassBackgroundOpacity = 0.8,
        GlassColorOpacity = 0.2,
        GlassTextShadowOpacity = 0,
        GlassTextContrast = false
    };

    private static Window Show(Control content, double width = 400, double height = 300)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static void SelectTab(DaisyGlassButtonDesigner designer, string name)
    {
        Tiles(designer, "gbd-tab").Single(tab => AutomationProperties.GetAutomationId(tab) == "GlassDesigner_Tab" + name).IsChecked = true;
        designer.UpdateLayout();
    }

    private static RadioButton Chip(DaisyGlassButtonDesigner designer, string name) =>
        Tiles(designer, "gbd-chip").First(chip => (string?)chip.Content == name);

    private static IEnumerable<RadioButton> Tiles(DaisyGlassButtonDesigner designer, string className) =>
        Tiles<RadioButton>(designer, className);

    // A rebuilt part inside a ScrollViewer joins the visual tree with the next layout pass.
    private static IEnumerable<T> Tiles<T>(DaisyGlassButtonDesigner designer, string className) where T : Control
    {
        designer.UpdateLayout();
        return designer.GetVisualDescendants().OfType<T>().Where(control => control.Classes.Contains(className)).ToList();
    }

    private static void SetSlider(DaisyGlassButtonDesigner designer, string key, double value)
    {
        var field = designer.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("gbd-field") && AutomationProperties.GetAutomationId(border) == key);
        field.GetVisualDescendants().OfType<DaisyRange>().Single().Value = value;
    }

    private static Button OverlayButton(OverlayLayer overlay, string key) =>
        overlay.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == key);
}
