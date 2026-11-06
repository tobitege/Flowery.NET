using System.Text.Json;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>Round trips, overrides, inheritance and validation of the tour style profile.</summary>
public class DaisyTourStyleProfileTests
{
    [AvaloniaFact]
    public void When_ProfileRoundTripsOrIsCloned_SparseOverridesInheritanceAndCollectionsRemainDistinct()
    {
        var profile = new DaisyTourStyleProfile { TourId = "tour" };
        profile.AnnotationDefaults = new DaisyTourStyleOverride();
        DaisyTourProfileResolver.SetValue(profile.AnnotationDefaults, "strokeThickness", 4d);
        var step = new DaisyTourStepStyleProfile { Id = "step" };
        var annotation = new DaisyTourAnnotationStyleProfile { Id = "arrow", Style = new DaisyTourStyleOverride() };
        DaisyTourProfileResolver.SetValue(annotation.Style, "path", DaisyTourAnnotationPath.Bezier);
        DaisyTourProfileResolver.SetValue(annotation.Style, "stroke", "#FF12AB34");
        DaisyTourProfileResolver.SetValue(annotation.Style, "dashArray", new[] { 1d, 3d });
        DaisyTourProfileResolver.InheritValue(annotation.Style, "headWidth");
        step.Annotations.Add(annotation);
        profile.Steps.Add(step);

        var json = DaisyTourStyleProfileSerializer.Serialize(profile);
        var copy = DaisyTourStyleProfileSerializer.Deserialize(json);
        var copiedStyle = copy.Steps[0].Annotations[0].Style!;
        Assert.Equal(DaisyTourAnnotationPath.Bezier, copiedStyle.Values.Path);
        Assert.Equal("#FF12AB34", copiedStyle.Values.Stroke);
        Assert.Equal(4d, copy.AnnotationDefaults!.Values.StrokeThickness);
        Assert.Equal(new[] { 1d, 3d }, copiedStyle.Values.DashArray!);
        Assert.Equal(new[] { "headWidth" }, copiedStyle.Inherit);
        Assert.Null(copiedStyle.Values.HeadWidth);
        Assert.Null(copiedStyle.Values.Fill);
        Assert.Contains("\"path\": \"Bezier\"", json);
        Assert.DoesNotContain("\"fill\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN", json, StringComparison.Ordinal);
        Assert.Equal(json, DaisyTourStyleProfileSerializer.Serialize(copy));

        var clone = DaisyTourStyleProfileSerializer.Clone(profile);
        clone.Steps[0].Annotations[0].Style!.Values.DashArray![0] = 7;
        clone.AnnotationDefaults!.Inherit.Add("stroke");
        Assert.Equal(1d, annotation.Style.Values.DashArray![0]);
        Assert.Empty(profile.AnnotationDefaults.Inherit);
    }

    [AvaloniaFact]
    public void When_OverrideIsSetInheritedClearedOrRejected_OnlyValidChangesAreKept()
    {
        var style = new DaisyTourStyleOverride();
        Assert.False(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out _, out _));
        DaisyTourProfileResolver.SetValue(style, "strokeThickness", 4d);
        Assert.True(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out var value, out var inherit));
        Assert.Equal<object?>(4d, value);
        Assert.False(inherit);
        DaisyTourProfileResolver.InheritValue(style, "strokeThickness");
        Assert.True(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out value, out inherit));
        Assert.True(inherit);
        Assert.Null(value);
        DaisyTourProfileResolver.ClearValue(style, "strokeThickness");
        Assert.False(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out _, out _));

        // A rejected value keeps the previous override or inheritance.
        DaisyTourProfileResolver.SetValue(style, "opacity", 0.5d);
        Assert.Throws<ArgumentException>(() => DaisyTourProfileResolver.SetValue(style, "opacity", 2d));
        Assert.Equal(0.5d, style.Values.Opacity);
        DaisyTourProfileResolver.InheritValue(style, "opacity");
        Assert.Throws<ArgumentException>(() => DaisyTourProfileResolver.SetValue(style, "opacity", double.NaN));
        Assert.Equal(new[] { "opacity" }, style.Inherit);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"tourId\":\"tour\"}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"unknown\":1}}}")]
    public void When_JsonIsInvalid_WholeProfileIsRejected(string json) =>
        Assert.Throws<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));

    [AvaloniaFact]
    public void When_ProfileTargetsAreValidated_UnknownOrDuplicatedIdsFailAndTheDefaultPointerIsAccepted()
    {
        var tour = new DaisyTour { Id = "tour" };
        var step = new DaisyTourStep { Id = "step" };
        step.Annotations.Add(new DaisyTourAnnotation { Id = "arrow" });
        tour.Steps.Add(step);
        var profile = new DaisyTourStyleProfile { TourId = "tour" };
        var stepProfile = new DaisyTourStepStyleProfile { Id = "step" };
        stepProfile.Annotations.Add(new DaisyTourAnnotationStyleProfile { Id = "arrow" });
        profile.Steps.Add(stepProfile);
        DaisyTourStyleProfileSerializer.Validate(profile, tour);

        stepProfile.Annotations[0].Id = "missing";
        Assert.Throws<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile, tour));
        stepProfile.Annotations[0].Id = "arrow";
        step.Annotations.Add(new DaisyTourAnnotation { Id = "arrow" });
        Assert.Throws<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile, tour));
        step.Annotations.RemoveAt(1);
        profile.TourId = "other";
        Assert.Throws<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile, tour));

        // A step without annotations shows the default pointer, which a profile addresses by its synthetic ID.
        var pointerTour = new DaisyTour { Id = "tour" };
        pointerTour.Steps.Add(new DaisyTourStep { Id = "step" });
        profile.TourId = "tour";
        stepProfile.Annotations[0].Id = "default-pointer";
        DaisyTourStyleProfileSerializer.Validate(profile, pointerTour);
    }
}
