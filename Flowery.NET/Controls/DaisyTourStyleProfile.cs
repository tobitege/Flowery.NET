using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Flowery.Controls;

/// <summary>Portable styling overrides for an application-owned tour.</summary>
public sealed class DaisyTourStyleProfile
{
    [JsonRequired]
    public int SchemaVersion { get; set; } = 1;
    [JsonRequired]
    public string TourId { get; set; } = string.Empty;
    public DaisyTourStyleOverride? Style { get; set; }
    public DaisyTourStyleOverride? AnnotationDefaults { get; set; }
    public List<DaisyTourStepStyleProfile> Steps { get; set; } = [];
}

public sealed class DaisyTourStepStyleProfile
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    public DaisyTourStyleOverride? Style { get; set; }
    public DaisyTourStyleOverride? AnnotationDefaults { get; set; }
    public List<DaisyTourAnnotationStyleProfile> Annotations { get; set; } = [];
}

public sealed class DaisyTourAnnotationStyleProfile
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    public DaisyTourStyleOverride? Style { get; set; }
}

/// <summary>Values override application settings; Inherit skips settings at this scope.</summary>
public sealed class DaisyTourStyleOverride
{
    public DaisyTourStyleValues Values { get; set; } = new();
    public List<string> Inherit { get; set; } = [];
}

/// <summary>Supported JSON values. Null means no override. Colors use #AARRGGBB.</summary>
public sealed class DaisyTourStyleValues
{
    public DaisyTourAnnotationPreset? Preset { get; set; }
    public DaisyTourAnnotationKind? Kind { get; set; }
    public DaisyTourAnnotationPath? Path { get; set; }
    public DaisyTourArrowHead? StartHead { get; set; }
    public DaisyTourArrowHead? EndHead { get; set; }
    public DaisyTourArrowStyle? ArrowStyle { get; set; }
    public DaisyTourStrokeStyle? StrokeStyle { get; set; }
    public DaisyTourAnnotationTail? Tail { get; set; }
    public string? Stroke { get; set; }
    public string? Fill { get; set; }
    public string? Foreground { get; set; }
    public double[]? DashArray { get; set; }
    public double? StrokeThickness { get; set; }
    public double? DashOffset { get; set; }
    public double? HeadLength { get; set; }
    public double? HeadWidth { get; set; }
    public double? BodyWidth { get; set; }
    public double? Curvature { get; set; }
    public double? ControlPoint1X { get; set; }
    public double? ControlPoint1Y { get; set; }
    public double? ControlPoint2X { get; set; }
    public double? ControlPoint2Y { get; set; }
    public double? SourceOffsetX { get; set; }
    public double? SourceOffsetY { get; set; }
    public double? TargetOffsetX { get; set; }
    public double? TargetOffsetY { get; set; }
    public double? OffsetX { get; set; }
    public double? OffsetY { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public double? MaxWidth { get; set; }
    public double? MaxHeight { get; set; }
    public double? FontSize { get; set; }
    public double? CornerRadius { get; set; }
    public double? TailWidth { get; set; }
    public double? TailLength { get; set; }
    public double? Opacity { get; set; }
    public DaisyTourAnchor? SourceAnchor { get; set; }
    public DaisyTourAnchor? TargetAnchor { get; set; }
    public DaisyTourRouteSide? RouteLimitSide { get; set; }
    public double? RouteLimitCoordinate { get; set; }
    public double? RouteLimitMargin { get; set; }
    public DaisySize? Size { get; set; }
    public DaisyTourPlacement? Placement { get; set; }
    public DaisyTourCardPosition? CardPosition { get; set; }
    public double? CardX { get; set; }
    public double? CardY { get; set; }
    public string? CardBorderBrush { get; set; }
    public double? HighlightPadding { get; set; }
    public double? HighlightCornerRadius { get; set; }
}

