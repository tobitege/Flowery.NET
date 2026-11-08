using SkiaSharp;

namespace Flowery.NET.Photos.Services;

/// <summary>
/// The frames of an animated GIF or WebP. A frame may draw over an earlier one, so frames are decoded on top of the
/// frame they require and kept in a cache of limited size; playing in order mostly needs only the previous frame.
/// </summary>
public sealed class AnimatedImage : IDisposable
{
    private const long CacheBytes = 128L * 1024 * 1024;
    // Browsers show frames that claim 10 ms or less for a tenth of a second.
    private static readonly TimeSpan DefaultFrame = TimeSpan.FromMilliseconds(100);

    private readonly SKData _data;
    private readonly SKCodec _codec;
    private readonly SKCodecFrameInfo[] _frames;
    private readonly SKImageInfo _info;
    private readonly Dictionary<int, SKBitmap> _cache = [];
    private readonly LinkedList<int> _recent = new();
    private readonly Lock _sync = new();
    private bool _disposed;

    private AnimatedImage(SKData data, SKCodec codec)
    {
        _data = data; _codec = codec; _frames = codec.FrameInfo;
        _info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
    }

    public int FrameCount => _frames.Length;

    /// <summary>How often the animation plays after the first time; -1 repeats it forever.</summary>
    public int Repetitions => _codec.RepetitionCount;

    public TimeSpan TotalDuration => TimeSpan.FromTicks(Enumerable.Range(0, FrameCount).Sum(index => FrameDuration(index).Ticks));

    public TimeSpan FrameDuration(int index) =>
        _frames[index].Duration <= 10 ? DefaultFrame : TimeSpan.FromMilliseconds(_frames[index].Duration);

    /// <summary>The frames of a GIF or WebP file with more than one frame, or null for any other image.</summary>
    public static AnimatedImage? TryOpen(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not (".gif" or ".webp")) return null;
        var data = SKData.Create(path);
        if (data == null) return null;
        var codec = SKCodec.Create(data);
        // PhotoDecoder turns images by their EXIF orientation; animations that would need it stay still.
        if (codec is { FrameCount: > 1, EncodedOrigin: SKEncodedOrigin.TopLeft }) return new AnimatedImage(data, codec);
        codec?.Dispose(); data.Dispose();
        return null;
    }

    /// <summary>A copy of a frame that the caller owns.</summary>
    public SKBitmap CopyFrame(int index)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Decode(index).Copy();
        }
    }

    private SKBitmap Decode(int index)
    {
        if (_cache.TryGetValue(index, out var cached))
        {
            _recent.Remove(index); _recent.AddFirst(index);
            return cached;
        }
        var bitmap = new SKBitmap(_info);
        var required = _frames[index].RequiredFrame;
        if (required >= 0) Decode(required).GetPixelSpan().CopyTo(bitmap.GetPixelSpan());
        else bitmap.Erase(SKColors.Transparent);
        var result = _codec.GetPixels(_info, bitmap.GetPixels(), new SKCodecOptions(index, required));
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidDataException($"Animation frame {index} could not be decoded: {result}");
        }
        _cache[index] = bitmap; _recent.AddFirst(index);
        // Keep at least this frame and the one before it, which the next frame usually draws over.
        while (_recent.Count > 2 && (long)_recent.Count * _info.BytesSize > CacheBytes && _recent.Last is { } oldest)
        {
            _cache.Remove(oldest.Value, out var evicted); evicted?.Dispose();
            _recent.RemoveLast();
        }
        return bitmap;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var bitmap in _cache.Values) bitmap.Dispose();
            _cache.Clear(); _recent.Clear();
            _codec.Dispose(); _data.Dispose();
        }
    }
}
