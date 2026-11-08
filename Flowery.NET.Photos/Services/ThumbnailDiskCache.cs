using System.Security.Cryptography;
using System.Text;

namespace Flowery.NET.Photos.Services;

/// <summary>
/// Encoded thumbnails in the app's data folder, so a thumbnail is made from its file once, which matters most for
/// videos and for files on a network drive. The least recently used ones go when the cache grows past its size.
/// </summary>
internal static class ThumbnailDiskCache
{
    internal const long MaxBytes = 512L * 1024 * 1024;
    /// <summary>How much is written between two prunes; the first write of a session prunes as well.</summary>
    internal const long PruneEvery = MaxBytes / 8;
    private static long _sincePrune = PruneEvery;

    internal static string Folder => Path.Combine(PhotosSettings.StateDirectory, "thumbnails");

    private static string FileOf(string key) =>
        Path.Combine(Folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32] + ".webp");

    /// <summary>The cached thumbnail, or null; a hit counts as a use.</summary>
    public static byte[]? Read(string key)
    {
        var path = FileOf(key);
        try
        {
            var data = File.ReadAllBytes(path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Drops a thumbnail, for example one that no longer decodes.</summary>
    public static void Forget(string key)
    {
        try { File.Delete(FileOf(key)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static void Write(string key, byte[] data)
    {
        var path = FileOf(key);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temporary); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            return;
        }
        // After the first new thumbnail, so starting the app reads no folder of thumbnails, and again after every
        // PruneEvery bytes, so a long session over a large library stays within the size. Only one writer prunes.
        if (Interlocked.Add(ref _sincePrune, data.Length) >= PruneEvery && Interlocked.Exchange(ref _sincePrune, 0) >= PruneEvery)
            _ = Task.Run(() => Prune(MaxBytes));
    }

    /// <summary>
    /// Removes the least recently used thumbnails until the cache fits in the given size, and the halves of writes that
    /// a crash cut short.
    /// </summary>
    internal static void Prune(long maxBytes)
    {
        try
        {
            var folder = new DirectoryInfo(Folder);
            // A write in progress is younger than an hour.
            var hourAgo = DateTime.UtcNow.AddHours(-1);
            foreach (var stale in folder.GetFiles("*.tmp"))
                if (stale.LastWriteTimeUtc < hourAgo) Delete(stale);
            long total = 0;
            foreach (var file in folder.GetFiles("*.webp").OrderByDescending(file => file.LastWriteTimeUtc))
                if ((total += file.Length) > maxBytes) Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static void Delete(FileInfo file)
    {
        try { file.Delete(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
