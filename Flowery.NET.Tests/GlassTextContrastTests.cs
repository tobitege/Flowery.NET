using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
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
    public void A_Text_Color_Without_Contrast_Moves_Just_Far_Enough_And_Keeps_Its_Hue_And_Alpha()
    {
        var text = Color.Parse("#802563EB");
        var background = Color.Parse("#1E40AF");

        var adjusted = FloweryColorHelpers.EnsureContrast(text, background, 4.5);

        var opaque = Color.FromRgb(adjusted.R, adjusted.G, adjusted.B);
        Assert.InRange(FloweryColorHelpers.GetContrastRatio(opaque, background), 4.5, 4.7);
        Assert.Equal(0x80, adjusted.A);
        Assert.True(adjusted.B > adjusted.R && adjusted.B > adjusted.G, $"The blue hue is lost: {adjusted}.");
    }

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

            var surface = FloweryColorHelpers.Composite(Color.FromRgb(text.R, text.G, text.B), glass.GlassTint, glass.GlassTintOpacity);
            var effective = Assert.IsAssignableFrom<ISolidColorBrush>(EffectiveForeground(control)).Color;
            Assert.NotEqual(text, effective);
            Assert.True(FloweryColorHelpers.GetContrastRatio(Color.FromRgb(effective.R, effective.G, effective.B), surface) >= 4.5,
                $"{effective} on {surface} has too little contrast.");
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
