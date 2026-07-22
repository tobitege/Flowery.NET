using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Flowery.Capture.Internals;

/// <summary>
/// Helper for waiting for layout and render operations to complete.
/// </summary>
internal static class LayoutAwaiter
{
    /// <summary>
    /// Waits for layout to settle after a scroll operation.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WaitForLayoutAsync(int delayMs = 500, CancellationToken ct = default)
    {
        // First, ensure we're on the UI thread and layout is processed
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        // Then wait for rendering to settle
        await Task.Delay(delayMs, ct);
    }

    /// <summary>
    /// Brings a control into view and waits for layout to settle.
    /// </summary>
    public static async Task BringIntoViewAndWaitAsync(Control control, int delayMs = 500, CancellationToken ct = default)
    {
        control.BringIntoView();
        await WaitForLayoutAsync(delayMs, ct);
    }

    /// <summary>
    /// Scrolls a ScrollViewer to a specific offset and waits for layout to settle.
    /// </summary>
    public static async Task ScrollToAndWaitAsync(
        ScrollViewer scrollViewer,
        double yOffset,
        int delayMs = 500,
        CancellationToken ct = default)
    {
        scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Offset.X, yOffset);
        await WaitForLayoutAsync(delayMs, ct);
    }
}

