using Avalonia.Headless;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
using DaisyBadgeVariant = Flowery.Controls.DaisyBadgeVariant;

namespace Flowery.NET.Tests;

/// <summary>
/// The glass themes of the standard button, badge, navbar and modal.
/// </summary>
public class DaisyGlassControlsTests
{
    [AvaloniaFact]
    public void The_Glass_Button_Theme_Blurs_The_Live_Backdrop_By_Default()
    {
        var button = new DaisyButton { Content = "Glass", Theme = Theme("DaisyGlassButtonTheme") };
        var window = Show(button);
        try
        {
            var glass = Part<DaisyGlass>(button, "PART_Glass");
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(GlassBlurMode.SkiaSharp, glass.BlurMode);
            Assert.Equal(30, glass.GlassBlur);
            Assert.Equal(0.35, DaisyGlassSurface.GetColorOpacity(button));
            Assert.Equal(0.5, DaisyGlassSurface.GetTextShadowOpacity(button));
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
        var button = new DaisyButton { Content = "Glass", Theme = Theme("DaisyGlassButtonTheme") };
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
    public void The_Whole_Surface_Of_A_Glass_Button_Takes_The_Pointer()
    {
        // The glass and tint layers ignore the pointer, so the border layer must take it, not only the text.
        var button = new DaisyButton
        {
            Content = "Glass",
            Theme = Theme("DaisyGlassButtonTheme"),
            Width = 200,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var window = Show(button);
        try
        {
            // Hit testing uses the rendered scene, which holds the button only after a frame was rendered.
            // A batch left by an earlier window is rendered first and holds back the button's commit,
            // so commit and render twice.
            for (var frame = 0; frame < 2; frame++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            var hit = button.InputHitTest(new Point(6, 24));
            Assert.True(hit is Visual visual && (ReferenceEquals(visual, button) || button.IsVisualAncestorOf(visual)),
                $"The edge of the button was not hit-testable: {hit}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DaisyButtonStyle.Default, true)]
    [InlineData(DaisyButtonStyle.Soft, true)]
    [InlineData(DaisyButtonStyle.Outline, false)]
    [InlineData(DaisyButtonStyle.Dash, false)]
    public void Hovering_A_Default_Glass_Button_Brightens_The_Glass_Except_For_Outline_And_Dash(DaisyButtonStyle style, bool brightens)
    {
        var button = new DaisyButton { Content = "OK", ButtonStyle = style, Width = 120, Height = 40, Theme = Theme("DaisyGlassButtonTheme") };
        var window = Show(button);
        try
        {
            // The pointer is hit tested against the rendered scene.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.MouseMove(button.TranslatePoint(new Point(60, 20), window)!.Value);
            Dispatcher.UIThread.RunJobs();

            // The hover color fades in through a transition; the value it fades to is the base value.
            var tint = Part<Border>(button, "PART_Background").GetBaseValue(Border.BackgroundProperty).GetValueOrDefault() as ISolidColorBrush;
            Assert.True(button.IsPointerOver);
            Assert.True(brightens == (tint?.Color == Color.Parse("#80FFFFFF")), $"Hover tint {tint?.Color}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Neumorphic_Shadow_Goes_On_The_Opaque_Border_Of_A_Glass_Button()
    {
        var button = new DaisyButton { Content = "OK", Variant = DaisyButtonVariant.Primary, Theme = Theme("DaisyGlassButtonTheme") };
        DaisyNeumorphic.SetIsEnabled(button, true);
        DaisyNeumorphic.SetMode(button, DaisyNeumorphicMode.Raised);
        var window = Show(button);
        try
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, Part<Border>(button, "PART_Border").BoxShadow.Count);
            Assert.Equal(0, Part<Border>(button, "PART_Background").BoxShadow.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glass_Badge_Tints_Its_Glass_With_The_Variant_Color()
    {
        var badge = new DaisyBadge { Content = "Beta", Variant = DaisyBadgeVariant.Primary, Theme = Theme("DaisyGlassBadgeTheme") };
        DaisyGlassSurface.SetGradient(badge, DaisyGlassGradient.Mint);
        var window = Show(badge);
        try
        {
            var glass = Part<DaisyGlass>(badge, "PART_Glass");
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(GlassBlurMode.SkiaSharp, glass.BlurMode);
            Assert.Equal(DaisyGlassGradient.Mint, glass.GlassGradient);
            AssertPaintedFirst(glass);

            var surface = Part<Border>(badge, "PART_Surface");
            Assert.True(badge.TryFindResource("DaisyPrimaryColor", badge.ActualThemeVariant, out var color));
            Assert.Equal(Assert.IsType<Color>(color), Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color);
            Assert.Equal(0.45, surface.Opacity);

            badge.Variant = DaisyBadgeVariant.Default;
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glass_Navbar_Has_No_Drop_Shadow_And_Passes_Its_Glass_Settings()
    {
        var navbar = new DaisyNavbar { NavbarStart = new TextBlock { Text = "Flowery" }, Theme = Theme("DaisyGlassNavbarTheme") };
        DaisyGlassSurface.SetPattern(navbar, DaisyCardPattern.Dots);
        var window = Show(navbar);
        try
        {
            Assert.False(navbar.HasShadow);
            Assert.Null(navbar.Effect);
            var glass = Part<DaisyGlass>(navbar, "PART_Glass");
            Assert.Equal(DaisyCardPattern.Dots, glass.GlassPattern);
            Assert.Equal(40, glass.GlassBlur);
            AssertPaintedFirst(glass);

            DaisyGlassSurface.SetBlur(navbar, 12);
            Assert.Equal(12, glass.GlassBlur);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glass_Modal_Dialog_Uses_The_Corner_Radii_Of_The_Modal()
    {
        var modal = new DaisyModal
        {
            IsOpen = true,
            Content = new TextBlock { Text = "Hello" },
            TopLeftRadius = 4,
            BottomRightRadius = 30,
            Theme = Theme("DaisyGlassModalTheme")
        };
        var window = Show(modal);
        try
        {
            var glass = Part<DaisyGlass>(modal, "PART_Glass");
            var dialog = Part<Border>(modal, "PART_Dialog");
            Assert.Equal(new CornerRadius(4, 16, 30, 16), dialog.CornerRadius);
            Assert.Equal(dialog.CornerRadius, glass.CornerRadius);
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(50, glass.GlassBlur);
            AssertPaintedFirst(glass);
        }
        finally
        {
            window.Close();
        }
    }

    private static Avalonia.Styling.ControlTheme Theme(string key) => DaisyGlassSurfaceTests.GlassTheme(key);

    // The backdrop holds everything painted before the glass, so the control's own tint and content must come later.
    private static void AssertPaintedFirst(DaisyGlass glass)
    {
        var parent = Assert.IsAssignableFrom<Panel>(glass.GetVisualParent());
        Assert.Equal(0, parent.Children.IndexOf(glass));
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
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);
}
