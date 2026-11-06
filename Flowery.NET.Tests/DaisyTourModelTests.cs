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

    // XAML sets attributes before it adds the steps, so the profile may come before the ID and the steps.
    [AvaloniaFact]
    public async Task When_AStyleProfileIsSetBeforeTheIdAndTheSteps_ItIsValidatedAtTheStart()
    {
        var tour = new DaisyTour { StyleProfile = ProfileWithStep("tour", "step") };
        tour.Id = "tour";
        tour.Steps.Add(new DaisyTourStep { Id = "step" });
        DaisyTourCloseReason? reason = null;
        tour.Closed += (_, args) => reason = args.Reason;
        // Without a window the tour closes at once, after the profile passed.
        await tour.StartAsync();
        Assert.Equal(DaisyTourCloseReason.TargetUnavailable, reason);
        Assert.Equal("step", Assert.Single(tour.StyleProfile!.Steps).Id);
    }

    [AvaloniaFact]
    public void When_TheIdIsSet_AProfileOfAnotherTourIsRejectedWhenItIsSet()
    {
        var tour = new DaisyTour { Id = "tour" };
        Assert.Throws<ArgumentException>(() => tour.StyleProfile = ProfileWithStep("other", "step"));
        Assert.Null(tour.StyleProfile);
    }

    [AvaloniaFact]
    public async Task When_AProfileNamesAnUnknownStep_TheStartRejectsIt()
    {
        var tour = new DaisyTour { Id = "tour" };
        tour.Steps.Add(new DaisyTourStep { Id = "step" });
        tour.StyleProfile = ProfileWithStep("tour", "missing");
        var closed = false;
        tour.Closed += (_, _) => closed = true;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => tour.StartAsync());
        Assert.Contains("unknown step ID", error.Message, StringComparison.Ordinal);
        Assert.False(tour.IsOpen);
        Assert.False(closed, "A rejected profile does not open the tour.");
    }

    [AvaloniaFact]
    public void When_AProfileIsLargerThanTheLimit_ItIsRejectedWhenItIsSet()
    {
        // Regression: the tour took a profile whose JSON exceeds 1 MiB, which the editor then could not open.
        var tour = new DaisyTour();
        var error = Assert.Throws<ArgumentException>(() => tour.StyleProfile = DaisyTourStyleProfileJsonTests.TooLargeProfile());
        Assert.Contains("1 MiB", error.Message, StringComparison.Ordinal);
        Assert.Null(tour.StyleProfile);
    }

    private static DaisyTourStyleProfile ProfileWithStep(string tourId, string stepId)
    {
        var profile = new DaisyTourStyleProfile { TourId = tourId };
        profile.Steps.Add(new DaisyTourStepStyleProfile { Id = stepId });
        return profile;
    }
}
