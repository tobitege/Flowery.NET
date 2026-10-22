using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Helpers;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisySelectionControlSizingTests
{
    [AvaloniaTheory]
    [InlineData(DaisySize.ExtraSmall, 8, 5, 4)]
    [InlineData(DaisySize.Small, 10, 6, 5)]
    [InlineData(DaisySize.Medium, 12, 8, 6)]
    [InlineData(DaisySize.Large, 14, 10, 7)]
    [InlineData(DaisySize.ExtraLarge, 16, 12, 8)]
    public void Indicators_And_Labels_Follow_Size_And_Toggle_Knob_Stays_Inside_Track(
        DaisySize size, double indicatorSize, double checkSize, double radioDotSize)
    {
        using var scope = new SizeScope();
        var check = new DaisyCheckBox { Size = size, Content = "Check label", IsChecked = true };
        var toggle = new DaisyToggle { Size = size, Content = "Toggle label" };
        var radio = new DaisyRadio { Size = size, Content = "Radio label", IsChecked = true };
        var themeCheckbox = new DaisyThemeController { Mode = ThemeControllerMode.Checkbox, Size = size };
        scope.Show(new StackPanel { Children = { check, toggle, radio, themeCheckbox } });
        var box = Part<Border>(check, "PART_Border");
        var mark = Part<Avalonia.Controls.Shapes.Path>(check, "CheckMark");
        var track = Part<Border>(toggle, "SwitchArea");
        var knob = Part<Avalonia.Controls.Shapes.Ellipse>(toggle, "Knob");
        knob.Transitions = null;
        var fontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, size);

        Assert.Equal(indicatorSize, box.Bounds.Height);
        Assert.Equal(indicatorSize, box.Bounds.Width);
        Assert.Equal(checkSize, mark.Bounds.Width);
        Assert.Equal(box.Bounds.Size, Part<Border>(themeCheckbox, "CheckboxMode").Bounds.Size);
        Assert.Equal(mark.Bounds.Size, Part<Avalonia.Controls.Shapes.Path>(themeCheckbox, "CheckMark").Bounds.Size);
        Assert.True(themeCheckbox.Bounds.Height > indicatorSize);
        Assert.True(mark.IsVisible);
        Assert.Equal(fontSize, check.FontSize);
        Assert.Equal(fontSize, toggle.FontSize);
        Assert.Equal(fontSize, check.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Check label").FontSize);
        Assert.True(box.Bounds.Height < fontSize);
        Assert.Equal(indicatorSize, track.Bounds.Height);
        Assert.Equal(indicatorSize * 2, track.Bounds.Width);
        Assert.Equal(indicatorSize - 4, knob.Bounds.Width);
        var radioBorder = Part<Border>(radio, "PART_Border");
        var radioDot = Part<Avalonia.Controls.Shapes.Ellipse>(radio, "CheckMark");
        Assert.Equal(indicatorSize, radioBorder.Bounds.Width);
        Assert.Equal(indicatorSize, radioBorder.Bounds.Height);
        Assert.Equal(radioDotSize, radioDot.Bounds.Width);
        Assert.True(radioDot.IsVisible);
        Assert.Equal(fontSize, radio.FontSize);
        Assert.True(radioBorder.Bounds.Height < radio.FontSize);
        Assert.Equal(fontSize, radio.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Radio label").FontSize);
        Assert.True(check.TryFindResource($"DaisySize{size}Height", out var rowHeight));
        Assert.Equal(Assert.IsType<double>(rowHeight), check.MinHeight);
        Assert.Equal(check.MinHeight, toggle.MinHeight);
        Assert.Equal(check.MinHeight, radio.MinHeight);

        var off = knob.TranslatePoint(default, track)!.Value;
        toggle.IsChecked = true;
        scope.Layout();
        var on = knob.TranslatePoint(default, track)!.Value;
        Assert.True(on.X > off.X);
        Assert.InRange(on.X + knob.Bounds.Width, 0, track.Bounds.Width - 1);
        Assert.InRange(on.Y + knob.Bounds.Height, 0, track.Bounds.Height - 1);
        Assert.Equal(track.Bounds.Width - off.X - knob.Bounds.Width, on.X, 3);
        toggle.IsChecked = false;
        scope.Layout();
        Assert.Equal(off.X, knob.TranslatePoint(default, track)!.Value.X, 3);
    }

    [AvaloniaFact]
    public void Global_Size_Changes_Repeat_And_Respect_Local_Size_And_OptOut()
    {
        using var scope = new SizeScope();
        var check = new DaisyCheckBox { Content = "Auto check" };
        var toggle = new DaisyToggle { Content = "Auto toggle" };
        var radio = new DaisyRadio { Content = "Auto radio" };
        var explicitCheck = new DaisyCheckBox { Size = DaisySize.Large };
        var explicitToggle = new DaisyToggle { Size = DaisySize.Large };
        var explicitRadio = new DaisyRadio { Size = DaisySize.Large };
        var ignoredCheck = new DaisyCheckBox();
        var ignoredToggle = new DaisyToggle();
        var ignoredRadio = new DaisyRadio();
        var ignored = new StackPanel { Children = { ignoredCheck, ignoredToggle, ignoredRadio } };
        FlowerySizeManager.SetIgnoreGlobalSize(ignored, true);
        var panel = new StackPanel { Children = { check, toggle, radio, explicitCheck, explicitToggle, explicitRadio, ignored } };
        scope.Show(panel);

        foreach (var size in new[] { DaisySize.ExtraSmall, DaisySize.ExtraLarge, DaisySize.Medium, DaisySize.Small })
        {
            FlowerySizeManager.ApplySize(size);
            scope.Layout();
            Assert.Equal(size, check.Size);
            Assert.Equal(size, toggle.Size);
            Assert.Equal(size, radio.Size);
            Assert.Equal(FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, size), check.FontSize);
            Assert.Equal(check.FontSize, toggle.FontSize);
            Assert.Equal(check.FontSize, radio.FontSize);
            Assert.True(Part<Border>(check, "PART_Border").Bounds.Height < check.FontSize);
            Assert.True(Part<Border>(toggle, "SwitchArea").Bounds.Height < toggle.FontSize);
            Assert.True(Part<Border>(radio, "PART_Border").Bounds.Height < radio.FontSize);
            Assert.Equal(DaisySize.Large, explicitCheck.Size);
            Assert.Equal(DaisySize.Large, explicitToggle.Size);
            Assert.Equal(DaisySize.Large, explicitRadio.Size);
            Assert.Equal(DaisySize.Medium, ignoredCheck.Size);
            Assert.Equal(DaisySize.Medium, ignoredToggle.Size);
            Assert.Equal(DaisySize.Medium, ignoredRadio.Size);
        }

        scope.Window.Content = null;
        FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
        scope.Window.Content = panel;
        scope.Layout();
        Assert.Equal(DaisySize.ExtraLarge, check.Size);
        Assert.Equal(DaisySize.ExtraLarge, toggle.Size);
        Assert.Equal(DaisySize.ExtraLarge, radio.Size);
    }

    [AvaloniaFact]
    public void Label_Clicks_Still_Toggle_And_Disabled_Controls_Stay_Disabled()
    {
        using var scope = new SizeScope();
        var check = new DaisyCheckBox { Content = "Checkbox", Variant = DaisyCheckBoxVariant.Primary };
        var toggle = new DaisyToggle { Content = "Toggle", Variant = DaisyToggleVariant.Primary };
        var firstRadio = new DaisyRadio { Content = "First radio", GroupName = "Sizing", IsChecked = true };
        var secondRadio = new DaisyRadio { Content = "Second radio", GroupName = "Sizing" };
        scope.Show(new StackPanel { Children = { check, toggle, firstRadio, secondRadio } });
        foreach (var control in new Control[] { check, toggle, secondRadio })
        {
            var label = control.GetVisualDescendants().OfType<TextBlock>().Single(text => !string.IsNullOrEmpty(text.Text));
            var point = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), scope.Window)!.Value;
            scope.Window.MouseMove(point);
            scope.Window.MouseDown(point, MouseButton.Left);
            scope.Window.MouseUp(point, MouseButton.Left);
        }
        Assert.True(check.IsChecked);
        Assert.True(toggle.IsChecked);
        Assert.False(firstRadio.IsChecked);
        Assert.True(secondRadio.IsChecked);
        check.IsEnabled = false;
        toggle.IsEnabled = false;
        secondRadio.IsEnabled = false;
        FlowerySizeManager.ApplySize(DaisySize.Large);
        scope.Layout();
        Assert.False(check.IsEffectivelyEnabled);
        Assert.False(toggle.IsEffectivelyEnabled);
        Assert.False(secondRadio.IsEffectivelyEnabled);
        Assert.True(check.IsChecked);
        Assert.True(toggle.IsChecked);
        Assert.True(Part<Avalonia.Controls.Shapes.Path>(check, "CheckMark").IsVisible);
        Assert.True(secondRadio.IsChecked);
    }

    [AvaloniaFact]
    public void Application_Size_Styles_Win_And_Removal_Restores_Global_Default()
    {
        using var scope = new SizeScope();
        var check = new DaisyCheckBox { Content = "Check" };
        var toggle = new DaisyToggle { Content = "Toggle" };
        var radio = new DaisyRadio { Content = "Radio" };
        scope.Window.Styles.Add(new Style(static selector => selector.OfType<DaisyCheckBox>())
            { Setters = { new Setter(DaisyCheckBox.SizeProperty, DaisySize.Small) } });
        scope.Window.Styles.Add(new Style(static selector => selector.OfType<DaisyToggle>())
            { Setters = { new Setter(DaisyToggle.SizeProperty, DaisySize.Small) } });
        scope.Window.Styles.Add(new Style(static selector => selector.OfType<DaisyRadio>())
            { Setters = { new Setter(DaisyRadio.SizeProperty, DaisySize.Small) } });
        scope.Show(new StackPanel { Children = { check, toggle, radio } });
        FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
        scope.Layout();
        Assert.Equal(DaisySize.Small, check.Size);
        Assert.Equal(DaisySize.Small, toggle.Size);
        Assert.Equal(DaisySize.Small, radio.Size);

        scope.Window.Styles.Clear();
        scope.Layout();
        Assert.Equal(DaisySize.ExtraLarge, check.Size);
        Assert.Equal(DaisySize.ExtraLarge, toggle.Size);
        Assert.Equal(DaisySize.ExtraLarge, radio.Size);
        check.Size = DaisySize.ExtraLarge; // Same value must still establish a local override.
        FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
        scope.Layout();
        Assert.Equal(DaisySize.ExtraLarge, check.Size);
        Assert.Equal(DaisySize.ExtraSmall, toggle.Size);
    }

    [AvaloniaFact]
    public void Scale_And_Size_Update_Font_And_Indicator_Together_Without_Local_Font_Writes()
    {
        using var scope = new SizeScope();
        var check = new DaisyCheckBox { Content = "Check" };
        var toggle = new DaisyToggle { Content = "Toggle" };
        var radio = new DaisyRadio { Content = "Radio" };
        var panel = new StackPanel { Children = { check, toggle, radio } };
        FloweryScaleManager.SetEnableScaling(panel, true);
        scope.Show(panel);
        foreach (var size in new[] { DaisySize.ExtraLarge, DaisySize.Small, DaisySize.Medium })
        {
            FlowerySizeManager.ApplySize(size);
            foreach (var factor in new[] { 1d, 0.75d })
            {
                check.ApplyScaleFactor(factor);
                toggle.ApplyScaleFactor(factor);
                radio.ApplyScaleFactor(factor);
                scope.Layout();
                var expectedFont = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, size) * factor;
                Assert.Equal(expectedFont, check.FontSize, 3);
                Assert.Equal(expectedFont, toggle.FontSize, 3);
                Assert.Equal(expectedFont, radio.FontSize, 3);
                foreach (var control in new Control[] { check, toggle, radio })
                {
                    var indicator = Part<Border>(control, control is DaisyToggle ? "SwitchArea" : "PART_Border");
                    var transform = indicator.TransformToVisual(control)!.Value;
                    var height = new Rect(indicator.Bounds.Size).TransformToAABB(transform).Height;
                    Assert.True(height < expectedFont);
                    Assert.Equal(indicator.Bounds.Height * factor, height, 3);
                }
            }
        }
        check.FontSize = 30;
        check.ApplyScaleFactor(1);
        FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
        scope.Layout();
        Assert.Equal(30, check.FontSize);
    }

    [AvaloniaFact]
    public void Theme_Checkbox_Follows_Global_Size_Across_Reattachment_And_Scales_The_Indicator()
    {
        using var scope = new SizeScope();
        var controller = new DaisyThemeController { Mode = ThemeControllerMode.Checkbox };
        var fixedController = new DaisyThemeController { Mode = ThemeControllerMode.Checkbox, Size = DaisySize.Large };
        var ignoredController = new DaisyThemeController { Mode = ThemeControllerMode.Checkbox };
        FlowerySizeManager.SetIgnoreGlobalSize(ignoredController, true);
        var panel = new StackPanel { Children = { controller, fixedController, ignoredController } };
        scope.Show(panel);
        var box = Part<Border>(controller, "CheckboxMode");
        Assert.Equal(10, box.Bounds.Height); // The reported Small case used to be fixed at 48.

        foreach (var (size, height) in new[]
                 {
                     (DaisySize.ExtraLarge, 16d), (DaisySize.ExtraSmall, 8d),
                     (DaisySize.Medium, 12d), (DaisySize.Small, 10d)
                 })
        {
            FlowerySizeManager.ApplySize(size);
            scope.Layout();
            Assert.Equal(size, controller.Size);
            Assert.Equal(height, box.Bounds.Height);
            Assert.Equal(14, Part<Border>(fixedController, "CheckboxMode").Bounds.Height);
            Assert.Equal(12, Part<Border>(ignoredController, "CheckboxMode").Bounds.Height);
        }

        scope.Window.Content = null;
        FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
        scope.Window.Content = panel;
        controller.ApplyScaleFactor(0.75);
        scope.Layout();
        Assert.Equal(DaisySize.ExtraLarge, controller.Size);
        var transform = box.TransformToVisual(controller)!.Value;
        Assert.Equal(12, new Rect(box.Bounds.Size).TransformToAABB(transform).Height, 3);

        scope.Window.Styles.Add(new Style(static selector => selector.OfType<DaisyThemeController>())
            { Setters = { new Setter(DaisyThemeController.SizeProperty, DaisySize.Small) } });
        FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
        scope.Layout();
        Assert.Equal(DaisySize.Small, controller.Size);
        Assert.Equal(10, box.Bounds.Height);
        Assert.Equal(DaisySize.Large, fixedController.Size);
    }

    [AvaloniaFact]
    public void Checked_Knob_That_Matches_The_Track_Gets_Contrast_An_Outline_And_A_Variant_Border()
    {
        using var scope = new SizeScope();
        var toggle = new DaisyToggle { Content = "Toggle", Variant = DaisyToggleVariant.Secondary, IsChecked = true };
        scope.Show(toggle);
        var track = Part<Border>(toggle, "SwitchArea");
        var knob = Part<Avalonia.Controls.Shapes.Ellipse>(toggle, "Knob");
        Assert.True(toggle.TryFindResource("DaisySecondaryBrush", toggle.ActualThemeVariant, out var resource));
        var secondary = Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color;

        // A track painted in the Secondary color hides a Secondary knob unless the toggle adjusts it.
        track.Background = new SolidColorBrush(secondary);
        scope.Layout();
        var knobColor = Assert.IsAssignableFrom<ISolidColorBrush>(knob.Fill).Color;
        Assert.NotEqual(secondary, knobColor);
        Assert.True(FloweryColorHelpers.GetColorDifference(knobColor, secondary) >= 20);
        Assert.Equal(1, Part<Avalonia.Controls.Shapes.Ellipse>(toggle, "PART_KnobOutline").StrokeThickness);
        Assert.Equal(secondary, Assert.IsAssignableFrom<ISolidColorBrush>(track.BorderBrush).Color);

        // Unchecked, the styles are in charge again: the plain knob and the neutral track border.
        toggle.IsChecked = false;
        scope.Layout();
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(knob.Fill).Color);
        Assert.NotEqual(secondary, Assert.IsAssignableFrom<ISolidColorBrush>(track.BorderBrush).Color);

        toggle.IsChecked = true;
        scope.Layout();
        Assert.Equal(knobColor, Assert.IsAssignableFrom<ISolidColorBrush>(knob.Fill).Color);
    }

    [AvaloniaFact]
    public void Checked_Info_Toggle_Uses_The_Info_Color()
    {
        using var scope = new SizeScope();
        var toggle = new DaisyToggle { Content = "Toggle", Variant = DaisyToggleVariant.Info, IsChecked = true };
        scope.Show(toggle);
        Assert.True(toggle.TryFindResource("DaisyInfoBrush", toggle.ActualThemeVariant, out var resource));
        var info = Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color;
        Assert.Equal(info, Assert.IsAssignableFrom<ISolidColorBrush>(Part<Border>(toggle, "SwitchArea").BorderBrush).Color);
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private sealed class SizeScope : IDisposable
    {
        private readonly Window? _previousWindow = FlowerySizeManager.MainWindow;
        private readonly DaisySize _previousSize = FlowerySizeManager.CurrentSize;
        private readonly bool _auto = FlowerySizeManager.EnableGlobalAutoSize;
        private readonly bool _useGlobal = FlowerySizeManager.UseGlobalSizeByDefault;
        public Window Window { get; } = new() { Width = 500, Height = 600 };

        public SizeScope()
        {
            FlowerySizeManager.MainWindow = null;
            FlowerySizeManager.EnableGlobalAutoSize = true;
            FlowerySizeManager.UseGlobalSizeByDefault = true;
            FlowerySizeManager.ApplySize(DaisySize.Small);
        }

        public void Show(Control content)
        {
            Window.Content = content;
            Window.Show();
            FlowerySizeManager.MainWindow = Window;
            FlowerySizeManager.RefreshAllSizes();
            Layout();
        }

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }

        public void Dispose()
        {
            Window.Close();
            FlowerySizeManager.MainWindow = null;
            FlowerySizeManager.ApplySize(_previousSize);
            FlowerySizeManager.EnableGlobalAutoSize = _auto;
            FlowerySizeManager.UseGlobalSizeByDefault = _useGlobal;
            FlowerySizeManager.MainWindow = _previousWindow;
        }
    }
}
