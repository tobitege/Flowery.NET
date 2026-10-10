using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisySoftButtonTests
{
    [AvaloniaTheory]
    [InlineData(DaisyButtonVariant.Primary)]
    [InlineData(DaisyButtonVariant.Secondary)]
    [InlineData(DaisyButtonVariant.Error)]
    public void Soft_Buttons_Draw_A_Tint_And_A_Border_In_The_Variant_Color(DaisyButtonVariant variant)
    {
        var button = new DaisyButton { Content = "Soft", ButtonStyle = DaisyButtonStyle.Soft, Variant = variant };
        var window = new Window { Width = 300, Height = 200, Content = button };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var background = button.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Background");
            Assert.True(button.TryFindResource($"Daisy{variant}Color", button.ActualThemeVariant, out var color));
            var variantColor = Assert.IsType<Color>(color);

            var fill = Assert.IsAssignableFrom<ISolidColorBrush>(background.Background);
            Assert.Equal(variantColor, fill.Color);
            Assert.InRange(fill.Opacity, 0.05, 0.3);

            var stroke = Assert.IsAssignableFrom<ISolidColorBrush>(background.BorderBrush);
            Assert.Equal(variantColor, stroke.Color);
            Assert.True(stroke.Opacity > fill.Opacity);
            Assert.True(background.BorderThickness.Left > 0);
            Assert.Equal(1, background.Opacity);
        }
        finally
        {
            window.Close();
        }
    }
}
