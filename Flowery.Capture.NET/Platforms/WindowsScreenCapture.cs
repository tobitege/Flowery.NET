#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Flowery.Capture.Chunking;
using Flowery.Capture.Internals;

namespace Flowery.Capture.Platforms;

/// <summary>
/// Windows-specific screen capture using GDI+ CopyFromScreen.
/// Provides high-quality, pixel-perfect screenshots on high-DPI displays.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsScreenCapture : IScreenCaptureService
{
    private readonly IChunkingStrategy _chunkingStrategy;

    public WindowsScreenCapture() : this(ChildBoundaryChunkingStrategy.Default) { }

    public WindowsScreenCapture(IChunkingStrategy chunkingStrategy)
    {
        _chunkingStrategy = chunkingStrategy;
    }

    /// <inheritdoc/>
    public bool IsHighQualityAvailable => true;

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

            // Calculate chunks
            var chunks = _chunkingStrategy.CalculateChunks(
                control,
                viewportHeight,
                options.ContentPanel as Panel);

            if (chunks.Count == 0)
                return ScreenCaptureResult.Fail("No chunks calculated");

            // Single chunk - simple capture
            if (chunks.Count == 1)
            {
                control.BringIntoView();
                await LayoutAwaiter.WaitForLayoutAsync(options.ScrollSettleDelayMs, ct);

                var bytes = CaptureControlRegion(control, scrollViewer, options.CaptureMargin);
                if (bytes.Length == 0)
                    return ScreenCaptureResult.Fail("Capture returned empty image");

                return ScreenCaptureResult.Ok(bytes);
            }

            // Multiple chunks - scroll and capture each
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

                var chunkBytes = CaptureControlRegion(control, scrollViewer, options.CaptureMargin);
                if (chunkBytes.Length > 0)
                    results.Add(chunkBytes);
            }

            if (results.Count == 0)
                return ScreenCaptureResult.Fail("All chunk captures failed");

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
        if (region.Width <= 0 || region.Height <= 0)
            return Task.FromResult(Array.Empty<byte>());

        try
        {
            using var bitmap = new System.Drawing.Bitmap(region.Width, region.Height);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(region.X, region.Y, 0, 0,
                    new System.Drawing.Size(region.Width, region.Height));
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return Task.FromResult(stream.ToArray());
        }
        catch
        {
            return Task.FromResult(Array.Empty<byte>());
        }
    }

    private byte[] CaptureControlRegion(Control control, ScrollViewer? scrollViewer, Thickness margin)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(control);
            if (topLevel == null)
                return Array.Empty<byte>();

            // Get screen bounds with margin adjustment
            var region = CoordinateHelper.GetScreenBounds(control, margin);

            if (region.Width <= 0 || region.Height <= 0)
                return Array.Empty<byte>();

            // Clip to viewport if available
            if (scrollViewer != null)
            {
                region = RegionClipper.ClipToViewport(region, scrollViewer);
            }

            // Clip to screen working area
            var screen = (topLevel as Window)?.Screens.ScreenFromVisual(control);
            if (screen != null)
            {
                region = RegionClipper.ClipToScreen(region, screen.WorkingArea);
            }

            if (region.Width <= 0 || region.Height <= 0)
                return Array.Empty<byte>();

            // Capture using GDI+
            using var bitmap = new System.Drawing.Bitmap(region.Width, region.Height);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(region.X, region.Y, 0, 0,
                    new System.Drawing.Size(region.Width, region.Height));
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Copies PNG image data to the Windows clipboard.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void CopyToClipboard(byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            return;

        var thread = new System.Threading.Thread(() =>
        {
            using var stream = new MemoryStream(pngBytes);
            using var image = System.Drawing.Image.FromStream(stream);
            System.Windows.Forms.Clipboard.SetImage(image);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
#endif

