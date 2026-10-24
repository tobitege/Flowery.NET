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
