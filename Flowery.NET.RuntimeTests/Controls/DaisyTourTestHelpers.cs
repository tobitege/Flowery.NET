using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Targets, roots, surface lookup, layout and cleanup that the DaisyTour runtime tests share.</summary>
internal static class DaisyTourTestHelpers
{
    internal static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    internal static Border CreateTarget(Thickness margin) => new()
    {
        Width = 100, Height = 40, Margin = margin,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        Child = new TextBlock { Text = "Tour target" }
    };

    internal static Grid CreateRoot(DaisyTour tour, params Control[] children)
    {
        var root = new Grid { Width = 600, Height = 400 };
        foreach (var child in children) root.Children.Add(child);
        root.Children.Add(tour);
        return root;
    }

    // WinUI's open popups; the Avalonia tour shows its surface in the overlay layer of the window, inside a theme scope.
    internal static IEnumerable<Control> OpenSurfaces(Visual element) =>
        OverlayLayer.GetOverlayLayer(element)?.Children.Where(child => child.IsVisible)
            .Select(child => child is ThemeVariantScope { Child: Control inner } ? inner : child) ?? [];

    internal static Control GetSurface(DaisyTour tour) =>
        OpenSurfaces(tour).Single(surface => surface.GetVisualDescendants().OfType<Border>()
            .Any(border => AutomationProperties.GetAutomationId(border) == "DaisyTourCard"));

    internal static IInputElement? FocusedElement(Visual element) => TopLevel.GetTopLevel(element)?.FocusManager?.GetFocusedElement();

    // WinUI's low queue priority runs after layout and rendering; Avalonia's Background priority does too.
    internal static async Task SettleAsync(Control element, DispatcherPriority? priority = null)
    {
        element.UpdateLayout();
        // Target tracking queues geometry, which first yields for settled layout.
        for (var turn = 0; turn < 2; turn++)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() => ready.TrySetResult(), priority ?? DispatcherPriority.Background);
            await ready.Task.WaitAsync(TestTimeout);
        }
    }

    internal static Task ArrangeAsync(Control root) => SettleAsync(root);

    internal static async Task CleanupAsync(Control root, DaisyTour tour)
    {
        tour.Cancel();
        RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
        await ArrangeAsync(root);
    }

    // Presses the left button in the middle of a control like the mouse does: the element under the pointer takes
    // the capture and receives the press, which bubbles to the control. The control must be what the pointer hits.
    internal static (TopLevel Root, Point Start, Pointer Pointer) PressAtCenter(Control control, PointerType type = PointerType.Mouse)
    {
        var root = TopLevel.GetTopLevel(control)!;
        var start = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)!.Value;
        var hit = MSTestAssert.IsInstanceOfType<Interactive>(root.InputHitTest(start), "Something is under the pointer.");
        MSTestAssert.IsTrue(ReferenceEquals(hit, control) || control.IsVisualAncestorOf(hit), $"The pointer hits {control.GetType().Name}, not {hit.GetType().Name}.");
        var pointer = new Pointer(Pointer.GetNextFreeId(), type, type == PointerType.Mouse);
        pointer.Capture((IInputElement)hit);
        hit.RaiseEvent(new PointerPressedEventArgs(hit, pointer, root, start, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        return (root, start, pointer);
    }

    // Raises a key press on an element, as the keyboard does on the focused element.
    internal static void PressKey(InputElement element, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        element.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = element });

    // A move with the left button held and its release, as the pointer devices raise them.
    internal static PointerEventArgs MovedArgs(Interactive source, Pointer pointer, TopLevel root, Point position) =>
        new(InputElement.PointerMovedEvent, source, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None);

    internal static PointerReleasedEventArgs ReleasedArgs(Interactive source, Pointer pointer, TopLevel root, Point position) =>
        new(source, pointer, root, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left);

    // Moves go to the element that holds the capture.
    internal static void MoveCaptured(Pointer pointer, TopLevel root, Point position)
    {
        var captured = (Interactive)pointer.Captured!;
        captured.RaiseEvent(MovedArgs(captured, pointer, root, position));
    }

    // The release goes to the element that holds the capture, which the pointer then lets go; after a lost capture
    // nothing receives it.
    internal static void ReleaseCaptured(Pointer pointer, TopLevel root, Point position)
    {
        if (pointer.Captured is not Interactive captured) return;
        captured.RaiseEvent(ReleasedArgs(captured, pointer, root, position));
        pointer.Capture(null);
    }
}
