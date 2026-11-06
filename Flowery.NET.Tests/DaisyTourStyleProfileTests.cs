using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
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
    public void When_ProfileResolves_ColorsAndDashPatternsUseNativeTypes()
    {
        var style = new DaisyTourStyleOverride();
        DaisyTourProfileResolver.SetValue(style, "stroke", "#7F12AB34");
        DaisyTourProfileResolver.SetValue(style, "dashArray", new[] { 2d, 5d });
        Assert.True(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeProperty, out var value, out var inherit));
        Assert.False(inherit);
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(value);
        Assert.Equal((byte)0x7F, brush.Color.A);
        Assert.Equal((byte)0x12, brush.Color.R);
        Assert.Equal((byte)0xAB, brush.Color.G);
        Assert.Equal((byte)0x34, brush.Color.B);
        // An unchanged color keeps its brush, so the shapes that use it do not change on every geometry update.
        Assert.True(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeProperty, out var again, out _));
        Assert.Same(brush, again);
        DaisyTourProfileResolver.SetValue(style, "stroke", "#FF000000");
        Assert.True(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeProperty, out var changed, out _));
        Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(changed).Color);
        Assert.True(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.DashArrayProperty, out value, out _));
        Assert.Equal(new[] { 2d, 5d }, ((DaisyTourDashCollection)value!).ToArray());
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
    [InlineData("{\"tourId\":\"tour\"}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"tourId\":\"tour\"}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":null}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"bad id\"}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":[{\"id\":\"step\"},{\"id\":\"step\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"unknown\":1}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"opacity\":2}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"width\":0}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"stroke\":\"red\"}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"dashArray\":[0,1]}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"path\":1}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"path\":\"curved\"}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"path\":\"Default\"}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"strokeThickness\":3},\"inherit\":[\"strokeThickness\"]}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"inherit\":[\"stroke\",\"stroke\"]}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"highlightPadding\":3}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"sourceAnchor\":\"Top\"}}}")]
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
