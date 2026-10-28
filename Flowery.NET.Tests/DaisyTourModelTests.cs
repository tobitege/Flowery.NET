using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyTourModelTests
{
    // The tour observes Changed to redraw an open step; Avalonia's OnPropertyChanged replaces WinUI's callbacks.
    [AvaloniaFact]
    public void When_AStylePropertyChanges_ChangedIsRaised()
    {
        var style = new DaisyTourAnnotationStyle();
        var changes = 0;
        style.Changed += (_, _) => changes++;

        style.StrokeThickness = 3;
        style.Kind = DaisyTourAnnotationKind.Line;

        Assert.Equal(2, changes);
    }

    [AvaloniaFact]
    public void When_AnAnnotationPropertyChanges_ChangedIsRaised()
    {
        var annotation = new DaisyTourAnnotation();
        var changes = 0;
        annotation.Changed += (_, _) => changes++;

        annotation.Text = "Explanation";
        annotation.SourceAnchor = DaisyTourAnchor.Left;
        annotation.Source = new Border();
        annotation.RouteLimitSide = DaisyTourRouteSide.Above;
        Assert.Equal(4, changes);

        // A style property of the annotation is reported once, not by both property filters.
        annotation.HeadWidth = 12;
        Assert.Equal(5, changes);
    }

    [AvaloniaFact]
    public void When_TheDashPatternChangesOrIsReplaced_OnlyTheCurrentPatternIsObserved()
    {
        var style = new DaisyTourAnnotationStyle();
        var first = new DaisyTourDashCollection { 4, 2 };
        var second = new DaisyTourDashCollection { 1, 1 };
        var changes = 0;
        style.Changed += (_, _) => changes++;

        style.DashArray = first;
        Assert.Equal(1, changes);
        first.Add(3);
        Assert.Equal(2, changes);

        style.DashArray = second;
        Assert.Equal(3, changes);
        first.Add(5);
        Assert.Equal(3, changes);
        second[0] = 2;
        Assert.Equal(4, changes);

        style.DashArray = null;
        second.Add(6);
        Assert.Equal(5, changes);
    }

    [AvaloniaFact]
    public void When_AStepPropertyOrItsAnnotationsChange_ChangedIsRaised()
    {
        var step = new DaisyTourStep();
        var changes = 0;
        step.Changed += (_, _) => changes++;

        step.Title = "Create";
        step.Placement = DaisyTourPlacement.Bottom;
        step.CardX = 0.5;
        step.Target = new Border();
        Assert.Equal(4, changes);

        step.Annotations.Add(new DaisyTourAnnotation());
        Assert.Equal(5, changes);
    }
}
