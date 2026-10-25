using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Skia;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Tests;

public class GlassTextContrastTests
{
    [Fact]
    public void The_Contrast_Ratio_Follows_WCAG()
    {
        Assert.Equal(21, FloweryColorHelpers.GetContrastRatio(Colors.White, Colors.Black), 2);
        Assert.Equal(1, FloweryColorHelpers.GetContrastRatio(Colors.Red, Colors.Red), 3);
        // #767676 is the lightest gray with 4.5:1 on white.
        Assert.InRange(FloweryColorHelpers.GetContrastRatio(Color.Parse("#767676"), Colors.White), 4.5, 4.6);
    }

    [Fact]
    public void A_Text_Color_Without_Contrast_Moves_Just_Far_Enough_And_Keeps_Its_Hue()
    {
        var text = Color.Parse("#2563EB");
        var background = Color.Parse("#1E40AF");

        var adjusted = FloweryColorHelpers.EnsureContrast(text, background, 4.5);

        Assert.InRange(FloweryColorHelpers.GetContrastRatio(adjusted, background), 4.5, 4.7);
        Assert.True(adjusted.B > adjusted.R && adjusted.B > adjusted.G, $"The blue hue is lost: {adjusted}.");
    }

    [Fact]
    public void A_Translucent_Text_Color_Is_Checked_As_It_Renders()
    {
        // White at 60 % on mid gray: opaque white would pass, the rendered light gray does not.
        var text = Color.Parse("#99FFFFFF");
        var background = Color.Parse("#737373");
        Assert.True(FloweryColorHelpers.GetContrastRatio(Colors.White, background) >= 4.5);

        var adjusted = FloweryColorHelpers.EnsureContrast(text, background, 4.5);

        Assert.True(Rendered(adjusted, background) >= 4.5, $"{adjusted} renders with too little contrast on {background}.");
    }

    [Fact]
    public void A_Translucent_Text_Color_Keeps_Its_Alpha_While_That_Is_Enough_And_Becomes_Opaque_Otherwise()
    {
        var kept = FloweryColorHelpers.EnsureContrast(Color.Parse("#C02563EB"), Colors.Black, 4.5);
        Assert.Equal(0xC0, kept.A);
        Assert.True(Rendered(kept, Colors.Black) >= 4.5);

        var background = Color.Parse("#1E40AF");
        var opaque = FloweryColorHelpers.EnsureContrast(Color.Parse("#802563EB"), background, 4.5);
        Assert.Equal(0xFF, opaque.A);
        Assert.True(Rendered(opaque, background) >= 4.5);
    }

    private static double Rendered(Color text, Color background) =>
        FloweryColorHelpers.GetContrastRatio(FloweryColorHelpers.Composite(background, text, 1), background);

    [Fact]
    public void A_Text_Color_With_Contrast_Stays_As_It_Is()
    {
        Assert.Equal(Colors.Black, FloweryColorHelpers.EnsureContrast(Colors.Black, Colors.White, 4.5));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Glass_Button_Or_Badge_Over_A_Backdrop_Of_Its_Text_Color_Gets_Readable_Text(bool badge)
    {
        TemplatedControl control = badge ? new DaisyGlassBadge { Content = "Glass" } : new DaisyGlassButton { Content = "Glass" };
        var window = Show(control);
        try
        {
            var glass = Glass(control);
            Assert.True(glass.MeasureBackdropColor);
            var text = Assert.IsAssignableFrom<ISolidColorBrush>(control.Foreground).Color;

            // Behind the glass lies the text color itself: without an adjustment the text would vanish.
            TestRenderer.Report(glass, text);

            var effective = Assert.IsAssignableFrom<ISolidColorBrush>(EffectiveForeground(control)).Color;
            Assert.NotEqual(text, effective);
            AssertReadable(control, glass, text, effective);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Contrast_Counts_The_Fill_The_Animated_Background_The_Shine_And_The_Variant_Tint()
    {
        var background = new DaisyGlassGradientBackground { Effect = DaisyGlassAnimation.Plasma };
        var button = new DaisyGlassButton
        {
            Content = "Glass",
            Variant = DaisyButtonVariant.Primary,
            GlassGradient = DaisyGlassGradient.Sunset,
            GlassBackground = background
        };
        var window = Show(button);
        try
        {
            var glass = Glass(button);
            var backdrop = Color.Parse("#808080");
            TestRenderer.Report(glass, backdrop);
            var effective = Assert.IsAssignableFrom<ISolidColorBrush>(button.EffectiveForeground).Color;
            AssertReadable(button, glass, backdrop, effective);

            // The effect changes the color behind the text, so the text follows it.
            background.Effect = DaisyGlassAnimation.Aurora;
            effective = Assert.IsAssignableFrom<ISolidColorBrush>(button.EffectiveForeground).Color;
            AssertReadable(button, glass, backdrop, effective);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Template_Draws_The_Shared_Top_Shine()
    {
        var glass = new DaisyGlass { Width = 200, Height = 80 };
        var window = Show(glass);
        try
        {
            var shine = glass.GetVisualDescendants().OfType<Border>().Single(border => ReferenceEquals(border.Background, DaisyGlass.TopShineBrush));
            Assert.Equal(DaisyGlass.TopShineHeight, shine.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Foreground_Set_On_The_Button_Is_Kept()
    {
        var foreground = new SolidColorBrush(Colors.White);
        var button = new DaisyGlassButton { Content = "Glass", Foreground = foreground };
        var window = Show(button);
        try
        {
            var glass = Glass(button);
            Assert.False(glass.MeasureBackdropColor);
            TestRenderer.Report(glass, Colors.White);
            Assert.Same(foreground, button.EffectiveForeground);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Text_Contrast_The_Glass_Does_Not_Measure_And_The_Text_Keeps_Its_Color()
    {
        var button = new DaisyGlassButton { Content = "Glass" };
        var window = Show(button);
        try
        {
            var glass = Glass(button);
            TestRenderer.Report(glass, Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
            Assert.NotSame(button.Foreground, button.EffectiveForeground);

            button.GlassTextContrast = false;

            Assert.False(glass.MeasureBackdropColor);
            Assert.Null(glass.BackdropColor);
            Assert.Same(button.Foreground, button.EffectiveForeground);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Skia_Measurement_Averages_The_Backdrop_Under_The_Glass()
    {
        // Red left half and blue right half under the glass; a green border around it lies outside the glass.
        using var surface = SKSurface.Create(new SKImageInfo(120, 60));
        surface.Canvas.Clear(SKColors.Lime);
        using (var red = new SKPaint { Color = SKColors.Red })
            surface.Canvas.DrawRect(SKRect.Create(10, 10, 50, 40), red);
        using (var blue = new SKPaint { Color = SKColors.Blue })
            surface.Canvas.DrawRect(SKRect.Create(60, 10, 50, 40), blue);
        using var image = surface.Snapshot();

        var color = SkiaGlassDrawOperation.MeasureBackdropColor(null, image, new SKRectI(0, 0, 120, 60), new SKRectI(10, 10, 110, 50));

        Assert.NotNull(color);
        Assert.InRange(color.Value.R, 117, 137);
        Assert.InRange(color.Value.G, 0, 10);
        Assert.InRange(color.Value.B, 117, 137);
    }

    // Builds the color behind the text from the layers of the glass and its template, independently of the control,
    // and checks the drawn text against it.
    private static void AssertReadable(TemplatedControl control, DaisyGlass glass, Color backdrop, Color text)
    {
        var surface = FloweryColorHelpers.Composite(Color.FromRgb(backdrop.R, backdrop.G, backdrop.B), glass.GlassTint, glass.GlassTintOpacity);
        if (glass.EffectiveGlassFill is IGradientBrush { GradientStops: { Count: > 0 } stops })
            surface = FloweryColorHelpers.Composite(surface, Average(stops), glass.GlassFillOpacity);
        if (glass.GlassBackground is { } animated)
            surface = FloweryColorHelpers.Composite(surface, AnimatedAverage(animated.Effect), glass.GlassBackgroundOpacity);
        surface = FloweryColorHelpers.Composite(surface, Colors.White, ShineAt(glass.Bounds.Height / 2));
        var tint = control.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(border => border.Name is "PART_Background" or "PART_Surface");
        if (tint?.Background is ISolidColorBrush solid)
            surface = FloweryColorHelpers.Composite(surface, Color.FromArgb((byte)(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B), tint.Opacity);
        var drawn = FloweryColorHelpers.Composite(surface, text, 1);
        Assert.True(FloweryColorHelpers.GetContrastRatio(drawn, surface) >= 4.5, $"{text} on {surface} has too little contrast.");
    }

    private static Color Average(IReadOnlyList<IGradientStop> stops) => Color.FromArgb(
        (byte)stops.Average(stop => stop.Color.A), (byte)stops.Average(stop => stop.Color.R),
        (byte)stops.Average(stop => stop.Color.G), (byte)stops.Average(stop => stop.Color.B));

    private static Color AnimatedAverage(DaisyGlassAnimation effect) => effect switch
    {
        DaisyGlassAnimation.Aurora => Color.FromRgb(25, 70, 80),
        DaisyGlassAnimation.LiquidGold => Color.FromRgb(185, 135, 35),
        _ => Color.FromRgb(150, 120, 190)
    };

    // The shine opacity at a height, interpolated from the stops of the shared brush.
    private static double ShineAt(double y)
    {
        var stops = ((IGradientBrush)DaisyGlass.TopShineBrush).GradientStops;
        var offset = y / DaisyGlass.TopShineHeight;
        if (offset >= 1)
            return 0;
        for (var i = 1; i < stops.Count; i++)
        {
            if (offset <= stops[i].Offset)
            {
                var t = (offset - stops[i - 1].Offset) / (stops[i].Offset - stops[i - 1].Offset);
                return (stops[i - 1].Color.A + (stops[i].Color.A - stops[i - 1].Color.A) * t) / 255.0;
            }
        }
        return 0;
    }

    private static IBrush? EffectiveForeground(TemplatedControl control) =>
        control is DaisyGlassBadge badge ? badge.EffectiveForeground : ((DaisyGlassButton)control).EffectiveForeground;

    private static DaisyGlass Glass(Control control) =>
        control.GetVisualDescendants().OfType<DaisyGlass>().First(glass => glass.Name == "PART_Glass");

    private static Window Show(Control content)
    {
        var window = new Window { Width = 300, Height = 200, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    // Reports measured backdrop colors the way a live renderer does.
    private sealed class TestRenderer : DaisyGlassLiveRenderer
    {
        public static void Report(DaisyGlass glass, Color color) => ReportBackdropColor(glass, color);

        public override void Render(DaisyGlass glass, DrawingContext context)
        {
        }
    }
}
