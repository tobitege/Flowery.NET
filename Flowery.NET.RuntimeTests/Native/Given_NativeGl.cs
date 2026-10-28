using System.Reflection;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Effects;
using Flowery.NET.Gallery.Effects;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native
{
    public class Given_NativeGl
    {
        [Fact]
        public async Task When_AllRegisteredGlEffectsSelected_NativeGpuDrawsTheirFrames()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, TransitionSeconds = 1, IntervalSeconds = 0 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                foreach (var kind in Enum.GetValues<CarouselGlEffectKind>().Where(kind => kind != CarouselGlEffectKind.None))
                {
                    Assert.True(CarouselGlEffectRegistry.TryGet(kind, out var definition), kind.ToString());
                    Assert.NotNull(definition);
                    Assert.True(definition.Assets.Count > 0, kind + " must keep its original image assets.");
                    host.Effect = kind;
                    var frame = await host.ReadFrameAsync(0.05);
                    AssertFrame(frame, kind.ToString());
                    if (kind != CarouselGlEffectKind.TextFill)
                        Assert.True(frame.Pixel[3] > 0, kind + " rendered an empty GPU surface.");
                }

                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
                Assert.False(host.IsRendererInitialized, "Unloading must close the owned GL canvas.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_GlTransitionHostRendersAndReloads_ItRecreatesItsRenderer()
        {
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var source = await host.ReadFrameAsync(0);
                var middle = await host.ReadFrameAsync(0.625);
                var destination = await host.ReadFrameAsync(1.25);
                AssertFrame(source, "source");
                AssertFrame(middle, "middle");
                AssertFrame(destination, "destination");
                Assert.False(source.Pixel.SequenceEqual(destination.Pixel), "The native shader must transition between the original blue and green images.");
                Assert.False(source.Pixel.SequenceEqual(middle.Pixel), "The middle frame must blend both textures.");
                Assert.False(destination.Pixel.SequenceEqual(middle.Pixel), "The middle frame must precede the destination image.");

                var compiled = await host.CompileShadersAsync();
                Assert.Equal(100, compiled.Count);
                Assert.Equal(100, compiled.Distinct(StringComparer.OrdinalIgnoreCase).Count());
                Assert.Equal(CarouselGlTransitionHost.AvailableShaders.Keys.Order(StringComparer.Ordinal), compiled.Order(StringComparer.Ordinal));
                foreach (var name in new[] { "fade", "cube", "GridFlip", "InvertedPageCurl", "Swirl", "pixelize", "displacement", "dissolve", "SimpleZoomOut" })
                {
                    host.TransitionName = name;
                    var frame = await host.ReadFrameAsync(0.05);
                    AssertFrame(frame, name);
                    Assert.True(frame.Pixel[3] > 0, name + " did not render its source image.");
                }

                // Reload with the first transition so the reloaded frame can be compared with the original source frame.
                host.TransitionName = "fade";
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
                Assert.False(host.IsRendererInitialized);
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var reloaded = await host.ReadFrameAsync(0);
                AssertFrame(reloaded, "reloaded");
                Assert.Equal(source.Pixel, reloaded.Pixel);
            }
            finally { RuntimeTestHelpers.GetHostPanel().Children.Clear(); await FlushAsync(host.Dispatcher); }
        }

        [Fact]
        public async Task When_PixelizeTransitionRuns_ItSamplesBlocksInsteadOfCrossfading()
        {
            // pixelize sizes its blocks in global initializers that read progress. Without blocks it draws the
            // same crossfade as fade. The times lie inside the steps of its block size (1.25 s transition).
            double[] elapsedSeconds = [0.51, 0.635, 0.69];
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 0, TransitionSeconds = 1 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var crossfades = new CarouselGlFrame[elapsedSeconds.Length];
                for (var index = 0; index < elapsedSeconds.Length; index++)
                {
                    crossfades[index] = await host.ReadFrameAsync(elapsedSeconds[index]);
                    AssertFrame(crossfades[index], "fade");
                }

                host.TransitionName = "pixelize";
                var details = new string[elapsedSeconds.Length];
                var largestDifference = 0;
                for (var index = 0; index < elapsedSeconds.Length; index++)
                {
                    var pixelized = await host.ReadFrameAsync(elapsedSeconds[index]);
                    AssertFrame(pixelized, "pixelize");
                    var difference = crossfades[index].Pixel.Zip(pixelized.Pixel, (fade, pixelize) => Math.Abs(fade - pixelize)).Max();
                    largestDifference = Math.Max(largestDifference, difference);
                    details[index] = $"{elapsedSeconds[index]} s: fade {string.Join(",", crossfades[index].Pixel)}, pixelize {string.Join(",", pixelized.Pixel)}";
                }

                Assert.True(largestDifference >= 8,
                    "pixelize must sample its blocks during the transition instead of crossfading like fade ("
                    + crossfades[0].Width + "x" + crossfades[0].Height + "; " + string.Join("; ", details) + ").");
            }
            finally { RuntimeTestHelpers.GetHostPanel().Children.Clear(); await FlushAsync(host.Dispatcher); }
        }

        [Fact]
        public async Task When_MotionIsReduced_TheHostsKeepShowingTheirFrame()
        {
            // Regression: pausing hid the canvas, as WinUI hid its overlay canvas; the canvas fills the host, so with
            // reduced motion the host showed nothing.
            CarouselGlGalleryPresets.EnsureRegistered();
            var motion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                DaisyAccessibility.ReducedMotionOverride = true;
                CarouselGlNativeHost[] hosts =
                [
                    new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 0 },
                    new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", TransitionSeconds = 1, IntervalSeconds = 0 }
                ];
                foreach (var host in hosts)
                {
                    var kind = host.GetType().Name;
                    RuntimeTestHelpers.AttachToHost(host);
                    await RuntimeTestHelpers.EnsureLoadedAsync(host);
                    var frame = await host.ReadFrameAsync(0);
                    AssertFrame(frame, kind);
                    await RuntimeTestHelpers.WaitUntilAsync(() => host.Content is CarouselGlCanvas { State: { Running: false } }, kind + " paused its loop");
                    await RuntimeTestHelpers.WaitForRenderAsync();
                    using (var screen = RuntimeTestHelpers.Render(host, "native-gl-reduced-motion-" + kind))
                    {
                        var pixel = screen.GetPixel(80, 50);
                        Assert.True(Close(frame.Pixel, pixel, 3), $"{kind} shows {pixel}, its frame is [{string.Join(", ", frame.Pixel)}].");
                    }
                    RuntimeTestHelpers.GetHostPanel().Children.Clear();
                    await FlushAsync(host.Dispatcher);
                }
            }
            finally
            {
                DaisyAccessibility.ReducedMotionOverride = motion;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(Dispatcher.UIThread);
            }
        }

        [Fact]
        public async Task When_TheHostIsHidden_ItPausesItsLoopUntilItIsShownAgain()
        {
            // Regression: a host in a hidden parent, such as a Gallery page that is kept but hidden, drew on every
            // frame; the WinUI host paused with the hidden document of its WebView2.
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 0 };
            var parent = new Border { Child = host };
            try
            {
                RuntimeTestHelpers.AttachToHost(parent);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(0), "running");
                await RuntimeTestHelpers.WaitUntilAsync(() => host.Content is CarouselGlCanvas { State.Running: true }, "the loop runs");

                parent.IsVisible = false;
                await RuntimeTestHelpers.WaitUntilAsync(() => host.Content is CarouselGlCanvas { State.Running: false }, "the hidden loop paused");
                var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
                var frames = canvas.Frames;
                await RuntimeTestHelpers.WaitForRenderAsync();
                await RuntimeTestHelpers.WaitForRenderAsync();
                Assert.Equal(frames, canvas.Frames);

                parent.IsVisible = true;
                await RuntimeTestHelpers.WaitUntilAsync(() => host.Content is CarouselGlCanvas { State.Running: true }, "the shown loop runs again");
                await RuntimeTestHelpers.WaitUntilAsync(() => canvas.Frames > frames, "the shown loop draws");
                Assert.Same(canvas, host.Content);
                Assert.Null(host.LastRenderError);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_HostLoadsAndUnloads_ItStartsAndClosesItsRenderer()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 100 };
            Assert.False(host.IsRendererInitialized);
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var frame = await host.ReadFrameAsync(0);
                AssertFrame(frame, "start-up");
                Assert.True(host.IsRendererInitialized);
                var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
                var scaling = TopLevel.GetTopLevel(host)!.RenderScaling;
                Assert.Equal(((int)Math.Floor(160 * scaling), (int)Math.Floor(100 * scaling)), (frame.Width, frame.Height));

                // The loop holds the first image for the interval; the screen shows the frame the canvas read back.
                await RuntimeTestHelpers.WaitForRenderAsync();
                Assert.StartsWith("Skia ", canvas.Renderer);
                using (var screen = RuntimeTestHelpers.Render(host, "native-gl-startup"))
                {
                    var pixel = screen.GetPixel(80, 50);
                    var held = await host.ReadFrameAsync();
                    Assert.True(Close(held.Pixel, pixel, 3), $"The screen shows {pixel}, the frame [{string.Join(", ", held.Pixel)}].");
                }

                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
                Assert.False(host.IsRendererInitialized);
                Assert.Null(host.Content);
                Assert.Null(canvas.State);
                Assert.Equal(new byte[4], canvas.ReadFrame().Pixel);
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.ReadFrameAsync());

                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var reloaded = await host.ReadFrameAsync(0);
                AssertFrame(reloaded, "reloaded");
                Assert.True(host.IsRendererInitialized);
                Assert.NotSame(canvas, host.Content);
                Assert.Equal(frame.Pixel, reloaded.Pixel);

                host.Effect = CarouselGlEffectKind.None;
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.ReadFrameAsync());
                Assert.Equal("No GL effect is selected.", error.Message);
                Assert.False(host.IsRendererInitialized);
                Assert.Null(host.Content);
                Assert.Null(host.LastRenderError);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_AShaderDoesNotCompile_TheErrorStaysVisible()
        {
            Assert.Null(SKRuntimeEffect.CreateShader(InvalidShaderEngine.Source, out var expected));
            Assert.False(string.IsNullOrEmpty(expected));
            var host = new InvalidShaderHost { Width = 160, Height = 100 };
            var failures = new List<Exception>();
            host.RenderFailed += (_, failure) => failures.Add(failure);
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                await RuntimeTestHelpers.WaitUntilAsync(() => host.LastRenderError is not null, "the renderer failed");
                await RuntimeTestHelpers.WaitForIdleAsync();

                Assert.Equal(expected, host.LastRenderError!.Message);
                // Regression: the failed request reported the error of the session a second time.
                Assert.Equal(expected, Assert.Single(failures).Message);
                Assert.False(host.IsRendererInitialized);
                var text = Assert.IsType<TextBlock>(host.Content);
                Assert.Equal("GL renderer: " + expected, text.Text);
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.ReadFrameAsync());
                Assert.Equal("The native GL renderer failed.", error.Message);
                Assert.Same(host.LastRenderError, error.InnerException);

                // Nothing replaces the message while the host stays loaded.
                await RuntimeTestHelpers.WaitForRenderAsync();
                Assert.Same(text, host.Content);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_AnAssetFailsToLoadWhileTheHostWaitsForAFrame_TheErrorIsReportedOnce()
        {
            // Regression: the canvas reported the failure to the session, and the rejected frame request reported it
            // a second time.
            var host = new InvalidAssetHost { Width = 160, Height = 100 };
            var failures = new List<Exception>();
            host.RenderFailed += (_, failure) => failures.Add(failure);
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                await RuntimeTestHelpers.WaitUntilAsync(() => host.LastRenderError is not null, "the renderer failed");
                await RuntimeTestHelpers.WaitForIdleAsync();
                await RuntimeTestHelpers.WaitForRenderAsync();

                Assert.Equal(InvalidAssetEngine.Message, Assert.Single(failures).Message);
                var text = Assert.IsType<TextBlock>(host.Content);
                Assert.Equal("GL renderer: " + InvalidAssetEngine.Message, text.Text);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_APausedHostChangesItsTiming_ItDrawsTheFrameWithoutWaiting()
        {
            // Regression: a paused loop drew no frame for the configure request, which timed out after 30 s and
            // failed the renderer.
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 0 };
            var motion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(0), "running");
                DaisyAccessibility.ReducedMotionOverride = true;
                await RuntimeTestHelpers.WaitUntilAsync(() => host.Content is CarouselGlCanvas { State: { Running: false } }, "the loop paused");

                host.IntervalSeconds = 5;
                var frame = await host.ReadFrameAsync().WaitAsync(TimeSpan.FromSeconds(5));
                AssertFrame(frame, "paused");
                Assert.Null(host.LastRenderError);
                Assert.True(host.IsRendererInitialized);
            }
            finally
            {
                DaisyAccessibility.ReducedMotionOverride = motion;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_APausedTransitionHostChangesItsTransition_ItShowsTheNewProgram()
        {
            // Regression: the engine applies a new transition after its definition loaded, which in JS follows the
            // configure request; the paused frame then kept the old program and clock, and nothing redrew it.
            var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", TransitionSeconds = 1, IntervalSeconds = 50 };
            var motion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                DaisyAccessibility.ReducedMotionOverride = false;
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var first = await host.ReadFrameAsync(0);
                var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
                var state = Assert.IsType<FloweryCarouselGLTransitions.State>(canvas.State);
                var second = await host.ReadFrameAsync(state.intervalSec + state.transitionSec);
                Assert.True(MaxDifference(first.Pixel, second.Pixel) >= 20, "The fade must go between two different images.");

                DaisyAccessibility.ReducedMotionOverride = true;
                await RuntimeTestHelpers.WaitUntilAsync(() => !state.running, "the loop paused");
                // The paused fade is 45 % of the way when the next transition replaces it; the new one starts its hold.
                state.startTime = CarouselGlCanvas.Now - (state.intervalSec + 0.45 * state.transitionSec) * 1000;
                host.TransitionName = "cube";
                var cube = CarouselGlTransitionHost.AvailableShaders["cube"];
                await RuntimeTestHelpers.WaitUntilAsync(() => state.definition?.Source == cube, "the cube transition applied");
                await RuntimeTestHelpers.WaitForIdleAsync();
                Assert.Same(state, canvas.State);
                Assert.False(state.running);

                using var screen = RuntimeTestHelpers.Render(host, "native-gl-paused-transition-change");
                var pixel = screen.GetPixel(80, 50);
                // A paused sample draws the cube at its hold, the frame the screen must already show.
                var expected = await host.ReadFrameAsync();
                Assert.True(Close(expected.Pixel, pixel, 3), $"The screen shows {pixel}, the new transition [{string.Join(", ", expected.Pixel)}].");
                Assert.Null(host.LastRenderError);
            }
            finally
            {
                DaisyAccessibility.ReducedMotionOverride = motion;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_APausedHostIsResized_ItShowsAFrameOfTheNewSize()
        {
            // Regression: a paused loop kept the frame drawn for the old size and stretched it over the new one; the
            // text mask of the old size stretched its letters.
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.TextFill };
            var motion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                DaisyAccessibility.ReducedMotionOverride = false;
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(0), "running");
                var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
                DaisyAccessibility.ReducedMotionOverride = true;
                await RuntimeTestHelpers.WaitUntilAsync(() => canvas.State is { Running: false }, "the loop paused");

                host.Width = 400;
                await RuntimeTestHelpers.WaitUntilAsync(() => canvas.Bounds.Width == 400, "the canvas has its new size");
                await RuntimeTestHelpers.WaitForIdleAsync();
                using var still = RuntimeTestHelpers.Render(host, "native-gl-paused-resize");
                Assert.Equal(400, still.Width);

                // A paused sample draws a fresh frame of the new size; the letters of the mask do not move.
                var fresh = await host.ReadFrameAsync();
                var scaling = TopLevel.GetTopLevel(host)!.RenderScaling;
                Assert.Equal((int)Math.Floor(400 * scaling), fresh.Width);
                await RuntimeTestHelpers.WaitForRenderAsync();
                using var redrawn = RuntimeTestHelpers.Render(host, "native-gl-paused-resize-redrawn");
                var differing = 0;
                for (var y = 10; y < 90; y += 4)
                {
                    for (var x = 0; x < 400; x++)
                    {
                        if (Math.Abs(still.GetPixel(x, y).Alpha - redrawn.GetPixel(x, y).Alpha) > 16)
                            differing++;
                    }
                }
                Assert.True(differing == 0, $"{differing} pixels of the paused frame differ from a frame of the new size.");
                Assert.Null(host.LastRenderError);
            }
            finally
            {
                DaisyAccessibility.ReducedMotionOverride = motion;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_TheHostPausesWhileARequestWaitsForAFrame_TheRequestGetsItsFrame()
        {
            // Regression: a request waited for the next frame of the running loop; the pause cancelled that frame, and
            // the request timed out after 30 s and failed the renderer.
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 0 };
            var motion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                DaisyAccessibility.ReducedMotionOverride = false;
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                AssertFrame(await host.ReadFrameAsync(0), "running");
                var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
                await RuntimeTestHelpers.WaitUntilAsync(() => canvas.State is { Running: true }, "the loop runs");

                var pending = host.ReadFrameAsync();
                Assert.False(pending.IsCompleted);
                DaisyAccessibility.ReducedMotionOverride = true;
                var frame = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                AssertFrame(frame, "paused while waiting");
                Assert.False(canvas.State?.Running);
                Assert.Null(host.LastRenderError);
                Assert.True(host.IsRendererInitialized);
            }
            finally
            {
                DaisyAccessibility.ReducedMotionOverride = motion;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_TheHostReloadsWhileAnUpdateWaitsOnItsClosedRenderer_TheUpdateStartsTheNewRenderer()
        {
            // Regression: an unload closed the session that a running update waited on, and the reload found that
            // update still running, so it started none. The update then gave up on the closed session, and the reloaded
            // host had no renderer ("No GL effect is selected."). The full runtime suite hit this in
            // When_GlTransitionHostRendersAndReloads_ItRecreatesItsRenderer when the cancellation came back late.
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 0 };
            var motion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                // A running loop: the update of the next change waits for its next frame.
                DaisyAccessibility.ReducedMotionOverride = false;
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var source = await host.ReadFrameAsync(0);
                AssertFrame(source, "source");
                var closed = Assert.IsType<CarouselGlCanvas>(host.Content);

                host.TransitionSeconds = 2;
                var panel = RuntimeTestHelpers.GetHostPanel();
                panel.Children.Clear();
                panel.Children.Add(host);
                // The reload is loaded before the update learns that its session closed: the cancellation reaches the
                // update through the thread pool and a post of the default priority, below the Loaded jobs run here.
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
                Assert.True(host.IsLoaded);

                var reloaded = await host.ReadFrameAsync(0).WaitAsync(TimeSpan.FromSeconds(10));
                AssertFrame(reloaded, "reloaded");
                Assert.True(host.IsRendererInitialized);
                Assert.NotSame(closed, host.Content);
                Assert.Equal(source.Pixel, reloaded.Pixel);
                Assert.Null(host.LastRenderError);
            }
            finally
            {
                DaisyAccessibility.ReducedMotionOverride = motion;
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_FramesAreReadAtSeveralTimes_TheyFollowTheTransition()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Fade, TransitionSeconds = 1, IntervalSeconds = 0 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var source = await host.ReadFrameAsync(0);
                var middle = await host.ReadFrameAsync(0.5);
                var destination = await host.ReadFrameAsync(1);
                var returned = await host.ReadFrameAsync(2);
                AssertFrame(source, "source");
                AssertFrame(middle, "middle");
                AssertFrame(destination, "destination");
                AssertFrame(returned, "returned");
                Assert.True(middle.Frames > source.Frames && destination.Frames > middle.Frames, "Every sample must draw a frame.");
                Assert.False(source.Pixel.SequenceEqual(destination.Pixel), "The fade must go from the blue to the green wallpaper.");
                // The fade mixes linearly, and after a cycle of two transitions it shows the first image again.
                for (var channel = 0; channel < 3; channel++)
                    Assert.InRange(middle.Pixel[channel], (source.Pixel[channel] + destination.Pixel[channel]) / 2 - 2, (source.Pixel[channel] + destination.Pixel[channel]) / 2 + 2);
                Assert.Equal(source.Pixel, returned.Pixel);

                // The center of the canvas shows the center of the images.
                AssertImageCenter("avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg", source.Pixel);
                AssertImageCenter("avares://Flowery.NET.Gallery/Assets/wallpaper_green.jpg", destination.Pixel);

                // Without a time, the running loop answers with its next frame.
                var live = await host.ReadFrameAsync();
                AssertFrame(live, "live");
                Assert.True(live.Frames > returned.Frames);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public async Task When_EffectShadersCompile_EveryVariantDrawsAFrame()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            var host = new CarouselGlEffectHost { Width = 160, Height = 100, Effect = CarouselGlEffectKind.Checkerboard, TransitionSeconds = 1 };
            try
            {
                RuntimeTestHelpers.AttachToHost(host);
                await RuntimeTestHelpers.EnsureLoadedAsync(host);
                var compiled = await host.CompileShadersAsync();
                Assert.Equal(49, compiled.Count);
                Assert.Equal(compiled.Count, compiled.Distinct().Count());
                Assert.Contains("flip:cube-right", compiled);
                Assert.Contains("text:text-fill", compiled);
                Assert.True(host.IsRendererInitialized);
                Assert.Null(host.LastRenderError);
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Clear();
                await FlushAsync(host.Dispatcher);
            }
        }

        [Fact]
        public void When_TheTextMaskIsDrawn_ItIsTransparentOutsideTheLetters()
        {
            var engine = typeof(CarouselGlEffectHost).Assembly.GetType("Flowery.Effects.FloweryCarouselGL", throwOnError: true)!;
            var createTextMask = engine.GetMethod("CreateTextMask", BindingFlags.NonPublic | BindingFlags.Static)!;
            // As wide as the letters at 45 pixels need; the mask of a host scales the font with its height only.
            using var mask = (SKImage)createTextMask.Invoke(null, [400, 100, 45d])!;
            using var pixels = SKBitmap.FromImage(mask);
            using (var png = mask.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(RuntimeTestHelpers.GetArtifactPath("native-gl-text-mask.png"), png.ToArray());
            int left = int.MaxValue, right = -1, top = int.MaxValue, bottom = -1, opaque = 0;
            for (var y = 0; y < 100; y++)
            {
                for (var x = 0; x < 400; x++)
                {
                    var pixel = pixels.GetPixel(x, y);
                    if (pixel.Alpha == 0)
                        continue;
                    if (pixel.Alpha >= 128)
                        Assert.True(pixel.Red >= 250 && pixel.Green >= 250 && pixel.Blue >= 250, $"The letters must be white, not {pixel}.");
                    if (pixel.Alpha == 255)
                        opaque++;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            // The letters fill part of the mask around its center; everything around them is clear.
            Assert.True(opaque > 500, $"Only {opaque} pixels of the letters are opaque.");
            Assert.True(left > 0 && right < 399 && top > 0 && bottom < 99, $"The letters reach the edge: {left}, {top}, {right}, {bottom}.");
            Assert.InRange((left + right) / 2.0, 190, 210);
            Assert.InRange((top + bottom) / 2.0, 40, 60);
        }

        private static void AssertImageCenter(string asset, byte[] pixel)
        {
            using var stream = AssetLoader.Open(new Uri(asset));
            using var image = SKBitmap.Decode(stream);
            int minRed = 255, minGreen = 255, minBlue = 255, maxRed = 0, maxGreen = 0, maxBlue = 0;
            for (var y = image.Height / 2 - 2; y <= image.Height / 2 + 2; y++)
            {
                for (var x = image.Width / 2 - 2; x <= image.Width / 2 + 2; x++)
                {
                    var color = image.GetPixel(x, y);
                    minRed = Math.Min(minRed, color.Red);
                    minGreen = Math.Min(minGreen, color.Green);
                    minBlue = Math.Min(minBlue, color.Blue);
                    maxRed = Math.Max(maxRed, color.Red);
                    maxGreen = Math.Max(maxGreen, color.Green);
                    maxBlue = Math.Max(maxBlue, color.Blue);
                }
            }
            Assert.InRange(pixel[0], minRed - 3, maxRed + 3);
            Assert.InRange(pixel[1], minGreen - 3, maxGreen + 3);
            Assert.InRange(pixel[2], minBlue - 3, maxBlue + 3);
            Assert.Equal(255, pixel[3]);
        }

        private static int MaxDifference(byte[] first, byte[] second) =>
            Math.Max(Math.Abs(first[0] - second[0]), Math.Max(Math.Abs(first[1] - second[1]), Math.Abs(first[2] - second[2])));

        private static bool Close(byte[] frame, SKColor screen, int tolerance) =>
            Math.Abs(frame[0] - screen.Red) <= tolerance
            && Math.Abs(frame[1] - screen.Green) <= tolerance
            && Math.Abs(frame[2] - screen.Blue) <= tolerance;

        private static void AssertFrame(CarouselGlFrame frame, string context)
        {
            Assert.True(frame.Frames > 0, context + " must execute a GPU draw.");
            Assert.True(frame.Width > 0 && frame.Height > 0, context + " must resize to native Avalonia bounds.");
            Assert.True(frame.Pixel.Length == 4, context + " must read back an actual RGBA pixel.");
            Assert.False(string.IsNullOrWhiteSpace(frame.Renderer), context + " must report the real GPU renderer.");
        }

        private static Task FlushAsync(Dispatcher dispatcher) =>
            dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background).GetTask();

        // A host whose engine fails to compile its program, as an engine whose SkSL has an error.
        private sealed class InvalidShaderHost : CarouselGlNativeHost
        {
            protected override CarouselGlEngine CreateEngine() => new InvalidShaderEngine();

            protected override CarouselGlConfiguration? CreateConfiguration() =>
                new("effects", "mask", "checkerboard", null, new Dictionary<string, string>(), 0, 1);
        }

        private sealed class InvalidShaderEngine : CarouselGlEngine
        {
            public const string Source = "half4 main(float2 p) { return undefinedColor; }";

            // The init of the engine: a program that does not compile fails the canvas.
            protected override void AttachOverlay(CarouselGlCanvas canvas, CarouselGlConfiguration configuration)
            {
                try { CreateProgram(Source); }
                catch (Exception error) { canvas.Fail(error); }
            }

            protected override void SetOverlayTiming(double intervalSec) { }

            protected override void SetOverlayTransitionDuration(double transitionSec) { }

            protected override void SetOverlayActive(bool isActive) { }

            protected override void Resize(CarouselGlCanvas canvas) { }

            protected override Task<IReadOnlyList<string>> CompileAsync() => Task.FromResult<IReadOnlyList<string>>([]);

            protected override void DetachAllOverlays() { }
        }

        // A host whose engine fails after its configure request started waiting for a frame, as an image that does
        // not decode fails the engine once it has loaded.
        private sealed class InvalidAssetHost : CarouselGlNativeHost
        {
            protected override CarouselGlEngine CreateEngine() => new InvalidAssetEngine();

            protected override CarouselGlConfiguration? CreateConfiguration() =>
                new("effects", "mask", "checkerboard", null, new Dictionary<string, string>(), 0, 1);
        }

        private sealed class InvalidAssetEngine : CarouselGlEngine
        {
            public const string Message = "Not supported (asset load failed)";

            protected override void AttachOverlay(CarouselGlCanvas canvas, CarouselGlConfiguration configuration) =>
                _ = LoadAsync(canvas);

            private static async Task LoadAsync(CarouselGlCanvas canvas)
            {
                // An image element loads after the current work, so the frame request is waiting when it fails.
                await Task.Yield();
                try { using var image = await DecodeImageAsync("data:image/png;base64,AAAA"); }
                catch (Exception) { canvas.DrawFallback(Message); }
            }

            protected override void SetOverlayTiming(double intervalSec) { }

            protected override void SetOverlayTransitionDuration(double transitionSec) { }

            protected override void SetOverlayActive(bool isActive) { }

            protected override void Resize(CarouselGlCanvas canvas) { }

            protected override Task<IReadOnlyList<string>> CompileAsync() => Task.FromResult<IReadOnlyList<string>>([]);

            protected override void DetachAllOverlays() { }
        }
    }
}
