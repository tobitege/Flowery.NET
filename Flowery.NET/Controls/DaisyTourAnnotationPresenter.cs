using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Flowery.Controls;

/// <summary>A connector route limit in overlay coordinates: y for Above and Below, x for LeftOf and RightOf.</summary>
internal readonly record struct DaisyTourRouteLimit(DaisyTourRouteSide Side, double Value);

/// <summary>Retained XAML vectors in the tour overlay's DIP coordinate space.</summary>
internal sealed class DaisyTourAnnotationPresenter : Canvas
{
    private readonly Path _body = CreatePath();
    private readonly Path _tail = CreatePath();
    private readonly Path _startHead = CreatePath();
    private readonly Path _endHead = CreatePath();
    private readonly GeometryGroup _customRoute = new();
    private readonly ContentPresenter _content = new();
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _contentHost;
    private readonly Path[] _shapes;
    private readonly Dictionary<AvaloniaProperty, DaisyTourResolvedValue> _resolvedValues = [];

    /// <summary>Narrows surfaces that cannot be placed at their preferred width.</summary>
    internal double WidthLimit { get; set; } = double.PositiveInfinity;

    /// <summary>The last valid placement direction, kept while it still fits.</summary>
    internal int PlacementDirection { get; set; } = -1;

    /// <summary>The drawn connector course; empty for surfaces.</summary>
    internal IReadOnlyList<Point> Course { get; private set; } = [];

    internal double CourseThickness { get; private set; }
    internal DaisyTourEditGeometry? EditGeometry { get; private set; }
    internal Rect? SelectionBounds { get; private set; }
    internal IReadOnlyDictionary<AvaloniaProperty, DaisyTourResolvedValue> ResolvedValues => _resolvedValues;
    internal Path Body => _body;
    internal Border ContentHost => _contentHost;

    internal DaisyTourAnnotationPresenter()
    {
        IsHitTestVisible = false;
        _shapes = [_body, _tail, _startHead, _endHead];
        _contentHost = new Border { Child = _content, Padding = new Thickness(16) };
        AutomationProperties.SetAccessibilityView(_contentHost, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_content, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_text, AccessibilityView.Raw);
        Children.Add(_tail);
        Children.Add(_body);
        Children.Add(_startHead);
        Children.Add(_endHead);
        Children.Add(_contentHost);
    }

    // The tour discards its presenters on every step and when it closes. A discarded presenter gives the
    // annotation's content control back, so the next presenter can show it without "already has a parent".
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _content.Content = null;
    }

    internal void Update(DaisyTourAnnotation annotation, Rect sourceBounds, Rect targetBounds,
        IBrush defaultStroke, IBrush defaultFill, IBrush defaultForeground,
        DaisyTourAnnotationStyle? hostDefaults = null, DaisyTourAnnotationStyle? stepDefaults = null, double scale = 1,
        FlowDirection textFlowDirection = FlowDirection.LeftToRight, DaisyTourRouteLimit? routeLimit = null,
        DaisyTourProfileLayers profile = default)
    {
        _contentHost.FlowDirection = textFlowDirection;
        _resolvedValues.Clear();
        var style = new EffectiveStyle(annotation, stepDefaults, hostDefaults, scale, profile, _resolvedValues);
        var kind = style.Kind;
        var stroke = style.Get(DaisyTourAnnotationStyle.StrokeProperty, defaultStroke);
        var fill = style.Get(DaisyTourAnnotationStyle.FillProperty, defaultFill);
        var thickness = Math.Max(0, style.Dimension(DaisyTourAnnotationStyle.StrokeThicknessProperty,
            style.Preset == DaisyTourAnnotationPreset.ThickArrow ? 7 : 3));
        var foreground = style.Get(DaisyTourAnnotationStyle.ForegroundProperty, defaultForeground);
        Opacity = Math.Clamp(style.Number(DaisyTourAnnotationStyle.OpacityProperty, 1), 0, 1);
        if (DaisyAccessibility.IsHighContrast)
        {
            stroke = defaultStroke;
            fill = defaultFill;
            foreground = defaultForeground;
            thickness = Math.Max(1, thickness);
            Opacity = 1;
        }
        style.Record(DaisyTourAnnotationStyle.StrokeProperty, stroke, highContrast: DaisyAccessibility.IsHighContrast);
        style.Record(DaisyTourAnnotationStyle.FillProperty, fill, highContrast: DaisyAccessibility.IsHighContrast);
        style.Record(DaisyTourAnnotationStyle.ForegroundProperty, foreground, highContrast: DaisyAccessibility.IsHighContrast);
        style.Record(DaisyTourAnnotationStyle.StrokeThicknessProperty, thickness);
        style.Record(DaisyTourAnnotationStyle.OpacityProperty, Opacity, highContrast: DaisyAccessibility.IsHighContrast);

        // Releases a previously fitted custom course before the shapes are rebuilt.
        _customRoute.Children!.Clear();
        Course = [];
        EditGeometry = null;
        SelectionBounds = null;
        CourseThickness = thickness;
        foreach (var shape in _shapes)
        {
            shape.Data = null;
            shape.RenderTransform = null;
            shape.Stretch = Stretch.None;
            // Each shape spans the overlay, as in WinUI; Avalonia draws its absolute geometry without clipping either way.
            shape.Width = Width;
            shape.Height = Height;
            SetLeft(shape, 0);
            SetTop(shape, 0);
            shape.Stroke = stroke;
            shape.Fill = null;
            shape.StrokeThickness = thickness;
            shape.StrokeJoin = PenLineJoin.Round;
            // Avalonia has one cap for the line ends and the dashes.
            shape.StrokeLineCap = PenLineCap.Round;
            shape.StrokeDashArray = null;
        }
        ApplyDashes(_body, style);
        _contentHost.IsVisible = false;
        var sourceAnchor = style.AnnotationValue(DaisyTourAnnotation.SourceAnchorProperty, annotation.SourceAnchor, DaisyTourAnchor.Auto);
        var targetAnchor = style.AnnotationValue(DaisyTourAnnotation.TargetAnchorProperty, annotation.TargetAnchor, DaisyTourAnchor.Auto);
        var source = Offset(Anchor(sourceBounds, Center(targetBounds), sourceAnchor),
            style.Number(DaisyTourAnnotationStyle.SourceOffsetXProperty, 0), style.Number(DaisyTourAnnotationStyle.SourceOffsetYProperty, 0));
        var target = Offset(Anchor(targetBounds, Center(sourceBounds), targetAnchor),
            style.Number(DaisyTourAnnotationStyle.TargetOffsetXProperty, 0), style.Number(DaisyTourAnnotationStyle.TargetOffsetYProperty, 0));

        if (kind is DaisyTourAnnotationKind.Arrow or DaisyTourAnnotationKind.Line)
        {
            (source, target) = AlignAutoAnchors(annotation, style, sourceBounds, targetBounds, source, target);
            // Heads and block bodies are solid in the stroke color unless a fill is specified.
            var solid = DaisyAccessibility.IsHighContrast ? stroke : style.Get<IBrush?>(DaisyTourAnnotationStyle.FillProperty, null) ?? stroke;
            style.Record(DaisyTourAnnotationStyle.FillProperty, solid, highContrast: DaisyAccessibility.IsHighContrast);
            var midpoint = DrawConnector(annotation, style, kind, source, target, sourceBounds, targetBounds, solid, routeLimit);
            ArrangeContent(annotation, style, midpoint, foreground);
            _contentHost.Background = fill;
            return;
        }

        DrawSurface(annotation, style, kind, sourceBounds, target, fill, foreground);
    }

    internal static DaisyTourAnnotationKind ResolveKind(DaisyTourAnnotation annotation,
        DaisyTourAnnotationStyle? hostDefaults = null, DaisyTourAnnotationStyle? stepDefaults = null,
        DaisyTourProfileLayers profile = default)
        => new EffectiveStyle(annotation, stepDefaults, hostDefaults, profile: profile).Kind;

    internal static DaisyTourAnnotationPath ResolvePath(DaisyTourAnnotation annotation,
        DaisyTourAnnotationStyle? host, DaisyTourAnnotationStyle? step, DaisyTourProfileLayers profile)
    {
        var style = new EffectiveStyle(annotation, step, host, profile: profile);
        return style.Get(DaisyTourAnnotationStyle.PathProperty,
            style.Preset == DaisyTourAnnotationPreset.CurvedArrow ? DaisyTourAnnotationPath.Curved : DaisyTourAnnotationPath.Straight);
    }

    internal Size MeasureSurface(DaisyTourAnnotation annotation, IBrush defaultForeground,
        DaisyTourAnnotationStyle? hostDefaults = null, DaisyTourAnnotationStyle? stepDefaults = null,
        double scale = 1, FlowDirection textFlowDirection = FlowDirection.LeftToRight, DaisyTourProfileLayers profile = default)
    {
        _contentHost.FlowDirection = textFlowDirection;
        var style = new EffectiveStyle(annotation, stepDefaults, hostDefaults, scale, profile);
        var foreground = DaisyAccessibility.IsHighContrast ? defaultForeground
            : style.Get(DaisyTourAnnotationStyle.ForegroundProperty, defaultForeground);
        var rect = ArrangeContent(annotation, style, new Point(), foreground);
        return new Size(rect.Width, rect.Height);
    }

    private Point DrawConnector(DaisyTourAnnotation annotation, EffectiveStyle style, DaisyTourAnnotationKind kind,
        Point source, Point target, Rect sourceBounds, Rect targetBounds, IBrush fill, DaisyTourRouteLimit? limit)
    {
        var path = style.Get(DaisyTourAnnotationStyle.PathProperty,
            style.Preset == DaisyTourAnnotationPreset.CurvedArrow ? DaisyTourAnnotationPath.Curved : DaisyTourAnnotationPath.Straight);
        var cubic = path is DaisyTourAnnotationPath.SCurve or DaisyTourAnnotationPath.Bezier;
        var curvature = style.Number(DaisyTourAnnotationStyle.CurvatureProperty, cubic ? 0.5 : 0.25);
        Geometry? route = null;
        var custom = false;
        var fixedSource = limit is not null && HasAuthoredEndpoint(style, annotation, true) ? source : (Point?)null;
        var fixedTarget = limit is not null && HasAuthoredEndpoint(style, annotation, false) ? target : (Point?)null;
        List<Point> points;
        if (limit is { } routeLimit && RouteAround(routeLimit, sourceBounds, targetBounds, fixedSource, fixedTarget) is { } corners)
        {
            // A route around the limit replaces the path's shape. Curve paths get smooth corners.
            var radius = path is DaisyTourAnnotationPath.Straight or DaisyTourAnnotationPath.Elbow
                ? Math.Max(0, style.Dimension(DaisyTourAnnotationStyle.CornerRadiusProperty, 0)) : double.PositiveInfinity;
            // Authored endpoints may leave a side facing away from the limit. Keep rounded
            // corners inside the clearance used to take those legs around the element.
            if (fixedSource is not null || fixedTarget is not null) radius = Math.Min(radius, 12);
            (route, points) = RoundedPolyline(corners, radius);
        }
        else if (cubic)
        {
            var (first, second) = CubicControls(style, path, source, target, sourceBounds, targetBounds, curvature);
            // Both ends are on the allowed side; the curve stays within its controls' hull there.
            if (limit is { } bound) (first, second) = (ClampToLimit(first, bound), ClampToLimit(second, bound));
            points = CubicPoints(source, first, second, target);
            route = CubicGeometry(source, first, second, target);
        }
        else if (path == DaisyTourAnnotationPath.Curved && limit is { } curveLimit)
        {
            var control = ClampToLimit(new Point((source.X + target.X) / 2 - (target.Y - source.Y) * curvature,
                (source.Y + target.Y) / 2 + (target.X - source.X) * curvature), curveLimit);
            // The quadratic curve as an equivalent cubic curve.
            var first = Lerp(source, control, 2d / 3);
            var second = Lerp(target, control, 2d / 3);
            points = CubicPoints(source, first, second, target);
            route = CubicGeometry(source, first, second, target);
        }
        else if (path == DaisyTourAnnotationPath.Custom && annotation.Geometry is { } geometry &&
                 FitCustomRoute(geometry, source, target) is { } fitted)
        {
            points = fitted;
            route = _customRoute;
            custom = true;
        }
        else
        {
            points = path == DaisyTourAnnotationPath.Elbow
                ? ElbowPoints(source, target, sourceBounds) : ConnectorPoints(source, target, path, curvature);
            if (path == DaisyTourAnnotationPath.Elbow)
                (route, points) = RoundedPolyline(points, Math.Max(0, style.Dimension(DaisyTourAnnotationStyle.CornerRadiusProperty, 0)));
        }
        // A fitted custom course keeps its own outline; block bodies follow the sampled course otherwise.
        var block = !custom && style.Get(DaisyTourAnnotationStyle.ArrowStyleProperty,
            style.Preset == DaisyTourAnnotationPreset.BlockArrow ? DaisyTourArrowStyle.Block : DaisyTourArrowStyle.Line) == DaisyTourArrowStyle.Block;
        var start = style.Get(DaisyTourAnnotationStyle.StartHeadProperty,
            style.Preset == DaisyTourAnnotationPreset.TwoEndedArrow ? DaisyTourArrowHead.Triangle : DaisyTourArrowHead.None);
        var end = style.Get(DaisyTourAnnotationStyle.EndHeadProperty,
            kind == DaisyTourAnnotationKind.Line ? DaisyTourArrowHead.None : DaisyTourArrowHead.Triangle);
        Course = points;
        var length = Math.Clamp(style.Dimension(DaisyTourAnnotationStyle.HeadLengthProperty, 22), 0, PolylineLength(points) / 3);
        var width = Math.Max(0, style.Dimension(DaisyTourAnnotationStyle.HeadWidthProperty, 18));
        var bodyWidth = Math.Max(1, style.Dimension(DaisyTourAnnotationStyle.BodyWidthProperty, 6));
        style.Record(DaisyTourAnnotationStyle.HeadLengthProperty, length);
        style.Record(DaisyTourAnnotationStyle.HeadWidthProperty, width);
        style.Record(DaisyTourAnnotationStyle.BodyWidthProperty, bodyWidth);
        CourseThickness = block ? Math.Max(CourseThickness, bodyWidth) : CourseThickness;
        var controls = CubicControls(style, path, source, target, sourceBounds, targetBounds, curvature);
        if (path == DaisyTourAnnotationPath.Bezier)
        {
            RecordControlPoint(style, controls.First, source, target, DaisyTourAnnotationStyle.ControlPoint1XProperty, DaisyTourAnnotationStyle.ControlPoint1YProperty);
            RecordControlPoint(style, controls.Second, source, target, DaisyTourAnnotationStyle.ControlPoint2XProperty, DaisyTourAnnotationStyle.ControlPoint2YProperty);
        }
        EditGeometry = new(points[0], points[^1], controls.First, controls.Second, sourceBounds, targetBounds,
            path, curvature, bodyWidth, length, width, limit is not null, PolylineMidpoint(points),
            Unit(points[^2], points[^1]), points[^1], block && kind == DaisyTourAnnotationKind.Arrow,
            end != DaisyTourArrowHead.None);
        _body.Data = block
            ? BlockGeometry(TrimEndpoints(points, start == DaisyTourArrowHead.None ? 0 : length, end == DaisyTourArrowHead.None ? 0 : length),
                bodyWidth)
            : route ?? ConnectorGeometry(source, target, path, curvature, points);
        _body.Fill = block ? fill : null;
        DrawHead(_startHead, start, points[0], points[1], length, width, fill);
        DrawHead(_endHead, end, points[^1], points[^2], length, width, fill);
        return PolylineMidpoint(points);
    }

    private void DrawSurface(DaisyTourAnnotation annotation, EffectiveStyle style, DaisyTourAnnotationKind kind,
        Rect sourceBounds, Point target, IBrush fill, IBrush foreground)
    {
        // Custom shapes without content fill their anchor box unless they are sized explicitly.
        var rect = ArrangeContent(annotation, style, Center(sourceBounds), foreground,
            kind == DaisyTourAnnotationKind.Custom ? new Size(sourceBounds.Width, sourceBounds.Height) : null);
        SelectionBounds = rect;
        _contentHost.Background = null;
        var radius = Math.Clamp(style.Dimension(DaisyTourAnnotationStyle.CornerRadiusProperty, 16), 0, Math.Min(rect.Width, rect.Height) / 2);
        style.Record(DaisyTourAnnotationStyle.CornerRadiusProperty, radius);
        _body.Fill = fill;
        _body.Data = kind switch
        {
            DaisyTourAnnotationKind.Cloud => CloudGeometry(rect, _contentHost.Padding.Left),
            DaisyTourAnnotationKind.Custom => annotation.Geometry,
            _ => RoundedRectangle(rect, radius)
        };
        if (kind == DaisyTourAnnotationKind.Custom && annotation.Geometry is not null)
        {
            _body.Stretch = Stretch.Fill;
            _body.Width = rect.Width;
            _body.Height = rect.Height;
            SetLeft(_body, rect.X);
            SetTop(_body, rect.Y);
        }

        var tail = style.Get(DaisyTourAnnotationStyle.TailProperty, DaisyTourAnnotationTail.None);
        DrawTail(tail, rect, target, style, fill);
    }

    private Rect ArrangeContent(DaisyTourAnnotation annotation, EffectiveStyle style, Point anchor, IBrush foreground, Size? emptySize = null)
    {
        var hasContent = annotation.Template is not null || annotation.Content is not null || !string.IsNullOrWhiteSpace(annotation.Text);
        var maxWidth = Math.Max(1, style.Dimension(DaisyTourAnnotationStyle.MaxWidthProperty, 280));
        var maxHeight = Math.Max(1, style.Dimension(DaisyTourAnnotationStyle.MaxHeightProperty, 180));
        if (double.IsFinite(Width)) maxWidth = Math.Min(maxWidth, Math.Max(1, Width - 24));
        maxWidth = Math.Max(1, Math.Min(maxWidth, WidthLimit));
        if (double.IsFinite(Height)) maxHeight = Math.Min(maxHeight, Math.Max(1, Height - 24));
        _text.Text = annotation.Text;
        _text.FontSize = Math.Max(1, style.Dimension(DaisyTourAnnotationStyle.FontSizeProperty, 16));
        _text.Foreground = foreground;
        _content.ContentTemplate = annotation.Template;
        _content.Content = annotation.Content ?? (annotation.Template is null ? _text : annotation);
        // Only explicit sizes are fixed. Content-sized hosts are measured with the same
        // unconstrained size that the canvas arranges, so wrapped text is never re-trimmed.
        var explicitWidth = style.Number(DaisyTourAnnotationStyle.WidthProperty, double.NaN);
        var explicitHeight = style.Number(DaisyTourAnnotationStyle.HeightProperty, double.NaN);
        _contentHost.MaxWidth = maxWidth;
        _contentHost.MaxHeight = maxHeight;
        _contentHost.Width = double.IsNaN(explicitWidth) ? double.NaN : Math.Clamp(explicitWidth, 1, maxWidth);
        _contentHost.Height = double.IsNaN(explicitHeight) ? double.NaN : Math.Clamp(explicitHeight, 1, maxHeight);
        _contentHost.MinWidth = double.IsNaN(explicitWidth) ? Math.Min(80 * style.Scale, maxWidth) : 0;
        _contentHost.MinHeight = double.IsNaN(explicitHeight) ? Math.Min(60 * style.Scale, maxHeight) : 0;
        _contentHost.Padding = new Thickness(16 * style.Scale);
        _contentHost.IsVisible = hasContent;
        if (hasContent)
        {
            // Avalonia's Measure skips a valid parent of a changed child, so the new text would keep the previous size.
            foreach (var layoutable in _contentHost.GetSelfAndVisualDescendants().OfType<Layoutable>())
                layoutable.InvalidateMeasure();
            _contentHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        var width = hasContent ? _contentHost.DesiredSize.Width
            : Math.Clamp(double.IsNaN(explicitWidth) ? emptySize?.Width ?? 100 * style.Scale : explicitWidth, 1,
                Math.Max(maxWidth, emptySize?.Width ?? 0));
        var height = hasContent ? _contentHost.DesiredSize.Height
            : Math.Clamp(double.IsNaN(explicitHeight) ? emptySize?.Height ?? 64 * style.Scale : explicitHeight, 1,
                Math.Max(maxHeight, emptySize?.Height ?? 0));
        _contentHost.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        var center = Offset(anchor, style.Number(DaisyTourAnnotationStyle.OffsetXProperty, 0), style.Number(DaisyTourAnnotationStyle.OffsetYProperty, 0));
        var rect = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
        style.Record(DaisyTourAnnotationStyle.WidthProperty, width, automatic: double.IsNaN(explicitWidth));
        style.Record(DaisyTourAnnotationStyle.HeightProperty, height, automatic: double.IsNaN(explicitHeight));
        SetLeft(_contentHost, rect.X);
        SetTop(_contentHost, rect.Y);
        AutomationProperties.SetName(_contentHost, annotation.Text);
        return rect;
    }

    private void DrawTail(DaisyTourAnnotationTail tail, Rect rect, Point target, EffectiveStyle style, IBrush fill)
    {
        if (tail == DaisyTourAnnotationTail.None)
            return;
        var start = Anchor(rect, target, DaisyTourAnchor.Auto);
        var direction = Unit(start, target);
        var distance = Distance(start, target);
        var length = Math.Min(distance, Math.Max(0, style.Dimension(DaisyTourAnnotationStyle.TailLengthProperty, distance)));
        var end = Offset(start, direction.X * length, direction.Y * length);
        var width = Math.Max(0, style.Dimension(DaisyTourAnnotationStyle.TailWidthProperty, 18));
        style.Record(DaisyTourAnnotationStyle.TailLengthProperty, length);
        style.Record(DaisyTourAnnotationStyle.TailWidthProperty, width);
        _tail.Fill = fill;
        if (tail == DaisyTourAnnotationTail.Speech)
        {
            _tail.Data = Polygon([
                Offset(start, -direction.Y * width / 2, direction.X * width / 2),
                end, Offset(start, direction.Y * width / 2, -direction.X * width / 2)]);
            return;
        }
        var geometry = new GeometryGroup();
        for (var index = 0; index < 3; index++)
        {
            var fraction = (index + 1) / 4d;
            var radius = width / 2 * (1 - fraction);
            geometry.Children!.Add(new EllipseGeometry
            {
                Center = Offset(start, direction.X * length * fraction, direction.Y * length * fraction),
                RadiusX = radius, RadiusY = radius
            });
        }
        _tail.Data = geometry;
    }

    private static void ApplyDashes(Path shape, EffectiveStyle style)
    {
        var customPattern = style.Get<DaisyTourDashCollection?>(DaisyTourAnnotationStyle.DashArrayProperty, null);
        var mode = style.Get(DaisyTourAnnotationStyle.StrokeStyleProperty, customPattern is not null ? DaisyTourStrokeStyle.Custom : style.Preset switch
        {
            DaisyTourAnnotationPreset.DottedLine => DaisyTourStrokeStyle.Dotted,
            DaisyTourAnnotationPreset.DashedLine => DaisyTourStrokeStyle.Dashed,
            DaisyTourAnnotationPreset.DashDotLine => DaisyTourStrokeStyle.DashDot,
            _ => DaisyTourStrokeStyle.Solid
        });
        IEnumerable<double>? pattern = mode switch
        {
            DaisyTourStrokeStyle.Custom => customPattern,
            DaisyTourStrokeStyle.Dotted => [0.1, 2],
            DaisyTourStrokeStyle.Dashed => [4, 2],
            DaisyTourStrokeStyle.DashDot => [4, 2, 0.1, 2],
            _ => null
        };
        if (pattern is null)
            return;
        var dashes = new AvaloniaList<double>();
        foreach (var dash in pattern)
        {
            if (double.IsFinite(dash) && dash >= 0)
                dashes.Add(dash);
        }
        if (dashes.Any(static dash => dash > 0))
            shape.StrokeDashArray = dashes;
        shape.StrokeDashOffset = style.Number(DaisyTourAnnotationStyle.DashOffsetProperty, 0);
    }

    private static Path CreatePath()
    {
        var path = new Path { IsHitTestVisible = false };
        AutomationProperties.SetAccessibilityView(path, AccessibilityView.Raw);
        return path;
    }

    internal static Point Anchor(Rect rect, Point toward, DaisyTourAnchor anchor)
    {
        var center = Center(rect);
        if (anchor != DaisyTourAnchor.Auto)
        {
            return anchor switch
            {
                DaisyTourAnchor.Top => new Point(center.X, rect.Top),
                DaisyTourAnchor.Bottom => new Point(center.X, rect.Bottom),
                DaisyTourAnchor.Left => new Point(rect.Left, center.Y),
                DaisyTourAnchor.Right => new Point(rect.Right, center.Y),
                _ => center
            };
        }
        var dx = toward.X - center.X;
        var dy = toward.Y - center.Y;
        if (Math.Abs(dx) + Math.Abs(dy) < 0.001)
            return center;
        var scaleX = Math.Abs(dx) > 0.001 ? rect.Width / 2 / Math.Abs(dx) : double.PositiveInfinity;
        var scaleY = Math.Abs(dy) > 0.001 ? rect.Height / 2 / Math.Abs(dy) : double.PositiveInfinity;
        var scale = Math.Min(scaleX, scaleY);
        return Offset(center, dx * scale, dy * scale);
    }

    private static List<Point> ConnectorPoints(Point start, Point end, DaisyTourAnnotationPath path, double curvature)
    {
        if (path == DaisyTourAnnotationPath.Elbow)
        {
            if (Math.Abs(end.X - start.X) >= Math.Abs(end.Y - start.Y))
                return [start, new Point((start.X + end.X) / 2, start.Y), new Point((start.X + end.X) / 2, end.Y), end];
            return [start, new Point(start.X, (start.Y + end.Y) / 2), new Point(end.X, (start.Y + end.Y) / 2), end];
        }
        if (path != DaisyTourAnnotationPath.Curved)
            return [start, end];

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var control = new Point((start.X + end.X) / 2 - dy * curvature, (start.Y + end.Y) / 2 + dx * curvature);
        var points = new List<Point>(25);
        for (var index = 0; index <= 24; index++)
        {
            var t = index / 24d;
            var u = 1 - t;
            points.Add(new Point(u * u * start.X + 2 * u * t * control.X + t * t * end.X,
                u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y));
        }
        return points;
    }

    private static Geometry BlockGeometry(IReadOnlyList<Point> points, double width)
    {
        var left = new List<Point>(points.Count);
        var right = new List<Point>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var direction = Unit(points[Math.Max(0, index - 1)], points[Math.Min(points.Count - 1, index + 1)]);
            left.Add(Offset(points[index], -direction.Y * width / 2, direction.X * width / 2));
            right.Add(Offset(points[index], direction.Y * width / 2, -direction.X * width / 2));
        }
        right.Reverse();
        left.AddRange(right);
        return Polygon(left);
    }

    private static double PolylineLength(IReadOnlyList<Point> points)
    {
        var length = 0d;
        for (var index = 1; index < points.Count; index++)
            length += Distance(points[index - 1], points[index]);
        return length;
    }

    private static Point PolylineMidpoint(IReadOnlyList<Point> points)
    {
        var remaining = PolylineLength(points) / 2;
        for (var index = 1; index < points.Count; index++)
        {
            var segment = Distance(points[index - 1], points[index]);
            if (remaining <= segment)
            {
                var direction = Unit(points[index - 1], points[index]);
                return Offset(points[index - 1], direction.X * remaining, direction.Y * remaining);
            }
            remaining -= segment;
        }
        return points[^1];
    }

    private static List<Point> TrimEndpoints(IReadOnlyList<Point> points, double start, double end)
    {
        var result = points.ToList();
        TrimStart(result, start);
        result.Reverse();
        TrimStart(result, end);
        result.Reverse();
        return result;
    }

    private static void TrimStart(List<Point> points, double distance)
    {
        while (points.Count > 2 && Distance(points[0], points[1]) < distance)
        {
            distance -= Distance(points[0], points[1]);
            points.RemoveAt(0);
        }
        var direction = Unit(points[0], points[1]);
        points[0] = Offset(points[0], direction.X * distance, direction.Y * distance);
    }

    private static Geometry ConnectorGeometry(Point start, Point end, DaisyTourAnnotationPath path, double curvature, IReadOnlyList<Point> points)
    {
        if (path != DaisyTourAnnotationPath.Curved)
            return LineGeometry(points);
        // Avalonia figures are closed unless IsClosed is turned off; WinUI figures are open by default.
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments!.Add(new QuadraticBezierSegment
        {
            Point1 = new Point((start.X + end.X) / 2 - (end.Y - start.Y) * curvature,
                (start.Y + end.Y) / 2 + (end.X - start.X) * curvature),
            Point2 = end
        });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    // A route limit is solved in a space where the course must stay at y <= limit; the other
    // three sides are mirrored or transposed into that space and back.
    private static Point ToRouteSpace(Point point, DaisyTourRouteSide side) => side switch
    {
        DaisyTourRouteSide.Below => new Point(point.X, -point.Y),
        DaisyTourRouteSide.LeftOf => new Point(point.Y, point.X),
        DaisyTourRouteSide.RightOf => new Point(point.Y, -point.X),
        _ => point
    };

    private static Point FromRouteSpace(Point point, DaisyTourRouteSide side) => side switch
    {
        DaisyTourRouteSide.Below => new Point(point.X, -point.Y),
        DaisyTourRouteSide.LeftOf => new Point(point.Y, point.X),
        DaisyTourRouteSide.RightOf => new Point(-point.Y, point.X),
        _ => point
    };

    private static double RouteSpaceLimit(DaisyTourRouteLimit limit) =>
        limit.Side is DaisyTourRouteSide.Below or DaisyTourRouteSide.RightOf ? -limit.Value : limit.Value;

    private static Point ClampToLimit(Point point, DaisyTourRouteLimit limit)
    {
        var routed = ToRouteSpace(point, limit.Side);
        return FromRouteSpace(new Point(routed.X, Math.Min(routed.Y, RouteSpaceLimit(limit))), limit.Side);
    }

    private static bool HasAuthoredEndpoint(EffectiveStyle style, DaisyTourAnnotation annotation, bool source) =>
        style.AnnotationValue(source ? DaisyTourAnnotation.SourceAnchorProperty : DaisyTourAnnotation.TargetAnchorProperty,
            source ? annotation.SourceAnchor : annotation.TargetAnchor, DaisyTourAnchor.Auto) != DaisyTourAnchor.Auto ||
        style.Number(source ? DaisyTourAnnotationStyle.SourceOffsetXProperty : DaisyTourAnnotationStyle.TargetOffsetXProperty, 0) != 0 ||
        style.Number(source ? DaisyTourAnnotationStyle.SourceOffsetYProperty : DaisyTourAnnotationStyle.TargetOffsetYProperty, 0) != 0;

    // Returns right-angle corners around the limit when an end lies beyond it, or null when both
    // ends are on the allowed side. The ends never belong to the area kept clear.
    private static List<Point>? RouteAround(DaisyTourRouteLimit limit, Rect sourceBounds, Rect targetBounds,
        Point? fixedSource, Point? fixedTarget)
    {
        const double clearance = 12;
        var value = RouteSpaceLimit(limit);
        // The corners can swap in route space; an Avalonia rect from two points is not normalized.
        var source = new Rect(ToRouteSpace(new Point(sourceBounds.Left, sourceBounds.Top), limit.Side),
            ToRouteSpace(new Point(sourceBounds.Right, sourceBounds.Bottom), limit.Side)).Normalize();
        var target = new Rect(ToRouteSpace(new Point(targetBounds.Left, targetBounds.Top), limit.Side),
            ToRouteSpace(new Point(targetBounds.Right, targetBounds.Bottom), limit.Side)).Normalize();
        var authoredStart = fixedSource is { } startPoint ? ToRouteSpace(startPoint, limit.Side) : (Point?)null;
        var authoredEnd = fixedTarget is { } endPoint ? ToRouteSpace(endPoint, limit.Side) : (Point?)null;
        var sourceBeyond = source.Bottom > value || authoredStart is { } a && a.Y > value;
        var targetBeyond = target.Bottom > value || authoredEnd is { } b && b.Y > value;
        if (!sourceBeyond && !targetBeyond)
            return null;
        // Both beyond: legs rise above both ends. One beyond: the course runs at the other end's middle.
        var level = sourceBeyond && targetBeyond
            ? Math.Min(value, Math.Min(source.Top, target.Top) - clearance)
            : Center(sourceBeyond ? target : source).Y;
        if (authoredStart is not null || authoredEnd is not null)
        {
            // A manual side must be reachable without taking its leg through either element.
            level = Math.Min(value, Math.Min(source.Top, target.Top) - clearance);
            var startLeg = RouteEndpoint(source, target, authoredStart, level, clearance);
            var endLeg = RouteEndpoint(target, source, authoredEnd, level, clearance);
            endLeg.Reverse();
            foreach (var point in endLeg) AddRoutePoint(startLeg, point);
            if (startLeg.Count == 1) startLeg.Add(startLeg[0]);
            return startLeg.ConvertAll(point => FromRouteSpace(point, limit.Side));
        }
        var start = RouteEnd(source, target, level);
        var end = RouteEnd(target, source, level);
        var points = new List<Point> { start };
        if (Math.Abs(start.Y - level) > 0.01) points.Add(new Point(start.X, level));
        if (Math.Abs(end.Y - level) > 0.01) points.Add(new Point(end.X, level));
        points.Add(end);
        return points.ConvertAll(point => FromRouteSpace(point, limit.Side));
    }

    private static List<Point> RouteEndpoint(Rect bounds, Rect other, Point? authored, double level, double clearance)
    {
        var point = authored ?? RouteEnd(bounds, other, level);
        var points = new List<Point> { point };
        if (authored is not null && point.Y > level)
        {
            double[] distances = [Math.Abs(point.X - bounds.Left), Math.Abs(point.X - bounds.Right),
                Math.Abs(point.Y - bounds.Top), Math.Abs(point.Y - bounds.Bottom)];
            var side = Array.IndexOf(distances, distances.Min());
            var departure = side switch
            {
                0 => new Point(Math.Min(point.X, bounds.Left - clearance), point.Y),
                1 => new Point(Math.Max(point.X, bounds.Right + clearance), point.Y),
                2 => new Point(point.X, Math.Min(point.Y, bounds.Top - clearance)),
                _ => new Point(point.X, Math.Max(point.Y, bounds.Bottom + clearance))
            };
            // Keep the outward leg in the gap when the other end is closer than the clearance.
            if (RouteSegmentIntersects(other, point, departure))
            {
                departure = side switch
                {
                    0 => new Point((point.X + other.Right) / 2, point.Y),
                    1 => new Point((point.X + other.Left) / 2, point.Y),
                    2 => new Point(point.X, (point.Y + other.Bottom) / 2),
                    _ => new Point(point.X, (point.Y + other.Top) / 2)
                };
            }
            point = departure;
            AddRoutePoint(points, point);
        }
        var rise = new Point(point.X, level);
        if (RouteSegmentIntersects(bounds, point, rise) || RouteSegmentIntersects(other, point, rise))
        {
            var left = new Point(Math.Min(bounds.Left, other.Left) - clearance, point.Y);
            var right = new Point(Math.Max(bounds.Right, other.Right) + clearance, point.Y);
            var leftClear = !RouteSegmentIntersects(bounds, point, left) && !RouteSegmentIntersects(other, point, left);
            var rightClear = !RouteSegmentIntersects(bounds, point, right) && !RouteSegmentIntersects(other, point, right);
            if (leftClear && (!rightClear || Distance(point, left) <= Distance(point, right))) point = left;
            else if (rightClear) point = right;
            AddRoutePoint(points, point);
        }
        AddRoutePoint(points, new Point(point.X, level));
        return points;
    }

    // Route legs are axis-aligned; touching an endpoint boundary does not cross its interior.
    private static bool RouteSegmentIntersects(Rect bounds, Point start, Point end) => start.X == end.X
        ? start.X > bounds.Left && start.X < bounds.Right && Math.Max(start.Y, end.Y) > bounds.Top && Math.Min(start.Y, end.Y) < bounds.Bottom
        : start.Y > bounds.Top && start.Y < bounds.Bottom && Math.Max(start.X, end.X) > bounds.Left && Math.Min(start.X, end.X) < bounds.Right;

    private static void AddRoutePoint(List<Point> points, Point point)
    {
        var last = points[^1];
        if (Distance(last, point) <= 0.01) return;
        // Do not create a rounded corner in the middle of a straight leg.
        if (points.Count > 1)
        {
            var previous = points[^2];
            if (previous.X == last.X && last.X == point.X && (last.Y - previous.Y) * (point.Y - last.Y) >= 0 ||
                previous.Y == last.Y && last.Y == point.Y && (last.X - previous.X) * (point.X - last.X) >= 0)
                points.RemoveAt(points.Count - 1);
        }
        points.Add(point);
    }

    // An end meets the course from its facing side when the course level crosses it, otherwise from its top.
    private static Point RouteEnd(Rect end, Rect other, double level) => level > end.Top && level <= end.Bottom
        ? new Point(Center(other).X >= Center(end).X ? end.Right : end.Left, level)
        : new Point(Center(end).X, end.Top);

    private static Point Lerp(Point from, Point to, double amount) =>
        new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount);

    // Automatic anchors dock at the facing side centers. A course that would run back through its own
    // element faces its first direction instead.
    private static (Point Source, Point Target) AlignAutoAnchors(DaisyTourAnnotation annotation, EffectiveStyle style,
        Rect sourceBounds, Rect targetBounds, Point source, Point target)
    {
        var path = style.Get(DaisyTourAnnotationStyle.PathProperty,
            style.Preset == DaisyTourAnnotationPreset.CurvedArrow ? DaisyTourAnnotationPath.Curved : DaisyTourAnnotationPath.Straight);
        var autoSource = style.AnnotationValue(DaisyTourAnnotation.SourceAnchorProperty, annotation.SourceAnchor, DaisyTourAnchor.Auto) == DaisyTourAnchor.Auto;
        var autoTarget = style.AnnotationValue(DaisyTourAnnotation.TargetAnchorProperty, annotation.TargetAnchor, DaisyTourAnchor.Auto) == DaisyTourAnchor.Auto;
        var sourceOffset = new Point(style.Number(DaisyTourAnnotationStyle.SourceOffsetXProperty, 0), style.Number(DaisyTourAnnotationStyle.SourceOffsetYProperty, 0));
        var targetOffset = new Point(style.Number(DaisyTourAnnotationStyle.TargetOffsetXProperty, 0), style.Number(DaisyTourAnnotationStyle.TargetOffsetYProperty, 0));
        Point first, last;
        var (sourceSide, targetSide) = FacingSides(sourceBounds, targetBounds);
        if (autoSource) source = Offset(sourceSide, sourceOffset.X, sourceOffset.Y);
        if (autoTarget) target = Offset(targetSide, targetOffset.X, targetOffset.Y);
        if (path == DaisyTourAnnotationPath.SCurve)
            return (source, target);
        if (path == DaisyTourAnnotationPath.Elbow)
        {
            var corners = ElbowPoints(source, target, sourceBounds);
            (first, last) = (corners[1], corners[2]);
        }
        else if (path == DaisyTourAnnotationPath.Curved)
        {
            var curvature = style.Number(DaisyTourAnnotationStyle.CurvatureProperty, 0.25);
            first = last = new Point((source.X + target.X) / 2 - (target.Y - source.Y) * curvature,
                (source.Y + target.Y) / 2 + (target.X - source.X) * curvature);
        }
        else if (path == DaisyTourAnnotationPath.Bezier)
        {
            (first, last) = CubicControls(style, path, source, target, sourceBounds, targetBounds,
                style.Number(DaisyTourAnnotationStyle.CurvatureProperty, 0.5));
        }
        else
        {
            // Straight and custom courses head from one anchor toward the other.
            (first, last) = (target, source);
        }
        // Keep the facing side while the course leaves it outward.
        autoSource &= !LeavesOutward(sourceBounds, source, first);
        autoTarget &= !LeavesOutward(targetBounds, target, last);
        if (autoSource)
            source = Offset(Anchor(sourceBounds, first, DaisyTourAnchor.Auto), sourceOffset.X, sourceOffset.Y);
        if (autoTarget)
            target = Offset(Anchor(targetBounds, last, DaisyTourAnchor.Auto), targetOffset.X, targetOffset.Y);
        return (source, target);
    }

    // The side pair along the main direction, unless the controls overlap on that axis and leave
    // room only along the other one.
    private static (Point Source, Point Target) FacingSides(Rect source, Rect target)
    {
        var dx = Center(target).X - Center(source).X;
        var dy = Center(target).Y - Center(source).Y;
        var horizontalGap = dx >= 0 ? target.Left - source.Right : source.Left - target.Right;
        var verticalGap = dy >= 0 ? target.Top - source.Bottom : source.Top - target.Bottom;
        var horizontal = Math.Abs(dx) >= Math.Abs(dy)
            ? horizontalGap >= 0 || verticalGap < 0
            : verticalGap < 0 && horizontalGap >= 0;
        return horizontal
            ? (new Point(dx >= 0 ? source.Right : source.Left, Center(source).Y), new Point(dx >= 0 ? target.Left : target.Right, Center(target).Y))
            : (new Point(Center(source).X, dy >= 0 ? source.Bottom : source.Top), new Point(Center(target).X, dy >= 0 ? target.Top : target.Bottom));
    }

    // Right-angle corners that leave and enter perpendicular to the anchored sides.
    private static List<Point> ElbowPoints(Point start, Point end, Rect sourceBounds)
    {
        var normal = EdgeNormal(sourceBounds, start, new Point());
        var horizontal = normal.X != 0 || normal.Y == 0 && Math.Abs(end.X - start.X) >= Math.Abs(end.Y - start.Y);
        return horizontal
            ? [start, new Point((start.X + end.X) / 2, start.Y), new Point((start.X + end.X) / 2, end.Y), end]
            : [start, new Point(start.X, (start.Y + end.Y) / 2), new Point(end.X, (start.Y + end.Y) / 2), end];
    }

    private static bool LeavesOutward(Rect rect, Point anchor, Point toward)
    {
        var normal = EdgeNormal(rect, anchor, new Point());
        return (toward.X - anchor.X) * normal.X + (toward.Y - anchor.Y) * normal.Y > 0;
    }

    // SCurve leaves both anchors perpendicular to their edges. Bezier replaces either
    // control point that is fully specified in connector coordinates.
    private static (Point First, Point Second) CubicControls(EffectiveStyle style, DaisyTourAnnotationPath path,
        Point source, Point target, Rect sourceBounds, Rect targetBounds, double curvature)
    {
        var length = Distance(source, target);
        var along = Unit(source, target);
        var startNormal = EdgeNormal(sourceBounds, source, along);
        var endNormal = EdgeNormal(targetBounds, target, new Point(-along.X, -along.Y));
        // Each control reaches out by the distance along its edge normal, not the full diagonal,
        // so curves between distant controls do not overshoot.
        var startReach = Math.Max(16, Math.Abs((target.X - source.X) * startNormal.X + (target.Y - source.Y) * startNormal.Y)) * curvature;
        var endReach = Math.Max(16, Math.Abs((target.X - source.X) * endNormal.X + (target.Y - source.Y) * endNormal.Y)) * curvature;
        var first = Offset(source, startNormal.X * startReach, startNormal.Y * startReach);
        var second = Offset(target, endNormal.X * endReach, endNormal.Y * endReach);
        if (path != DaisyTourAnnotationPath.Bezier)
            return (first, second);
        return (ConnectorPoint(style, DaisyTourAnnotationStyle.ControlPoint1XProperty, DaisyTourAnnotationStyle.ControlPoint1YProperty,
                source, along, length, first),
            ConnectorPoint(style, DaisyTourAnnotationStyle.ControlPoint2XProperty, DaisyTourAnnotationStyle.ControlPoint2YProperty,
                source, along, length, second));
    }

    private static Point ConnectorPoint(EffectiveStyle style, AvaloniaProperty xProperty, AvaloniaProperty yProperty,
        Point origin, Point along, double length, Point fallback)
    {
        var x = style.Number(xProperty, double.NaN);
        var y = style.Number(yProperty, double.NaN);
        if (double.IsNaN(x) || double.IsNaN(y))
            return fallback;
        // Positive Y bends to the same side as a positive Curvature.
        return new Point(origin.X + (along.X * x - along.Y * y) * length, origin.Y + (along.Y * x + along.X * y) * length);
    }

    private static void RecordControlPoint(EffectiveStyle style, Point point, Point source, Point target,
        AvaloniaProperty xProperty, AvaloniaProperty yProperty)
    {
        var length = Math.Max(0.001, Distance(source, target));
        var along = Unit(source, target);
        var dx = point.X - source.X;
        var dy = point.Y - source.Y;
        style.Record(xProperty, (dx * along.X + dy * along.Y) / length, automatic: style.IsUnset(xProperty));
        style.Record(yProperty, (-dx * along.Y + dy * along.X) / length, automatic: style.IsUnset(yProperty));
    }

    private static Point EdgeNormal(Rect rect, Point point, Point fallback)
    {
        double[] distances = [Math.Abs(point.X - rect.Left), Math.Abs(point.X - rect.Right), Math.Abs(point.Y - rect.Top), Math.Abs(point.Y - rect.Bottom)];
        var nearest = Array.IndexOf(distances, distances.Min());
        if (distances[nearest] > 0.5)
            return fallback;
        return nearest switch { 0 => new Point(-1, 0), 1 => new Point(1, 0), 2 => new Point(0, -1), _ => new Point(0, 1) };
    }

    private static List<Point> CubicPoints(Point start, Point first, Point second, Point end)
    {
        var points = new List<Point>(25);
        for (var index = 0; index <= 24; index++)
            points.Add(Cubic(start, first, second, end, index / 24d));
        return points;
    }

    private static PathGeometry CubicGeometry(Point start, Point first, Point second, Point end)
    {
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments!.Add(new BezierSegment { Point1 = first, Point2 = second, Point3 = end });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    private static Point Quadratic(Point start, Point control, Point end, double t)
    {
        var u = 1 - t;
        return new Point(u * u * start.X + 2 * u * t * control.X + t * t * end.X, u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y);
    }

    private static Point Cubic(Point start, Point first, Point second, Point end, double t)
    {
        var u = 1 - t;
        return new Point(u * u * u * start.X + 3 * u * u * t * first.X + 3 * u * t * t * second.X + t * t * t * end.X,
            u * u * u * start.Y + 3 * u * u * t * first.Y + 3 * u * t * t * second.Y + t * t * t * end.Y);
    }

    // Rounds the right-angle corners of an elbow or route polyline.
    private static (Geometry Route, List<Point> Points) RoundedPolyline(List<Point> points, double radius)
    {
        if (radius <= 0 || points.Count < 3)
            return (LineGeometry(points), points);
        var figure = new PathFigure { StartPoint = points[0], IsClosed = false, IsFilled = false };
        var sampled = new List<Point> { points[0] };
        for (var index = 1; index < points.Count - 1; index++)
        {
            var corner = points[index];
            // Each corner may use at most half of its adjacent segments.
            var cornerRadius = Math.Min(radius, Math.Min(Distance(points[index - 1], corner), Distance(corner, points[index + 1])) / 2);
            if (cornerRadius < 0.01)
            {
                figure.Segments!.Add(new LineSegment { Point = corner });
                sampled.Add(corner);
                continue;
            }
            var before = Unit(points[index - 1], corner);
            var after = Unit(corner, points[index + 1]);
            var entry = Offset(corner, -before.X * cornerRadius, -before.Y * cornerRadius);
            var exit = Offset(corner, after.X * cornerRadius, after.Y * cornerRadius);
            figure.Segments!.Add(new LineSegment { Point = entry });
            figure.Segments.Add(new ArcSegment
            {
                Point = exit, Size = new Size(cornerRadius, cornerRadius),
                SweepDirection = before.X * after.Y - before.Y * after.X > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise
            });
            var center = Offset(entry, after.X * cornerRadius, after.Y * cornerRadius);
            for (var step = 0; step <= 6; step++)
            {
                var angle = step * Math.PI / 12;
                sampled.Add(new Point(center.X + (entry.X - center.X) * Math.Cos(angle) + (exit.X - center.X) * Math.Sin(angle),
                    center.Y + (entry.Y - center.Y) * Math.Cos(angle) + (exit.Y - center.Y) * Math.Sin(angle)));
            }
        }
        figure.Segments!.Add(new LineSegment { Point = points[^1] });
        sampled.Add(points[^1]);
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return (geometry, sampled);
    }

    // Rotates and scales a custom course so that it starts at the source and ends at the target.
    // Geometry parsed from path markup does not expose its figures; its contour then gives the course.
    // A closed geometry without figures, such as an ellipse or markup that ends with Z, meets the anchors
    // at the middle of its left and right bounds edges.
    private List<Point>? FitCustomRoute(Geometry geometry, Point source, Point target)
    {
        var samples = geometry is PathGeometry { Figures.Count: > 0 } pathGeometry && pathGeometry.Figures![0] is { Segments.Count: > 0 } figure
            ? Flatten(figure, geometry.Transform) : ContourSamples(geometry);
        var bounds = geometry.Bounds;
        var from = samples?[0] ?? new Point(bounds.Left, bounds.Top + bounds.Height / 2);
        var to = samples?[^1] ?? new Point(bounds.Right, bounds.Top + bounds.Height / 2);
        var span = Distance(from, to);
        var length = Distance(source, target);
        // An empty Avalonia geometry has default bounds instead of an empty rect.
        if (bounds is { Width: <= 0, Height: <= 0 } || span < 0.001 || length < 0.001)
            return null;
        var scale = length / span;
        var angle = Math.Atan2(target.Y - source.Y, target.X - source.X) - Math.Atan2(to.Y - from.Y, to.X - from.X);
        var cos = Math.Cos(angle) * scale;
        var sin = Math.Sin(angle) * scale;
        var matrix = new Matrix(cos, sin, -sin, cos, source.X - (from.X * cos - from.Y * sin), source.Y - (from.X * sin + from.Y * cos));
        // A geometry transform keeps the stroke thickness and avoids clipping the course in the
        // path's untransformed bounds. The caller's geometry instance is not modified.
        _customRoute.Children!.Add(geometry);
        _customRoute.Transform = new MatrixTransform { Matrix = matrix };
        return samples is null ? [source, target] : samples.ConvertAll(point => Transform(matrix, point));
    }

    // Samples the first contour in 24 steps, like the first figure of a path geometry; Skia measures only that one.
    // Null for a closed or unmeasurable contour.
    private static List<Point>? ContourSamples(Geometry geometry)
    {
        var length = geometry.ContourLength;
        if (!(length > 0.001) || !double.IsFinite(length))
            return null;
        var points = new List<Point>(25);
        for (var index = 0; index <= 24; index++)
        {
            if (!geometry.TryGetPointAtDistance(length * index / 24, out var point))
                return null;
            points.Add(point);
        }
        return Distance(points[0], points[^1]) < 0.001 ? null : points;
    }

    private static Point Transform(Matrix matrix, Point point) =>
        new(point.X * matrix.M11 + point.Y * matrix.M21 + matrix.M31, point.X * matrix.M12 + point.Y * matrix.M22 + matrix.M32);

    // The figure is in the geometry's own coordinates; its bounds, its contour and its drawing include the
    // geometry's transform, so the flattened points take it too.
    private static List<Point> Flatten(PathFigure figure, Transform? transform)
    {
        var points = new List<Point> { figure.StartPoint };
        foreach (var segment in figure.Segments!)
        {
            var from = points[^1];
            switch (segment)
            {
                case LineSegment line:
                    points.Add(line.Point);
                    break;
                case PolyLineSegment polyline:
                    points.AddRange(polyline.Points);
                    break;
                case QuadraticBezierSegment quadratic:
                    for (var index = 1; index <= 12; index++)
                        points.Add(Quadratic(from, quadratic.Point1, quadratic.Point2, index / 12d));
                    break;
                case BezierSegment bezier:
                    for (var index = 1; index <= 12; index++)
                        points.Add(Cubic(from, bezier.Point1, bezier.Point2, bezier.Point3, index / 12d));
                    break;
                case ArcSegment arc:
                    points.Add(arc.Point);
                    break;
            }
        }
        if (transform is { } own)
        {
            var matrix = own.Value;
            for (var index = 0; index < points.Count; index++)
                points[index] = Transform(matrix, points[index]);
        }
        return points;
    }

    private static void DrawHead(Path shape, DaisyTourArrowHead head, Point tip, Point neighbor, double length, double width, IBrush fill)
    {
        if (head == DaisyTourArrowHead.None)
            return;
        var direction = Unit(tip, neighbor);
        var baseCenter = Offset(tip, direction.X * length, direction.Y * length);
        var left = Offset(baseCenter, -direction.Y * width / 2, direction.X * width / 2);
        var right = Offset(baseCenter, direction.Y * width / 2, -direction.X * width / 2);
        shape.Fill = head == DaisyTourArrowHead.Open ? null : fill;
        shape.Data = head switch
        {
            DaisyTourArrowHead.Open => LineGeometry([left, tip, right]),
            DaisyTourArrowHead.Diamond => Polygon([tip, left, Offset(tip, direction.X * length * 2, direction.Y * length * 2), right]),
            DaisyTourArrowHead.Circle => new EllipseGeometry { Center = baseCenter, RadiusX = width / 2, RadiusY = width / 2 },
            _ => Polygon([tip, left, right])
        };
    }

    private static Geometry CloudGeometry(Rect rect, double padding)
    {
        // Scallops bulge from an inner rectangle to the outer bounds. Their valleys stay
        // shallower than the content padding, so the padded text area remains inside.
        var inset = Math.Min(Math.Max(0, padding) * 0.47, Math.Min(rect.Width, rect.Height) / 4);
        var spacing = Math.Max(1, padding * 1.75);
        Point[] corners =
        [
            new(rect.Left + inset, rect.Top + inset), new(rect.Right - inset, rect.Top + inset),
            new(rect.Right - inset, rect.Bottom - inset), new(rect.Left + inset, rect.Bottom - inset)
        ];
        var figure = new PathFigure { StartPoint = corners[0], IsClosed = true, IsFilled = true };
        for (var side = 0; side < corners.Length; side++)
        {
            var start = corners[side];
            var end = corners[(side + 1) % corners.Length];
            var count = Math.Max(1, (int)Math.Ceiling(Distance(start, end) / spacing));
            var chord = Distance(start, end) / count;
            var sag = Math.Min(inset, chord / 2);
            var radius = (chord * chord / 4 + sag * sag) / (2 * sag);
            for (var index = 1; index <= count; index++)
            {
                var fraction = (double)index / count;
                figure.Segments!.Add(new ArcSegment
                {
                    Point = new Point(start.X + (end.X - start.X) * fraction, start.Y + (end.Y - start.Y) * fraction),
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise
                });
            }
        }
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    private static PathGeometry RoundedRectangle(Rect rect, double radius)
    {
        var figure = new PathFigure { StartPoint = new Point(rect.Left + radius, rect.Top), IsClosed = true, IsFilled = true };
        figure.Segments!.Add(new LineSegment { Point = new Point(rect.Right - radius, rect.Top) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Right, rect.Top), Point2 = new Point(rect.Right, rect.Top + radius) });
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Right, rect.Bottom - radius) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Right, rect.Bottom), Point2 = new Point(rect.Right - radius, rect.Bottom) });
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Left + radius, rect.Bottom) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Left, rect.Bottom), Point2 = new Point(rect.Left, rect.Bottom - radius) });
        figure.Segments.Add(new LineSegment { Point = new Point(rect.Left, rect.Top + radius) });
        figure.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Left, rect.Top), Point2 = new Point(rect.Left + radius, rect.Top) });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    private static PathGeometry LineGeometry(IReadOnlyList<Point> points) => FigureGeometry(points, false);
    private static PathGeometry Polygon(IReadOnlyList<Point> points) => FigureGeometry(points, true);

    private static PathGeometry FigureGeometry(IReadOnlyList<Point> points, bool closed)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = closed, IsFilled = closed };
        for (var index = 1; index < points.Count; index++)
            figure.Segments!.Add(new LineSegment { Point = points[index] });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    private static Point Center(Rect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    private static Point Offset(Point point, double x, double y) => new(point.X + x, point.Y + y);
    private static double Distance(Point start, Point end) => Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
    private static Point Unit(Point start, Point end)
    {
        var length = Distance(start, end);
        return length > 0.001 ? new Point((end.X - start.X) / length, (end.Y - start.Y) / length) : new Point(1, 0);
    }

    private sealed class EffectiveStyle(DaisyTourAnnotation annotation, DaisyTourAnnotationStyle? step, DaisyTourAnnotationStyle? host,
        double scale = 1, DaisyTourProfileLayers profile = default,
        Dictionary<AvaloniaProperty, DaisyTourResolvedValue>? resolvedValues = null)
    {
        private readonly DaisyTourAnnotationStyle?[] _styles = [annotation, step, host];
        private readonly DaisyTourStyleOverride?[] _overrides = [profile.Annotation, profile.Step, profile.Tour];
        internal double Scale { get; } = double.IsFinite(scale) && scale > 0 ? scale : 1;
        internal int SourceScope { get; private set; } = 3;
        internal bool SourceIsOverride { get; private set; }
        internal DaisyTourAnnotationPreset Preset => Get(DaisyTourAnnotationStyle.PresetProperty, DaisyTourAnnotationPreset.ThinArrow);
        internal DaisyTourAnnotationKind Kind => Get(DaisyTourAnnotationStyle.KindProperty, Preset switch
        {
            DaisyTourAnnotationPreset.Cloud => DaisyTourAnnotationKind.Cloud,
            DaisyTourAnnotationPreset.SpeechBubble => DaisyTourAnnotationKind.SpeechBubble,
            DaisyTourAnnotationPreset.DottedLine or DaisyTourAnnotationPreset.DashedLine or DaisyTourAnnotationPreset.DashDotLine => DaisyTourAnnotationKind.Line,
            _ => DaisyTourAnnotationKind.Arrow
        });

        internal T Get<T>(AvaloniaProperty property, T fallback)
        {
            SourceScope = 3;
            SourceIsOverride = false;
            for (var index = 0; index < _styles.Length; index++)
            {
                if (DaisyTourProfileResolver.TryGetValue(_overrides[index], property, out var authored, out var inherit))
                {
                    if (inherit) continue;
                    SourceScope = index;
                    SourceIsOverride = true;
                    Record(property, authored);
                    return (T)authored!;
                }
                var value = _styles[index]?.GetValue(property);
                if (value is null || value is double number && !double.IsFinite(number) ||
                    value is Enum enumeration && Convert.ToInt32(enumeration) == 0)
                    continue;
                SourceScope = index;
                Record(property, value);
                return (T)value;
            }
            Record(property, fallback);
            return fallback;
        }

        internal void Record(AvaloniaProperty property, object? value, bool automatic = false, bool highContrast = false)
        {
            if (resolvedValues is null) return;
            var entry = resolvedValues.TryGetValue(property, out var previous)
                ? previous : new DaisyTourResolvedValue(value, SourceScope, SourceIsOverride);
            resolvedValues[property] = entry with { Value = value, Automatic = automatic, HighContrast = highContrast };
        }

        internal bool IsUnset(AvaloniaProperty property) => resolvedValues is not null &&
            resolvedValues.TryGetValue(property, out var value) && value.Value is double number && double.IsNaN(number);

        internal double Number(AvaloniaProperty property, double fallback) => Get(property, fallback);
        internal double Dimension(AvaloniaProperty property, double fallback) => Get(property, fallback * Scale);
        internal T AnnotationValue<T>(AvaloniaProperty property, T value, T fallback)
        {
            SourceScope = 0;
            SourceIsOverride = false;
            if (DaisyTourProfileResolver.TryGetValue(profile.Annotation, property, out var authored, out var inherit))
            {
                value = inherit ? fallback : (T)authored!;
                SourceScope = inherit ? 3 : 0;
                SourceIsOverride = !inherit;
            }
            Record(property, value);
            return value;
        }
    }
}

internal sealed record DaisyTourEditGeometry(Point Source, Point Target, Point FirstControl, Point SecondControl,
    Rect SourceBounds, Rect TargetBounds, DaisyTourAnnotationPath Path, double Curvature, double BodyWidth,
    double HeadLength, double HeadWidth, bool HasRouteLimit, Point BodyCenter, Point HeadDirection,
    Point HeadTip, bool IsBlock, bool HasEndHead);

internal sealed record DaisyTourResolvedValue(object? Value, int Scope, bool IsOverride,
    bool Automatic = false, bool HighContrast = false);
