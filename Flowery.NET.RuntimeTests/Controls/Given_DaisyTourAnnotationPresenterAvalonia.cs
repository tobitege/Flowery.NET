using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;
using static Flowery.NET.RuntimeTests.Controls.DaisyTourTestHelpers;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Covers the presenter and handle code that Avalonia needs differently from WinUI.</summary>
public class Given_DaisyTourAnnotationPresenterAvalonia
{
    private static readonly PointerPointProperties Released = new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);

    // Avalonia's Measure skips a valid parent whose child changed; without the invalidation, the surface keeps
    // the size of the previous text. A new scale changes the padding and limits of the host itself, so the host
    // measures again without it; only the text change depends on the invalidation.
    [Fact]
    public async Task When_TheTextOfAMeasuredSurfaceChanges_ItsSizeFollowsAtOnce()
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
            Assert.AreEqual(longText.Width, presenter.ContentHost.DesiredSize.Width, 0.01);
            await RuntimeTestHelpers.WaitForRenderAsync();

            annotation.Text = "Short.";
            var shortAgain = Update(presenter, annotation, 1);
            Assert.AreEqual(shortText.Width, shortAgain.Width, 0.01, "The surface shrinks back with the text.");
            Assert.AreEqual(shortText.Height, shortAgain.Height, 0.01);
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
            var route = Assert.IsInstanceOfType<GeometryGroup>(presenter.Body.Data);
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

    // A closed contour has no ends; it meets the anchors at the middle of its left and right bounds edges, and the
    // course between them is the chord. Without the closed check, its start and end coincide and no course is fitted.
    [Fact]
    public async Task When_ACustomPathIsClosed_ItMeetsTheAnchorsAtItsBoundsEdges()
    {
        var root = new Canvas { Width = 600, Height = 400 };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var geometries = new (string Name, Geometry Geometry)[]
        {
            ("closed markup", Geometry.Parse("M0,0 L50,-40 L100,0 Z")),
            ("ellipse", new EllipseGeometry { Center = new Point(50, 20), RadiusX = 50, RadiusY = 20 })
        };
        var source = new Point(120, 220);
        var target = new Point(420, 220);
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
            foreach (var (name, geometry) in geometries)
            {
                var annotation = new DaisyTourAnnotation
                {
                    Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Custom, Geometry = geometry,
                    SourceAnchor = DaisyTourAnchor.Center, TargetAnchor = DaisyTourAnchor.Center
                };
                presenter.Update(annotation, new Rect(100, 200, 40, 40), new Rect(400, 200, 40, 40),
                    Brushes.Red, Brushes.Lime, Brushes.Blue);
                var route = Assert.IsInstanceOfType<GeometryGroup>(presenter.Body.Data, $"{name}: the geometry is fitted between the anchors.");
                var matrix = route.Transform!.Value;
                var bounds = geometry.Bounds;
                AssertNear(source, new Point(bounds.Left, bounds.Top + bounds.Height / 2).Transform(matrix),
                    $"{name}: the middle of the left bounds edge meets the source anchor.");
                AssertNear(target, new Point(bounds.Right, bounds.Top + bounds.Height / 2).Transform(matrix),
                    $"{name}: the middle of the right bounds edge meets the target anchor.");
                Assert.AreEqual(2, presenter.Course.Count, $"{name}: the course is the chord between the anchors.");
                AssertNear(source, presenter.Course[0], $"{name}: the course starts at the source anchor.");
                AssertNear(target, presenter.Course[1], $"{name}: the course ends at the target anchor.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    // The figure of a path geometry is in the geometry's own coordinates; the drawing applies its transform, so the
    // flattened course must too, or a rotated geometry misses both anchors.
    [Fact]
    public async Task When_ACustomPathGeometryHasATransform_ItsCourseMeetsBothAnchors()
    {
        var root = new Canvas { Width = 600, Height = 400 };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var figure = new PathFigure { StartPoint = new Point(0, 0), IsClosed = false };
        figure.Segments!.Add(new LineSegment { Point = new Point(50, -30) });
        figure.Segments!.Add(new LineSegment { Point = new Point(100, 0) });
        var geometry = new PathGeometry { Transform = new RotateTransform(30) };
        geometry.Figures!.Add(figure);
        var annotation = new DaisyTourAnnotation
        {
            Kind = DaisyTourAnnotationKind.Line, Path = DaisyTourAnnotationPath.Custom, Geometry = geometry,
            SourceAnchor = DaisyTourAnchor.Center, TargetAnchor = DaisyTourAnchor.Center
        };
        var source = new Point(120, 220);
        var target = new Point(420, 220);
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
            presenter.Update(annotation, new Rect(100, 200, 40, 40), new Rect(400, 200, 40, 40),
                Brushes.Red, Brushes.Lime, Brushes.Blue);
            var route = Assert.IsInstanceOfType<GeometryGroup>(presenter.Body.Data);
            var drawn = geometry.Transform!.Value * route.Transform!.Value;
            AssertNear(source, figure.StartPoint.Transform(drawn), "The drawn course starts at the source anchor.");
            AssertNear(target, new Point(100, 0).Transform(drawn), "The drawn course ends at the target anchor.");
            AssertNear(source, presenter.Course[0], "The course starts at the source anchor.");
            AssertNear(target, presenter.Course[^1], "The course ends at the target anchor.");
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    // Avalonia closes a figure by default; an open course would otherwise draw and hit-test a closing line along the
    // chord between its ends.
    [Fact]
    public async Task When_AConnectorCurvesOrBends_NoClosingLineRunsAlongItsChord()
    {
        var root = new Canvas { Width = 600, Height = 400 };
        var presenter = new DaisyTourAnnotationPresenter { Width = 600, Height = 400 };
        root.Children.Add(presenter);
        var paths = new (DaisyTourAnnotationPath Path, double CornerRadius)[]
        {
            (DaisyTourAnnotationPath.Curved, double.NaN),
            (DaisyTourAnnotationPath.Bezier, double.NaN),
            (DaisyTourAnnotationPath.Elbow, 12)
        };
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
            foreach (var (path, radius) in paths)
            {
                var annotation = new DaisyTourAnnotation { Kind = DaisyTourAnnotationKind.Line, Path = path, CornerRadius = radius };
                presenter.Update(annotation, new Rect(100, 100, 40, 40), new Rect(400, 300, 40, 40),
                    Brushes.Red, Brushes.Lime, Brushes.Blue);
                var course = presenter.Course;
                var chord = Lerp(course[0], course[^1], 0.25);
                var pen = new Pen(Brushes.Red, presenter.Body.StrokeThickness);
                Assert.IsFalse(presenter.Body.Data!.StrokeContains(pen, chord), $"{path}: no closing line runs along the chord.");
                Assert.IsTrue(presenter.Body.Data.StrokeContains(pen, course[course.Count / 2]), $"{path}: the stroke follows the course.");
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    [Fact]
    public Task When_AConnectorIsEdited_ItsHitPathDoesNotCoverTheChord() =>
        InEditorAsync((editor, tour) =>
        {
            var course = tour.AnnotationPresenters[0].Course;
            var hitPath = editor.HandleLayer.Children.OfType<Avalonia.Controls.Shapes.Path>().Single(path => path.Tag is 0);
            var pen = new Pen(Brushes.Transparent, hitPath.StrokeThickness);
            Assert.IsFalse(hitPath.Data!.StrokeContains(pen, Lerp(course[0], course[^1], 0.25)), "The hit area ends at the course ends.");
            Assert.IsTrue(hitPath.Data.StrokeContains(pen, course[course.Count / 2]), "The hit area follows the course.");
            return Task.CompletedTask;
        });

    // A real press hits the handle's template part, which takes the pointer first; the handle then takes it over.
    [Fact]
    public Task When_AHandleIsDraggedWithThePointer_TheDragIsOneUndoActionWithTheFinalValue() =>
        InEditorAsync(async (editor, tour) =>
        {
            var handle = Handle(editor, "DaisyTourHandleCurvature");
            var geometry = tour.AnnotationPresenters[0].EditGeometry!;
            var before = editor.ExportJson();
            var (root, start, pointer) = PressAtCenter(handle, PointerType.Mouse);
            Assert.AreSame(handle, pointer.Captured, "The handle takes the pointer over from its template part.");
            MoveCaptured(pointer, root, start + new Point(0, 10));
            MoveCaptured(pointer, root, start + new Point(0, 24));
            ReleaseCaptured(pointer, root, start + new Point(0, 24));
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(start + new Point(0, 24), geometry, start), Curvature(editor), 0.001);
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
            var handle = Handle(editor, "DaisyTourHandleCurvature");
            var geometry = tour.AnnotationPresenters[0].EditGeometry!;
            var before = editor.ExportJson();
            var (root, start, pointer) = PressAtCenter(handle, PointerType.Mouse);
            Assert.AreSame(handle, pointer.Captured, "The handle captures the pointer.");
            MoveCaptured(pointer, root, start + new Point(0, 18));
            pointer.Capture(null);
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(start + new Point(0, 18), geometry, start), Curvature(editor), 0.001);
            // A release that still arrives belongs to no drag.
            handle.RaiseEvent(new PointerReleasedEventArgs(handle, pointer, root, start + new Point(0, 40), 0, Released,
                KeyModifiers.None, MouseButton.Left));
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(start + new Point(0, 18), geometry, start), Curvature(editor), 0.001);
            editor.Undo();
            Assert.AreEqual(before, editor.ExportJson(), "The drag that ended with the capture is one undo action.");
        });

    // Without the check, the second finger's press restarts the drag on its own handle, and the first finger then moves
    // that handle from the second finger's origin.
    [Fact]
    public Task When_ASecondPointerPressesDuringADrag_TheFirstDragKeepsItsHandle() =>
        InEditorAsync(async (editor, tour) =>
        {
            var curvature = Handle(editor, "DaisyTourHandleCurvature");
            var other = Handle(editor, "DaisyTourHandleSource");
            var geometry = tour.AnnotationPresenters[0].EditGeometry!;
            var before = editor.ExportJson();
            var (root, start, first) = PressAtCenter(curvature, PointerType.Touch);
            MoveCaptured(first, root, start + new Point(0, 10));
            var (_, otherStart, second) = PressAtCenter(other, PointerType.Touch);
            MoveCaptured(second, root, otherStart + new Point(30, 30));
            // The second finger lifts first: neither its release nor its lost capture may end the first finger's drag.
            ReleaseCaptured(second, root, otherStart + new Point(30, 30));
            MoveCaptured(first, root, start + new Point(0, 24));
            ReleaseCaptured(first, root, start + new Point(0, 24));
            await RuntimeTestHelpers.WaitForRenderAsync();
            var style = editor.DraftProfile!.Steps.Single().Annotations.Single().Style!.Values;
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(start + new Point(0, 24), geometry, start), style.Curvature!.Value, 0.001,
                "The first finger keeps dragging the curvature from its own origin.");
            Assert.IsNull(style.SourceAnchor, "The second finger does not move the source end.");
            Assert.IsNull(style.SourceOffsetX);
            editor.Undo();
            Assert.AreEqual(before, editor.ExportJson(), "The first drag is one undo action.");
        });

    // The arrow keys move a focused handle by 1 DIP, or by 10 DIP with Shift; each key press is one undo action.
    [Fact]
    public Task When_AFocusedHandleIsNudgedWithTheArrowKeys_ShiftMovesItTenTimesFurther() =>
        InEditorAsync(async (editor, tour) =>
        {
            var handle = Handle(editor, "DaisyTourHandleCurvature");
            var geometry = tour.AnnotationPresenters[0].EditGeometry!;
            var before = editor.ExportJson();
            var origin = new Point(0, 0);
            Assert.IsTrue(handle.Focus());

            Nudge(handle, KeyModifiers.None);
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(origin + new Point(1, 0), geometry, origin), Curvature(editor), 0.001);
            editor.Undo();
            Assert.AreEqual(before, editor.ExportJson(), "One key press is one undo action.");
            await RuntimeTestHelpers.WaitForRenderAsync();

            Nudge(handle, KeyModifiers.Shift);
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.AreEqual(DaisyTourHandleLayer.ProjectCurvature(origin + new Point(10, 0), geometry, origin), Curvature(editor), 0.001);
            editor.Undo();
            Assert.AreEqual(before, editor.ExportJson(), "One key press with Shift is one undo action.");
        });

    // The tour discards its presenters on every step and when it closes; an annotation's content control must leave
    // the discarded presenter, or showing its step again throws because the control already has a parent.
    [Fact]
    public async Task When_AStepWithAnnotationContentIsShownAgain_TheContentMovesToTheNewPresenter()
    {
        var first = CreateTarget(new Thickness(40, 40, 0, 0));
        var second = CreateTarget(new Thickness(320, 220, 0, 0));
        var content = new TextBlock { Text = "Live content" };
        var withContent = new DaisyTourStep { Id = "content", Target = first, Title = "Content", Description = "An annotation with a control." };
        withContent.Annotations.Add(new DaisyTourAnnotation { Id = "note", Kind = DaisyTourAnnotationKind.SpeechBubble, Content = content });
        var tour = new DaisyTour();
        tour.Steps.Add(withContent);
        tour.Steps.Add(new DaisyTourStep { Id = "next", Target = second, Title = "Next", Description = "Another step." });
        var root = CreateRoot(tour, first, second);
        var closed = new List<DaisyTourClosedEventArgs>();
        tour.Closed += (_, args) => closed.Add(args);
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(TestTimeout);
            await tour.StartAsync().WaitAsync(TestTimeout);
            await ArrangeAsync(root);
            AssertShown(tour, content, "first visit");

            await tour.NextAsync().WaitAsync(TestTimeout);
            await tour.BackAsync().WaitAsync(TestTimeout);
            await ArrangeAsync(root);
            Assert.IsTrue(tour.IsOpen, "Going back to the step keeps the tour open.");
            Assert.AreEqual(0, tour.CurrentIndex);
            AssertShown(tour, content, "after Back");

            tour.Cancel();
            closed.Clear();
            await tour.StartAsync().WaitAsync(TestTimeout);
            await ArrangeAsync(root);
            Assert.IsTrue(tour.IsOpen, "Replaying keeps the tour open.");
            AssertShown(tour, content, "after a replay");
            Assert.AreEqual(0, closed.Count, "The replay does not close with an error.");
        }
        finally { await CleanupAsync(root, tour); }
    }

    private static void AssertShown(DaisyTour tour, Control content, string visit)
    {
        var presenter = tour.AnnotationPresenters.Single();
        Assert.IsTrue(presenter.ContentHost.IsVisualAncestorOf(content), $"{visit}: the current presenter shows the content control.");
        Assert.IsNotNull(TopLevel.GetTopLevel(content), $"{visit}: the content control is in the window.");
    }

    private static void Nudge(Button handle, KeyModifiers modifiers) =>
        handle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right, KeyModifiers = modifiers, Source = handle });

    private static Rect Update(DaisyTourAnnotationPresenter presenter, DaisyTourAnnotation annotation, double scale)
    {
        presenter.Update(annotation, new Rect(200, 150, 120, 40), new Rect(520, 340, 40, 30),
            Brushes.Red, Brushes.Lime, Brushes.Blue, scale: scale);
        Assert.IsNotNull(presenter.SelectionBounds);
        return presenter.SelectionBounds!.Value;
    }

    private static Point Lerp(Point from, Point to, double amount) =>
        new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount);

    private static void AssertNear(Point expected, Point actual, string message)
    {
        Assert.AreEqual(expected.X, actual.X, 0.5, message);
        Assert.AreEqual(expected.Y, actual.Y, 0.5, message);
    }

    private static Button Handle(DaisyTourEditor editor, string automationId)
    {
        var handle = editor.HandleLayer.Children.OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == automationId);
        Assert.IsTrue(handle.IsVisible, $"{automationId} is shown.");
        return handle;
    }

    private static double Curvature(DaisyTourEditor editor) =>
        editor.DraftProfile!.Steps.Single().Annotations.Single().Style!.Values.Curvature!.Value;

    private static Task InEditorAsync(Func<DaisyTourEditor, DaisyTour, Task> action)
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
        return TourEditorTestHelpers.InEditorAsync(root, tour, editor => action(editor, tour),
            () => tour.IsOpen && !tour.IsTransitioning && tour.AnnotationPresenters.Count == 1 && tour.AnnotationPresenters[0].EditGeometry is not null);
    }
}
