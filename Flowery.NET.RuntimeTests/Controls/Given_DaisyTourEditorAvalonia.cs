using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Theming;
using Xunit;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Editor behavior that depends on Avalonia mechanisms WinUI does not have: presenter themes, the theme
/// scope, the resize thumb and the macOS command key.</summary>
public class Given_DaisyTourEditorAvalonia
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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
    public async Task When_The_Resize_Thumb_Is_Dragged_The_Inspector_Takes_The_Final_Width()
    {
        await InEditorAsync(async (tour, editor) =>
        {
            var initial = editor.InspectorBounds.Width;
            var (thumb, root, start, pointer) = PressThumb(editor);
            var end = new Point(start.X - 40, start.Y);
            await ChangeGeometryAsync(tour, () => thumb.RaiseEvent(Moved(thumb, pointer, root, end)));
            await ChangeGeometryAsync(tour, () => thumb.RaiseEvent(Released(thumb, pointer, root, end)));
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
            // A real mouse captures the thumb when it is pressed.
            pointer.Capture(thumb);
            await ChangeGeometryAsync(tour, () => thumb.RaiseEvent(Moved(thumb, pointer, root, new Point(start.X - 40, start.Y))));
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
    public async Task When_Command_Z_Is_Pressed_On_A_Mac_The_Editor_Undoes()
    {
        await InEditorAsync((_, editor) =>
        {
            var original = editor.ExportJson();
            editor.SetValue("opacity", 0.4d);
            Assert.True(editor.HasChanges);
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z, KeyModifiers = KeyModifiers.Meta, Source = editor });
            Assert.Equal(original, editor.ExportJson());
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Y, KeyModifiers = KeyModifiers.Meta, Source = editor });
            Assert.True(editor.HasChanges);
            return Task.CompletedTask;
        });
    }

    private static async Task InEditorAsync(Func<DaisyTour, DaisyTourEditor, Task> action)
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
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(Timeout);
            await SettleAsync(root);
            await editor.OpenAsync(tour).WaitAsync(Timeout);
            await SettleAsync(root);
            await action(tour, editor);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await SettleAsync(root);
        }
    }

    private static T Field<T>(DaisyTourEditor editor, string automationId) where T : Control =>
        editor.GetLogicalDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    // Presses the left button in the middle of the resize thumb, in the coordinates of its top level.
    private static (Thumb Thumb, TopLevel Root, Point Start, Pointer Pointer) PressThumb(DaisyTourEditor editor)
    {
        var thumb = editor.GetVisualDescendants().OfType<Thumb>().Single(item => AutomationProperties.GetAutomationId(item) == "DaisyTourEditorResize");
        var root = TopLevel.GetTopLevel(thumb)!;
        var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), root)!.Value;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        thumb.RaiseEvent(new PointerPressedEventArgs(thumb, pointer, root, start, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        return (thumb, root, start, pointer);
    }

    private static PointerEventArgs Moved(Thumb thumb, Pointer pointer, TopLevel root, Point position) =>
        new(InputElement.PointerMovedEvent, thumb, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None);

    private static PointerReleasedEventArgs Released(Thumb thumb, Pointer pointer, TopLevel root, Point position) =>
        new(thumb, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left);

    private static async Task ChangeGeometryAsync(DaisyTour tour, Action change)
    {
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnGeometryUpdated(object? sender, EventArgs args) => updated.TrySetResult();
        tour.GeometryUpdated += OnGeometryUpdated;
        try
        {
            change();
            await updated.Task.WaitAsync(Timeout);
        }
        finally { tour.GeometryUpdated -= OnGeometryUpdated; }
    }

    private static async Task SettleAsync(Control element)
    {
        element.UpdateLayout();
        for (var turn = 0; turn < 2; turn++)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() => ready.TrySetResult(), DispatcherPriority.Background);
            await ready.Task.WaitAsync(Timeout);
        }
    }
}
