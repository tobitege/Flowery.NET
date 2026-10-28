using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Examples;
using Flowery.NET.Gallery.Localization;
using Flowery.Theming;
using SkiaSharp;
using Xunit;
using static Flowery.NET.RuntimeTests.Controls.DaisyTourTestHelpers;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;
using CollectionAssert = Flowery.NET.RuntimeTests.Controls.MSTestCollectionAssert;
using Path = Avalonia.Controls.Shapes.Path;
using StringAssert = Flowery.NET.RuntimeTests.Controls.MSTestStringAssert;

namespace Flowery.NET.RuntimeTests.Controls;

public class Given_DaisyTour
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_GalleryTourStarts_DeclaredStepsResolveAndNavigationPreparesRealTargets()
    {
        // The CI window is 752x480; larger local windows also show the wider page.
        foreach (var (name, maxWidth, maxHeight) in new[] { ("default viewport", 900d, 600d), ("CI viewport", 752d, 480d) })
            await RuntimeTestHelpers.RunCaseAsync(name, () => VerifyGalleryTourAsync(maxWidth, maxHeight), TimeSpan.FromMinutes(2));
    }

    private static async Task VerifyGalleryTourAsync(double maxWidth, double maxHeight)
    {
        var previousCulture = FloweryLocalization.CurrentCulture.Name;
        var previousResolver = FloweryLocalization.CustomResolver;
        TourExamples? page = null;
        DaisyTour? tour = null;
        try
        {
            FloweryLocalization.SetCulture("en");
            page = new TourExamples { MaxWidth = maxWidth, MaxHeight = maxHeight };
            await LoadAsync(page);
            AssertInViewport(BoundsInRoot(page), RootSize(page), "The Gallery page");
            tour = page.FindControl<DaisyTour>("Tour")!;
            var primary = page.FindControl<DaisyButton>("PrimaryTarget")!;
            var nested = page.FindControl<DaisyButton>("NestedTarget")!;
            var tabs = page.FindControl<TabControl>("DemoTabs")!;
            var overview = page.FindControl<TabItem>("OverviewTab")!;
            var details = page.FindControl<TabItem>("DetailsTab")!;
            var mainScroll = page.FindControl<ScrollViewer>("MainScrollViewer")!;
            Assert.HasCount(3, tour.Steps);
            var welcome = tour.Steps[0];
            var scrolling = tour.Steps[1];
            var deferred = tour.Steps[2];
            Assert.AreSame(primary, welcome.Target, "The first typed XAML target must resolve during loading.");
            Assert.AreSame(nested, scrolling.Target, "The second typed XAML target must resolve during loading.");
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_WelcomeTitle"), welcome.Title);
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_WelcomeDescription"), welcome.Description);
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_ScrollTitle"), scrolling.Title);
            Assert.AreEqual(GalleryLocalization.GetString("Gallery_Tour_ScrollDescription"), scrolling.Description);
            Assert.IsFalse(string.IsNullOrWhiteSpace(welcome.Title));
            Assert.IsFalse(string.IsNullOrWhiteSpace(welcome.Description));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scrolling.Title));
            Assert.IsFalse(string.IsNullOrWhiteSpace(scrolling.Description));
            var closed = new List<DaisyTourClosedEventArgs>();
            tour.Closed += (_, args) => { closed.Add(args); tour.Tag = args; };

            await StartGalleryTourAsync(page, tour);
            AssertActive(tour, welcome, 0);
            AssertTargetBounds(tour, primary);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, scrolling, 1);
            Assert.IsTrue(mainScroll.Offset.Y > 0);
            var innerScroll = Descendants<ScrollViewer>(page).Single(viewer =>
                !ReferenceEquals(viewer, mainScroll) && DaisyTourLayout.IsDescendant(nested, viewer));
            Assert.IsTrue(innerScroll.Offset.Y > 0);
            AssertFits(nested, innerScroll);
            AssertFits(nested, mainScroll);
            AssertInViewport(BoundsInRoot(nested), RootSize(page), "The nested Gallery target");
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, deferred, 2);
            Assert.AreSame(details, tabs.SelectedItem);
            Assert.IsInstanceOfType<DaisyButton>(details.Content);
            Assert.AreSame(details.Content, deferred.Target);
            Assert.IsTrue(deferred.Target!.IsLoaded);
            AssertTargetBounds(tour, deferred.Target);
            var createdTarget = deferred.Target;
            await tour.BackAsync().WaitAsync(Timeout);
            AssertActive(tour, scrolling, 1);
            Assert.AreSame(overview, tabs.SelectedItem);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, deferred, 2);
            Assert.AreSame(createdTarget, deferred.Target, "Returning to the deferred tab must reuse its existing control.");
            await tour.NextAsync().WaitAsync(Timeout);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Completed, closed[0].Reason);
            Assert.AreEqual("deferred-tab", closed[0].StepId);
            await StartGalleryTourAsync(page, tour);
            AssertActive(tour, welcome, 0);
            Assert.AreSame(primary, tour.CurrentStep!.Target);
            tour.Skip();
            Assert.HasCount(2, closed);
            Assert.AreEqual(DaisyTourCloseReason.Skipped, closed[1].Reason);
        }
        finally
        {
            try
            {
                tour?.Cancel();
                if (page is not null)
                {
                    RuntimeTestHelpers.GetHostPanel().Children.Remove(page);
                    await ArrangeAsync(page);
                }
            }
            finally
            {
                FloweryLocalization.SetCulture(previousCulture);
                FloweryLocalization.CustomResolver = previousResolver;
            }
        }
    }

    [Fact]
    public async Task When_GalleryCustomShapeRuns_ItEnclosesTheHighlightedTarget()
    {
        var previousCulture = FloweryLocalization.CurrentCulture.Name;
        TourExamples? page = null;
        DaisyTour? tour = null;
        try
        {
            FloweryLocalization.SetCulture("de");
            page = new TourExamples { MaxWidth = 752, MaxHeight = 480 };
            await LoadAsync(page);
            tour = page.FindControl<DaisyTour>("Tour")!;
            await StartGalleryTourAsync(page, tour, "Gallery_Tour_Custom");
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            var box = new Rect(Canvas.GetLeft(body), Canvas.GetTop(body), body.Width, body.Height);
            var target = BoundsInRoot(page.FindControl<Control>("PrimaryTarget")!);
            var margin = tour.HighlightPadding + 2;
            var diagnostic = $"marker={box}, target={target}";
            Assert.AreEqual(target.X + target.Width / 2, box.X + box.Width / 2, 1, diagnostic);
            Assert.AreEqual(target.Y + target.Height / 2, box.Y + box.Height / 2, 1, diagnostic);
            foreach (var (x, y) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                var dx = x * (target.Width / 2 + margin) / (box.Width / 2);
                var dy = y * (target.Height / 2 + margin) / (box.Height / 2);
                Assert.IsTrue(dx * dx + dy * dy <= 1.0001, $"{diagnostic}: highlight corner ({x},{y}) must lie inside the ellipse.");
            }
        }
        finally
        {
            try
            {
                tour?.Cancel();
                if (page is not null)
                {
                    RuntimeTestHelpers.GetHostPanel().Children.Remove(page);
                    await ArrangeAsync(page);
                }
            }
            finally { FloweryLocalization.SetCulture(previousCulture); }
        }
    }

    [Fact]
    public async Task When_ConnectorSpansIndependentScrollRegions_BothEndsAreRevealedAndTracked()
    {
        var selection = new ComboBox { Width = 120, Margin = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Left };
        var sidebar = new ScrollViewer { Content = new StackPanel { Children = { selection, new Border { Height = 800 } } } };
        var target = CreateTarget();
        target.Margin = new Thickness(0, 820, 16, 0);
        target.HorizontalAlignment = HorizontalAlignment.Right;
        var content = new ScrollViewer { Content = new Grid { Height = 1000, Children = { target } } };
        Grid.SetColumn(content, 1);
        var annotation = new DaisyTourAnnotation
        {
            Id = "regions", Source = selection, Target = target, Preset = DaisyTourAnnotationPreset.CurvedArrow,
            Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red)
        };
        var step = new DaisyTourStep { Id = "regions", Target = target };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, sidebar, content);
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumnSpan(tour, 2);
        try
        {
            await LoadAsync(root);
            Assert.IsTrue(ChangeView(sidebar, null, 500), "The sidebar scrolls its selection out of view.");
            await RenderFrameAsync(root);
            // The offset is clamped to the extent, so the assertion compares with the offset that hid the selection.
            var hiddenOffset = sidebar.Offset.Y;
            Assert.IsTrue(TransformBounds(selection, sidebar, new Rect(0, 0, selection.Bounds.Width, selection.Bounds.Height)).Bottom <= 0,
                "The selection starts out of view.");
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            Assert.IsTrue(sidebar.Offset.Y < hiddenOffset, "The sidebar must scroll its selection back into view.");
            Assert.IsTrue(content.Offset.Y > 0, "The content region must reveal the target.");
            AssertFits(selection, sidebar);
            AssertFits(target, content);
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            AssertConnectorEnds(body, Frame(tour, BoundsInRoot(selection)), Frame(tour, BoundsInRoot(target)));
            Assert.IsTrue(await CountConnectorPixelsAsync(tour, Colors.Red, BoundsInRoot(selection), BoundsInRoot(target)) >= 100);

            var updated = NewCompletion();
            EventHandler handler = (_, _) => updated.TrySetResult();
            tour.GeometryUpdated += handler;
            try
            {
                ChangeView(content, null, content.Offset.Y + 60);
                await updated.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            await RenderFrameAsync(root);
            AssertActive(tour, step, 0);
            AssertConnectorEnds(body, Frame(tour, BoundsInRoot(selection)), Frame(tour, BoundsInRoot(target)));
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_ConnectorEndCannotBeShownWithTheStepTarget_TheStepStaysOpenAndTheLineReachesIt()
    {
        var target = CreateTarget();
        var far = CreateTarget();
        far.Margin = new Thickness(300, 1200, 0, 0);
        var viewer = new ScrollViewer { Content = new Grid { Height = 1400, Children = { target, far } } };
        var annotation = new DaisyTourAnnotation
        {
            Id = "far", Source = target, Target = far, Kind = DaisyTourAnnotationKind.Line,
            Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 4
        };
        var step = new DaisyTourStep { Id = "far", Target = target };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, viewer);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertFits(target, viewer);
            AssertTargetBounds(tour, target);
            var farBounds = BoundsInRoot(far);
            Assert.IsTrue(farBounds.Top > BoundsInRoot(viewer).Bottom, $"The far end must stay outside the visible area: {farBounds}.");
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            AssertConnectorEnds(body, Frame(tour, BoundsInRoot(target)), Frame(tour, farBounds));
            await RenderFrameAsync(root);
            Assert.IsTrue(await CountConnectorPixelsAsync(tour, Colors.Red, BoundsInRoot(target), farBounds) >= 50,
                "The visible part of the line must render.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_GalleryRegionsDemoRuns_TheSidebarThemeSelectionConnectsToThePage()
    {
        // The CI window size leaves little room below the Gallery header.
        var view = new MainView { MaxWidth = 752, MaxHeight = 480 };
        DaisyTour? tour = null;
        try
        {
            await LoadAsync(view);
            typeof(MainView).GetMethod("NavigateToCategory", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(view, ["Sidebar_Tour", null]);
            await ArrangeAsync(view);
            var page = Descendants<TourExamples>(view).Single();
            await RuntimeTestHelpers.EnsureLoadedAsync(page).WaitAsync(Timeout);
            await ArrangeAsync(view);
            tour = page.FindControl<DaisyTour>("Tour")!;
            var regionTarget = page.FindControl<Control>("RegionTarget")!;
            string ScrollChain()
            {
                var chain = new List<string>();
                for (Visual? current = regionTarget; current is not null; current = current.GetVisualParent())
                {
                    if (current is ScrollViewer viewer)
                        chain.Add($"{viewer.Name}: offset={viewer.Offset.Y}, viewport={viewer.Viewport.Height}, extent={viewer.Extent.Height}, " +
                            $"scrollable={viewer.ScrollBarMaximum.Y}, actual={viewer.Bounds.Height}, mode={viewer.VerticalScrollBarVisibility}");
                }
                return $"view={view.Bounds.Width}x{view.Bounds.Height}, page={page.Bounds.Width}x{page.Bounds.Height}, target={BoundsInRoot(regionTarget)}; " + string.Join(" | ", chain);
            }
            var before = ScrollChain();
            try { await StartGalleryTourAsync(page, tour, "Gallery_Tour_Regions"); }
            catch (Exception error) { throw new AssertFailedException($"{error.Message} Before: {before}. After: {ScrollChain()}", error); }
            // The sidebar can still settle after the step opens, such as after an earlier theme change; the target
            // trackers then place the connector again in a queued update, which runs before the connector is measured.
            await ArrangeAsync(view);
            var annotation = tour.CurrentStep!.Annotations.Single();
            Assert.IsInstanceOfType<DaisyThemeDropdown>(annotation.Source);
            Assert.IsFalse(DaisyTourLayout.IsDescendant(annotation.Source, page), "The source must come from the sidebar region.");
            var source = BoundsInRoot(annotation.Source);
            var target = BoundsInRoot(annotation.Target!);
            AssertInViewport(Frame(tour, source), RootSize(view), "The sidebar theme selection");
            AssertInViewport(Frame(tour, target), RootSize(view), "The page target");
            var body = (Path)Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Children[1];
            AssertConnectorEnds(body, Frame(tour, source), Frame(tour, target));
            var stroke = ((ISolidColorBrush)DaisyResourceLookup.GetBrush("DaisyPrimaryBrush")!).Color;
            Assert.IsTrue(await CountConnectorPixelsAsync(tour, stroke, source, target) >= 100, "The connector must render between both regions.");

            tour.Cancel();
            await StartGalleryTourAsync(page, tour, "Gallery_Tour_Paths");
            for (var index = 0; index < tour.Steps.Count; index++)
            {
                if (index > 0) await tour.NextAsync().WaitAsync(Timeout);
                AssertActive(tour, tour.Steps[index], index);
                await RenderFrameAsync(view);
                var path = tour.CurrentStep!.Annotations.Single();
                Assert.IsTrue(await CountConnectorPixelsAsync(tour, stroke, BoundsInRoot(path.Source!), BoundsInRoot(path.Target!)) >= 100,
                    $"The {path.Path} connector must render between both regions.");
            }
        }
        finally
        {
            tour?.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(view);
            await ArrangeAsync(view);
        }
    }

    private static void AssertConnectorEnds(Path body, Rect sourceFrame, Rect targetFrame)
    {
        var figure = ((PathGeometry)body.Data!).Figures!.Single();
        var end = figure.Segments!.Last() switch
        {
            LineSegment line => line.Point,
            QuadraticBezierSegment curve => curve.Point2,
            BezierSegment cubic => cubic.Point3,
            var segment => throw new AssertFailedException($"Unexpected connector segment {segment.GetType().Name}.")
        };
        Assert.IsTrue(OnBoundary(figure.StartPoint, sourceFrame), $"Start {figure.StartPoint} must lie on the source frame {sourceFrame}.");
        Assert.IsTrue(OnBoundary(end, targetFrame), $"End {end} must lie on the target frame {targetFrame}.");
    }

    private static bool OnBoundary(Point point, Rect rect) =>
        point.X >= rect.Left - 0.5 && point.X <= rect.Right + 0.5 && point.Y >= rect.Top - 0.5 && point.Y <= rect.Bottom + 0.5 &&
        (Math.Abs(point.X - rect.Left) <= 0.5 || Math.Abs(point.X - rect.Right) <= 0.5 ||
         Math.Abs(point.Y - rect.Top) <= 0.5 || Math.Abs(point.Y - rect.Bottom) <= 0.5);

    // Renders the tour overlay; Scale converts overlay DIP to bitmap pixels.
    private static Task<(byte[] Pixels, int Width, int Height, double Scale)> RenderSurfaceAsync(DaisyTour tour)
    {
        var surface = GetSurface(tour);
        var bitmap = RenderBitmap(surface);
        return Task.FromResult((bitmap.Pixels, bitmap.PixelWidth, bitmap.PixelHeight, bitmap.PixelWidth / surface.Bounds.Width));
    }

    // An opaque BGRA pixel close to the color.
    private static bool IsColor(byte[] pixels, int offset, Color color) =>
        pixels[offset + 3] >= 240 && Math.Abs(pixels[offset] - color.B) <= 20 &&
        Math.Abs(pixels[offset + 1] - color.G) <= 20 && Math.Abs(pixels[offset + 2] - color.R) <= 20;

    private static async Task<int> CountSurfacePixelsAsync(DaisyTour tour, Color color, Rect area)
    {
        var (pixels, width, height, scale) = await RenderSurfaceAsync(tour);
        var matches = 0;
        for (var y = Math.Max(0, (int)(area.Top * scale)); y < Math.Min(height, (int)(area.Bottom * scale)); y++)
        {
            for (var x = Math.Max(0, (int)(area.Left * scale)); x < Math.Min(width, (int)(area.Right * scale)); x++)
            {
                if (IsColor(pixels, (y * width + x) * 4, color)) matches++;
            }
        }
        return matches;
    }

    // Counts stroke-colored overlay pixels outside both highlight frames and the card.
    private static async Task<int> CountConnectorPixelsAsync(DaisyTour tour, Color stroke, Rect source, Rect target)
    {
        var (pixels, width, height, scale) = await RenderSurfaceAsync(tour);
        var excluded = new[] { Frame(tour, source), Frame(tour, target), tour.CardBounds }
            .Select(rect => new Rect(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6)).ToArray();
        var matches = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsColor(pixels, (y * width + x) * 4, stroke))
                    continue;
                var point = new Point((x + 0.5) / scale, (y + 0.5) / scale);
                if (!excluded.Any(rect => rect.Contains(point))) matches++;
            }
        }
        return matches;
    }

    [Fact]
    public async Task When_Navigating_CompletesOnceAndReplaysFromTheFirstStep()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var preparations = new List<string>();
        var first = new DaisyTourStep { Id = "first", Target = target, Title = "First" };
        var last = new DaisyTourStep { Id = "last", Target = target, Title = "Last" };
        first.PrepareAsync = (step, _) => { preparations.Add(step.Id); return Task.CompletedTask; };
        last.PrepareAsync = (step, _) => { preparations.Add(step.Id); return Task.CompletedTask; };
        tour.Steps.Add(first);
        tour.Steps.Add(last);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, first, 0);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, last, 1);
            await tour.BackAsync().WaitAsync(Timeout);
            AssertActive(tour, first, 0);
            CollectionAssert.AreEqual(new[] { "first", "last", "first" }, preparations);
            await tour.NextAsync().WaitAsync(Timeout);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Completed, closed[0].Reason);
            Assert.AreEqual("last", closed[0].StepId);
            tour.Skip();
            tour.Cancel();
            Assert.HasCount(1, closed);

            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, first, 0);
            tour.Skip();
            Assert.HasCount(2, closed);
            Assert.AreEqual(DaisyTourCloseReason.Skipped, closed[1].Reason);
            Assert.AreEqual("first", closed[1].StepId);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_StartNextAndBackAwaitPreparation_TheCardNeverRendersAtAPlaceholderPosition()
    {
        var firstTarget = CreateTarget();
        var secondTarget = CreateTarget();
        secondTarget.Margin = new Thickness(400, 240, 0, 0);
        var startGate = new PreparationGate();
        var nextGate = new PreparationGate();
        var backGate = new PreparationGate();
        var first = new DaisyTourStep
        {
            Id = "first-staged", Title = "First prepared target", Target = firstTarget, PrepareAsync = startGate.PrepareAsync
        };
        var second = new DaisyTourStep
        {
            Id = "second-staged", Title = "Second prepared target", Target = secondTarget, PrepareAsync = nextGate.PrepareAsync
        };
        var tour = new DaisyTour();
        tour.Steps.Add(first);
        tour.Steps.Add(second);
        var root = CreateRoot(tour, firstTarget, secondTarget);
        try
        {
            await LoadAsync(root);
            var starting = tour.StartAsync();
            await startGate.Entered.Task.WaitAsync(Timeout);
            var surface = GetSurface(tour);
            var card = Descendants<Border>(surface).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            await RenderFrameAsync(root);
            Assert.IsTrue(tour.IsTransitioning);
            Assert.IsFalse(card.IsVisible, "Initial preparation must not render a centered instruction card.");
            startGate.Release.TrySetResult();
            await starting.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, first, 0);
            AssertPlacedCard(tour, card, firstTarget);

            var firstBounds = tour.CardBounds;
            var navigating = tour.NextAsync();
            await nextGate.Entered.Task.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertRetainedCard(tour, card, first, 0, firstBounds);
            nextGate.Release.TrySetResult();
            await navigating.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, second, 1);
            AssertPlacedCard(tour, card, secondTarget);

            first.PrepareAsync = backGate.PrepareAsync;
            var secondBounds = tour.CardBounds;
            var returning = tour.BackAsync();
            await backGate.Entered.Task.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertRetainedCard(tour, card, second, 1, secondBounds);
            backGate.Release.TrySetResult();
            await returning.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, first, 0);
            AssertPlacedCard(tour, card, firstTarget);
        }
        finally
        {
            startGate.Release.TrySetResult();
            nextGate.Release.TrySetResult();
            backGate.Release.TrySetResult();
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_ClosedDuringInitialPreparation_LateCompletionCannotReopenTheTour()
    {
        foreach (var (name, skip, reason) in new[]
        {
            ("caller cancellation", false, DaisyTourCloseReason.Cancelled), ("Skip", true, DaisyTourCloseReason.Skipped)
        })
            await RuntimeTestHelpers.RunCaseAsync(name, () => VerifyClosedDuringInitialPreparationAsync(skip, reason));
    }

    private static async Task VerifyClosedDuringInitialPreparationAsync(bool skip, DaisyTourCloseReason reason)
    {
        var target = CreateTarget();
        var gate = new PreparationGate();
        var tour = new DaisyTour();
        tour.Steps.Add(new DaisyTourStep { Id = "pending", Target = target, PrepareAsync = gate.PrepareAsync });
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await LoadAsync(root);
            var starting = tour.StartAsync(cancellation.Token);
            await gate.Entered.Task.WaitAsync(Timeout);
            var card = Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            await RenderFrameAsync(root);
            Assert.IsFalse(card.IsVisible, "Initial preparation must not show the card.");
            if (skip) tour.Skip();
            else cancellation.Cancel();
            Assert.IsTrue(gate.Token.IsCancellationRequested, "Closing must cancel the pending preparation.");
            AssertClosed(tour);
            gate.Release.TrySetResult();
            await starting.WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(reason, closed[0].Reason);
            Assert.AreEqual("pending", closed[0].StepId);
            Assert.IsFalse(OpenSurfaces(root).Any(surface =>
                Descendants<Border>(surface).Any(border => ReferenceEquals(border, card))), "The late completion must not show the card again.");
        }
        finally { gate.Release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_NextIsRepeatedDuringPreparation_ItDoesNotSkipTheDestination()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var entered = NewCompletion();
        var release = NewCompletion();
        var calls = 0;
        tour.Steps.Add(new DaisyTourStep { Id = "first", Target = target });
        var second = new DaisyTourStep
        {
            Id = "second", Target = target,
            PrepareAsync = async (_, _) => { calls++; entered.TrySetResult(); await release.Task; }
        };
        tour.Steps.Add(second);
        tour.Steps.Add(new DaisyTourStep { Id = "third", Target = target });
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var navigation = tour.NextAsync();
            await entered.Task.WaitAsync(Timeout);
            Assert.IsTrue(tour.IsTransitioning);
            var repeatedNavigation = Task.WhenAll(tour.NextAsync(), tour.NextAsync());
            release.TrySetResult();
            await navigation.WaitAsync(Timeout);
            await repeatedNavigation.WaitAsync(Timeout);
            AssertActive(tour, second, 1);
            Assert.AreEqual(1, calls);
        }
        finally { release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_ReplayedDuringPreparation_OldCompletionCannotReplaceTheNewSession()
    {
        var target = CreateTarget();
        var entered = NewCompletion();
        var release = NewCompletion();
        var calls = 0;
        var tour = new DaisyTour();
        var step = new DaisyTourStep
        {
            Id = "replay", Target = target,
            PrepareAsync = async (_, _) =>
            {
                if (++calls == 1) { entered.TrySetResult(); await release.Task; }
            }
        };
        tour.Steps.Add(step);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            var oldStarting = tour.StartAsync();
            await entered.Task.WaitAsync(Timeout);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            release.TrySetResult();
            await oldStarting.WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            Assert.AreEqual(2, calls);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[0].Reason);
        }
        finally { release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_CallerCancelsOrHostUnloads_TheActiveSessionClosesOnce()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        tour.Steps.Add(new DaisyTourStep { Id = "cancel", Target = target });
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        var root = CreateRoot(tour, target);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await LoadAsync(root);
            await tour.StartAsync(cancellation.Token).WaitAsync(Timeout);
            cancellation.Cancel();
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[0].Reason);

            await tour.StartAsync().WaitAsync(Timeout);
            root.Children.Remove(tour);
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(2, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[1].Reason);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_StepsAreEmptyOrIdsAreInvalid_ValidationDoesNotOpenAnOverlay()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertClosed(tour);
            tour.Steps.Add(new DaisyTourStep { Id = "duplicate", Target = target });
            tour.Steps.Add(new DaisyTourStep { Id = "duplicate", Target = target });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => tour.StartAsync());
            AssertClosed(tour);
            tour.Steps.Clear();
            tour.Steps.Add(new DaisyTourStep { Id = string.Empty, Target = target });
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => tour.StartAsync());
            AssertClosed(tour);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_TargetIsUnavailableOrPreparationFails_TheStepStaysOpenWithAWarningThatIdentifiesIt()
    {
        var visible = CreateTarget();
        var detached = CreateTarget();
        var hidden = CreateTarget();
        hidden.IsVisible = false;
        var foreign = CreateTarget();
        var foreignRoot = new Grid { Children = { foreign } };
        var foreignWindow = new Window { Content = foreignRoot, Width = 320, Height = 200 };
        var tour = new DaisyTour();
        var root = CreateRoot(tour, visible, hidden);
        var closed = new List<DaisyTourClosedEventArgs>();
        var unavailable = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        tour.StepTargetUnavailable += (_, args) => unavailable.Add(args);
        Func<DaisyTourStep, CancellationToken, Task> failing = (_, _) => throw new InvalidOperationException("Preparation failed");
        var cases = new (string Id, Control? Target, Func<DaisyTourStep, CancellationToken, Task>? PrepareAsync, DaisyTourTargetProblem Problem, string Error)[]
        {
            ("missing", null, null, DaisyTourTargetProblem.Target, "unavailable"),
            ("detached", detached, null, DaisyTourTargetProblem.Target, "unavailable"),
            ("hidden", hidden, null, DaisyTourTargetProblem.Target, "unavailable"),
            ("foreign", foreign, null, DaisyTourTargetProblem.Target, "unavailable"),
            ("failed-prepare", visible, failing, DaisyTourTargetProblem.Preparation, "Preparation failed")
        };
        try
        {
            foreignWindow.Show();
            await RuntimeTestHelpers.EnsureLoadedAsync(foreignRoot).WaitAsync(Timeout);
            await LoadAsync(root);
            Assert.AreNotSame(TopLevel.GetTopLevel(root), TopLevel.GetTopLevel(foreign), "The foreign target must belong to another window.");
            foreach (var (id, target, prepare, problem, error) in cases)
            {
                await RuntimeTestHelpers.RunCaseAsync(id, async () =>
                {
                    tour.Steps.Clear();
                    var step = new DaisyTourStep { Id = id, Target = target, PrepareAsync = prepare };
                    tour.Steps.Add(step);
                    await tour.StartAsync().WaitAsync(Timeout);
                    await ArrangeAsync(root);
                    AssertActive(tour, step, 0, problem);
                    AssertWarning(tour, problem);
                    Assert.AreEqual(id, unavailable[^1].StepId);
                    Assert.AreEqual(problem, unavailable[^1].Problem);
                    Assert.AreSame(target, unavailable[^1].Target);
                    StringAssert.Contains(unavailable[^1].Error.Message, error);
                    // The card waits inside the window instead of vanishing; without a target to stand beside, in the middle.
                    var surface = GetSurface(tour);
                    var card = tour.CardBounds;
                    Assert.IsTrue(card.Width > 0 && card.Left >= 0 && card.Top >= 0 && card.Right <= surface.Bounds.Width && card.Bottom <= surface.Bounds.Height,
                        $"The card {card} must stay in the window.");
                    if (problem == DaisyTourTargetProblem.Target)
                    {
                        Assert.AreEqual(surface.Bounds.Width / 2, card.X + card.Width / 2, 1, "The card is centered horizontally.");
                        Assert.AreEqual(surface.Bounds.Height / 2, card.Y + card.Height / 2, 1, "The card is centered vertically.");
                    }
                    tour.Skip();
                    Assert.AreEqual(DaisyTourCloseReason.Skipped, closed[^1].Reason);
                });
            }
            Assert.HasCount(cases.Length, unavailable, "Each case reports its cause once.");
            Assert.HasCount(cases.Length, closed);
        }
        finally
        {
            foreignWindow.Close();
            RuntimeTestContext.Window?.Activate();
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_TheStepTargetLeavesTheVisibleArea_TheStepShowsAWarningUntilTheTargetReturns()
    {
        var target = CreateTarget();
        var step = new DaisyTourStep { Id = "outside", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        var reports = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.StepTargetUnavailable += (_, args) => reports.Add(args);
        var previousCulture = FloweryLocalization.CurrentCulture.Name;
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertWarning(tour, null);
            // No scroll area can bring the target back: it moves far below the window.
            target.Margin = new Thickness(24, 5000, 0, 0);
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is not null, "the warning shows");
            await ArrangeAsync(root);
            AssertActive(tour, step, 0, DaisyTourTargetProblem.Target);
            AssertWarning(tour, DaisyTourTargetProblem.Target);
            Assert.HasCount(1, reports);
            Assert.AreEqual("outside", reports[0].StepId);
            Assert.AreEqual(DaisyTourTargetProblem.Target, reports[0].Problem);
            Assert.AreSame(target, reports[0].Target);
            // Screen readers read the note first, and it follows the language.
            FloweryLocalization.SetCulture("de");
            await ArrangeAsync(root);
            AssertWarning(tour, DaisyTourTargetProblem.Target);
            StringAssert.StartsWith(AutomationProperties.GetName(Card(tour)), FloweryLocalization.GetStringInternal("Tour_TargetUnavailable"));
            FloweryLocalization.SetCulture(previousCulture);
            target.Margin = new Thickness(24);
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is null, "the target returns");
            await ArrangeAsync(root);
            AssertActive(tour, step, 0);
            AssertWarning(tour, null);
            AssertTargetBounds(tour, target);
            Assert.HasCount(1, reports, "The returning target raises no further report.");
        }
        finally
        {
            FloweryLocalization.SetCulture(previousCulture);
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_TargetProblemsRepeatOrTheVisitChanges_EachCauseIsReportedOncePerVisit()
    {
        var target = CreateTarget();
        var first = CreateTarget();
        var second = CreateTarget();
        first.Margin = new Thickness(220, 24, 0, 0);
        second.Margin = new Thickness(400, 24, 0, 0);
        var connections = new DaisyTourStep { Id = "connections", Target = target };
        connections.Annotations.Add(new DaisyTourAnnotation { Id = "first", Source = target, Target = first });
        connections.Annotations.Add(new DaisyTourAnnotation { Id = "second", Source = target, Target = second });
        var other = new DaisyTourStep { Id = "other", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(connections);
        tour.Steps.Add(other);
        var root = CreateRoot(tour, target, first, second);
        var reports = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.StepTargetUnavailable += (_, args) => reports.Add(args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, connections, 0);
            // Both ends leave at once, and each is reported.
            first.IsVisible = second.IsVisible = false;
            await RuntimeTestHelpers.WaitUntilAsync(() => reports.Count >= 2, "both ends are reported");
            await ArrangeAsync(root);
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, reports.Select(report => report.AnnotationId).ToArray());
            Assert.IsTrue(reports.All(report => report.Problem == DaisyTourTargetProblem.Annotation), "Both are connector ends.");
            AssertActive(tour, connections, 0, DaisyTourTargetProblem.Annotation);
            // An end that returns and leaves again within the visit is not reported again.
            first.IsVisible = second.IsVisible = true;
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is null, "the ends return");
            first.IsVisible = false;
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is not null, "the first end leaves again");
            await ArrangeAsync(root);
            Assert.HasCount(2, reports, "A cause is reported once per visit.");
            // Another visit of the step reports it again.
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, other, 1);
            await tour.BackAsync().WaitAsync(Timeout);
            await RuntimeTestHelpers.WaitUntilAsync(() => reports.Count == 3, "the new visit reports the end");
            Assert.AreEqual("first", reports[^1].AnnotationId);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AReportHandlerSkipsMovesOnReplaysOrThrows_TheTourStaysConsistent()
    {
        // Skipping from the handler closes the tour without leftovers.
        await RuntimeTestHelpers.RunCaseAsync("Skip", () => WithReportedTourAsync(static (tour, _) => tour.Skip(), async (tour, _, _, closed) =>
        {
            await RuntimeTestHelpers.WaitUntilAsync(() => !tour.IsOpen, "the handler skips");
            AssertClosed(tour);
            Assert.AreEqual(DaisyTourCloseReason.Skipped, closed.Single().Reason);
            Assert.AreEqual(default, tour.TargetBounds);
            Assert.AreEqual(default, tour.CardBounds);
        }));
        // Moving on from the handler works, because the report waits until the step is settled.
        await RuntimeTestHelpers.RunCaseAsync("Next", () => WithReportedTourAsync(static (tour, _) => tour.NextAsync(), async (tour, _, next, _) =>
        {
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.CurrentIndex == 1 && !tour.IsTransitioning, "the handler moves on");
            AssertActive(tour, next, 1);
        }));
        // Replaying from the handler starts a clean session, which reports its own visit.
        var replays = 0;
        await RuntimeTestHelpers.RunCaseAsync("Replay", () => WithReportedTourAsync((tour, report) => { if (replays++ == 0) _ = tour.StartAsync(); },
            async (tour, missing, _, closed) =>
            {
                await RuntimeTestHelpers.WaitUntilAsync(() => replays == 2 && !tour.IsTransitioning, "the replay reports its visit");
                AssertActive(tour, missing, 0, DaisyTourTargetProblem.Target);
                Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed.Single().Reason);
            }));
        // A handler that throws is logged; the tour keeps its warning.
        var thrown = 0;
        await RuntimeTestHelpers.RunCaseAsync("Throw", () => WithReportedTourAsync((_, _) => { thrown++; throw new InvalidOperationException("Handler failed"); },
            async (tour, missing, _, closed) =>
            {
                await RuntimeTestHelpers.WaitUntilAsync(() => thrown == 1, "the handler throws");
                await ArrangeAsync(tour);
                AssertActive(tour, missing, 0, DaisyTourTargetProblem.Target);
                Assert.IsEmpty(closed);
            }));
    }

    // A tour whose first step has no target, so it is reported at once, followed by a step with a target.
    private static async Task WithReportedTourAsync(Action<DaisyTour, DaisyTourStepTargetUnavailableEventArgs> handler,
        Func<DaisyTour, DaisyTourStep, DaisyTourStep, List<DaisyTourClosedEventArgs>, Task> verify)
    {
        var target = CreateTarget();
        var missing = new DaisyTourStep { Id = "missing" };
        var next = new DaisyTourStep { Id = "next", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(missing);
        tour.Steps.Add(next);
        var root = CreateRoot(tour, target);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        tour.StepTargetUnavailable += (_, args) => handler(tour, args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            await verify(tour, missing, next, closed);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AStepWithoutTargetHasAFixedCardPosition_TheCardKeepsIt()
    {
        var step = new DaisyTourStep { Id = "bottom", CardPosition = DaisyTourCardPosition.Bottom };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            await ArrangeAsync(root);
            AssertActive(tour, step, 0, DaisyTourTargetProblem.Target);
            var surface = GetSurface(tour);
            var card = tour.CardBounds;
            Assert.AreEqual(surface.Bounds.Width / 2, card.X + card.Width / 2, 1, "The card is centered horizontally.");
            Assert.AreEqual(surface.Bounds.Height - 12, card.Bottom, 1, "The card keeps its bottom position.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_TheTargetOrAnAncestorTurnsTransparentOrCollapses_TheWarningShowsUntilItReturns()
    {
        var target = CreateTarget();
        var holder = new Grid { Children = { target } };
        var step = new DaisyTourStep { Id = "fading", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, holder);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            // Opacity and the visibility of an ancestor change no bounds of the target, so only the tour's own
            // availability check notices them.
            foreach (var (name, hide, show) in new (string, Action, Action)[]
            {
                ("target opacity", () => target.Opacity = 0, () => target.Opacity = 1),
                ("ancestor opacity", () => holder.Opacity = 0, () => holder.Opacity = 1),
                ("ancestor visibility", () => holder.IsVisible = false, () => holder.IsVisible = true)
            })
            {
                await RuntimeTestHelpers.RunCaseAsync(name, async () =>
                {
                    hide();
                    await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is not null, "the warning shows");
                    await ArrangeAsync(root);
                    AssertActive(tour, step, 0, DaisyTourTargetProblem.Target);
                    show();
                    await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is null, "the target returns");
                    await ArrangeAsync(root);
                    AssertActive(tour, step, 0);
                    AssertTargetBounds(tour, target);
                });
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AnnotationEndsAreMissing_EachMissingEndIsReportedOnceAndTheStepTargetOnlyAsTheTarget()
    {
        var target = CreateTarget();
        var lost = CreateTarget();
        var gone = CreateTarget();
        var away = CreateTarget();
        lost.Margin = new Thickness(220, 24, 0, 0);
        gone.Margin = new Thickness(220, 120, 0, 0);
        away.Margin = new Thickness(400, 120, 0, 0);
        // Two annotations without an id, one with both ends missing, and one that ends at the step target.
        var step = new DaisyTourStep { Id = "ends", Target = target };
        step.Annotations.Add(new DaisyTourAnnotation { Source = target, Target = lost });
        step.Annotations.Add(new DaisyTourAnnotation { Source = target, Target = gone });
        step.Annotations.Add(new DaisyTourAnnotation { Id = "both", Source = gone, Target = away });
        step.Annotations.Add(new DaisyTourAnnotation { Id = "to-target", Source = away, Target = target });
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target, lost, gone, away);
        var reports = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.StepTargetUnavailable += (_, args) => reports.Add(args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            lost.IsVisible = gone.IsVisible = away.IsVisible = target.IsVisible = false;
            await RuntimeTestHelpers.WaitUntilAsync(() => reports.Count >= 6, "every missing end is reported");
            await ArrangeAsync(root);
            AssertActive(tour, step, 0, DaisyTourTargetProblem.Target);
            // The step target once as the target; its connector ends are not reported again for it.
            Assert.HasCount(1, reports.Where(report => report.Problem == DaisyTourTargetProblem.Target).ToArray());
            var ends = reports.Where(report => report.Problem == DaisyTourTargetProblem.Annotation).Select(report => report.Target).ToArray();
            CollectionAssert.AreEquivalent(new Control[] { lost, gone, gone, away, away }, ends,
                "Each annotation reports each of its missing ends, also without an id.");
            Assert.IsFalse(reports.Any(report => report.Problem == DaisyTourTargetProblem.Annotation && ReferenceEquals(report.Target, target)),
                "An end that is the step target is reported as the target.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AHandlerMovesOnFromTheFirstOfTwoProblems_TheSecondIsNotReported()
    {
        var target = CreateTarget();
        var first = CreateTarget();
        var second = CreateTarget();
        first.IsVisible = second.IsVisible = false;
        var step = new DaisyTourStep { Id = "two", Target = target };
        step.Annotations.Add(new DaisyTourAnnotation { Id = "first", Source = target, Target = first });
        step.Annotations.Add(new DaisyTourAnnotation { Id = "second", Source = target, Target = second });
        var next = new DaisyTourStep { Id = "next", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        tour.Steps.Add(next);
        var root = CreateRoot(tour, target, first, second);
        var reports = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.StepTargetUnavailable += (_, args) =>
        {
            reports.Add(args);
            _ = tour.NextAsync();
        };
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.CurrentIndex == 1 && !tour.IsTransitioning, "the handler moves on");
            await ArrangeAsync(root);
            AssertActive(tour, next, 1);
            Assert.HasCount(1, reports, "The problem left behind belongs to a visit that is over.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_HighContrastIsOn_TheWarningUsesTheSystemColors()
    {
        var step = new DaisyTourStep { Id = "missing" };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour);
        // WinUI forces the Windows contrast flag; Flowery.NET has a public override for it.
        var original = DaisyAccessibility.HighContrastOverride;
        try
        {
            await LoadAsync(root);
            DaisyAccessibility.HighContrastOverride = true;
            await tour.StartAsync().WaitAsync(Timeout);
            await ArrangeAsync(root);
            AssertActive(tour, step, 0, DaisyTourTargetProblem.Target);
            AssertWarning(tour, DaisyTourTargetProblem.Target);
            var note = Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourWarning");
            var system = ((ISolidColorBrush)GetBorderBrush()).Color;
            Assert.AreEqual(Colors.Transparent, ((ISolidColorBrush)note.Background!).Color, "The note has no decorative fill.");
            Assert.AreEqual(system, ((ISolidColorBrush)note.BorderBrush!).Color, "The note frame uses the system border.");
            Assert.AreEqual(system, ((ISolidColorBrush)Card(tour).BorderBrush!).Color, "The card frame keeps the system border.");
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = original;
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public void When_NothingIsHighlighted_StationaryAndAvoidingPlacementsHaveNoTargetObstacle()
    {
        var card = new Size(200, 100);
        var viewport = new Size(600, 400);
        Assert.AreEqual(new Rect(200, 150, 200, 100), DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Center, card, viewport, null));
        Assert.AreEqual(new Rect(200, 288, 200, 100), DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Bottom, card, viewport, null));
        // Without a target, a card that must leave a course goes to the nearest clear spot from the center.
        var course = new Rect(150, 180, 300, 20);
        var placed = DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target, DaisyTourPlacement.Auto, null, card, viewport, [course]);
        Assert.IsNotNull(placed, "A clear spot exists.");
        // WinUI's Intersect keeps rectangles that only touch, so a card on the edge of the course fails there too.
        Assert.IsFalse(IntersectsOrTouches(placed.Value, course), $"The card {placed} must not cover the course.");
        Assert.IsTrue(placed.Value.Left >= 0 && placed.Value.Top >= 0 && placed.Value.Right <= viewport.Width && placed.Value.Bottom <= viewport.Height,
            $"The card {placed} stays in view.");
    }

    [Fact]
    public async Task When_ACoordinateRouteLimitLiesAboveBothEnds_TheCourseBridgesAboveItAndKeepsTheAreaBelowClear()
    {
        var start = CreateTarget();
        var end = CreateTarget();
        start.Margin = new Thickness(40, 280, 0, 0);
        end.Margin = new Thickness(440, 280, 0, 0);
        const double limit = 220;
        var step = new DaisyTourStep { Id = "bridge", Target = start, CardPosition = DaisyTourCardPosition.Top };
        step.Annotations.Add(new DaisyTourAnnotation
        {
            Id = "limited", Source = start, Target = end, Kind = DaisyTourAnnotationKind.Line,
            Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 4,
            RouteLimitCoordinate = limit, RouteLimitSide = DaisyTourRouteSide.Above
        });
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = new Grid { Width = 600, Height = 400, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        root.Children.Add(start);
        root.Children.Add(end);
        root.Children.Add(tour);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            await RenderFrameAsync(root);
            AssertActive(tour, step, 0);
            var source = BoundsInRoot(start);
            var target = BoundsInRoot(end);
            // The coordinate is in window coordinates, like the bounds.
            const double level = limit;
            // Below the limit and between both ends, the course must not cut across.
            var clear = new Rect(source.Right + 8, level + 8, target.Left - source.Right - 16, source.Bottom - level - 8);
            Assert.AreEqual(0, await CountSurfacePixelsAsync(tour, Colors.Red, clear), $"The area {clear} below the limit must stay clear.");
            var bridge = new Rect(source.Right, level - 40, target.Left - source.Right, 46);
            Assert.IsTrue(await CountSurfacePixelsAsync(tour, Colors.Red, bridge) >= 50, $"The course must bridge along the limit inside {bridge}.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_PreparationCreatesTheTarget_ItResolvesAfterNativeLayout()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var root = CreateRoot(tour);
        var step = new DaisyTourStep { Id = "deferred" };
        step.PrepareAsync = (_, _) =>
        {
            root.Children.Insert(0, target);
            step.Target = target;
            return Task.CompletedTask;
        };
        tour.Steps.Add(step);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertTargetBounds(tour, target);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_TargetIsInsideNestedScrollers_ItIsVisibleBeforeTheStepOpens()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(24, 700, 0, 0);
        var innerContent = new Grid { Height = 1000, Children = { target } };
        var inner = new ScrollViewer { Height = 200, Content = innerContent, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        inner.Margin = new Thickness(0, 550, 0, 0);
        inner.VerticalAlignment = VerticalAlignment.Top;
        var outerContent = new Grid { Height = 1000, Children = { inner } };
        var outer = new ScrollViewer { Content = outerContent, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "nested", Target = target };
        tour.Steps.Add(step);
        var root = CreateRoot(tour, outer);
        var views = new List<string>();
        var frames = 0;
        var counting = true;
        // WinUI counts CompositionTarget.Rendering; Avalonia asks the window for each next frame.
        void Rendering(TimeSpan _)
        {
            frames++;
            if (counting) RuntimeTestHelpers.GetWindow().RequestAnimationFrame(Rendering);
        }
        // Avalonia applies scroll offsets at once, so there are no intermediate views to tell apart.
        inner.ScrollChanged += (_, _) => views.Add($"inner={inner.Offset.Y}/frame={frames}");
        outer.ScrollChanged += (_, _) => views.Add($"outer={outer.Offset.Y}/frame={frames}");
        tour.Closed += (_, _) => views.Add($"closed: inner={inner.Offset.Y}, outer={outer.Offset.Y}, " +
            $"target={BoundsInRoot(target)}, hostVisible={TopLevel.GetTopLevel(root)?.IsVisible}, frame={frames}");
        RuntimeTestHelpers.GetWindow().RequestAnimationFrame(Rendering);
        try
        {
            await LoadAsync(root);
            var before = $"{DescribeScroll("inner", inner)}; {DescribeScroll("outer", outer)}; frame={frames}";
            await tour.StartAsync().WaitAsync(Timeout);
            Assert.IsTrue(tour.IsOpen, $"Before start: {before}. At closure: {DescribeScroll("inner", inner)}; " +
                $"{DescribeScroll("outer", outer)}. ScrollChanged: [{string.Join(", ", views)}]. Closure: {(tour.Tag as DaisyTourClosedEventArgs)?.Error?.Message}");
            AssertActive(tour, step, 0);
            Assert.IsTrue(inner.Offset.Y > 0, "The inner scroller must reveal the target.");
            Assert.IsTrue(outer.Offset.Y > 0, "The outer scroller must reveal the inner viewport.");
            AssertFits(target, inner);
            AssertFits(target, outer);
            AssertTargetBounds(tour, target);
        }
        finally
        {
            counting = false;
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_ScrollingRevealsATarget_ItsHighlightFrameIsVisibleAndStaysRevealedAfterAnotherScroll()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(24, 900, 0, 0);
        var viewer = new ScrollViewer { Content = new Grid { Height = 1200, Children = { target } } };
        var step = new DaisyTourStep { Id = "scrolled-away", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, viewer);
        var revealed = NewCompletion();
        var scrolledAway = false;
        // Avalonia applies scroll offsets at once, so every change is a final view.
        EventHandler<ScrollChangedEventArgs> viewChanged = (_, _) =>
        {
            if (!scrolledAway) scrolledAway = viewer.Offset.Y == 0;
            else if (viewer.Offset.Y > 0) revealed.TrySetResult();
        };
        tour.Closed += (_, args) => revealed.TrySetException(
            new AssertFailedException($"The tour closed: {args.Reason}, {args.Error?.Message}"));
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            AssertFrameInViewer(tour, target, viewer, "The highlight frame after the first reveal");
            viewer.ScrollChanged += viewChanged;
            // A page control bringing itself into view scrolls the target out of the viewport.
            Assert.IsTrue(ChangeView(viewer, null, 0));
            await revealed.Task.WaitAsync(Timeout);
            await ArrangeAsync(root);
            AssertActive(tour, step, 0);
            AssertFits(target, viewer);
            AssertTargetBounds(tour, target);
            AssertFrameInViewer(tour, target, viewer, "The highlight frame after the second reveal");
        }
        finally
        {
            viewer.ScrollChanged -= viewChanged;
            await CleanupAsync(root, tour);
        }

        // The viewer ends at the root's edge, so a frame outside it would be cut off.
        static void AssertFrameInViewer(DaisyTour tour, Control target, ScrollViewer viewer, string diagnostic)
        {
            var margin = tour.HighlightPadding + 2;
            var bounds = TransformBounds(target, viewer, new Rect(0, 0, target.Bounds.Width, target.Bounds.Height));
            var frame = new Rect(bounds.X - margin, bounds.Y - margin, bounds.Width + 2 * margin, bounds.Height + 2 * margin);
            AssertInViewport(frame, new Size(viewer.Bounds.Width, viewer.Bounds.Height), diagnostic);
        }
    }

    [Fact]
    public async Task When_ScrollViewerHasNotRenderedYet_PreparedBoundsIncludeTheNewOffset()
    {
        // Each pass starts before a new viewer's first frame, as after a tab switch. The first
        // pass can coincide with startup frames; later passes start from an idle frame loop.
        for (var pass = 0; pass < 3; pass++)
        {
            var target = CreateTarget();
            target.Margin = new Thickness(24, 700, 0, 0);
            var viewer = new ScrollViewer
            {
                Height = 200, VerticalAlignment = VerticalAlignment.Top, Content = new Grid { Height = 1000, Children = { target } }
            };
            var root = new Grid { Width = 600, Height = 400, Children = { viewer } };
            try
            {
                RuntimeTestHelpers.AttachToHost(root);
                // The tour measures directly in the preparation's continuation, without another frame.
                var prepared = DaisyTourLayout.PrepareTargetsAsync([target], root, CancellationToken.None).ContinueWith(
                    task =>
                    {
                        task.GetAwaiter().GetResult();
                        return (viewer.Offset.Y, target.TranslatePoint(new Point(), root)!.Value.Y);
                    },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var (offset, y) = await prepared.WaitAsync(Timeout);
                Assert.AreEqual(540, offset, 0.5, $"Pass {pass}");
                Assert.AreEqual(160, y, 0.5, $"Pass {pass}: prepared bounds must include the accepted scroll offset.");
            }
            finally
            {
                RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
                await ArrangeAsync(root);
            }
        }
    }

    [Fact]
    public async Task When_TargetReflows_TheHighlightTracksItsCurrentBounds()
    {
        var target = CreateTarget();
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "reflow", Target = target };
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var original = tour.TargetBounds;
            var updated = NewCompletion();
            EventHandler handler = (_, _) =>
            {
                if (tour.TargetBounds != original) updated.TrySetResult();
            };
            tour.GeometryUpdated += handler;
            try
            {
                target.Margin = new Thickness(220, 190, 0, 0);
                target.Width = 130;
                target.Height = 70;
                root.UpdateLayout();
                await updated.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            AssertActive(tour, step, 0);
            Assert.AreNotEqual(original, tour.TargetBounds);
            AssertTargetBounds(tour, target);
            Assert.IsTrue(tour.CardBounds.Width > 0 && tour.CardBounds.Height > 0);
            Assert.IsTrue(tour.CardBounds.X >= 0 && tour.CardBounds.Y >= 0);
            Assert.IsTrue(tour.CardBounds.Right <= RootSize(root).Width + 1);
            Assert.IsTrue(tour.CardBounds.Bottom <= RootSize(root).Height + 1);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AnnotationRenders_NativePixelsContainItsStrokeAndFill()
    {
        // Arrows have a filled head, lines only a stroke, and surfaces a stroked and filled body.
        var cases = new (DaisyTourAnnotationKind Kind, DaisyTourAnnotationPreset Preset, DaisyTourStrokeStyle StrokeStyle)[]
        {
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.ThinArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.ThickArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.BlockArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.CurvedArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Arrow, DaisyTourAnnotationPreset.TwoEndedArrow, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Solid),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Dotted),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Dashed),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.DashDot),
            (DaisyTourAnnotationKind.Line, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Custom),
            (DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.SpeechBubble, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Default),
            (DaisyTourAnnotationKind.Custom, DaisyTourAnnotationPreset.Default, DaisyTourStrokeStyle.Default)
        };
        foreach (var (kind, preset, strokeStyle) in cases)
            await RuntimeTestHelpers.RunCaseAsync($"{kind}/{preset}/{strokeStyle}", () => VerifyAnnotationPixelsAsync(kind, preset, strokeStyle));
    }

    [Fact]
    public async Task When_TourOverlayShowsAnArrow_TheOpenPopupRendersItsStroke()
    {
        var source = CreateTarget();
        var target = CreateTarget();
        source.Margin = new Thickness(40, 120, 0, 0);
        target.Margin = new Thickness(420, 120, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "overlay-arrow", Source = source, Target = target, Preset = DaisyTourAnnotationPreset.ThinArrow,
            StrokeThickness = 3, Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red)
        };
        var step = new DaisyTourStep { Id = "overlay", Target = source, Placement = DaisyTourPlacement.Bottom };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, source, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            await RenderFrameAsync(root);
            var surface = GetSurface(tour);
            var bitmap = RenderBitmap(surface);
            var pixels = bitmap.Pixels;
            var sourceBounds = BoundsInRoot(source);
            var targetBounds = BoundsInRoot(target);
            var scaleX = bitmap.PixelWidth / surface.Bounds.Width;
            var scaleY = bitmap.PixelHeight / surface.Bounds.Height;
            var red = 0;
            var top = (int)((sourceBounds.Top + 10) * scaleY);
            var bottom = (int)((sourceBounds.Bottom - 10) * scaleY);
            for (var y = top; y < bottom; y++)
            {
                for (var x = (int)((sourceBounds.Right + 20) * scaleX); x < (int)((targetBounds.Left - 40) * scaleX); x++)
                {
                    var offset = (y * bitmap.PixelWidth + x) * 4;
                    if (pixels[offset + 2] >= 200 && pixels[offset + 1] <= 60 && pixels[offset] <= 60) red++;
                }
            }
            var presenter = Descendants<DaisyTourAnnotationPresenter>(surface).SingleOrDefault();
            var layers = string.Join("; ", ((Canvas)surface).Children.OfType<Control>().Select(child =>
                $"{child.GetType().Name}: {child.Bounds.Width}x{child.Bounds.Height}, vis={child.IsVisible}, opacity={child.Opacity}, children={(child as Panel)?.Children.Count}"));
            var paths = presenter is null ? "no presenter" : string.Join("; ", presenter.Children.OfType<Path>().Select((path, index) =>
                $"Path{index}: actual={path.Bounds.Width}x{path.Bounds.Height}, size={path.Width}x{path.Height}, data={path.Data?.Bounds}, stroke={DescribeBrush(path.Stroke)}, loaded={path.IsLoaded}"));
            // The gap between each control and its highlight frame stays free of the connector.
            var gapRed = 0;
            foreach (var gapStart in new[] { sourceBounds.Right + 0.5, targetBounds.Left - tour.HighlightPadding + 0.5 })
            {
                for (var y = (int)((sourceBounds.Top + sourceBounds.Height / 2 - 4) * scaleY); y < (int)((sourceBounds.Top + sourceBounds.Height / 2 + 4) * scaleY); y++)
                {
                    for (var x = (int)Math.Ceiling(gapStart * scaleX); x < (int)((gapStart + tour.HighlightPadding - 1.5) * scaleX); x++)
                    {
                        var offset = (y * bitmap.PixelWidth + x) * 4;
                        if (pixels[offset + 2] >= 120 && pixels[offset + 1] <= 60 && pixels[offset] <= 60) gapRed++;
                    }
                }
            }
            Assert.AreEqual(0, gapRed, "Connector pixels must not cross the highlight frame into the control gap.");
            Assert.IsTrue(red >= 100, $"Red overlay stroke pixels={red}, bitmap={bitmap.PixelWidth}x{bitmap.PixelHeight}, surface={surface.Bounds.Width}x{surface.Bounds.Height}, " +
                $"presenter={presenter?.Bounds.Width}x{presenter?.Bounds.Height} size={presenter?.Width}x{presenter?.Height} opacity={presenter?.Opacity} loaded={presenter?.IsLoaded}. Layers: {layers}. {paths}");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_ArrowHasNoFill_ItsHeadIsSolidInTheStrokeColor()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        try
        {
            await LoadAsync(root);
            foreach (var preset in new[] { DaisyTourAnnotationPreset.ThinArrow, DaisyTourAnnotationPreset.TwoEndedArrow })
            {
                var annotation = new DaisyTourAnnotation
                {
                    Preset = preset, SourceAnchor = DaisyTourAnchor.Right, TargetAnchor = DaisyTourAnchor.Left, HeadLength = 40, HeadWidth = 40
                };
                // The default fill is the surface color; an arrowhead must not use it.
                presenter.Update(annotation, new Rect(60, 180, 80, 40), new Rect(460, 180, 80, 40),
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Blue), new SolidColorBrush(Colors.White));
                await ArrangeAsync(root);
                var bitmap = RenderBitmap(root);
                var pixels = bitmap.Pixels;
                foreach (var head in new[] { (Path)presenter.Children[2], (Path)presenter.Children[3] }.Where(head => head.Data is not null))
                {
                    var bounds = head.Data!.Bounds;
                    var inner = new Rect(bounds.X + bounds.Width / 2 - 3, bounds.Y + bounds.Height / 2 - 3, 6, 6);
                    Assert.IsTrue(CountRenderedColor(pixels, bitmap, inner, Colors.Red) >= 20, $"{preset}: head interior at {inner} must be stroke-colored.");
                    Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, bounds, Colors.Blue), $"{preset}: heads must not use the surface fill.");
                }
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [Fact]
    public async Task When_RouteLimitIsSet_TheCourseKeepsTheAreaBetweenBothEndsClear()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        // Every pair is offset across the limit, so a direct line would cross the area kept clear.
        var cases = new (string Name, DaisyTourAnnotationPath Path, (Rect Source, Rect Target) Ends, DaisyTourRouteSide Side, double Limit, Rect Clear, Rect Allowed)[]
        {
            // Both ends beyond: legs of different lengths rise to the limit (uneven ⊓).
            ("Above, both beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 220, 100, 40), new Rect(460, 300, 100, 40)),
                DaisyTourRouteSide.Above, 180, new(150, 184, 300, 216), new(150, 0, 300, 180)),
            ("Above, both beyond, curve", DaisyTourAnnotationPath.SCurve, (new Rect(40, 220, 100, 40), new Rect(460, 300, 100, 40)),
                DaisyTourRouteSide.Above, 180, new(150, 184, 300, 216), new(150, 0, 300, 180)),
            // One end beyond: the course runs at the other end's height, then turns down (┐).
            ("Above, one beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 40, 100, 40), new Rect(460, 300, 100, 40)),
                DaisyTourRouteSide.Above, 180, new(150, 184, 300, 216), new(150, 0, 300, 180)),
            ("Below, both beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 140, 100, 40), new Rect(460, 60, 100, 40)),
                DaisyTourRouteSide.Below, 220, new(150, 0, 300, 216), new(150, 220, 300, 180)),
            ("Below, one beyond", DaisyTourAnnotationPath.Elbow, (new Rect(40, 300, 100, 40), new Rect(460, 60, 100, 40)),
                DaisyTourRouteSide.Below, 220, new(150, 0, 300, 216), new(150, 220, 300, 180)),
            ("LeftOf, both beyond", DaisyTourAnnotationPath.Elbow, (new Rect(260, 20, 100, 40), new Rect(340, 330, 100, 40)),
                DaisyTourRouteSide.LeftOf, 220, new(224, 65, 376, 260), new(0, 65, 220, 260)),
            ("RightOf, one beyond", DaisyTourAnnotationPath.Elbow, (new Rect(60, 20, 100, 40), new Rect(460, 330, 100, 40)),
                DaisyTourRouteSide.RightOf, 420, new(0, 65, 416, 260), new(420, 65, 180, 260)),
            // Both ends allowed: the curve keeps its shape but may not bend across the limit.
            ("Curve inside", DaisyTourAnnotationPath.Curved, (new Rect(40, 60, 100, 40), new Rect(460, 90, 100, 40)),
                DaisyTourRouteSide.Above, 150, new(150, 154, 300, 246), new(150, 0, 300, 150))
        };
        try
        {
            await LoadAsync(root);
            foreach (var (name, path, ends, side, limit, clear, allowed) in cases)
            {
                var annotation = new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = path, StrokeThickness = 4, CornerRadius = 10, Curvature = 0.6 };
                presenter.Update(annotation, ends.Source, ends.Target,
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White),
                    routeLimit: new DaisyTourRouteLimit(side, limit));
                await ArrangeAsync(root);
                var bitmap = RenderBitmap(root);
                var pixels = bitmap.Pixels;
                Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, clear, Colors.Red), $"{name}: the course must keep {clear} clear.");
                Assert.IsTrue(CountRenderedColor(pixels, bitmap, allowed, Colors.Red) >= 50, $"{name}: the course must cross on the allowed side.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [Fact]
    public async Task When_RouteLimitControlMoves_TheRoutedCourseFollowsIt()
    {
        // Source top left, target bottom right, obstacle between: a direct line would cross it.
        var source = CreateTarget();
        source.Margin = new Thickness(24, 40, 0, 0);
        var target = CreateTarget();
        target.Margin = new Thickness(476, 300, 0, 0);
        var obstacle = new Border
        {
            Width = 120, Height = 60, Margin = new Thickness(240, 170, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var annotation = new DaisyTourAnnotation
        {
            Id = "around", Source = source, Target = target, Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Elbow,
            RouteLimit = obstacle, RouteLimitSide = DaisyTourRouteSide.Above, Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 4
        };
        var step = new DaisyTourStep { Id = "around", Target = source, CardPosition = DaisyTourCardPosition.BottomLeft };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, source, target, obstacle);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            await AssertClearAsync("initial");
            var published = NewCompletion();
            EventHandler handler = (_, _) => published.TrySetResult();
            tour.GeometryUpdated += handler;
            try
            {
                // Now both ends lie beyond the limit, so the course turns into a bracket above both.
                obstacle.Margin = new Thickness(240, 20, 0, 0);
                root.UpdateLayout();
                await published.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            AssertActive(tour, step, 0);
            await AssertClearAsync("moved");

            async Task AssertClearAsync(string phase)
            {
                await RenderFrameAsync(root);
                var area = BoundsInRoot(obstacle);
                // The limit lies 12 DIP above the control; the line keeps the control and that margin clear.
                var clear = new Rect(area.X, area.Y - 8, area.Width, area.Height + 8);
                Assert.AreEqual(0, await CountSurfacePixelsAsync(tour, Colors.Red, clear), $"{phase}: the course must stay above {clear}.");
                Assert.IsTrue(await CountConnectorPixelsAsync(tour, Colors.Red, BoundsInRoot(source), BoundsInRoot(target)) >= 100, phase);
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_ConnectorBendsBetweenItsAnchors_ItDocksAtTheFacingSidesAndDoesNotCrossItsEndpoints()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var wave = new PathGeometry();
        // An Avalonia figure is closed by default; a WinUI figure, like this course, is open.
        var waveFigure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = false };
        waveFigure.Segments!.Add(new BezierSegment { Point1 = new Point(20, -30), Point2 = new Point(80, 30), Point3 = new Point(100, 0) });
        wave.Figures!.Add(waveFigure);
        // Arrows to a wide control below and right: the horizontal distance dominates, so the course starts at the
        // source's right side and ends at the target's left side.
        var cases = new List<(string Name, DaisyTourAnnotation Annotation, Rect Source, Rect Target, (Point Start, Point Tip)? Docks)>();
        foreach (var path in new[]
        {
            DaisyTourAnnotationPath.Straight, DaisyTourAnnotationPath.Elbow, DaisyTourAnnotationPath.Curved,
            DaisyTourAnnotationPath.SCurve, DaisyTourAnnotationPath.Bezier, DaisyTourAnnotationPath.Custom
        })
        {
            cases.Add(($"{path} arrow to a wide target", new DaisyTourAnnotation
            {
                Kind = DaisyTourAnnotationKind.Arrow, Path = path,
                ControlPoint1X = 0.25, ControlPoint1Y = -0.25, ControlPoint2X = 0.75, ControlPoint2Y = 0.25,
                Geometry = path == DaisyTourAnnotationPath.Custom ? wave : null
            }, new Rect(60, 40, 80, 40), new Rect(400, 300, 175, 28), (new Point(140, 60), new Point(400, 314))));
        }
        // Lines that bend away from their anchors.
        cases.Add(("Bezier line", new DaisyTourAnnotation
        {
            Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Bezier,
            ControlPoint1X = 0.2, ControlPoint1Y = -0.6, ControlPoint2X = 0.8, ControlPoint2Y = 0.6
        }, new Rect(80, 60, 120, 40), new Rect(400, 280, 120, 40), null));
        cases.Add(("Curved line", new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Curved, Curvature = -0.6 },
            new Rect(80, 60, 120, 40), new Rect(400, 280, 120, 40), null));
        cases.Add(("Elbow line", new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Elbow },
            new Rect(40, 60, 320, 40), new Rect(440, 300, 80, 40), null));
        try
        {
            await LoadAsync(root);
            foreach (var (name, annotation, source, target, docks) in cases)
            {
                await RuntimeTestHelpers.RunCaseAsync(name, async () =>
                {
                    annotation.StrokeThickness = 4;
                    presenter.Update(annotation, source, target,
                        new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White));
                    await ArrangeAsync(root);
                    if (docks is { } dock)
                    {
                        // The arrowhead's tip is the course's end; a custom course is drawn through a geometry transform.
                        var tip = ((PathGeometry)((Path)presenter.Children[3]).Data!).Figures!.Single().StartPoint;
                        Assert.AreEqual(dock.Tip.X, tip.X, 0.001, $"{name}: end must dock at the target's left side");
                        Assert.AreEqual(dock.Tip.Y, tip.Y, 0.001, $"{name}: end must dock at the target's left side");
                        if (((Path)presenter.Children[1]).Data is PathGeometry body)
                            Assert.AreEqual(dock.Start, body.Figures!.Single().StartPoint, $"{name}: start must dock at the source's right side");
                    }
                    var bitmap = RenderBitmap(root);
                    var pixels = bitmap.Pixels;
                    Assert.IsTrue(CountRenderedColor(pixels, bitmap, new Rect(0, 0, 600, 400), Colors.Red) >= 100, $"{name}: the connector must render.");
                    foreach (var (part, rect) in new[] { ("source", source), ("target", target) })
                    {
                        var inside = new Rect(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
                        Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, inside, Colors.Red), $"{name}: the connector must not cross its {part} element.");
                    }
                });
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [Fact]
    public async Task When_ConnectorPathsRender_NativePixelsFollowTheirCourse()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var source = new Rect(60, 60, 80, 40);
        var target = new Rect(440, 280, 80, 40);
        var start = new Point(140, 80);
        var end = new Point(440, 300);
        var length = Math.Sqrt(300 * 300 + 220 * 220);
        var along = new Point(300 / length, 220 / length);
        var across = new Point(-along.Y, along.X);
        Point Connector(double x, double y) =>
            new(start.X + (along.X * x + across.X * y) * length, start.Y + (along.Y * x + across.Y * y) * length);
        Point Bezier(Point a, Point b, Point c, Point d, double t)
        {
            var u = 1 - t;
            return new(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
        }
        var hump = new PathGeometry();
        // An Avalonia figure is closed by default; a WinUI figure, like this course, is open.
        var humpFigure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = false };
        humpFigure.Segments!.Add(new LineSegment { Point = new Point(50, -40) });
        humpFigure.Segments.Add(new LineSegment { Point = new Point(100, 0) });
        hump.Figures!.Add(humpFigure);
        // SCurve controls reach out by half the distance along each edge normal (here: horizontal).
        var scurveFirst = new Point(start.X + 300 * 0.5, start.Y);
        var scurveSecond = new Point(end.X - 300 * 0.5, end.Y);
        var cases = new (string Name, DaisyTourAnnotation Annotation, Point[] Drawn, Point[] Empty)[]
        {
            ("SCurve", new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.SCurve },
                [Bezier(start, scurveFirst, scurveSecond, end, 0.25), Bezier(start, scurveFirst, scurveSecond, end, 0.5),
                 Bezier(start, scurveFirst, scurveSecond, end, 0.75)], []),
            ("Bezier", new DaisyTourAnnotation
            {
                Path = DaisyTourAnnotationPath.Bezier, ControlPoint1X = 0.2, ControlPoint1Y = -0.6, ControlPoint2X = 0.8, ControlPoint2Y = 0.6
            },
                [Bezier(start, Connector(0.2, -0.6), Connector(0.8, 0.6), end, 0.25), Bezier(start, Connector(0.2, -0.6), Connector(0.8, 0.6), end, 0.75)],
                [Connector(0.25, 0)]),
            // Elbow corners at (290,80) and (290,300); radius 30 rounds them off.
            ("Elbow", new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.Elbow, CornerRadius = 30 },
                [new Point(200, 80), new Point(260 + 30 * Math.Sin(Math.PI / 4), 110 - 30 * Math.Cos(Math.PI / 4)), new Point(290, 190)],
                [new Point(290, 80), new Point(290, 300)]),
            // The hump's ends meet the anchors; its apex lies 40 units beside the straight line.
            ("Custom", new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.Custom, Geometry = hump },
                [Connector(0.5, -0.4)], [Connector(0.5, 0)])
        };
        try
        {
            await LoadAsync(root);
            foreach (var (name, annotation, drawn, empty) in cases)
            {
                annotation.Kind = DaisyTourAnnotationKind.Line;
                annotation.StrokeThickness = 6;
                annotation.SourceAnchor = DaisyTourAnchor.Right;
                annotation.TargetAnchor = DaisyTourAnchor.Left;
                presenter.Update(annotation, source, target,
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White));
                await ArrangeAsync(root);
                var bitmap = RenderBitmap(root);
                var pixels = bitmap.Pixels;
                var body = (Path)presenter.Children[1];
                var transform = (body.RenderTransform as MatrixTransform)?.Matrix.ToString() ?? "none";
                foreach (var point in drawn)
                    Assert.IsTrue(CountRenderedColor(pixels, bitmap, new Rect(point.X - 2, point.Y - 2, 4, 4), Colors.Red) >= 4,
                        $"{name}: the course must pass {point}. Transform={transform}, thickness={body.StrokeThickness}, data={body.Data?.Bounds}.");
                foreach (var point in empty)
                    Assert.AreEqual(0, CountRenderedColor(pixels, bitmap, new Rect(point.X - 1, point.Y - 1, 2, 2), Colors.Red),
                        $"{name}: the course must not pass {point}.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [Fact]
    public async Task When_SurfaceTailPointsAtTheTarget_ItEndsAtTheHighlightFrame()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(250, 260, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "tail", Text = "Tail", Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Red)
        };
        var step = new DaisyTourStep { Id = "tail", Target = target, Placement = DaisyTourPlacement.Top };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            foreach (var (kind, tail) in new[]
            {
                (DaisyTourAnnotationKind.SpeechBubble, DaisyTourAnnotationTail.Speech), (DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationTail.Thought)
            })
            {
                var published = NewCompletion();
                EventHandler handler = (_, _) => published.TrySetResult();
                tour.GeometryUpdated += handler;
                try
                {
                    annotation.Kind = kind;
                    annotation.Tail = tail;
                    await published.Task.WaitAsync(Timeout);
                }
                finally { tour.GeometryUpdated -= handler; }
                AssertActive(tour, step, 0);
                await RenderFrameAsync(root);
                var surface = GetSurface(tour);
                var bitmap = RenderBitmap(surface);
                var pixels = bitmap.Pixels;
                var scale = bitmap.PixelWidth / surface.Bounds.Width;
                var bounds = BoundsInRoot(target);
                Rect Grow(double margin) => new(bounds.X - margin, bounds.Y - margin, bounds.Width + 2 * margin, bounds.Height + 2 * margin);
                // The gap between the control and the inner edge of its highlight frame.
                var (inner, outer) = (Grow(0.5), Grow(tour.HighlightPadding - 1.5));
                var crossing = 0;
                for (var y = (int)(outer.Top * scale); y < (int)Math.Ceiling(outer.Bottom * scale); y++)
                {
                    for (var x = (int)(outer.Left * scale); x < (int)Math.Ceiling(outer.Right * scale); x++)
                    {
                        var point = new Point((x + 0.5) / scale, (y + 0.5) / scale);
                        var offset = (y * bitmap.PixelWidth + x) * 4;
                        if (outer.Contains(point) && !inner.Contains(point) &&
                            pixels[offset + 2] >= 120 && pixels[offset + 1] <= 60 && pixels[offset] <= 60)
                            crossing++;
                    }
                }
                Assert.AreEqual(0, crossing, $"{kind}/{tail}: the tail must end at the highlight frame.");
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public void When_ConnectorBoundsChange_RetainedPathsFollowBothAnchorsAndStyleOverrides()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var pattern = new DaisyTourDashCollection { 2, 3, 1, 3 };
        var annotation = new DaisyTourAnnotation
        {
            Preset = DaisyTourAnnotationPreset.TwoEndedArrow,
            SourceAnchor = DaisyTourAnchor.Right,
            TargetAnchor = DaisyTourAnchor.Left,
            StrokeThickness = 7,
            DashArray = pattern,
            DashOffset = 4,
            Opacity = 0.6
        };
        var hostDefaults = new DaisyTourAnnotationStyle { StrokeThickness = 1 };
        var stepDefaults = new DaisyTourAnnotationStyle { StrokeThickness = 3 };
        var stroke = new SolidColorBrush(Colors.Blue);
        var fill = new SolidColorBrush(Colors.Orange);
        presenter.Update(annotation, new Rect(10, 20, 100, 40), new Rect(300, 100, 80, 60),
            stroke, fill, stroke, hostDefaults, stepDefaults);
        var path = (Path)presenter.Children[1];
        AssertEndpoints(path, new Point(110, 40), new Point(300, 130));
        Assert.AreSame(stroke, path.Stroke);
        Assert.AreEqual(7d, path.StrokeThickness);
        Assert.AreEqual(4d, path.StrokeDashOffset);
        Assert.AreEqual(0.6, presenter.Opacity, "The annotation's own opacity applies.");
        CollectionAssert.AreEqual(new[] { 2d, 3d, 1d, 3d }, path.StrokeDashArray!.ToArray());
        Assert.IsNotNull(((Path)presenter.Children[2]).Data);
        Assert.IsNotNull(((Path)presenter.Children[3]).Data);

        annotation.SourceAnchor = DaisyTourAnchor.Auto;
        annotation.TargetAnchor = DaisyTourAnchor.Auto;
        annotation.StrokeThickness = double.NaN;
        pattern[0] = 5;
        presenter.Update(annotation, new Rect(300, 20, 100, 40), new Rect(10, 20, 80, 40),
            stroke, fill, stroke, hostDefaults, stepDefaults);
        Assert.AreSame(path, presenter.Children[1], "Geometry changes must retain the existing path controls.");
        AssertEndpoints(path, new Point(300, 40), new Point(90, 40));
        Assert.AreEqual(3d, path.StrokeThickness, "Step defaults override host defaults when the annotation inherits.");
        Assert.AreEqual(5d, path.StrokeDashArray![0]);
    }

    [Fact]
    public void When_AnnotationPresetsOrContentChange_AllShapesRetainIndependentTextAndTailBehavior()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var annotation = new DaisyTourAnnotation();
        var brush = new SolidColorBrush(Colors.Blue);
        var source = new Rect(150, 150, 100, 50);
        var target = new Rect(400, 150, 60, 40);
        foreach (var preset in Enum.GetValues<DaisyTourAnnotationPreset>())
        {
            annotation.Preset = preset;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.IsNotNull(((Path)presenter.Children[1]).Data, preset.ToString());
        }

        foreach (var kind in new[] { DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationKind.SpeechBubble })
        {
            annotation.Kind = kind;
            annotation.Tail = DaisyTourAnnotationTail.None;
            annotation.Text = string.Empty;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.IsFalse(presenter.Children[4].IsVisible);
            Assert.IsNull(((Path)presenter.Children[0]).Data);

            annotation.Tail = DaisyTourAnnotationTail.Thought;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.IsFalse(presenter.Children[4].IsVisible);
            Assert.IsNotNull(((Path)presenter.Children[0]).Data);

            annotation.Text = "A localized explanation can wrap across several lines.";
            annotation.Tail = DaisyTourAnnotationTail.None;
            presenter.Update(annotation, source, target, brush, brush, brush);
            Assert.IsTrue(presenter.Children[4].IsVisible);
            Assert.IsNull(((Path)presenter.Children[0]).Data);
        }
        var geometry = new EllipseGeometry { Center = new Point(10, 10), RadiusX = 10, RadiusY = 10 };
        annotation.Kind = DaisyTourAnnotationKind.Custom;
        annotation.Geometry = geometry;
        presenter.Update(annotation, source, target, brush, brush, brush);
        Assert.AreSame(geometry, ((Path)presenter.Children[1]).Data);
    }

    [Fact]
    public async Task When_OpenTourAppearanceChanges_TextButtonsAndProgressRemainCurrent()
    {
        var previousCulture = FloweryLocalization.CurrentCulture.Name;
        var previousTheme = DaisyThemeManager.CurrentThemeName;
        var previousSize = FlowerySizeManager.CurrentSize;
        var target = CreateTarget();
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "appearance", Target = target, Title = "Original title" };
        tour.Steps.Add(step);
        tour.Steps.Add(new DaisyTourStep { Id = "last", Target = target });
        var root = CreateRoot(tour, target);
        try
        {
            FloweryLocalization.SetCulture("en");
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Light"));
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var surface = GetSurface(tour);
            var next = Descendants<DaisyButton>(surface).Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourNext");
            var card = Descendants<Border>(surface).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            var lightColor = ((ISolidColorBrush)card.Background!).Color;
            // Flowery.NET has no DaisySize.Auto and no ResolvedSize: the global size manager supplies the size to a
            // tour without a Size of its own, so its Size is the size in use.
            foreach (var size in Enum.GetValues<DaisySize>())
            {
                FlowerySizeManager.ApplySize(size);
                await ArrangeAsync(root);
                Assert.AreEqual(size, tour.Size);
                Assert.AreEqual(size, next.Size);
                Assert.AreEqual(size, tour.Progress.Size);
                AssertActive(tour, step, 0);
                Assert.IsTrue(tour.CardBounds.Bottom <= RootSize(root).Height + 1);
            }
            Assert.IsTrue(DaisyThemeManager.ApplyTheme("Dark"));
            await ArrangeAsync(root);
            Assert.AreNotEqual(lightColor, ((ISolidColorBrush)card.Background!).Color);
            step.Title = "Updated application title";
            step.Description = string.Join(" ", Enumerable.Repeat("Long localized content wraps without hiding navigation.", 20));
            FloweryLocalization.SetCulture("de");
            await ArrangeAsync(root);
            Assert.AreEqual<object?>(FloweryLocalization.GetStringInternal("Tour_Next"), next.Content);
            Assert.IsTrue(Descendants<TextBlock>(surface).Any(text => text.Text == step.Title));
            FloweryLocalization.SetCulture("ar");
            await ArrangeAsync(root);
            Assert.AreEqual(FlowDirection.RightToLeft, card.FlowDirection);
            Assert.AreEqual<object?>(FloweryLocalization.GetStringInternal("Tour_Next"), next.Content);
            AssertActive(tour, step, 0);
            Assert.IsTrue(next.IsEnabled);
            Assert.IsTrue(next.Bounds.Width > 0 && next.Bounds.Height > 0);
        }
        finally
        {
            try { await CleanupAsync(root, tour); }
            finally
            {
                FloweryLocalization.SetCulture(previousCulture);
                FlowerySizeManager.ApplySize(previousSize);
                if (previousTheme is not null) DaisyThemeManager.ApplyTheme(previousTheme);
            }
        }
    }

    [Fact]
    public async Task When_ConnectorTargetsReflow_BothEndpointsTrackAndAnUnavailableEndIsReportedUntilItReturns()
    {
        var source = CreateTarget();
        var target = CreateTarget();
        source.Margin = new Thickness(20, 100, 0, 0);
        target.Margin = new Thickness(400, 100, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "relationship", Source = source, Target = target,
            Preset = DaisyTourAnnotationPreset.TwoEndedArrow
        };
        var step = new DaisyTourStep { Id = "connection", Target = source };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, source, target);
        DaisyTourClosedEventArgs? closed = null;
        tour.Closed += (_, args) => closed = args;
        var reports = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.StepTargetUnavailable += (_, args) => reports.Add(args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var presenter = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single();
            var path = (Path)presenter.Children[1];
            // Connectors meet the outer edge of each highlight frame.
            var sourceBounds = Frame(tour, BoundsInRoot(source));
            var targetBounds = Frame(tour, BoundsInRoot(target));
            AssertEndpoints(path, new Point(sourceBounds.Right, sourceBounds.Y + sourceBounds.Height / 2),
                new Point(targetBounds.Left, targetBounds.Y + targetBounds.Height / 2));
            var geometryUpdated = NewCompletion();
            EventHandler updated = (_, _) => geometryUpdated.TrySetResult();
            tour.GeometryUpdated += updated;
            try
            {
                source.Margin = new Thickness(400, 180, 0, 0);
                target.Margin = new Thickness(20, 180, 0, 0);
                root.UpdateLayout();
                await geometryUpdated.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= updated; }
            AssertActive(tour, step, 0);
            Assert.AreSame(path, presenter.Children[1]);
            sourceBounds = Frame(tour, BoundsInRoot(source));
            targetBounds = Frame(tour, BoundsInRoot(target));
            AssertEndpoints(path, new Point(sourceBounds.Left, sourceBounds.Y + sourceBounds.Height / 2),
                new Point(targetBounds.Right, targetBounds.Y + targetBounds.Height / 2));
            // An end that leaves the layout hides its connector and shows the warning; the step stays open.
            target.IsVisible = false;
            await RuntimeTestHelpers.WaitUntilAsync(() => reports.Count > 0, "the end is reported");
            await ArrangeAsync(root);
            var reported = reports.Single();
            AssertActive(tour, step, 0, DaisyTourTargetProblem.Annotation);
            AssertWarning(tour, DaisyTourTargetProblem.Annotation);
            Assert.AreEqual(DaisyTourTargetProblem.Annotation, reported.Problem);
            Assert.IsNull(closed, "An unavailable end must not close the tour.");
            Assert.AreEqual("connection", reported.StepId);
            Assert.AreEqual("relationship", reported.AnnotationId);
            Assert.AreSame(target, reported.Target);
            Assert.IsFalse(presenter.IsVisible, "The connector to the unavailable end is hidden.");
            // The end returns, and so do its connector and the normal card.
            target.IsVisible = true;
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is null, "the end returns");
            await ArrangeAsync(root);
            AssertActive(tour, step, 0);
            AssertWarning(tour, null);
            Assert.IsTrue(presenter.IsVisible, "The connector returns with its end.");
            Assert.HasCount(1, reports, "The returning end raises no further report.");
            Assert.IsNull(closed);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_SurfaceAnnotationsHaveNoSource_TheirBodiesRemainOutsideTheCard()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(240, 180, 0, 0);
        var tour = new DaisyTour();
        var annotation = new DaisyTourAnnotation
        {
            Preset = DaisyTourAnnotationPreset.Cloud,
            Text = string.Join(" ", Enumerable.Repeat("Long localized annotation text must wrap within its visible shape.", 80))
        };
        var step = new DaisyTourStep { Id = "surface", Target = target };
        step.Annotations.Add(annotation);
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            foreach (var size in Enum.GetValues<DaisySize>())
            {
                foreach (var preset in new[] { DaisyTourAnnotationPreset.Cloud, DaisyTourAnnotationPreset.SpeechBubble })
                {
                    var published = NewCompletion();
                    EventHandler handler = (_, _) => published.TrySetResult();
                    tour.GeometryUpdated += handler;
                    try
                    {
                        tour.Size = size;
                        annotation.Preset = preset;
                        root.UpdateLayout();
                        await published.Task.WaitAsync(Timeout);
                    }
                    finally { tour.GeometryUpdated -= handler; }
                    AssertActive(tour, step, 0);
                    var presenter = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single();
                    var bounds = ((Path)presenter.Children[1]).Data!.Bounds;
                    var obscured = bounds.Intersect(tour.CardBounds);
                    Assert.IsTrue(IsEmpty(obscured) || obscured.Width * obscured.Height < bounds.Width * bounds.Height,
                        $"{preset}/{size} must not be completely covered by the card.");
                    AssertInViewport(bounds, RootSize(root), $"{preset}/{size}");
                    var content = (Border)presenter.Children[4];
                    Assert.IsTrue(content.IsVisible);
                    // Avalonia rounds the desired size up to whole device pixels, so the limit is rounded the same way.
                    var scale = LayoutHelper.GetLayoutScale(content);
                    Assert.IsTrue(content.DesiredSize.Width <= LayoutHelper.RoundLayoutValueUp(content.MaxWidth, scale) &&
                        content.DesiredSize.Height <= LayoutHelper.RoundLayoutValueUp(content.MaxHeight, scale),
                        $"{preset}/{size}: {content.DesiredSize}, max={content.MaxWidth}x{content.MaxHeight}");
                }
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_LongSurfaceAnnotationsUseNarrowViewports_TheirMeasuredGeometryFitsAtEverySize()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var root = new Grid { Children = { presenter } };
        var annotation = new DaisyTourAnnotation
        {
            Text = string.Join(" ", Enumerable.Repeat("Long localized explanation wraps in a narrow annotation.", 80))
        };
        var brush = new SolidColorBrush(Colors.Blue);
        try
        {
            await LoadAsync(root);
            var cases = from viewport in new[] { new Size(600, 400), new Size(260, 220) }
                        from size in Enum.GetValues<DaisySize>()
                        from preset in new[] { DaisyTourAnnotationPreset.Cloud, DaisyTourAnnotationPreset.SpeechBubble }
                        select (viewport, size, preset);
            foreach (var (viewport, size, preset) in cases)
            {
                root.Width = presenter.Width = viewport.Width;
                root.Height = presenter.Height = viewport.Height;
                var scale = size switch
                {
                    DaisySize.ExtraSmall => 0.8, DaisySize.Small => 0.9, DaisySize.Large => 1.15,
                    DaisySize.ExtraLarge => 1.3, _ => 1
                };
                annotation.Preset = preset;
                var measured = presenter.MeasureSurface(annotation, brush, scale: scale);
                var target = new Rect(viewport.Width / 2 - 20, 60, 40, 30);
                var card = new Rect(12, viewport.Height - 100, viewport.Width - 24, 88);
                var source = DaisyTourLayout.PlaceAnnotation(target, card, viewport, measured);
                presenter.Update(annotation, source, target, brush, brush, brush, scale: scale);
                await ArrangeAsync(root);
                var bounds = ((Path)presenter.Children[1]).Data!.Bounds;
                AssertInViewport(bounds, viewport, $"{preset}/{size}/{viewport}");
                var content = (Border)presenter.Children[4];
                Assert.IsTrue(content.Bounds.Width <= viewport.Width - 24 + 0.01);
                Assert.IsTrue(content.Bounds.Height <= viewport.Height - 24 + 0.01);
                Assert.AreEqual(measured.Width, content.Bounds.Width, 0.01);
                Assert.AreEqual(measured.Height, content.Bounds.Height, 0.01);
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [Fact]
    public async Task When_SurfaceAnnotationsContainText_ItRendersUntrimmedInsideTheirContour()
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var samples = new[]
        {
            ("This explanation wraps inside its annotation and follows the live target.", FlowDirection.LeftToRight),
            ("Diese Erklärung wird in der Anmerkung umbrochen und folgt dem vorhandenen Ziel.", FlowDirection.LeftToRight),
            ("يلتف هذا الشرح داخل التعليق ويتبع الهدف الحالي.", FlowDirection.RightToLeft)
        };
        var cases = from kind in new[] { DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationKind.SpeechBubble }
                    from scale in new[] { 0.8, 1, 1.3 }
                    from width in new[] { double.NaN, 230 }
                    from sample in samples
                    select (kind, scale, width, sample.Item1, sample.Item2);
        try
        {
            await LoadAsync(root);
            foreach (var (kind, scale, width, text, flow) in cases)
            {
                // MaxHeight is raised so no sample reaches the size limit, where overflowing text is trimmed.
                var annotation = new DaisyTourAnnotation { Kind = kind, Text = text, Width = width, MaxHeight = 360 };
                presenter.Update(annotation, new Rect(150, 100, 300, 200), new Rect(520, 340, 40, 30),
                    new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.Blue),
                    scale: scale, textFlowDirection: flow);
                await ArrangeAsync(root);
                var diagnostic = $"{kind}/{scale}/{width}/{flow}";
                var host = (Border)presenter.Children[4];
                var block = (TextBlock)((ContentPresenter)host.Child!).Content!;
                Assert.IsTrue(host.IsVisible, diagnostic);
                Assert.IsTrue(block.Bounds.Width > 0 && block.Bounds.Height > 0, diagnostic);
                Assert.IsFalse(IsTextTrimmed(block), $"{diagnostic}: the measured shape must hold the complete text.");
                var outer = TransformBounds(host, presenter, new Rect(0, 0, host.Bounds.Width, host.Bounds.Height));
                var padding = host.Padding.Left;
                var textArea = new Rect(outer.X + padding, outer.Y + padding, outer.Width - 2 * padding, outer.Height - 2 * padding);
                var contour = ContourPoints((Path)presenter.Children[1]);
                foreach (var corner in new[] { new Point(textArea.Left, textArea.Top), new Point(textArea.Right, textArea.Top),
                    new Point(textArea.Right, textArea.Bottom), new Point(textArea.Left, textArea.Bottom) })
                    Assert.IsTrue(IsInside(corner, contour), $"{diagnostic}: text area {textArea} must stay inside the contour at {corner}.");

                var bitmap = RenderBitmap(root);
                var pixels = bitmap.Pixels;
                var inside = 0;
                var outside = 0;
                for (var y = 0; y < bitmap.PixelHeight; y++)
                {
                    for (var x = 0; x < bitmap.PixelWidth; x++)
                    {
                        var offset = (y * bitmap.PixelWidth + x) * 4;
                        if (pixels[offset] < 160 || pixels[offset + 1] > 90 || pixels[offset + 2] > 90)
                            continue;
                        var point = new Point((x + 0.5) * 600 / bitmap.PixelWidth, (y + 0.5) * 400 / bitmap.PixelHeight);
                        if (textArea.Contains(point)) inside++;
                        if (!IsInside(point, contour)) outside++;
                    }
                }
                Assert.IsTrue(inside >= 30, $"{diagnostic}: text pixels inside the text area={inside}.");
                Assert.AreEqual(0, outside, $"{diagnostic}: text pixels outside the contour.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    [Fact]
    public void When_CardPositionIsStationary_EachOfTheNinePositionsUsesItsWindowSpot()
    {
        var viewport = new Size(600, 400);
        var card = new Size(200, 100);
        var away = new Rect(-500, -500, 10, 10);
        var expected = new Dictionary<DaisyTourCardPosition, Point>
        {
            [DaisyTourCardPosition.TopLeft] = new(12, 12), [DaisyTourCardPosition.Top] = new(200, 12),
            [DaisyTourCardPosition.TopRight] = new(388, 12), [DaisyTourCardPosition.Left] = new(12, 150),
            [DaisyTourCardPosition.Center] = new(200, 150), [DaisyTourCardPosition.Right] = new(388, 150),
            [DaisyTourCardPosition.BottomLeft] = new(12, 288), [DaisyTourCardPosition.Bottom] = new(200, 288),
            [DaisyTourCardPosition.BottomRight] = new(388, 288)
        };
        foreach (var (position, origin) in expected)
            Assert.AreEqual(new Rect(origin.X, origin.Y, 200, 100), DaisyTourLayout.PlaceStationary(position, card, viewport, away), position.ToString());
        Assert.IsNull(DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Target, card, viewport, away));
        Assert.IsNull(DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Default, card, viewport, away));

        // A covered position slides along its own edge by the smallest clear distance.
        Assert.AreEqual(new Rect(12, 82, 200, 100),
            DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.TopLeft, card, viewport, new Rect(40, 40, 60, 30)));
        Assert.AreEqual(new Rect(68, 288, 200, 100),
            DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Bottom, card, viewport, new Rect(280, 320, 40, 30)));
        Assert.AreEqual(new Rect(200, 78, 200, 100),
            DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Center, card, viewport, new Rect(280, 190, 40, 20)));
        // Without room on that edge, the card goes beside the target.
        Assert.IsNull(DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Left, card, viewport, new Rect(20, 20, 60, 360)));
    }

    [Fact]
    public async Task When_NextCardWouldCoverACourse_TheClearPreviousPositionIsKept()
    {
        // Window coordinates from the top left that fit the 752x480 CI window as well as larger ones.
        var first = CreateTarget();
        first.Margin = new Thickness(24, 24, 0, 0);
        var start = CreateTarget();
        start.Margin = new Thickness(24, 400, 0, 0);
        var end = CreateTarget();
        end.Margin = new Thickness(600, 400, 0, 0);
        // Nothing fits beside the first target, so its card goes to the clamped spot below it.
        var opening = new DaisyTourStep { Id = "opening", Target = first, Title = "Opening", Description = "The card goes below this target." };
        // Both ends lie below the limit, so the course bridges above them, where the card above both would go.
        // Top is preferred so that the outcome does not depend on the room below in larger windows.
        var crossing = new DaisyTourStep
        {
            Id = "crossing", Target = end, Placement = DaisyTourPlacement.Top, Title = "Crossing", Description = "The card goes back where it was."
        };
        crossing.Annotations.Add(new DaisyTourAnnotation
        {
            Id = "bridge", Source = start, Target = end, Kind = DaisyTourAnnotationKind.Line, Stroke = new SolidColorBrush(Colors.Red),
            StrokeThickness = 4, RouteLimitCoordinate = 360, RouteLimitSide = DaisyTourRouteSide.Above
        });
        var tour = new DaisyTour();
        tour.Steps.Add(opening);
        tour.Steps.Add(crossing);
        var root = new Grid { Width = 720, Height = 476, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        tour.Closed += (_, args) => tour.Tag = args;
        foreach (var child in new Control[] { first, start, end, tour }) root.Children.Add(child);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, opening, 0);
            var kept = tour.CardBounds;
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, crossing, 1);
            var course = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single().Course;
            // The bridge's top run with half its 4 DIP stroke on each side.
            var level = course.Min(point => point.Y);
            var bridge = new Rect(course.Min(point => point.X), level - 2, course.Max(point => point.X) - course.Min(point => point.X), 4);
            // The card's size changes with its content; the position is what is kept.
            var card = tour.CardBounds;
            var ends = BoundsInRoot(start);
            ends = ends.Union(BoundsInRoot(end));
            var above = new Rect(ends.X + ends.Width / 2 - card.Width / 2, ends.Top - 32 - card.Height, card.Width, card.Height);
            var diagnostic = $"first card={kept}, card={card}, bridge={bridge}, above={above}";
            if (!Overlaps(above, bridge) || Overlaps(new Rect(kept.X, kept.Y, card.Width, card.Height), bridge))
                Assert.Inconclusive($"{diagnostic}: this window does not produce a course that only the card above both ends covers.");
            Assert.AreEqual(new Point(kept.X, kept.Y), new Point(card.X, card.Y), diagnostic);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public void When_CardWouldCoverACourse_ItMovesToTheNearestClearSpot()
    {
        var viewport = new Size(600, 400);
        var card = new Size(200, 100);
        var target = new Rect(250, 300, 100, 30);
        // A course runs where the card above the target would go; the right side is clear.
        var course = new Rect(0, 180, 600, 10);
        Assert.AreEqual(new Rect(382, 265, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target,
            DaisyTourPlacement.Auto, target, card, viewport, [target, course]));
        // A fixed position slides along its edge past the course.
        var band = new Rect(0, 0, 300, 130);
        Assert.AreEqual(new Rect(12, 142, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.TopLeft,
            DaisyTourPlacement.Auto, target, card, viewport, [target, band]));
        // Without a clear side, the card above the target moves up past the course.
        var low = new Rect(200, 400, 200, 30);
        var tall = new Size(600, 500);
        var crossing = new Rect(0, 300, 600, 10);
        Assert.AreEqual(new Rect(200, 188, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target,
            DaisyTourPlacement.Auto, low, card, tall, [low, crossing]));
        // A previous position that is still clear is kept.
        Assert.AreEqual(new Rect(12, 12, 200, 100), DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target,
            DaisyTourPlacement.Auto, low, card, tall, [low, crossing], new Rect(12, 12, 200, 100)));
        // Courses everywhere leave no clear spot.
        Assert.IsNull(DaisyTourLayout.AvoidObstacles(DaisyTourCardPosition.Target, DaisyTourPlacement.Auto, target, card,
            viewport, [new Rect(0, 0, 600, 400)]));
    }

    [Fact]
    public async Task When_CardContentGrowsAfterPlacement_TheCardIsPlacedForItsFinalSize()
    {
        var target = CreateTarget();
        // Content that grows later without notifying the tour, like progress items that load after placement.
        var template = new FuncDataTemplate<object>((_, _) => new Border { Height = 20, Tag = "grows" });
        var step = new DaisyTourStep { Id = "grows", Target = target, Title = "Final card size", ContentTemplate = template };
        var tour = new DaisyTour { CardPosition = DaisyTourCardPosition.Bottom };
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            // Let the update queued at the end of the transition finish first; the growth comes later.
            await RenderFrameAsync(root);
            await ArrangeAsync(root);
            await ArrangeAsync(root);
            var card = Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");
            var placedHeight = tour.CardBounds.Height;
            Descendants<Border>(card).Single(border => Equals(border.Tag, "grows")).Height = 120;
            await RenderFrameAsync(root);
            await RenderFrameAsync(root);
            var visible = DaisyTourLayout.VisibleArea(TopLevel.GetTopLevel(root)!);
            Assert.IsTrue(card.Bounds.Height > placedHeight + 50, $"The card must have grown: {placedHeight} -> {card.Bounds.Height}.");
            Assert.AreEqual(card.Bounds.Height, tour.CardBounds.Height, 0.5, "The placement must use the card's final height.");
            Assert.AreEqual(visible.Bottom - 12, Canvas.GetTop(card) + card.Bounds.Height, 0.5, "The bottom card must end 12 DIP above the visible edge.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public void When_WindowExtendsBeyondItsDisplay_OnlyTheShownPartIsUsedForPlacement()
    {
        var content = new Rect(0, 0, 1000, 800);
        // At 125 %, content starting at (100, 200) on a 1000 px high work area shows its top 640 DIP.
        Assert.AreEqual(new Rect(0, 0, 1000, 640),
            DaisyTourLayout.VisibleArea(content, 100, 200, new PixelRect(0, 0, 1920, 1000), 1.25));
        // Content starting left of the screen loses its left part.
        Assert.AreEqual(new Rect(80, 0, 920, 800),
            DaisyTourLayout.VisibleArea(content, -100, 0, new PixelRect(0, 0, 1920, 1080), 1.25));
        Assert.AreEqual(content,
            DaisyTourLayout.VisibleArea(content, 0, 0, new PixelRect(0, 0, 3000, 3000), 1.25));
        // A window that is mostly off its display keeps its full content area.
        Assert.AreEqual(content,
            DaisyTourLayout.VisibleArea(content, 0, 950, new PixelRect(0, 0, 3000, 1000), 1));
    }

    [Fact]
    public async Task When_TourUsesAStationaryCardPosition_StepsInheritOverrideAndAvoidTheTarget()
    {
        // The root is centered in the host window, whatever its size: the side target stays clear of
        // top-left and bottom-right cards, and the middle target lies under a centered card.
        var side = CreateTarget();
        side.Margin = new Thickness(24, 300, 0, 0);
        var middle = CreateTarget();
        middle.Margin = new Thickness(250, 180, 0, 0);
        var inherited = new DaisyTourStep { Id = "inherited", Target = side };
        var overridden = new DaisyTourStep { Id = "overridden", Target = side, CardPosition = DaisyTourCardPosition.TopLeft };
        var covered = new DaisyTourStep { Id = "covered", Target = middle, CardPosition = DaisyTourCardPosition.Center };
        var tour = new DaisyTour { CardPosition = DaisyTourCardPosition.BottomRight };
        tour.Steps.Add(inherited);
        tour.Steps.Add(overridden);
        tour.Steps.Add(covered);
        var root = CreateRoot(tour, side, middle);
        try
        {
            await LoadAsync(root);
            // Positions refer to the part of the window that the display shows.
            var visible = DaisyTourLayout.VisibleArea(TopLevel.GetTopLevel(root)!);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, inherited, 0);
            Assert.AreEqual(visible.Right - 12, tour.CardBounds.Right, 0.5, "The tour position applies to steps that inherit it.");
            Assert.AreEqual(visible.Bottom - 12, tour.CardBounds.Bottom, 0.5);
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, overridden, 1);
            Assert.AreEqual(new Point(visible.X + 12, visible.Y + 12), new Point(tour.CardBounds.X, tour.CardBounds.Y), "A step position overrides the tour.");
            await tour.NextAsync().WaitAsync(Timeout);
            AssertActive(tour, covered, 2);
            // A small window may have no clear edge slide and no clear side; the card then stays in view over the target.
            var card = tour.CardBounds;
            var local = BoundsInRoot(middle);
            local = new Rect(local.X - visible.X, local.Y - visible.Y, local.Width, local.Height);
            var highlight = new Rect(local.X - tour.HighlightPadding - 2, local.Y - tour.HighlightPadding - 2,
                local.Width + 2 * tour.HighlightPadding + 4, local.Height + 2 * tour.HighlightPadding + 4);
            var size = new Size(card.Width, card.Height);
            var viewport = new Size(visible.Width, visible.Height);
            if (DaisyTourLayout.PlaceStationary(DaisyTourCardPosition.Center, size, viewport, highlight) is null &&
                Overlaps(DaisyTourLayout.Place(local, size, viewport, DaisyTourPlacement.Auto), highlight))
                Assert.Inconclusive($"The visible area {visible} has no room for the card {card} beside the target {local}.");
            Assert.IsFalse(Overlaps(tour.CardBounds, Frame(tour, BoundsInRoot(middle))),
                $"A centered card that would cover the target returns to target-relative placement: card={tour.CardBounds}");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public void When_SurfaceTargetsSitAtEdgesOrCorners_EightDirectionsKeepTheSurfaceBesideAndInView()
    {
        var viewport = new Size(600, 400);
        var surface = new Size(180, 90);
        var positions = from x in new[] { 16d, 250, 484 } from y in new[] { 16d, 180, 344 } select new Rect(x, y, 100, 40);
        // WinUI's Rect.Empty means no card; an Avalonia rect without size covers nothing either.
        foreach (var card in new[] { default, new Rect(380, 230, 200, 150) })
        {
            foreach (var target in positions)
            {
                if (!IsEmpty(card) && Overlaps(card, target)) continue;
                var placed = DaisyTourLayout.PlaceAnnotation(target, card, viewport, surface, [target], -1, false, out var direction);
                var diagnostic = $"target={target}, card={card}, placed={placed}, direction={direction}";
                Assert.IsTrue(direction >= 0, diagnostic);
                AssertInViewport(new Rect(placed.X - 12, placed.Y - 12, placed.Width + 24, placed.Height + 24), viewport, diagnostic);
                Assert.IsFalse(Overlaps(placed, target), diagnostic);
                Assert.IsTrue(IsEmpty(card) || !Overlaps(placed, card), diagnostic);
                var gapX = Math.Max(0, Math.Max(placed.Left - target.Right, target.Left - placed.Right));
                var gapY = Math.Max(0, Math.Max(placed.Top - target.Bottom, target.Top - placed.Bottom));
                Assert.IsTrue(gapX <= 28 && gapY <= 28, $"{diagnostic}: the surface must stay beside its target.");
            }
        }

        // No room above, but beside: left-to-right prefers east (0), right-to-left west (4).
        var nearTop = new Rect(250, 60, 100, 40);
        DaisyTourLayout.PlaceAnnotation(nearTop, default, viewport, surface, [], -1, false, out var leftToRight);
        DaisyTourLayout.PlaceAnnotation(nearTop, default, viewport, surface, [], -1, true, out var rightToLeft);
        Assert.AreEqual(0, leftToRight);
        Assert.AreEqual(4, rightToLeft);

        var center = new Rect(250, 180, 100, 40);
        DaisyTourLayout.PlaceAnnotation(center, default, viewport, surface, [], 2, false, out var kept);
        Assert.AreEqual(2, kept, "A previous direction that still fits is kept.");
    }

    [Fact]
    public async Task When_SpeechBubbleTargetsTheTopRightCorner_TheBubbleStaysInViewBesideTheTarget()
    {
        var target = CreateTarget();
        target.Margin = new Thickness(476, 16, 0, 0);
        var annotation = new DaisyTourAnnotation
        {
            Id = "corner", Kind = DaisyTourAnnotationKind.SpeechBubble, Tail = DaisyTourAnnotationTail.Speech,
            Text = "This explanation wraps inside its annotation and follows the live target."
        };
        var step = new DaisyTourStep { Id = "corner", Target = target };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            var presenter = Descendants<DaisyTourAnnotationPresenter>(GetSurface(tour)).Single();
            var body = ((Path)presenter.Children[1]).Data!.Bounds;
            var diagnostic = $"bubble={body}, target={BoundsInRoot(target)}, card={tour.CardBounds}";
            AssertInViewport(body, RootSize(root), diagnostic);
            Assert.IsFalse(Overlaps(body, Frame(tour, BoundsInRoot(target))), diagnostic);
            Assert.IsFalse(Overlaps(body, tour.CardBounds), diagnostic);
            await ArrangeAsync(root);
            var text = (TextBlock)((ContentPresenter)((Border)presenter.Children[4]).Child!).Content!;
            Assert.IsFalse(IsTextTrimmed(text), diagnostic);
        }
        finally { await CleanupAsync(root, tour); }
    }

    private static bool Overlaps(Rect first, Rect second)
    {
        first = first.Intersect(second);
        return first.Width > 0 && first.Height > 0;
    }

    // Avalonia has no Rect.IsEmpty; an intersection without overlap has no size.
    private static bool IsEmpty(Rect rect) => rect.Width <= 0 || rect.Height <= 0;

    // A nonempty WinUI intersection: the rectangles overlap or share an edge.
    private static bool IntersectsOrTouches(Rect first, Rect second) =>
        first.Left <= second.Right && second.Left <= first.Right && first.Top <= second.Bottom && second.Top <= first.Bottom;

    [Fact]
    public async Task When_EightStepsUseExtraLargeSize_TheCurrentCompactIndicatorRemainsVisible()
    {
        var target = CreateTarget();
        var tour = new DaisyTour { Size = DaisySize.ExtraLarge };
        for (var index = 0; index < 8; index++)
            tour.Steps.Add(new DaisyTourStep { Id = "step-" + index, Target = target });
        var root = CreateRoot(tour, target);
        try
        {
            await LoadAsync(root);
            tour.Size = DaisySize.ExtraLarge;
            await tour.StartAsync().WaitAsync(Timeout);
            for (var index = 0; index < 8; index++)
            {
                if (index > 0) await tour.NextAsync().WaitAsync(Timeout);
                await ArrangeAsync(root);
                AssertActive(tour, tour.Steps[index], index);
                var viewport = Descendants<ScrollViewer>(GetSurface(tour)).Single(viewer => ReferenceEquals(viewer.Content, tour.Progress));
                var items = Descendants<DaisyStepItem>(tour.Progress).ToArray();
                var itemsPanel = tour.Progress.ItemsPanelRoot;
                Assert.HasCount(8, items,
                    $"Step={index}, ModelCount={tour.Progress.ItemCount}, PresentedChildren={itemsPanel?.Children.Count}, ProgressSize={tour.Progress.Bounds.Width}x{tour.Progress.Bounds.Height}, ViewportSize={viewport.Bounds.Width}x{viewport.Bounds.Height}");
                Assert.IsTrue(items.All(item => item.IsCompact));
                Assert.AreEqual(index, tour.Progress.SelectedIndex);
                Assert.IsTrue(viewport.IsVisible);
                var currentItem = items[index];
                await RuntimeTestHelpers.EnsureLoadedAsync(currentItem).WaitAsync(Timeout);
                await RenderFrameAsync(root);
                // The WinUI indicator is an Ellipse; the Flowery.NET step template draws it as the Border PART_HCircle.
                var indicators = Descendants<Border>(currentItem).Where(border => border.Name == "PART_HCircle").ToArray();
                if (indicators.Length != 1)
                {
                    var visualChildren = currentItem.GetVisualChildren().Select(child => child.GetType().Name);
                    Assert.Fail($"Expected one indicator; found {indicators.Length}. Step={index}, ItemIndex={currentItem.Index}, Loaded={currentItem.IsLoaded}, Size={currentItem.Bounds.Width}x{currentItem.Bounds.Height}, TemplateNull={currentItem.Template is null}, VisualChildren={string.Join(",", visualChildren)}, Content={currentItem.Content?.GetType().Name}");
                }
                var indicator = indicators.Single();
                AssertFits(indicator, viewport);
                // Compact indicators have no label row below the circle.
                Assert.AreEqual(indicator.Bounds.Height, currentItem.Bounds.Height, 1, "A compact indicator must not reserve label space.");
            }
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public void When_AnnotationScaleChanges_FallbackDimensionsScaleAndExplicitStrokeRemainsFixed()
    {
        var presenter = new DaisyTourAnnotationPresenter();
        var annotation = new DaisyTourAnnotation { Preset = DaisyTourAnnotationPreset.ThinArrow };
        var brush = new SolidColorBrush(Colors.Blue);
        var source = new Rect(10, 10, 40, 40);
        var target = new Rect(400, 10, 40, 40);
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 0.8);
        var body = (Path)presenter.Children[1];
        var head = (Path)presenter.Children[3];
        var smallStroke = body.StrokeThickness;
        var smallHead = head.Data!.Bounds;
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 1.3);
        Assert.AreEqual(smallStroke * 1.3 / 0.8, body.StrokeThickness, 0.0001);
        Assert.AreEqual(smallHead.Width * 1.3 / 0.8, head.Data!.Bounds.Width, 0.0001);
        Assert.AreEqual(smallHead.Height * 1.3 / 0.8, head.Data!.Bounds.Height, 0.0001);
        annotation.StrokeThickness = 7;
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 0.8);
        Assert.AreEqual(7d, body.StrokeThickness);
        presenter.Update(annotation, source, target, brush, brush, brush, scale: 1.3);
        Assert.AreEqual(7d, body.StrokeThickness);
    }

    [Fact]
    public async Task When_TargetHighlightsOverlap_TheBackdropContainsOneCombinedCutout()
    {
        var first = CreateTarget();
        var second = CreateTarget();
        second.Margin = new Thickness(90, 24, 0, 0);
        var annotation = new DaisyTourAnnotation { Id = "overlap", Source = first, Target = second };
        var step = new DaisyTourStep { Id = "combined", Target = first };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, first, second);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            var surface = (Canvas)GetSurface(tour);
            var backdrop = (Path)surface.Children[0];
            var outline = (Path)surface.Children[1];
            var cutouts = (GeometryGroup)backdrop.Data!;
            Assert.AreEqual(FillRule.EvenOdd, cutouts.FillRule);
            Assert.HasCount(2, cutouts.Children, "Backdrop plus one combined hole prevents overlapping cutouts from restoring dimming.");
            Assert.HasCount(1, ((GeometryGroup)outline.Data!).Children);
            var combined = cutouts.Children[1].Bounds;
            Assert.IsTrue(combined.Left <= BoundsInRoot(first).Left && combined.Right >= BoundsInRoot(second).Right);

            // Tracking publishes the reflowed geometry asynchronously after layout.
            var published = NewCompletion();
            EventHandler handler = (_, _) => published.TrySetResult();
            tour.GeometryUpdated += handler;
            try
            {
                second.Margin = new Thickness(300, 24, 0, 0);
                root.UpdateLayout();
                await published.Task.WaitAsync(Timeout);
            }
            finally { tour.GeometryUpdated -= handler; }
            Assert.HasCount(3, ((GeometryGroup)backdrop.Data!).Children, "Separated targets need separate visible cutouts.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_PreparationIsPending_SkipIsUsableAndClosingRestoresPreviousFocus()
    {
        var target = CreateTarget();
        var previous = new Button { Content = "Previous focus", HorizontalAlignment = HorizontalAlignment.Right };
        var tour = new DaisyTour();
        var entered = NewCompletion();
        var release = NewCompletion();
        tour.Steps.Add(new DaisyTourStep { Id = "ready-focus", Target = target });
        tour.Steps.Add(new DaisyTourStep
        {
            Id = "pending-focus", Target = target,
            PrepareAsync = async (_, _) => { entered.TrySetResult(); await release.Task; }
        });
        var root = CreateRoot(tour, target, previous);
        try
        {
            await LoadAsync(root);
            Assert.IsTrue(previous.Focus(NavigationMethod.Unspecified));
            Assert.AreSame(previous, FocusedElement(root));
            await tour.StartAsync().WaitAsync(Timeout);
            var starting = tour.NextAsync();
            await entered.Task.WaitAsync(Timeout);
            await ArrangeAsync(root);
            var surface = GetSurface(tour);
            var skip = Descendants<DaisyButton>(surface).Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourSkip");
            var next = Descendants<DaisyButton>(surface).Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourNext");
            Assert.IsTrue(skip.IsEnabled);
            Assert.IsTrue(skip.Bounds.Width > 0 && skip.Bounds.Height > 0);
            Assert.IsFalse(next.IsEnabled);
            Assert.IsTrue(skip.Focus(NavigationMethod.Unspecified));
            Assert.AreSame(skip, FocusedElement(root));
            var peer = ControlAutomationPeer.CreatePeerForElement(skip);
            Assert.IsNotNull(peer);
            var invoke = peer.GetProvider<IInvokeProvider>();
            Assert.IsNotNull(invoke);
            invoke.Invoke();
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.AreSame(previous, FocusedElement(root));
            release.TrySetResult();
            await starting.WaitAsync(Timeout);
            AssertClosed(tour);
        }
        finally { release.TrySetResult(); await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_TheTourBelongsToNoWindow_StartReportsTargetUnavailableOnce()
    {
        var tour = new DaisyTour();
        tour.Steps.Add(new DaisyTourStep { Id = "first", Target = CreateTarget() });
        tour.Steps.Add(new DaisyTourStep { Id = "second", Target = CreateTarget() });
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        await tour.StartAsync().WaitAsync(Timeout);
        AssertClosed(tour);
        Assert.HasCount(1, closed, "A tour without a window closes once.");
        Assert.AreEqual(DaisyTourCloseReason.TargetUnavailable, closed[0].Reason);
        Assert.AreEqual("first", closed[0].StepId);
        Assert.IsNull(closed[0].Error);
    }

    [Fact]
    public async Task When_TheTourItselfCannotContinue_ItClosesAsFailedWithTheError()
    {
        var target = CreateTarget();
        var failure = new InvalidOperationException("The card content cannot be measured.");
        // Card content that cannot be laid out is a failure of the tour itself, not a problem of its target.
        var step = new DaisyTourStep
        {
            Id = "failing", Target = target,
            ContentTemplate = new FuncDataTemplate<DaisyTourStep>((_, _) => new FailingMeasure(failure))
        };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed, "A failed session closes once.");
            Assert.AreEqual(DaisyTourCloseReason.Failed, closed[0].Reason);
            Assert.AreEqual("failing", closed[0].StepId);
            Assert.AreSame(target, closed[0].Target);
            Assert.AreSame(failure, closed[0].Error);
            Assert.IsFalse(OpenSurfaces(root).Any(surface => Descendants<Border>(surface).Any(border =>
                AutomationProperties.GetAutomationId(border) == "DaisyTourCard")), "A failed tour leaves no card in the overlay.");
            // The tour opens again once its content can be laid out.
            step.ContentTemplate = null;
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            await ArrangeAsync(root);
            AssertPlacedCard(tour, Card(tour), target);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_CancelIsCalledOnAnOpenTour_ItClosesAsCancelledAndRestoresPreviousFocus()
    {
        var target = CreateTarget();
        var previous = new Button { Content = "Previous focus", HorizontalAlignment = HorizontalAlignment.Right };
        var step = new DaisyTourStep { Id = "cancel", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target, previous);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        try
        {
            await LoadAsync(root);
            Assert.IsTrue(previous.Focus(NavigationMethod.Unspecified));
            Assert.AreSame(previous, FocusedElement(root));
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            Assert.IsTrue(FocusedElement(root) is Visual focused && DaisyTourLayout.IsDescendant(focused, GetSurface(tour)),
                "The open tour keeps the focus in its card.");
            tour.Cancel();
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Cancelled, closed[0].Reason);
            Assert.AreEqual("cancel", closed[0].StepId);
            Assert.AreSame(previous, FocusedElement(root), "Closing gives the focus back.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_EscapeIsPressedInAnOpenTour_ItSkips()
    {
        var target = CreateTarget();
        var step = new DaisyTourStep { Id = "escape", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        tour.Steps.Add(new DaisyTourStep { Id = "after-escape", Target = target });
        var root = CreateRoot(tour, target);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            var focused = FocusedElement(root) as InputElement;
            Assert.IsNotNull(focused, "The open tour has a focused element.");
            Assert.IsTrue(DaisyTourLayout.IsDescendant(focused, GetSurface(tour)), "The focus is inside the tour.");
            var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
            focused.RaiseEvent(escape);
            Assert.IsTrue(escape.Handled, "The tour handles Escape.");
            await ArrangeAsync(root);
            AssertClosed(tour);
            Assert.HasCount(1, closed);
            Assert.AreEqual(DaisyTourCloseReason.Skipped, closed[0].Reason);
            Assert.AreEqual("escape", closed[0].StepId);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_TheStepTargetLeavesTheVisualTree_TheStepWarnsAndRecoversWhenItReturns()
    {
        var target = CreateTarget();
        var step = new DaisyTourStep { Id = "detached", Target = target };
        var tour = new DaisyTour();
        tour.Steps.Add(step);
        var root = CreateRoot(tour, target);
        var reports = new List<DaisyTourStepTargetUnavailableEventArgs>();
        tour.StepTargetUnavailable += (_, args) => reports.Add(args);
        try
        {
            await LoadAsync(root);
            await tour.StartAsync().WaitAsync(Timeout);
            AssertActive(tour, step, 0);
            // A removed control raises no layout event of its own; the step must still notice it.
            root.Children.Remove(target);
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is not null, "the removed target puts the step in its warning state");
            await RuntimeTestHelpers.WaitUntilAsync(() => reports.Count > 0, "the removed target is reported");
            await ArrangeAsync(root);
            AssertActive(tour, step, 0, DaisyTourTargetProblem.Target);
            AssertWarning(tour, DaisyTourTargetProblem.Target);
            Assert.AreEqual(DaisyTourTargetProblem.Target, reports.Single().Problem);
            Assert.AreSame(target, reports[0].Target);
            root.Children.Insert(0, target);
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.Warning is null, "the returned target clears the warning");
            await ArrangeAsync(root);
            AssertActive(tour, step, 0);
            AssertWarning(tour, null);
            AssertTargetBounds(tour, target);
            Assert.HasCount(1, reports, "A cause is reported once per visit.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    // A control whose measure fails, such as card content that throws while it is laid out.
    private sealed class FailingMeasure(Exception failure) : Control
    {
        protected override Size MeasureOverride(Size availableSize) => throw failure;
    }

    private static async Task VerifyAnnotationPixelsAsync(DaisyTourAnnotationKind kind,
        DaisyTourAnnotationPreset preset, DaisyTourStrokeStyle strokeStyle)
    {
        var root = new Canvas { Width = 600, Height = 400, Background = new SolidColorBrush(Colors.Black) };
        var calibration = new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Colors.Blue) };
        Canvas.SetLeft(calibration, 10);
        Canvas.SetTop(calibration, 10);
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        var presenterCalibration = new Border { Width = 20, Height = 20, Background = new SolidColorBrush(Colors.Yellow) };
        Canvas.SetLeft(presenterCalibration, 10);
        Canvas.SetTop(presenterCalibration, 40);
        presenter.Children.Add(presenterCalibration);
        root.Children.Add(calibration);
        root.Children.Add(presenter);
        var annotation = new DaisyTourAnnotation
        {
            Kind = kind, Preset = preset, StrokeStyle = strokeStyle,
            Path = strokeStyle switch
            {
                DaisyTourStrokeStyle.Dotted => DaisyTourAnnotationPath.Curved,
                DaisyTourStrokeStyle.Dashed => DaisyTourAnnotationPath.Elbow,
                _ => DaisyTourAnnotationPath.Default
            },
            DashArray = strokeStyle == DaisyTourStrokeStyle.Custom ? new DaisyTourDashCollection { 6, 2, 1, 2, 1, 2 } : null,
            Geometry = kind == DaisyTourAnnotationKind.Custom ? new EllipseGeometry { Center = new Point(60, 40), RadiusX = 60, RadiusY = 40 } : null,
            StrokeThickness = 12, Width = 120, Height = 80, HeadLength = 48, HeadWidth = 44,
            SourceAnchor = DaisyTourAnchor.Right, TargetAnchor = DaisyTourAnchor.Left,
            Stroke = new SolidColorBrush(Colors.Red), Fill = new SolidColorBrush(Colors.Lime)
        };
        try
        {
            await LoadAsync(root);
            var isSurface = kind is DaisyTourAnnotationKind.Cloud or DaisyTourAnnotationKind.SpeechBubble or DaisyTourAnnotationKind.Custom;
            var source = isSurface ? new Rect(140, 120, 120, 100) : new Rect(80, 180, 80, 40);
            var target = new Rect(440, strokeStyle == DaisyTourStrokeStyle.Dashed ? 260 : 180, 80, 40);
            presenter.Update(annotation, source, target,
                new SolidColorBrush(Colors.Red), new SolidColorBrush(Colors.Lime), new SolidColorBrush(Colors.White));
            await ArrangeAsync(root);
            Assert.IsFalse(presenter.Children[4].IsVisible,
                "The rendering probe must contain no annotation text or content.");
            var bitmap = RenderBitmap(root);
            var pixels = bitmap.Pixels;
            Assert.IsTrue(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0);
            Assert.AreEqual(bitmap.PixelWidth * bitmap.PixelHeight * 4, pixels.Length);
            var calibrationPixels = CountRenderedColor(pixels, bitmap, new Rect(10, 10, 20, 20), Colors.Blue);
            var presenterCalibrationPixels = CountRenderedColor(pixels, bitmap, new Rect(10, 40, 20, 20), Colors.Yellow);
            Assert.IsTrue(calibrationPixels >= 100, "Native bitmap calibration must render before shape pixels are assessed.");
            var strokeRegion = isSurface
                ? new Rect(130, 110, 140, 120)
                : kind == DaisyTourAnnotationKind.Line ? new Rect(170, 150, 260, 150) : new Rect(220, 150, 160, 120);
            var fillRegion = isSurface
                ? new Rect(150, 140, 100, 60) : new Rect(388, 170, 60, 60);
            var strokePixels = CountRenderedColor(pixels, bitmap, strokeRegion, Colors.Red);
            var fillPixels = CountRenderedColor(pixels, bitmap, fillRegion, Colors.Lime);
            var startHeadPixels = preset == DaisyTourAnnotationPreset.TwoEndedArrow
                ? CountRenderedColor(pixels, bitmap, new Rect(150, 170, 60, 60), Colors.Lime) : 0;
            var paths = presenter.Children.OfType<Path>().Select((path, index) =>
                $"Path{index}: Actual={path.Bounds.Width}x{path.Bounds.Height}, Desired={path.DesiredSize}, DataBounds={path.Data?.Bounds}, Canvas={Canvas.GetLeft(path)},{Canvas.GetTop(path)}, Origin={path.TranslatePoint(new Point(), presenter)}, Stroke={DescribeBrush(path.Stroke)}, Fill={DescribeBrush(path.Fill)}, Thickness={path.StrokeThickness}, Visible={path.IsVisible}, Opacity={path.Opacity}");
            var diagnostic = $"{kind}/{preset}/{strokeStyle}: RedStrokePixels={strokePixels}, LimeFillPixels={fillPixels}, StartHeadPixels={startHeadPixels}, CalibrationPixels={calibrationPixels}, PresenterCalibrationPixels={presenterCalibrationPixels}, PresenterCalibrationActual={presenterCalibration.Bounds.Width}x{presenterCalibration.Bounds.Height}, PresenterCalibrationDesired={presenterCalibration.DesiredSize}, Bitmap={bitmap.PixelWidth}x{bitmap.PixelHeight}, PresenterActual={presenter.Bounds.Width}x{presenter.Bounds.Height}, RootActual={root.Bounds.Width}x{root.Bounds.Height}. {string.Join("; ", paths)}";
            var renderedCorrectly = strokePixels >= 100 && (kind == DaisyTourAnnotationKind.Line || fillPixels >= 20)
                && (preset != DaisyTourAnnotationPreset.TwoEndedArrow || startHeadPixels >= 20);
            Assert.IsTrue(renderedCorrectly, diagnostic);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await ArrangeAsync(root);
        }
    }

    private static int CountRenderedColor(byte[] pixels, RenderedBitmap bitmap, Rect region, Color expected)
    {
        var left = Math.Clamp((int)Math.Floor(region.Left * bitmap.PixelWidth / 600), 0, bitmap.PixelWidth);
        var right = Math.Clamp((int)Math.Ceiling(region.Right * bitmap.PixelWidth / 600), 0, bitmap.PixelWidth);
        var top = Math.Clamp((int)Math.Floor(region.Top * bitmap.PixelHeight / 400), 0, bitmap.PixelHeight);
        var bottom = Math.Clamp((int)Math.Ceiling(region.Bottom * bitmap.PixelHeight / 400), 0, bitmap.PixelHeight);
        var matches = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = (y * bitmap.PixelWidth + x) * 4;
                if (pixels[offset + 3] >= 240 && Math.Abs(pixels[offset] - expected.B) <= 20 &&
                    Math.Abs(pixels[offset + 1] - expected.G) <= 20 && Math.Abs(pixels[offset + 2] - expected.R) <= 20)
                    matches++;
            }
        }
        return matches;
    }

    // Segment endpoints form a polygon inside convex corners and outward-bulging scallops.
    private static List<Point> ContourPoints(Path path)
    {
        var figure = ((PathGeometry)path.Data!).Figures!.Single();
        var points = new List<Point> { figure.StartPoint };
        foreach (var segment in figure.Segments!)
        {
            points.Add(segment switch
            {
                LineSegment line => line.Point,
                ArcSegment arc => arc.Point,
                QuadraticBezierSegment curve => curve.Point2,
                _ => throw new AssertFailedException($"Unexpected contour segment {segment.GetType().Name}.")
            });
        }
        return points;
    }

    private static bool IsInside(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
        {
            var (a, b) = (polygon[index], polygon[previous]);
            if (a.Y > point.Y != b.Y > point.Y && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static string DescribeBrush(IBrush? brush) => brush is ISolidColorBrush solid
        ? $"#{solid.Color.A:X2}{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : brush?.GetType().Name ?? "null";

    private sealed class PreparationGate
    {
        internal TaskCompletionSource Entered { get; } = NewCompletion();
        internal TaskCompletionSource Release { get; } = NewCompletion();
        internal CancellationToken Token { get; private set; }

        internal Task PrepareAsync(DaisyTourStep _, CancellationToken token)
        {
            Token = token;
            Entered.TrySetResult();
            return Release.Task;
        }
    }

    private static void AssertRetainedCard(DaisyTour tour, Border card, DaisyTourStep previous, int index, Rect previousBounds)
    {
        Assert.IsTrue(tour.IsTransitioning);
        Assert.IsTrue(card.IsVisible);
        Assert.AreSame(previous, tour.CurrentStep);
        Assert.AreEqual(index, tour.CurrentIndex);
        Assert.AreEqual(index, tour.Progress.SelectedIndex);
        Assert.AreEqual(previousBounds, tour.CardBounds);
        Assert.AreEqual(previousBounds.X, Canvas.GetLeft(card), 0.001, "Preparation must retain the last valid card X coordinate.");
        Assert.AreEqual(previousBounds.Y, Canvas.GetTop(card), 0.001, "Preparation must retain the last valid card Y coordinate.");
        Assert.IsTrue(Descendants<TextBlock>(card).Any(text => text.Text == previous.Title));
        var count = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            FloweryLocalization.GetStringInternal("Tour_StepCount"), index + 1, tour.Steps.Count);
        Assert.IsTrue(Descendants<TextBlock>(card).Any(text => text.Text == count));
    }

    private static void AssertPlacedCard(DaisyTour tour, Border card, Control target)
    {
        Assert.IsTrue(card.IsVisible);
        Assert.AreEqual(tour.CardBounds.X, Canvas.GetLeft(card), 0.001);
        Assert.AreEqual(tour.CardBounds.Y, Canvas.GetTop(card), 0.001);
        Assert.IsTrue(tour.CardBounds.Width > 0 && tour.CardBounds.Height > 0);
        AssertTargetBounds(tour, target);
    }

    // WinUI waits for CompositionTarget.Rendering; Avalonia asks the window for its next frame.
    private static async Task RenderFrameAsync(Control root)
    {
        var rendered = NewCompletion();
        root.InvalidateArrange();
        root.UpdateLayout();
        TopLevel.GetTopLevel(root)!.RequestAnimationFrame(_ => rendered.TrySetResult());
        await rendered.Task.WaitAsync(Timeout);
    }

    private static async Task StartGalleryTourAsync(TourExamples page, DaisyTour tour, string buttonKey = "Gallery_Tour_Start")
    {
        var opened = NewCompletion();
        PropertyChangedEventHandler changed = (_, _) =>
        {
            if (tour.IsOpen && !tour.IsTransitioning && tour.CurrentIndex == 0) opened.TrySetResult();
        };
        EventHandler<DaisyTourClosedEventArgs> failed = (_, args) => opened.TrySetException(
            new InvalidOperationException($"Gallery tour closed while opening: {args.Reason}, Step={args.StepId}, " +
                $"Annotation={args.AnnotationId}, Target={args.Target?.GetType().Name}, Error={args.Error?.Message}", args.Error));
        // AvaloniaObject raises INotifyPropertyChanged for the tour state properties.
        ((INotifyPropertyChanged)tour).PropertyChanged += changed;
        tour.Closed += failed;
        try
        {
            var button = Descendants<DaisyButton>(page).Single(control =>
                Equals(control.Content, GalleryLocalization.GetString(buttonKey)));
            var peer = ControlAutomationPeer.CreatePeerForElement(button);
            Assert.IsNotNull(peer);
            var invoke = peer.GetProvider<IInvokeProvider>();
            Assert.IsNotNull(invoke);
            invoke.Invoke();
            await opened.Task.WaitAsync(Timeout);
        }
        finally
        {
            ((INotifyPropertyChanged)tour).PropertyChanged -= changed;
            tour.Closed -= failed;
        }
    }

    private static Border CreateTarget() => DaisyTourTestHelpers.CreateTarget(new Thickness(24));

    private static Grid CreateRoot(DaisyTour tour, params Control[] children)
    {
        tour.Closed += (_, args) => tour.Tag = args;
        return DaisyTourTestHelpers.CreateRoot(tour, children);
    }

    // A step that shows its targets has no warning; a test that expects one names the problem.
    private static void AssertActive(DaisyTour tour, DaisyTourStep step, int index, DaisyTourTargetProblem? warning = null)
    {
        var diagnostic = tour.Tag is DaisyTourClosedEventArgs closed
            ? $"Last closure: Reason={closed.Reason}, Step={closed.StepId}, Error={closed.Error}"
            : "No closure notification received.";
        Assert.AreEqual(warning, tour.Warning, $"Warning of step {step.Id}. {diagnostic}");
        Assert.IsTrue(tour.IsOpen, diagnostic);
        Assert.IsFalse(tour.IsTransitioning, diagnostic);
        Assert.AreSame(step, tour.CurrentStep, diagnostic);
        Assert.AreEqual(index, tour.CurrentIndex, diagnostic);
        Assert.AreEqual(index, tour.Progress.SelectedIndex, diagnostic);
    }

    private static void AssertWarning(DaisyTour tour, DaisyTourTargetProblem? expected)
    {
        Assert.AreEqual(expected, tour.Warning, "warning state");
        var warning = Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourWarning");
        Assert.AreEqual(expected is not null, warning.IsVisible, "warning note");
        if (expected is { } problem)
            Assert.AreEqual(FloweryLocalization.GetStringInternal(problem == DaisyTourTargetProblem.Preparation ? "Tour_PreparationFailed" : "Tour_TargetUnavailable"),
                ((TextBlock)warning.Child!).Text, "warning text");
    }

    private static Border Card(DaisyTour tour) =>
        Descendants<Border>(GetSurface(tour)).Single(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard");

    private static void AssertClosed(DaisyTour tour)
    {
        Assert.IsFalse(tour.IsOpen);
        Assert.IsFalse(tour.IsTransitioning);
        Assert.IsNull(tour.CurrentStep);
        Assert.AreEqual(-1, tour.CurrentIndex);
    }

    private static void AssertTargetBounds(DaisyTour tour, Control target)
    {
        var expected = BoundsInRoot(target);
        var actual = tour.TargetBounds;
        Assert.AreEqual(expected.X, actual.X, 1);
        Assert.AreEqual(expected.Y, actual.Y, 1);
        Assert.AreEqual(expected.Width, actual.Width, 1);
        Assert.AreEqual(expected.Height, actual.Height, 1);
    }

    private static Rect Frame(DaisyTour tour, Rect bounds)
    {
        var margin = tour.HighlightPadding + 1;
        return new Rect(bounds.X - margin, bounds.Y - margin, bounds.Width + 2 * margin, bounds.Height + 2 * margin);
    }

    // The window plays the part of WinUI's XamlRoot content; the tour overlay covers it from its origin.
    private static Rect BoundsInRoot(Control element) =>
        TransformBounds(element, TopLevel.GetTopLevel(element)!, new Rect(0, 0, element.Bounds.Width, element.Bounds.Height));

    private static Size RootSize(Visual element) => TopLevel.GetTopLevel(element)!.ClientSize;

    private static Rect TransformBounds(Visual element, Visual target, Rect bounds) =>
        element.TransformToVisual(target) is { } transform
            ? bounds.TransformToAABB(transform)
            : throw new AssertFailedException($"{element.GetType().Name} and {target.GetType().Name} share no visual root.");

    private static IEnumerable<T> Descendants<T>(Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static string DescribeScroll(string name, ScrollViewer viewer) =>
        $"{name}: loaded={viewer.IsLoaded}, offset={viewer.Offset.Y}, viewport={viewer.Viewport.Height}, " +
        $"extent={viewer.Extent.Height}, scrollable={viewer.ScrollBarMaximum.Y}, actual={viewer.Bounds.Height}";

    // WinUI's ChangeView without animation; Avalonia takes the offset directly and clamps it to the extent.
    // Like ChangeView, the result tells whether the view changed.
    private static bool ChangeView(ScrollViewer viewer, double? horizontal, double? vertical)
    {
        var before = viewer.Offset;
        viewer.Offset = new Vector(horizontal ?? viewer.Offset.X, vertical ?? viewer.Offset.Y);
        return viewer.Offset != before;
    }

    private static void AssertFits(Control child, Control parent)
    {
        var bounds = TransformBounds(child, parent, new Rect(0, 0, child.Bounds.Width, child.Bounds.Height));
        Assert.IsTrue(bounds.X >= -1 && bounds.Right <= parent.Bounds.Width + 1, "Target must fit horizontally in each scroll viewport.");
        Assert.IsTrue(bounds.Y >= -1 && bounds.Bottom <= parent.Bounds.Height + 1, "Target must fit vertically in each scroll viewport.");
    }

    private static void AssertInViewport(Rect bounds, Size viewport, string diagnostic)
    {
        Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0, diagnostic);
        Assert.IsTrue(bounds.Left >= -0.01 && bounds.Top >= -0.01 && bounds.Right <= viewport.Width + 0.01 &&
            bounds.Bottom <= viewport.Height + 0.01, $"{diagnostic} must fit in the overlay. Bounds={bounds}, Viewport={viewport}");
    }

    private static void AssertEndpoints(Path path, Point start, Point end)
    {
        Assert.IsInstanceOfType<PathGeometry>(path.Data);
        var figure = ((PathGeometry)path.Data).Figures!.Single();
        Assert.AreEqual(start, figure.StartPoint);
        Assert.IsInstanceOfType<LineSegment>(figure.Segments!.Last());
        Assert.AreEqual(end, ((LineSegment)figure.Segments!.Last()).Point);
    }

    // WinUI's DaisyResourceLookup.GetBorderBrush: the system text color with high contrast, else Base300.
    private static IBrush GetBorderBrush() =>
        DaisyResourceLookup.GetBrush(DaisyAccessibility.IsHighContrast ? "DaisyBaseContentBrush" : "DaisyBase300Brush")!;

    // Avalonia's TextBlock has no IsTextTrimmed; a trimmed line is collapsed.
    private static bool IsTextTrimmed(TextBlock block) => block.TextLayout.TextLines.Any(line => line.HasCollapsed);

    // A rendered bitmap as BGRA bytes, in the byte order of WinUI's pixel buffer. It is rendered at one pixel per DIP;
    // the tests map pixels to DIP through the bitmap size.
    private sealed record RenderedBitmap(byte[] Pixels, int PixelWidth, int PixelHeight);

    private static RenderedBitmap RenderBitmap(Visual visual)
    {
        using var rendered = RuntimeTestHelpers.Render(visual);
        using var bgra = rendered.Copy(SKColorType.Bgra8888) ?? throw new AssertFailedException("The rendered bitmap could not be converted.");
        return new RenderedBitmap(bgra.Bytes, bgra.Width, bgra.Height);
    }

    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task LoadAsync(Control root)
    {
        RuntimeTestHelpers.AttachToHost(root);
        await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
        await ArrangeAsync(root);
    }
}
