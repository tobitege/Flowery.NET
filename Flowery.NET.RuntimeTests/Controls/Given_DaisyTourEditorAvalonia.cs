using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Theming;
using Xunit;
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
            var end = new Point(start.X - 40, start.Y);
            await ChangeGeometryAsync(tour, () => Move(pointer, root, end));
            await ChangeGeometryAsync(tour, () => Release(pointer, root, end));
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, 1);
        });
    }

    [Fact]
    public async Task When_The_Thumb_Loses_The_Capture_The_Last_Width_Stays()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var (thumb, root, start, pointer) = PressThumb(editor);
            await ChangeGeometryAsync(tour, () => Move(pointer, root, new Point(start.X - 40, start.Y)));
            pointer.Capture(null);
            await SettleAsync(editor);

            // A release that still arrives belongs to a drag that is over.
            thumb.RaiseEvent(Released(thumb, pointer, root, new Point(start.X - 80, start.Y)));
            await SettleAsync(editor);
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, 1);
        });
    }

    [Fact]
    public async Task When_Escape_Cancels_A_Resize_The_Width_Returns_And_The_Thumb_Lets_Go()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var (thumb, root, start, pointer) = PressThumb(editor);
            await ChangeGeometryAsync(tour, () => Move(pointer, root, new Point(start.X - 40, start.Y)));
            Assert.Equal(initial + 40, editor.InspectorBounds.Width, 1);

            await ChangeGeometryAsync(tour, () => thumb.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = thumb }));
            Assert.Equal(initial, editor.InspectorBounds.Width, 1);
            Assert.Null(pointer.Captured);
            Assert.True(editor.IsOpen, "Escape cancels the resize before it would discard the editor.");

            // The rest of the canceled drag changes nothing.
            var later = new Point(start.X - 80, start.Y);
            thumb.RaiseEvent(Moved(thumb, pointer, root, later));
            thumb.RaiseEvent(Released(thumb, pointer, root, later));
            await SettleAsync(editor);
            Assert.Equal(initial, editor.InspectorBounds.Width, 1);
        });
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
            editor.SetValue("opacity", 0.4d);
            var changed = editor.ExportJson();
            foreach (var undo in hotkeys.Undo)
            {
                Press(editor, undo);
                Assert.Equal(original, editor.ExportJson());
                Press(editor, hotkeys.Redo[0]);
                Assert.Equal(changed, editor.ExportJson());
            }
            // A redo gesture that includes the undo key, such as Cmd+Shift+Z on macOS, redoes.
            foreach (var redo in hotkeys.Redo)
            {
                Press(editor, hotkeys.Undo[0]);
                Assert.Equal(original, editor.ExportJson());
                Press(editor, redo);
                Assert.Equal(changed, editor.ExportJson());
            }
            return Task.CompletedTask;
        });
    }

    private static Task InEditorAsync(Func<DaisyTour, DaisyTourEditor, Task> action)
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
        return TourEditorTestHelpers.InEditorAsync(root, tour, editor => action(tour, editor));
    }

    private static T Field<T>(DaisyTourEditor editor, string automationId) where T : Control =>
        editor.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static Thumb ResizeThumb(DaisyTourEditor editor) =>
        editor.GetVisualDescendants().OfType<Thumb>().Single(item => AutomationProperties.GetAutomationId(item) == "DaisyTourEditorResize");

    private static void Press(DaisyTourEditor editor, KeyGesture gesture) =>
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = gesture.Key, KeyModifiers = gesture.KeyModifiers, Source = editor });

    // Presses the left button in the middle of the resize thumb like the mouse does: the element under the pointer
    // takes the capture and receives the press, which bubbles to the thumb.
    private static (Thumb Thumb, TopLevel Root, Point Start, Pointer Pointer) PressThumb(DaisyTourEditor editor)
    {
        var thumb = ResizeThumb(editor);
        var root = TopLevel.GetTopLevel(thumb)!;
        var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), root)!.Value;
        var hit = Assert.IsAssignableFrom<Interactive>(root.InputHitTest(start));
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        pointer.Capture((IInputElement)hit);
        hit.RaiseEvent(new PointerPressedEventArgs(hit, pointer, root, start, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        return (thumb, root, start, pointer);
    }

    // Moves go to the element that holds the capture.
    private static void Move(Pointer pointer, TopLevel root, Point position)
    {
        var captured = (Interactive)pointer.Captured!;
        captured.RaiseEvent(Moved(captured, pointer, root, position));
    }

    // The release goes to the element that holds the capture, which the mouse then lets go.
    private static void Release(Pointer pointer, TopLevel root, Point position)
    {
        var captured = (Interactive)pointer.Captured!;
        captured.RaiseEvent(Released(captured, pointer, root, position));
        pointer.Capture(null);
    }

    private static PointerEventArgs Moved(Interactive source, Pointer pointer, TopLevel root, Point position) =>
        new(InputElement.PointerMovedEvent, source, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None);

    private static PointerReleasedEventArgs Released(Interactive source, Pointer pointer, TopLevel root, Point position) =>
        new(source, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left);
}
