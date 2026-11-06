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
using SkiaSharp;
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
    public void A_Card_Keeps_Its_Own_Foreground_And_Follows_The_Theme_Otherwise()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            var gold = new SolidColorBrush(Color.Parse("#FFD700"));
            var custom = new DaisyPatternedCard { Width = 200, Height = 100, Pattern = DaisyCardPattern.Honeycomb, Foreground = gold };
            var themed = new DaisyPatternedCard { Width = 200, Height = 100, Pattern = DaisyCardPattern.Honeycomb };
            using var window = new TestWindow(new StackPanel { Children = { custom, themed } });
            var lightBrush = PatternLayer(themed).Fill;

            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            window.Window.UpdateLayout();

            Assert.Same(gold, PatternLayer(custom).Fill);
            Assert.Same(themed.Foreground, PatternLayer(themed).Fill);
            Assert.NotEqual(BrushColor(lightBrush), BrushColor(PatternLayer(themed).Fill));
        }
        finally
        {
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }

    [AvaloniaFact]
    public void Each_Svg_Mask_Is_White_With_The_Opacity_Of_Its_Pattern()
    {
        foreach (var pattern in Patterns)
        {
            var path = FloweryPatternSvgLoader.GetAssetPath(pattern);
            Assert.NotNull(path);
            foreach (var scale in new[] { 1, 2 })
            {
                using var tile = FloweryPatternSvgLoader.RenderTile(path, scale);
                Assert.NotNull(tile);
                Assert.Equal(120 * scale, tile.Width);
                Assert.Equal(120 * scale, tile.Height);
                var covered = CoveredPixels(tile);
                Assert.True(covered.Count > 0, $"The mask of {pattern} is empty.");
                // The patterns are faint: no shape draws above 0.25 opacity, and where two overlap, as in Perforated
                // (0.1 under 0.2), they stay below 0.3.
                Assert.All(covered, color => Assert.True(color.Alpha <= 0.3 * 255, $"The mask of {pattern} has alpha {color.Alpha}."));
                Assert.All(covered.Where(color => color.Alpha >= 24), color =>
                    Assert.True(color.Red >= 247 && color.Green >= 247 && color.Blue >= 247, $"The mask of {pattern} is not white: {color}."));
            }
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

    [AvaloniaFact]
    public void The_Mask_Matches_The_Scaling_Of_The_Display()
    {
        Assert.Equal(1, FloweryPatternSvgLoader.GetMaskScale(1));
        Assert.Equal(2, FloweryPatternSvgLoader.GetMaskScale(1.25));
        Assert.Equal(2, FloweryPatternSvgLoader.GetMaskScale(2));
        Assert.Equal(4, FloweryPatternSvgLoader.GetMaskScale(5));
        var normal = FloweryPatternSvgLoader.GetMask(DaisyCardPattern.Dots, 1);
        var high = FloweryPatternSvgLoader.GetMask(DaisyCardPattern.Dots, 1.5);
        Assert.NotNull(normal);
        Assert.NotNull(high);
        Assert.Equal(new PixelSize(120, 120), normal.PixelSize);
        Assert.Equal(new PixelSize(240, 240), high.PixelSize);
        Assert.Same(high, FloweryPatternSvgLoader.GetMask(DaisyCardPattern.Dots, 2));
    }

    private static List<SKColor> CoveredPixels(SKBitmap tile)
    {
        var colors = new List<SKColor>();
        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var color = tile.GetPixel(x, y);
                if (color.Alpha > 0)
                    colors.Add(color);
            }
        }
        return colors;
    }

    private static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
#endif

    // Without the SVG patterns (FlowerySvgPatterns=false), the default asset mode draws the generated tiles as well.
    [AvaloniaTheory]
    [InlineData(FloweryPatternMode.Tiled)]
#if !FLOWERY_SVG_PATTERNS
    [InlineData(FloweryPatternMode.SvgAsset)]
#endif
    public void The_Generated_Tiles_Need_No_Mask(FloweryPatternMode mode)
    {
        var card = new DaisyPatternedCard { Width = 200, Height = 100, PatternMode = mode };
        using var window = new TestWindow(card);

        foreach (var pattern in Patterns)
        {
            card.Pattern = pattern;
            window.Window.UpdateLayout();
            Assert.True(card.GetVisualDescendants().OfType<Canvas>().Sum(canvas => canvas.Children.Count) > 0,
                $"{pattern} draws no generated tiles.");
            Assert.DoesNotContain(card.GetVisualDescendants().OfType<Shape>(), shape => shape.OpacityMask is ImageBrush);
        }
    }

    private static Rectangle PatternLayer(DaisyPatternedCard card) =>
        Assert.Single(card.GetVisualDescendants().OfType<Rectangle>(), rectangle => rectangle.OpacityMask is ImageBrush);
}
