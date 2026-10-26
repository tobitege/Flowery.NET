using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyGlassInterfaceTests
{
    [AvaloniaFact]
    public void A_Glass_Segmented_Selects_Its_First_Item_And_Puts_The_Pill_Under_The_Selection()
    {
        var segmented = new DaisyGlassSegmented { ItemsSource = new[] { "Day", "Week", "Month" }, GlassBlur = 12 };
        var window = Show(segmented);
        try
        {
            Assert.Equal(0, segmented.SelectedIndex);
            var glass = Part<DaisyGlass>(segmented, "PART_Glass");
            AssertPaintedFirst(glass);
            Assert.Equal(12, glass.GlassBlur);

            var pill = Part<Border>(segmented, "PART_Indicator");
            AssertPillUnder(segmented, pill, 0);

            segmented.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AssertPillUnder(segmented, pill, 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Glass_Range_Turns_Its_Thumb_Into_A_Lens_Until_Release()
    {
        var range = new DaisyGlassRange { Minimum = 0, Maximum = 100, Value = 50, Width = 300 };
        var window = Show(range);
        try
        {
            var glass = Part<DaisyGlass>(range, "PART_Glass");
            Assert.True(glass.EnableBackdropBlur);
            var thumb = Part<Thumb>(range, "PART_Thumb");
            var lens = Part<Panel>(range, "PART_Lens");
            Assert.Equal(thumb.Bounds.X, Canvas.GetLeft(lens));
            Assert.Equal(thumb.Bounds.Width, lens.Width);
            Assert.Equal(thumb.Bounds.X + thumb.Bounds.Width / 2, Part<Border>(range, "PART_Progress").Width);

            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var center = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(":lens", range.Classes);
            Assert.True(Part<Panel>(range, "PART_LensBody").GetBaseValue(Layoutable.MarginProperty).GetValueOrDefault().Left < 0);

            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(":lens", range.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Checked_Glass_Toggle_Fills_Its_Track_With_The_Variant_Color()
    {
        var toggle = new DaisyGlassToggle { Variant = DaisyToggleVariant.Primary };
        var window = Show(toggle);
        try
        {
            AssertPaintedFirst(Part<DaisyGlass>(toggle, "PART_Glass"));
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

    [AvaloniaFact]
    public void A_Glass_Menu_Marks_Destructive_Items_And_Keeps_Separators_Out_Of_Reach()
    {
        var delete = new ListBoxItem { Content = "Delete", Classes = { "menu-destructive" } };
        var separator = new ListBoxItem { Classes = { "menu-separator" } };
        var menu = new DaisyGlassMenu { Items = { new ListBoxItem { Content = "Pin" }, separator, delete } };
        var window = Show(menu);
        try
        {
            AssertPaintedFirst(Part<DaisyGlass>(menu, "PART_Glass"));
            Assert.True(delete.TryFindResource("DaisyErrorColor", delete.ActualThemeVariant, out var error));
            Assert.Equal(Assert.IsType<Color>(error), Assert.IsAssignableFrom<ISolidColorBrush>(delete.Foreground).Color);
            Assert.False(separator.IsHitTestVisible);
            Assert.False(separator.Focusable);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glassy_Theme_Is_Dark_And_Tints_The_Glass_Controls_Navy()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var button = new DaisyGlassButton { Content = "OK" };
        var window = Show(button);
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Colors.White, button.GlassTint);

            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(DaisyThemeManager.IsDarkTheme("Glassy"));
            Assert.Equal(Color.Parse("#1E2F7A"), button.GlassTint);
            Assert.Equal(button.GlassTint, Part<DaisyGlass>(button, "PART_Glass").GlassTint);
        }
        finally
        {
            window.Close();
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    private static void AssertPillUnder(DaisyGlassSegmented segmented, Border pill, int index)
    {
        var container = Assert.IsAssignableFrom<Control>(segmented.ContainerFromIndex(index));
        var origin = container.TranslatePoint(default, Assert.IsAssignableFrom<Visual>(pill.GetVisualParent()))!.Value;
        var transform = Assert.IsType<TransformOperations>(pill.GetBaseValue(Visual.RenderTransformProperty).GetValueOrDefault());
        Assert.True(pill.IsVisible);
        Assert.Equal(container.Bounds.Width, pill.Width);
        Assert.Equal(origin.X, transform.Operations[0].Data.Translate.X, 3);
        Assert.Equal(origin.Y, transform.Operations[0].Data.Translate.Y, 3);
    }

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
