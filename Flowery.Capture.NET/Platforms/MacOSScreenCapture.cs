using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// macOS screen capture using the screencapture CLI tool.
/// Requires Screen Recording permission in System Preferences.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSScreenCapture : IScreenCaptureService
{
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly FallbackScreenCapture _fallback;

    public MacOSScreenCapture() : this(ChildBoundaryChunkingStrategy.Default) { }

    public MacOSScreenCapture(IChunkingStrategy chunkingStrategy)
    {
        _chunkingStrategy = chunkingStrategy;
        _fallback = new FallbackScreenCapture(chunkingStrategy);
    }

    /// <inheritdoc/>
    public bool IsHighQualityAvailable => IsScreencaptureAvailable();

    /// <inheritdoc/>
    public async Task<ScreenCaptureResult> CaptureControlAsync(
        Control control,
        ScreenCaptureOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ScreenCaptureOptions();

        // Fall back to RTB if screencapture is not available
        if (!IsHighQualityAvailable)
            return await _fallback.CaptureControlAsync(control, options, ct);

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

                var region = CoordinateHelper.GetScreenBounds(control, options.CaptureMargin);
                if (scrollViewer != null)
                    region = RegionClipper.ClipToViewport(region, scrollViewer);

                var bytes = await CaptureRegionAsync(region, ct);
                if (bytes.Length == 0)
                    return ScreenCaptureResult.Fail("Capture returned empty image (check Screen Recording permission)");

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

                var region = CoordinateHelper.GetScreenBounds(control, options.CaptureMargin);
                if (scrollViewer != null)
                    region = RegionClipper.ClipToViewport(region, scrollViewer);

                var chunkBytes = await CaptureRegionAsync(region, ct);
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
    public async Task<byte[]> CaptureRegionAsync(PixelRect region, CancellationToken ct = default)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return Array.Empty<byte>();

        var tempFile = Path.Combine(Path.GetTempPath(), $"flowery_capture_{Guid.NewGuid():N}.png");

        try
        {
            // screencapture -R x,y,w,h <file>
            var startInfo = new ProcessStartInfo
            {
                FileName = "screencapture",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add($"{region.X},{region.Y},{region.Width},{region.Height}");
            startInfo.ArgumentList.Add(tempFile);

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(tempFile))
                return Array.Empty<byte>();

            return await File.ReadAllBytesAsync(tempFile, ct);
        }
        catch
        {
            return Array.Empty<byte>();
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private static bool IsScreencaptureAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add("screencapture");

            using var process = new Process
            {
                StartInfo = startInfo
            };
            process.Start();
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

