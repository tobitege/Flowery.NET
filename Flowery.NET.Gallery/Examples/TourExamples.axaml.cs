using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Gallery.Localization;

namespace Flowery.NET.Gallery.Examples;

public sealed partial class TourExamples : UserControl, IScrollableExample
{
    private readonly DaisyTourStep[] _standardSteps;

    public TourExamples()
    {
        InitializeComponent();
        _standardSteps = Tour.Steps.ToArray();
        BindText(_standardSteps[0], DaisyTourStep.TitleProperty, "Gallery_Tour_WelcomeTitle");
        BindText(_standardSteps[0], DaisyTourStep.DescriptionProperty, "Gallery_Tour_WelcomeDescription");
        BindText(_standardSteps[1], DaisyTourStep.TitleProperty, "Gallery_Tour_ScrollTitle");
        BindText(_standardSteps[1], DaisyTourStep.DescriptionProperty, "Gallery_Tour_ScrollDescription");
        BindText(_standardSteps[2], DaisyTourStep.TitleProperty, "Gallery_Tour_DeferredTitle");
        BindText(_standardSteps[2], DaisyTourStep.DescriptionProperty, "Gallery_Tour_DeferredDescription");
        _standardSteps[0].PrepareAsync = PrepareOverviewAsync;
        _standardSteps[1].PrepareAsync = PrepareOverviewAsync;
        _standardSteps[2].PrepareAsync = PrepareDetailsAsync;
        // Positions are API names and stay untranslated.
        CardPositionSelector.ItemsSource = Enum.GetValues<DaisyTourCardPosition>()
            .Where(position => position != DaisyTourCardPosition.Default).ToArray();
        CardPositionSelector.SelectedItem = Tour.CardPosition;
        InitializeTourDesigner();
    }

    public GalleryLocalization Localization => GalleryLocalization.Instance;

    private void CardPosition_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CardPositionSelector.SelectedItem is DaisyTourCardPosition position)
            Tour.CardPosition = position;
    }

    public void ScrollToSection(string sectionName)
    {
        var header = this.GetVisualDescendants().OfType<SectionHeader>()
            .FirstOrDefault(item => string.Equals(item.SectionId, sectionName, StringComparison.OrdinalIgnoreCase));
        if (header?.TransformToVisual(MainScrollViewer) is { } transform)
            MainScrollViewer.Offset = new Vector(0, transform.Transform(new Point(0, 0)).Y + MainScrollViewer.Offset.Y);
    }

    private async void StartTour_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        Tour.Steps.Clear();
        foreach (var step in _standardSteps)
            Tour.Steps.Add(step);
        await Tour.StartAsync();
    }

    private async void Arrows_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        var presets = new[]
        {
            DaisyTourAnnotationPreset.ThinArrow,
            DaisyTourAnnotationPreset.ThickArrow,
            DaisyTourAnnotationPreset.BlockArrow,
            DaisyTourAnnotationPreset.CurvedArrow,
            DaisyTourAnnotationPreset.TwoEndedArrow
        };
        Tour.Steps.Clear();
        foreach (var preset in presets)
        {
            var annotation = new DaisyTourAnnotation { Preset = preset };
            if (preset == DaisyTourAnnotationPreset.BlockArrow)
            {
                annotation.Stroke = new SolidColorBrush(Colors.OrangeRed);
                annotation.Fill = new SolidColorBrush(Colors.Gold);
            }
            if (preset == DaisyTourAnnotationPreset.CurvedArrow)
            {
                annotation.StartHead = DaisyTourArrowHead.Circle;
                annotation.EndHead = DaisyTourArrowHead.Open;
            }
            AddAnnotationStep(preset.ToString(), annotation, connectTargets: true);
        }
        await Tour.StartAsync();
    }

    private async void Lines_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        Tour.Steps.Clear();
        var styles = new[]
        {
            DaisyTourStrokeStyle.Solid,
            DaisyTourStrokeStyle.Dotted,
            DaisyTourStrokeStyle.Dashed,
            DaisyTourStrokeStyle.DashDot,
            DaisyTourStrokeStyle.Custom
        };
        foreach (var style in styles)
        {
            var annotation = new DaisyTourAnnotation
            {
                Kind = DaisyTourAnnotationKind.Line,
                StrokeStyle = style,
                Path = style switch
                {
                    DaisyTourStrokeStyle.Dashed => DaisyTourAnnotationPath.Elbow,
                    DaisyTourStrokeStyle.Dotted => DaisyTourAnnotationPath.Curved,
                    _ => DaisyTourAnnotationPath.Straight
                },
                StartHead = DaisyTourArrowHead.None,
                EndHead = DaisyTourArrowHead.None,
                StrokeThickness = 4,
                DashArray = style == DaisyTourStrokeStyle.Custom ? new DaisyTourDashCollection { 6, 2, 1, 2, 1, 2 } : null,
                DashOffset = 1
            };
            // Curved and elbow lines start below the first targets, so their course is visible.
            var source = style is DaisyTourStrokeStyle.Dotted or DaisyTourStrokeStyle.Dashed ? NestedTarget : PrimaryTarget;
            AddAnnotationStep(style.ToString(), annotation, connectTargets: true, source);
        }
        await Tour.StartAsync();
    }

    private async void Bubbles_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        Tour.Steps.Clear();
        AddBubbleSteps(DaisyTourAnnotationKind.Cloud, DaisyTourAnnotationTail.Thought);
        AddBubbleSteps(DaisyTourAnnotationKind.SpeechBubble, DaisyTourAnnotationTail.Speech);
        await Tour.StartAsync();
    }

    private void AddBubbleSteps(DaisyTourAnnotationKind kind, DaisyTourAnnotationTail tail)
    {
        AddBubbleStep(kind, DaisyTourAnnotationTail.None, hasText: false);
        AddBubbleStep(kind, tail, hasText: false);
        AddBubbleStep(kind, DaisyTourAnnotationTail.None, hasText: true);
        AddBubbleStep(kind, tail, hasText: true);
    }

    private void AddBubbleStep(DaisyTourAnnotationKind kind, DaisyTourAnnotationTail tail, bool hasText)
    {
        var annotation = new DaisyTourAnnotation
        {
            Kind = kind,
            Tail = tail,
            Width = hasText ? 230 : 90,
            Height = hasText ? double.NaN : 70,
            MaxWidth = 240,
            CornerRadius = tail == DaisyTourAnnotationTail.None ? 0 : 20
        };
        if (hasText)
            BindText(annotation, DaisyTourAnnotation.TextProperty, "Gallery_Tour_BubbleText");
        AddAnnotationStep($"{kind} / {tail} / {(hasText ? "Text" : "No text")}", annotation);
    }

    private async void Custom_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        Tour.Steps.Clear();
        var annotation = new DaisyTourAnnotation
        {
            Kind = DaisyTourAnnotationKind.Custom,
            Geometry = new EllipseGeometry { Center = new Point(60, 35), RadiusX = 58, RadiusY = 33 },
            StrokeThickness = 5,
            Stroke = new SolidColorBrush(Colors.OrangeRed),
            Fill = new SolidColorBrush(Colors.Transparent)
        };
        AddAnnotationStep("Custom", annotation);
        BindText(Tour.Steps[0], DaisyTourStep.DescriptionProperty, "Gallery_Tour_CustomDescription");
        await Tour.StartAsync();
    }

    private async void Regions_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        if (FindThemeSelection() is not { } themeSelection)
            return;
        Tour.Steps.Clear();
        var step = new DaisyTourStep { Id = "regions", Target = RegionTarget, PrepareAsync = PrepareOverviewAsync };
        BindText(step, DaisyTourStep.TitleProperty, "Gallery_Tour_RegionsTitle");
        BindText(step, DaisyTourStep.DescriptionProperty, "Gallery_Tour_RegionsDescription");
        step.Annotations.Add(new DaisyTourAnnotation
        {
            Id = "theme-to-page",
            Source = themeSelection,
            Target = RegionTarget,
            Preset = DaisyTourAnnotationPreset.CurvedArrow,
            Curvature = 0.15
        });
        Tour.Steps.Add(step);
        await Tour.StartAsync();
    }

    private async void Paths_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        if (FindThemeSelection() is not { } themeSelection)
            return;
        Tour.Steps.Clear();
        var wave = Geometry.Parse("M0,0 C20,-30 30,-30 50,0 C70,30 80,30 100,0");
        var paths = new[]
        {
            new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.Elbow, CornerRadius = 18 },
            new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.SCurve },
            new DaisyTourAnnotation
            {
                Path = DaisyTourAnnotationPath.Bezier,
                ControlPoint1X = 0.25, ControlPoint1Y = -0.25, ControlPoint2X = 0.75, ControlPoint2Y = 0.25
            },
            new DaisyTourAnnotation { Path = DaisyTourAnnotationPath.Custom, Geometry = wave }
        };
        foreach (var annotation in paths)
        {
            var step = new DaisyTourStep
            {
                Id = $"path-{Tour.Steps.Count}", Title = annotation.Path.ToString(), Target = RegionTarget,
                PrepareAsync = PrepareOverviewAsync
            };
            BindText(step, DaisyTourStep.DescriptionProperty, "Gallery_Tour_PathsDescription");
            annotation.Id = $"path-shape-{Tour.Steps.Count}";
            annotation.Source = themeSelection;
            annotation.Target = RegionTarget;
            annotation.StrokeThickness = 4;
            step.Annotations.Add(annotation);
            Tour.Steps.Add(step);
        }
        await Tour.StartAsync();
    }

    private async void RouteLimit_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        Tour.Steps.Clear();
        // The fixed coordinate is taken once; unlike the control limit, it does not follow scrolling.
        // The tour draws in the overlay layer of the window, so the coordinate is relative to that layer.
        var keepClear = OverlayLayer.GetOverlayLayer(this) is { } overlay && KeepClearArea.TransformToVisual(overlay) is { } transform
            ? new Rect(0, 0, KeepClearArea.Bounds.Width, KeepClearArea.Bounds.Height).TransformToAABB(transform)
            : default;
        var limits = new[]
        {
            new DaisyTourAnnotation
            {
                Path = DaisyTourAnnotationPath.Elbow, CornerRadius = 12,
                RouteLimit = KeepClearArea, RouteLimitSide = DaisyTourRouteSide.Above
            },
            new DaisyTourAnnotation
            {
                Path = DaisyTourAnnotationPath.SCurve, RouteLimit = KeepClearArea, RouteLimitSide = DaisyTourRouteSide.Below
            },
            new DaisyTourAnnotation
            {
                Path = DaisyTourAnnotationPath.Elbow, CornerRadius = 12,
                RouteLimitCoordinate = keepClear.Top - 12, RouteLimitSide = DaisyTourRouteSide.Above
            }
        };
        foreach (var annotation in limits)
        {
            annotation.StrokeThickness = 4;
            var title = annotation.RouteLimit is null ? nameof(DaisyTourAnnotation.RouteLimitCoordinate) : annotation.RouteLimitSide.ToString();
            // The lower left and upper right targets have the keep-clear area between them.
            AddAnnotationStep(title, annotation, connectTargets: true, NestedTarget);
            // Both ends of the coordinate demo lie below the limit, so its route bridges over it.
            if (annotation.RouteLimit is null) annotation.Target = LimitTarget;
            BindText(Tour.Steps[^1], DaisyTourStep.DescriptionProperty, "Gallery_Tour_RouteLimitDescription");
        }
        await Tour.StartAsync();
    }

    // The theme selection belongs to the window's sidebar, outside this page.
    private DaisyThemeDropdown? FindThemeSelection()
    {
        return TopLevel.GetTopLevel(this) is { } root ? FindDescendant<DaisyThemeDropdown>(root) : null;
    }

    private static T? FindDescendant<T>(Visual root) where T : Visual
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is T match)
                return match;
            if (FindDescendant<T>(child) is { } nested)
                return nested;
        }
        return null;
    }

    private void AddAnnotationStep(string title, DaisyTourAnnotation annotation, bool connectTargets = false,
        Control? source = null)
    {
        source ??= PrimaryTarget;
        var step = new DaisyTourStep
        {
            Id = $"annotation-{Tour.Steps.Count}",
            Title = title,
            Target = source,
            PrepareAsync = PrepareOverviewAsync,
            AnnotationDefaults = new DaisyTourAnnotationStyle { Opacity = 0.95 }
        };
        BindText(step, DaisyTourStep.DescriptionProperty, "Gallery_Tour_AnnotationDescription");
        annotation.Id = $"shape-{Tour.Steps.Count}";
        annotation.Target = connectTargets ? RelatedTarget : PrimaryTarget;
        annotation.Source = connectTargets ? source : null;
        step.Annotations.Add(annotation);
        Tour.Steps.Add(step);
    }

    private Task PrepareOverviewAsync(DaisyTourStep step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DemoTabs.SelectedItem = OverviewTab;
        return Task.CompletedTask;
    }

    private Task PrepareDetailsAsync(DaisyTourStep step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DetailsTab.Content is not DaisyButton)
        {
            var target = new DaisyButton
            {
                Margin = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Variant = DaisyButtonVariant.Primary
            };
            BindText(target, ContentControl.ContentProperty, "Gallery_Tour_Target");
            DetailsTab.Content = target;
        }
        DemoTabs.SelectedItem = DetailsTab;
        step.Target = (DaisyButton)DetailsTab.Content;
        return Task.CompletedTask;
    }

    private void BindText(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        target.Bind(property, new Binding($"[{key}]")
        {
            Source = Localization,
            Mode = BindingMode.OneWay
        });
    }
}
