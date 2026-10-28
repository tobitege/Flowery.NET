using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.Controls;

/// <summary>Pointer and keyboard editing over the same geometry used by the tour presenter.</summary>
internal sealed class DaisyTourHandleLayer : Canvas
{
    private enum HandleKind { Source, Target, Curvature, FirstControl, SecondControl, BodyWidth, HeadLength, HeadWidth }
    private readonly DaisyTourEditor _editor;
    private readonly Dictionary<HandleKind, Button> _handles = [];
    private readonly List<Path> _hitPaths = [];
    private readonly Dictionary<HandleKind, Point> _positions = [];
    private HandleKind? _drag;
    private DaisyTourEditGeometry? _dragGeometry;
    private IPointer? _pointer;
    private Point _dragOrigin;
    private Point _dragPointerOrigin;
    private Point _dragDisplayOffset;
    // The last delivered drag point. A lost capture has no position of its own.
    private Point _dragPoint;
    private bool _dragMoved;

    internal DaisyTourHandleLayer(DaisyTourEditor editor)
    {
        _editor = editor;
        KeyboardNavigation.SetIsTabStop(this, false);
        foreach (var kind in Enum.GetValues<HandleKind>())
        {
            Shape marker = kind is HandleKind.Source or HandleKind.Target
                ? new Ellipse()
                : new Polygon { Points = [new Point(7, 1), new Point(13, 7), new Point(7, 13), new Point(1, 7)] };
            marker.Width = 14;
            marker.Height = 14;
            marker.Fill = new SolidColorBrush(Colors.DarkOrange);
            marker.StrokeThickness = 1.5;
            marker.IsHitTestVisible = false;
            var transparent = new SolidColorBrush(Colors.Transparent);
            var button = new Button
            {
                Width = 28, Height = 28, MinWidth = 0, MinHeight = 0,
                Padding = new Thickness(0), BorderThickness = new Thickness(0),
                Background = transparent, BorderBrush = transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = marker, Tag = kind
            };
            // Keep a forgiving hit area without painting over the annotation.
            foreach (var state in new[] { "PointerOver", "Pressed", "Disabled" })
            {
                button.Resources["ButtonBackground" + state] = transparent;
                button.Resources["ButtonBorderBrush" + state] = transparent;
            }
            AutomationProperties.SetAutomationId(button, "DaisyTourHandle" + kind);
            // Bubble only: a handler for tunnel and bubble would run twice for one press.
            button.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
            button.KeyDown += OnKeyDown;
            _handles.Add(kind, button);
            Children.Add(button);
        }
    }

    internal void Update()
    {
        var tour = _editor.Tour;
        var step = _editor.SelectedStep;
        if (tour is null || step is null || tour.CurrentStep != step) { HideHandles(); return; }
        while (_hitPaths.Count < tour.AnnotationPresenters.Count)
        {
            var path = new Path { Stroke = new SolidColorBrush(Colors.Transparent), Fill = new SolidColorBrush(Colors.Transparent), StrokeThickness = 16 };
            path.PointerPressed += OnSelect;
            _hitPaths.Add(path);
            Children.Insert(0, path);
        }
        for (var index = 0; index < _hitPaths.Count; index++)
        {
            var path = _hitPaths[index];
            path.Data = null;
            // A connector hidden for an unavailable target keeps no hit area.
            if (index >= tour.AnnotationPresenters.Count || !tour.AnnotationPresenters[index].IsVisible) continue;
            var points = tour.AnnotationPresenters[index].Course;
            path.Tag = index;
            path.Width = Width;
            path.Height = Height;
            if (points.Count < 2)
            {
                if (tour.AnnotationPresenters[index].SelectionBounds is { } bounds)
                    path.Data = new RectangleGeometry { Rect = bounds };
                continue;
            }
            // Avalonia figures are closed unless IsClosed is turned off; WinUI figures are open by default.
            var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
            for (var point = 1; point < points.Count; point++) figure.Segments!.Add(new LineSegment { Point = points[point] });
            var hitGeometry = new PathGeometry();
            hitGeometry.Figures!.Add(figure);
            path.Data = hitGeometry;
        }
        var selected = _editor.SelectedAnnotationIndex;
        if (selected < 0 || selected >= tour.AnnotationPresenters.Count || !tour.AnnotationPresenters[selected].IsVisible ||
            tour.AnnotationPresenters[selected].EditGeometry is not { } geometryForEdit)
        { HideHandles(); return; }
        var geometry = geometryForEdit;
        var along = Unit(geometry.Source, geometry.Target);
        var normal = new Point(-along.Y, along.X);
        var below = normal.Y < 0 || (normal.Y == 0 && normal.X < 0) ? new Point(-normal.X, -normal.Y) : normal;
        // Keep the 28-DIP hit area just below the rendered midpoint, including on diagonal paths.
        var clearance = 6 + 14 * (Math.Abs(below.X) + Math.Abs(below.Y)) + tour.AnnotationPresenters[selected].CourseThickness / 2;
        var curve = Add(geometry.BodyCenter, below, clearance);
        var tipDirection = geometry.HeadDirection;
        var tipNormal = new Point(-tipDirection.Y, tipDirection.X);
        var headBase = Add(geometry.HeadTip, tipDirection, -geometry.HeadLength);
        var course = tour.AnnotationPresenters[selected].Course;
        var sourceDirection = course.Count > 1 ? Unit(course[0], course[1]) : along;
        var targetDirection = course.Count > 1 ? Unit(course[^2], course[^1]) : along;
        var sourceClearance = 18 + Math.Max(geometry.BodyWidth, tour.AnnotationPresenters[selected].CourseThickness) / 2;
        var targetClearance = 18 + Math.Max(geometry.HeadWidth, tour.AnnotationPresenters[selected].CourseThickness) / 2;
        SetHandle(HandleKind.Source, geometryForEdit.Source, true, new Point(sourceDirection.Y * sourceClearance, -sourceDirection.X * sourceClearance));
        SetHandle(HandleKind.Target, geometryForEdit.Target, true, new Point(targetDirection.Y * targetClearance, -targetDirection.X * targetClearance));
        SetHandle(HandleKind.Curvature, curve, geometry.Path == DaisyTourAnnotationPath.Curved && !geometry.HasRouteLimit && Distance(geometry.Source, geometry.Target) > 0.001);
        SetBezierHandle(HandleKind.FirstControl, geometry, 1d / 3, sourceClearance);
        SetBezierHandle(HandleKind.SecondControl, geometry, 2d / 3, sourceClearance);
        SetHandle(HandleKind.BodyWidth, Add(geometry.BodyCenter, below, -geometry.BodyWidth / 2), geometry.IsBlock, Add(default, below, -18));
        SetHandle(HandleKind.HeadLength, headBase, geometry.HasEndHead, Add(default, tipNormal, -(18 + geometry.HeadWidth / 2)));
        SetHandle(HandleKind.HeadWidth, Add(headBase, tipNormal, geometry.HeadWidth / 2), geometry.HasEndHead, Add(default, tipNormal, 18));
    }

    private void SetBezierHandle(HandleKind kind, DaisyTourEditGeometry geometry, double t, double clearance)
    {
        var u = 1 - t;
        var point = new Point(
            u * u * u * geometry.Source.X + 3 * u * u * t * geometry.FirstControl.X + 3 * u * t * t * geometry.SecondControl.X + t * t * t * geometry.Target.X,
            u * u * u * geometry.Source.Y + 3 * u * u * t * geometry.FirstControl.Y + 3 * u * t * t * geometry.SecondControl.Y + t * t * t * geometry.Target.Y);
        var tangent = new Point(
            u * u * (geometry.FirstControl.X - geometry.Source.X) + 2 * u * t * (geometry.SecondControl.X - geometry.FirstControl.X) + t * t * (geometry.Target.X - geometry.SecondControl.X),
            u * u * (geometry.FirstControl.Y - geometry.Source.Y) + 2 * u * t * (geometry.SecondControl.Y - geometry.FirstControl.Y) + t * t * (geometry.Target.Y - geometry.SecondControl.Y));
        var direction = Unit(default, tangent);
        SetHandle(kind, point, geometry.Path == DaisyTourAnnotationPath.Bezier && !geometry.HasRouteLimit,
            new Point(direction.Y * clearance, -direction.X * clearance));
    }

    private void SetHandle(HandleKind kind, Point point, bool visible, Point displayOffset = default)
    {
        var button = _handles[kind];
        button.IsVisible = visible;
        // Drag deltas and keyboard edits use the real geometry, not the offset marker.
        _positions[kind] = point;
        if (_drag == kind) displayOffset = _dragDisplayOffset;
        SetLeft(button, point.X + displayOffset.X - button.Width / 2);
        SetTop(button, point.Y + displayOffset.Y - button.Height / 2);
        if (button.Content is Shape marker)
        {
            marker.Stroke = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
        }
        var name = FloweryLocalization.GetStringInternal("TourEditor_Handle_" + kind);
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
    }

    private void OnSelect(object? sender, PointerPressedEventArgs args)
    {
        if (sender is Path { Tag: int index }) { _editor.SelectAnnotation(index); args.Handled = true; }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Button { Tag: HandleKind kind } button || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // A second pointer, such as another finger, does not take over a running drag.
        if (_drag is not null || GetGeometry() is not { } geometry) return;
        _drag = kind;
        _dragGeometry = geometry;
        _pointer = args.Pointer;
        _dragOrigin = _positions[kind];
        _dragPoint = _dragOrigin;
        _dragDisplayOffset = new Point(GetLeft(button) + button.Width / 2 - _dragOrigin.X, GetTop(button) + button.Height / 2 - _dragOrigin.Y);
        _dragPointerOrigin = args.GetPosition(this);
        _dragMoved = false;
        _editor.BeginGesture();
        args.Pointer.Capture(button);
        button.Focus(NavigationMethod.Pointer);
        args.Handled = true;
    }

    private void OnMoved(object? sender, PointerEventArgs args)
    {
        if (_drag is not { } kind || _dragGeometry is not { } geometry || !IsDragPointer(args.Pointer)) return;
        args.Handled = true;
        var point = DragPoint(args);
        if (!_dragMoved && point == _dragOrigin) return;
        _dragMoved = true;
        _dragPoint = point;
        ApplyPoint(kind, point, geometry, _dragOrigin);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_drag is null || !IsDragPointer(args.Pointer)) return;
        FinishGesture(DragPoint(args));
        args.Handled = true;
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs args)
    {
        // Avalonia releases the capture after PointerReleased, which has finished the gesture already.
        // A capture lost during a drag, such as to another element, ends it at the last delivered point. So does a
        // canceled touch, whose pointer Avalonia disposes; WinUI reverted that drag through PointerCanceled.
        if (!IsDragPointer(args.Pointer)) return;
        FinishGesture(_dragPoint);
    }

    private bool IsDragPointer(IPointer pointer) => _pointer is not null && pointer.Id == _pointer.Id;

    private void FinishGesture(Point point)
    {
        if (_drag is not { } kind || _dragGeometry is not { } geometry) return;
        if (_dragMoved && point == _dragOrigin)
            _editor.CancelGesture();
        else
        {
            if (point != _dragOrigin) ApplyPoint(kind, point, geometry, _dragOrigin);
            _editor.EndGesture();
        }
        ReleaseGesture();
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (sender is not Button { Tag: HandleKind kind } || GetGeometry() is not { } geometry) return;
        if (args.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        var step = args.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10d : 1d;
        var point = _positions[kind];
        point = point.WithX(point.X + (args.Key == Key.Left ? -step : args.Key == Key.Right ? step : 0));
        point = point.WithY(point.Y + (args.Key == Key.Up ? -step : args.Key == Key.Down ? step : 0));
        _editor.BeginGesture();
        ApplyPoint(kind, point, geometry, _positions[kind]);
        _editor.EndGesture();
        args.Handled = true;
    }

    private Point DragPoint(PointerEventArgs args)
    {
        var point = args.GetPosition(this);
        return new Point(_dragOrigin.X + (point.X - _dragPointerOrigin.X), _dragOrigin.Y + (point.Y - _dragPointerOrigin.Y));
    }

    private void ApplyPoint(HandleKind kind, Point point, DaisyTourEditGeometry geometry, Point origin)
    {
        var along = Unit(geometry.Source, geometry.Target);
        var normal = new Point(-along.Y, along.X);
        var length = Math.Max(1, Distance(geometry.Source, geometry.Target));
        var delta = new Point(point.X - geometry.Source.X, point.Y - geometry.Source.Y);
        switch (kind)
        {
            case HandleKind.Source:
            case HandleKind.Target:
                var source = kind == HandleKind.Source;
                var bounds = source ? geometry.SourceBounds : geometry.TargetBounds;
                var anchor = NearestAnchor(bounds, point);
                var dock = DaisyTourAnnotationPresenter.Anchor(bounds, point, anchor);
                _editor.SetAnnotationValue(source ? "sourceAnchor" : "targetAnchor", anchor, publish: false);
                _editor.SetAnnotationValue(source ? "sourceOffsetX" : "targetOffsetX", point.X - dock.X, publish: false);
                _editor.SetAnnotationValue(source ? "sourceOffsetY" : "targetOffsetY", point.Y - dock.Y);
                break;
            case HandleKind.Curvature:
                _editor.SetAnnotationValue("curvature", ProjectCurvature(point, geometry, origin));
                break;
            case HandleKind.FirstControl:
            case HandleKind.SecondControl:
                // At t=1/3 or 2/3 the corresponding control contributes 4/9 of the curve point.
                // Invert that weight so the visible grip follows the pointer one-for-one.
                var control = kind == HandleKind.FirstControl ? geometry.FirstControl : geometry.SecondControl;
                delta = new Point(control.X - geometry.Source.X + (point.X - origin.X) * 9 / 4,
                    control.Y - geometry.Source.Y + (point.Y - origin.Y) * 9 / 4);
                var prefix = kind == HandleKind.FirstControl ? "controlPoint1" : "controlPoint2";
                _editor.SetAnnotationValue(prefix + "X", (delta.X * along.X + delta.Y * along.Y) / length, publish: false);
                _editor.SetAnnotationValue(prefix + "Y", (delta.X * normal.X + delta.Y * normal.Y) / length);
                break;
            case HandleKind.BodyWidth:
                _editor.SetAnnotationValue("bodyWidth", Math.Max(1, ProjectWidth(point, geometry.BodyCenter, normal)));
                break;
            case HandleKind.HeadLength:
                _editor.SetAnnotationValue("headLength", ProjectHeadLength(point, geometry));
                break;
            case HandleKind.HeadWidth:
                var headNormal = new Point(-geometry.HeadDirection.Y, geometry.HeadDirection.X);
                _editor.SetAnnotationValue("headWidth", ProjectWidth(point, geometry.HeadTip, headNormal));
                break;
        }
    }

    private DaisyTourEditGeometry? GetGeometry()
    {
        var index = _editor.SelectedAnnotationIndex;
        var presenters = _editor.Tour?.AnnotationPresenters;
        return presenters is not null && index >= 0 && index < presenters.Count ? presenters[index].EditGeometry : null;
    }

    internal void ReleaseGesture()
    {
        var kind = _drag;
        var pointer = _pointer;
        _drag = null;
        _dragGeometry = null;
        _pointer = null;
        _dragMoved = false;
        if (kind is { } selected && pointer is not null && ReferenceEquals(pointer.Captured, _handles[selected])) pointer.Capture(null);
    }

    internal void Clear()
    {
        ReleaseGesture();
        foreach (var path in _hitPaths) path.Data = null;
        HideHandles();
    }
    private void HideHandles() { foreach (var handle in _handles.Values) handle.IsVisible = false; }

    private static Point Add(Point point, Point direction, double scale) => new(point.X + direction.X * scale, point.Y + direction.Y * scale);
    internal static double ProjectWidth(Point point, Point center, Point normal) =>
        Math.Max(0, 2 * Math.Abs((point.X - center.X) * normal.X + (point.Y - center.Y) * normal.Y));
    internal static double ProjectHeadLength(Point point, DaisyTourEditGeometry geometry) =>
        Math.Max(0, (geometry.HeadTip.X - point.X) * geometry.HeadDirection.X + (geometry.HeadTip.Y - point.Y) * geometry.HeadDirection.Y);
    internal static double ProjectCurvature(Point point, DaisyTourEditGeometry geometry, Point origin)
    {
        var length = Distance(geometry.Source, geometry.Target);
        if (length <= 0.001) return geometry.Curvature;
        var along = Unit(geometry.Source, geometry.Target);
        return geometry.Curvature + 2 * (-(point.X - origin.X) * along.Y + (point.Y - origin.Y) * along.X) / length;
    }
    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
    private static Point Unit(Point a, Point b)
    {
        var length = Distance(a, b);
        return length > 0.001 ? new((b.X - a.X) / length, (b.Y - a.Y) / length) : new(1, 0);
    }
    private static DaisyTourAnchor NearestAnchor(Rect bounds, Point point)
    {
        var sides = new[] { Math.Abs(point.X - bounds.Left), Math.Abs(point.X - bounds.Right), Math.Abs(point.Y - bounds.Top), Math.Abs(point.Y - bounds.Bottom) };
        return Array.IndexOf(sides, sides.Min()) switch { 0 => DaisyTourAnchor.Left, 1 => DaisyTourAnchor.Right, 2 => DaisyTourAnchor.Top, _ => DaisyTourAnchor.Bottom };
    }
}
