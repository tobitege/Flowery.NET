using Flowery.Skia;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// The lens of a SkiaSharp glass, drawn on a raster surface: how it bends, magnifies and saturates a backdrop.
/// </summary>
public class SkiaGlassLensTests
{
    // Red left of x = 100, blue from there on; the glass starts at the color edge and is 80 by 80.
    private static readonly SKRect Glass = new(100, 10, 180, 90);

    [Fact]
    public void The_Rim_Shows_What_Lies_Beyond_The_Edge_And_The_Middle_Stays()
    {
        using var flat = Draw(SkiaGlassLens.None, SplitEdge);
        using var bent = Draw(new SkiaGlassLens(1, 0.5, 1, 1), SplitEdge);

        Assert.True(IsBlue(flat.GetPixel(103, 50)), $"{flat.GetPixel(103, 50)}");
        Assert.True(IsRed(bent.GetPixel(103, 50)), $"{bent.GetPixel(103, 50)}");
        Assert.True(IsBlue(bent.GetPixel(140, 50)), $"{bent.GetPixel(140, 50)}");
        // The bezel is half of half the shorter side, 20 pixels, and the edge looks one bezel width beyond itself.
        Assert.Equal(21, new SkiaGlassLens(1, 0.5, 1, 1).Reach(Glass, 0), 3);
        Assert.Equal(25, new SkiaGlassLens(1, 0.5, 1, 1).Reach(Glass, 4), 3);
        // A zoom below 1 shows the backdrop around the glass: at 0.8 a quarter of half the glass beyond its edge.
        Assert.Equal(11, new SkiaGlassLens(0, 0.5, 0.8, 1).Reach(Glass, 0), 3);
    }

    [Fact]
    public void A_Zoom_Magnifies_The_Backdrop_Around_The_Middle()
    {
        // A red stripe four pixels wide right of the middle of the glass, at x = 140.
        static void Stripe(SKCanvas canvas)
        {
            canvas.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(SKRect.Create(140, 0, 4, 100), red);
        }

        using var flat = Draw(SkiaGlassLens.None, Stripe);
        using var zoomed = Draw(new SkiaGlassLens(0, 0.05, 2, 1), Stripe);

        Assert.True(IsBlue(flat.GetPixel(146, 50)));
        // Twice as large: x = 146 shows x = 143 of the backdrop.
        Assert.True(IsRed(zoomed.GetPixel(146, 50)), $"{zoomed.GetPixel(146, 50)}");
    }

    private static void SplitEdge(SKCanvas canvas)
    {
        canvas.Clear(SKColors.Blue);
        using var red = new SKPaint { Color = SKColors.Red };
        canvas.DrawRect(SKRect.Create(0, 0, 100, 100), red);
    }

    // Draws the backdrop through the lens over the glass on a raster surface; the whole surface holds backdrop unless
    // valid says otherwise.
    private static SKBitmap Draw(SkiaGlassLens lens, Action<SKCanvas> paintBackdrop, SKRect? valid = null)
    {
        using var source = SKSurface.Create(new SKImageInfo(240, 100));
        paintBackdrop(source.Canvas);
        using var image = source.Snapshot();
        using var backdrop = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        using var shader = lens.CreateShader(backdrop, Glass, 0, 0, valid ?? SKRect.Create(240, 100));
        var bitmap = new SKBitmap(240, 100);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(Glass, paint);
        return bitmap;
    }

    private static bool IsRed(SKColor color) => color.Red > 200 && color.Blue < 60;

    private static bool IsBlue(SKColor color) => color.Blue > 200 && color.Red < 60;
}
