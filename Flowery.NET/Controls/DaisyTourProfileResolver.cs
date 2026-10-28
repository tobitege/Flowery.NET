using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace Flowery.Controls;

/// <summary>Reads the supported portable values without changing application properties.</summary>
internal static class DaisyTourProfileResolver
{
    private sealed record Property(string Name, Type Type, AvaloniaProperty[] Properties,
        Func<DaisyTourStyleValues, object?> Read, Action<DaisyTourStyleValues, object?> Write);

    private static readonly Property[] Properties =
    [
        new("preset", typeof(DaisyTourAnnotationPreset), [DaisyTourAnnotationStyle.PresetProperty], values => values.Preset, (values, value) => values.Preset = (DaisyTourAnnotationPreset?)value),
        new("kind", typeof(DaisyTourAnnotationKind), [DaisyTourAnnotationStyle.KindProperty], values => values.Kind, (values, value) => values.Kind = (DaisyTourAnnotationKind?)value),
        new("path", typeof(DaisyTourAnnotationPath), [DaisyTourAnnotationStyle.PathProperty], values => values.Path, (values, value) => values.Path = (DaisyTourAnnotationPath?)value),
        new("startHead", typeof(DaisyTourArrowHead), [DaisyTourAnnotationStyle.StartHeadProperty], values => values.StartHead, (values, value) => values.StartHead = (DaisyTourArrowHead?)value),
        new("endHead", typeof(DaisyTourArrowHead), [DaisyTourAnnotationStyle.EndHeadProperty], values => values.EndHead, (values, value) => values.EndHead = (DaisyTourArrowHead?)value),
        new("arrowStyle", typeof(DaisyTourArrowStyle), [DaisyTourAnnotationStyle.ArrowStyleProperty], values => values.ArrowStyle, (values, value) => values.ArrowStyle = (DaisyTourArrowStyle?)value),
        new("strokeStyle", typeof(DaisyTourStrokeStyle), [DaisyTourAnnotationStyle.StrokeStyleProperty], values => values.StrokeStyle, (values, value) => values.StrokeStyle = (DaisyTourStrokeStyle?)value),
        new("tail", typeof(DaisyTourAnnotationTail), [DaisyTourAnnotationStyle.TailProperty], values => values.Tail, (values, value) => values.Tail = (DaisyTourAnnotationTail?)value),
        new("stroke", typeof(string), [DaisyTourAnnotationStyle.StrokeProperty], values => values.Stroke, (values, value) => values.Stroke = (string?)value),
        new("fill", typeof(string), [DaisyTourAnnotationStyle.FillProperty], values => values.Fill, (values, value) => values.Fill = (string?)value),
        new("foreground", typeof(string), [DaisyTourAnnotationStyle.ForegroundProperty], values => values.Foreground, (values, value) => values.Foreground = (string?)value),
        new("dashArray", typeof(double[]), [DaisyTourAnnotationStyle.DashArrayProperty], values => values.DashArray, (values, value) => values.DashArray = (double[]?)value),
        new("strokeThickness", typeof(double), [DaisyTourAnnotationStyle.StrokeThicknessProperty], values => values.StrokeThickness, (values, value) => values.StrokeThickness = (double?)value),
        new("dashOffset", typeof(double), [DaisyTourAnnotationStyle.DashOffsetProperty], values => values.DashOffset, (values, value) => values.DashOffset = (double?)value),
        new("headLength", typeof(double), [DaisyTourAnnotationStyle.HeadLengthProperty], values => values.HeadLength, (values, value) => values.HeadLength = (double?)value),
        new("headWidth", typeof(double), [DaisyTourAnnotationStyle.HeadWidthProperty], values => values.HeadWidth, (values, value) => values.HeadWidth = (double?)value),
        new("bodyWidth", typeof(double), [DaisyTourAnnotationStyle.BodyWidthProperty], values => values.BodyWidth, (values, value) => values.BodyWidth = (double?)value),
        new("curvature", typeof(double), [DaisyTourAnnotationStyle.CurvatureProperty], values => values.Curvature, (values, value) => values.Curvature = (double?)value),
        new("controlPoint1X", typeof(double), [DaisyTourAnnotationStyle.ControlPoint1XProperty], values => values.ControlPoint1X, (values, value) => values.ControlPoint1X = (double?)value),
        new("controlPoint1Y", typeof(double), [DaisyTourAnnotationStyle.ControlPoint1YProperty], values => values.ControlPoint1Y, (values, value) => values.ControlPoint1Y = (double?)value),
        new("controlPoint2X", typeof(double), [DaisyTourAnnotationStyle.ControlPoint2XProperty], values => values.ControlPoint2X, (values, value) => values.ControlPoint2X = (double?)value),
        new("controlPoint2Y", typeof(double), [DaisyTourAnnotationStyle.ControlPoint2YProperty], values => values.ControlPoint2Y, (values, value) => values.ControlPoint2Y = (double?)value),
        new("sourceOffsetX", typeof(double), [DaisyTourAnnotationStyle.SourceOffsetXProperty], values => values.SourceOffsetX, (values, value) => values.SourceOffsetX = (double?)value),
        new("sourceOffsetY", typeof(double), [DaisyTourAnnotationStyle.SourceOffsetYProperty], values => values.SourceOffsetY, (values, value) => values.SourceOffsetY = (double?)value),
        new("targetOffsetX", typeof(double), [DaisyTourAnnotationStyle.TargetOffsetXProperty], values => values.TargetOffsetX, (values, value) => values.TargetOffsetX = (double?)value),
        new("targetOffsetY", typeof(double), [DaisyTourAnnotationStyle.TargetOffsetYProperty], values => values.TargetOffsetY, (values, value) => values.TargetOffsetY = (double?)value),
        new("offsetX", typeof(double), [DaisyTourAnnotationStyle.OffsetXProperty], values => values.OffsetX, (values, value) => values.OffsetX = (double?)value),
        new("offsetY", typeof(double), [DaisyTourAnnotationStyle.OffsetYProperty], values => values.OffsetY, (values, value) => values.OffsetY = (double?)value),
        new("width", typeof(double), [DaisyTourAnnotationStyle.WidthProperty], values => values.Width, (values, value) => values.Width = (double?)value),
        new("height", typeof(double), [DaisyTourAnnotationStyle.HeightProperty], values => values.Height, (values, value) => values.Height = (double?)value),
        new("maxWidth", typeof(double), [DaisyTourAnnotationStyle.MaxWidthProperty], values => values.MaxWidth, (values, value) => values.MaxWidth = (double?)value),
        new("maxHeight", typeof(double), [DaisyTourAnnotationStyle.MaxHeightProperty], values => values.MaxHeight, (values, value) => values.MaxHeight = (double?)value),
        new("fontSize", typeof(double), [DaisyTourAnnotationStyle.FontSizeProperty], values => values.FontSize, (values, value) => values.FontSize = (double?)value),
        new("cornerRadius", typeof(double), [DaisyTourAnnotationStyle.CornerRadiusProperty], values => values.CornerRadius, (values, value) => values.CornerRadius = (double?)value),
        new("tailWidth", typeof(double), [DaisyTourAnnotationStyle.TailWidthProperty], values => values.TailWidth, (values, value) => values.TailWidth = (double?)value),
        new("tailLength", typeof(double), [DaisyTourAnnotationStyle.TailLengthProperty], values => values.TailLength, (values, value) => values.TailLength = (double?)value),
        new("opacity", typeof(double), [DaisyTourAnnotationStyle.OpacityProperty], values => values.Opacity, (values, value) => values.Opacity = (double?)value),
        new("sourceAnchor", typeof(DaisyTourAnchor), [DaisyTourAnnotation.SourceAnchorProperty], values => values.SourceAnchor, (values, value) => values.SourceAnchor = (DaisyTourAnchor?)value),
        new("targetAnchor", typeof(DaisyTourAnchor), [DaisyTourAnnotation.TargetAnchorProperty], values => values.TargetAnchor, (values, value) => values.TargetAnchor = (DaisyTourAnchor?)value),
        new("routeLimitSide", typeof(DaisyTourRouteSide), [DaisyTourAnnotation.RouteLimitSideProperty], values => values.RouteLimitSide, (values, value) => values.RouteLimitSide = (DaisyTourRouteSide?)value),
        new("routeLimitCoordinate", typeof(double), [DaisyTourAnnotation.RouteLimitCoordinateProperty], values => values.RouteLimitCoordinate, (values, value) => values.RouteLimitCoordinate = (double?)value),
        new("routeLimitMargin", typeof(double), [DaisyTourAnnotation.RouteLimitMarginProperty], values => values.RouteLimitMargin, (values, value) => values.RouteLimitMargin = (double?)value),
        new("size", typeof(DaisySize), [DaisyTour.SizeProperty], values => values.Size, (values, value) => values.Size = (DaisySize?)value),
        new("placement", typeof(DaisyTourPlacement), [DaisyTourStep.PlacementProperty], values => values.Placement, (values, value) => values.Placement = (DaisyTourPlacement?)value),
        new("cardPosition", typeof(DaisyTourCardPosition), [DaisyTour.CardPositionProperty, DaisyTourStep.CardPositionProperty], values => values.CardPosition, (values, value) => values.CardPosition = (DaisyTourCardPosition?)value),
        new("cardX", typeof(double), [DaisyTourStep.CardXProperty], values => values.CardX, (values, value) => values.CardX = (double?)value),
        new("cardY", typeof(double), [DaisyTourStep.CardYProperty], values => values.CardY, (values, value) => values.CardY = (double?)value),
        new("cardBorderBrush", typeof(string), [DaisyTour.CardBorderBrushProperty], values => values.CardBorderBrush, (values, value) => values.CardBorderBrush = (string?)value),
        new("highlightPadding", typeof(double), [DaisyTour.HighlightPaddingProperty], values => values.HighlightPadding, (values, value) => values.HighlightPadding = (double?)value),
        new("highlightCornerRadius", typeof(double), [DaisyTour.HighlightCornerRadiusProperty], values => values.HighlightCornerRadius, (values, value) => values.HighlightCornerRadius = (double?)value),
    ];

    internal static IReadOnlyList<string> PropertyNames { get; } = Array.AsReadOnly(Properties.Select(property => property.Name).ToArray());
    internal static AvaloniaProperty? FindProperty(string name) => Find(name)?.Properties[0];
    internal static Type? ValueType(string name) => Find(name)?.Type;
    internal static object? GetValue(DaisyTourStyleOverride? scope, string name) => scope is null ? null : Required(name).Read(scope.Values);

    internal static void SetValue(DaisyTourStyleOverride scope, string name, object? value)
    {
        var property = Required(name);
        if (value is not null) ValidateValue(name, value);
        property.Write(scope.Values, value is double[] numbers ? numbers.ToArray() : value);
        scope.Inherit.RemoveAll(item => item == name);
    }

    internal static void ClearValue(DaisyTourStyleOverride scope, string name) => SetValue(scope, name, null);

    internal static void InheritValue(DaisyTourStyleOverride scope, string name)
    {
        ClearValue(scope, name);
        scope.Inherit.Add(name);
    }

    internal static DaisyTourStepStyleProfile? FindStep(DaisyTourStyleProfile? profile, string id) =>
        profile?.Steps.FirstOrDefault(step => step.Id == id);

    internal static DaisyTourAnnotationStyleProfile? FindAnnotation(DaisyTourStepStyleProfile? step, string id) =>
        step?.Annotations.FirstOrDefault(annotation => annotation.Id == id);

    internal static bool TryGetValue(DaisyTourStyleOverride? scope, AvaloniaProperty property, out object? value, out bool inherit)
    {
        value = null;
        inherit = false;
        if (scope is null) return false;
        var entry = Properties.FirstOrDefault(entry => entry.Properties.Contains(property));
        if (entry is null) return false;
        inherit = scope.Inherit.Contains(entry.Name, StringComparer.Ordinal);
        if (inherit) return true;
        value = entry.Read(scope.Values);
        if (value is null) return false;
        if (value is string color)
            value = new SolidColorBrush(ParseColor(color));
        else if (value is double[] numbers)
        {
            var dashes = new DaisyTourDashCollection();
            foreach (var number in numbers) dashes.Add(number);
            value = dashes;
        }
        return true;
    }

    internal static void ValidateValue(string name, object value)
    {
        var property = Required(name);
        if (!property.Type.IsInstanceOfType(value))
            throw new ArgumentException($"'{name}' requires {property.Type.Name}.");
        if (value is double number)
        {
            if (!double.IsFinite(number)) throw new ArgumentException($"'{name}' must be finite.");
            if (name == "opacity" && (number < 0 || number > 1)) throw new ArgumentException("'opacity' must be between 0 and 1.");
            if (name is "cardX" or "cardY" && (number < 0 || number > 1)) throw new ArgumentException($"'{name}' must be between 0 and 1.");
            if (Nonnegative.Contains(name) && number < 0) throw new ArgumentException($"'{name}' cannot be negative.");
            if (Positive.Contains(name) && number <= 0) throw new ArgumentException($"'{name}' must be greater than zero.");
        }
        else if (value is double[] numbers)
        {
            if (numbers.Length is < 1 or > 64 || numbers.Any(number => !double.IsFinite(number) || number <= 0))
                throw new ArgumentException("'dashArray' requires 1 to 64 positive finite lengths.");
        }
        else if (value is string color) _ = ParseColor(color);
        else if (value is Enum enumeration &&
            (!Enum.IsDefined(property.Type, enumeration) || enumeration.ToString() == "Default"))
            throw new ArgumentException($"'{name}' requires a defined value; use inherit instead of Default.");
    }

    internal static bool IsEnumValue(string name, string value)
    {
        var type = ValueType(name);
        return type?.IsEnum == true && Enum.GetNames(type).Contains(value, StringComparer.Ordinal) && value != "Default";
    }

    private static readonly HashSet<string> Nonnegative = new(StringComparer.Ordinal)
    {
        "strokeThickness", "headLength", "headWidth", "bodyWidth", "cornerRadius", "tailWidth", "tailLength",
        "routeLimitMargin", "highlightPadding", "highlightCornerRadius"
    };
    private static readonly HashSet<string> Positive = new(StringComparer.Ordinal)
    {
        "width", "height", "maxWidth", "maxHeight", "fontSize"
    };

    private static Property? Find(string name) => Properties.FirstOrDefault(property => property.Name == name);
    private static Property Required(string name) => Find(name) ?? throw new ArgumentException($"Unknown style property '{name}'.");

    private static Color ParseColor(string text)
    {
        if (text.Length != 9 || text[0] != '#' ||
            !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Colors must use #AARRGGBB.");
        return Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
}

