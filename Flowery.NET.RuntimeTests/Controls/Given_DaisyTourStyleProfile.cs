using System.Text.Json;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;
using CollectionAssert = Flowery.NET.RuntimeTests.Controls.MSTestCollectionAssert;
using StringAssert = Flowery.NET.RuntimeTests.Controls.MSTestStringAssert;

namespace Flowery.NET.RuntimeTests.Controls;

public class Given_DaisyTourStyleProfile
{
    [Fact]
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
        Assert.AreEqual(DaisyTourAnnotationPath.Bezier, copiedStyle.Values.Path);
        Assert.AreEqual("#FF12AB34", copiedStyle.Values.Stroke);
        Assert.AreEqual(4d, copy.AnnotationDefaults!.Values.StrokeThickness);
        CollectionAssert.AreEqual(new[] { 1d, 3d }, copiedStyle.Values.DashArray!);
        CollectionAssert.AreEqual(new[] { "headWidth" }, copiedStyle.Inherit);
        Assert.IsNull(copiedStyle.Values.HeadWidth);
        Assert.IsNull(copiedStyle.Values.Fill);
        StringAssert.Contains(json, "\"path\": \"Bezier\"");
        Assert.IsFalse(json.Contains("\"fill\"", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("NaN", StringComparison.Ordinal));
        Assert.AreEqual(json, DaisyTourStyleProfileSerializer.Serialize(copy));

        var clone = DaisyTourStyleProfileSerializer.Clone(profile);
        clone.Steps[0].Annotations[0].Style!.Values.DashArray![0] = 7;
        clone.AnnotationDefaults!.Inherit.Add("stroke");
        Assert.AreEqual(1d, annotation.Style.Values.DashArray![0]);
        Assert.HasCount(0, profile.AnnotationDefaults.Inherit);
    }

    [Fact]
    public void When_ProfileResolves_ColorsAndDashPatternsUseNativeTypes()
    {
        var style = new DaisyTourStyleOverride();
        DaisyTourProfileResolver.SetValue(style, "stroke", "#7F12AB34");
        DaisyTourProfileResolver.SetValue(style, "dashArray", new[] { 2d, 5d });
        Assert.IsTrue(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeProperty, out var value, out var inherit));
        Assert.IsFalse(inherit);
        Assert.IsInstanceOfType<SolidColorBrush>(value);
        var brush = (SolidColorBrush)value!;
        Assert.AreEqual((byte)0x7F, brush.Color.A);
        Assert.AreEqual((byte)0x12, brush.Color.R);
        Assert.AreEqual((byte)0xAB, brush.Color.G);
        Assert.AreEqual((byte)0x34, brush.Color.B);
        Assert.IsTrue(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.DashArrayProperty, out value, out inherit));
        CollectionAssert.AreEqual(new[] { 2d, 5d }, ((DaisyTourDashCollection)value!).ToArray());
    }

    [Fact]
    public void When_OverrideIsSetInheritedClearedOrRejected_OnlyValidChangesAreKept()
    {
        var style = new DaisyTourStyleOverride();
        Assert.IsFalse(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out _, out _));
        DaisyTourProfileResolver.SetValue(style, "strokeThickness", 4d);
        Assert.IsTrue(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out var value, out var inherit));
        Assert.AreEqual<object?>(4d, value);
        Assert.IsFalse(inherit);
        DaisyTourProfileResolver.InheritValue(style, "strokeThickness");
        Assert.IsTrue(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out value, out inherit));
        Assert.IsTrue(inherit);
        Assert.IsNull(value);
        DaisyTourProfileResolver.ClearValue(style, "strokeThickness");
        Assert.IsFalse(DaisyTourProfileResolver.TryGetValue(style, DaisyTourAnnotationStyle.StrokeThicknessProperty, out _, out _));

        // A rejected value keeps the previous override or inheritance.
        DaisyTourProfileResolver.SetValue(style, "opacity", 0.5d);
        Assert.ThrowsExactly<ArgumentException>(() => DaisyTourProfileResolver.SetValue(style, "opacity", 2d));
        Assert.AreEqual(0.5d, style.Values.Opacity);
        DaisyTourProfileResolver.InheritValue(style, "opacity");
        Assert.ThrowsExactly<ArgumentException>(() => DaisyTourProfileResolver.SetValue(style, "opacity", double.NaN));
        CollectionAssert.AreEqual(new[] { "opacity" }, style.Inherit);
    }

    [Fact]
    public void When_JsonIsInvalid_WholeProfileIsRejected()
    {
        string[] cases =
        [
            "{\"schemaVersion\":2,\"tourId\":\"tour\"}",
            "{\"tourId\":\"tour\"}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"extra\":true}",
            "{\"schemaVersion\":1,\"schemaVersion\":1,\"tourId\":\"tour\"}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":null}",
            "{\"schemaVersion\":1,\"tourId\":\"bad id\"}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":[{\"id\":\"step\"},{\"id\":\"step\"}]}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"unknown\":1}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"opacity\":2}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"width\":0}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"stroke\":\"red\"}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"dashArray\":[0,1]}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"path\":1}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"path\":\"curved\"}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"path\":\"Default\"}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"strokeThickness\":3},\"inherit\":[\"strokeThickness\"]}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"inherit\":[\"stroke\",\"stroke\"]}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"highlightPadding\":3}}}",
            "{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"sourceAnchor\":\"Top\"}}}",
        ];
        foreach (var json in cases)
            Assert.ThrowsExactly<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json), json);
    }

    [Fact]
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
        Assert.ThrowsExactly<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile, tour));
        stepProfile.Annotations[0].Id = "arrow";
        step.Annotations.Add(new DaisyTourAnnotation { Id = "arrow" });
        Assert.ThrowsExactly<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile, tour));
        step.Annotations.RemoveAt(1);
        profile.TourId = "other";
        Assert.ThrowsExactly<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile, tour));

        // A step without annotations shows the default pointer, which a profile addresses by its synthetic ID.
        var pointerTour = new DaisyTour { Id = "tour" };
        pointerTour.Steps.Add(new DaisyTourStep { Id = "step" });
        profile.TourId = "tour";
        stepProfile.Annotations[0].Id = "default-pointer";
        DaisyTourStyleProfileSerializer.Validate(profile, pointerTour);
    }
}
