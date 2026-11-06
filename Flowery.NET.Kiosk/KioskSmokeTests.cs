using System.Runtime.InteropServices;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Flowery.Controls.ColorPicker;
using Flowery.Localization;
using Flowery.NET.Kiosk.Slides;

namespace Flowery.NET.Kiosk;

/// <summary>
/// The smoke run of <c>--smoke-test</c>: it drives the kiosk in its real desktop window, checks every slide, the
/// manual navigation and one automatic cycle, and writes the results to the report.
/// </summary>
internal static class KioskSmokeTests
{
    private const double ResourceValueTolerance = 0.0001;

    internal static async Task RunAsync(MainWindow window, KioskSmokeReport report)
    {
        var page = window.KioskPage;
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = lifetime.Token;
        await CheckAsync(report, "DesktopWindowAndSlideshow", () =>
        {
            Require(page.IsLoaded && TopLevel.GetTopLevel(page) is not null && page.Bounds.Width > 0 && page.Bounds.Height > 0,
                "The opened desktop window has no loaded, measured kiosk page.");
            Require(page.SlideCount == 6 && page.CurrentIndex == 0, "The initial six-slide sequence was not loaded.");
            Require(!page.IsAutoRunning && page.Interval == 8, "Smoke mode must start manually with the eight-second interval.");
            Require(FloweryLocalization.CurrentCulture.Name == "en-US", "Smoke mode must run in English.");
            return Task.CompletedTask;
        });
        await CheckAsync(report, "ThemeResourcesAndFonts", () =>
        {
            var application = Application.Current ?? throw new InvalidOperationException("The application is missing.");
            Require(FindResource(application, "DaisyBase100Brush") is ISolidColorBrush, "The base theme brush is missing.");
            Require(FindResource(application, "DaisyPrimaryBrush") is ISolidColorBrush, "The primary theme brush is missing.");
            Require(FindResource(application, "KioskSlideTitleFontSize") is double size && size == 43, "The overlay title resource is missing.");
            foreach (var asset in new[] { "NotoSans-Regular.ttf", "NotoSansArabic-Regular.ttf", "NotoSansJP-Regular.otf", "NotoSansKR-Regular.otf", "NotoSansSC-Regular.otf", "OFL.txt" })
                RequireFile(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", asset));
            var family = FindResource(application, "NotoSansFamily") as FontFamily
                ?? throw new InvalidOperationException("The Noto Sans font family resource is missing.");
            Require(window.FontFamily == family, "The window does not use the Noto Sans font family.");
            Require(FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyphs) && glyphs.FamilyName == "Noto Sans",
                "The embedded Noto Sans font does not load.");
            return Task.CompletedTask;
        });
        await CheckAsync(report, "AppIconAndSplashScreen", () =>
        {
            RequireFile(Path.Combine(AppContext.BaseDirectory, "Assets", "Kiosk", "app_icon.svg"));
            RequireFile(Path.Combine(AppContext.BaseDirectory, "Assets", "Kiosk", "splash_screen.svg"));
            return Task.CompletedTask;
        });
        await CheckAsync(report, "WindowIcon", () =>
        {
            Require(window.Icon is not null, "The window shows no icon.");
            return Task.CompletedTask;
        });

        DataSlide? dataSlide = null;
        ScalingSlide? scalingSlide = null;
        PatternsSlide? patternsSlide = null;
        var previousScale = ScaleState.Capture();
        for (var index = 0; index < page.SlideCount; index++)
        {
            var currentIndex = index;
            var slideInfo = page.SlideAt(index);
            await CheckAsync(report, $"Slide{index + 1}.NavigationAndTheme", async () =>
            {
                await page.NavigateToAsync(currentIndex, token);
                Require(page.CurrentIndex == currentIndex && page.CurrentSlide is { IsLoaded: true }, "The requested slide did not load.");
                Require(DaisyThemeManager.CurrentThemeName == slideInfo.Theme, "The requested slide theme was not applied.");
                Require(page.CurrentTitle == slideInfo.Title.ToUpperInvariant() && page.CurrentKeywords == slideInfo.Keywords,
                    "The slide title or keywords do not match the visible content.");
            });
            await CheckAsync(report, $"Slide{index + 1}.Function", async () =>
            {
                switch (currentIndex)
                {
                    case 0:
                        await CheckLoginAsync(RequireSlide<LoginSlide>(page), token);
                        break;
                    case 1:
                        dataSlide = RequireSlide<DataSlide>(page);
                        await CheckDataAsync(dataSlide, token);
                        break;
                    case 2:
                        CheckFeedback(RequireSlide<FeedbackSlide>(page));
                        break;
                    case 3:
                        CheckShowcase(RequireSlide<ShowcaseSlide>(page));
                        break;
                    case 4:
                        scalingSlide = RequireSlide<ScalingSlide>(page);
                        var previousMaxWidth = scalingSlide.MaxWidth;
                        var previousMaxHeight = scalingSlide.MaxHeight;
                        try
                        {
                            // Exercise the compact CI viewport even on larger local displays.
                            scalingSlide.MaxWidth = Math.Min(previousMaxWidth, 752);
                            scalingSlide.MaxHeight = Math.Min(previousMaxHeight, 480);
                            await CheckScalingAsync(scalingSlide, page, report, token);
                        }
                        finally
                        {
                            scalingSlide.MaxWidth = previousMaxWidth;
                            scalingSlide.MaxHeight = previousMaxHeight;
                        }
                        break;
                    case 5:
                        patternsSlide = RequireSlide<PatternsSlide>(page);
                        await CheckPatternsAsync(patternsSlide, token);
                        break;
                }
            });
            await CheckAsync(report, $"Slide{index + 1}.RenderedPixels", async () =>
            {
                Require(page.CurrentIndex == currentIndex, "A different slide is active during render verification.");
                var slide = page.CurrentSlide ?? throw new InvalidOperationException("The active slide is missing.");
                await WaitForFrameAsync(slide, token);
                CheckRenderedPixels(slide);
                if (slide is FeedbackSlide)
                    SaveScreenshot(page, Path.Combine(report.Folder, "communication.png"));
                else if (slide is PatternsSlide)
                    SaveScreenshot(page, Path.Combine(report.Folder, "background-patterns.png"));
            });
        }

        await CheckAsync(report, "UnloadedSlidesReleaseTimersAndScaling", () =>
        {
            Require(dataSlide is { IsUpdateTimerRunning: false }, "The data timer is still running after unload.");
            Require(scalingSlide is { IsZoomTimerRunning: false }, "The scaling timer is still running after unload.");
            Require(previousScale == ScaleState.Capture(), "The scaling slide did not restore the previous scale configuration.");
            Require(patternsSlide is { IsEntranceTimerRunning: false }, "The completed pattern entrance timer is still running.");
            return Task.CompletedTask;
        });
        await CheckAsync(report, "ManualNextWrapsToFirstSlide", async () =>
        {
            var arrived = page.WaitForSlideAsync(0, token);
            page.NextSlide();
            await arrived;
            Require(page.CurrentIndex == 0 && !page.IsAutoRunning, "Manual next did not wrap to the first slide.");
        });
        await CheckAsync(report, "ManualPreviousWrapsToLastSlide", async () =>
        {
            var arrived = page.WaitForSlideAsync(5, token);
            page.PreviousSlide();
            await arrived;
            Require(page.CurrentIndex == 5 && !page.IsAutoRunning, "Manual previous did not wrap to the last slide.");
        });
        await CheckAsync(report, "LoginEntranceStopsWhenUnloaded", () => WithMotionAsync(() => CheckLoginLeftDuringEntranceAsync(page, token)));
        await CheckAsync(report, "KeyboardNavigationIgnoresTyping", () => CheckKeyboardAsync(page, token));
        await CheckAsync(report, "NewNavigationCancelsRunningCrossfade", () => WithMotionAsync(() => CheckCancelledCrossfadeAsync(page, token)));
        await CheckAsync(report, "ProgressOnlyWhileTheSlideshowRuns", async () =>
        {
            Require(!page.IsAutoRunning && page.ProgressValue == 0, $"Manual mode shows a countdown: {page.ProgressValue:F2}.");
            var previousMotion = DaisyAccessibility.ReducedMotionOverride;
            try
            {
                DaisyAccessibility.ReducedMotionOverride = true;
                page.ConfigureManualMode(false);
                await WaitForFrameAsync(page, token);
                Require(!page.IsAutoRunning && page.ProgressValue == 0,
                    $"With reduced motion the slideshow must not run or count down: running {page.IsAutoRunning}, progress {page.ProgressValue:F2}.");

                DaisyAccessibility.ReducedMotionOverride = false;
                await WaitForProgressAsync(page, token);
                Require(page.IsAutoRunning, "The slideshow did not start when reduced motion ended.");
                CheckProgressVisibility(page);
                SaveScreenshot(page, Path.Combine(report.Folder, "progress-coffee-theme.png"));
            }
            finally
            {
                page.ConfigureManualMode(true);
                DaisyAccessibility.ReducedMotionOverride = previousMotion;
            }
            Require(!page.IsAutoRunning && page.ProgressValue == 0, "Manual mode did not stop the countdown.");
        });
        try
        {
            var cycle = await CheckAutomaticCycleAsync(page, token);
            report.Add("AutomaticEightSecondCycle", true, cycle);
        }
        catch (Exception exception)
        {
            report.Add("AutomaticEightSecondCycle", false, exception.ToString());
        }
        await CheckAsync(report, "ManualMoveRestartsTheEightSecondTimer", () => CheckTimerRestartAsync(page, token));
    }

    // The login slide is left right after it was shown, while its seven features still fade in one by one.
    private static async Task CheckLoginLeftDuringEntranceAsync(KioskMainPage page, CancellationToken token)
    {
        Require(page.CurrentIndex == 5, "The login entrance check must begin at the last slide.");
        await page.NavigateToAsync(0, token);
        var login = RequireSlide<LoginSlide>(page);
        Require(login.IsFeatureTimerRunning, "The login entrance had already ended when the slide was shown.");
        await page.NavigateToAsync(5, token);
        Require(login is { IsFeatureTimerRunning: false, FeaturesShown.IsCanceled: true },
            "The login entrance went on after the slide was unloaded.");
    }

    // The check runs with animations, whatever the motion preference of the host.
    private static async Task WithMotionAsync(Func<Task> check)
    {
        var previousMotion = DaisyAccessibility.ReducedMotionOverride;
        try
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            await check();
        }
        finally
        {
            DaisyAccessibility.ReducedMotionOverride = previousMotion;
        }
    }

    // Keys pressed on the page move between the slides; typed into a field of a slide, they stay in the field.
    private static async Task CheckKeyboardAsync(KioskMainPage page, CancellationToken token)
    {
        Require(page.CurrentIndex == 5, "The keyboard check must begin at the last slide.");
        var arrived = page.WaitForSlideAsync(0, token);
        PressKey(page, Key.Right);
        await arrived;
        Require(page.CurrentIndex == 0, "The right arrow did not move to the next slide.");

        var email = Find<DaisyInput>(RequireSlide<LoginSlide>(page), "EmailInput");
        var shown = new List<int>();
        void OnShown(object? sender, int index) => shown.Add(index);
        page.SlideShown += OnShown;
        try
        {
            foreach (var key in new[] { Key.N, Key.P, Key.Space })
                PressKey(email, key);
            // Longer than a crossfade, which a navigation would end with.
            await Task.Delay(TimeSpan.FromSeconds(1.5), token);
        }
        finally
        {
            page.SlideShown -= OnShown;
        }
        if (shown.Count > 0 || page.CurrentIndex != 0)
        {
            // Back at the last slide, so the checks after this one start where they expect.
            await page.NavigateToAsync(5, token);
            throw new InvalidOperationException($"Typing into the email field changed the slide: {string.Join(", ", shown)}.");
        }

        arrived = page.WaitForSlideAsync(5, token);
        PressKey(page, Key.Left);
        await arrived;
        Require(page.CurrentIndex == 5 && !page.IsAutoRunning, "The left arrow did not move to the previous slide.");
    }

    private static void PressKey(Interactive source, Key key) =>
        source.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = source, Key = key });

    // A navigation that starts during the crossfade of another one cancels it and shows only its own slide.
    private static async Task CheckCancelledCrossfadeAsync(KioskMainPage page, CancellationToken token)
    {
        // The first navigation returns at its fade-out, so the second one starts inside its crossfade.
        var cancelled = page.NavigateToAsync(1, token);
        Require(!cancelled.IsCompleted, "The first navigation did not start a crossfade.");
        var target = page.NavigateToAsync(2, token);
        try
        {
            await cancelled;
            throw new InvalidOperationException("The interrupted crossfade still completed.");
        }
        catch (OperationCanceledException)
        {
            // The newer navigation cancelled it.
        }
        await target;
        Require(page.CurrentIndex == 2 && page.CurrentSlide is FeedbackSlide { IsLoaded: true } &&
            DaisyThemeManager.CurrentThemeName == page.SlideAt(2).Theme && page.CurrentTitle == page.SlideAt(2).Title.ToUpperInvariant(),
            "The second navigation did not show its slide.");
        var visible = new[] { page.SlideContentA, page.SlideContentB }.Where(static container => container.IsVisible).ToArray();
        Require(visible is [{ Opacity: 1 }] && page.OverlayLayer.Opacity == 1,
            $"After the cancelled crossfade {visible.Length} slide containers are visible, overlay opacity {page.OverlayLayer.Opacity:F2}.");
        await page.NavigateToAsync(5, token);
    }

    // A manual move in the running slideshow gives the new slide the full eight seconds.
    private static async Task CheckTimerRestartAsync(KioskMainPage page, CancellationToken token)
    {
        Require(page.CurrentIndex == 5 && !page.IsAutoRunning, "The timer check must begin in manual mode at the last slide.");
        var previousMotion = DaisyAccessibility.ReducedMotionOverride;
        try
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            page.ConfigureManualMode(false);
            Require(page.IsAutoRunning, "The slideshow timer did not start.");
            await Task.Delay(TimeSpan.FromSeconds(page.Interval / 2), token);
            var moved = page.WaitForSlideAsync(0, token);
            page.NextSlide();
            await moved;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await page.WaitForSlideAsync(1, token);
            var elapsed = stopwatch.Elapsed.TotalSeconds;
            Require(elapsed >= page.Interval && elapsed < page.Interval + 5,
                $"The slideshow advanced {elapsed:F3}s after the manual move instead of after {page.Interval}s.");
        }
        finally
        {
            page.ConfigureManualMode(true);
            DaisyAccessibility.ReducedMotionOverride = previousMotion;
        }
    }

    private static async Task<string> CheckAutomaticCycleAsync(KioskMainPage page, CancellationToken token)
    {
        Require(page.SlideCount == 6 && page.CurrentIndex == 5 && !page.IsAutoRunning,
            "The automatic cycle must begin in manual mode at the last of six slides.");
        Require(page.Interval == 8, "The normal slideshow interval must be eight seconds.");
        var expected = Enumerable.Range(0, page.SlideCount).ToArray();
        var observed = new List<(int Index, TimeSpan Elapsed)>();
        var stopwatch = new System.Diagnostics.Stopwatch();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string DescribeCycle() => string.Join(", ", observed.Select(item => $"{item.Index}@{item.Elapsed.TotalSeconds:F3}s"));
        void OnShown(object? sender, int index)
        {
            var elapsed = stopwatch.Elapsed;
            var previous = observed.Count == 0 ? TimeSpan.Zero : observed[^1].Elapsed;
            var position = observed.Count;
            observed.Add((index, elapsed));
            if (position >= expected.Length || index != expected[position])
                completed.TrySetException(new InvalidOperationException($"Unexpected automatic slide order: {DescribeCycle()}. Expected 0, 1, 2, 3, 4, 5."));
            else if ((elapsed - previous).TotalSeconds < page.Interval)
                completed.TrySetException(new InvalidOperationException($"The automatic slideshow advanced before eight seconds: {DescribeCycle()}."));
            else if (observed.Count == expected.Length)
                completed.TrySetResult();
        }
        void OnFailed(object? sender, Exception exception) => completed.TrySetException(exception);
        page.SlideShown += OnShown;
        page.NavigationFailed += OnFailed;
        var previousMotion = DaisyAccessibility.ReducedMotionOverride;
        try
        {
            // Exercise normal automatic navigation independently of the CI host's motion preference.
            DaisyAccessibility.ReducedMotionOverride = false;
            stopwatch.Start();
            page.ConfigureManualMode(false);
            Require(page.IsAutoRunning, "The slideshow timer did not start.");
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(page.Interval * page.SlideCount + 20), token);
            Require(observed.Select(item => item.Index).SequenceEqual(expected) && page.CurrentIndex == 5,
                $"The complete automatic cycle did not return to the last slide: {DescribeCycle()}.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Automatic cycle failed after {stopwatch.Elapsed.TotalSeconds:F3}s. Observed: {DescribeCycle()}.", exception);
        }
        finally
        {
            page.ConfigureManualMode(true);
            page.SlideShown -= OnShown;
            page.NavigationFailed -= OnFailed;
            stopwatch.Stop();
            DaisyAccessibility.ReducedMotionOverride = previousMotion;
        }
        Require(!page.IsAutoRunning, "Manual mode did not stop the slideshow timer.");
        return $"Natural sequence {DescribeCycle()}; elapsed {stopwatch.Elapsed.TotalSeconds:F3}s; no advance before the eight-second interval.";
    }

    private static async Task CheckAsync(KioskSmokeReport report, string name, Func<Task> check)
    {
        try
        {
            await check();
            report.Add(name, true, "The runtime check completed successfully.");
        }
        catch (InconclusiveException exception)
        {
            report.Add(name, SmokeOutcome.Inconclusive, exception.Message);
        }
        catch (Exception exception)
        {
            report.Add(name, false, exception.ToString());
        }
    }

    private static async Task CheckLoginAsync(LoginSlide slide, CancellationToken token)
    {
        await slide.FeaturesShown.WaitAsync(TimeSpan.FromSeconds(8), token);
        for (var index = 1; index <= 7; index++)
            Require(Find<TextBlock>(slide, $"Feature{index}").Opacity == 1, "A login feature did not finish its fade.");
        var email = Find<DaisyInput>(slide, "EmailInput");
        var password = Find<DaisyInput>(slide, "PasswordInput");
        email.Text = "kiosk@example.test";
        password.Text = "Desktop control input";
        Require(email.Text == "kiosk@example.test" && password.Text == "Desktop control input", "The form did not retain input.");
        var presenter = Descendants<TextPresenter>(email).Single();
        Require(presenter.Text == email.Text, "The input value did not reach the text presenter of the template.");
    }

    private static async Task CheckDataAsync(DataSlide slide, CancellationToken token)
    {
        await slide.FirstUpdate.WaitAsync(TimeSpan.FromSeconds(6), token);
        var cpu = Find<DaisyProgress>(slide, "CpuProgress").Value;
        var ram = Find<DaisyProgress>(slide, "RamProgress").Value;
        var io = Find<DaisyProgress>(slide, "IoProgress").Value;
        Require(cpu is >= 35 and < 85 && ram is >= 60 and < 92 && io is >= 10 and < 45, "Live metrics did not update within their expected ranges.");
        Require(Find<TextBlock>(slide, "CpuText").Text == $"{cpu}%", "The CPU label and progress value differ.");
        Require(Find<DaisyNumberFlow>(slide, "SessionFlow").Value > 1280, "The live session counter did not advance.");
    }

    private static void CheckFeedback(FeedbackSlide slide)
    {
        var rating = Find<DaisyRating>(slide, "StarRating");
        Require(rating.Value == 5 && rating.Maximum == 5, "The initial rating is incorrect.");
        rating.Value = 3;
        Require(rating.Value == 3, "The rating control did not accept a changed value.");
        Require(Descendants<DaisyLoading>(slide).Count() == 12, "The twelve loading variants are incomplete.");

        // The heading of the black loading card stays readable in the light theme of the slide.
        var heading = Find<TextBlock>(slide, "LoadingHeading");
        var card = heading.FindAncestorOfType<DaisyCard>() ?? throw new InvalidOperationException("The loading card is missing.");
        var background = (card.Background as ISolidColorBrush ?? throw new InvalidOperationException("The loading card has no solid background.")).Color;
        var text = heading.Foreground as ISolidColorBrush ?? throw new InvalidOperationException("The loading heading has no solid color.");
        var shown = CompositeColor(text, background, heading.Opacity);
        var contrast = ContrastRatio(shown, background);
        Require(contrast >= 4.5, $"The loading heading has a contrast of {contrast:F2} on its card in {DaisyThemeManager.CurrentThemeName}.");
    }

    private static void CheckShowcase(ShowcaseSlide slide)
    {
        var wheel = Find<DaisyColorWheel>(slide, "ColorWheel");
        wheel.Color = Colors.Lime;
        Require(Find<TextBlock>(slide, "HexText").Text == "#00FF00", "The color event did not update the hexadecimal preview.");
        Require(Find<Border>(slide, "ColorPreview").Background is ISolidColorBrush brush && brush.Color == Colors.Lime,
            "The color event did not update the visible preview brush.");
        Require(Find<Control>(slide, "WeatherCard").IsLoaded, "The weather card is not loaded.");
    }

    private static async Task CheckScalingAsync(ScalingSlide slide, KioskMainPage page, KioskSmokeReport report, CancellationToken token)
    {
        await slide.ZoomFinished.WaitAsync(TimeSpan.FromSeconds(12), token);
        Require(Find<DaisyRadialProgress>(slide, "ZoomProgress").Value == 200 && FloweryScaleManager.OverrideScaleFactor == 2,
            "The scaling demonstration did not reach 200 percent.");
        var checkpoints = new (double Percent, DaisySize Size)[]
        {
            (50, DaisySize.ExtraSmall), (65, DaisySize.Small), (90, DaisySize.Medium),
            (100, DaisySize.Medium), (120, DaisySize.Large), (160, DaisySize.ExtraLarge), (200, DaisySize.ExtraLarge)
        };
        var failed = 0;
        var inconclusive = 0;
        foreach (var checkpoint in checkpoints)
        {
            var diagnostics = "Layout measurement did not complete.";
            var screenshot = Path.Combine(report.Folder, $"scaling-{checkpoint.Percent:000}.png");
            var captureAttempted = false;
            try
            {
                slide.SetZoomForVerification(checkpoint.Percent);
                await WaitForFrameAsync(slide, token);
                diagnostics = DescribeScalingLayout(slide, checkpoint.Percent);
                if (checkpoint.Percent is 50 or 100 or 200)
                {
                    captureAttempted = true;
                    SaveScreenshot(page, screenshot);
                    diagnostics += $"\nScreenshot={screenshot}";
                }
                CheckScalingLayout(slide, checkpoint.Percent, checkpoint.Size);
                report.Add($"Scaling.Geometry.{checkpoint.Percent}", true, diagnostics);
            }
            catch (Exception exception)
            {
                var outcome = exception is InconclusiveException ? SmokeOutcome.Inconclusive : SmokeOutcome.Failed;
                if (outcome == SmokeOutcome.Inconclusive) inconclusive++;
                else failed++;
                if (!captureAttempted)
                {
                    try
                    {
                        SaveScreenshot(page, screenshot);
                        diagnostics += $"\nScreenshot={screenshot}";
                    }
                    catch (Exception captureException)
                    {
                        diagnostics += $"\nScreenshot failed: {captureException}";
                    }
                }
                report.Add($"Scaling.Geometry.{checkpoint.Percent}", outcome, $"{exception}\n{diagnostics}");
            }
        }
        Require(failed == 0, $"{failed} scaling geometry checkpoints failed; see Scaling.Geometry entries.");
        if (inconclusive > 0)
            throw new InconclusiveException($"{inconclusive} scaling geometry checkpoints were inconclusive; see Scaling.Geometry entries.");
    }

    private static void CheckScalingLayout(ScalingSlide slide, double percent, DaisySize size)
    {
        var factor = percent / 100;
        var card = Find<DaisyCard>(slide, "DemoCard");
        var content = Find<StackPanel>(slide, "DemoCardContent");
        var title = Find<TextBlock>(slide, "DemoTitle");
        var input = Find<DaisyInput>(slide, "DemoInput");
        var expectedPadding = ScaleHelper.GetScaledThickness(FloweryScalePreset.SpacingSmall, factor);
        Require(ApproximatelyEqual(card.BodyPadding, expectedPadding),
            "The card body did not receive the displayed scaling factor.");
        Require(ApproximatelyEqual(title.FontSize, ScaleHelper.FontSubheading(factor)) && ApproximatelyEqual(content.Spacing, ScaleHelper.SpacingSmall(factor)),
            "The title or content spacing differs from the displayed scaling factor.");
        Require(ApproximatelyEqual(Find<WrapPanel>(slide, "DemoButtonRow").ItemSpacing, ScaleHelper.SpacingXS(factor)) &&
            ApproximatelyEqual(Find<WrapPanel>(slide, "DemoBadgeRow").ItemSpacing, ScaleHelper.SpacingXS(factor)),
            "The wrapping rows did not receive the displayed scaling factor.");
        Require(Find<DaisyRadialProgress>(slide, "ZoomProgress").Value == percent,
            "The zoom indicator differs from the applied zoom.");
        var zoomPanel = Find<StackPanel>(slide, "ZoomPanel");
        // The zoom panel does not scale; on a small screen its text can overflow the compact slide. That overflow is
        // inconclusive; the remaining checks still apply.
        string? clippedZoomPanel = null;
        try { RequireContained(slide, zoomPanel, "Zoom panel within slide"); }
        catch (InvalidOperationException exception) { clippedZoomPanel = exception.Message; }
        if (clippedZoomPanel is null)
            foreach (var text in Descendants<TextBlock>(zoomPanel).Where(IsVisibleText))
                CheckTextVisible(text, zoomPanel, $"Zoom panel text '{text.Text}'");
        RequireContained(slide, card, "Card within slide");
        RequireContained(card, content, "Demo content within card");
        CheckTextVisible(title, content, "Complete demo title");

        var application = Application.Current!;
        var expectedFont = RequireDouble(application, $"DaisySize{size}FontSize");
        var expectedHeight = RequireDouble(application, $"DaisySize{size}Height");
        Require(input.Size == size, "The input has the wrong size tier.");
        Require(ApproximatelyEqual(input.FontSize, expectedFont), $"The input did not receive the size-tier font: {input.FontSize:F2}, expected {expectedFont:F2}.");
        var inputPadding = FindResource(application, $"DaisyInput{size}Padding") as Thickness?
            ?? throw new InvalidOperationException($"The resource DaisyInput{size}Padding is missing.");
        Require(ApproximatelyEqual(input.Padding.Left, inputPadding.Left) && ApproximatelyEqual(input.Padding.Right, inputPadding.Right),
            $"The input did not receive the size-tier horizontal padding: {input.Padding}, expected {inputPadding}.");
        RequireContained(content, input, "Input within demo content");
        foreach (var label in Descendants<TextBlock>(input).Where(IsVisibleText))
            CheckTextVisible(label, input, $"Input text '{label.Text}'");

        for (var index = 1; index <= 3; index++)
        {
            var button = Find<DaisyButton>(slide, $"DemoButton{index}");
            Require(button.Size == size && ApproximatelyEqual(button.FontSize, expectedFont) && Math.Abs(button.Bounds.Height - expectedHeight) <= 1,
                $"Button {index} did not render the expected size-tier dimensions: font {button.FontSize:F2}, height {button.Bounds.Height:F2}.");
            var buttonPadding = FindResource(application, $"DaisyButton{size}Padding") as Thickness?
                ?? throw new InvalidOperationException($"The resource DaisyButton{size}Padding is missing.");
            Require(ApproximatelyEqual(button.Padding, buttonPadding), $"Button {index} did not receive the size-tier padding.");
            RequireContained(content, button, $"Button {index} within demo content");
            foreach (var label in Descendants<TextBlock>(button).Where(IsVisibleText))
                CheckTextVisible(label, button, $"Button {index} text '{label.Text}'");

            var badge = Find<DaisyBadge>(slide, $"DemoBadge{index}");
            Require(badge.Size == size, $"Badge {index} has the wrong size tier.");
            RequireContained(content, badge, $"Badge {index} within demo content");
            foreach (var border in Descendants<Border>(badge).Where(element => element.Bounds.Width > 0 && element.Bounds.Height > 0))
                RequireContained(badge, border, $"Badge {index} inner border");
            foreach (var label in Descendants<TextBlock>(badge).Where(IsVisibleText))
                CheckTextVisible(label, badge, $"Badge {index} text '{label.Text}'");
        }
        if (clippedZoomPanel is not null)
            throw new InconclusiveException(clippedZoomPanel);
    }

    private static bool IsVisibleText(TextBlock text) => !string.IsNullOrEmpty(text.Text) &&
        text.IsEffectivelyVisible && text.Bounds.Width > 0 && text.Bounds.Height > 0;

    private static bool ApproximatelyEqual(double left, double right) => Math.Abs(left - right) <= ResourceValueTolerance;

    private static bool ApproximatelyEqual(Thickness left, Thickness right) =>
        ApproximatelyEqual(left.Left, right.Left) && ApproximatelyEqual(left.Top, right.Top) &&
        ApproximatelyEqual(left.Right, right.Right) && ApproximatelyEqual(left.Bottom, right.Bottom);

    /// <summary>
    /// Checks that a text block lies inside its container and has the room its full text needs: a detached copy with
    /// the same font, measured in the same width, must fit into the text block's bounds.
    /// </summary>
    private static void CheckTextVisible(TextBlock text, Control container, string description)
    {
        RequireContained(container, text, description);
        var probe = new TextBlock
        {
            Text = text.Text,
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontWeight = text.FontWeight,
            FontStyle = text.FontStyle,
            FontStretch = text.FontStretch,
            LetterSpacing = text.LetterSpacing,
            LineHeight = text.LineHeight,
            TextWrapping = text.TextWrapping,
            TextTrimming = TextTrimming.None,
            FlowDirection = text.FlowDirection,
            Padding = text.Padding
        };
        var width = text.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : text.Bounds.Width;
        probe.Measure(new Size(width, double.PositiveInfinity));
        var measured = probe.DesiredSize;
        Require(measured.Width <= text.Bounds.Width + 1 && measured.Height <= text.Bounds.Height + 1,
            $"{description} is clipped: full text needs {measured}, actual {text.Bounds.Width:F2}x{text.Bounds.Height:F2}, font {text.FontSize:F2}, family {text.FontFamily}.");
    }

    private static void RequireContained(Control container, Control element, string description)
    {
        var transform = element.TransformToVisual(container)
            ?? throw new InvalidOperationException($"{description}: the element is not below the container.");
        var bounds = new Rect(element.Bounds.Size).TransformToAABB(transform);
        Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= container.Bounds.Width + 1 && bounds.Bottom <= container.Bounds.Height + 1,
            $"{description} is clipped: bounds {bounds}, container {container.Bounds.Width:F2}x{container.Bounds.Height:F2}, desired {element.DesiredSize}.");
    }

    private static string DescribeScalingLayout(ScalingSlide slide, double percent)
    {
        var names = new[] { "DemoLayout", "DemoCard", "DemoCardContent", "DemoTitle", "DemoInput", "DemoButtonRow",
            "DemoButton1", "DemoButton2", "DemoButton3", "DemoBadgeRow", "DemoBadge1", "DemoBadge2", "DemoBadge3" };
        var details = new List<string> { $"Zoom={percent:F2}%; RenderScaling={TopLevel.GetTopLevel(slide)?.RenderScaling:F2}; Slide={slide.Bounds.Width:F2}x{slide.Bounds.Height:F2}" };
        foreach (var name in names)
        {
            var element = Find<Control>(slide, name);
            var bounds = element.TransformToVisual(slide) is { } transform ? new Rect(element.Bounds.Size).TransformToAABB(transform) : default;
            var font = element is TemplatedControl control ? control.FontSize : element is TextBlock text ? text.FontSize : 0;
            details.Add($"{name}: Bounds={bounds}; Desired={element.DesiredSize}; Font={font:F2}");
        }
        var card = Find<DaisyCard>(slide, "DemoCard");
        details.Add($"Card.BodyPadding={card.BodyPadding}");
        var input = Find<DaisyInput>(slide, "DemoInput");
        details.Add($"Input: Actual={input.Bounds.Width:F2}x{input.Bounds.Height:F2}; Desired={input.DesiredSize}; Font={input.FontSize:F2}; Padding={input.Padding}");
        return string.Join("\n", details);
    }

    private static async Task CheckPatternsAsync(PatternsSlide slide, CancellationToken token)
    {
        await slide.EntranceFinished.WaitAsync(TimeSpan.FromSeconds(8), token);
        var cards = Descendants<DaisyPatternedCard>(slide).ToArray();
        Require(cards.Length == 6 && cards.Select(card => card.Pattern).Distinct().Count() == 6, "The six distinct pattern cards are incomplete.");
        Require(cards.All(card => card.IsLoaded && card.Opacity == 1 && card.RenderTransform is TranslateTransform { X: 0 }),
            "A pattern card did not finish its visible entrance animation.");
        // A card draws its pattern when it renders differently from the same card without one.
        var blank = new List<Flowery.Enums.DaisyCardPattern>();
        foreach (var card in cards)
        {
            var pattern = card.Pattern;
            var drawn = RenderPixels(card, out _);
            card.Pattern = Flowery.Enums.DaisyCardPattern.None;
            try
            {
                await WaitForFrameAsync(card, token);
                if (drawn.AsSpan().SequenceEqual(RenderPixels(card, out _)))
                    blank.Add(pattern);
            }
            finally
            {
                card.Pattern = pattern;
            }
        }
        await WaitForFrameAsync(slide, token);
        Require(blank.Count == 0, $"These pattern cards draw no pattern: {string.Join(", ", blank)}.");
    }

    private static void CheckRenderedPixels(Control slide)
    {
        var pixels = RenderPixels(slide, out var size);
        Require(size.Width > 0 && size.Height > 0, "Rendering returned an empty bitmap.");
        Require(pixels.Length >= 4 && pixels.Length == checked(size.Width * size.Height * 4), "The rendered bitmap has invalid pixel storage.");
        var visible = false;
        var varied = false;
        for (var index = 0; index < pixels.Length; index += 4)
        {
            visible |= pixels[index + 3] > 0;
            varied |= pixels[index] != pixels[0] || pixels[index + 1] != pixels[1] || pixels[index + 2] != pixels[2];
            if (visible && varied)
                return;
        }
        throw new InvalidOperationException("The slide render is transparent or contains no visual detail.");
    }

    private static RenderTargetBitmap Render(Control element)
    {
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(element.Bounds.Width)), Math.Max(1, (int)Math.Ceiling(element.Bounds.Height)));
        var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(element);
        return bitmap;
    }

    private static byte[] RenderPixels(Control element, out PixelSize size)
    {
        using var bitmap = Render(element);
        size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[checked(stride * size.Height)];
        var buffer = Marshal.AllocHGlobal(pixels.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), buffer, pixels.Length, stride);
            Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return pixels;
    }

    private static void SaveScreenshot(Control element, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PixelSize expected;
        using (var bitmap = Render(element))
        {
            expected = bitmap.PixelSize;
            bitmap.Save(path, PngBitmapEncoderOptions.Default);
        }
        using var readback = new Bitmap(path);
        Require(readback.PixelSize == expected, "The PNG screenshot failed dimensions readback.");
    }

    private static async Task WaitForProgressAsync(KioskMainPage page, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (page.ProgressValue is < 10 or >= 100)
        {
            Require(DateTime.UtcNow < deadline, $"The progress did not advance on rendered frames: {page.ProgressValue:F2}.");
            await WaitForFrameAsync(page, token);
        }
    }

    private static async Task WaitForFrameAsync(Control element, CancellationToken token)
    {
        var topLevel = TopLevel.GetTopLevel(element) ?? throw new InvalidOperationException("The element is not in a window.");
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        topLevel.RequestAnimationFrame(_ => rendered.TrySetResult());
        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    private static void CheckProgressVisibility(KioskMainPage page)
    {
        var progress = Find<DaisyProgress>(page, "SlideProgress");
        var fill = Descendants<Border>(progress).FirstOrDefault(element => element.Name == "PART_Indicator")
            ?? throw new InvalidOperationException("The progress indicator is missing.");
        var pageBrush = page.Background as ISolidColorBrush ?? throw new InvalidOperationException("The page background is not a solid theme brush.");
        var trackBrush = progress.Background as ISolidColorBrush ?? throw new InvalidOperationException("The progress track is not a solid theme brush.");
        var fillBrush = (fill.Background ?? progress.Foreground) as ISolidColorBrush ?? throw new InvalidOperationException("The progress fill is not a solid theme brush.");
        var pageColor = pageBrush.Color;
        var trackColor = CompositeColor(trackBrush, pageColor);
        var fillColor = CompositeColor(fillBrush, trackColor);
        var trackContrast = ContrastRatio(fillColor, trackColor);
        var pageContrast = ContrastRatio(fillColor, pageColor);
        Require(trackContrast >= 3 && pageContrast >= 3,
            $"The progress fill is not distinguishable in {DaisyThemeManager.CurrentThemeName}: fill={fillColor}, track={trackColor}, page={pageColor}, contrast={trackContrast:F2}/{pageContrast:F2}.");

        var transform = fill.TransformToVisual(progress) ?? throw new InvalidOperationException("The progress fill is not below the progress bar.");
        var bounds = new Rect(fill.Bounds.Size).TransformToAABB(transform);
        var visibleWidth = Math.Max(0, Math.Min(bounds.Right, progress.Bounds.Width) - Math.Max(0, bounds.Left));
        var visibleHeight = Math.Max(0, Math.Min(bounds.Bottom, progress.Bounds.Height) - Math.Max(0, bounds.Top));
        Require(visibleWidth >= 1 && visibleHeight >= 1 && ApproximatelyEqual(progress.Bounds.Height, 4),
            $"The progress fill has no visible area: fill={bounds}, progress={progress.Bounds.Width:F2}x{progress.Bounds.Height:F2}, visible={visibleWidth:F2}x{visibleHeight:F2}.");
    }

    // The brush over the background, with the opacity of the brush and of the element that draws it.
    private static Color CompositeColor(ISolidColorBrush foreground, Color background, double elementOpacity = 1)
    {
        var color = foreground.Color;
        var alpha = color.A / 255.0 * foreground.Opacity * elementOpacity;
        return Color.FromArgb(255,
            (byte)Math.Round(color.R * alpha + background.R * (1 - alpha)),
            (byte)Math.Round(color.G * alpha + background.G * (1 - alpha)),
            (byte)Math.Round(color.B * alpha + background.B * (1 - alpha)));
    }

    private static double ContrastRatio(Color first, Color second)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static object? FindResource(Application application, string key) =>
        application.TryGetResource(key, application.ActualThemeVariant, out var value) ? value : null;

    private static double RequireDouble(Application application, string key) =>
        FindResource(application, key) as double? ?? throw new InvalidOperationException($"The resource {key} is missing.");

    private static T RequireSlide<T>(KioskMainPage page) where T : Control =>
        page.CurrentSlide as T ?? throw new InvalidOperationException($"The active slide is not {typeof(T).Name}.");

    private static T Find<T>(Control root, string name) where T : Control =>
        root.FindControl<T>(name) ?? throw new InvalidOperationException($"The element '{name}' was not found.");

    private static IEnumerable<T> Descendants<T>(Visual root) => root.GetVisualDescendants().OfType<T>();

    private static void RequireFile(string path) => Require(File.Exists(path) && new FileInfo(path).Length > 0, $"Required asset is missing or empty: {path}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    // A check whose result depends on the session rather than the code; it is reported without failing the run.
    private sealed class InconclusiveException(string message) : Exception(message);

    private sealed record ScaleState(bool Enabled, double Minimum, double Maximum, double? Override)
    {
        internal static ScaleState Capture() => new(FloweryScaleManager.IsEnabled, FloweryScaleManager.MinScaleFactor,
            FloweryScaleManager.MaxScaleFactor, FloweryScaleManager.OverrideScaleFactor);
    }
}
