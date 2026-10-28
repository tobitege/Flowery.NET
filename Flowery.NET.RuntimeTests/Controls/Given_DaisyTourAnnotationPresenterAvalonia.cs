using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Covers the presenter and handle code that Avalonia needs differently from WinUI.</summary>
public class Given_DaisyTourAnnotationPresenterAvalonia
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Avalonia's Measure skips a valid parent whose child changed; without the invalidation, the surface keeps
    // the size of the previous text.
    [Fact]
    public async Task When_TheTextOrScaleOfAMeasuredSurfaceChanges_ItsSizeFollowsAtOnce()
    {
        var root = new Canvas { Width = 600, Height = 400 };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var annotation = new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.SpeechBubble, Tail = DaisyTourAnnotationTail.None, Text = "Short." };
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
            var shortText = Update(presenter, annotation, 1);
            await RuntimeTestHelpers.WaitForRenderAsync();

            annotation.Text = string.Join(" ", Enumerable.Repeat("This explanation is much longer and wraps.", 4));
            var longText = Update(presenter, annotation, 1);
            Assert.IsTrue(longText.Width > shortText.Width && longText.Height > shortText.Height,
                $"The longer text grows the surface at once: {shortText} -> {longText}.");
            Assert.AreEqual(longText.Width, ContentHost(presenter).DesiredSize.Width, 0.01);
            await RuntimeTestHelpers.WaitForRenderAsync();

            annotation.Text = "Short.";
            var shortAgain = Update(presenter, annotation, 1);
            Assert.AreEqual(shortText.Width, shortAgain.Width, 0.01, "The surface shrinks back with the text.");
            Assert.AreEqual(shortText.Height, shortAgain.Height, 0.01);
            await RuntimeTestHelpers.WaitForRenderAsync();

            var scaled = Update(presenter, annotation, 1.3);
            Assert.IsTrue(scaled.Width > shortText.Width && scaled.Height > shortText.Height,
                $"A larger scale grows the surface at once: {shortText} -> {scaled}.");
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    // Path markup parses to a geometry without figures; its contour, not its bounds, decides where it meets the ends.
    [Fact]
    public async Task When_ACustomPathComesFromAsymmetricMarkup_ItsCourseMeetsBothAnchors()
    {
        var root = new Canvas { Width = 600, Height = 400 };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var annotation = new DaisyTourAnnotation
        {
            Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Custom,
            Geometry = Geometry.Parse("M0,0 C20,-30 30,-30 50,0"),
            SourceAnchor = DaisyTourAnchor.Center, TargetAnchor = DaisyTourAnchor.Center
        };
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
            presenter.Update(annotation, new Rect(100, 200, 40, 40), new Rect(400, 200, 40, 40),
                Brushes.Red, Brushes.Lime, Brushes.Blue);
            var source = new Point(120, 220);
            var target = new Point(420, 220);
            var body = (Avalonia.Controls.Shapes.Path)presenter.Children[1];
            var route = Assert.IsInstanceOfType<GeometryGroup>(body.Data);
            var matrix = route.Transform!.Value;
            Assert.IsTrue(annotation.Geometry!.TryGetPointAtDistance(0, out var start));
            Assert.IsTrue(annotation.Geometry.TryGetPointAtDistance(annotation.Geometry.ContourLength, out var end));
            AssertNear(source, start.Transform(matrix), "The drawn course starts at the source anchor.");
            AssertNear(target, end.Transform(matrix), "The drawn course ends at the target anchor.");
            AssertNear(source, presenter.Course[0], "The course starts at the source anchor.");
            AssertNear(target, presenter.Course[^1], "The course ends at the target anchor.");
            Assert.IsTrue(presenter.Course.Count > 2, "The course follows the curve, not the chord.");
            Assert.IsTrue(presenter.Course.Min(point => point.Y) < 220 - 60,
                "The curve bulges away from the chord, so heads and hit area follow it.");
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    // Real pointer events: Avalonia routes them differently and has no canceled pointer event.
    [Fact]
    public Task When_AHandleIsDraggedWithThePointer_TheDragIsOneUndoActionWithTheFinalValue() =>
        InEditorAsync(async (editor, tour) =>
        {
            var (handle, layer, origin) = CurvatureHandle(editor);
            var geometry = tour.AnnotationPresenters[0].EditGeometry!;
            var before = editor.ExportJson();
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, layer, origin, 0, Pressed, KeyModifiers.None, 1));
            Move(handle, pointer, layer, origin + new Point(0, 10));
            Move(handle, pointer, layer, origin + new Point(0, 24));
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, layer, origin + new Point(0, 24), 0, Released,
                KeyModifiers.None, MouseButton.Left));
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(origin + new Point(0, 24), geometry, origin), Curvature(editor), 0.001);
            var after = editor.ExportJson();
            Assert.AreNotEqual(before, after);
            editor.Undo();
            Assert.AreEqual(before, editor.ExportJson(), "One undo action takes back the whole drag.");
            editor.Redo();
            Assert.AreEqual(after, editor.ExportJson());
        });

    [Fact]
    public Task When_TheCaptureIsLostDuringADrag_TheDragEndsAtTheLastMovedPoint() =>
        InEditorAsync(async (editor, tour) =>
        {
            var (handle, layer, origin) = CurvatureHandle(editor);
            var geometry = tour.AnnotationPresenters[0].EditGeometry!;
            var before = editor.ExportJson();
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            handle.RaiseEvent(new PointerPressedEventArgs(handle, pointer, layer, origin, 0, Pressed, KeyModifiers.None, 1));
            Assert.AreSame(handle, pointer.Captured, "The handle captures the pointer.");
            Move(handle, pointer, layer, origin + new Point(0, 18));
            pointer.Capture(null);
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(origin + new Point(0, 18), geometry, origin), Curvature(editor), 0.001);
            // A release that still arrives belongs to no drag.
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, layer, origin + new Point(0, 40), 0, Released,
                KeyModifiers.None, MouseButton.Left));
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(origin + new Point(0, 18), geometry, origin), Curvature(editor), 0.001);
            editor.Undo();
            Assert.AreEqual(before, editor.ExportJson(), "The drag that ended with the capture is one undo action.");
        });

    private static readonly PointerPointProperties Pressed = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
    private static readonly PointerPointProperties Moved = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
    private static readonly PointerPointProperties Released = new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);

    private static void Move(Button handle, IPointer pointer, Visual layer, Point point) =>
        handle.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, handle, pointer, layer, point, 0, Moved, KeyModifiers.None));

    private static Rect Update(DaisyTourAnnotationPresenter presenter, DaisyTourAnnotation annotation, double scale)
    {
        presenter.Update(annotation, new Rect(200, 150, 120, 40), new Rect(520, 340, 40, 30),
            Brushes.Red, Brushes.Lime, Brushes.Blue, scale: scale);
        Assert.IsNotNull(presenter.SelectionBounds);
        return presenter.SelectionBounds!.Value;
    }

    private static Border ContentHost(DaisyTourAnnotationPresenter presenter) => (Border)presenter.Children[4];

    private static void AssertNear(Point expected, Point actual, string message)
    {
        Assert.AreEqual(expected.X, actual.X, 0.5, message);
        Assert.AreEqual(expected.Y, actual.Y, 0.5, message);
    }

    private static (Button Handle, DaisyTourHandleLayer Layer, Point Origin) CurvatureHandle(DaisyTourEditor editor)
    {
        var layer = editor.HandleLayer;
        var handle = layer.Children.OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "DaisyTourHandleCurvature");
        Assert.IsTrue(handle.IsVisible);
        return (handle, layer, new Point(Canvas.GetLeft(handle) + handle.Width / 2, Canvas.GetTop(handle) + handle.Height / 2));
    }

    private static double Curvature(DaisyTourEditor editor) =>
        editor.DraftProfile!.Steps.Single().Annotations.Single().Style!.Values.Curvature!.Value;

    private static async Task InEditorAsync(Func<DaisyTourEditor, DaisyTour, Task> action)
    {
        var root = new Grid { Width = 752, Height = 480, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var source = new Button { Content = "Source", Width = 90, Height = 40, Margin = new Thickness(24, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var target = new Button { Content = "Target", Width = 90, Height = 40, Margin = new Thickness(250, 190, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var annotation = new DaisyTourAnnotation { Id = "arrow", Source = source, Target = target, Path = DaisyTourAnnotationPath.Curved, Curvature = 0.3 };
        var step = new DaisyTourStep { Id = "step", Target = target, Title = "Drag", Description = "A handle drag." };
        step.Annotations.Add(annotation);
        var tour = new DaisyTour { Id = "drag-test" };
        tour.Steps.Add(step);
        root.Children.Add(source);
        root.Children.Add(target);
        root.Children.Add(tour);
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
            await RuntimeTestHelpers.WaitForRenderAsync();
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            await RuntimeTestHelpers.WaitUntilAsync(() => tour.IsOpen && !tour.IsTransitioning && tour.AnnotationPresenters.Count == 1 &&
                tour.AnnotationPresenters[0].EditGeometry is not null, "the preview is laid out");
            await RuntimeTestHelpers.WaitForRenderAsync();
            await action(editor, tour);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }
}
