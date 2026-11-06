using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Flowery.Localization;
using Flowery.Theming;
using Xunit;
using static Flowery.NET.RuntimeTests.Controls.DaisyTourTestHelpers;
using static Flowery.NET.RuntimeTests.Controls.TourEditorTestHelpers;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Editor behavior that depends on Avalonia mechanisms WinUI does not have: presenter themes, the theme
/// scope, the resize thumb and the platform's undo and redo gestures.</summary>
public class Given_DaisyTourEditorAvalonia
{
    [Fact]
    public async Task When_A_Field_Flyout_Opens_The_First_Time_It_Has_The_Daisy_Presenter_Theme()
    {
        await InEditorAsync(async (_, editor) =>
        {
            var background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
            var mode = Field<DaisyButton>(editor, "DaisyTourEditorMode_stroke");
            var menu = Assert.IsType<MenuFlyout>(mode.Flyout);
            menu.ShowAt(mode);
            await RuntimeTestHelpers.WaitForRenderAsync();
            try { Assert.Same(background, Assert.IsType<MenuFlyoutPresenter>(menu.Popup.Child).Background); }
            finally { menu.Hide(); }

            var color = Field<DaisyButton>(editor, "DaisyTourEditorValue_stroke");
            var flyout = Assert.IsType<Flyout>(color.Flyout);
            flyout.ShowAt(color);
            await RuntimeTestHelpers.WaitForRenderAsync();
            try { Assert.Same(background, Assert.IsType<FlyoutPresenter>(flyout.Popup.Child).Background); }
            finally { flyout.Hide(); }
        });
    }

    [Fact]
    public async Task When_The_Editor_Shows_A_Field_Its_Caption_Uses_The_Daisy_Text_Color()
    {
        await InEditorAsync((_, editor) =>
        {
            var label = FloweryLocalization.GetString("TourEditor_Field_stroke");
            var caption = editor.GetLogicalDescendants().OfType<TextBlock>().First(text => text.Text == label);
            Assert.Same(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), caption.Foreground);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task When_The_Pointer_Is_Over_The_Resize_Grip_It_Hits_The_Thumb()
    {
        await InEditorAsync((_, editor) =>
        {
            var thumb = ResizeThumb(editor);
            var root = TopLevel.GetTopLevel(thumb)!;
            var center = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), root)!.Value;
            var hit = Assert.IsAssignableFrom<Visual>(root.InputHitTest(center));
            Assert.True(ReferenceEquals(hit, thumb) || thumb.IsVisualAncestorOf(hit), $"The grip hits {hit.GetType().Name}, not the thumb.");
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task When_The_Resize_Thumb_Is_Dragged_The_Inspector_Takes_The_Final_Width()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var (thumb, root, start, pointer) = PressThumb(editor);
            Assert.Same(thumb, pointer.Captured);
            await ChangeGeometryAsync(tour, () => MoveCaptured(pointer, root, new Point(start.X - 40, start.Y)));
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, tolerance: 0.5);
            // The release lands away from the last move, with no final move: the inspector takes its position.
            await ChangeGeometryAsync(tour, () => ReleaseCaptured(pointer, root, new Point(start.X - 55, start.Y)));
            Assert.Equal(initial + 55, editor.InspectorBounds.Width, tolerance: 0.5);
        });
    }

    [Fact]
    public async Task When_The_Thumb_Loses_The_Capture_The_Last_Width_Stays()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var (thumb, root, start, pointer) = PressThumb(editor);
            await ChangeGeometryAsync(tour, () => MoveCaptured(pointer, root, new Point(start.X - 40, start.Y)));
            pointer.Capture(null);
            await SettleAsync(editor);

            // A release that still arrives belongs to a drag that is over.
            thumb.RaiseEvent(ReleasedArgs(thumb, pointer, root, new Point(start.X - 80, start.Y)));
            await SettleAsync(editor);
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, tolerance: 0.5);
        });
    }

    [Fact]
    public async Task When_Escape_Cancels_A_Resize_The_Width_Returns_And_The_Thumb_Lets_Go()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var (thumb, root, start, pointer) = PressThumb(editor);
            await ChangeGeometryAsync(tour, () => MoveCaptured(pointer, root, new Point(start.X - 40, start.Y)));
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, tolerance: 0.5);

            await ChangeGeometryAsync(tour, () => PressKey(thumb, Key.Escape));
            Assert.Equal(initial, editor.InspectorBounds.Width, tolerance: 0.5);
            Assert.Null(pointer.Captured);
            Assert.True(editor.IsOpen, "Escape cancels the resize before it would discard the editor.");

            // The rest of the canceled drag changes nothing.
            var later = new Point(start.X - 80, start.Y);
            thumb.RaiseEvent(MovedArgs(thumb, pointer, root, later));
            thumb.RaiseEvent(ReleasedArgs(thumb, pointer, root, later));
            await SettleAsync(editor);
            Assert.Equal(initial, editor.InspectorBounds.Width, tolerance: 0.5);
        });
    }

    [Fact]
    public async Task When_A_Second_Touch_Presses_The_Thumb_During_A_Resize_The_First_Keeps_The_Resize()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var thumb = ResizeThumb(editor);
            var (root, start, first) = PressAtCenter(thumb, PointerType.Touch);
            await ChangeGeometryAsync(tour, () => MoveCaptured(first, root, new Point(start.X - 40, start.Y)));
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, tolerance: 0.5);

            // A second finger on the grip neither moves the origin of the drag nor resizes, and its release does not
            // end the resize of the first finger. Hit testing sees the moved grip once it has been rendered.
            await WaitUntilHitAsync(thumb);
            var (_, otherStart, second) = PressAtCenter(thumb, PointerType.Touch);
            var otherEnd = new Point(otherStart.X - 90, otherStart.Y);
            MoveCaptured(second, root, otherEnd);
            ReleaseCaptured(second, root, otherEnd);
            await SettleAsync(editor);
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, tolerance: 0.5);

            var end = new Point(start.X - 60, start.Y);
            await ChangeGeometryAsync(tour, () => MoveCaptured(first, root, end));
            await ChangeGeometryAsync(tour, () => ReleaseCaptured(first, root, end));
            Assert.Equal(initial + 60, editor.InspectorBounds.Width, tolerance: 0.5);

            // Once the first finger has finished, a third one resizes on its own, and its release away from its last
            // move sets the width.
            await WaitUntilHitAsync(thumb);
            var before = editor.InspectorBounds.Width;
            var (_, thirdStart, third) = PressAtCenter(thumb, PointerType.Touch);
            await ChangeGeometryAsync(tour, () => MoveCaptured(third, root, new Point(thirdStart.X - 20, thirdStart.Y)));
            Assert.Equal(before + 20, editor.InspectorBounds.Width, tolerance: 0.5);
            await ChangeGeometryAsync(tour, () => ReleaseCaptured(third, root, new Point(thirdStart.X - 35, thirdStart.Y)));
            Assert.Equal(before + 35, editor.InspectorBounds.Width, tolerance: 0.5);
        });
    }

    [Fact]
    public async Task When_A_Pens_Second_Button_Joins_A_Resize_Escape_Still_Restores_The_Width_From_Before()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var thumb = ResizeThumb(editor);
            var (root, start, pointer) = PressAtCenter(thumb, PointerType.Pen);
            var moved = new Point(start.X - 40, start.Y);
            await ChangeGeometryAsync(tour, () => MoveCaptured(pointer, root, moved));

            // A pen's second button joins the drag: Avalonia's Thumb starts its drag again for the same pointer. A mouse
            // reports another button as a move instead.
            thumb.RaiseEvent(new PointerPressedEventArgs(thumb, pointer, root, moved, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton | RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
                KeyModifiers.None));
            await SettleAsync(editor);
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, tolerance: 0.5);

            await ChangeGeometryAsync(tour, () => PressKey(thumb, Key.Escape));
            Assert.Equal(initial, editor.InspectorBounds.Width, tolerance: 0.5);
        });
    }

    [Fact]
    public async Task When_The_Focused_Resize_Thumb_Gets_Arrow_Keys_The_Inspector_Width_Follows()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            // Avalonia's Thumb takes no focus by default; the editor makes it focusable for the keyboard.
            var thumb = ResizeThumb(editor);
            Assert.True(thumb.Focus(), "The resize thumb takes the focus.");
            var initial = editor.InspectorBounds.Width;
            // Docked on the right, the inner edge is the left one: Right narrows the inspector, Left widens it.
            await ChangeGeometryAsync(tour, () => PressKey(thumb, Key.Right));
            Assert.Equal(initial - 10, editor.InspectorBounds.Width, tolerance: 0.5);
            await ChangeGeometryAsync(tour, () => PressKey(thumb, Key.Left));
            Assert.Equal(initial, editor.InspectorBounds.Width, tolerance: 0.5);
        });
    }

    [Fact]
    public async Task When_The_Daisy_Theme_Changes_The_Editor_Takes_Its_Theme_Variant()
    {
        var previousTheme = DaisyThemeManager.CurrentThemeName;
        try
        {
            // The page is forced to the other variant, so only the editor's own code gives it the Daisy variant.
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            await InEditorAsync(async (tour, editor) =>
            {
                var page = tour.GetVisualAncestors().OfType<ThemeVariantScope>().First();
                var scope = editor.GetVisualDescendants().OfType<ThemeVariantScope>().First();
                Assert.Equal(ThemeVariant.Light, tour.ActualThemeVariant);
                Assert.Equal(ThemeVariant.Dark, scope.ActualThemeVariant);
                // Each switch changes the theme, so it places the preview again.
                await ChangeGeometryAsync(tour, () =>
                {
                    page.RequestedThemeVariant = ThemeVariant.Dark;
                    Assert.True(DaisyThemeManager.ApplyTheme("Light"));
                });
                Assert.Equal(ThemeVariant.Dark, tour.ActualThemeVariant);
                Assert.Equal(ThemeVariant.Light, scope.ActualThemeVariant);
            }, ThemeVariant.Light);
        }
        finally
        {
            if (previousTheme is not null) DaisyThemeManager.ApplyTheme(previousTheme);
        }
    }

    [Fact]
    public async Task When_The_Platform_Undo_And_Redo_Gestures_Are_Pressed_The_Editor_Follows_Them()
    {
        await InEditorAsync((_, editor) =>
        {
            var hotkeys = Application.Current!.PlatformSettings!.HotkeyConfiguration;
            // Ctrl+Z on Windows and Linux, Cmd+Z on macOS.
            Assert.Contains(hotkeys.Undo, gesture => gesture.Key == Key.Z && gesture.KeyModifiers == hotkeys.CommandModifiers);
            var original = editor.ExportJson();
            editor.SetOverride("opacity", 0.4d);
            var changed = editor.ExportJson();
            foreach (var undo in hotkeys.Undo)
            {
                PressKey(editor, undo.Key, undo.KeyModifiers);
                Assert.Equal(original, editor.ExportJson());
                PressKey(editor, hotkeys.Redo[0].Key, hotkeys.Redo[0].KeyModifiers);
                Assert.Equal(changed, editor.ExportJson());
            }
            // A redo gesture that includes the undo key, such as Cmd+Shift+Z on macOS, redoes.
            foreach (var redo in hotkeys.Redo)
            {
                PressKey(editor, hotkeys.Undo[0].Key, hotkeys.Undo[0].KeyModifiers);
                Assert.Equal(original, editor.ExportJson());
                PressKey(editor, redo.Key, redo.KeyModifiers);
                Assert.Equal(changed, editor.ExportJson());
            }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task When_The_Step_List_Changes_The_Step_With_The_Keyboard_It_Keeps_The_Focus()
    {
        // Regression: every transition moved the focus to the preview card, so arrowing through the step list
        // stopped after one step, and on the last step the focus stayed on the surface that is not focusable.
        var (root, tour) = StepsTour(3);
        await TourEditorTestHelpers.InEditorAsync(root, tour, async editor =>
        {
            var steps = Field<DaisySelect>(editor, "DaisyTourEditorSteps");
            Assert.True(steps.Focus(NavigationMethod.Tab), "The step list takes the focus.");
            foreach (var index in new[] { 1, 2 })
            {
                PressKey(steps, Key.Down);
                await RuntimeTestHelpers.WaitUntilAsync(() => tour.CurrentIndex == index && !tour.IsTransitioning, $"step {index + 1} is shown");
                await SettleAsync(root);
                Assert.Same(steps, FocusedElement(root));
                Assert.True(steps.IsEffectivelyEnabled, "The step list is enabled again.");
            }
        });
    }

    [Fact]
    public async Task When_The_Preview_Card_Reaches_The_Last_Step_The_Focus_Goes_To_A_Control_That_Takes_It()
    {
        // Regression: on the last step of the preview, Next is disabled, and the focus stayed on the surface; a focus
        // move out of the tour was redirected to the disabled Next as well.
        var outside = new Button
        {
            Content = "Outside", Margin = new Thickness(400, 24, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var (root, tour) = StepsTour(2, outside);
        await TourEditorTestHelpers.InEditorAsync(root, tour, async editor =>
        {
            var next = Find<DaisyButton>(tour, "DaisyTourNext");
            var back = Find<DaisyButton>(tour, "DaisyTourBack");
            Assert.True(next.Focus(NavigationMethod.Tab), "Next takes the focus.");
            await tour.NextAsync().WaitAsync(TestTimeout);
            await SettleAsync(root);
            Assert.False(next.IsEffectivelyEnabled, "The preview cannot finish the tour.");
            Assert.Same(back, FocusedElement(root));

            outside.Focus();
            Assert.Same(back, FocusedElement(root));
        });
    }

    [Fact]
    public async Task When_The_Kind_Is_Chosen_With_The_Keyboard_The_Inspector_Keeps_Its_Fields_And_The_Focus()
    {
        // Regression: every change of the kind built the whole inspector again, so the list that changed it lost the
        // focus, and every color field built its picker up front.
        await InEditorAsync(async (tour, editor) =>
        {
            var kind = Field<DaisySelect>(editor, "DaisyTourEditorValue_kind");
            var stroke = Field<DaisyButton>(editor, "DaisyTourEditorValue_stroke");
            Assert.Null(Assert.IsType<Flyout>(stroke.Flyout).Content);
            Assert.True(kind.Focus(NavigationMethod.Tab), "The kind list takes the focus.");
            var before = editor.ExportJson();
            await ChangeGeometryAsync(tour, () => PressKey(kind, Key.Down));
            Assert.NotEqual(before, editor.ExportJson());
            Assert.Same(kind, FocusedElement(editor));
            Assert.Same(kind, Field<DaisySelect>(editor, "DaisyTourEditorValue_kind"));
            Assert.Same(stroke, Field<DaisyButton>(editor, "DaisyTourEditorValue_stroke"));
            Assert.Equal<object?>(editor.ExportProfile().Steps.Single().Annotations.Single().Style!.Values.Kind, kind.SelectedItem);
        });
    }

    [Fact]
    public async Task When_The_Color_Wheel_Is_Dragged_The_Drag_Is_One_Undo_Action()
    {
        // Regression: every pointer move on the wheel was an undo action of its own, so a short drag pushed the real
        // changes out of the history.
        await InEditorAsync(async (_, editor) =>
        {
            var original = editor.ExportJson();
            var stroke = Field<DaisyButton>(editor, "DaisyTourEditorValue_stroke");
            var flyout = Assert.IsType<Flyout>(stroke.Flyout);
            flyout.ShowAt(stroke);
            try
            {
                await RuntimeTestHelpers.WaitForRenderAsync();
                Assert.Equal(original, editor.ExportJson());
                var wheel = Assert.IsType<StackPanel>(flyout.Content).Children.OfType<DaisyColorWheel>().Single();
                // The flyout's own top level may not have rendered for hit testing yet; the press goes to the wheel.
                var root = TopLevel.GetTopLevel(wheel)!;
                var start = wheel.TranslatePoint(new Point(wheel.Bounds.Width / 2, wheel.Bounds.Height / 2), root)!.Value;
                var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                wheel.RaiseEvent(new PointerPressedEventArgs(wheel, pointer, root, start, 0,
                    new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
                Assert.Same(wheel, pointer.Captured);
                foreach (var (x, y) in new[] { (40d, 0d), (0d, 40d), (-40d, 20d), (30d, -30d) })
                    MoveCaptured(pointer, root, new Point(start.X + x, start.Y + y));
                var dragged = editor.ExportJson();
                ReleaseCaptured(pointer, root, new Point(start.X + 30, start.Y - 30));
                Assert.Equal(dragged, editor.ExportJson());
                Assert.NotEqual(original, dragged);

                editor.Undo();
                Assert.Equal(original, editor.ExportJson());
                editor.Redo();
                Assert.Equal(dragged, editor.ExportJson());
            }
            finally { flyout.Hide(); }
        });
    }

    private static (Grid Root, DaisyTour Tour) StepsTour(int count, params Control[] others)
    {
        var root = new Grid { Width = 752, Height = 480 };
        var target = new Button
        {
            Content = "Target", Width = 90, Height = 40, Margin = new Thickness(160, 80, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var tour = new DaisyTour { Id = "editor-steps" };
        for (var index = 0; index < count; index++)
            tour.Steps.Add(new DaisyTourStep { Id = "step" + index, Target = target, Title = "Step " + index });
        root.Children.Add(target);
        foreach (var other in others) root.Children.Add(other);
        root.Children.Add(tour);
        return (root, tour);
    }

    private static T Find<T>(DaisyTour tour, string automationId) where T : Control =>
        GetSurface(tour).GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static Task InEditorAsync(Func<DaisyTour, DaisyTourEditor, Task> action, ThemeVariant? pageVariant = null)
    {
        var root = new Grid { Width = 752, Height = 480 };
        var target = new Button
        {
            Content = "Target", Width = 90, Height = 40, Margin = new Thickness(160, 80, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var tour = new DaisyTour { Id = "editor-avalonia" };
        tour.Steps.Add(new DaisyTourStep { Id = "step", Target = target, Title = "Step" });
        root.Children.Add(target);
        root.Children.Add(tour);
        // A page variant puts the page in a scope with its own variant, apart from the Daisy theme.
        Control page = pageVariant is null ? root : new ThemeVariantScope { RequestedThemeVariant = pageVariant, Child = root };
        return TourEditorTestHelpers.InEditorAsync(page, tour, editor => action(tour, editor));
    }

    private static T Field<T>(DaisyTourEditor editor, string automationId) where T : Control =>
        editor.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static Thumb ResizeThumb(DaisyTourEditor editor) =>
        editor.GetVisualDescendants().OfType<Thumb>().Single(item => AutomationProperties.GetAutomationId(item) == "DaisyTourEditorResize");

    // Presses the resize thumb like the mouse does; see PressAtCenter.
    private static (Thumb Thumb, TopLevel Root, Point Start, Pointer Pointer) PressThumb(DaisyTourEditor editor)
    {
        var thumb = ResizeThumb(editor);
        var (root, start, pointer) = PressAtCenter(thumb);
        return (thumb, root, start, pointer);
    }
}
