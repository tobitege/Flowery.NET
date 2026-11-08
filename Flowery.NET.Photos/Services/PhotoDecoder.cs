using SkiaSharp;

namespace Flowery.NET.Photos.Services;

public static class PhotoDecoder
{
    /// <param name="path">The photo or video.</param>
    /// <param name="maxEdge">The longest edge the picture is scaled down to.</param>
    /// <param name="token">Stops taking the still of a video, for example when its thumbnail leaves the screen.</param>
    public static SKBitmap Load(string path, int maxEdge = int.MaxValue, CancellationToken token = default)
    {
        if (ImageMetadataReader.IsVideo(path)) return LoadVideoFrame(path, Math.Min(maxEdge, 1920), token);
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream);
        if (codec == null)
        {
#if WINDOWS
            try { return WindowsPhotoCodec.DecodeAsync(path, maxEdge).GetAwaiter().GetResult(); }
            catch (System.Runtime.InteropServices.COMException ex)
            { throw new InvalidDataException("Image decoding failed.", ex); }
#else
            throw new InvalidDataException("Unsupported image format.");
#endif
        }
        var scale = Math.Min(1f, (float)maxEdge / Math.Max(codec.Info.Width, codec.Info.Height));
        var dimensions = codec.GetScaledDimensions(scale);
        using var decoded = new SKBitmap(new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new InvalidDataException($"Image decoding failed: {result}");

        var origin = (int)codec.EncodedOrigin;
        var swap = origin >= 5;
        var oriented = new SKBitmap(swap ? decoded.Height : decoded.Width, swap ? decoded.Width : decoded.Height);
        using (var canvas = new SKCanvas(oriented))
        {
            switch (origin)
            {
                case 2: canvas.Translate(decoded.Width, 0); canvas.Scale(-1, 1); break;
                case 3: canvas.Translate(decoded.Width, decoded.Height); canvas.RotateDegrees(180); break;
                case 4: canvas.Translate(0, decoded.Height); canvas.Scale(1, -1); break;
                case 5: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case 6: canvas.Translate(decoded.Height, 0); canvas.RotateDegrees(90); break;
                case 7: canvas.Translate(decoded.Height, decoded.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case 8: canvas.Translate(0, decoded.Width); canvas.RotateDegrees(-90); break;
            }
            canvas.DrawBitmap(decoded, 0, 0);
        }
        var longest = Math.Max(oriented.Width, oriented.Height);
        if (longest <= maxEdge) return oriented;
        // PNG and other codecs can require full decode before downsampling.
        var ratio = (double)maxEdge / longest;
        var resized = oriented.Resize(new SKImageInfo(Math.Max(1, (int)(oriented.Width * ratio)),
            Math.Max(1, (int)(oriented.Height * ratio))), new SKSamplingOptions(SKFilterMode.Linear));
        oriented.Dispose();
        return resized ?? throw new InvalidDataException("Image resampling failed.");
    }

    // The still of a video: a frame from the Windows player, otherwise from ffmpeg when it is installed.
    private static SKBitmap LoadVideoFrame(string path, int maxEdge, CancellationToken token)
    {
#if WINDOWS
        try
        {
            // The player reads the file by its URI. The shell's thumbnails waited while the folder dialog of Browse…
            // kept the shell busy, on a network drive for twenty seconds.
            if (WindowsVideoPlayback.CaptureStillAsync(path, maxEdge, StillPosition, token).GetAwaiter().GetResult() is { } still) return still;
        }
        // The player rejects some files and paths; ffmpeg may still read them.
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException
            or ArgumentException) { }
#endif
        return LoadFrameWithFfmpeg(path, maxEdge) ?? throw new InvalidDataException("No installed decoder can read this video.");
    }

    /// <summary>Where a still is taken: a second into the video, or a third of a shorter one, past the black first frame many cameras record.</summary>
    internal static TimeSpan StillPosition(TimeSpan duration) => TimeSpan.FromSeconds(Math.Min(1, duration.TotalSeconds / 3));

    private static SKBitmap? LoadFrameWithFfmpeg(string path, int maxEdge)
    {
        var seconds = StillPosition(TimeSpan.FromSeconds(VideoMetadataReader.Probe(path)?.Details?.DurationSeconds ?? 0)).TotalSeconds;
        var info = new System.Diagnostics.ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-v", "error", "-ss", seconds.ToString("0.###", CultureInfo.InvariantCulture), "-i", path, "-frames:v", "1",
            "-vf", $"scale=min({maxEdge}\\,iw):min({maxEdge}\\,ih):force_original_aspect_ratio=decrease",
            "-f", "image2pipe", "-c:v", "png", "-"
        }) info.ArgumentList.Add(argument);
        try
        {
            using var process = System.Diagnostics.Process.Start(info);
            if (process == null) return null;
            var errors = process.StandardError.ReadToEndAsync();
            using var frame = new MemoryStream();
            var copy = process.StandardOutput.BaseStream.CopyToAsync(frame);
            if (!process.WaitForExit(15_000)) { process.Kill(true); return null; }
            copy.GetAwaiter().GetResult(); errors.GetAwaiter().GetResult();
            frame.Position = 0;
            return process.ExitCode == 0 && frame.Length > 0 ? SKBitmap.Decode(frame) : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; } // ffmpeg is not installed.
    }
}
