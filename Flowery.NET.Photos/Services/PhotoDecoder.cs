using SkiaSharp;

namespace Flowery.NET.Photos.Services;

public static class PhotoDecoder
{
    public static SKBitmap Load(string path, int maxEdge = int.MaxValue)
    {
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
}
