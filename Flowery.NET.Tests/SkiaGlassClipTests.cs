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
        Assert.True(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen, null));
        canvas.Restore();

        canvas.Save();
        canvas.ClipRect(new SKRect(10, 10, 100, 70));
        Assert.False(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen, null));
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
        Assert.False(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen, null));
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
        Assert.True(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen, null));
    }

    [Fact]
    public void A_Rounded_Clip_Of_An_Ancestor_Covers_What_The_First_Render_Saw()
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 100));
        var canvas = surface.Canvas;

        // The ancestor's rounded corner cuts into the glass; the first render sees the glass through it.
        using var ancestor = new SKRoundRect(new SKRect(0, 0, 200, 70), 30);
        canvas.ClipRoundRect(ancestor, antialias: true);
        var seen = Seen(canvas);
        var seenCells = SkiaGlassDrawOperation.ProbeSeenCells(canvas, Glass);
        Assert.NotNull(seenCells);

        Assert.True(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen, seenCells));

        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, 100, 100));
        Assert.False(SkiaGlassDrawOperation.CoversSeen(canvas, Glass, seen, seenCells));
        canvas.Restore();
    }

    private static SKRectI Seen(SKCanvas canvas)
    {
        var seen = SKRectI.Ceiling(canvas.TotalMatrix.MapRect(Glass));
        seen.Intersect(canvas.DeviceClipBounds);
        return seen;
    }
}
