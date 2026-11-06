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
using Xunit;
using static Flowery.NET.Tests.TestControls;

namespace Flowery.NET.Tests;

/// <summary>
/// The glass themes of the standard button, badge, navbar and modal.
/// </summary>
public class DaisyGlassControlsTests
{
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
            RealInput.RenderFrames();
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
    [InlineData(DaisyButtonStyle.Outline, false)]
    public void Hovering_A_Default_Glass_Button_Brightens_The_Glass_Except_For_Outline_And_Dash(DaisyButtonStyle style, bool brightens)
    {
        var button = new DaisyButton { Content = "OK", ButtonStyle = style, Width = 120, Height = 40, Theme = Theme("DaisyGlassButtonTheme") };
        var window = Show(button);
        try
        {
            // The pointer is hit tested against the rendered scene.
            RealInput.RenderFrames();
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

    private static Avalonia.Styling.ControlTheme Theme(string key) => DaisyGlassSurfaceTests.GlassTheme(key);
}
