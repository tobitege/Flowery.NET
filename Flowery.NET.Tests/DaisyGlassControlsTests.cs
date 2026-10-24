using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
using DaisyBadgeVariant = Flowery.Controls.DaisyBadgeVariant;
using DaisyLabelPosition = Flowery.Controls.DaisyLabelPosition;

namespace Flowery.NET.Tests;

public class DaisyGlassControlsTests
{
    [AvaloniaFact]
    public void A_Glass_Badge_Tints_Its_Glass_With_The_Variant_Color()
    {
        var badge = new DaisyGlassBadge { Content = "Beta", Variant = DaisyBadgeVariant.Primary, GlassGradient = DaisyGlassGradient.Mint };
        var window = Show(badge);
        try
        {
            Assert.True(badge.EnableBackdropBlur);
            Assert.Equal(GlassBlurMode.SkiaSharp, badge.BlurMode);
            Assert.Equal(0.45, badge.GlassColorOpacity);

            var glass = Part<DaisyGlass>(badge, "PART_Glass");
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
    public void A_Glass_Navbar_Has_No_Drop_Shadow_And_Passes_Its_Glass_Properties()
    {
        var navbar = new DaisyGlassNavbar { NavbarStart = new TextBlock { Text = "Flowery" }, GlassPattern = DaisyCardPattern.Dots };
        var window = Show(navbar);
        try
        {
            Assert.False(navbar.HasShadow);
            Assert.Null(navbar.Effect);
            var glass = Part<DaisyGlass>(navbar, "PART_Glass");
            Assert.Equal(DaisyCardPattern.Dots, glass.GlassPattern);
            Assert.Equal(40, glass.GlassBlur);
            AssertPaintedFirst(glass);

            navbar.GlassBlur = 12;
            Assert.Equal(12, glass.GlassBlur);
            Assert.Equal(0.25, Part<Border>(navbar, "PART_Tint").Opacity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glass_Modal_Dialog_Uses_The_Corner_Radii_Of_The_Modal()
    {
        var modal = new DaisyGlassModal { IsOpen = true, Content = new TextBlock { Text = "Hello" }, TopLeftRadius = 4, BottomRightRadius = 30 };
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

    [AvaloniaTheory]
    [InlineData(DaisyLabelPosition.Top)]
    [InlineData(DaisyLabelPosition.Floating)]
    public void A_Glass_Input_Puts_Its_Glass_Behind_The_Field(DaisyLabelPosition position)
    {
        var input = new DaisyGlassInput { Label = "Email", LabelPosition = position, Foreground = Brushes.White, GlassBlur = 18 };
        var window = Show(input);
        try
        {
            var glass = Assert.Single(input.GetVisualDescendants().OfType<DaisyGlass>());
            var field = Part<Border>(input, "PART_Background");
            var panel = Assert.IsAssignableFrom<Panel>(field.GetVisualParent());
            Assert.Equal(panel.Children.IndexOf(field) - 1, panel.Children.IndexOf(glass));
            Assert.Equal(field.CornerRadius, glass.CornerRadius);
            Assert.Equal(18, glass.GlassBlur);

            var text = Part<TextPresenter>(input, "PART_TextPresenter");
            Assert.Same(Brushes.White, text.GetValue(TextElement.ForegroundProperty));
            Assert.Same(Brushes.White, Part<TextBlock>(input, "PART_Watermark").Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glass_Input_Keeps_One_Glass_When_Its_Template_Changes()
    {
        var input = new DaisyGlassInput { Label = "Email" };
        var window = Show(input);
        try
        {
            input.LabelPosition = DaisyLabelPosition.Floating;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var glass = Assert.Single(input.GetVisualDescendants().OfType<DaisyGlass>());
            input.GlassTintOpacity = 0.42;
            Assert.Equal(0.42, glass.GlassTintOpacity);
        }
        finally
        {
            window.Close();
        }
    }

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
