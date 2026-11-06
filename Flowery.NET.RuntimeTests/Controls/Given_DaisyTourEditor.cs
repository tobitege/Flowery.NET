using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Theming;
using Xunit;
using static Flowery.NET.RuntimeTests.Controls.DaisyTourTestHelpers;
using static Flowery.NET.RuntimeTests.Controls.TourEditorTestHelpers;
using Assert = Flowery.NET.RuntimeTests.Controls.MSTestAssert;
using StringAssert = Flowery.NET.RuntimeTests.Controls.MSTestStringAssert;

namespace Flowery.NET.RuntimeTests.Controls;

public class Given_DaisyTourEditor
{
    [Fact]
    public Task When_EditingThenApplying_LiveGeometryAndReplayUseProfileWithoutChangingApplicationValues() =>
        InScenarioAsync(async scenario =>
        {
            var normalClosures = 0;
            var editorClosures = 0;
            scenario.Tour.Closed += (_, _) => normalClosures++;
            scenario.Editor.Closed += (_, _) => editorClosures++;
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            Assert.IsTrue(scenario.Editor.IsOpen);
            Assert.IsFalse(scenario.Editor.HasChanges);
            await ChangeGeometryAsync(scenario.Tour, () => scenario.Editor.SetOverride("bodyWidth", 18d));
            Assert.AreEqual(18d, scenario.Tour.AnnotationPresenters.Single().EditGeometry!.BodyWidth, 0.001);
            Assert.AreEqual(5d, scenario.Annotation.BodyWidth);
            Assert.IsTrue(scenario.Editor.HasChanges);
            await ChangeGeometryAsync(scenario.Tour, scenario.Editor.Undo);
            Assert.AreEqual(5d, scenario.Tour.AnnotationPresenters.Single().EditGeometry!.BodyWidth, 0.001);
            Assert.IsFalse(scenario.Editor.HasChanges);
            await ChangeGeometryAsync(scenario.Tour, scenario.Editor.Redo);
            Assert.AreEqual(18d, scenario.Tour.AnnotationPresenters.Single().EditGeometry!.BodyWidth, 0.001);
            scenario.Editor.Apply();
            Assert.IsFalse(scenario.Editor.IsOpen);
            Assert.IsFalse(scenario.Tour.IsOpen);
            Assert.AreEqual(1, editorClosures);
            Assert.AreEqual(0, normalClosures);
            Assert.AreEqual(18d, scenario.Tour.StyleProfile!.Steps[0].Annotations[0].Style!.Values.BodyWidth);
            await scenario.Tour.StartAsync().WaitAsync(TestTimeout);
            Assert.AreEqual(18d, scenario.Tour.AnnotationPresenters.Single().EditGeometry!.BodyWidth, 0.001);
            await scenario.Tour.NextAsync().WaitAsync(TestTimeout);
            Assert.AreEqual(1, normalClosures);
        });

    [Fact]
    public Task When_Importing_InvalidInputLeavesDraftAndHistoryIntactAndValidImportIsOneUndoAction() =>
        InScenarioAsync(async scenario =>
        {
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            scenario.Editor.SetOverride("bodyWidth", 11d);
            var before = scenario.Editor.ExportJson();
            Assert.Throws<JsonException>(() => scenario.Editor.ImportJson("{"));
            Assert.AreEqual(before, scenario.Editor.ExportJson());
            var wrongTour = scenario.Editor.ExportProfile();
            wrongTour.TourId = "other";
            Assert.ThrowsExactly<ArgumentException>(() => scenario.Editor.ImportProfile(wrongTour));
            Assert.AreEqual(before, scenario.Editor.ExportJson());
            // JSON of another tour is invalid JSON input like a syntax error.
            var wrongTourJson = DaisyTourStyleProfileSerializer.Serialize(wrongTour);
            var error = Assert.ThrowsExactly<JsonException>(() => scenario.Editor.ImportJson(wrongTourJson));
            StringAssert.Contains(error.Message, "different tour");
            Assert.AreEqual(before, scenario.Editor.ExportJson());
            Assert.ThrowsExactly<ArgumentException>(() => scenario.Editor.SetOverride("opacity", 2d));
            Assert.ThrowsExactly<ArgumentException>(() => scenario.Editor.SetOverride("missing", null));
            Assert.ThrowsExactly<ArgumentException>(() => scenario.Editor.InheritValue("missing"));
            Assert.AreEqual(before, scenario.Editor.ExportJson());

            var imported = scenario.Editor.ExportProfile();
            imported.Steps[0].Annotations[0].Style!.Values.BodyWidth = 19;
            imported.Steps[0].Annotations[0].Style!.Values.Curvature = 0.7;
            scenario.Editor.ImportProfile(imported);
            var after = scenario.Editor.ExportJson();
            imported.Steps[0].Annotations[0].Style!.Values.BodyWidth = 27;
            Assert.AreEqual(after, scenario.Editor.ExportJson());
            scenario.Editor.Undo();
            Assert.AreEqual(before, scenario.Editor.ExportJson());
            scenario.Editor.Redo();
            Assert.AreEqual(after, scenario.Editor.ExportJson());
            scenario.Editor.Undo();
            scenario.Editor.Undo();
            Assert.IsFalse(scenario.Editor.HasChanges);
        });

    [Fact]
    public Task When_Discarding_ActiveProfileBindingAndCustomBrushArePreserved() =>
        InScenarioAsync(async scenario =>
        {
            var source = new Slider { Value = 7, Minimum = 0, Maximum = 100 };
            scenario.Tour.Bind(DaisyTour.HighlightPaddingProperty, new ReflectionBinding
            {
                Source = source,
                Path = nameof(Slider.Value),
                Mode = BindingMode.OneWay
            });
            var binding = BindingOperations.GetBindingExpressionBase(scenario.Tour, DaisyTour.HighlightPaddingProperty);
            var brush = new LinearGradientBrush();
            brush.GradientStops.Add(new GradientStop { Color = Colors.Red, Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = Colors.Blue, Offset = 1 });
            scenario.Annotation.Fill = brush;
            var active = new DaisyTourStyleProfile { TourId = scenario.Tour.Id, Style = new DaisyTourStyleOverride() };
            active.Style.Values.HighlightCornerRadius = 13;
            scenario.Tour.StyleProfile = active;
            var before = DaisyTourStyleProfileSerializer.Serialize(scenario.Tour.StyleProfile!);
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            var draft = scenario.Editor.ExportProfile();
            draft.Style!.Values.HighlightPadding = 22;
            scenario.Editor.ImportProfile(draft);
            source.Value = 9;
            await SettleAsync(scenario.Root);
            Assert.AreEqual(9d, scenario.Tour.HighlightPadding);
            scenario.Editor.Discard();
            Assert.AreSame(binding, BindingOperations.GetBindingExpressionBase(scenario.Tour, DaisyTour.HighlightPaddingProperty));
            Assert.AreSame(brush, scenario.Annotation.Fill);
            Assert.AreEqual(before, DaisyTourStyleProfileSerializer.Serialize(scenario.Tour.StyleProfile!));
            source.Value = 10;
            await SettleAsync(scenario.Root);
            Assert.AreEqual(10d, scenario.Tour.HighlightPadding);
        });

    [Fact]
    public Task When_GestureChangesSeveralValues_CommitIsOneUndoAndCancellationRestoresExactDraft() =>
        InScenarioAsync(async scenario =>
        {
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            var before = scenario.Editor.ExportJson();
            scenario.Editor.BeginGesture();
            scenario.Editor.SetAnnotationValue("bodyWidth", 13d);
            scenario.Editor.SetAnnotationValue("bodyWidth", 17d);
            scenario.Editor.SetAnnotationValue("headWidth", 29d);
            scenario.Editor.EndGesture();
            var after = scenario.Editor.ExportJson();
            scenario.Editor.Undo();
            Assert.AreEqual(before, scenario.Editor.ExportJson());
            scenario.Editor.Redo();
            Assert.AreEqual(after, scenario.Editor.ExportJson());
            scenario.Editor.BeginGesture();
            scenario.Editor.SetAnnotationValue("bodyWidth", 21d);
            scenario.Editor.CancelGesture();
            Assert.AreEqual(after, scenario.Editor.ExportJson());
            scenario.Editor.Undo();
            Assert.AreEqual(before, scenario.Editor.ExportJson());
        });

    [Fact]
    public Task When_CancelledDuringPreparation_EditorClosesOnceAndLateCompletionCannotReopenPreview() =>
        InScenarioAsync(async scenario =>
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scenario.Step.PrepareAsync = async (_, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            };
            var closures = 0;
            var normalClosures = 0;
            scenario.Editor.Closed += (_, _) => closures++;
            scenario.Tour.Closed += (_, _) => normalClosures++;
            using var cancellation = new CancellationTokenSource();
            var opening = scenario.Editor.OpenAsync(scenario.Tour, cancellation.Token);
            await entered.Task.WaitAsync(TestTimeout);
            cancellation.Cancel();
            try { await opening.WaitAsync(TestTimeout); }
            catch (OperationCanceledException) { }
            release.TrySetResult();
            await SettleAsync(scenario.Root);
            Assert.IsFalse(scenario.Editor.IsOpen);
            Assert.IsFalse(scenario.Tour.IsOpen);
            Assert.AreEqual(1, closures);
            Assert.AreEqual(0, normalClosures);
            Assert.IsNull(scenario.Editor.GetVisualParent());
            scenario.Step.PrepareAsync = null;
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            Assert.IsTrue(scenario.Editor.IsOpen);
        });

    [Fact]
    public Task When_DiscardedOpeningCompletesAfterReopening_ItCannotChangeTheNewSession() =>
        InScenarioAsync(async scenario =>
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scenario.Step.PrepareAsync = async (_, _) =>
            {
                entered.TrySetResult();
                try { await release.Task; }
                finally { callbackFinished.TrySetResult(); }
            };
            var closures = 0;
            var normalClosures = 0;
            scenario.Editor.Closed += (_, _) => closures++;
            scenario.Tour.Closed += (_, _) => normalClosures++;
            var oldOpening = scenario.Editor.OpenAsync(scenario.Tour);
            try
            {
                await entered.Task.WaitAsync(TestTimeout);
                scenario.Editor.Discard();
                scenario.Step.PrepareAsync = null;
                // Do not yield between discard and reopen: the old cancelled await is still queued.
                var newOpening = scenario.Editor.OpenAsync(scenario.Tour);
                await newOpening.WaitAsync(TestTimeout);
                Assert.IsTrue(scenario.Editor.IsOpen);
                Assert.IsTrue(scenario.Tour.IsOpen);
                await ChangeGeometryAsync(scenario.Tour, () => scenario.Editor.SetOverride("bodyWidth", 18d));
                var draft = scenario.Editor.ExportJson();
                var currentStep = scenario.Tour.CurrentStep;
                release.TrySetResult();
                await callbackFinished.Task.WaitAsync(TestTimeout);
                try { await oldOpening.WaitAsync(TestTimeout); }
                catch (OperationCanceledException) { }
                Assert.IsTrue(scenario.Editor.IsOpen);
                Assert.IsTrue(scenario.Tour.IsOpen);
                Assert.AreSame(currentStep, scenario.Tour.CurrentStep);
                Assert.AreEqual(draft, scenario.Editor.ExportJson());
                Assert.AreEqual(18d, scenario.Tour.AnnotationPresenters.Single().EditGeometry!.BodyWidth, 0.001);
                Assert.AreEqual(1, closures);
                Assert.AreEqual(0, normalClosures);
                Assert.IsNull(scenario.Editor.LastError);
                Assert.AreSame(scenario.Tour.EditingSurface, scenario.Editor.GetVisualParent());
            }
            finally
            {
                release.TrySetResult();
                await callbackFinished.Task.WaitAsync(TestTimeout);
                try { await oldOpening.WaitAsync(TestTimeout); }
                catch (OperationCanceledException) { }
            }
        });

    [Fact]
    public Task When_OpenValidationFails_EditorHasNoDraftAndCanOpenAValidTour() =>
        InScenarioAsync(async scenario =>
        {
            var closures = 0;
            scenario.Editor.Closed += (_, _) => closures++;
            scenario.Tour.Id = "invalid tour id";
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => scenario.Editor.OpenAsync(scenario.Tour));
            Assert.IsFalse(scenario.Editor.IsOpen);
            Assert.IsFalse(scenario.Editor.HasChanges);
            Assert.IsNull(scenario.Editor.DraftProfile);
            Assert.IsNull(scenario.Editor.GetVisualParent());
            Assert.AreEqual(0, closures);
            scenario.Tour.Id = "editor-test";
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            Assert.IsTrue(scenario.Editor.IsOpen);
            Assert.IsTrue(scenario.Tour.IsOpen);
            Assert.IsFalse(scenario.Editor.HasChanges);
        });

    [Fact]
    public void When_TheAssignedProfileIsMutated_TourRetainsItsValidatedCopy()
    {
        var tour = new DaisyTour { Id = "profile-isolation" };
        var profile = new DaisyTourStyleProfile { TourId = tour.Id, Style = new DaisyTourStyleOverride() };
        profile.Style.Values.HighlightPadding = 17;
        tour.StyleProfile = profile;
        profile.Style.Values.HighlightPadding = 23;
        Assert.AreEqual(17d, tour.StyleProfile!.Style!.Values.HighlightPadding);
        // Reading the profile does not copy it again.
        Assert.AreSame(tour.StyleProfile, tour.StyleProfile);
        Assert.AreSame(tour.StyleProfile, tour.GetValue(DaisyTour.StyleProfileProperty));
    }

    [Fact]
    public Task When_AStyleProfileIsRejected_TheActiveProfileStaysInEffectEverywhere() =>
        InScenarioAsync(async scenario =>
        {
            // Regression: the profile was validated after it was stored, so the property kept a rejected profile
            // while the tour went on with the previous one.
            var active = new DaisyTourStyleProfile { TourId = scenario.Tour.Id, Style = new DaisyTourStyleOverride() };
            active.Style.Values.HighlightPadding = 13;
            scenario.Tour.StyleProfile = active;
            var stored = scenario.Tour.StyleProfile;
            await scenario.Tour.StartAsync().WaitAsync(TestTimeout);
            await SettleAsync(scenario.Root);
            var outline = scenario.Tour.Outline.Data!.Bounds;
            var otherTour = new DaisyTourStyleProfile { TourId = "other", Style = new DaisyTourStyleOverride() };
            otherTour.Style.Values.HighlightPadding = 40;
            Assert.ThrowsExactly<ArgumentException>(() => scenario.Tour.StyleProfile = otherTour);
            Assert.ThrowsExactly<ArgumentException>(() => scenario.Tour.SetValue(DaisyTour.StyleProfileProperty, otherTour));
            Assert.AreSame(stored, scenario.Tour.StyleProfile);
            Assert.AreSame(stored, scenario.Tour.GetValue(DaisyTour.StyleProfileProperty));
            await SettleAsync(scenario.Root);
            Assert.AreEqual(outline, scenario.Tour.Outline.Data!.Bounds, "The highlight keeps the padding of the active profile.");
        });

    [Fact]
    public Task When_InheritanceSkipsApplicationScopes_RendererUsesNextScopeThenItsDefault() =>
        InScenarioAsync(async scenario =>
        {
            // Block-arrow clearance also includes BodyWidth; a line isolates the resolved stroke thickness.
            scenario.Annotation.ArrowStyle = DaisyTourArrowStyle.Line;
            FlowerySizeManager.SetIgnoreGlobalSize(scenario.Tour, true);
            scenario.Tour.Size = DaisySize.Medium;
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            var renderedFallback = scenario.Tour.AnnotationPresenters.Single().CourseThickness;
            Assert.IsTrue(renderedFallback > 0);
            await ChangeGeometryAsync(scenario.Tour, () =>
            {
                scenario.Annotation.StrokeThickness = 7;
                scenario.Step.AnnotationDefaults = new DaisyTourAnnotationStyle { StrokeThickness = 9 };
                scenario.Tour.AnnotationDefaults = new DaisyTourAnnotationStyle { StrokeThickness = 11 };
            });
            Assert.AreEqual(7d, scenario.Tour.AnnotationPresenters.Single().CourseThickness);
            await ChangeGeometryAsync(scenario.Tour, () => scenario.Editor.InheritValue("strokeThickness"));
            Assert.AreEqual(9d, scenario.Tour.AnnotationPresenters.Single().CourseThickness);
            var profile = scenario.Editor.ExportProfile();
            profile.Steps[0].AnnotationDefaults = new DaisyTourStyleOverride();
            profile.Steps[0].AnnotationDefaults!.Inherit.Add("strokeThickness");
            await ChangeGeometryAsync(scenario.Tour, () => scenario.Editor.ImportProfile(profile));
            Assert.AreEqual(11d, scenario.Tour.AnnotationPresenters.Single().CourseThickness);
            profile.AnnotationDefaults = new DaisyTourStyleOverride();
            profile.AnnotationDefaults.Inherit.Add("strokeThickness");
            await ChangeGeometryAsync(scenario.Tour, () => scenario.Editor.ImportProfile(profile));
            Assert.AreEqual(renderedFallback, scenario.Tour.AnnotationPresenters.Single().CourseThickness);
            Assert.AreEqual(DaisySize.Medium, scenario.Tour.Size);
            Assert.AreEqual(7d, scenario.Annotation.StrokeThickness);
            Assert.AreEqual(9d, scenario.Step.AnnotationDefaults?.StrokeThickness);
            Assert.AreEqual(11d, scenario.Tour.AnnotationDefaults?.StrokeThickness);
        });

    [Fact]
    public Task When_CurvatureChanges_GripStaysNearRenderedMidpointAndProjectsMovementWithoutJumping() =>
        InScenarioAsync(async scenario =>
        {
            scenario.Root.Width = Math.Min(scenario.Root.Width, RuntimeTestHelpers.GetHostPanel().Bounds.Width);
            var source = scenario.Annotation.Source!;
            var target = scenario.Annotation.Target!;
            scenario.Annotation.SourceAnchor = DaisyTourAnchor.Center;
            scenario.Annotation.TargetAnchor = DaisyTourAnchor.Center;
            var right = scenario.Root.Width - target.Width - 24;
            Assert.IsTrue(right > 240, "The host must provide room for distinct narrow and wide connectors.");
            var arrangements = new[]
            {
                (Name: "horizontal narrow", Source: new Point(24, 180), Target: new Point(164, 180)),
                (Name: "horizontal wide", Source: new Point(24, 180), Target: new Point(right, 180)),
                (Name: "diagonal", Source: new Point(24, 80), Target: new Point(right, 280)),
                (Name: "reversed", Source: new Point(right, 180), Target: new Point(24, 180)),
                (Name: "vertical", Source: new Point(120, 80), Target: new Point(120, 300))
            };
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            foreach (var arrangement in arrangements)
            {
                foreach (var curvature in new[] { -0.35, 0, 0.35 })
                {
                    var context = $"{arrangement.Name}, curvature={curvature}";
                    await ChangeGeometryAsync(scenario.Tour, () =>
                    {
                        source.Margin = new Thickness(arrangement.Source.X, arrangement.Source.Y, 0, 0);
                        target.Margin = new Thickness(arrangement.Target.X, arrangement.Target.Y, 0, 0);
                        scenario.Editor.SetOverride("curvature", curvature);
                    });
                    scenario.Editor.UpdateHandles();
                    var presenter = scenario.Tour.AnnotationPresenters.Single();
                    var geometry = presenter.EditGeometry!;
                    var midpoint = presenter.Course[presenter.Course.Count / 2];
                    Assert.AreEqual(midpoint.X, geometry.BodyCenter.X, 0.001, context);
                    Assert.AreEqual(midpoint.Y, geometry.BodyCenter.Y, 0.001, context);
                    var dx = geometry.Target.X - geometry.Source.X;
                    var dy = geometry.Target.Y - geometry.Source.Y;
                    var length = Math.Sqrt(dx * dx + dy * dy);
                    var normal = new Point(-dy / length, dx / length);
                    var below = normal.Y < 0 || (normal.Y == 0 && normal.X < 0)
                        ? new Point(-normal.X, -normal.Y) : normal;
                    var handle = HandleCenter(scenario.Editor, "DaisyTourHandleCurvature");
                    var offset = new Point(handle.X - midpoint.X, handle.Y - midpoint.Y);
                    // Project the 28-DIP square and stroke onto the normal: the visible gap stays six DIP.
                    var gap = offset.X * below.X + offset.Y * below.Y
                        - 14 * (Math.Abs(below.X) + Math.Abs(below.Y)) - presenter.CourseThickness / 2;
                    Assert.AreEqual(6, gap, 0.001, context);
                    Assert.AreEqual(0, offset.X * below.Y - offset.Y * below.X, 0.001, context);
                    Assert.AreEqual(curvature, DaisyTourHandleLayer.ProjectCurvature(handle, geometry, handle), 0.001, context);

                    // Twelve DIP perpendicular movement changes the bow by twelve DIP; tangential movement has no effect.
                    var moved = new Point(handle.X + normal.X * 12 + dx / length * 7,
                        handle.Y + normal.Y * 12 + dy / length * 7);
                    var projected = DaisyTourHandleLayer.ProjectCurvature(moved, geometry, handle);
                    await ChangeGeometryAsync(scenario.Tour, () => scenario.Editor.SetOverride("curvature", projected));
                    var changed = presenter.EditGeometry!;
                    Assert.AreEqual(geometry.Source, changed.Source, context);
                    Assert.AreEqual(geometry.Target, changed.Target, context);
                    var changedMidpoint = presenter.Course[presenter.Course.Count / 2];
                    Assert.AreEqual(midpoint.X + normal.X * 12, changedMidpoint.X, 0.001, context);
                    Assert.AreEqual(midpoint.Y + normal.Y * 12, changedMidpoint.Y, 0.001, context);
                }
            }
        });

    [Fact]
    public Task When_TheCultureThemeOrContrastChanges_TheHandlesTakeTheirNamesAndColorsWithoutAnUpdate() =>
        InScenarioAsync(async scenario =>
        {
            // The handles set their names on culture changes and take their frames from the resource instead of
            // setting both on every update; high contrast changes only the resource.
            var previousCulture = FloweryLocalization.CurrentCulture.Name;
            var previousTheme = DaisyThemeManager.CurrentThemeName;
            var previousContrast = DaisyAccessibility.HighContrastOverride;
            try
            {
                await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
                var handle = scenario.Editor.HandleLayer.Children.OfType<Button>().Single(button =>
                    AutomationProperties.GetAutomationId(button) == "DaisyTourHandleSource");
                var marker = (Shape)handle.Content!;
                Assert.AreEqual("Move start point", AutomationProperties.GetName(handle));
                Assert.AreSame(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), marker.Stroke);

                FloweryLocalization.SetCulture("de");
                Assert.AreEqual("Startpunkt verschieben", AutomationProperties.GetName(handle));
                Assert.AreEqual<object?>("Startpunkt verschieben", ToolTip.GetTip(handle));
                Assert.IsTrue(DaisyThemeManager.ApplyTheme(previousTheme == "Dark" ? "Light" : "Dark"));
                Assert.AreSame(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), marker.Stroke);

                var themed = marker.Stroke;
                DaisyAccessibility.HighContrastOverride = true;
                await RuntimeTestHelpers.WaitUntilAsync(() => !ReferenceEquals(themed, marker.Stroke), "the frame takes the contrast color");
                Assert.AreSame(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), marker.Stroke);
            }
            finally
            {
                DaisyAccessibility.HighContrastOverride = previousContrast;
                FloweryLocalization.SetCulture(previousCulture);
                if (previousTheme is not null) DaisyThemeManager.ApplyTheme(previousTheme);
            }
        });

    [Fact]
    public Task When_AFieldWithoutAColorGetsTheRedThePickerShows_TheFieldTakesRed() =>
        InScenarioAsync(async scenario =>
        {
            // Regression: a field without a color shows the red of the picker, and choosing that red was taken for
            // no change.
            var gradient = new LinearGradientBrush();
            gradient.GradientStops.Add(new GradientStop { Color = Colors.Blue, Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Colors.Green, Offset = 1 });
            scenario.Annotation.Fill = gradient;
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            // The sections of the inspector put their fields into the tree when they are laid out.
            await SettleAsync(scenario.Root);
            await RuntimeTestHelpers.WaitForRenderAsync();
            var original = scenario.Editor.ExportJson();
            var fill = scenario.Editor.GetLogicalDescendants().OfType<DaisyButton>().Single(button =>
                AutomationProperties.GetAutomationId(button) == "DaisyTourEditorValue_fill");
            var flyout = (Flyout)fill.Flyout!;
            flyout.ShowAt(fill);
            try
            {
                await RuntimeTestHelpers.WaitUntilAsync(() => flyout.Content is StackPanel { IsLoaded: true }, "the picker is shown");
                await RuntimeTestHelpers.WaitForRenderAsync();
                Assert.AreEqual(original, scenario.Editor.ExportJson(), "Opening the picker changes nothing.");
                var wheel = ((StackPanel)flyout.Content!).Children.OfType<Flowery.Controls.ColorPicker.DaisyColorWheel>().Single();
                Assert.AreEqual(Colors.Red, wheel.Color, "The picker shows red for the field without a color.");
                // The right edge of the wheel is fully saturated red at the lightness of the shown red.
                var root = TopLevel.GetTopLevel(wheel)!;
                var edge = wheel.TranslatePoint(new Point(wheel.Bounds.Width - 1, wheel.Bounds.Height / 2), root)!.Value;
                var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);
                wheel.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(wheel, pointer, root, edge, 0,
                    new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.LeftMouseButton, Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
                    Avalonia.Input.KeyModifiers.None));
                ReleaseCaptured(pointer, root, edge);
                Assert.AreEqual(Colors.Red, wheel.Color);
                Assert.AreEqual("#FFFF0000", scenario.Editor.ExportProfile().Steps[0].Annotations[0].Style?.Values.Fill);
            }
            finally { flyout.Hide(); }
        });

    private static Point HandleCenter(DaisyTourEditor editor, string id)
    {
        var handle = editor.HandleLayer.Children.OfType<Button>().Single(button =>
            AutomationProperties.GetAutomationId(button) == id);
        Assert.IsTrue(handle.IsVisible);
        return new Point(Canvas.GetLeft(handle) + handle.Width / 2, Canvas.GetTop(handle) + handle.Height / 2);
    }

    [Fact]
    public Task When_APreviewedConnectorEndIsUnavailable_TheEditorStaysOpenAndShowsWhyInsteadOfTheHost() =>
        InScenarioAsync(async scenario =>
        {
            var hostReports = 0;
            scenario.Tour.StepTargetUnavailable += (_, _) => hostReports++;
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            scenario.Annotation.Source!.IsVisible = false;
            await RuntimeTestHelpers.WaitUntilAsync(() => scenario.Editor.ErrorText.Length > 0, "the editor shows why");
            await SettleAsync(scenario.Root);
            Assert.IsTrue(scenario.Editor.IsOpen, "The editor stays open.");
            Assert.IsTrue(scenario.Tour.IsOpen, "The preview stays open.");
            Assert.AreEqual(DaisyTourTargetProblem.Annotation, scenario.Tour.Warning);
            Assert.AreEqual(0, hostReports, "The host hears nothing while the editor previews.");
            StringAssert.Contains(scenario.Editor.ErrorText, "connector end");
            var presenter = scenario.Tour.AnnotationPresenters.Single();
            Assert.IsFalse(presenter.IsVisible, "The connector without its end is hidden.");
            Assert.IsNotEmpty(presenter.ResolvedValues, "The hidden connector still resolves its style for the inspector.");
        });

    [Fact]
    public Task When_APreviewedStepFailsToBeLaidOutAfterAnEdit_TheEditorClosesWithTheError() =>
        InScenarioAsync(async scenario =>
        {
            // Regression: an edit updates the preview outside the transition, and a step that threw there reached
            // the caller of the edit instead of closing the preview as failed.
            var content = new BreakableMeasure();
            scenario.Annotation.Content = content;
            var normalClosures = 0;
            scenario.Tour.Closed += (_, _) => normalClosures++;
            await scenario.Editor.OpenAsync(scenario.Tour).WaitAsync(TestTimeout);
            var failure = new InvalidOperationException("The annotation content cannot be measured again.");
            content.Failure = failure;
            scenario.Editor.SetOverride("bodyWidth", 18d);
            Assert.IsFalse(scenario.Editor.IsOpen, "The editor closes with its failed preview.");
            Assert.IsFalse(scenario.Tour.IsOpen);
            Assert.AreSame(failure, scenario.Editor.LastError);
            Assert.AreEqual(0, normalClosures, "The host hears nothing while the editor previews.");
        });

    private static async Task InScenarioAsync(Func<Scenario, Task> action)
    {
        var scenario = new Scenario();
        try
        {
            RuntimeTestHelpers.AttachToHost(scenario.Root);
            await RuntimeTestHelpers.EnsureLoadedAsync(scenario.Root);
            await SettleAsync(scenario.Root);
            await action(scenario);
        }
        finally
        {
            scenario.Editor.Discard();
            scenario.Tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(scenario.Root);
            // Cancellation already detaches the session; cleanup only needs dispatcher turns for unloading.
            await SettleAsync(scenario.Root, DispatcherPriority.Normal);
        }
    }

    private sealed class Scenario
    {
        internal Grid Root { get; } = new() { Width = 752, Height = 480, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        internal DaisyTour Tour { get; } = new() { Id = "editor-test" };
        internal DaisyTourEditor Editor { get; } = new();
        internal DaisyTourStep Step { get; }
        internal DaisyTourAnnotation Annotation { get; }

        internal Scenario()
        {
            var source = new Button { Content = "Source", Width = 90, Height = 40, Margin = new Thickness(24, 40, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var target = new Button { Content = "Target", Width = 90, Height = 40, Margin = new Thickness(250, 190, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            Annotation = new DaisyTourAnnotation
            {
                Id = "arrow", Source = source, Target = target, ArrowStyle = DaisyTourArrowStyle.Block,
                Path = DaisyTourAnnotationPath.Curved, BodyWidth = 5, Curvature = 0.3
            };
            Step = new DaisyTourStep { Id = "step", Target = target, Title = "Test step", Description = "A stable editor fixture." };
            Step.Annotations.Add(Annotation);
            Tour.Steps.Add(Step);
            Root.Children.Add(source);
            Root.Children.Add(target);
            Root.Children.Add(Tour);
        }
    }
}
