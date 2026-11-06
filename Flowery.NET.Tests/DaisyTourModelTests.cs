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
    public void When_AProfileIsOfAnotherTourOrLargerThanTheLimit_ItIsRejectedWhenItIsSet()
    {
        var tour = new DaisyTour { Id = "tour" };
        Assert.Throws<ArgumentException>(() => tour.StyleProfile = ProfileWithStep("other", "step"));
        Assert.Null(tour.StyleProfile);

        // Regression: the tour took a profile whose JSON exceeds 1 MiB, which the editor then could not open.
        var unnamed = new DaisyTour();
        var error = Assert.Throws<ArgumentException>(() => unnamed.StyleProfile = DaisyTourStyleProfileJsonTests.TooLargeProfile());
        Assert.Contains("1 MiB", error.Message, StringComparison.Ordinal);
        Assert.Null(unnamed.StyleProfile);
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

    private static DaisyTourStyleProfile ProfileWithStep(string tourId, string stepId)
    {
        var profile = new DaisyTourStyleProfile { TourId = tourId };
        profile.Steps.Add(new DaisyTourStepStyleProfile { Id = stepId });
        return profile;
    }
}
