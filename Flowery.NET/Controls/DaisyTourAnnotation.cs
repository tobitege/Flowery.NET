using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Flowery.Controls;

/// <summary>A live annotation anchored to the current bounds of one or two controls.</summary>
public sealed class DaisyTourAnnotation : DaisyTourAnnotationStyle
{
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, string>(nameof(Id), string.Empty);
    public static readonly StyledProperty<Control?> SourceProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, Control?>(nameof(Source));
    public static readonly StyledProperty<Control?> TargetProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, Control?>(nameof(Target));
    public static readonly StyledProperty<DaisyTourAnchor> SourceAnchorProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, DaisyTourAnchor>(nameof(SourceAnchor), DaisyTourAnchor.Auto);
    public static readonly StyledProperty<DaisyTourAnchor> TargetAnchorProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, DaisyTourAnchor>(nameof(TargetAnchor), DaisyTourAnchor.Auto);
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, string>(nameof(Text), string.Empty);
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, object?>(nameof(Content));
    public static readonly StyledProperty<Geometry?> GeometryProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, Geometry?>(nameof(Geometry));
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, IDataTemplate?>(nameof(Template));

    public static readonly StyledProperty<Control?> RouteLimitProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, Control?>(nameof(RouteLimit));
    public static readonly StyledProperty<double> RouteLimitCoordinateProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, double>(nameof(RouteLimitCoordinate), double.NaN);
    public static readonly StyledProperty<DaisyTourRouteSide> RouteLimitSideProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, DaisyTourRouteSide>(nameof(RouteLimitSide), DaisyTourRouteSide.None);
    public static readonly StyledProperty<double> RouteLimitMarginProperty =
        AvaloniaProperty.Register<DaisyTourAnnotation, double>(nameof(RouteLimitMargin), 12d);

    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }
    /// <summary>A control whose edge, plus RouteLimitMargin, limits the connector's course.</summary>
    public Control? RouteLimit { get => GetValue(RouteLimitProperty); set => SetValue(RouteLimitProperty, value); }
    /// <summary>A fixed window coordinate in DIP that limits the course when RouteLimit is not set:
    /// y for Above and Below, x for LeftOf and RightOf.</summary>
    public double RouteLimitCoordinate { get => GetValue(RouteLimitCoordinateProperty); set => SetValue(RouteLimitCoordinateProperty, value); }
    /// <summary>The side of the limit that the course between both ends must stay on.</summary>
    public DaisyTourRouteSide RouteLimitSide { get => GetValue(RouteLimitSideProperty); set => SetValue(RouteLimitSideProperty, value); }
    public double RouteLimitMargin { get => GetValue(RouteLimitMarginProperty); set => SetValue(RouteLimitMarginProperty, value); }
    public Control? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public Control? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public DaisyTourAnchor SourceAnchor { get => GetValue(SourceAnchorProperty); set => SetValue(SourceAnchorProperty, value); }
    public DaisyTourAnchor TargetAnchor { get => GetValue(TargetAnchorProperty); set => SetValue(TargetAnchorProperty, value); }
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public Geometry? Geometry { get => GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }
    public IDataTemplate? Template { get => GetValue(TemplateProperty); set => SetValue(TemplateProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == typeof(DaisyTourAnnotation)) NotifyChanged();
    }
}
