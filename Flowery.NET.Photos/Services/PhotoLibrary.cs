using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flowery.NET.Photos.Models;
using Avalonia.Threading;

namespace Flowery.NET.Photos.Services
{
    public enum PhotoSourceKind
    {
        All,
        Favorites,
        Album,
        Folder
    }

    public enum PhotoTypeFilter
    {
        All,
        Jpeg,
        Png,
        Other
    }

    public enum PhotoOrientationFilter
    {
        Any,
        Landscape,
        Portrait,
        Square
    }

    /// <summary>
    /// Describes which photos the gallery shows.
    /// </summary>
    public sealed record PhotoQuery
    {
        public PhotoSourceKind Source { get; init; } = PhotoSourceKind.All;
        public string? SourceKey { get; init; }
        public string? SearchText { get; init; }
        public PhotoTypeFilter TypeFilter { get; init; } = PhotoTypeFilter.All;
        public PhotoOrientationFilter OrientationFilter { get; init; } = PhotoOrientationFilter.Any;
        public PhotoSortMode Sort { get; init; } = PhotoSortMode.DateTakenNewest;
    }

    /// <summary>
    /// Owns the photo library: source folders, scanning with a metadata cache, favorites, albums and file operations.
    /// All public collections are only mutated on the UI thread.
    /// </summary>
    public sealed class PhotoLibrary : IAsyncDisposable
    {
        public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static readonly Lazy<PhotoLibrary> _instance = new(() => new PhotoLibrary());
        public static PhotoLibrary Instance => _instance.Value;

        private readonly List<string> _folders = [];
        private readonly List<PhotoAlbum> _albums = [];
        private HashSet<string> _favorites = new(PathComparer);
        private List<PhotoItem> _items = [];
        private Dictionary<string, PhotoItem> _byPath = new(PathComparer);
        private readonly List<FileSystemWatcher> _watchers = [];
        private Dispatcher? _dispatcher;
        private DispatcherTimer? _rescanTimer;
        private readonly List<Task> _scans = [];
        private readonly object _cacheWriteLock = new();
        private bool _disposed;
        private CancellationTokenSource? _scanCts;
        private ConcurrentDictionary<string, MetadataCacheEntry> _cache = new(PathComparer);

        public IReadOnlyList<string> Folders => _folders;
        public IReadOnlyList<PhotoItem> Items => _items;
        public IReadOnlyList<PhotoAlbum> Albums => _albums;
        public bool IsScanning { get; private set; }

        public static string SamplesFolder => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Samples");

        /// <summary>Raised on the UI thread whenever items, folders, favorites or albums change.</summary>
        public event EventHandler? Changed;

        /// <summary>Raised on the UI thread with (processed, total) while scanning.</summary>
        public event EventHandler<(int Done, int Total)>? ScanProgress;

        public event EventHandler<bool>? ScanningChanged;

        private string CachePath => System.IO.Path.Combine(PhotosSettings.StateDirectory, "metadata-cache.json");

        public void Initialize(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _favorites = PhotosSettings.LoadFavorites();

            if (PhotosSettings.HasSavedFolders())
            {
                _folders.AddRange(PhotosSettings.LoadFolders().Where(f => !string.IsNullOrWhiteSpace(f)));
            }
            else
            {
                var pictures = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT"))
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : string.Empty;
                if (!string.IsNullOrWhiteSpace(pictures) && Directory.Exists(pictures))
                    _folders.Add(pictures);
                PhotosSettings.SaveFolders(_folders);
            }

            foreach (var (name, paths) in PhotosSettings.LoadAlbums())
                _albums.Add(new PhotoAlbum(name, paths));

            LoadCache();
            UpdateWatchers();
        }

        #region Scanning

        public Task RescanAsync()
        {
            if (_disposed) return Task.CompletedTask;
            _scans.RemoveAll(task => task.IsCompleted);
            var scan = RescanCoreAsync();
            _scans.Add(scan);
            return scan;
        }

        private async Task RescanCoreAsync()
        {
            if (_dispatcher == null)
                return;

            _scanCts?.Cancel();
            var cts = new CancellationTokenSource();
            _scanCts = cts;
            var token = cts.Token;

            SetScanning(true);
            try
            {
                var folders = _folders.ToList();
                var recurse = PhotosSettings.IncludeSubfolders;
                var favorites = new HashSet<string>(_favorites, PathComparer);
                var result = await Task.Run(() => ScanCore(folders, recurse, favorites, token), token).ConfigureAwait(true);
                if (token.IsCancellationRequested)
                    return;

                foreach (var item in result)
                {
                    item.IsFavorite = _favorites.Contains(item.Path);
                    item.IsSelected = _byPath.GetValueOrDefault(item.Path)?.IsSelected == true;
                }
                _items = result;
                _byPath = result.ToDictionary(i => i.Path, PathComparer);
                Changed?.Invoke(this, EventArgs.Empty);
                await Task.Run(SaveCache, token);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer scan.
            }
            finally
            {
                if (_scanCts == cts)
                {
                    _scanCts = null;
                    SetScanning(false);
                }
                cts.Dispose();
            }
        }

        private void SetScanning(bool value)
        {
            IsScanning = value;
            ScanningChanged?.Invoke(this, value);
        }

        private List<PhotoItem> ScanCore(List<string> folders, bool recurse, HashSet<string> favorites, CancellationToken token)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recurse,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            var files = new HashSet<string>(PathComparer);
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder))
                    continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", options))
                    {
                        token.ThrowIfCancellationRequested();
                        if (ImageMetadataReader.IsSupported(file))
                            files.Add(file);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Skip unreadable folders.
                }
            }

            var list = files.ToList();
            var total = list.Count;
            var done = 0;
            var results = new PhotoItem?[total];
            var lastReport = Environment.TickCount64;

            Parallel.For(0, total, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2), CancellationToken = token }, i =>
            {
                results[i] = CreateItem(list[i], favorites);
                var current = Interlocked.Increment(ref done);
                var now = Environment.TickCount64;
                if (now - Interlocked.Read(ref lastReport) > 150)
                {
                    Interlocked.Exchange(ref lastReport, now);
                    _dispatcher?.Post(() => ScanProgress?.Invoke(this, (current, total)));
                }
            });

            _dispatcher?.Post(() => ScanProgress?.Invoke(this, (total, total)));
            return [.. results.Where(r => r != null).Cast<PhotoItem>()];
        }

        private PhotoItem? CreateItem(string path, HashSet<string> favorites)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return null;

                var item = new PhotoItem(path)
                {
                    FileSize = info.Length,
                    DateModified = info.LastWriteTime,
                    IsFavorite = favorites.Contains(path)
                };

                if (_cache.TryGetValue(path, out var cached) && cached.Size == info.Length && cached.Ticks == info.LastWriteTimeUtc.Ticks)
                {
                    item.PixelWidth = cached.Width;
                    item.PixelHeight = cached.Height;
                    item.DateTaken = cached.TakenTicks > 0 ? new DateTime(cached.TakenTicks) : null;
                    item.CameraModel = cached.Camera;
                    item.Details = cached.Details;
                    return item;
                }

                var probe = ImageMetadataReader.ProbeAsync(path).GetAwaiter().GetResult();
                if (probe is { } p)
                {
                    item.PixelWidth = p.Width;
                    item.PixelHeight = p.Height;
                    item.DateTaken = p.DateTaken;
                    item.CameraModel = p.CameraModel;
                    item.Details = p.Details;
                }

                _cache[path] = new MetadataCacheEntry
                {
                    Size = info.Length,
                    Ticks = info.LastWriteTimeUtc.Ticks,
                    Width = item.PixelWidth,
                    Height = item.PixelHeight,
                    TakenTicks = item.DateTaken?.Ticks ?? 0,
                    Camera = item.CameraModel,
                    Details = item.Details
                };
                return item;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates an item for a file outside the library (e.g. opened from the command line or dropped).
        /// </summary>
        public PhotoItem? GetOrCreateItem(string path)
        {
            if (_byPath.TryGetValue(path, out var existing))
                return existing;
            return CreateItem(path, _favorites);
        }

        public PhotoItem? FindByPath(string path) => _byPath.GetValueOrDefault(path);

        /// <summary>
        /// Re-reads metadata for a file after it changed (e.g. edited and saved) and returns the refreshed item.
        /// </summary>
        public PhotoItem? Refresh(string path)
        {
            _cache.TryRemove(path, out _);
            var fresh = CreateItem(path, _favorites);
            if (fresh == null)
                return null;

            if (_byPath.TryGetValue(path, out var existing))
            {
                existing.FileSize = fresh.FileSize;
                existing.DateModified = fresh.DateModified;
                existing.PixelWidth = fresh.PixelWidth;
                existing.PixelHeight = fresh.PixelHeight;
                existing.DateTaken = fresh.DateTaken;
                existing.CameraModel = fresh.CameraModel;
                existing.Details = fresh.Details;
                existing.Revision++;
                Changed?.Invoke(this, EventArgs.Empty);
                return existing;
            }

            if (IsInLibrary(path))
            {
                _items.Add(fresh);
                _byPath[path] = fresh;
                Changed?.Invoke(this, EventArgs.Empty);
            }
            return fresh;
        }

        public bool IsInLibrary(string path) => _folders.Any(folder => IsWithin(folder, path));

        public static bool IsWithin(string folder, string path)
        {
            var relative = Path.GetRelativePath(folder, path);
            return !Path.IsPathRooted(relative) && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        #endregion

        #region Folders

        public bool AddFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return false;
            folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (_folders.Any(f => PathComparer.Equals(Path.TrimEndingDirectorySeparator(f), folder)))
                return false;
            _folders.Add(folder);
            PhotosSettings.SaveFolders(_folders);
            UpdateWatchers();
            _ = RescanAsync();
            return true;
        }

        public void RemoveFolder(string folder)
        {
            if (_folders.RemoveAll(f => string.Equals(f, folder, PathComparison)) == 0)
                return;
            PhotosSettings.SaveFolders(_folders);
            UpdateWatchers();
            _ = RescanAsync();
        }

        /// <summary>
        /// Restores a previous folder configuration (used when the settings dialog is cancelled).
        /// </summary>
        public void RestoreFolders(IReadOnlyList<string> folders, bool includeSubfolders)
        {
            var sameFolders = folders.Count == _folders.Count
                && folders.Zip(_folders).All(p => string.Equals(p.First, p.Second, PathComparison));
            if (sameFolders && includeSubfolders == PhotosSettings.IncludeSubfolders)
                return;

            _folders.Clear();
            _folders.AddRange(folders);
            PhotosSettings.SaveFolders(_folders);
            PhotosSettings.IncludeSubfolders = includeSubfolders;
            PhotosSettings.SaveOptions();
            UpdateWatchers();
            _ = RescanAsync();
        }

        public List<PhotoFolderInfo> GetFolderSummaries()
        {
            var roots = _folders.Select(Path.TrimEndingDirectorySeparator).ToList();
            return [.. _items
                .GroupBy(i => i.FolderPath, PathComparer)
                .Select(g =>
                {
                    var cover = g.OrderByDescending(i => i.SortDate).First();
                    var isRoot = roots.Contains(Path.TrimEndingDirectorySeparator(g.Key), PathComparer);
                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(g.Key));
                    return new PhotoFolderInfo(g.Key, string.IsNullOrEmpty(name) ? g.Key : name, g.Count(), cover, isRoot);
                })
                .OrderByDescending(f => f.IsRoot)
                .ThenBy(f => f.Path, PathComparer)];
        }

        private void UpdateWatchers()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();

            foreach (var folder in _folders)
            {
                if (!Directory.Exists(folder))
                    continue;
                try
                {
                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = PhotosSettings.IncludeSubfolders,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
                    };
                    watcher.Created += OnWatcherChanged;
                    watcher.Deleted += OnWatcherChanged;
                    watcher.Renamed += OnWatcherChanged;
                    watcher.Changed += OnWatcherChanged;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch
                {
                    // Watching is best-effort.
                }
            }
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            if (!ImageMetadataReader.IsSupported(e.FullPath) && Path.HasExtension(e.FullPath)
                && !Directory.Exists(e.FullPath) && e.ChangeType != WatcherChangeTypes.Renamed)
                return;

            _dispatcher?.Post(() =>
            {
                if (_disposed) return;
                if (_rescanTimer == null && _dispatcher != null)
                {
                    _rescanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    _rescanTimer.Tick += (_, _) => { _rescanTimer.Stop(); _ = RescanAsync(); };
                }
                _rescanTimer?.Stop();
                _rescanTimer?.Start();
            });
        }

        public void ApplyIncludeSubfolders(bool include)
        {
            if (PhotosSettings.IncludeSubfolders == include)
                return;
            PhotosSettings.IncludeSubfolders = include;
            PhotosSettings.SaveOptions();
            UpdateWatchers();
            _ = RescanAsync();
        }

        #endregion

        #region Favorites

        public void SetFavorite(IEnumerable<PhotoItem> items, bool favorite)
        {
            foreach (var item in items)
            {
                item.IsFavorite = favorite;
                if (favorite)
                    _favorites.Add(item.Path);
                else
                    _favorites.Remove(item.Path);
            }
            PhotosSettings.SaveFavorites(_favorites);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleFavorite(PhotoItem item) => SetFavorite([item], !item.IsFavorite);

        #endregion

        #region Albums

        public PhotoAlbum? FindAlbum(string? name) =>
            name == null ? null : _albums.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.CurrentCultureIgnoreCase));

        public PhotoAlbum CreateAlbum(string name, IEnumerable<PhotoItem>? items = null)
        {
            var baseName = string.IsNullOrWhiteSpace(name) ? "Album" : name.Trim();
            var unique = baseName;
            var counter = 2;
            while (FindAlbum(unique) != null)
                unique = $"{baseName} ({counter++})";

            var album = new PhotoAlbum(unique);
            if (items != null)
                album.Paths.AddRange(items.Select(i => i.Path).Distinct(PathComparer));
            _albums.Add(album);
            SaveAlbums();
            return album;
        }

        public int AddToAlbum(PhotoAlbum album, IEnumerable<PhotoItem> items)
        {
            var added = 0;
            foreach (var item in items)
            {
                if (album.Paths.Contains(item.Path, PathComparer))
                    continue;
                album.Paths.Add(item.Path);
                added++;
            }
            SaveAlbums();
            return added;
        }

        public void RemoveFromAlbum(PhotoAlbum album, IEnumerable<PhotoItem> items)
        {
            var remove = new HashSet<string>(items.Select(i => i.Path), PathComparer);
            album.Paths.RemoveAll(remove.Contains);
            SaveAlbums();
        }

        public bool RenameAlbum(PhotoAlbum album, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return false;
            var existing = FindAlbum(newName.Trim());
            if (existing != null && existing != album)
                return false;
            album.Name = newName.Trim();
            SaveAlbums();
            return true;
        }

        public void DeleteAlbum(PhotoAlbum album)
        {
            _albums.Remove(album);
            SaveAlbums();
        }

        public IEnumerable<PhotoItem> GetAlbumItems(PhotoAlbum album) =>
            album.Paths.Select(p => _byPath.GetValueOrDefault(p)).Where(i => i != null).Cast<PhotoItem>();

        private void SaveAlbums()
        {
            PhotosSettings.SaveAlbums(_albums.Select(a => (a.Name, (IEnumerable<string>)a.Paths)));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region File operations

        /// <summary>
        /// Moves the given photos to the Windows Recycle Bin. Returns the number of deleted files.
        /// </summary>
        public async Task<int> DeleteAsync(IReadOnlyList<PhotoItem> items)
        {
            var paths = items.Select(i => i.Path).ToList();
            var deleted = await Task.Run(() =>
            {
                List<string> ok = [];
                foreach (var path in paths)
                {
                    if (FileOperations.MoveToRecycleBin(path))
                        ok.Add(path);
                }
                return ok;
            }).ConfigureAwait(true);

            if (deleted.Count == 0)
                return 0;

            var set = new HashSet<string>(deleted, PathComparer);
            _items = [.. _items.Where(i => !set.Contains(i.Path))];
            foreach (var path in deleted)
            {
                _byPath.Remove(path);
                _favorites.Remove(path);
                _cache.TryRemove(path, out _);
            }
            foreach (var album in _albums)
                album.Paths.RemoveAll(set.Contains);

            PhotosSettings.SaveFavorites(_favorites);
            PhotosSettings.SaveAlbums(_albums.Select(a => (a.Name, (IEnumerable<string>)a.Paths)));
            Changed?.Invoke(this, EventArgs.Empty);
            return deleted.Count;
        }

        /// <summary>
        /// Copies external files into the library's import folder ("Pictures\Flowery Photos\Imported yyyy-MM-dd").
        /// </summary>
        public async Task<int> ImportFilesAsync(IEnumerable<string> files)
        {
            var root = _folders.FirstOrDefault(Directory.Exists)
                ?? (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT"))
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : PhotosSettings.StateDirectory);
            var target = System.IO.Path.Combine(root, "Flowery Photos", "Imported " + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            var list = files.Where(ImageMetadataReader.IsSupported).ToList();
            var imported = await Task.Run(() =>
            {
                var count = 0;
                Directory.CreateDirectory(target);
                foreach (var file in list)
                {
                    try
                    {
                        if (IsInLibrary(file))
                            continue;
                        var dest = FileOperations.GetUniquePath(System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)));
                        File.Copy(file, dest);
                        File.SetLastWriteTime(dest, File.GetLastWriteTime(file));
                        count++;
                    }
                    catch
                    {
                        // Skip files that cannot be copied.
                    }
                }
                return count;
            }).ConfigureAwait(true);

            if (!IsInLibrary(target) || !PhotosSettings.IncludeSubfolders)
            {
                AddFolder(target);
            }
            else
            {
                await RescanAsync();
            }
            return imported;
        }

        #endregion

        #region Query

        public List<PhotoItem> Query(PhotoQuery query)
        {
            IEnumerable<PhotoItem> source = query.Source switch
            {
                PhotoSourceKind.Favorites => _items.Where(i => i.IsFavorite),
                PhotoSourceKind.Album => FindAlbum(query.SourceKey) is { } album ? GetAlbumItems(album) : [],
                PhotoSourceKind.Folder => _items.Where(i => string.Equals(i.FolderPath, query.SourceKey, PathComparison)),
                _ => _items
            };

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var terms = query.SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                source = source.Where(i => terms.All(t => Matches(i, t)));
            }

            source = query.TypeFilter switch
            {
                PhotoTypeFilter.Jpeg => source.Where(i => i.Extension is "JPG" or "JPEG" or "JPE" or "JFIF"),
                PhotoTypeFilter.Png => source.Where(i => i.Extension == "PNG"),
                PhotoTypeFilter.Other => source.Where(i => i.Extension is not ("JPG" or "JPEG" or "JPE" or "JFIF" or "PNG")),
                _ => source
            };

            source = query.OrientationFilter switch
            {
                PhotoOrientationFilter.Landscape => source.Where(i => i.AspectRatio > 1.05),
                PhotoOrientationFilter.Portrait => source.Where(i => i.AspectRatio < 0.95),
                PhotoOrientationFilter.Square => source.Where(i => i.AspectRatio is >= 0.95 and <= 1.05),
                _ => source
            };

            // Albums keep their manual order unless a specific sort is requested.
            return query.Sort switch
            {
                PhotoSortMode.DateTakenOldest => [.. source.OrderBy(i => i.SortDate)],
                PhotoSortMode.DateModifiedNewest => [.. source.OrderByDescending(i => i.DateModified)],
                PhotoSortMode.NameAscending => [.. source.OrderBy(i => i.FileName, StringComparer.CurrentCultureIgnoreCase)],
                PhotoSortMode.NameDescending => [.. source.OrderByDescending(i => i.FileName, StringComparer.CurrentCultureIgnoreCase)],
                PhotoSortMode.SizeLargest => [.. source.OrderByDescending(i => i.FileSize)],
                _ => [.. source.OrderByDescending(i => i.SortDate)]
            };
        }

        private static bool Matches(PhotoItem item, string term)
        {
            if (item.FileName.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                return true;
            if (item.FolderPath.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                return true;
            if (item.CameraModel?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true)
                return true;
            if (item.Extension.Equals(term.TrimStart('.'), StringComparison.OrdinalIgnoreCase))
                return true;

            var culture = Localization.PhotosLocalization.CurrentCulture;
            var date = item.SortDate;
            return date.Year.ToString(CultureInfo.InvariantCulture) == term
                || date.ToString("MMMM", culture).Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || date.ToString("MMMM", CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Metadata cache

        private void LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath))
                    return;
                var json = File.ReadAllText(CachePath);
                var data = JsonSerializer.Deserialize(json, PhotosJsonContext.Default.DictionaryStringMetadataCacheEntry);
                if (data != null)
                    _cache = new ConcurrentDictionary<string, MetadataCacheEntry>(data, PathComparer);
            }
            catch
            {
                _cache = new ConcurrentDictionary<string, MetadataCacheEntry>(PathComparer);
            }
        }

        private void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(PhotosSettings.StateDirectory);
                var snapshot = new Dictionary<string, MetadataCacheEntry>(_cache, PathComparer);
                lock (_cacheWriteLock)
                {
                    var temporary = CachePath + ".tmp";
                    File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, PhotosJsonContext.Default.DictionaryStringMetadataCacheEntry));
                    File.Move(temporary, CachePath, true);
                }
            }
            catch
            {
                // Cache is optional.
            }
        }

        public void ClearCache()
        {
            _cache.Clear();
            try
            {
                SaveCache();
            }
            catch
            {
                // Ignore.
            }
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            _disposed = true;
            _rescanTimer?.Stop();
            _scanCts?.Cancel();
            foreach (var watcher in _watchers) watcher.Dispose();
            _watchers.Clear();
            await Task.WhenAll(_scans);
            _scans.Clear();
        }
    }

    public sealed class MetadataCacheEntry
    {
        public long Size { get; set; }
        public long Ticks { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long TakenTicks { get; set; }
        public string? Camera { get; set; }
        public PhotoDetails? Details { get; set; }
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
    [JsonSerializable(typeof(Dictionary<string, MetadataCacheEntry>))]
    internal partial class PhotosJsonContext : JsonSerializerContext
    {
    }
}
