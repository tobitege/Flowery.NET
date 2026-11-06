using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

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
        AttachToHost(control);
        await WaitForRenderAsync();
        return control;
    }

    /// <summary>
    /// Replaces the host panel's content with <paramref name="element"/> and runs layout, without waiting for a frame.
    /// </summary>
    public static void AttachToHost(Control element)
    {
        var hostPanel = GetHostPanel();
        hostPanel.Children.Clear();
        hostPanel.Children.Add(element);
        element.UpdateLayout();
    }

    /// <summary>
    /// Completes once <paramref name="element"/> has raised its Loaded event, or at once when it is loaded.
    /// Throws a <see cref="TimeoutException"/> when it is not loaded within <see cref="LoadTimeout"/>.
    /// </summary>
    public static async Task EnsureLoadedAsync(Control element)
    {
        if (element.IsLoaded)
            return;

        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, RoutedEventArgs args) => loaded.TrySetResult();
        element.Loaded += OnLoaded;
        try
        {
            await loaded.Task.WaitAsync(LoadTimeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"{element.GetType().Name} was not loaded within {LoadTimeout.TotalSeconds:0} seconds.");
        }
        finally
        {
            element.Loaded -= OnLoaded;
        }
    }

    /// <summary>
    /// Runs one case of a table-driven test. A failure or an exception names the case; a skipped result passes
    /// through unchanged. A case that does not finish within 30 seconds (or <paramref name="timeout"/>) may still
    /// run on the UI thread, so it throws a <see cref="CaseTimeoutException"/>, which stops the run like a test timeout.
    /// A <see cref="TimeoutException"/> that the case itself throws is a normal failure. On a case timeout the test's
    /// own finally blocks run while the case is still running, and an exception from that cleanup replaces the
    /// <see cref="CaseTimeoutException"/>, so the run then goes on.
    /// </summary>
    public static async Task RunCaseAsync(string name, Func<Task> body, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(30);
        Task? task = null;
        try
        {
            task = body();
            await task.WaitAsync(limit);
        }
        catch (TimeoutException error) when (task is { IsCompleted: false })
        {
            throw new CaseTimeoutException(string.Create(CultureInfo.InvariantCulture,
                $"{name} did not finish within {limit.TotalSeconds:0.###} seconds."), error);
        }
        catch (Exception error) when (error is not (Xunit.Sdk.SkipException or CaseTimeoutException))
        {
            throw new Xunit.Sdk.XunitException($"{name}: {error.Message}", error);
        }
    }

    /// <summary>Polls <paramref name="condition"/> on the UI thread until it holds.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting until {description}.");
            await Task.Delay(16);
        }
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
        await SettleDispatcherAsync();
        await WaitForRenderAsync();
    }

    /// <summary>
    /// Runs all work already posted to the dispatcher at a higher priority. Timers and thread-pool continuations
    /// that post later are not covered.
    /// </summary>
    public static async Task SettleDispatcherAsync() =>
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);

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

    /// <summary>A case of <see cref="RunCaseAsync"/> timed out; the runner reports it as a timeout and stops.</summary>
    internal sealed class CaseTimeoutException(string message, Exception innerException) : TimeoutException(message, innerException);

    private sealed class FrameSignal
    {
        private readonly TaskCompletionSource _rendered = new();

        public Task Rendered => _rendered.Task;

        public void OnFrame(TimeSpan time) => _rendered.TrySetResult();
    }
}
