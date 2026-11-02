using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// Card patterns are generated tiles in both modes. The asset mode looked for PNG files that the library never shipped
/// and fell back to the tiles; that path is gone.
/// </summary>
public class PatternedCardTests
{
    [AvaloniaFact]
    public void The_Asset_Mode_Draws_The_Same_Generated_Tiles_As_The_Tiled_Mode()
    {
        var patterns = new[] { DaisyCardPattern.CarbonFiber, DaisyCardPattern.Honeycomb, DaisyCardPattern.Circuit,
            DaisyCardPattern.DiamondPlate, DaisyCardPattern.Dots, DaisyCardPattern.Stripes };
        var tiled = new DaisyPatternedCard { Width = 200, Height = 100, PatternMode = FloweryPatternMode.Tiled };
        var asset = new DaisyPatternedCard { Width = 200, Height = 100, PatternMode = FloweryPatternMode.SvgAsset };
        using var window = new TestWindow(new StackPanel { Children = { tiled, asset } });

        foreach (var pattern in patterns)
        {
            tiled.Pattern = pattern;
            asset.Pattern = pattern;
            window.Window.UpdateLayout();
            var tiledShapes = CountPatternChildren(tiled);
            Assert.True(tiledShapes > 0, $"{pattern} draws no pattern.");
            Assert.Equal(tiledShapes, CountPatternChildren(asset));
            Assert.Empty(asset.GetVisualDescendants().OfType<Image>());
        }
    }

    private static int CountPatternChildren(DaisyPatternedCard card) =>
        card.GetVisualDescendants().OfType<Canvas>().Sum(canvas => canvas.Children.Count);
}
