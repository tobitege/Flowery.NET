using Avalonia.Media.Imaging;
using Flowery.NET.Photos.Models;
using SkiaSharp;

namespace Flowery.NET.Photos.Services;

/// <summary>Cache encoded thumbnails; each realized tile owns and disposes its bitmap.</summary>
public static class ThumbnailLoader
{
    private const int MaxEntries = 300;
    private static readonly SemaphoreSlim Gate = new(4);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, LinkedListNode<(string Key, byte[] Data)>> Cache = [];
    private static readonly LinkedList<(string Key, byte[] Data)> Lru = new();

    public static async Task<Bitmap?> LoadAsync(PhotoItem item, double height, CancellationToken token)
    {
        var edge = height <= 200 ? 400 : height <= 350 ? 700 : 1000;
        var key = $"{item.Path}|{item.Revision}|{item.DateModified.Ticks}|{edge}";
        byte[]? data = null;
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out var entry))
            {
                Lru.Remove(entry);
                Lru.AddFirst(entry);
                data = entry.Value.Data;
            }
        }
        if (data == null)
        {
            await Gate.WaitAsync(token);
            try
            {
                data = await Task.Run(() =>
                {
                    using var bitmap = PhotoDecoder.Load(item.Path, edge);
                    using var image = SKImage.FromBitmap(bitmap);
                    using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
                    return encoded.ToArray();
                }, token);
                token.ThrowIfCancellationRequested();
                lock (Sync)
                {
                    if (Cache.Remove(key, out var old)) Lru.Remove(old);
                    Cache[key] = Lru.AddFirst((key, data));
                    while (Lru.Count > MaxEntries && Lru.Last is { } last)
                    {
                        Cache.Remove(last.Value.Key);
                        Lru.RemoveLast();
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return null;
            }
            finally { Gate.Release(); }
        }
        token.ThrowIfCancellationRequested();
        using var stream = new MemoryStream(data);
        return new Bitmap(stream);
    }
}
