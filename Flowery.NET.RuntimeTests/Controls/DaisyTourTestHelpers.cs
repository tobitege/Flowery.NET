using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Targets, roots, surface lookup, layout and cleanup that the DaisyTour runtime tests share.</summary>
internal static class DaisyTourTestHelpers
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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
    internal static async Task ArrangeAsync(Control root)
    {
        root.UpdateLayout();
        // Target tracking queues geometry, which first yields for settled layout.
        for (var turn = 0; turn < 2; turn++)
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(() => ready.TrySetResult(), DispatcherPriority.Background);
            await ready.Task.WaitAsync(Timeout);
        }
    }

    internal static async Task CleanupAsync(Control root, DaisyTour tour)
    {
        tour.Cancel();
        RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
        await ArrangeAsync(root);
    }
}
