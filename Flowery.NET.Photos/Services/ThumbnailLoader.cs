using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Flowery.NET.Photos.Models;
using SkiaSharp;

namespace Flowery.NET.Photos.Services;

/// <summary>
/// Thumbnails from memory, then from the cache on disk, then from their files. Files are read a few at a time, the
/// thumbnail nearest the top of the screen first. Videos have places of their own: their first still, above all on a
/// network drive, takes far longer than a photo. Each realized view owns and disposes its bitmap. Call on the UI thread.
/// </summary>
public static class ThumbnailLoader
{
    private const int MaxEntries = 300;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, LinkedListNode<(string Key, byte[] Data)>> Cache = [];
    private static readonly LinkedList<(string Key, byte[] Data)> Lru = new();
    internal static readonly Places PhotoPlaces = new(4);
    internal static readonly Places VideoPlaces = new(8);

    /// <param name="item">The photo or video.</param>
    /// <param name="height">The height the thumbnail shows at.</param>
    /// <param name="token">Cancels the load, for example when the view leaves the screen.</param>
    /// <param name="view">The view that shows the thumbnail; the higher on the screen, the sooner it loads.</param>
    /// <param name="started">Called on the UI thread when the file is read, after waiting for a place; not for a cached thumbnail.</param>
    public static async Task<Bitmap?> LoadAsync(PhotoItem item, double height, CancellationToken token, Visual? view = null, Action? started = null)
    {
        var edge = height <= 200 ? 400 : height <= 350 ? 700 : 1000;
        var key = $"{item.Path}|{item.Revision}|{item.DateModified.Ticks}|{item.FileSize}|{edge}";
        var cached = FromMemory(key) ?? await Task.Run(() => ThumbnailDiskCache.Read(key), token);
        if (cached != null && Decode(cached) is { } fromCache)
        {
            Remember(key, cached);
            return fromCache;
        }
        // A cached thumbnail that does not decode, cut short by a crash or a full disk, is made again.
        if (cached != null) ThumbnailDiskCache.Forget(key);

        var places = item.IsVideo ? VideoPlaces : PhotoPlaces;
        await places.EnterAsync(view, token);
        started?.Invoke();
        byte[] data;
        try
        {
            data = await Task.Run(() =>
            {
                using var bitmap = PhotoDecoder.Load(item.Path, edge, token);
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, 85);
                var bytes = encoded.ToArray();
                ThumbnailDiskCache.Write(key, bytes);
                return bytes;
            }, token);
        }
        // A thumbnail that cannot be made leaves its tile empty; it never takes the app down.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
        finally { places.Leave(); }
        Remember(key, data);
        token.ThrowIfCancellationRequested();
        return Decode(data);
    }

    private static Bitmap? Decode(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    /// <summary>Forgets the thumbnails in memory, so the next ones come from the disk cache, as after a restart.</summary>
    internal static void ClearMemory()
    {
        lock (Sync) { Cache.Clear(); Lru.Clear(); }
    }

    private static byte[]? FromMemory(string key)
    {
        lock (Sync)
        {
            if (!Cache.TryGetValue(key, out var entry)) return null;
            Lru.Remove(entry);
            Lru.AddFirst(entry);
            return entry.Value.Data;
        }
    }

    private static void Remember(string key, byte[] data)
    {
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

    /// <summary>
    /// A number of places for reading files, given to the waiting view highest on the screen when one frees up, on the
    /// UI thread, where the views tell their place.
    /// </summary>
    internal sealed class Places(int count)
    {
        private readonly List<Waiter> _waiting = [];
        private int _free = count;
        private long _arrivals;

        public int Count { get; } = count;
        public int Waiting => _waiting.Count;

        public Task EnterAsync(Visual? view, CancellationToken token)
        {
            Dispatcher.UIThread.VerifyAccess();
            token.ThrowIfCancellationRequested();
            if (_free > 0) { _free--; return Task.CompletedTask; }
            var waiter = new Waiter(view, _arrivals++);
            _waiting.Add(waiter);
            waiter.Cancellation = token.Register(() => Dispatcher.UIThread.Post(() =>
            {
                if (_waiting.Remove(waiter)) waiter.Ready.TrySetCanceled(token);
            }));
            return waiter.Ready.Task;
        }

        public void Leave()
        {
            Dispatcher.UIThread.VerifyAccess();
            if (_waiting.Count == 0) { _free++; return; }
            var next = _waiting.MinBy(waiter => (ScreenOrder(waiter.View), waiter.Arrival))!;
            _waiting.Remove(next);
            next.Cancellation.Dispose();
            next.Ready.TrySetResult();
        }

        private sealed class Waiter(Visual? view, long arrival)
        {
            public Visual? View { get; } = view;
            public long Arrival { get; } = arrival;
            public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public CancellationTokenRegistration Cancellation { get; set; }
        }
    }

    // On screen first, from the top left; then by the distance to the screen; views not shown come last.
    private static double ScreenOrder(Visual? view)
    {
        if (view is not { IsEffectivelyVisible: true } || TopLevel.GetTopLevel(view) is not { } top
            || view.TranslatePoint(default, top) is not { } at) return double.MaxValue;
        var away = Math.Max(at.Y - top.Bounds.Height, -(at.Y + view.Bounds.Height));
        return away > 0 ? 1e12 + away : at.Y * 1e5 + at.X;
    }
}
