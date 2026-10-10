using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyNumberFlowDigitForegroundTests
{
    [AvaloniaFact]
    public void Digits_Use_Neutral_Content_Brush_Inside_Digit_Boxes()
    {
        var plain = new DaisyNumberFlow { Value = 42, FormatString = "00" };
        var boxed = new DaisyNumberFlow { Value = 42, FormatString = "00", ShowDigitBoxes = true };
        var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Children = { plain, boxed } } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.True(window.TryFindResource("DaisyBaseContentBrush", window.ActualThemeVariant, out var baseContent));
            Assert.True(window.TryFindResource("DaisyNeutralContentBrush", window.ActualThemeVariant, out var neutralContent));

            Assert.Same(baseContent, Digit(plain).Foreground);
            Assert.Same(neutralContent, Digit(boxed).Foreground);

            boxed.ShowDigitBoxes = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(baseContent, Digit(boxed).Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    private static TextBlock Digit(DaisyNumberFlow numberFlow) =>
        numberFlow.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "4" && text.IsVisible);
}
