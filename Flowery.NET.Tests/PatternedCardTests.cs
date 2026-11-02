using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
#if FLOWERY_SVG_PATTERNS
using Avalonia;
using Avalonia.Media.Imaging;
using Flowery.Helpers;
using SkiaSharp;
#endif

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyPatternedCard tiles its SVG pattern assets by default, drawn with Svg.Skia: white patterns on dark themes, black
/// ones on light themes. The asset mode looked for PNG files that the library never shipped and always fell back to the
/// generated tiles, which only the Tiled mode draws now. The headless platform keeps no real bitmap pixels, so the
/// pixel checks read the tiles that Svg.Skia draws.
/// </summary>
public class PatternedCardTests
{
    private static readonly DaisyCardPattern[] Patterns =
    [
        DaisyCardPattern.CarbonFiber, DaisyCardPattern.Dots, DaisyCardPattern.Grid, DaisyCardPattern.Stripes,
        DaisyCardPattern.Noise, DaisyCardPattern.Honeycomb, DaisyCardPattern.Circuit, DaisyCardPattern.Twill,
        DaisyCardPattern.DiamondPlate, DaisyCardPattern.Mesh, DaisyCardPattern.Perforated, DaisyCardPattern.Bumps,
        DaisyCardPattern.Scales
    ];

#if FLOWERY_SVG_PATTERNS
    [AvaloniaFact]
    public void A_Card_Tiles_The_Svg_Asset_Of_Each_Pattern_By_Default()
    {
        var card = new DaisyPatternedCard { Width = 200, Height = 100 };
        Assert.Equal(FloweryPatternMode.SvgAsset, card.PatternMode);
        using var window = new TestWindow(card);

        foreach (var pattern in Patterns)
        {
            card.Pattern = pattern;
            window.Window.UpdateLayout();
            var tiles = card.GetVisualDescendants().OfType<Image>().ToArray();
            // 200 x 100 needs two by one tiles of 120, plus one more in each direction.
            Assert.Equal(6, tiles.Length);
            var bitmap = Assert.IsAssignableFrom<Bitmap>(tiles[0].Source);
            Assert.All(tiles, tile => Assert.Same(bitmap, tile.Source));
            Assert.Equal(new PixelSize(120, 120), bitmap.PixelSize);
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark", "white", 255)]
    [InlineData("Light", "black", 0)]
    public void Each_Svg_Asset_Draws_In_The_Color_Of_The_Theme(string theme, string folder, byte channel)
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme(theme));
            foreach (var pattern in Patterns)
            {
                var path = FloweryPatternSvgLoader.GetAssetPath(pattern);
                Assert.NotNull(path);
                Assert.Contains($"/Patterns/{folder}/", path);
                foreach (var scale in new[] { 1, 2 })
                {
                    using var tile = FloweryPatternSvgLoader.RenderTile(path, scale);
                    Assert.NotNull(tile);
                    Assert.Equal(120 * scale, tile.Width);
                    Assert.Equal(120 * scale, tile.Height);
                    var opaque = OpaquePixels(tile);
                    Assert.True(opaque.Count > 0, $"The {folder} asset of {pattern} drew nothing.");
                    Assert.All(opaque, color =>
                        Assert.True(Math.Abs(color.Red - channel) <= 8 && Math.Abs(color.Green - channel) <= 8 && Math.Abs(color.Blue - channel) <= 8,
                            $"The {folder} asset of {pattern} drew {color}."));
                }
            }
        }
        finally
        {
            DaisyThemeManager.ApplyTheme(previous ?? "Light");
        }
    }
#endif

    // Without the SVG patterns (FlowerySvgPatterns=false), the default asset mode draws the generated tiles as well.
    [AvaloniaTheory]
    [InlineData(FloweryPatternMode.Tiled)]
#if !FLOWERY_SVG_PATTERNS
    [InlineData(FloweryPatternMode.SvgAsset)]
#endif
    public void The_Generated_Tiles_Need_No_Images(FloweryPatternMode mode)
    {
        var card = new DaisyPatternedCard { Width = 200, Height = 100, PatternMode = mode };
        using var window = new TestWindow(card);

        foreach (var pattern in Patterns)
        {
            card.Pattern = pattern;
            window.Window.UpdateLayout();
            Assert.True(card.GetVisualDescendants().OfType<Canvas>().Sum(canvas => canvas.Children.Count) > 0,
                $"{pattern} draws no generated tiles.");
            Assert.Empty(card.GetVisualDescendants().OfType<Image>());
        }
    }

#if FLOWERY_SVG_PATTERNS
    // The unpremultiplied colors of the pixels that are opaque enough to keep their color exact.
    private static List<SKColor> OpaquePixels(SKBitmap tile)
    {
        var colors = new List<SKColor>();
        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var color = tile.GetPixel(x, y);
                if (color.Alpha >= 24)
                    colors.Add(color);
            }
        }
        return colors;
    }
#endif
}
