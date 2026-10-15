using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Effects;
using Flowery.Enums;
using Flowery.Helpers;
using Xunit;

namespace Flowery.NET.Tests;

public class MotionRegressionTests
{
    [AvaloniaFact]
    public async Task ReducedMotionCompletesInterpolationBeforeStartAndDuringAnimation()
    {
        using var scope = new Scope(new Border());
        DaisyAccessibility.ReducedMotionOverride = true;
        var frames = new List<double>();
        await AnimationHelper.AnimateAsync(frames.Add, 0, 10, TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { 10d }, frames);
        DaisyAccessibility.ReducedMotionOverride = false;
        var firstFrame = new TaskCompletionSource();
        var animation = AnimationHelper.AnimateAsync(value => { frames.Add(value); firstFrame.TrySetResult(); }, 0, 20, TimeSpan.FromSeconds(1));
        await firstFrame.Task;
        DaisyAccessibility.ReducedMotionOverride = true;
        await animation.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(20, frames[^1]);
        var count = frames.Count;
        await Task.Delay(40);
        Assert.Equal(count, frames.Count);
    }

    [AvaloniaFact]
    public async Task NativeSkeletonAnimationStopsOnPreferenceChangeAndResumes()
    {
        var skeleton = new DaisySkeleton { Width = 150, Height = 30 };
        using var scope = new Scope(skeleton);
        var target = skeleton.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_BackgroundBorder");
        DaisyAccessibility.ReducedMotionOverride = true;
        Assert.False(DaisyAccessibility.GetMotionEnabled(skeleton));
        Assert.False(DaisyAccessibility.GetMotionEnabled(target));
        var still = target.Opacity;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(40);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.Equal(still, target.Opacity);
        DaisyAccessibility.ReducedMotionOverride = false;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(60);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.NotEqual(still, target.Opacity);
        DaisyAccessibility.ReducedMotionOverride = true;
        still = target.Opacity;
        await Task.Delay(40);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Assert.Equal(still, target.Opacity);
    }

    [AvaloniaFact]
    public void SlideshowPolicyPreservesExplicitStopAndDispose()
    {
        using var scope = new Scope(new Border());
        DaisyAccessibility.ReducedMotionOverride = true;
        using var controller = new FlowerySlideshowController(_ => { }, () => 3, () => 0);
        controller.Mode = FlowerySlideshowMode.Slideshow;
        Assert.False(controller.IsRunning);
        DaisyAccessibility.ReducedMotionOverride = false;
        Assert.True(controller.IsRunning);
        DaisyAccessibility.ReducedMotionOverride = true;
        Assert.False(controller.IsRunning);
        controller.Stop();
        DaisyAccessibility.ReducedMotionOverride = false;
        Assert.False(controller.IsRunning);
        controller.Start();
        controller.Dispose();
        DaisyAccessibility.ReducedMotionOverride = true;
        DaisyAccessibility.ReducedMotionOverride = false;
        Assert.False(controller.IsRunning);
    }

    [AvaloniaFact]
    public async Task MotionPolicyRestoresCompleteTextAndDoesNotRestartDetachedOrDisabledEffects()
    {
        var writer = new TextBlock { Text = "Complete text" };
        var wave = new TextBlock { Text = "Wave text" };
        TypewriterBehavior.SetIsEnabled(writer, true);
        WaveTextBehavior.SetIsPerCharacter(wave, true);
        WaveTextBehavior.SetIsEnabled(wave, true);
        var panel = new StackPanel { Children = { writer, wave } };
        using var scope = new Scope(panel);
        DaisyAccessibility.ReducedMotionOverride = true;
        Assert.Equal("Complete text", writer.Text);
        Assert.Equal("Wave text", wave.Text);
        Assert.Empty(wave.Inlines!);
        TypewriterBehavior.SetIsEnabled(writer, false);
        WaveTextBehavior.SetIsEnabled(wave, false);
        scope.Window.Content = null;
        DaisyAccessibility.ReducedMotionOverride = false;
        scope.Window.Content = panel;
        scope.Flush();
        await Task.Delay(40);
        Assert.Equal("Complete text", writer.Text);
        Assert.Equal("Wave text", wave.Text);
    }

    [AvaloniaFact]
    public async Task FunctionalTimerContinuesWhenDecorativeMotionIsReduced()
    {
        var clock = new DaisyClock { Mode = ClockMode.Timer, TimerDuration = TimeSpan.FromSeconds(5) };
        using var scope = new Scope(clock);
        DaisyAccessibility.ReducedMotionOverride = true;
        var tick = new TaskCompletionSource<TimeSpan>();
        clock.TimerTick += (_, remaining) => tick.TrySetResult(remaining);
        clock.Start();
        var remaining = await tick.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(remaining < clock.TimerDuration);
        clock.Pause();
    }

    [AvaloniaFact]
    public void CarouselTransitionCanBeInterruptedWithoutLosingSlidesOrChangingTheirTransforms()
    {
        var transform = new TranslateTransform(7, 9);
        var first = new Border { RenderTransform = transform };
        var second = new Border();
        var carousel = new DaisyCarousel
        {
            Content = new StackPanel { Children = { first, second } },
            SlideTransition = FlowerySlideTransition.Fade, TransitionDuration = 10
        };
        using var scope = new Scope(carousel);
        carousel.SelectedIndex = 1;
        carousel.SelectedIndex = 0;
        DaisyAccessibility.ReducedMotionOverride = true;
        scope.Flush();
        Assert.Equal(2, carousel.ItemCount);
        Assert.Contains(first, carousel.GetVisualDescendants());
        Assert.DoesNotContain(second, carousel.GetVisualDescendants());
        Assert.Same(transform, first.RenderTransform);
        Assert.Equal(1, first.Opacity);
        scope.Window.Content = null;
        scope.Window.Content = carousel;
        scope.Flush();
        Assert.Contains(first, carousel.GetVisualDescendants());
    }

    private sealed class Scope : IDisposable
    {
        private readonly bool? _previous = DaisyAccessibility.ReducedMotionOverride;
        public Window Window { get; }
        public Scope(Control content)
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            Window = new Window { Width = 400, Height = 300, Content = content };
            Window.Show();
            Flush();
        }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { Window.Close(); DaisyAccessibility.ReducedMotionOverride = _previous; }
    }
}
