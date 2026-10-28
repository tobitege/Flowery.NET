using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;
using static Flowery.NET.RuntimeTests.Controls.DaisyTourTestHelpers;
using static Flowery.NET.RuntimeTests.Controls.TourEditorTestHelpers;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;
using DaisyIconSymbol = Flowery.Enums.DaisyIconSymbol;
using Path = Avalonia.Controls.Shapes.Path;

namespace Flowery.NET.RuntimeTests.Controls;

public class Given_DaisyTourEditorNavigation
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_PreviewNavigatesNextAndBack_InspectorAndHandlesFollowTheActiveStep()
    {
        var root = new Grid { Width = 752, Height = 480 };
        var source = new Button
        {
            Content = "Source", Width = 90, Height = 40, Margin = new Thickness(24, 30, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var target = new Button
        {
            Content = "Target", Width = 90, Height = 40, Margin = new Thickness(260, 220, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var firstAnnotation = new DaisyTourAnnotation
        {
            Id = "first-arrow", Source = source, Target = target, ArrowStyle = DaisyTourArrowStyle.Block,
            Path = DaisyTourAnnotationPath.Curved, BodyWidth = 5
        };
        var secondAnnotation = new DaisyTourAnnotation
        {
            Id = "second-arrow", Source = source, Target = target, ArrowStyle = DaisyTourArrowStyle.Block,
            Path = DaisyTourAnnotationPath.Bezier, BodyWidth = 9,
            ControlPoint1X = 0.2, ControlPoint1Y = -0.2, ControlPoint2X = 0.8, ControlPoint2Y = 0.2
        };
        var first = new DaisyTourStep { Id = "first", Target = target, Title = "First" };
        var second = new DaisyTourStep { Id = "second", Target = target, Title = "Second" };
        first.Annotations.Add(firstAnnotation);
        second.Annotations.Add(secondAnnotation);
        var tour = new DaisyTour { Id = "editor-navigation" };
        tour.Steps.Add(first);
        tour.Steps.Add(second);
        root.Children.Add(source);
        root.Children.Add(target);
        root.Children.Add(tour);
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
            await SettleAsync(root);
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            Assert.AreSame(first, editor.SelectedStep);
            Assert.AreSame(firstAnnotation, editor.SelectedAnnotation);
            AssertHandleVisibility(editor, "Curvature", true);
            AssertHandleVisibility(editor, "FirstControl", false);

            // These are the same entry points used by the preview card's Next and Back buttons.
            await tour.NextAsync().WaitAsync(Timeout);
            await SettleAsync(root);
            Assert.AreSame(second, tour.CurrentStep);
            Assert.AreSame(second, editor.SelectedStep);
            Assert.AreSame(secondAnnotation, editor.SelectedAnnotation);
            AssertHandleVisibility(editor, "Curvature", false);
            AssertHandleVisibility(editor, "FirstControl", true);
            AssertHandleVisibility(editor, "SecondControl", true);
            editor.SetValue("bodyWidth", 23d);
            var profile = editor.ExportProfile();
            Assert.AreEqual("second", profile.Steps.Single().Id);
            Assert.AreEqual("second-arrow", profile.Steps.Single().Annotations.Single().Id);
            Assert.AreEqual(23d, profile.Steps.Single().Annotations.Single().Style!.Values.BodyWidth);

            await tour.BackAsync().WaitAsync(Timeout);
            await SettleAsync(root);
            Assert.AreSame(first, tour.CurrentStep);
            Assert.AreSame(first, editor.SelectedStep);
            Assert.AreSame(firstAnnotation, editor.SelectedAnnotation);
            AssertHandleVisibility(editor, "Curvature", true);
            AssertHandleVisibility(editor, "FirstControl", false);
            Assert.AreEqual(5d, tour.AnnotationPresenters.Single().EditGeometry!.BodyWidth, 0.001);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await SettleAsync(root);
        }
    }

    [Fact]
    public async Task When_SurfaceAnnotationHasNoCourse_ItsSelectionAreaCoversTheRenderedSurface()
    {
        var root = new Grid { Width = 752, Height = 480 };
        var target = new Button
        {
            Content = "Target", Width = 90, Height = 40, Margin = new Thickness(160, 180, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var bubble = new DaisyTourAnnotation
        {
            Id = "bubble", Preset = DaisyTourAnnotationPreset.SpeechBubble,
            Text = "Select this explanation.", Width = 160, Height = 80
        };
        var step = new DaisyTourStep { Id = "surface", Target = target, Title = "Surface" };
        step.Annotations.Add(new DaisyTourAnnotation { Id = "arrow", Preset = DaisyTourAnnotationPreset.ThinArrow });
        step.Annotations.Add(bubble);
        var tour = new DaisyTour { Id = "surface-selection" };
        tour.Steps.Add(step);
        root.Children.Add(target);
        root.Children.Add(tour);
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
            await SettleAsync(root);
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            var presenter = tour.AnnotationPresenters[1];
            Assert.HasCount(0, presenter.Course);
            Assert.IsNotNull(presenter.SelectionBounds);
            var bounds = presenter.SelectionBounds.Value;
            Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0);
            var hitArea = editor.HandleLayer.Children.OfType<Path>().Single(path => path.Tag is int index && index == 1);
            Assert.IsInstanceOfType<RectangleGeometry>(hitArea.Data);
            Assert.AreEqual(bounds, ((RectangleGeometry)hitArea.Data).Rect);
            Assert.IsNotNull(hitArea.Fill, "The interior of the surface must receive pointer input.");
            Assert.IsTrue(hitArea.IsHitTestVisible);
            Assert.AreEqual(editor.HandleLayer.Width, hitArea.Width);
            Assert.AreEqual(editor.HandleLayer.Height, hitArea.Height);

            // This is the selection method used by the area's PointerPressed handler.
            editor.SelectAnnotation((int)hitArea.Tag!);
            Assert.AreSame(bubble, editor.SelectedAnnotation);
            editor.SetValue("width", 175d);
            Assert.AreEqual("bubble", editor.ExportProfile().Steps.Single().Annotations.Single().Id);
            AssertHandleVisibility(editor, "Source", false);
            AssertHandleVisibility(editor, "Target", false);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await SettleAsync(root);
        }
    }

    // WinUI compares Visibility values; Avalonia controls are visible or not.
    private static void AssertHandleVisibility(DaisyTourEditor editor, string kind, bool expected)
    {
        var handle = editor.HandleLayer.Children.OfType<Button>().Single(button =>
            AutomationProperties.GetAutomationId(button) == "DaisyTourHandle" + kind);
        Assert.AreEqual(expected, handle.IsVisible, kind);
    }

    [Fact]
    public async Task When_InspectorMovesOrCollapses_PreviewCardStaysAtBottomWithoutChangingPlaybackPosition()
    {
        var root = new Grid { Width = 752, Height = 480 };
        var target = new Button
        {
            Content = "Target", Width = 90, Height = 40, Margin = new Thickness(160, 180, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var step = new DaisyTourStep
        {
            Id = "position", Target = target, Title = "Position", CardPosition = DaisyTourCardPosition.TopRight
        };
        var tour = new DaisyTour { Id = "editor-card-position", CardPosition = DaisyTourCardPosition.Top };
        tour.Steps.Add(step);
        root.Children.Add(target);
        root.Children.Add(tour);
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
            await SettleAsync(root);
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            var originalProfile = editor.ExportJson();
            var viewport = DaisyTourLayout.VisibleArea(TopLevel.GetTopLevel(root)!);
            var cardWidth = tour.CardBounds.Width;
            Assert.AreEqual(viewport.Bottom - 12, tour.CardBounds.Bottom, 0.01);
            AssertCardAvoidance(tour, editor);

            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorMovePanel");
            Assert.AreEqual(viewport.Bottom - 12, tour.CardBounds.Bottom, 0.01);
            AssertCardAvoidance(tour, editor);

            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorTogglePanel");
            Assert.IsFalse(editor.IsExpanded);
            Assert.AreEqual(viewport.Bottom - 12, tour.CardBounds.Bottom, 0.01);
            Assert.AreEqual(viewport.Left + viewport.Width / 2, tour.CardBounds.Left + tour.CardBounds.Width / 2, 0.01);
            Assert.AreEqual(cardWidth, tour.CardBounds.Width, 0.01, "The inspector must not resize the card.");
            Assert.AreEqual(originalProfile, editor.ExportJson());
            Assert.AreEqual(DaisyTourCardPosition.Top, tour.CardPosition);
            Assert.AreEqual(DaisyTourCardPosition.TopRight, step.CardPosition);

            editor.Discard();
            await tour.StartAsync().WaitAsync(Timeout);
            Assert.AreEqual(viewport.Top + 12, tour.CardBounds.Top, 0.01);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await SettleAsync(root);
        }
    }

    [Fact]
    public async Task When_InspectorResizes_ItPreservesCardSizeAndHistoryActionsRemainAccessible()
    {
        var root = new Grid { Width = 752, Height = 480 };
        var target = new Button
        {
            Content = "Target", Width = 90, Height = 40, Margin = new Thickness(160, 80, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var tour = new DaisyTour { Id = "editor-resize" };
        tour.Steps.Add(new DaisyTourStep { Id = "resize", Target = target, Title = "Resize" });
        root.Children.Add(target);
        root.Children.Add(tour);
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
            await SettleAsync(root);
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            var original = editor.ExportJson();
            var initialWidth = editor.InspectorBounds.Width;
            var cardWidth = tour.CardBounds.Width;
            await ChangeGeometryAsync(tour, () => editor.ResizeInspectorBy(-40));
            Assert.AreEqual(initialWidth + 40, editor.InspectorBounds.Width, 0.01);
            AssertCardAvoidance(tour, editor);

            await ChangeGeometryAsync(tour, () => editor.ResizeInspectorBy(-10000));
            Assert.IsTrue(editor.InspectorBounds.Width <= 600);
            Assert.AreEqual(cardWidth, tour.CardBounds.Width, 0.01, "Widening the inspector must not shrink the card.");
            AssertCardAvoidance(tour, editor);
            await ChangeGeometryAsync(tour, () => editor.ResizeInspectorBy(10000));
            Assert.AreEqual(180d, editor.InspectorBounds.Width, 0.01);

            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorMovePanel");
            await ChangeGeometryAsync(tour, () => editor.ResizeInspectorBy(60));
            Assert.AreEqual(240d, editor.InspectorBounds.Width, 0.01);
            AssertCardAvoidance(tour, editor);
            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorTogglePanel");
            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorTogglePanel");
            Assert.AreEqual(240d, editor.InspectorBounds.Width, 0.01);
            Assert.AreEqual(original, editor.ExportJson(), "Panel resizing must not edit the tour profile.");
            Assert.IsFalse(editor.HasChanges);

            var undo = Descendants(editor).OfType<DaisyButton>().Single(button =>
                AutomationProperties.GetAutomationId(button) == "DaisyTourEditorUndo");
            var redo = Descendants(editor).OfType<DaisyButton>().Single(button =>
                AutomationProperties.GetAutomationId(button) == "DaisyTourEditorRedo");
            // WinUI shows SymbolIcon(Symbol.Undo/Redo) as the only content; the Avalonia buttons show the same
            // symbols as IconSymbol, without text content.
            Assert.AreEqual<DaisyIconSymbol?>(DaisyIconSymbol.Undo, undo.IconSymbol);
            Assert.AreEqual<DaisyIconSymbol?>(DaisyIconSymbol.Redo, redo.IconSymbol);
            Assert.IsNull(undo.Content, "The undo button shows only its icon.");
            Assert.IsNull(redo.Content, "The redo button shows only its icon.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(undo)));
            Assert.AreEqual<object?>(AutomationProperties.GetName(undo), ToolTip.GetTip(undo));
            Assert.AreEqual<object?>(AutomationProperties.GetName(redo), ToolTip.GetTip(redo));
            Assert.IsFalse(undo.IsEnabled);
            Assert.IsFalse(redo.IsEnabled);
            editor.SetValue("opacity", 0.4d);
            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorUndo");
            Assert.AreEqual(original, editor.ExportJson());
            Assert.IsTrue(redo.IsEnabled);
            await InvokeEditorButtonAsync(editor, tour, "DaisyTourEditorRedo");
            Assert.IsTrue(editor.HasChanges);
            Assert.AreEqual(240d, editor.InspectorBounds.Width, 0.01);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await SettleAsync(root);
        }
    }

    private static void AssertCardAvoidance(DaisyTour tour, DaisyTourEditor editor)
    {
        var viewport = DaisyTourLayout.VisibleArea(TopLevel.GetTopLevel(tour)!);
        var card = tour.CardBounds;
        var inspector = editor.InspectorBounds;
        Assert.IsTrue(card.Left >= viewport.Left + 12 - 0.01);
        Assert.IsTrue(card.Right <= viewport.Right - 12 + 0.01);
        var freeWidth = Math.Max(inspector.Left - viewport.Left - 24, viewport.Right - inspector.Right - 24);
        if (freeWidth >= card.Width)
        {
            var overlap = card.Intersect(inspector);
            Assert.IsTrue(overlap.Width <= 0 || overlap.Height <= 0,
                "The card must avoid the inspector when there is enough room beside it.");
        }
    }

    private static async Task InvokeEditorButtonAsync(DaisyTourEditor editor, DaisyTour tour, string id)
    {
        var button = Descendants(editor).OfType<DaisyButton>().Single(item => AutomationProperties.GetAutomationId(item) == id);
        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        Assert.IsNotNull(peer);
        var invoke = peer.GetProvider<IInvokeProvider>();
        Assert.IsNotNull(invoke);
        await ChangeGeometryAsync(tour, invoke.Invoke);
    }

    private static IEnumerable<Visual> Descendants(Visual parent) => parent.GetVisualDescendants();
}
