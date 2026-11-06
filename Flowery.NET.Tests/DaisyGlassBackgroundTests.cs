using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Skia;
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
        // A mutable brush becomes a compositor resource each frame; converted, it also made the aurora curtains round.
        var layer = new DaisyGlassGradientBackground { Effect = effect }.CreateLayer();
        layer.Measure(new Size(200, 100));
        layer.Arrange(new Rect(0, 0, 200, 100));
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

    [Theory]
    [InlineData(60, 30, 30)]
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
