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
    public void Without_A_Change_The_Backdrop_Is_Not_Measured_Again_Right_Away()
    {
        var redraw = new SkiaGlassRedraw(new Border()) { MeasureBackdrop = true };
        Assert.True(redraw.TryStartMeasure());
        Assert.False(redraw.TryStartMeasure());
    }

    [AvaloniaFact]
    public void After_A_Change_The_Backdrop_Is_Measured_After_The_Pause_And_The_Glass_Then_Stops()
    {
        var glass = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());
            redraw.BackdropChanged();

            // Each frame redraws the glass in full and then renders it, which asks for a measurement.
            var (measured, lastRedraw) = Run([redraw], 100, _ => redraw.TryStartMeasure() ? redraw.Version : null);

            // Measured after MeasureFrames full redraws, and no redraw after the group stopped.
            Assert.Equal([SkiaGlassRedraw.MeasureFrames], measured);
            Assert.Equal(Math.Max(SkiaGlassRedraw.LiveFrames, SkiaGlassRedraw.MeasureFrames), redraw.Version);
            Assert.InRange(lastRedraw[0], 1, 99);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Measurement_Due_When_The_Change_Ends_Keeps_The_Group_Redrawing_Until_It_Ran()
    {
        // The old code redrew such a glass alone from a timer after the group had stopped, which an overlapping
        // glass saw as a change behind it, so both started redrawing in full again.
        var first = new Border { Width = 50, Height = 20 };
        var second = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = new StackPanel { Children = { first, second } } };
        window.Show();
        try
        {
            var measuring = new SkiaGlassRedraw(first) { MeasureBackdrop = true };
            var changed = new SkiaGlassRedraw(second);
            Assert.True(measuring.TryStartMeasure());
            changed.BackdropChanged();

            // Shortly before the change ends, the first glass asks again for a measurement, which is too soon.
            var measuredInFrame = 0;
            var (measured, lastRedraw) = Run([measuring, changed], 100, frame =>
            {
                if (changed.Version == SkiaGlassRedraw.LiveFrames - 2)
                {
                    measuring.MeasureBackdrop = false;
                    measuring.MeasureBackdrop = true;
                }
                if (!measuring.MeasureBackdrop || changed.Version < SkiaGlassRedraw.LiveFrames - 2 || !measuring.TryStartMeasure())
                    return null;
                measuredInFrame = frame;
                return measuring.Version;
            });

            // The first glass joined the group and was measured after MeasureFrames full redraws; the group ran on
            // until then, past the frames of the change, and both glasses stopped in the frame of the measurement.
            Assert.Equal([SkiaGlassRedraw.MeasureFrames], measured);
            Assert.True(changed.Version > SkiaGlassRedraw.LiveFrames, $"The group stopped after {changed.Version} redraws.");
            Assert.Equal([measuredInFrame, measuredInFrame], lastRedraw);
        }
        finally
        {
            window.Close();
        }
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

            var (_, lastRedraw) = Run(redraws, 100, static _ => null);

            // The second change restarted the count for both: the second glass redrew LiveFrames times, the first also
            // in the frames before, and both stopped in the same frame.
            Assert.Equal(SkiaGlassRedraw.LiveFrames, redraws[1].Version);
            Assert.True(redraws[0].Version > redraws[1].Version);
            Assert.InRange(lastRedraw[0], 1, 99);
            Assert.Equal(lastRedraw[0], lastRedraw[1]);
        }
        finally
        {
            window.Close();
        }
    }

    // Runs frames 1 to count. After each frame, render(frame) stands for the render of the glasses and returns a value
    // when it measured. Returns those values and the last frame in which each glass redrew in full.
    private static (int[] Measured, int[] LastRedraw) Run(SkiaGlassRedraw[] redraws, int count, Func<int, int?> render)
    {
        var measured = new List<int>();
        var lastRedraw = new int[redraws.Length];
        for (var frame = 1; frame <= count; frame++)
        {
            var versions = redraws.Select(static redraw => redraw.Version).ToArray();
            Frame();
            for (var i = 0; i < redraws.Length; i++)
            {
                if (redraws[i].Version != versions[i])
                    lastRedraw[i] = frame;
            }
            if (render(frame) is { } measurement)
                measured.Add(measurement);
        }
        return (measured.ToArray(), lastRedraw);
    }

    // One frame of the render timer. The redraw logic counts frames, not time, so no time has to pass.
    private static void Frame()
    {
        Dispatcher.UIThread.RunJobs();
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
