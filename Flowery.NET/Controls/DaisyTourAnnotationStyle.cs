using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;

namespace Flowery.Controls;

/// <summary>Common appearances that can be overridden property by property.</summary>
public enum DaisyTourAnnotationPreset { Default, ThinArrow, ThickArrow, BlockArrow, CurvedArrow, TwoEndedArrow, DottedLine, DashedLine, DashDotLine, Cloud, SpeechBubble }
public enum DaisyTourAnnotationKind { Default, Arrow, Line, Cloud, SpeechBubble, Custom }
public enum DaisyTourAnnotationPath { Default, Straight, Curved, Elbow, SCurve, Bezier, Custom }
public enum DaisyTourArrowHead { Default, None, Open, Triangle, Diamond, Circle }
public enum DaisyTourArrowStyle { Default, Line, Block }
public enum DaisyTourStrokeStyle { Default, Solid, Dotted, Dashed, DashDot, Custom }
public enum DaisyTourAnnotationTail { Default, None, Speech, Thought }
public enum DaisyTourAnchor { Auto, Top, Bottom, Left, Right, Center }
/// <summary>The side of a route limit that a connector's course must stay on.</summary>
public enum DaisyTourRouteSide { None, Above, Below, LeftOf, RightOf }

/// <summary>A live dash pattern whose values are multiples of stroke thickness.</summary>
public sealed class DaisyTourDashCollection : ObservableCollection<double>
{
}

/// <summary>
/// Appearance defaults shared by a tour, a step, or an individual annotation.
/// Null brushes, Default enums, and NaN measurements inherit the containing defaults.
/// Measurements are device-independent pixels, except Curvature and dash values.
/// </summary>
public class DaisyTourAnnotationStyle : AvaloniaObject
{
    public static readonly StyledProperty<DaisyTourAnnotationPreset> PresetProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourAnnotationPreset>(nameof(Preset), DaisyTourAnnotationPreset.Default);
    public static readonly StyledProperty<DaisyTourAnnotationKind> KindProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourAnnotationKind>(nameof(Kind), DaisyTourAnnotationKind.Default);
    public static readonly StyledProperty<DaisyTourAnnotationPath> PathProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourAnnotationPath>(nameof(Path), DaisyTourAnnotationPath.Default);
    public static readonly StyledProperty<DaisyTourArrowHead> StartHeadProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourArrowHead>(nameof(StartHead), DaisyTourArrowHead.Default);
    public static readonly StyledProperty<DaisyTourArrowHead> EndHeadProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourArrowHead>(nameof(EndHead), DaisyTourArrowHead.Default);
    public static readonly StyledProperty<DaisyTourArrowStyle> ArrowStyleProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourArrowStyle>(nameof(ArrowStyle), DaisyTourArrowStyle.Default);
    public static readonly StyledProperty<DaisyTourStrokeStyle> StrokeStyleProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourStrokeStyle>(nameof(StrokeStyle), DaisyTourStrokeStyle.Default);
    public static readonly StyledProperty<DaisyTourAnnotationTail> TailProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourAnnotationTail>(nameof(Tail), DaisyTourAnnotationTail.Default);
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<DaisyTourDashCollection?> DashArrayProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, DaisyTourDashCollection?>(nameof(DashArray));
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(StrokeThickness), double.NaN);
    public static readonly StyledProperty<double> DashOffsetProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(DashOffset), double.NaN);
    public static readonly StyledProperty<double> HeadLengthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(HeadLength), double.NaN);
    public static readonly StyledProperty<double> HeadWidthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(HeadWidth), double.NaN);
    public static readonly StyledProperty<double> BodyWidthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(BodyWidth), double.NaN);
    public static readonly StyledProperty<double> CurvatureProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(Curvature), double.NaN);
    public static readonly StyledProperty<double> ControlPoint1XProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(ControlPoint1X), double.NaN);
    public static readonly StyledProperty<double> ControlPoint1YProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(ControlPoint1Y), double.NaN);
    public static readonly StyledProperty<double> ControlPoint2XProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(ControlPoint2X), double.NaN);
    public static readonly StyledProperty<double> ControlPoint2YProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(ControlPoint2Y), double.NaN);
    public static readonly StyledProperty<double> SourceOffsetXProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(SourceOffsetX), double.NaN);
    public static readonly StyledProperty<double> SourceOffsetYProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(SourceOffsetY), double.NaN);
    public static readonly StyledProperty<double> TargetOffsetXProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(TargetOffsetX), double.NaN);
    public static readonly StyledProperty<double> TargetOffsetYProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(TargetOffsetY), double.NaN);
    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(OffsetX), double.NaN);
    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(OffsetY), double.NaN);
    public static readonly StyledProperty<double> WidthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(Width), double.NaN);
    public static readonly StyledProperty<double> HeightProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(Height), double.NaN);
    public static readonly StyledProperty<double> MaxWidthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(MaxWidth), double.NaN);
    public static readonly StyledProperty<double> MaxHeightProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(MaxHeight), double.NaN);
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(FontSize), double.NaN);
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(CornerRadius), double.NaN);
    public static readonly StyledProperty<double> TailWidthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(TailWidth), double.NaN);
    public static readonly StyledProperty<double> TailLengthProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(TailLength), double.NaN);
    public static readonly StyledProperty<double> OpacityProperty =
        AvaloniaProperty.Register<DaisyTourAnnotationStyle, double>(nameof(Opacity), double.NaN);

    public DaisyTourAnnotationPreset Preset { get => GetValue(PresetProperty); set => SetValue(PresetProperty, value); }
    public DaisyTourAnnotationKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public DaisyTourAnnotationPath Path { get => GetValue(PathProperty); set => SetValue(PathProperty, value); }
    public DaisyTourArrowHead StartHead { get => GetValue(StartHeadProperty); set => SetValue(StartHeadProperty, value); }
    public DaisyTourArrowHead EndHead { get => GetValue(EndHeadProperty); set => SetValue(EndHeadProperty, value); }
    public DaisyTourArrowStyle ArrowStyle { get => GetValue(ArrowStyleProperty); set => SetValue(ArrowStyleProperty, value); }
    public DaisyTourStrokeStyle StrokeStyle { get => GetValue(StrokeStyleProperty); set => SetValue(StrokeStyleProperty, value); }
    public DaisyTourAnnotationTail Tail { get => GetValue(TailProperty); set => SetValue(TailProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public DaisyTourDashCollection? DashArray { get => GetValue(DashArrayProperty); set => SetValue(DashArrayProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double DashOffset { get => GetValue(DashOffsetProperty); set => SetValue(DashOffsetProperty, value); }
    public double HeadLength { get => GetValue(HeadLengthProperty); set => SetValue(HeadLengthProperty, value); }
    public double HeadWidth { get => GetValue(HeadWidthProperty); set => SetValue(HeadWidthProperty, value); }
    public double BodyWidth { get => GetValue(BodyWidthProperty); set => SetValue(BodyWidthProperty, value); }
    public double Curvature { get => GetValue(CurvatureProperty); set => SetValue(CurvatureProperty, value); }
    /// <summary>Bezier control points in connector coordinates: (0,0) is the start, (1,0) the end.
    /// Positive Y bends to the same side as a positive Curvature.</summary>
    public double ControlPoint1X { get => GetValue(ControlPoint1XProperty); set => SetValue(ControlPoint1XProperty, value); }
    public double ControlPoint1Y { get => GetValue(ControlPoint1YProperty); set => SetValue(ControlPoint1YProperty, value); }
    public double ControlPoint2X { get => GetValue(ControlPoint2XProperty); set => SetValue(ControlPoint2XProperty, value); }
    public double ControlPoint2Y { get => GetValue(ControlPoint2YProperty); set => SetValue(ControlPoint2YProperty, value); }
    public double SourceOffsetX { get => GetValue(SourceOffsetXProperty); set => SetValue(SourceOffsetXProperty, value); }
    public double SourceOffsetY { get => GetValue(SourceOffsetYProperty); set => SetValue(SourceOffsetYProperty, value); }
    public double TargetOffsetX { get => GetValue(TargetOffsetXProperty); set => SetValue(TargetOffsetXProperty, value); }
    public double TargetOffsetY { get => GetValue(TargetOffsetYProperty); set => SetValue(TargetOffsetYProperty, value); }
    public double OffsetX { get => GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
    public double OffsetY { get => GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }
    public double Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
    public double Height { get => GetValue(HeightProperty); set => SetValue(HeightProperty, value); }
    public double MaxWidth { get => GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty, value); }
    public double MaxHeight { get => GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double TailWidth { get => GetValue(TailWidthProperty); set => SetValue(TailWidthProperty, value); }
    public double TailLength { get => GetValue(TailLengthProperty); set => SetValue(TailLengthProperty, value); }
    public double Opacity { get => GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }

    internal event EventHandler? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == typeof(DaisyTourAnnotationStyle)) OnChanged(this, change);
    }

    private static void OnChanged(AvaloniaObject sender, AvaloniaPropertyChangedEventArgs args)
    {
        var style = (DaisyTourAnnotationStyle)sender;
        if (args.Property == DashArrayProperty)
        {
            if (args.OldValue is DaisyTourDashCollection oldPattern)
                oldPattern.CollectionChanged -= style.OnDashPatternChanged;
            if (args.NewValue is DaisyTourDashCollection newPattern)
                newPattern.CollectionChanged += style.OnDashPatternChanged;
        }
        style.NotifyChanged();
    }

    private void OnDashPatternChanged(object? sender, NotifyCollectionChangedEventArgs args) => NotifyChanged();
    protected void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
