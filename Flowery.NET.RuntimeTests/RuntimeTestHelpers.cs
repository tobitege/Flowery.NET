using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests;

internal static class RuntimeTestHelpers
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);

    public static Window GetWindow()
    {
        if (RuntimeTestContext.Window is { } window)
            return window;

        Assert.Skip("The runtime test window is not initialized.");
        return null!;
    }

    public static Panel GetHostPanel()
    {
        if (RuntimeTestContext.HostPanel is { } hostPanel)
            return hostPanel;

        Assert.Skip("The runtime test host panel is not initialized.");
        return null!;
    }

    /// <summary>
    /// Replaces the host panel's content with <paramref name="control"/> and waits until it was rendered.
    /// </summary>
    public static async Task<T> AttachAsync<T>(T control) where T : Control
    {
        var hostPanel = GetHostPanel();
        hostPanel.Children.Clear();
        hostPanel.Children.Add(control);
        await WaitForRenderAsync();
        return control;
    }

    /// <summary>
    /// Runs layout, then waits for two rendered frames so that the compositor has drawn the current state.
    /// </summary>
    public static async Task WaitForRenderAsync()
    {
        var window = GetWindow();
        window.UpdateLayout();
        for (var frame = 0; frame < 2; frame++)
        {
            var rendered = new TaskCompletionSource();
            window.RequestAnimationFrame(_ => rendered.TrySetResult());
            await rendered.Task.WaitAsync(FrameTimeout);
        }

        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
    }

    /// <summary>
    /// Renders <paramref name="visual"/> into a bitmap at its current size. With <paramref name="artifactName"/>
    /// the image is also saved to the artifact directory.
    /// </summary>
    public static SKBitmap Render(Visual visual, string? artifactName = null, double scale = 1)
    {
        var size = visual.Bounds.Size;
        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(size.Height * scale)));
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96 * scale, 96 * scale));
        bitmap.Render(visual);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        if (artifactName is not null)
            File.WriteAllBytes(GetArtifactPath(artifactName + ".png"), stream.ToArray());

        stream.Position = 0;
        return SKBitmap.Decode(stream) ?? throw new InvalidOperationException("The rendered bitmap could not be decoded.");
    }

    /// <summary>Returns a path for <paramref name="fileName"/> in the run's artifact directory.</summary>
    public static string GetArtifactPath(string fileName)
    {
        var directory = RuntimeTestContext.ArtifactsDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
