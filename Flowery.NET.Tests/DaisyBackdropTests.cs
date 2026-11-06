using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using SkiaSharp;
using Xunit;
using static Flowery.NET.Tests.TestControls;

namespace Flowery.NET.Tests;

/// <summary>
/// The backdrop of a theme: DaisyBackdropBrush, DaisyBackdropEffect, the DaisyBackdrop control and
/// DaisyThemeManager.BackdropOverride.
/// </summary>
public class DaisyBackdropTests
{
    [AvaloniaFact]
    public void The_Backdrop_Is_Base100_Of_The_Theme_And_Follows_A_Theme_Switch()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var backdrop = new DaisyBackdrop { Content = new TextBlock { Text = "Content" } };
        var window = Show(backdrop);
        try
        {
            foreach (var theme in new[] { "Dracula", "Light" })
            {
                Assert.True(DaisyThemeManager.ApplyTheme(theme));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(Base100(), Assert.IsAssignableFrom<ISolidColorBrush>(backdrop.Background).Color);
                Assert.Same(backdrop.Background, Part<Border>(backdrop, "PART_Backdrop").Background);
            }
            Assert.Null(backdrop.BackdropEffect);
            Assert.Empty(Part<Panel>(backdrop, "PART_EffectHost").Children);
            Assert.Equal("Content", Assert.IsType<TextBlock>(backdrop.Content).Text);
        }
        finally
        {
            window.Close();
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void In_The_Glass_Mode_A_Theme_Without_A_Backdrop_Gets_Glows_In_Its_Colors()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Dracula"));
            DaisyGlassSurface.GlobalEnabled = true;

            var layers = Layers(Resource("DaisyBackdropBrush"));
            Assert.Equal(Base100(), Assert.IsAssignableFrom<ISolidColorBrush>(layers[0]).Color);
            Assert.Equal(WithoutAlpha(ThemeColor("DaisyPrimaryColor")), WithoutAlpha(Assert.IsAssignableFrom<IRadialGradientBrush>(layers[1]).GradientStops[0].Color));
            Assert.Equal(WithoutAlpha(ThemeColor("DaisySecondaryColor")), WithoutAlpha(Assert.IsAssignableFrom<IRadialGradientBrush>(layers[2]).GradientStops[0].Color));

            // A theme switch computes the glows again.
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            Assert.Equal(Base100(), Assert.IsAssignableFrom<ISolidColorBrush>(Layers(Resource("DaisyBackdropBrush"))[0]).Color);

            DaisyGlassSurface.GlobalEnabled = false;
            Assert.Equal(Base100(), Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBackdropBrush")).Color);
        }
        finally
        {
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void Glassy_Brings_Its_Wave_Backdrop_Also_In_The_Glass_Mode()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            var wave = Resource("DaisyBackdropBrush");
            var sky = Assert.IsAssignableFrom<ILinearGradientBrush>(Layers(wave)[0]);
            Assert.Equal(Color.Parse("#4A2BD6"), sky.GradientStops[0].Color);

            DaisyGlassSurface.GlobalEnabled = true;
            Assert.Same(wave, Resource("DaisyBackdropBrush"));
        }
        finally
        {
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void A_Color_Override_Replaces_The_Backdrop_Of_Every_Theme_Until_It_Is_Cleared()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var changes = 0;
        void Count(object? sender, EventArgs e) => changes++;
        DaisyThemeManager.BackdropOverrideChanged += Count;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            Assert.IsType<DrawingBrush>(Resource("DaisyBackdropBrush"));

            DaisyThemeManager.BackdropOverride = " #0b1b5e ";
            Assert.Equal("#0B1B5E", DaisyThemeManager.BackdropOverride);
            Assert.Equal(Color.Parse("#0B1B5E"), Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBackdropBrush")).Color);

            // The override stays across a theme switch and the glass mode.
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            DaisyGlassSurface.GlobalEnabled = true;
            Assert.Equal(Color.Parse("#0B1B5E"), Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBackdropBrush")).Color);
            DaisyGlassSurface.GlobalEnabled = false;

            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            DaisyThemeManager.BackdropOverride = null;
            Assert.IsType<DrawingBrush>(Resource("DaisyBackdropBrush"));
            Assert.Equal(2, changes);
        }
        finally
        {
            DaisyThemeManager.BackdropOverrideChanged -= Count;
            DaisyThemeManager.BackdropOverride = null;
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void An_Effect_Override_Moves_Over_The_Brush_Of_The_Theme_And_Hides_With_High_Contrast()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var backdrop = new DaisyBackdrop();
        var window = Show(backdrop);
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Dracula"));
            var brush = backdrop.Background;

            DaisyThemeManager.BackdropOverride = "effect:aurora";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("effect:Aurora", DaisyThemeManager.BackdropOverride);
            Assert.Equal(Flowery.Enums.DaisyGlassAnimation.Aurora, backdrop.BackdropEffect!.Effect);
            Assert.Same(brush, backdrop.Background);
            var host = Part<Panel>(backdrop, "PART_EffectHost");
            Assert.IsAssignableFrom<DaisyGlassAnimationLayer>(Assert.Single(host.Children));

            DaisyAccessibility.HighContrastOverride = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(host.Children);
            Assert.Null(backdrop.BackdropEffect);
            var plain = Assert.IsAssignableFrom<ISolidColorBrush>(backdrop.Background).Color;
            Assert.True(plain == Colors.Black || plain == Colors.White, $"{plain}");

            DaisyAccessibility.HighContrastOverride = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Single(host.Children);

            // A color override puts the effect away again.
            DaisyThemeManager.BackdropOverride = "Teal";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("#008080", DaisyThemeManager.BackdropOverride);
            Assert.Null(backdrop.BackdropEffect);
            Assert.Empty(host.Children);
        }
        finally
        {
            window.Close();
            DaisyAccessibility.HighContrastOverride = null;
            DaisyThemeManager.BackdropOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void An_Image_Override_Fills_The_Backdrop_With_The_Image()
    {
        // The headless platform decodes no pixels; the runtime tests check the size of a decoded backdrop.
        var previous = DaisyThemeManager.CurrentThemeName;
        string? file = null;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            file = Png(40, 20);
            foreach (var source in new[] { file, new Uri(file).AbsoluteUri })
            {
                DaisyThemeManager.BackdropOverride = "image:" + source;
                Assert.Equal("image:" + source, DaisyThemeManager.BackdropOverride);
                var image = Assert.IsType<ImageBrush>(Resource("DaisyBackdropBrush"));
                Assert.Equal(Stretch.UniformToFill, image.Stretch);
                Assert.IsType<Bitmap>(image.Source);
            }

            // The same image written differently is no change: no new load, the bitmap stays and no event.
            var loaded = Resource("DaisyBackdropBrush");
            var changes = 0;
            void Count(object? sender, EventArgs e) => changes++;
            DaisyThemeManager.BackdropOverrideChanged += Count;
            try
            {
                DaisyThemeManager.BackdropOverride = " IMAGE: " + new Uri(file).AbsoluteUri;
                Assert.Same(loaded, Resource("DaisyBackdropBrush"));
                Assert.Equal(0, changes);
            }
            finally
            {
                DaisyThemeManager.BackdropOverrideChanged -= Count;
            }

            // An image that cannot be loaded leaves the backdrop of the theme and keeps the text, which the app saved.
            var missing = "image:" + Path.Combine(Path.GetTempPath(), "flowery-backdrop-missing", "none.png");
            DaisyThemeManager.BackdropOverride = missing;
            Assert.Equal(missing, DaisyThemeManager.BackdropOverride);
            Assert.Equal(Base100(), Assert.IsAssignableFrom<ISolidColorBrush>(Resource("DaisyBackdropBrush")).Color);
        }
        finally
        {
            DaisyThemeManager.BackdropOverride = null;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
            if (file is not null)
                File.Delete(file);
        }
    }

    [AvaloniaFact]
    public void An_Image_That_Cannot_Be_Loaded_Leaves_The_Glows_Of_The_Glass_Mode()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Dracula"));
            DaisyGlassSurface.GlobalEnabled = true;
            DaisyThemeManager.BackdropOverride = "image:" + Path.Combine(Path.GetTempPath(), "flowery-backdrop-missing", "none.png");
            Assert.Equal(Base100(), Assert.IsAssignableFrom<ISolidColorBrush>(Layers(Resource("DaisyBackdropBrush"))[0]).Color);
        }
        finally
        {
            DaisyThemeManager.BackdropOverride = null;
            DaisyGlassSurface.GlobalEnabled = false;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [Fact]
    public void The_Width_Of_A_Png_Or_Jpeg_Is_Read_From_Its_Header()
    {
        foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg })
        {
            using var bitmap = new SKBitmap(4100, 6);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 90);
            Assert.Equal(4100, DaisyThemeManager.ReadImageWidth(data.AsSpan()));
        }
        Assert.Null(DaisyThemeManager.ReadImageWidth("GIF89a........"u8));
        Assert.Null(DaisyThemeManager.ReadImageWidth([0xFF, 0xD8, 0x00]));
    }

    [AvaloniaFact]
    public void The_Sidebar_Selector_And_The_Backdrop_Override_Follow_Each_Other()
    {
        var sidebar = new FloweryComponentSidebar();
        var window = Show(sidebar);
        try
        {
            Assert.Equal("Theme", sidebar.Backdrop);
            Assert.Equal(["Theme", "Plasma", "Aurora", "LiquidGold", "LiquidColor", "Color...", "Image..."], sidebar.AvailableBackdrops);

            sidebar.Backdrop = "Aurora";
            Assert.Equal("effect:Aurora", DaisyThemeManager.BackdropOverride);

            // The color in use shows as an entry of its own, so "Color..." can pick another one.
            DaisyThemeManager.BackdropOverride = "#112233";
            Assert.Equal("#112233", sidebar.Backdrop);
            Assert.Equal(["Theme", "Plasma", "Aurora", "LiquidGold", "LiquidColor", "#112233", "Color...", "Image..."], sidebar.AvailableBackdrops);
            DaisyThemeManager.BackdropOverride = "image:/pictures/beach.jpg";
            Assert.Equal("image:/pictures/beach.jpg", sidebar.Backdrop);
            Assert.Equal("beach.jpg", FloweryComponentSidebar.ChoiceLabel(sidebar.Backdrop));
            Assert.DoesNotContain("#112233", sidebar.AvailableBackdrops);
            DaisyThemeManager.BackdropOverride = "effect:LiquidGold";
            Assert.Equal("LiquidGold", sidebar.Backdrop);
            Assert.Equal(7, sidebar.AvailableBackdrops.Count);

            // An image named like an effect is an entry of its own: the effect stays selectable and in the list.
            DaisyThemeManager.BackdropOverride = "image:/pictures/Aurora";
            Assert.Equal("image:/pictures/Aurora", sidebar.Backdrop);
            sidebar.Backdrop = "Aurora";
            Assert.Equal("effect:Aurora", DaisyThemeManager.BackdropOverride);
            Assert.Equal(["Theme", "Plasma", "Aurora", "LiquidGold", "LiquidColor", "Color...", "Image..."], sidebar.AvailableBackdrops);

            sidebar.Backdrop = "Theme";
            Assert.Null(DaisyThemeManager.BackdropOverride);
        }
        finally
        {
            window.Close();
            DaisyThemeManager.BackdropOverride = null;
        }
    }

    [AvaloniaFact]
    public void The_Sidebar_Shows_Its_Glass_Settings_In_The_Language_Of_The_Controls()
    {
        using var culture = new CultureScope();
        var sidebar = new FloweryComponentSidebar
        {
            Categories =
            [
                new SidebarCategory
                {
                    Name = "Glass", IsExpanded = true,
                    Items = { new SidebarGlassModeItem { Name = "Glass" }, new SidebarBaseColorItem { Name = "Base" }, new SidebarBackdropItem { Name = "Backdrop" } }
                }
            ]
        };
        var window = Show(sidebar);
        try
        {
            FloweryLocalization.SetCulture("de-DE");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var select = sidebar.GetVisualDescendants().OfType<DaisySelect>().Single(static box => box.ItemsSource is IReadOnlyList<string>);
            Assert.Equal("Hintergrund", AutomationProperties.GetName(select));
            var texts = sidebar.GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text).ToList();
            // The base color of the theme and the backdrop of the theme, in the button and in the selector.
            Assert.Equal(2, texts.Count(static text => text == "Design"));
            Assert.Contains("Grundfarbe", texts);
            Assert.Contains(sidebar.GetVisualDescendants().OfType<DaisyToggle>(), static toggle => Equals(toggle.Content, "Glasmodus"));

            select.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var items = select.GetRealizedContainers().Select(static item => item.GetVisualDescendants().OfType<TextBlock>().First().Text).ToList();
            Assert.Contains("Farbe...", items);
            Assert.Contains("Bild...", items);
            select.IsDropDownOpen = false;

            FloweryLocalization.SetCulture("en-US");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, sidebar.GetVisualDescendants().OfType<TextBlock>().Count(static text => text.Text == "Theme"));
            // The choices themselves stay the same in every language.
            Assert.Equal("Theme", sidebar.Backdrop);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Picker_That_Fails_Is_Logged_And_The_Selector_Goes_Back_To_The_Backdrop_In_Use()
    {
        // Not in a window: the color dialog has no overlay layer to show in and throws.
        var sidebar = new FloweryComponentSidebar();
        var resets = new List<object?>();
        sidebar.PropertyChanged += (_, e) =>
        {
            if (e.Property == FloweryComponentSidebar.BackdropProperty)
                resets.Add(e.NewValue);
        };
        var sink = new ErrorSink();
        var previousSink = Avalonia.Logging.Logger.Sink;
        Avalonia.Logging.Logger.Sink = sink;
        try
        {
            sidebar.Backdrop = "Color...";
            sidebar.PickBaseColorCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["Theme"], resets);
            Assert.Equal(2, sink.Errors.Count(static error => error.Contains("FloweryComponentSidebar")));
        }
        finally
        {
            Avalonia.Logging.Logger.Sink = previousSink;
        }
    }

    private sealed class ErrorSink : Avalonia.Logging.ILogSink
    {
        public List<string> Errors { get; } = [];

        public bool IsEnabled(Avalonia.Logging.LogEventLevel level, string area) => level >= Avalonia.Logging.LogEventLevel.Error;

        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate) =>
            Errors.Add(messageTemplate);

        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Errors.Add(messageTemplate);
    }

    [AvaloniaTheory]
    [InlineData("effect:Nope")]
    [InlineData("effect:2")]
    [InlineData("effect:")]
    [InlineData("image:")]
    [InlineData("not a backdrop")]
    public void Text_That_Is_No_Backdrop_Fails_With_A_Format_Exception_And_Changes_Nothing(string text)
    {
        DaisyThemeManager.BackdropOverride = "#123456";
        try
        {
            Assert.Throws<FormatException>(() => DaisyThemeManager.BackdropOverride = text);
            Assert.Equal("#123456", DaisyThemeManager.BackdropOverride);
        }
        finally
        {
            DaisyThemeManager.BackdropOverride = null;
        }
    }

    // A new PNG of the given size in the temp folder, under a name no other test or run uses.
    private static string Png(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"flowery-backdrop-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
        return path;
    }

    // The brushes of the layers of a drawing backdrop, bottom first.
    private static IBrush?[] Layers(object? brush) =>
        Assert.IsType<DrawingGroup>(Assert.IsType<DrawingBrush>(brush).Drawing).Children
            .Select(static drawing => Assert.IsType<GeometryDrawing>(drawing).Brush).ToArray();

    private static Color WithoutAlpha(Color color) => Color.FromRgb(color.R, color.G, color.B);

    private static Color Base100() => ThemeColor("DaisyBase100Color");
}
