using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;
using static Flowery.NET.RuntimeTests.Controls.DaisyTourTestHelpers;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>Opening a tour editor in the test window, and waiting for its preview to settle.</summary>
internal static class TourEditorTestHelpers
{

    /// <summary>
    /// Attaches <paramref name="root"/>, which contains <paramref name="tour"/>, opens an editor for the tour, waits
    /// until <paramref name="ready"/> holds, runs <paramref name="action"/>, and closes and detaches everything.
    /// </summary>
    internal static async Task InEditorAsync(Control root, DaisyTour tour, Func<DaisyTourEditor, Task> action, Func<bool>? ready = null)
    {
        var editor = new DaisyTourEditor();
        try
        {
            RuntimeTestHelpers.AttachToHost(root);
            await RuntimeTestHelpers.EnsureLoadedAsync(root).WaitAsync(TestTimeout);
            await SettleAsync(root);
            await editor.OpenAsync(tour).WaitAsync(TestTimeout);
            if (ready is not null) await RuntimeTestHelpers.WaitUntilAsync(ready, "the preview is laid out");
            await SettleAsync(root);
            // Pointer hit testing uses the rendered scene.
            await RuntimeTestHelpers.WaitForRenderAsync();
            await action(editor);
        }
        finally
        {
            editor.Discard();
            tour.Cancel();
            RuntimeTestHelpers.GetHostPanel().Children.Remove(root);
            await SettleAsync(root);
        }
    }

    /// <summary>Runs <paramref name="change"/> and waits until the tour has placed its geometry again.</summary>
    internal static async Task ChangeGeometryAsync(DaisyTour tour, Action change)
    {
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnGeometryUpdated(object? sender, EventArgs args) => updated.TrySetResult();
        tour.GeometryUpdated += OnGeometryUpdated;
        try
        {
            change();
            await updated.Task.WaitAsync(TestTimeout);
        }
        finally { tour.GeometryUpdated -= OnGeometryUpdated; }
    }
}
