using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
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

    [Fact]
    public void A_Shader_Background_Draws_At_The_Opacity_It_Is_Given()
    {
        // Skia multiplies a shader by the alpha of its paint, which carries the opacity of the lease.
        using var opaque = Draw(DaisyGlassAnimation.Plasma, 1);
        using var half = new SKBitmap(new SKImageInfo(64, 32, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(half))
            DaisyGlassShaderBackground.Draw(canvas, 64, 32, DaisyGlassAnimation.Plasma, 1, opacity: 0.5);

        var expected = Pixels(opaque);
        var actual = Pixels(half);
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.InRange(actual[i].Alpha, 127, 129);
            Assert.InRange(actual[i].Red, expected[i].Red - 3, expected[i].Red + 3);
            Assert.InRange(actual[i].Green, expected[i].Green - 3, expected[i].Green + 3);
            Assert.InRange(actual[i].Blue, expected[i].Blue - 3, expected[i].Blue + 3);
        }
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

    [AvaloniaFact]
    public void A_New_Measured_Color_Is_Drawn_With_The_Group_Before_It_Stops()
    {
        // A new backdrop color can change the text over the glass. Drawn after the group stopped, that text would
        // repaint part of the glass, which looks like a change behind it, and the glasses would start again.
        var previous = DaisyGlassLiveRenderer.Current;
        FlowerySkia.Register();
        var glass = MeasuringGlass();
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());
            redraw.BackdropChanged();

            // Each render that measures hands over a new color, as the render thread does.
            var measuredInFrame = 0;
            var (measured, lastRedraw) = Run([redraw], 100, frame =>
            {
                if (!redraw.TryStartMeasure())
                    return null;
                measuredInFrame = frame;
                redraw.ReportBackdropColor(Color.FromRgb(200, 10, 10));
                return redraw.Version;
            });

            // The group stopped after the frame that drew the new color, not in the frame of the measurement.
            Assert.Equal([SkiaGlassRedraw.MeasureFrames], measured);
            Assert.Equal(Color.FromRgb(200, 10, 10), glass.BackdropColor);
            Assert.InRange(lastRedraw[0], measuredInFrame + 1, measuredInFrame + 3);
        }
        finally
        {
            window.Close();
            DaisyGlassLiveRenderer.Register(previous);
        }
    }

    [AvaloniaFact]
    public void A_Measured_Color_Holds_The_Group_Until_It_Arrived_And_An_Unchanged_One_Is_Not_Drawn_Again()
    {
        var previous = DaisyGlassLiveRenderer.Current;
        FlowerySkia.Register();
        var glass = MeasuringGlass();
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());
            redraw.ReportBackdropColor(Colors.Teal);
            // Posted, not yet applied: the color is on its way to the glass.
            Assert.True(redraw.HoldsGroup);
            Frame();
            Frame();
            Assert.Equal(Colors.Teal, glass.BackdropColor);
            Assert.Equal(1, redraw.Version);
            Assert.False(redraw.HoldsGroup);

            // The same color again changes nothing, so nothing is drawn for it.
            redraw.ReportBackdropColor(Colors.Teal);
            Assert.True(redraw.HoldsGroup);
            Frame();
            Frame();
            Assert.Equal(1, redraw.Version);
            Assert.False(redraw.HoldsGroup);
        }
        finally
        {
            window.Close();
            DaisyGlassLiveRenderer.Register(previous);
        }
    }

    [AvaloniaFact]
    public void A_Repaint_Of_The_Whole_Glass_Is_Measured_Once_A_Second_Without_Extra_Redraws()
    {
        // A layout pass, a hover color or an animated background repaints all of the glass: no old blur shows, but the
        // backdrop may have changed.
        var (previousClock, previousWake) = (SkiaGlassRedraw.Clock, SkiaGlassRedraw.Wake);
        var now = 10_000L;
        var wakes = new List<(TimeSpan Delay, Action Action)>();
        SkiaGlassRedraw.Clock = () => now;
        SkiaGlassRedraw.Wake = (delay, action) => wakes.Add((delay, action));
        var glass = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());

            // A second after the last measurement, a repaint is measured in its own render: no group, no redraw.
            now += SkiaGlassRedraw.IdleMeasureInterval;
            redraw.BackdropRepainted();
            Assert.True(redraw.TryStartMeasure());
            Frame();
            Assert.Equal(0, redraw.Version);
            Assert.Empty(wakes);

            // Right after it, a repaint waits for a wake-up at the end of the second, without redrawing.
            now += 100;
            redraw.BackdropRepainted();
            Assert.False(redraw.TryStartMeasure());
            Assert.False(redraw.TryStartMeasure());
            Frame();
            var wake = Assert.Single(wakes);
            Assert.Equal(TimeSpan.FromMilliseconds(SkiaGlassRedraw.IdleMeasureInterval - 100), wake.Delay);
            Assert.False(redraw.HoldsGroup);
            var (early, _) = Run([redraw], 10, static _ => null);
            Assert.Empty(early);
            Assert.Equal(0, redraw.Version);

            // At the wake-up, one frame of the group redraws the glass, and its render measures.
            now += SkiaGlassRedraw.IdleMeasureInterval;
            wake.Action();
            var (measured, lastRedraw) = Run([redraw], 10, _ => redraw.TryStartMeasure() ? redraw.Version : null);
            Assert.Equal([1], measured);
            Assert.Equal(1, redraw.Version);
            Assert.Equal(1, lastRedraw[0]);
            Assert.False(redraw.HoldsGroup);
        }
        finally
        {
            window.Close();
            (SkiaGlassRedraw.Clock, SkiaGlassRedraw.Wake) = (previousClock, previousWake);
        }
    }

    [AvaloniaFact]
    public void At_The_Wake_Up_Overlapping_Glasses_Redraw_Together()
    {
        // A glass inside another, such as a badge in a glass card: a lone redraw of the inner one would repaint part of
        // the outer one, which looks like a change behind it.
        using var clock = new FakeClock();
        var outer = new Border { Width = 120, Height = 60 };
        var inner = new Border { Width = 40, Height = 20 };
        Canvas.SetLeft(inner, 20);
        Canvas.SetTop(inner, 20);
        var window = new Window { Width = 200, Height = 100, Content = new Canvas { Children = { outer, inner } } };
        window.Show();
        try
        {
            var outerRedraw = new SkiaGlassRedraw(outer);
            var innerRedraw = new SkiaGlassRedraw(inner) { MeasureBackdrop = true };
            outerRedraw.Register();
            innerRedraw.Register();
            Assert.True(innerRedraw.TryStartMeasure());
            clock.Now += 100;
            innerRedraw.BackdropRepainted();
            Assert.False(innerRedraw.TryStartMeasure());
            Frame();

            clock.Now += SkiaGlassRedraw.IdleMeasureInterval;
            Assert.Single(clock.Wakes).Action();
            var (measured, lastRedraw) = Run([innerRedraw, outerRedraw], 10, _ => innerRedraw.TryStartMeasure() ? innerRedraw.Version : null);

            Assert.Equal([1], measured);
            Assert.Equal((1, 1), (innerRedraw.Version, outerRedraw.Version));
            Assert.Equal(lastRedraw[0], lastRedraw[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wake_Up_That_Comes_Before_The_Interval_Since_A_Later_Measurement_Waits_Again()
    {
        using var clock = new FakeClock();
        var glass = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());
            clock.Now += 100;
            redraw.BackdropRepainted();
            Assert.False(redraw.TryStartMeasure());
            Frame();

            // Measured in another render after the interval, then repainted again: the first wake-up comes too soon.
            clock.Now += SkiaGlassRedraw.IdleMeasureInterval;
            Assert.True(redraw.TryStartMeasure());
            clock.Now += 100;
            redraw.BackdropRepainted();
            Assert.False(redraw.TryStartMeasure());
            Frame();
            clock.Wakes[0].Action();
            Frame();
            Assert.Equal(0, redraw.Version);
            Assert.Equal(2, clock.Wakes.Count);

            clock.Now += SkiaGlassRedraw.IdleMeasureInterval;
            clock.Wakes[1].Action();
            var (measured, _) = Run([redraw], 10, _ => redraw.TryStartMeasure() ? redraw.Version : null);
            Assert.Equal([1], measured);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Glass_Not_Rendered_At_The_Wake_Up_Does_Not_Hold_The_Group()
    {
        using var clock = new FakeClock();
        var glass = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());
            clock.Now += 100;
            redraw.BackdropRepainted();
            Assert.False(redraw.TryStartMeasure());
            Frame();
            clock.Now += SkiaGlassRedraw.IdleMeasureInterval;
            Assert.Single(clock.Wakes).Action();

            // No render measures: the glass is redrawn once, then the group stops.
            Run([redraw], 10, static _ => null);
            Assert.Equal(1, redraw.Version);
            Assert.False(redraw.HoldsGroup);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Repaint_Leaves_A_Due_Change_Paced_Like_A_Change()
    {
        var glass = new Border { Width = 50, Height = 20 };
        var window = new Window { Width = 200, Height = 100, Content = glass };
        window.Show();
        try
        {
            var redraw = new SkiaGlassRedraw(glass) { MeasureBackdrop = true };
            Assert.True(redraw.TryStartMeasure());
            redraw.BackdropChanged();
            redraw.BackdropRepainted();

            var (measured, _) = Run([redraw], 100, _ => redraw.TryStartMeasure() ? redraw.Version : null);
            Assert.Equal([SkiaGlassRedraw.MeasureFrames], measured);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Only_A_Draw_Operation_With_A_New_Version_Is_A_Redraw_Of_The_Glass()
    {
        // A new size, for one, renders a new operation with the same version: that is a repaint, not a redraw.
        var redraw = new SkiaGlassRedraw(new Border());
        Assert.True(redraw.IsNewRedraw(0));
        Assert.False(redraw.IsNewRedraw(0));
        Assert.True(redraw.IsNewRedraw(1));
    }

    // Replaces the clock and the wake-up of the measurement pacing for one test.
    private sealed class FakeClock : IDisposable
    {
        private readonly Func<long> _clock = SkiaGlassRedraw.Clock;
        private readonly Action<TimeSpan, Action> _wake = SkiaGlassRedraw.Wake;

        public FakeClock()
        {
            SkiaGlassRedraw.Clock = () => Now;
            SkiaGlassRedraw.Wake = (delay, action) => Wakes.Add((delay, action));
        }

        public long Now { get; set; } = 10_000;

        public List<(TimeSpan Delay, Action Action)> Wakes { get; } = [];

        public void Dispose() => (SkiaGlassRedraw.Clock, SkiaGlassRedraw.Wake) = (_clock, _wake);
    }

    // A glass that measures its backdrop with the SkiaSharp renderer; a plain DaisyGlass does not blur its backdrop.
    private static DaisyGlass MeasuringGlass() => new()
    {
        Width = 50, Height = 20, EnableBackdropBlur = true, BlurMode = GlassBlurMode.SkiaSharp, MeasureBackdropColor = true
    };

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
