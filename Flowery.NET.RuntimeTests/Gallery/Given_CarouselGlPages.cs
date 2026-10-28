using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Effects;
using Flowery.Localization;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Effects;
using Flowery.NET.Gallery.Examples;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>The Carousel GL and Carousel GL Transitions pages of the Gallery, ported from Flowery.WinUI.</summary>
public class Given_CarouselGlPages
{
    [Fact]
    public async Task When_The_Sidebar_Opens_The_Carousel_Gl_Pages_Their_Hosts_Draw_Frames()
    {
        try
        {
            var mainView = await RuntimeTestHelpers.AttachAsync(new MainView());

            var effects = await OpenAsync<CarouselGlExamples>(mainView, "carousel-gl");
            Assert.Equal(GalleryText("Sidebar_CarouselGL"), mainView.FindControl<TextBlock>("CategoryTitle")!.Text);
            var hosts = effects.GetVisualDescendants().OfType<CarouselGlEffectHost>().ToList();
            Assert.Equal(
                new[] { CarouselGlEffectKind.Fade, CarouselGlEffectKind.MaskTransition, CarouselGlEffectKind.FlipPlane, CarouselGlEffectKind.TextFill },
                hosts.Select(static host => host.Effect));
            foreach (var host in hosts)
            {
                var frame = await host.ReadFrameAsync(0.05);
                AssertFrame(host, frame, host.Effect.ToString(), opaque: host.Effect != CarouselGlEffectKind.TextFill);
            }
            await RuntimeTestHelpers.WaitForRenderAsync();
            RuntimeTestHelpers.Render(mainView, "carousel-gl-page").Dispose();

            var transitions = await OpenAsync<CarouselGlTransitionsExamples>(mainView, "carousel-gl-transitions");
            Assert.Equal(GalleryText("Sidebar_CarouselGLTransitions"), mainView.FindControl<TextBlock>("CategoryTitle")!.Text);
            var transitionHost = Assert.Single(transitions.GetVisualDescendants().OfType<CarouselGlTransitionHost>());
            // The slideshow starts with the first transition of the list.
            Assert.Equal("angular", transitionHost.TransitionName);
            AssertFrame(transitionHost, await transitionHost.ReadFrameAsync(0.05), "angular", opaque: true);
            await RuntimeTestHelpers.WaitForRenderAsync();
            RuntimeTestHelpers.Render(mainView, "carousel-gl-transitions-page").Dispose();
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_Another_Page_Is_Shown_The_Hosts_Of_The_Hidden_Page_Pause()
    {
        try
        {
            var mainView = await RuntimeTestHelpers.AttachAsync(new MainView());
            var page = await OpenAsync<CarouselGlExamples>(mainView, "carousel-gl");
            var preview = page.FindControl<CarouselGlEffectHost>("GlPreviewHost")!;
            AssertFrame(preview, await preview.ReadFrameAsync(0.05), "preview", opaque: true);
            await RuntimeTestHelpers.WaitUntilAsync(() => preview.Content is CarouselGlCanvas { State.Running: true }, "the preview runs");

            // The Gallery keeps a page it showed once and only hides it.
            await OpenAsync<CardsExamples>(mainView, "card");
            Assert.False(page.IsVisible);
            await RuntimeTestHelpers.WaitUntilAsync(() => preview.Content is CarouselGlCanvas { State.Running: false },
                "the hidden preview paused");
            var canvas = Assert.IsType<CarouselGlCanvas>(preview.Content);
            var frames = canvas.Frames;
            for (var frame = 0; frame < 3; frame++)
                await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.Equal(frames, canvas.Frames);

            await OpenAsync<CarouselGlExamples>(mainView, "carousel-gl");
            await RuntimeTestHelpers.WaitUntilAsync(() => preview.Content is CarouselGlCanvas { State.Running: true },
                "the shown preview runs again");
            Assert.Same(canvas, preview.Content);
            Assert.Null(preview.LastRenderError);
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_Every_Transition_And_Effect_Of_The_Selectors_Is_Selected_The_Preview_Draws_It()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new CarouselGlExamples());
        try
        {
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            var preview = page.FindControl<CarouselGlEffectHost>("GlPreviewHost")!;
            var transitions = page.FindControl<DaisySelect>("GlTransitionSelect")!;
            var effects = page.FindControl<DaisySelect>("GlEffectSelect")!;
            // Manual mode: no timer changes the selection while the cases run.
            page.FindControl<DaisySelect>("GlModeSelect")!.SelectedIndex = 0;
            Assert.Equal(39, transitions.ItemCount);
            Assert.Equal(13, effects.ItemCount);

            // A transition alone.
            effects.SelectedIndex = 0;
            foreach (var (index, kind) in Kinds(transitions))
            {
                await RuntimeTestHelpers.RunCaseAsync("transition " + kind, async () =>
                {
                    transitions.SelectedIndex = index;
                    Assert.Equal(kind, preview.Effect);
                    Assert.Equal(CarouselGlEffectKind.None, preview.OverlayEffect);
                    if (kind == CarouselGlEffectKind.None)
                    {
                        // Neither a transition nor an effect: the host draws nothing, as in WinUI.
                        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ReadFrameAsync(0.05));
                        Assert.Equal("No GL effect is selected.", error.Message);
                        return;
                    }
                    AssertFrame(preview, await preview.ReadFrameAsync(0.05), kind.ToString(), opaque: true);
                });
            }

            // An effect alone, with the transition None.
            transitions.SelectedIndex = 0;
            foreach (var (index, kind) in Kinds(effects).Where(static entry => entry.Kind != CarouselGlEffectKind.None))
            {
                await RuntimeTestHelpers.RunCaseAsync("effect " + kind, async () =>
                {
                    effects.SelectedIndex = index;
                    Assert.Equal(kind, preview.Effect);
                    Assert.Equal(CarouselGlEffectKind.None, preview.OverlayEffect);
                    AssertFrame(preview, await preview.ReadFrameAsync(0.05), kind.ToString(), opaque: kind != CarouselGlEffectKind.TextFill);
                });
            }

            // A continuous effect over a transition; the text fill replaces the transition.
            transitions.SelectedIndex = 1;
            foreach (var (index, kind) in Kinds(effects).Where(static entry => entry.Kind != CarouselGlEffectKind.None))
            {
                await RuntimeTestHelpers.RunCaseAsync("Fade with " + kind, async () =>
                {
                    effects.SelectedIndex = index;
                    var textFill = kind == CarouselGlEffectKind.TextFill;
                    Assert.Equal(textFill ? kind : CarouselGlEffectKind.Fade, preview.Effect);
                    Assert.Equal(textFill ? CarouselGlEffectKind.None : kind, preview.OverlayEffect);
                    AssertFrame(preview, await preview.ReadFrameAsync(0.05), "Fade with " + kind, opaque: !textFill);
                });
            }
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_Every_Shader_Of_The_Transition_Selector_Is_Selected_The_Preview_Draws_It()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new CarouselGlTransitionsExamples());
        try
        {
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            var preview = page.FindControl<CarouselGlTransitionHost>("GlPreviewHost")!;
            var transitions = page.FindControl<DaisySelect>("GlTransitionSelect")!;
            page.FindControl<DaisySelect>("GlModeSelect")!.SelectedIndex = 0;

            var names = transitions.Items.OfType<ComboBoxItem>().Select(static item => (string)item.Tag!).ToList();
            // The selector offers the whole catalog of the host.
            Assert.Equal(CarouselGlTransitionHost.AvailableShaders.Keys.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
            for (var index = 0; index < names.Count; index++)
            {
                var name = names[index];
                var selected = index;
                await RuntimeTestHelpers.RunCaseAsync(name, async () =>
                {
                    transitions.SelectedIndex = selected;
                    Assert.Equal(name, preview.TransitionName);
                    AssertFrame(preview, await preview.ReadFrameAsync(0.05), name, opaque: true);
                });
            }
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_The_Transitions_Page_Runs_A_Slideshow_It_Moves_To_The_Next_Shader()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new CarouselGlTransitionsExamples());
        try
        {
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            var preview = page.FindControl<CarouselGlTransitionHost>("GlPreviewHost")!;
            // Slideshow is the default mode; Instant keeps the cycle at its shortest: twice the hold of one second
            // and the scaled transition.
            page.FindControl<DaisySelect>("GlDurationSelect")!.SelectedIndex = 0;
            Assert.Equal("angular", preview.TransitionName);

            await RuntimeTestHelpers.WaitUntilAsync(() => preview.TransitionName == "BookFlip", "the slideshow moved on",
                TimeSpan.FromSeconds(6));
            Assert.Equal("BookFlip", (page.FindControl<DaisySelect>("GlTransitionSelect")!.SelectedItem as ComboBoxItem)?.Tag);
            Assert.Null(preview.LastRenderError);
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_The_Transitions_Page_Loads_Again_It_Keeps_Its_Interval()
    {
        var page = await RuntimeTestHelpers.AttachAsync(new CarouselGlTransitionsExamples());
        try
        {
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            var interval = page.FindControl<DaisyNumericUpDown>("GlIntervalBox")!;
            var preview = page.FindControl<CarouselGlTransitionHost>("GlPreviewHost")!;
            interval.Value = 4.5m;
            Assert.Equal(4.5, preview.IntervalSeconds);
            // Regression: the box showed the interval without decimals, 4.5 as 5 (WinUI as well).
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.Equal(4.5m.ToString("0.0", CultureInfo.CurrentCulture), ShownText(page, "GlIntervalBox"));

            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await RuntimeTestHelpers.WaitForIdleAsync();
            RuntimeTestHelpers.AttachToHost(page);
            await RuntimeTestHelpers.EnsureLoadedAsync(page);

            // Regression: every load set the interval to its default again (WinUI).
            Assert.Equal(4.5m, interval.Value);
            Assert.Equal(4.5, preview.IntervalSeconds);
            AssertFrame(preview, await preview.ReadFrameAsync(0.05), "reloaded", opaque: true);
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_The_Page_Loads_It_Restores_The_Saved_Settings_And_Saves_Changes()
    {
        GallerySettings.SaveCarouselGlSettings(new CarouselGlSettings(
            Enabled: true, ModeTag: "Manual", TransitionTag: "Checkerboard", DurationTag: "1.0", EffectTag: "EffectPulse",
            IntervalSeconds: 7m, Infinite: true, SliceCount: 12m, Stagger: false, StaggerMs: 80m, PixelateSize: 30m,
            DissolveDensity: 0.7m, FlipAngle: 120m));
        var page = await RuntimeTestHelpers.AttachAsync(new CarouselGlExamples());
        try
        {
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            var preview = page.FindControl<CarouselGlEffectHost>("GlPreviewHost")!;

            // Regression: loading the XAML raised the selection events of the initial items, which saved the defaults
            // over the settings before the page read them.
            Assert.Equal("Manual", SelectedTag(page, "GlModeSelect"));
            Assert.Equal("Checkerboard", SelectedTag(page, "GlTransitionSelect"));
            Assert.Equal("1.0", SelectedTag(page, "GlDurationSelect"));
            Assert.Equal("EffectPulse", SelectedTag(page, "GlEffectSelect"));
            Assert.Equal(7m, page.FindControl<DaisyNumericUpDown>("GlIntervalBox")!.Value);
            Assert.True(page.FindControl<DaisyToggle>("GlInfiniteToggle")!.IsChecked);
            Assert.Equal(CarouselGlEffectKind.Checkerboard, preview.Effect);
            Assert.Equal(CarouselGlEffectKind.EffectPulse, preview.OverlayEffect);
            Assert.Equal(1.0, preview.TransitionSeconds);
            Assert.Equal(0, preview.IntervalSeconds);
            Assert.Equal(12, preview.TransitionSliceCount);
            Assert.False(preview.TransitionStaggerSlices);
            Assert.Equal(80, preview.TransitionSliceStaggerMs);
            Assert.Equal(30, preview.TransitionPixelateSize);
            Assert.Equal(0.7, preview.TransitionDissolveDensity, 9);
            Assert.Equal(120, preview.TransitionFlipAngle);
            // Regression: the box showed the density without decimals, 0.7 as 1 (WinUI as well).
            Assert.Equal(0.7m.ToString("0.00", CultureInfo.CurrentCulture), ShownText(page, "GlDissolveDensityBox"));
            AssertFrame(preview, await preview.ReadFrameAsync(0.05), "restored", opaque: true);

            page.FindControl<DaisyNumericUpDown>("GlPixelateSizeBox")!.Value = 40m;
            Assert.Equal(40, preview.TransitionPixelateSize);
            Assert.Equal(40m, GallerySettings.LoadCarouselGlSettings()!.PixelateSize);
            Assert.Equal("Checkerboard", GallerySettings.LoadCarouselGlSettings()!.TransitionTag);
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_The_Page_Loads_Again_It_Keeps_Its_Values_And_Runs_Its_Slideshow_Again()
    {
        GallerySettings.SaveCarouselGlSettings(new CarouselGlSettings(
            Enabled: true, ModeTag: "Slideshow", TransitionTag: "Fade", DurationTag: "0.1", EffectTag: "None",
            IntervalSeconds: 1m, Infinite: true, SliceCount: 12m, Stagger: true, StaggerMs: 50m, PixelateSize: 20m,
            DissolveDensity: 0.5m, FlipAngle: 90m));
        var page = new CarouselGlExamples();
        try
        {
            await RuntimeTestHelpers.AttachAsync(page);
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            var effects = page.FindControl<DaisySelect>("GlEffectSelect")!;
            var interval = page.FindControl<DaisyNumericUpDown>("GlIntervalBox")!;
            var slices = page.FindControl<DaisyNumericUpDown>("GlSliceCountBox")!;
            // One cycle lasts the interval and the transition: 1.1 seconds.
            await RuntimeTestHelpers.WaitUntilAsync(() => effects.SelectedIndex == 1, "the slideshow showed the next effect",
                TimeSpan.FromSeconds(4));

            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await RuntimeTestHelpers.WaitForIdleAsync();
            Assert.False(page.IsLoaded);
            var shown = effects.SelectedIndex;
            await Task.Delay(1500);
            Assert.Equal(shown, effects.SelectedIndex);

            RuntimeTestHelpers.AttachToHost(page);
            await RuntimeTestHelpers.EnsureLoadedAsync(page);
            // Regression: every load set the boxes to their defaults and saved them over the settings (WinUI), and
            // without a new selection the timer stopped by Unloaded did not run again.
            Assert.Equal(1m, interval.Value);
            Assert.Equal(12m, slices.Value);
            Assert.Equal(1m, GallerySettings.LoadCarouselGlSettings()!.IntervalSeconds);
            Assert.Equal(12m, GallerySettings.LoadCarouselGlSettings()!.SliceCount);
            await RuntimeTestHelpers.WaitUntilAsync(() => effects.SelectedIndex != shown, "the slideshow ran again",
                TimeSpan.FromSeconds(4));
        }
        finally
        {
            await DetachAsync();
        }
    }

    [Fact]
    public async Task When_An_Effect_Fails_To_Render_The_Page_Shows_The_Error_In_Its_Host()
    {
        CarouselGlGalleryPresets.EnsureRegistered();
        Assert.True(CarouselGlEffectRegistry.TryGet(CarouselGlEffectKind.TextFill, out var original));
        CarouselGlEffectRegistry.Register(new CarouselGlEffectDefinition(CarouselGlEffectKind.TextFill, original!.Mode,
            new Dictionary<string, string> { ["text"] = "avares://Flowery.NET.Gallery/Assets/missing.jpg" }, original.Variant));
        try
        {
            var page = await RuntimeTestHelpers.AttachAsync(new CarouselGlExamples());
            var host = page.FindControl<CarouselGlEffectHost>("GlTextHost")!;
            await RuntimeTestHelpers.WaitUntilAsync(() => host.LastRenderError is not null, "the text fill failed");
            await RuntimeTestHelpers.WaitForIdleAsync();

            var text = Assert.IsType<TextBlock>(host.Content);
            Assert.Equal("GL renderer: " + host.LastRenderError!.Message, text.Text);
            // The other hosts of the page keep drawing.
            var preview = page.FindControl<CarouselGlEffectHost>("GlPreviewHost")!;
            AssertFrame(preview, await preview.ReadFrameAsync(0.05), "preview", opaque: true);
        }
        finally
        {
            CarouselGlEffectRegistry.Register(original!);
            await DetachAsync();
        }
    }

    private static async Task<T> OpenAsync<T>(MainView mainView, string itemId) where T : Control
    {
        var sidebar = mainView.FindControl<FloweryComponentSidebar>("ComponentSidebar")!;
        var category = sidebar.Categories!.First(category => category.Items.Any(item => item.Id == itemId));
        var item = category.Items.First(entry => entry.Id == itemId);
        // The sidebar raises this event when an item is clicked; MainView navigates in its handler.
        sidebar.SelectedItem = item;
        sidebar.RaiseEvent(new SidebarItemSelectedEventArgs(FloweryComponentSidebar.ItemSelectedEvent, item, category));
        await RuntimeTestHelpers.WaitForRenderAsync();
        var page = Assert.IsType<T>(mainView.FindControl<Panel>("MainContentHost")!.Children.Single(static child => child.IsVisible));
        await RuntimeTestHelpers.EnsureLoadedAsync(page);
        return page;
    }

    private static IEnumerable<(int Index, CarouselGlEffectKind Kind)> Kinds(DaisySelect select) =>
        select.Items.OfType<ComboBoxItem>()
            .Select(static (item, index) => (index, Enum.Parse<CarouselGlEffectKind>((string)item.Tag!)))
            .ToList();

    private static string? SelectedTag(Control page, string name) =>
        (page.FindControl<DaisySelect>(name)!.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private static string GalleryText(string key) => FloweryLocalization.GetString(key);

    // The text the number box shows in its template's text box.
    private static string? ShownText(Control page, string name) =>
        page.FindControl<DaisyNumericUpDown>(name)!.GetVisualDescendants().OfType<TextBox>().First().Text;

    private static void AssertFrame(CarouselGlNativeHost host, CarouselGlFrame frame, string name, bool opaque)
    {
        Assert.True(host.LastRenderError is null, name + ": " + host.LastRenderError?.Message);
        Assert.IsType<CarouselGlCanvas>(host.Content);
        Assert.StartsWith("Skia", frame.Renderer);
        Assert.True(frame.Frames > 0 && frame.Width > 0 && frame.Height > 0, name + " drew no frame.");
        Assert.Equal(4, frame.Pixel.Length);
        if (opaque)
            Assert.True(frame.Pixel[3] > 0, name + " drew an empty frame.");
    }

    private static async Task DetachAsync()
    {
        RuntimeTestHelpers.GetHostPanel().Children.Clear();
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }
}
