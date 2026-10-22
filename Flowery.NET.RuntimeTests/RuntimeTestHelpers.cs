using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests;

internal static class RuntimeTestHelpers
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(2);

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
            var signal = new FrameSignal();
            window.RequestAnimationFrame(signal.OnFrame);
            await signal.Rendered.WaitAsync(FrameTimeout);
        }

        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
    }

    /// <summary>
    /// Lets all work already posted to the dispatcher run (for example queued effect refreshes), then waits for
    /// rendered frames.
    /// </summary>
    public static async Task WaitForIdleAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);
        await WaitForRenderAsync();
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

    /// <summary>
    /// Renders <paramref name="visual"/> once per frame until the pixel at (<paramref name="x"/>, <paramref name="y"/>)
    /// matches <paramref name="expected"/>, so that animated transitions can settle. Returns the last sampled color.
    /// </summary>
    public static async Task<SKColor> WaitForPixelAsync(Visual visual, int x, int y, Color expected, int tolerance = 2)
    {
        var deadline = DateTime.UtcNow + SettleTimeout;
        while (true)
        {
            using (var pixels = Render(visual))
            {
                var actual = pixels.GetPixel(x, y);
                if (IsClose(actual, expected, tolerance) || DateTime.UtcNow >= deadline)
                    return actual;
            }

            await WaitForRenderAsync();
        }
    }

    /// <summary>Compares the color channels of a rendered pixel with <paramref name="expected"/>.</summary>
    public static bool IsClose(SKColor actual, Color expected, int tolerance = 2) =>
        Math.Abs(actual.Red - expected.R) <= tolerance
        && Math.Abs(actual.Green - expected.G) <= tolerance
        && Math.Abs(actual.Blue - expected.B) <= tolerance;

    /// <summary>Resolves a theme color resource as <paramref name="control"/> sees it.</summary>
    public static Color GetThemeColor(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, control.ActualThemeVariant, out var resource), $"Missing resource {key}.");
        return Assert.IsType<Color>(resource);
    }

    /// <summary>Returns the exception that a reflection call wrapped, or <paramref name="exception"/> itself.</summary>
    public static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

    /// <summary>Returns a path for <paramref name="fileName"/> in the run's artifact directory.</summary>
    public static string GetArtifactPath(string fileName)
    {
        var directory = RuntimeTestContext.ArtifactsDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class FrameSignal
    {
        private readonly TaskCompletionSource _rendered = new();

        public Task Rendered => _rendered.Task;

        public void OnFrame(TimeSpan time) => _rendered.TrySetResult();
    }
}
