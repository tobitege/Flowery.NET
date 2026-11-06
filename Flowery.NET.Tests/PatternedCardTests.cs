using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
#if FLOWERY_SVG_PATTERNS
using Avalonia;
using Flowery.Helpers;
#endif

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyPatternedCard tiles its SVG pattern assets by default, drawn with Svg.Skia as a mask that the foreground brush
/// of the card fills, as it colors the generated tiles. The asset mode looked for PNG files that the library never
/// shipped and always fell back to the generated tiles. The headless platform keeps no real bitmap pixels, so the pixel
/// checks read the masks that Svg.Skia draws.
/// </summary>
public class PatternedCardTests
{
    // Every pattern, so one added later is checked too.
    private static readonly DaisyCardPattern[] Patterns =
        [.. Enum.GetValues<DaisyCardPattern>().Where(static pattern => pattern != DaisyCardPattern.None)];

#if FLOWERY_SVG_PATTERNS
    [AvaloniaFact]
    public void A_Card_Fills_The_Tiled_Svg_Mask_Of_Each_Pattern_With_Its_Foreground_By_Default()
    {
        var card = new DaisyPatternedCard { Width = 200, Height = 100 };
        Assert.Equal(FloweryPatternMode.SvgAsset, card.PatternMode);
        using var window = new TestWindow(card);

        foreach (var pattern in Patterns)
        {
            card.Pattern = pattern;
            window.Window.UpdateLayout();
            var layer = PatternLayer(card);
            Assert.Equal(200, layer.Width);
            Assert.Equal(100, layer.Height);
            Assert.Same(card.Foreground, layer.Fill);
            var mask = Assert.IsType<ImageBrush>(layer.OpacityMask);
            Assert.Same(FloweryPatternSvgLoader.GetMask(pattern, 1), mask.Source);
            Assert.Equal(TileMode.Tile, mask.TileMode);
            Assert.Equal(new RelativeRect(0, 0, 120, 120, RelativeUnit.Absolute), mask.DestinationRect);
            Assert.Empty(card.GetVisualDescendants().OfType<Image>());
        }
    }

    [AvaloniaFact]
    public void A_Card_Draws_Its_Pattern_Again_When_The_Display_Scaling_Changes()
    {
        var card = new DaisyPatternedCard { Width = 200, Height = 100, Pattern = DaisyCardPattern.Dots };
        using var window = new TestWindow(card);
        var before = PatternLayer(card);
        var reportScaling = ScalingCallback(window.Window);

        reportScaling(2);

        Assert.NotSame(before, PatternLayer(card));
        card.PatternMode = FloweryPatternMode.Tiled;
        var tiles = card.GetVisualDescendants().OfType<Canvas>().SelectMany(canvas => canvas.Children).ToArray();
        reportScaling(1);
        Assert.Equal(tiles, card.GetVisualDescendants().OfType<Canvas>().SelectMany(canvas => canvas.Children));
    }

    // The callback through which the platform window reports a move to a display with another scaling; Avalonia keeps
    // it internal, so the test reads it from the headless window.
    private static Action<double> ScalingCallback(Window window)
    {
        var platform = window.PlatformImpl ?? throw new InvalidOperationException("The window has no platform window.");
        var property = platform.GetType().GetProperty("ScalingChanged")
            ?? throw new InvalidOperationException("The headless window has no ScalingChanged callback.");
        return property.GetValue(platform) as Action<double>
            ?? throw new InvalidOperationException("The window did not register for scaling changes.");
    }
#endif

    private static Rectangle PatternLayer(DaisyPatternedCard card) =>
        Assert.Single(card.GetVisualDescendants().OfType<Rectangle>(), rectangle => rectangle.OpacityMask is ImageBrush);
}
