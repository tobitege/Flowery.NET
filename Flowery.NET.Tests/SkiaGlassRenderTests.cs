using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Enums;
using Flowery.Skia;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// The SkiaSharp drawing of the animated shader backgrounds and the pacing of the backdrop measurement.
/// </summary>
public class SkiaGlassRenderTests
{
    [Theory]
    [InlineData(DaisyGlassAnimation.Plasma)]
    [InlineData(DaisyGlassAnimation.Aurora)]
    [InlineData(DaisyGlassAnimation.LiquidGold)]
    public void A_Shader_Background_Fills_Its_Area_And_Moves_With_Time(DaisyGlassAnimation effect)
    {
        using var early = Draw(effect, 1);
        using var late = Draw(effect, 3);

        // Opaque everywhere, with more than one color, and different two seconds later.
        Assert.All(Pixels(early), pixel => Assert.Equal(255, pixel.Alpha));
        Assert.True(Pixels(early).Distinct().Count() > 10, "The shader drew a flat color.");
        Assert.NotEqual(Pixels(early), Pixels(late));
    }

    [AvaloniaFact]
    public void Without_A_Change_The_Backdrop_Is_Measured_Rarely_And_After_A_Change_Soon()
    {
        var redraw = new SkiaGlassRedraw(new Border()) { MeasureBackdrop = true };
        Assert.True(redraw.TryStartMeasure());

        Thread.Sleep(350);
        Assert.False(redraw.TryStartMeasure());

        redraw.BackdropChanged();
        Assert.True(redraw.TryStartMeasure());
    }

    [AvaloniaFact]
    public void A_Glass_That_Does_Not_Ask_For_It_Is_Never_Measured()
    {
        var redraw = new SkiaGlassRedraw(new Border());
        redraw.BackdropChanged();
        Assert.False(redraw.TryStartMeasure());

        redraw.MeasureBackdrop = true;
        Assert.True(redraw.TryStartMeasure());
    }

    private static SKBitmap Draw(DaisyGlassAnimation effect, float time)
    {
        var bitmap = new SKBitmap(new SKImageInfo(64, 32, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        DaisyGlassShaderBackground.Draw(canvas, 64, 32, effect, time);
        return bitmap;
    }

    private static SKColor[] Pixels(SKBitmap bitmap) => bitmap.Pixels;
}
