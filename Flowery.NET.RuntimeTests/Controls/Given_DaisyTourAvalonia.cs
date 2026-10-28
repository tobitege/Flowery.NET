using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;
using Path = Avalonia.Controls.Shapes.Path;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Behavior of DaisyTour that depends on Avalonia mechanisms which replace the WinUI ones.</summary>
public class Given_DaisyTourAvalonia
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_TheTourSizeDiffersFromTheGlobalSize_TheProgressStepsFollowTheTour()
    {
        // Regression: the global size manager gave every loaded progress step the global size.
        var previousSize = FlowerySizeManager.CurrentSize;
        var (tour, root) = CreateTour(3);
        tour.Size = DaisySize.ExtraLarge;
        try
        {
            FlowerySizeManager.ApplySize(DaisySize.Small);
            await StartAsync(root, tour);
            AssertProgressSize(tour, DaisySize.ExtraLarge, 32);
            // A later change of the global size leaves the steps at the tour's size.
            FlowerySizeManager.ApplySize(DaisySize.Medium);
            await ArrangeAsync(root);
            AssertProgressSize(tour, DaisySize.ExtraLarge, 32);
        }
        finally
        {
            FlowerySizeManager.ApplySize(previousSize);
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_AProgressStepIsInvoked_TheTourMovesThereAlsoAfterTheStepIsTemplatedAgain()
    {
        // Regression: a navigation button that is lost, covered or not focusable after a new template.
        var (tour, root) = CreateTour(3);
        try
        {
            await StartAsync(root, tour);
            var third = ProgressButton(tour, 3);
            Assert.IsTrue(third.Focusable && third.IsEffectivelyEnabled, "The progress step can take the focus.");
            await InvokeAsync(tour, third, 2);

            var first = tour.Progress.Items.OfType<DaisyStepItem>().First();
            first.Template = new FuncControlTemplate<DaisyStepItem>((_, scope) =>
                new Panel { Name = "PART_Root", Children = { new Grid { Name = "PART_HorizontalLayout" }.RegisterInNameScope(scope) } }
                    .RegisterInNameScope(scope));
            await ArrangeAsync(root);
            var button = ProgressButton(tour, 1);
            var templateRoot = (Panel)first.GetVisualChildren().Single();
            Assert.AreSame(button, templateRoot.Children[0], "The button lies under the indicator of the new template.");
            Assert.IsFalse(templateRoot.Children.OfType<Grid>().Single().IsHitTestVisible, "The layout lets the pointer through to the button.");
            await InvokeAsync(tour, button, 0);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_ManyStepsScroll_TheCurrentStepIsCentered()
    {
        // Regression: BringIntoView only scrolls the step to the nearest edge; WinUI centers it.
        var (tour, root) = CreateTour(24);
        try
        {
            await StartAsync(root, tour);
            await InvokeAsync(tour, ProgressButton(tour, 12), 11);
            await ArrangeAsync(root);
            var item = tour.Progress.Items.OfType<DaisyStepItem>().ElementAt(11);
            var viewer = tour.Progress.FindAncestorOfType<ScrollViewer>()!;
            Assert.IsTrue(viewer.Extent.Width > viewer.Viewport.Width + 1, "The progress scrolls.");
            var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, 0), viewer)!.Value;
            Assert.AreEqual(viewer.Viewport.Width / 2, center.X, 1, "The current step is centered.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AnAppControlTakesTheFocus_TheTourKeepsIt()
    {
        // Regression: the focus trap misses focus moves whose GettingFocus a handler marked as handled.
        var outside = new Button { Content = "Outside", Margin = new Thickness(300, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var preparation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (tour, root) = CreateTour(2, outside);
        tour.Steps[1].PrepareAsync = (_, token) => preparation.Task.WaitAsync(token);
        root.AddHandler(InputElement.GettingFocusEvent, static (_, args) => args.Handled = true, RoutingStrategies.Bubble);
        try
        {
            await StartAsync(root, tour);
            outside.Focus();
            Assert.AreSame(Find<DaisyButton>(tour, "DaisyTourNext"), FocusedElement(root), "An open step keeps the focus on Next.");

            var next = tour.NextAsync();
            await WaitUntilAsync(() => tour.IsTransitioning, "the next step prepares");
            outside.Focus();
            Assert.AreSame(Surface(tour), FocusedElement(root), "A preparing step keeps the focus on the tour surface.");
            preparation.SetResult();
            await next.WaitAsync(Timeout);
            Assert.AreEqual(1, tour.CurrentIndex);
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_AnOverlayPopupTakesTheFocus_OnlyTheEditorLetsItKeepIt()
    {
        // Regression: the editor's flyouts in the overlay layer lose the focus to the tour, or the playback tour
        // lets an overlay popup of the app take it.
        var field = new TextBox { Width = 120 };
        var popup = new Popup { ShouldUseOverlayLayer = true, Child = field };
        var (tour, root) = CreateTour(2, popup);
        tour.Id = "popup";
        var editor = new DaisyTourEditor();
        try
        {
            await StartAsync(root, tour);
            popup.IsOpen = true;
            await ArrangeAsync(root);
            field.Focus();
            Assert.AreSame(Find<DaisyButton>(tour, "DaisyTourNext"), FocusedElement(root), "Playback keeps the focus in the tour.");

            popup.IsOpen = false;
            tour.Cancel();
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            await ArrangeAsync(root);
            popup.IsOpen = true;
            await ArrangeAsync(root);
            field.Focus();
            Assert.AreSame(field, FocusedElement(root), "The editor's overlay popup keeps the focus.");
        }
        finally
        {
            popup.IsOpen = false;
            editor.Discard();
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_HighlightFramesTouch_TheyShareOneCutout()
    {
        // Regression: Avalonia's Intersect returns an empty rectangle for adjacent frames, which kept them apart.
        // With the default padding of 6, frames of targets 12 apart meet at one edge.
        var first = CreateTarget(24);
        var second = CreateTarget(136);
        var tour = new DaisyTour();
        var step = new DaisyTourStep { Id = "touching", Target = first };
        step.Annotations.Add(new DaisyTourAnnotation { Id = "link", Source = first, Target = second });
        tour.Steps.Add(step);
        var root = CreateRoot(tour, first, second);
        try
        {
            await StartAsync(root, tour);
            var paths = Surface(tour).GetVisualChildren().OfType<Path>().ToArray();
            var backdrop = Assert.IsInstanceOfType<GeometryGroup>(paths[0].Data);
            var outline = Assert.IsInstanceOfType<GeometryGroup>(paths[1].Data);
            Assert.HasCount(1, outline.Children!, "Both frames form one outline.");
            Assert.HasCount(2, backdrop.Children!, "The backdrop has the window and one cutout.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    [Fact]
    public async Task When_TheGlobalSizeChanges_ATourWhoseProfileInheritsTheSizeFollowsIt()
    {
        // Regression: a tour with a size of its own got no property change and kept the old global size.
        var previousSize = FlowerySizeManager.CurrentSize;
        var (tour, root) = CreateTour(2);
        tour.Size = DaisySize.Small;
        tour.Id = "inherits-size";
        tour.StyleProfile = new DaisyTourStyleProfile { TourId = tour.Id, Style = new DaisyTourStyleOverride { Inherit = ["size"] } };
        try
        {
            FlowerySizeManager.ApplySize(DaisySize.Large);
            await StartAsync(root, tour);
            Assert.AreEqual(DaisySize.Large, Find<DaisyButton>(tour, "DaisyTourNext").Size);
            FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
            await WaitUntilAsync(() => Find<DaisyButton>(tour, "DaisyTourNext").Size == DaisySize.ExtraSmall, "the card follows the global size");
        }
        finally
        {
            FlowerySizeManager.ApplySize(previousSize);
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public async Task When_ControlsDoNotUseTheGlobalSizeByDefault_AnInheritedProfileSizeIsMedium()
    {
        // Regression: the profile fallback used the global size although the size manager supplies it to no control.
        var previousSize = FlowerySizeManager.CurrentSize;
        var previousDefault = FlowerySizeManager.UseGlobalSizeByDefault;
        var (tour, root) = CreateTour(2);
        tour.Id = "inherits-size";
        tour.StyleProfile = new DaisyTourStyleProfile { TourId = tour.Id, Style = new DaisyTourStyleOverride { Inherit = ["size"] } };
        try
        {
            FlowerySizeManager.UseGlobalSizeByDefault = false;
            FlowerySizeManager.ApplySize(DaisySize.Large);
            await StartAsync(root, tour);
            Assert.AreEqual(DaisySize.Medium, Find<DaisyButton>(tour, "DaisyTourNext").Size);
        }
        finally
        {
            FlowerySizeManager.UseGlobalSizeByDefault = previousDefault;
            FlowerySizeManager.ApplySize(previousSize);
            await CleanupAsync(root, tour);
        }
    }

    [Fact]
    public void When_AViewHasSafeAreaInsets_TheVisibleAreaLeavesThemOut()
    {
        // Regression: on a phone the card and annotations could be placed under the system bars.
        var content = new Rect(0, 0, 400, 800);
        Assert.AreEqual(new Rect(0, 24, 400, 728), DaisyTourLayout.SafeArea(content, new Thickness(0, 24, 0, 48)));
        Assert.AreEqual(new Rect(16, 0, 368, 800), DaisyTourLayout.SafeArea(content, new Thickness(16, 0)));
        Assert.AreEqual(content, DaisyTourLayout.SafeArea(content, default));
        Assert.AreEqual(content, DaisyTourLayout.SafeArea(content, new Thickness(0, 500, 0, 400)), "Insets that leave nothing keep the content.");
    }

    private static (DaisyTour Tour, Grid Root) CreateTour(int steps, params Control[] extra)
    {
        var target = CreateTarget(24);
        var tour = new DaisyTour();
        for (var index = 0; index < steps; index++)
            tour.Steps.Add(new DaisyTourStep { Id = "step" + index, Target = target, Title = "Step " + index });
        return (tour, CreateRoot(tour, [target, .. extra]));
    }

    private static Border CreateTarget(double left) => new()
    {
        Width = 100, Height = 40, Margin = new Thickness(left, 24, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        Child = new TextBlock { Text = "Tour target" }
    };

    private static Grid CreateRoot(DaisyTour tour, params Control[] children)
    {
        var root = new Grid { Width = 600, Height = 400 };
        foreach (var child in children) root.Children.Add(child);
        root.Children.Add(tour);
        return root;
    }

    private static async Task StartAsync(Grid root, DaisyTour tour)
    {
        RuntimeTestHelpers.AttachToHost(root);
        await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
        await ArrangeAsync(root);
        await tour.StartAsync().WaitAsync(Timeout);
        await ArrangeAsync(root);
        Assert.IsTrue(tour.IsOpen && !tour.IsTransitioning, "The tour is open.");
    }

    private static async Task InvokeAsync(DaisyTour tour, Button button, int index)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        var invoke = peer.GetProvider<IInvokeProvider>();
        Assert.IsNotNull(invoke);
        invoke.Invoke();
        await WaitUntilAsync(() => tour.CurrentIndex == index && !tour.IsTransitioning, $"step {index + 1} is shown");
    }

    private static void AssertProgressSize(DaisyTour tour, DaisySize size, double circle)
    {
        Assert.AreEqual(size, tour.Progress.Size);
        foreach (var item in tour.Progress.Items.OfType<DaisyStepItem>())
        {
            Assert.AreEqual(size, item.Size, "Every progress step has the tour's size.");
            var indicator = item.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_HCircle");
            Assert.AreEqual(circle, indicator.Width, 0.01, "The indicator has the size of the tour's size tier.");
        }
    }

    private static Button ProgressButton(DaisyTour tour, int number) =>
        tour.Progress.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourStep" + number);

    private static T Find<T>(DaisyTour tour, string automationId) where T : Control =>
        Surface(tour).GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    // The tour shows its surface, inside a theme variant scope, in the overlay layer of the window.
    private static Canvas Surface(DaisyTour tour) =>
        OverlayLayer.GetOverlayLayer(tour)!.Children.OfType<ThemeVariantScope>()
            .Select(scope => scope.Child).OfType<Canvas>()
            .Single(surface => surface.GetVisualDescendants().OfType<Border>().Any(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard"));

    private static IInputElement? FocusedElement(Visual root) => TopLevel.GetTopLevel(root)!.FocusManager!.GetFocusedElement();

    private static Task WaitUntilAsync(Func<bool> condition, string description) =>
        RuntimeTestHelpers.WaitUntilAsync(condition, description, Timeout);

    private static async Task ArrangeAsync(Control root)
    {
        root.UpdateLayout();
        for (var turn = 0; turn < 2; turn++)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() => ready.TrySetResult(), DispatcherPriority.Background);
            await ready.Task.WaitAsync(Timeout);
        }
    }

    private static async Task CleanupAsync(Control root, DaisyTour tour)
    {
        tour.Cancel();
        RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
        await ArrangeAsync(root);
    }
}
