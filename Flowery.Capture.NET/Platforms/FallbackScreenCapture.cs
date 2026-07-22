using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Flowery.Capture.Chunking;
using Flowery.Capture.Internals;

namespace Flowery.Capture.Platforms;

/// <summary>
/// Fallback screen capture using Avalonia's RenderTargetBitmap.
/// Works on all platforms but produces lower quality on high-DPI displays.
/// </summary>
public sealed class FallbackScreenCapture : IScreenCaptureService
{
    private readonly IChunkingStrategy _chunkingStrategy;

    public FallbackScreenCapture() : this(ChildBoundaryChunkingStrategy.Default) { }

    public FallbackScreenCapture(IChunkingStrategy chunkingStrategy)
    {
        _chunkingStrategy = chunkingStrategy;
    }

    /// <inheritdoc/>
    public bool IsHighQualityAvailable => false;

    /// <inheritdoc/>
    public async Task<ScreenCaptureResult> CaptureControlAsync(
        Control control,
        ScreenCaptureOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ScreenCaptureOptions();

        try
        {
            var scrollViewer = options.ScrollViewer ?? control.FindAncestorOfType<ScrollViewer>();
            var viewportHeight = scrollViewer?.Bounds.Height ?? options.MaxChunkHeight;

            var chunks = _chunkingStrategy.CalculateChunks(
                control,
                viewportHeight,
                options.ContentPanel as Panel);

            if (chunks.Count == 0)
                return ScreenCaptureResult.Fail("No chunks calculated");

            // Single chunk
            if (chunks.Count == 1)
            {
                control.BringIntoView();
                await LayoutAwaiter.WaitForLayoutAsync(options.ScrollSettleDelayMs, ct);

                var bytes = RenderControl(control);
                if (bytes.Length == 0)
                    return ScreenCaptureResult.Fail("Render returned empty image");

                return ScreenCaptureResult.Ok(bytes);
            }

            // Multiple chunks
            var results = new List<byte[]>();

            for (int i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var targetY = chunks[i];

                if (scrollViewer != null)
                {
                    var sectionPos = control.TranslatePoint(new Point(0, 0), scrollViewer);
                    if (sectionPos.HasValue)
                    {
                        var absoluteSectionY = sectionPos.Value.Y + scrollViewer.Offset.Y;
                        await LayoutAwaiter.ScrollToAndWaitAsync(
                            scrollViewer,
                            absoluteSectionY + targetY,
                            options.ScrollSettleDelayMs,
                            ct);
                    }
                }

                var chunkBytes = RenderControl(control);
                if (chunkBytes.Length > 0)
                    results.Add(chunkBytes);
            }

            if (results.Count == 0)
                return ScreenCaptureResult.Fail("All chunk renders failed");

            return ScreenCaptureResult.Ok(results);
        }
        catch (OperationCanceledException)
        {
            return ScreenCaptureResult.Fail("Capture cancelled");
        }
        catch (Exception ex)
        {
            return ScreenCaptureResult.Fail($"Capture failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<byte[]> CaptureRegionAsync(PixelRect region, CancellationToken ct = default)
    {
        // RenderTargetBitmap cannot capture arbitrary screen regions
        // This is a limitation of the fallback implementation
        return Task.FromResult(Array.Empty<byte>());
    }

    private static byte[] RenderControl(Control control)
    {
        try
        {
            var bounds = control.Bounds;
            var pixelSize = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(bounds.Height)));

            using var renderBitmap = new RenderTargetBitmap(pixelSize);
            renderBitmap.Render(control);

            using var stream = new MemoryStream();
            renderBitmap.Save(stream, PngBitmapEncoderOptions.Default);
            return stream.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}

