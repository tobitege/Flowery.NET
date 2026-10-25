using Flowery.Skia;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// A live glass redraws in full after a render that redrew only part of it. These tests check how a render tells,
/// from the clip of the canvas, whether it redraws all of the glass.
/// </summary>
public class SkiaGlassClipTests
{
    private static readonly SKRect Glass = new(20, 20, 180, 60);

    [Fact]
    public void A_Rectangular_Clip_Over_The_Glass_Covers_It_And_One_Over_Half_Does_Not()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;
        var seen = Seen(canvas);

        canvas.Save();
        canvas.ClipRect(new SKRect(10, 10, 190, 70));
        Assert.True(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen));
        canvas.Restore();

        canvas.Save();
        canvas.ClipRect(new SKRect(10, 10, 100, 70));
        Assert.False(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen));
        canvas.Restore();
    }

    [Fact]
    public void A_Region_Whose_Bounds_Span_The_Glass_With_A_Gap_Does_Not_Cover_It()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;
        var seen = Seen(canvas);

        // Two redrawn areas left and right of the glass center; their bounds hold the whole glass.
        using var region = new SKRegion();
        region.Op(new SKRectI(10, 10, 80, 70), SKRegionOperation.Union);
        region.Op(new SKRectI(120, 10, 190, 70), SKRegionOperation.Union);
        canvas.ClipRegion(region);

        Assert.True(canvas.DeviceClipBounds.Contains(seen));
        Assert.False(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen));
    }

    [Fact]
    public void A_Region_Of_Adjoining_Areas_Over_The_Glass_Covers_It()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;
        var seen = Seen(canvas);

        using var region = new SKRegion();
        region.Op(new SKRectI(10, 10, 100, 70), SKRegionOperation.Union);
        region.Op(new SKRectI(100, 10, 190, 70), SKRegionOperation.Union);
        region.Op(new SKRectI(150, 80, 190, 95), SKRegionOperation.Union);
        canvas.ClipRegion(region);

        Assert.False(canvas.IsClipRect);
        Assert.True(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen));
    }

    [Fact]
    public void A_Rounded_Clip_Of_An_Ancestor_Is_Not_Taken_For_A_Partial_Render()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // The ancestor's rounded corners cut the corners off the glass; the clip is no rectangle any more.
        using var ancestor = new SKRoundRect(Glass, 20);
        canvas.ClipRoundRect(ancestor, antialias: true);
        var seen = Seen(canvas);
        Assert.False(canvas.IsClipRect);

        Assert.True(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen));

        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, 100, 100));
        Assert.False(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen));
        canvas.Restore();
    }

    [Fact]
    public void A_Large_Glass_Is_Checked_In_At_Most_1024_Cells()
    {
        var glass = new SKRect(0, 0, 3800, 2100);

        var cells = SkiaGlassDrawOperation.Cells(glass);

        Assert.InRange(cells.Length, 1, 1024);
        Assert.Equal(glass, cells.Aggregate(SKRect.Empty, (all, cell) => all.IsEmpty ? cell : SKRect.Union(all, cell)));
    }

    private static SKRectI Seen(SKCanvas canvas)
    {
        var seen = SKRectI.Ceiling(canvas.TotalMatrix.MapRect(Glass));
        seen.Intersect(canvas.DeviceClipBounds);
        return seen;
    }
}
