using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyGlassButtonTests
{
    [AvaloniaFact]
    public void The_Theme_Blurs_The_Live_Backdrop_By_Default()
    {
        var button = new DaisyGlassButton { Content = "Glass" };
        var window = Show(button);
        try
        {
            Assert.True(button.EnableBackdropBlur);
            Assert.Equal(GlassBlurMode.SkiaSharp, button.BlurMode);
            Assert.Equal(30, button.GlassBlur);
            Assert.Equal(0.35, button.GlassColorOpacity);
            Assert.Equal(0.5, button.GlassTextShadowOpacity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Properties_Reach_The_Glass_Surface()
    {
        var button = new DaisyGlassButton
        {
            Content = "Glass",
            BlurMode = GlassBlurMode.BitmapCapture,
            GlassBlur = 12,
            GlassSaturation = 0.4,
            GlassTint = Colors.Red,
            GlassTintOpacity = 0.3,
            GlassDispersion = 0.5,
            GlassGradient = DaisyGlassGradient.Holo,
            GlassFillOpacity = 0.7,
            GlassPattern = DaisyCardPattern.Stripes,
            GlassPatternColor = Colors.Gold,
            GlassPatternOpacity = 0.3,
            CornerRadius = new CornerRadius(7)
        };
        var window = Show(button);
        try
        {
            var glass = Assert.Single(button.GetVisualDescendants().OfType<DaisyGlass>());
            Assert.Equal("PART_Glass", glass.Name);
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(GlassBlurMode.BitmapCapture, glass.BlurMode);
            Assert.Equal(12, glass.GlassBlur);
            Assert.Equal(0.4, glass.GlassSaturation);
            Assert.Equal(Colors.Red, glass.GlassTint);
            Assert.Equal(0.3, glass.GlassTintOpacity);
            Assert.Equal(0.5, glass.GlassDispersion);
            Assert.Equal(DaisyGlassGradient.Holo, glass.GlassGradient);
            Assert.IsType<ConicGradientBrush>(glass.EffectiveGlassFill);
            Assert.Equal(0.7, glass.GlassFillOpacity);
            Assert.Equal(DaisyCardPattern.Stripes, glass.GlassPattern);
            Assert.Equal(Colors.Gold, glass.GlassPatternColor);
            Assert.Equal(0.3, glass.GlassPatternOpacity);
            Assert.Equal(new CornerRadius(7), glass.CornerRadius);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Variant_Color_Tints_The_Glass_At_The_Color_Opacity()
    {
        var button = new DaisyGlassButton { Content = "Primary", Variant = DaisyButtonVariant.Primary };
        var window = Show(button);
        try
        {
            var background = Part<Border>(button, "PART_Background");
            Assert.True(button.TryFindResource("DaisyPrimaryColor", button.ActualThemeVariant, out var color));
            var fill = Assert.IsAssignableFrom<ISolidColorBrush>(background.Background);
            Assert.Equal(Assert.IsType<Color>(color), fill.Color);
            Assert.Equal(0.35, background.Opacity);

            button.GlassColorOpacity = 0.6;
            Assert.Equal(0.6, background.Opacity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Is_Painted_Before_The_Tint_And_The_Content()
    {
        // The backdrop holds everything painted before the glass, so the button's own tint and text must come later.
        var button = new DaisyGlassButton { Content = "Glass" };
        var window = Show(button);
        try
        {
            var root = Assert.IsAssignableFrom<Panel>(Part<DaisyGlass>(button, "PART_Glass").GetVisualParent());
            var order = root.Children.Select(child => child.Name).ToList();
            Assert.Equal(0, order.IndexOf("PART_Glass"));
            Assert.True(order.IndexOf("PART_Background") > 0);
            Assert.True(order.IndexOf("PART_ContentPanel") > order.IndexOf("PART_Background"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Whole_Surface_Takes_The_Pointer()
    {
        // The glass and tint layers ignore the pointer, so the border layer must take it, not only the text.
        var button = new DaisyGlassButton
        {
            Content = "Glass",
            Width = 200,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = Show(button);
        try
        {
            // Hit testing uses the rendered scene, which holds the button only after a frame was rendered.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var hit = button.InputHitTest(new Point(6, 24));
            Assert.True(hit is Visual visual && (ReferenceEquals(visual, button) || button.IsVisualAncestorOf(visual)),
                $"The edge of the button was not hit-testable: {hit}.");
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 300, Height = 200, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);
}
