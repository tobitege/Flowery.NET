using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Skia;
using SkiaSharp;
using Xunit;
using static Flowery.NET.Tests.TestControls;

namespace Flowery.NET.Tests;

public class DaisyGlassBackgroundTests
{
    [AvaloniaTheory]
    [InlineData(DaisyGlassAnimation.Plasma)]
    [InlineData(DaisyGlassAnimation.Aurora)]
    [InlineData(DaisyGlassAnimation.LiquidGold)]
    [InlineData(DaisyGlassAnimation.LiquidColor)]
    public void Every_Shader_Effect_Compiles(DaisyGlassAnimation effect)
    {
        Assert.NotNull(DaisyGlassShaderBackground.GetEffect(effect));
    }

    [AvaloniaTheory]
    [InlineData(DaisyGlassAnimation.Plasma)]
    [InlineData(DaisyGlassAnimation.Aurora)]
    [InlineData(DaisyGlassAnimation.LiquidGold)]
    [InlineData(DaisyGlassAnimation.LiquidColor)]
    public void A_Gradient_Frame_Draws_Only_Immutable_Brushes_And_Stretched_Aurora_Curtains(DaisyGlassAnimation effect)
    {
        // A mutable brush would become a compositor resource, created and freed every frame. The conversion of a mutable
        // radial brush also gave the curtains of the aurora their horizontal radius vertically, so they were round.
        var layer = new DaisyGlassGradientBackground { Effect = effect }.CreateLayer();
        layer.Measure(new Avalonia.Size(200, 100));
        layer.Arrange(new Avalonia.Rect(0, 0, 200, 100));
        var frame = new DrawingGroup();
        using (var context = frame.Open())
            layer.Render(context);

        var brushes = frame.Children.OfType<GeometryDrawing>().Select(static drawing => drawing.Brush).ToList();
        Assert.NotEmpty(brushes);
        Assert.All(brushes, static brush => Assert.IsAssignableFrom<IImmutableBrush>(brush));
        if (effect == DaisyGlassAnimation.Aurora)
        {
            var curtains = brushes.OfType<IRadialGradientBrush>().ToList();
            Assert.Equal(3, curtains.Count);
            Assert.All(curtains, static curtain => Assert.True(curtain.RadiusX.Scalar > 3 * curtain.RadiusY.Scalar,
                $"A curtain is {curtain.RadiusX.Scalar} wide and {curtain.RadiusY.Scalar} high."));
        }
    }

    [AvaloniaFact]
    public void The_Liquid_Color_Shader_Draws_In_Its_Color()
    {
        using var redBitmap = DrawLiquid(Colors.Red);
        using var blueBitmap = DrawLiquid(Colors.Blue);
        var red = Average(redBitmap);
        var blue = Average(blueBitmap);
        Assert.True(red.Red > red.Blue + 60, $"red {red}");
        Assert.True(blue.Blue > blue.Red + 60, $"blue {blue}");
    }

    [AvaloniaFact]
    public void The_Liquid_Color_Takes_The_Accent_Color_Unless_A_Color_Is_Set()
    {
        var background = new DaisyGlassGradientBackground { Effect = DaisyGlassAnimation.LiquidColor };
        var accent = DaisyGlassBackground.AccentColor();
        Assert.NotEqual(0, accent.A);
        Assert.Equal(accent, background.GetEffectColor());

        background.EffectColor = Colors.Teal;
        Assert.Equal(Colors.Teal, background.GetEffectColor());
    }

    [AvaloniaFact]
    public void Without_An_Accent_Color_Of_The_System_The_Primary_Color_Of_The_Theme_Stands_In()
    {
        var app = Avalonia.Application.Current!;
        Assert.True(app.TryFindResource("DaisyPrimaryColor", app.ActualThemeVariant, out var primary));
        Assert.Equal(primary, DaisyGlassBackground.ResolveAccentColor(null, app));
        // A transparent accent is no accent.
        Assert.Equal(primary, DaisyGlassBackground.ResolveAccentColor(Colors.Transparent, app));
        Assert.Equal(Colors.Orange, DaisyGlassBackground.ResolveAccentColor(Colors.Orange, app));
        Assert.Equal(Colors.DodgerBlue, DaisyGlassBackground.ResolveAccentColor(null, null));
    }

    [AvaloniaFact]
    public void A_New_Theme_Reads_The_Accent_Color_Again()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var changes = 0;
        void Count(object? sender, EventArgs e) => changes++;
        try
        {
            // An accent read before, which the new theme has to replace.
            DaisyGlassBackground.OnAccentColorChanged(Colors.Orange);
            Assert.Equal(Colors.Orange, DaisyGlassBackground.AccentColor());
            DaisyGlassBackground.AccentColorChanged += Count;
            Assert.True(DaisyThemeManager.ApplyTheme(previous == "Dracula" ? "Light" : "Dracula"));
            Assert.Equal(1, changes);
            Assert.NotEqual(Colors.Orange, DaisyGlassBackground.AccentColor());
        }
        finally
        {
            DaisyGlassBackground.AccentColorChanged -= Count;
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    private static SKBitmap DrawLiquid(Color color)
    {
        var bitmap = new SKBitmap(new SKImageInfo(64, 32, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        DaisyGlassShaderBackground.Draw(canvas, 64, 32, DaisyGlassAnimation.LiquidColor, 3, color);
        return bitmap;
    }

    private static SKColor Average(SKBitmap bitmap)
    {
        var pixels = bitmap.Pixels;
        return new SKColor((byte)pixels.Average(p => p.Red), (byte)pixels.Average(p => p.Green), (byte)pixels.Average(p => p.Blue));
    }

    [AvaloniaFact]
    public void A_Background_Puts_One_Layer_Into_The_Glass_And_A_New_One_Replaces_It()
    {
        var glass = new DaisyGlass { Width = 160, Height = 60, GlassBackground = new DaisyGlassGradientBackground() };
        var window = Show(glass);
        try
        {
            var host = Part<Panel>(glass, "PART_BackgroundHost");
            var first = Assert.IsAssignableFrom<DaisyGlassAnimationLayer>(Assert.Single(host.Children));
            Assert.Equal(0.6, host.Opacity);

            glass.GlassBackground = new DaisyGlassShaderBackground { Effect = DaisyGlassAnimation.Aurora };
            var second = Assert.IsAssignableFrom<DaisyGlassAnimationLayer>(Assert.Single(host.Children));
            Assert.NotSame(first, second);

            glass.GlassBackgroundOpacity = 0.3;
            Assert.Equal(0.3, host.Opacity);

            glass.GlassBackground = null;
            Assert.Empty(host.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Shared_Background_Gives_Each_Glass_Its_Own_Layer()
    {
        var background = new DaisyGlassGradientBackground { Effect = DaisyGlassAnimation.LiquidGold };
        var button = new DaisyButton { Content = "Gold", Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonTheme") };
        var badge = new DaisyBadge { Content = "Gold", Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassBadgeTheme") };
        DaisyGlassSurface.SetBackground(button, background);
        DaisyGlassSurface.SetBackground(badge, background);
        var window = Show(new StackPanel { Children = { button, badge } });
        try
        {
            var buttonLayer = Assert.Single(Part<Panel>(button, "PART_BackgroundHost").Children);
            var badgeLayer = Assert.Single(Part<Panel>(badge, "PART_BackgroundHost").Children);
            Assert.NotSame(buttonLayer, badgeLayer);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void A_Background_Redraws_30_Times_A_Second_Unless_Set_Otherwise()
    {
        Assert.Equal(30, new DaisyGlassGradientBackground().FrameRate);
        Assert.Equal(30, new DaisyGlassShaderBackground().FrameRate);
    }

    [Theory]
    [InlineData(60, 30, 30)]
    [InlineData(60, 60, 60)]
    [InlineData(144, 30, 30)]
    [InlineData(144, 60, 60)]
    [InlineData(240, 30, 30)]
    [InlineData(144, 0, 144)]
    public void A_Background_Keeps_Its_Frame_Rate_On_Any_Refresh_Rate(int refreshRate, double frameRate, int expected)
    {
        var sinceRedraw = 0.0;
        var redraws = 0;
        for (var frame = 0; frame < refreshRate * 10; frame++)
        {
            if (DaisyGlassAnimationLayer.IsRedrawFrame(ref sinceRedraw, 1.0 / refreshRate, frameRate))
                redraws++;
        }

        Assert.InRange(redraws / 10.0, expected - 0.5, expected + 0.5);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1000.0)]
    public void Switching_From_Every_Frame_To_A_Frame_Rate_Takes_Effect_At_Once(double previousRate)
    {
        var sinceRedraw = 0.0;
        for (var frame = 0; frame < 144 * 60; frame++)
            DaisyGlassAnimationLayer.IsRedrawFrame(ref sinceRedraw, 1.0 / 144, previousRate);

        // A minute without a rate, or at a rate above the display rate, must not leave time that makes the next second
        // redraw on every frame.
        var redraws = 0;
        for (var frame = 0; frame < 144; frame++)
        {
            if (DaisyGlassAnimationLayer.IsRedrawFrame(ref sinceRedraw, 1.0 / 144, 30))
                redraws++;
        }
        Assert.InRange(redraws, 29, 31);
    }

    [AvaloniaFact]
    public void A_Background_That_Cannot_Move_Asks_For_No_Frames_Until_It_Can()
    {
        // Motion stays on whatever the system says; a remote desktop session, for one, turns animations off.
        var previous = DaisyAccessibility.ReducedMotionOverride;
        Window? window = null;
        try
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            var background = new DaisyGlassGradientBackground { Speed = 0 };
            var glass = new DaisyGlass { Width = 160, Height = 60, GlassBackground = background };
            window = Show(glass);
            var layer = Assert.IsAssignableFrom<DaisyGlassAnimationLayer>(Assert.Single(Part<Panel>(glass, "PART_BackgroundHost").Children));
            Assert.False(layer.FrameRequested);

            background.Speed = 1;
            Assert.True(layer.FrameRequested);
        }
        finally
        {
            window?.Close();
            DaisyAccessibility.ReducedMotionOverride = previous;
        }
    }

    [AvaloniaFact]
    public void A_Background_Asks_For_No_Frames_While_It_Is_Hidden_Or_Scrolled_Out_Of_View()
    {
        var previous = DaisyAccessibility.ReducedMotionOverride;
        Window? window = null;
        try
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            var glass = new DaisyGlass { Width = 160, Height = 60, GlassBackground = new DaisyGlassGradientBackground() };
            var host = new Border { Child = glass };
            var below = new Border { Height = 600 };
            var scroll = new ScrollViewer { Height = 200, Content = new StackPanel { Children = { host, below } } };
            window = Show(scroll);
            var layer = Assert.IsAssignableFrom<DaisyGlassAnimationLayer>(Assert.Single(Part<Panel>(glass, "PART_BackgroundHost").Children));
            Assert.True(layer.FrameRequested);

            // A hidden ancestor reports nothing, so the layer stops at its next frame and looks again now and then.
            host.IsVisible = false;
            RealInput.RenderFrames();
            Assert.False(layer.FrameRequested);
            host.IsVisible = true;
            Thread.Sleep(400);
            Dispatcher.UIThread.Post(static () => { });
            Dispatcher.UIThread.RunJobs();
            Assert.True(layer.FrameRequested);

            // Out of view, the layer stops; back in view, it asks again at once.
            scroll.Offset = new Vector(0, 400);
            window.UpdateLayout();
            RealInput.RenderFrames();
            Assert.False(layer.FrameRequested);
            scroll.Offset = default;
            window.UpdateLayout();
            Assert.True(layer.FrameRequested);
        }
        finally
        {
            window?.Close();
            DaisyAccessibility.ReducedMotionOverride = previous;
        }
    }
}
