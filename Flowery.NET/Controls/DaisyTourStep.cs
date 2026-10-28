using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Flowery.Controls;

public enum DaisyTourPlacement { Auto, Top, Bottom, Left, Right }

/// <summary>Where the step card appears: beside the target, or at a fixed window position.</summary>
public enum DaisyTourCardPosition { Default, Target, TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }
/// <summary>Why a session ended. <c>TargetUnavailable</c>: the tour could not open because it belongs to no window.
/// <c>Failed</c>: the tour itself could not continue, such as when its dispatcher no longer accepts work; the error
/// tells why. A target that cannot be shown during a session keeps the step open and raises
/// <see cref="DaisyTour.StepTargetUnavailable"/> instead.</summary>
public enum DaisyTourCloseReason { Completed, Skipped, Cancelled, TargetUnavailable, Failed }

/// <summary>What keeps a step from showing everything it describes.</summary>
public enum DaisyTourTargetProblem
{
    /// <summary>The step's <see cref="DaisyTourStep.PrepareAsync"/> failed. The warning lasts for the visit of the step.</summary>
    Preparation,
    /// <summary>The step target is missing or cannot be shown.</summary>
    Target,
    /// <summary>An end of one of the step's annotations cannot be measured.</summary>
    Annotation
}

/// <summary>The result of one tour session. A failed session carries its error.</summary>
public sealed class DaisyTourClosedEventArgs : EventArgs
{
    internal DaisyTourClosedEventArgs(DaisyTourCloseReason reason, string? stepId,
        Control? target = null, Exception? error = null)
    {
        Reason = reason;
        StepId = stepId;
        Target = target;
        Error = error;
    }

    public DaisyTourCloseReason Reason { get; }
    public string? StepId { get; }
    /// <summary>Always null: target problems no longer close the tour. <see cref="DaisyTour.StepTargetUnavailable"/>
    /// reports the affected annotation.</summary>
    public string? AnnotationId => null;
    public Control? Target { get; }
    public Exception? Error { get; }
}

/// <summary>A step that cannot show everything it describes. The step stays open in its warning state.</summary>
public sealed class DaisyTourStepTargetUnavailableEventArgs : EventArgs
{
    internal DaisyTourStepTargetUnavailableEventArgs(string stepId, DaisyTourTargetProblem problem, string? annotationId,
        Control? target, Exception error)
    {
        StepId = stepId;
        Problem = problem;
        AnnotationId = annotationId;
        Target = target;
        Error = error;
    }

    public string StepId { get; }
    public DaisyTourTargetProblem Problem { get; }
    /// <summary>The annotation whose end is unavailable, for <see cref="DaisyTourTargetProblem.Annotation"/>.</summary>
    public string? AnnotationId { get; }
    /// <summary>The element that cannot be shown: the step target, or the annotation end.</summary>
    public Control? Target { get; }
    public Exception Error { get; }
}

/// <summary>A nonvisual, bindable definition. Preparation runs again on every visit.</summary>
public sealed class DaisyTourStep : AvaloniaObject
{
    public DaisyTourStep()
    {
        Annotations.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public ObservableCollection<DaisyTourAnnotation> Annotations { get; } = [];
    public Func<DaisyTourStep, CancellationToken, Task>? PrepareAsync { get; set; }
    internal event EventHandler? Changed;

    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<DaisyTourStep, string>(nameof(Id), "");
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }
    public static readonly StyledProperty<Control?> TargetProperty =
        AvaloniaProperty.Register<DaisyTourStep, Control?>(nameof(Target), null);
    public Control? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DaisyTourStep, string>(nameof(Title), "");
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<DaisyTourStep, string>(nameof(Description), "");
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public static readonly StyledProperty<DaisyTourPlacement> PlacementProperty =
        AvaloniaProperty.Register<DaisyTourStep, DaisyTourPlacement>(nameof(Placement), DaisyTourPlacement.Auto);
    public DaisyTourPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    /// <summary>Overrides the tour's card position for this step; Default inherits it.</summary>
    public static readonly StyledProperty<DaisyTourCardPosition> CardPositionProperty =
        AvaloniaProperty.Register<DaisyTourStep, DaisyTourCardPosition>(nameof(CardPosition), DaisyTourCardPosition.Default);
    public DaisyTourCardPosition CardPosition { get => GetValue(CardPositionProperty); set => SetValue(CardPositionProperty, value); }
    /// <summary>Horizontal card position within its available travel area, from 0 to 1. NaN uses automatic placement.</summary>
    public static readonly StyledProperty<double> CardXProperty =
        AvaloniaProperty.Register<DaisyTourStep, double>(nameof(CardX), double.NaN);
    public double CardX { get => GetValue(CardXProperty); set => SetValue(CardXProperty, value); }
    /// <summary>Vertical card position within its available travel area, from 0 to 1. NaN uses automatic placement.</summary>
    public static readonly StyledProperty<double> CardYProperty =
        AvaloniaProperty.Register<DaisyTourStep, double>(nameof(CardY), double.NaN);
    public double CardY { get => GetValue(CardYProperty); set => SetValue(CardYProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<DaisyTourStep, IDataTemplate?>(nameof(ContentTemplate), null);
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
    public static readonly StyledProperty<DaisyTourAnnotationStyle?> AnnotationDefaultsProperty =
        AvaloniaProperty.Register<DaisyTourStep, DaisyTourAnnotationStyle?>(nameof(AnnotationDefaults), null);
    public DaisyTourAnnotationStyle? AnnotationDefaults { get => GetValue(AnnotationDefaultsProperty); set => SetValue(AnnotationDefaultsProperty, value); }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == typeof(DaisyTourStep)) Changed?.Invoke(this, EventArgs.Empty);
    }
}
