using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Skia;
using SkiaSharp;
using Xunit;

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

    [AvaloniaFact]
    public void The_Liquid_Color_Shader_Draws_In_Its_Color()
    {
        var red = Average(DrawLiquid(Colors.Red));
        var blue = Average(DrawLiquid(Colors.Blue));
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
        try
        {
            var changes = 0;
            void Count(object? sender, EventArgs e) => changes++;
            DaisyGlassBackground.AccentColor();
            DaisyGlassBackground.AccentColorChanged += Count;
            Assert.True(DaisyThemeManager.ApplyTheme(previous == "Dracula" ? "Light" : "Dracula"));
            DaisyGlassBackground.AccentColorChanged -= Count;
            Assert.Equal(1, changes);
        }
        finally
        {
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

    [Fact]
    public void Switching_From_Every_Frame_To_A_Frame_Rate_Takes_Effect_At_Once()
    {
        var isRedrawFrame = typeof(DaisyGlassAnimationLayer).GetMethod("IsRedrawFrame", BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = new object[] { 0.0, 1.0 / 144, 0.0 };
        for (var frame = 0; frame < 144 * 60; frame++)
            isRedrawFrame.Invoke(null, args);

        // A minute without a rate must not leave time that makes the next second redraw on every frame.
        args[2] = 30.0;
        var redraws = 0;
        for (var frame = 0; frame < 144; frame++)
        {
            if ((bool)isRedrawFrame.Invoke(null, args)!)
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
            var frameRequested = typeof(DaisyGlassAnimationLayer).GetField("_frameRequested", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.False((bool)frameRequested.GetValue(layer)!);

            background.Speed = 1;
            Assert.True((bool)frameRequested.GetValue(layer)!);
        }
        finally
        {
            window?.Close();
            DaisyAccessibility.ReducedMotionOverride = previous;
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
