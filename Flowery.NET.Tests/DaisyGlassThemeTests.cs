using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// The glass themes of the standard controls, which replace the DaisyGlass* controls.
/// </summary>
public class DaisyGlassThemeTests
{
    [AvaloniaFact]
    public void Pressing_A_Glass_Range_Turns_Its_Thumb_Into_A_Lens_Until_Release()
    {
        var range = new DaisyRange { Minimum = 0, Maximum = 100, Value = 50, Width = 300, Theme = Theme("DaisyGlassRangeTheme") };
        var window = Show(range);
        try
        {
            var glass = Part<DaisyGlass>(range, "PART_Glass");
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(30, glass.GlassBlur);
            var lensGlass = Part<DaisyGlass>(range, "PART_LensGlass");
            Assert.True(lensGlass.EnableBackdropBlur);
            Assert.Equal(glass.BlurMode, lensGlass.BlurMode);
            var thumb = Part<Thumb>(range, "PART_Thumb");
            var lens = Part<Panel>(range, "PART_Lens");
            Assert.Equal(thumb.Bounds.X, Canvas.GetLeft(lens));
            Assert.Equal(thumb.Bounds.Width, lens.Width);
            Assert.Equal(thumb.Bounds.X + thumb.Bounds.Width / 2, Part<Border>(range, "PART_Progress").Width);

            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var center = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(":dragging", range.Classes);
            Assert.True(Part<Panel>(range, "PART_LensBody").GetBaseValue(Layoutable.MarginProperty).GetValueOrDefault().Left < 0);
            // The frosted thumb gives way to the clear lens.
            Assert.Equal(0, glass.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault());
            Assert.Equal(1, lensGlass.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault());

            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(":dragging", range.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Moving_The_Value_Moves_The_Lens_And_The_Progress()
    {
        var range = new DaisyRange { Minimum = 0, Maximum = 100, Value = 20, Width = 300, Theme = Theme("DaisyGlassRangeTheme") };
        var window = Show(range);
        try
        {
            range.Value = 80;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var thumb = Part<Thumb>(range, "PART_Thumb");
            Assert.Equal(thumb.Bounds.X, Canvas.GetLeft(Part<Panel>(range, "PART_Lens")));
            Assert.Equal(thumb.Bounds.X + thumb.Bounds.Width / 2, Part<Border>(range, "PART_Progress").Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Checked_Glass_Toggle_Fills_Its_Track_With_The_Variant_Color()
    {
        var toggle = new DaisyToggle { Variant = DaisyToggleVariant.Primary, Theme = Theme("DaisyGlassToggleTheme") };
        var window = Show(toggle);
        try
        {
            var glass = Part<DaisyGlass>(toggle, "PART_Glass");
            AssertPaintedFirst(glass);
            Assert.Equal(20, glass.GlassBlur);
            Assert.Equal(0.18, glass.GlassTintOpacity);
            var fill = Part<Border>(toggle, "PART_TrackFill");
            Assert.Equal(0, fill.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault());

            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, fill.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault());
            Assert.True(toggle.TryFindResource("DaisyPrimaryColor", toggle.ActualThemeVariant, out var color));
            Assert.Equal(Assert.IsType<Color>(color), Assert.IsAssignableFrom<ISolidColorBrush>(fill.Background).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("DaisyGlassToggleTheme")]
    [InlineData(null)]
    public void Dragging_The_Knob_Of_A_Toggle_Switches_It(string? themeKey)
    {
        var toggle = new DaisyToggle();
        if (themeKey != null)
            toggle.Theme = Theme(themeKey);
        var window = Show(toggle);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var knobs = Part<Canvas>(toggle, "PART_MovingKnobs");
            var start = knobs.TranslatePoint(new Point(8, knobs.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(start + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(30, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(30, 0), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(toggle.IsChecked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("DaisyGlassMenuTheme")]
    public void A_Menu_Marks_Destructive_Items_And_Keeps_Separators_Out_Of_Reach(string? themeKey)
    {
        var delete = new ListBoxItem { Content = "Delete", Classes = { "menu-destructive" } };
        var separator = new ListBoxItem { Classes = { "menu-separator" } };
        var menu = new DaisyMenu { Items = { new ListBoxItem { Content = "Pin" }, separator, delete } };
        if (themeKey != null)
            menu.Theme = Theme(themeKey);
        var window = Show(menu);
        try
        {
            if (themeKey != null)
            {
                var glass = Part<DaisyGlass>(menu, "PART_Glass");
                AssertPaintedFirst(glass);
                Assert.Equal(40, glass.GlassBlur);
            }
            Assert.True(delete.TryFindResource("DaisyErrorColor", delete.ActualThemeVariant, out var error));
            Assert.Equal(Assert.IsType<Color>(error), Assert.IsAssignableFrom<ISolidColorBrush>(delete.Foreground).Color);
            Assert.False(separator.IsHitTestVisible);
            Assert.False(separator.Focusable);
            Assert.True(separator.Bounds.Height < 20);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glassy_Theme_Is_Dark_And_Tints_The_Glass_Themes_Navy()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var button = new DaisyButton { Content = "OK", Theme = Theme("DaisyGlassButtonTheme") };
        var window = Show(button);
        try
        {
            var glass = Part<DaisyGlass>(button, "PART_Glass");
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Colors.White, glass.GlassTint);

            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(DaisyThemeManager.IsDarkTheme("Glassy"));
            Assert.Equal(Color.Parse("#1E2F7A"), glass.GlassTint);
        }
        finally
        {
            window.Close();
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    internal static Avalonia.Styling.ControlTheme Theme(string key) => DaisyGlassSurfaceTests.GlassTheme(key);

    // The backdrop holds everything painted before the glass, so the control's own parts must come later.
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
