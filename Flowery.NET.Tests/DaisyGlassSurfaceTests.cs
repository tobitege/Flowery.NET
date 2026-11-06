using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
using static Flowery.NET.Tests.TestControls;
using DaisyLabelPosition = Flowery.Controls.DaisyLabelPosition;

namespace Flowery.NET.Tests;

public class DaisyGlassSurfaceTests
{
    [AvaloniaFact]
    public void The_Text_Follows_The_Foreground_While_The_Backdrop_Is_Not_Measured()
    {
        // Without a measured backdrop the contrast keeps the foreground as it is. A captured backdrop would be measured,
        // and the headless platform leaves its pixels undefined, so the glass does not capture here.
        var button = new DaisyButton { Content = "Glass", Theme = GlassTheme("DaisyGlassButtonTheme") };
        DaisyGlassSurface.SetBlurMode(button, GlassBlurMode.Simulated);
        var window = Show(button);
        try
        {
            var glass = Part<DaisyGlass>(button, "PART_Glass");
            Assert.Null(glass.BackdropColor);
            var theme = button.Foreground;
            var themeEffective = DaisyGlassSurface.GetEffectiveForeground(button);
            button.Foreground = Brushes.Orange;
            var effective = DaisyGlassSurface.GetEffectiveForeground(button);
            // The content draws with the brush in use, adjusted or not.
            Assert.Same(effective, Part<StackPanel>(button, "PART_ContentPanel").GetValue(TextElement.ForegroundProperty));
            Assert.Same(theme, themeEffective);
            Assert.Same(Brushes.Orange, effective);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_The_Glass_Off_Hides_The_Glass_Of_A_Glass_Theme()
    {
        var button = new DaisyButton { Content = "Glass", Theme = GlassTheme("DaisyGlassButtonTheme") };
        var window = Show(button);
        try
        {
            var glass = Part<DaisyGlass>(button, "PART_Glass");
            DaisyGlassSurface.SetIsEnabled(button, false);
            Assert.False(glass.IsVisible);
            Assert.Null(DaisyGlassSurface.GetGlass(button));

            button.Foreground = Brushes.Orange;
            Assert.Same(Brushes.Orange, DaisyGlassSurface.GetEffectiveForeground(button));

            DaisyGlassSurface.SetIsEnabled(button, true);
            Assert.True(glass.IsVisible);
            Assert.Equal(30, glass.GlassBlur);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Input_Keeps_One_Glass_When_Its_Template_Changes()
    {
        var input = new DaisyInput { Label = "Email", PlaceholderText = "you@example.com" };
        DaisyGlassSurface.SetIsEnabled(input, true);
        DaisyGlassSurface.SetFollowForeground(input, true);
        var window = Show(input);
        try
        {
            var oldWatermark = Part<TextBlock>(input, "PART_Watermark");
            input.LabelPosition = DaisyLabelPosition.Floating;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var glass = Assert.Single(input.GetVisualDescendants().OfType<DaisyGlass>());
            DaisyGlassSurface.SetTintOpacity(input, 0.42);
            Assert.Equal(0.42, glass.GlassTintOpacity);

            input.Foreground = Brushes.Orange;
            Assert.Same(Brushes.Orange, Part<TextBlock>(input, "PART_FloatingLabel").Foreground);
            // A binding of the old template that still listens to the input would keep its parts alive with it.
            Assert.NotSame(Brushes.Orange, oldWatermark.Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Glass_With_A_Tint_Layer_Measures_Its_Backdrop()
    {
        // Glass buttons and badges draw their own text on the glass; navbar and group show app content, so a design
        // that turns the text contrast on must not make their glass read pixels back.
        var button = new DaisyButton { Content = "OK", Theme = GlassTheme("DaisyGlassButtonTheme") };
        var navbar = new DaisyNavbar { NavbarStart = "Flowery", Theme = GlassTheme("DaisyGlassNavbarTheme") };
        var group = new DaisyButtonGroup { Theme = GlassTheme("DaisyGlassButtonGroupTheme"), Items = { new DaisyButton { Content = "Day" } } };
        var window = Show(new StackPanel { Children = { button, navbar, group } });
        try
        {
            var design = new DaisyGlassDesign { TextContrast = true };
            design.ApplyTo(navbar);
            design.ApplyTo(group);
            Dispatcher.UIThread.RunJobs();

            Assert.True(Part<DaisyGlass>(button, "PART_Glass").MeasureBackdropColor);
            Assert.False(Part<DaisyGlass>(navbar, "PART_Glass").MeasureBackdropColor);
            Assert.False(Part<DaisyGlass>(group, "PART_Glass").MeasureBackdropColor);
            Assert.Same(navbar.Foreground, DaisyGlassSurface.GetEffectiveForeground(navbar));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Shared_Animated_Background_Does_Not_Keep_Removed_Controls_Alive()
    {
        var background = new DaisyGlassGradientBackground { Effect = DaisyGlassAnimation.Aurora };
        var reference = ShowAndDiscardButtonOn(background);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(reference.IsAlive);
        GC.KeepAlive(background);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ShowAndDiscardButtonOn(DaisyGlassBackground background)
    {
        var button = new DaisyButton { Content = "OK", Theme = GlassTheme("DaisyGlassButtonTheme") };
        DaisyGlassSurface.SetBackground(button, background);
        var window = Show(button);
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        // Release the closed window's pending composition batch before the GC assertion.
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return new WeakReference(button);
    }

    internal static ControlTheme GlassTheme(string key) =>
        Assert.IsType<ControlTheme>(Application.Current!.FindResource(key));

    private static Window Show(Control content)
    {
        var window = new Window { Width = 300, Height = 200, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }
}
