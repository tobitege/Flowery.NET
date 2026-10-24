using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Skia;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyGlassBackgroundTests
{
    [AvaloniaTheory]
    [InlineData(DaisyGlassAnimation.Plasma)]
    [InlineData(DaisyGlassAnimation.Aurora)]
    [InlineData(DaisyGlassAnimation.LiquidGold)]
    public void Every_Shader_Effect_Compiles(DaisyGlassAnimation effect)
    {
        Assert.NotNull(DaisyGlassShaderBackground.GetEffect(effect));
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
        var button = new DaisyGlassButton { Content = "Gold", GlassBackground = background };
        var badge = new DaisyGlassBadge { Content = "Gold", GlassBackground = background };
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
        var isRedrawFrame = typeof(DaisyGlassAnimationLayer).GetMethod("IsRedrawFrame", BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = new object[] { 0.0, 1.0 / refreshRate, frameRate };
        var redraws = 0;
        for (var frame = 0; frame < refreshRate * 10; frame++)
        {
            if ((bool)isRedrawFrame.Invoke(null, args)!)
                redraws++;
        }

        Assert.InRange(redraws / 10.0, expected - 0.5, expected + 0.5);
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().First(part => part.Name == name);
}
