using Avalonia;
using Flowery.NET.Photos.Editing;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [Fact]
    public void BlurHasTheSameRelativeStrengthInPreviewAndFullResolution()
    {
        using var source = new SKBitmap(3200, 1600);
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(SKColors.Black);
            using var paint = new SKPaint { Color = SKColors.White };
            canvas.DrawRect(1600, 0, 1600, 1600, paint);
        }
        using var preview = source.Resize(new SKImageInfo(800, 400), new SKSamplingOptions(SKFilterMode.Linear));
        var state = EditState.Original with { Sharpness = -100 };
        using var fullResult = EditRenderer.Render(source, state);
        using var previewResult = EditRenderer.Render(preview, state);
        using var scaled = fullResult.Resize(previewResult.Info, new SKSamplingOptions(SKFilterMode.Linear));
        for (var x = 385; x < 415; x++)
            Assert.InRange(Math.Abs(scaled.GetPixel(x, 200).Red - previewResult.GetPixel(x, 200).Red), 0, 8);
        Assert.InRange(previewResult.GetPixel(399, 200).Red, 90, 120);
    }

    [Fact]
    public void EffectsApplyToOutputGeometryAndVignetteFollowsColorMatrix()
    {
        using var source = new SKBitmap(800, 400);
        source.Erase(new SKColor(130, 90, 60, 160));
        var state = EditState.Original with { Rotation = 1, Crop = new Rect(.1, .2, .5, .5), Brightness = 70, Fade = 20, Vignette = 100 };
        using var composed = EditRenderer.Render(source, state);
        using var geometry = EditRenderer.Render(source, state.GeometryOnly());
        using var staged = EditRenderer.Render(geometry, state with { Rotation = 0, Crop = EditState.FullCrop });
        Assert.Equal(composed.Width, staged.Width);
        foreach (var (x, y) in new[] { (5, 5), (100, 200), (180, 380) })
        {
            var actual = composed.GetPixel(x, y);
            var reference = staged.GetPixel(x, y);
            Assert.InRange(Math.Abs(actual.Red - reference.Red), 0, 2);
            Assert.InRange(actual.Alpha, 159, 161);
        }
        Assert.True(composed.GetPixel(5, 5).Red < composed.GetPixel(100, 200).Red / 3);
    }
}
