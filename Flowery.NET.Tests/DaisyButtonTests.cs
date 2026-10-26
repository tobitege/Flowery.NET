using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.HeadlessTestingFramework;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyButtonTests
    {
        [AvaloniaFact]
        public void Variant_Paints_The_Theme_Colors_And_The_Hover_Color_Under_The_Pointer()
        {
            var button = new DaisyButton { Content = "Save" };
            Assert.Equal(DaisyButtonVariant.Default, button.Variant);
            using var window = new TestWindow(new StackPanel { Margin = new Thickness(40), Children = { button } });

            button.Variant = DaisyButtonVariant.Primary;
            Assert.Same(Brush(button, "DaisyPrimaryBrush"), button.Background);
            Assert.Same(Brush(button, "DaisyPrimaryContentBrush"), button.Foreground);

            var background = button.GetByName<Border>("PART_Background");
            // The hover colour fades in; the test checks the colour, not the fade.
            background.Transitions = null;
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window.Window)!.Value;
            window.Window.MouseMove(center);
            Assert.Same(Brush(button, "DaisyPrimaryFocusBrush"), background.Background);
            window.Window.MouseMove(new Point(5, 5));
            Assert.Same(Brush(button, "DaisyPrimaryBrush"), background.Background);

            button.Variant = DaisyButtonVariant.Secondary;
            Assert.Same(Brush(button, "DaisySecondaryBrush"), button.Background);
            Assert.Same(Brush(button, "DaisySecondaryContentBrush"), button.Foreground);
        }

        private static IBrush Brush(Control control, string key)
        {
            Assert.True(control.TryFindResource(key, control.ActualThemeVariant, out var value), key);
            return Assert.IsAssignableFrom<IBrush>(value);
        }
    }
}
