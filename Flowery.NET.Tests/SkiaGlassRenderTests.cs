using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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

    [AvaloniaFact]
    public void Glasses_That_Start_Redrawing_In_Full_In_Different_Frames_Stop_In_The_Same_Frame()
    {
        // A glass that stopped a frame before another would see that one's last redraw as a change behind it and start
        // again; overlapping glasses would then keep each other redrawing.
        var first = new Border { Width = 50, Height = 20 };
        var second = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = new StackPanel { Children = { first, second } } };
        window.Show();
        try
        {
            var redraws = new[] { new SkiaGlassRedraw(first), new SkiaGlassRedraw(second) };
            redraws[0].BackdropChanged();
            Frame();
            Frame();
            redraws[1].BackdropChanged();

            var lastRedraw = new int[2];
            for (var frame = 1; frame <= 200; frame++)
            {
                var versions = redraws.Select(redraw => redraw.Version).ToArray();
                Frame();
                for (var i = 0; i < 2; i++)
                {
                    if (redraws[i].Version != versions[i])
                        lastRedraw[i] = frame;
                }
            }

            Assert.All(lastRedraw, frame => Assert.InRange(frame, 2, 199));
            Assert.Equal(lastRedraw[0], lastRedraw[1]);
        }
        finally
        {
            window.Close();
        }
    }

    // One frame of the render timer, about 120 per second.
    private static void Frame()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(8);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
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
