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
    public void Only_A_Bend_A_Zoom_Or_A_Rim_Saturation_Makes_A_Lens()
    {
        Assert.False(SkiaGlassLens.None.IsActive);
        Assert.Equal(0, SkiaGlassLens.None.Reach(Glass, 4));
        Assert.True(new SkiaGlassLens(0.3, 0.5, 1, 1).IsActive);
        Assert.True(new SkiaGlassLens(0, 0.5, 1.1, 1).IsActive);
        Assert.True(new SkiaGlassLens(0, 0.5, 1, 1.4).IsActive);
    }

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
    public void A_Lens_Claims_The_Area_Around_The_Glass_It_Shows_So_A_Change_There_Redraws_It()
    {
        static SkiaGlassDrawOperation Operation(SkiaGlassLens lens) => new(new SkiaGlassRedraw(new Avalonia.Controls.Border()),
            new Avalonia.Rect(0, 0, 80, 80), 20, Avalonia.Media.Colors.White, 0.1, 8, 1, 100, 0.15, 0.65, 0.6, 0.35, 0, lens);

        using var flat = Operation(SkiaGlassLens.None);
        using var bent = Operation(new SkiaGlassLens(1, 0.5, 1, 1));

        Assert.Equal(new Avalonia.Rect(0, 0, 80, 80), flat.Bounds);
        // The bend reaches one bezel width, 20, and a pixel more.
        Assert.Equal(new Avalonia.Rect(-21, -21, 122, 122), bent.Bounds);
        // The pointer still hits the glass only.
        Assert.False(bent.HitTest(new Avalonia.Point(-10, 40)));
    }

    [Fact]
    public void The_Lens_Samples_Only_Within_The_Area_That_Holds_Backdrop()
    {
        // The snapshot starts at the edge of the glass, as at the edge of a window: the red left of it is no backdrop.
        using var bent = Draw(new SkiaGlassLens(1, 0.5, 1, 1), SplitEdge, new SKRect(100, 0, 240, 100));

        Assert.True(IsBlue(bent.GetPixel(103, 50)), $"{bent.GetPixel(103, 50)}");
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

    [Fact]
    public void The_Rim_Saturation_Strengthens_The_Colors_Toward_The_Edge_Only()
    {
        static void Muted(SKCanvas canvas) => canvas.Clear(new SKColor(150, 110, 130));

        using var flat = Draw(SkiaGlassLens.None, Muted);
        using var saturated = Draw(new SkiaGlassLens(0, 0.5, 1, 2), Muted);

        Assert.True(Spread(saturated.GetPixel(101, 50)) > Spread(flat.GetPixel(101, 50)) + 10);
        Assert.InRange(Spread(saturated.GetPixel(140, 50)) - Spread(flat.GetPixel(140, 50)), -1, 1);
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

    private static int Spread(SKColor color) =>
        Math.Max(color.Red, Math.Max(color.Green, color.Blue)) - Math.Min(color.Red, Math.Min(color.Green, color.Blue));
}
