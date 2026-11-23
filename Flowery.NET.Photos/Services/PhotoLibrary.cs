using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
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
        Folder,
        /// <summary>A folder outside the library, viewed without adding it.</summary>
        Browsed
    }

    public enum PhotoTypeFilter
    {
        All,
        Jpeg,
        Png,
        Other,
        Video
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
        private readonly Dictionary<string, FolderLocation> _libraryLocations = new(PathComparer);
        private Dictionary<string, FolderLocation> _browseLocations = new(PathComparer);
        private HiddenFolderStore? _hiddenFolderStore;
        private HashSet<string>? _hiddenFolders = new(PathComparer);
        private HashSet<string> _favorites = new(PathComparer);
        private List<PhotoItem> _items = [];
        private Dictionary<string, PhotoItem> _byPath = new(PathComparer);
        // The photos of a browsed folder that the library does not read; a folder it reads shows its library photos.
        private List<PhotoItem> _browsed = [];
        private ConcurrentDictionary<string, MetadataCacheEntry> _browseCache = new(PathComparer);
        private bool _browseRecurse;
        private bool _browseInLibrary;
        private int _browseVersion;
        private Dispatcher? _dispatcher;
        private readonly List<Task> _scans = [];
        private readonly object _cacheWriteLock = new();
        private bool _disposed;
        private CancellationTokenSource? _scanCts;
        private Task<Exception?>? _activeScan;
        private ConcurrentDictionary<string, MetadataCacheEntry> _cache = new(PathComparer);

        public IReadOnlyList<string> Folders => _folders;
        public IReadOnlyList<PhotoItem> Items => _items;
        internal IEnumerable<PhotoItem> VisibleLibraryItems => GetVisibleItems(_items);
        public IReadOnlyList<PhotoAlbum> Albums => _albums;
        public bool IsScanning { get; private set; }
        internal Exception? HiddenFoldersError { get; private set; }
        internal bool RequiresResolvedMedia => _hiddenFolders?.Count != 0;

        public static string SamplesFolder => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Samples");

        /// <summary>Raised on the UI thread whenever items, folders, favorites or albums change.</summary>
        public event EventHandler? Changed;
        internal event EventHandler? HiddenFoldersChanged;

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

            _hiddenFolderStore = new HiddenFolderStore(PhotosSettings.StateDirectory);
            SyncHiddenFolders();
            LoadCache();
        }

        #region Scanning

        public Task RescanAsync() => StartScan(null, false, default);

        private Task<Exception?> StartScan(string? folder, bool recurse, CancellationToken token)
        {
            if (_disposed || token.IsCancellationRequested) return Task.FromResult<Exception?>(null);
            _scans.RemoveAll(task => task.IsCompleted);
            var scan = _activeScan = RescanCoreAsync(folder, recurse, token);
            _scans.Add(scan);
            return scan;
        }

        private async Task RefreshLibraryAsync(string? folder, bool recurse, CancellationToken token)
        {
            if (folder != null)
            {
                // Let the active scan finish and then read only the requested folder.
                while (_activeScan is { IsCompleted: false } active)
                    await active.WaitAsync(token);
                if (!ScansFolder(folder)) return;
                recurse &= PhotosSettings.IncludeSubfolders;
            }
            if (await StartScan(folder, recurse, token) is { } error)
                throw error;
        }

        private async Task<Exception?> RescanCoreAsync(string? folder, bool recurse, CancellationToken cancellationToken)
        {
            if (_dispatcher == null)
                return null;

            _scanCts?.Cancel();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _scanCts = cts;
            var token = cts.Token;

            SetScanning(true);
            try
            {
                var folders = folder == null ? _folders.ToList() : [folder];
                var includeSubfolders = folder == null ? PhotosSettings.IncludeSubfolders : recurse;
                var favorites = new HashSet<string>(_favorites, PathComparer);
                var (items, locations, failures) = await Task.Run(
                    () => ScanCore(folders, includeSubfolders, favorites, _cache, token), token).ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
                var error = failures.Count == 0 ? null : failures[0].Error;

                foreach (var item in items)
                {
                    item.IsFavorite = _favorites.Contains(item.Path);
                    item.IsSelected = _byPath.GetValueOrDefault(item.Path)?.IsSelected == true;
                }
                var byPath = items.ToDictionary(item => item.Path, PathComparer);
                foreach (var previous in _items)
                {
                    if (folder != null && !MatchesFolder(folder, previous.FolderPath, recurse) ||
                        IsFailedFolder(previous.FolderPath, failures, includeSubfolders))
                        byPath.TryAdd(previous.Path, previous);
                }
                _items = [.. byPath.Values];
                _byPath = byPath;
                foreach (var previous in _libraryLocations.Keys.ToArray())
                {
                    if ((folder == null || MatchesFolder(folder, previous, recurse)) &&
                        !IsFailedFolder(previous, failures, includeSubfolders))
                        _libraryLocations.Remove(previous);
                }
                foreach (var (path, location) in locations) _libraryLocations[path] = location;
                Changed?.Invoke(this, EventArgs.Empty);
                await Task.Run(SaveCache, token);
                return error;
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer scan or canceled when its folder view closed.
                return null;
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

        private static bool IsFailedFolder(string folder, List<(string Folder, Exception Error)> failures, bool recurse) =>
            failures.Any(failure => MatchesFolder(failure.Folder, folder, recurse));

        private (List<PhotoItem> Items, Dictionary<string, FolderLocation> Locations, List<(string Folder, Exception Error)> Failures) ScanCore(
            List<string> folders, bool recurse, HashSet<string> favorites, ConcurrentDictionary<string, MetadataCacheEntry> cache,
            CancellationToken token)
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            var files = new Dictionary<string, (FolderLocation? Location, bool Link)>(PathComparer);
            var locations = new Dictionary<string, FolderLocation>(PathComparer);
            var failures = new List<(string Folder, Exception Error)>();
            var pending = new Stack<string>(folders);
            var visited = new HashSet<string>(PathComparer);
            while (pending.TryPop(out var folder))
            {
                token.ThrowIfCancellationRequested();
                if (!visited.Add(folder)) continue;
                var location = FolderIdentity.ReadFolder(folder);
                if (location != null) locations.Add(folder, location);
                // A directory link back to one of this path's ancestors would repeat the tree.
                if (location?.ResolvedParents.Contains(location.Path, PathComparer) == true) continue;

                try
                {
                    var entries = new FileSystemEnumerable<(string Path, bool IsDirectory, bool Link)>(folder,
                        static (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory,
                            entry.Attributes.HasFlag(FileAttributes.ReparsePoint)), options);
                    var inFolder = new List<(string Path, bool Link)>();
                    var children = new List<string>();
                    foreach (var entry in entries)
                    {
                        token.ThrowIfCancellationRequested();
                        if (entry.IsDirectory)
                        {
                            if (recurse) children.Add(entry.Path);
                        }
                        else if (ImageMetadataReader.IsSupported(entry.Path)) inFolder.Add((entry.Path, entry.Link));
                    }
                    foreach (var child in children) pending.Push(child);
                    foreach (var (path, link) in inFolder) files.TryAdd(path, (location, link));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failures.Add((folder, ex));
                }
            }

            var list = files.ToArray();
            var total = list.Length;
            var done = 0;
            var results = new PhotoItem?[total];
            var lastReport = Environment.TickCount64;

            Parallel.For(0, total, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2), CancellationToken = token }, i =>
            {
                var (path, (location, link)) = list[i];
                results[i] = CreateItem(path, favorites, cache, location, link);
                var current = Interlocked.Increment(ref done);
                var now = Environment.TickCount64;
                if (now - Interlocked.Read(ref lastReport) > 150)
                {
                    Interlocked.Exchange(ref lastReport, now);
                    _dispatcher?.Post(() => ScanProgress?.Invoke(this, (current, total)));
                }
            });

            _dispatcher?.Post(() => ScanProgress?.Invoke(this, (total, total)));
            return ([.. results.OfType<PhotoItem>()], locations, failures);
        }

        private static PhotoItem? CreateItem(string path, HashSet<string> favorites, ConcurrentDictionary<string, MetadataCacheEntry> cache,
            FolderLocation? location, bool link)
        {
            try
            {
                var resolved = link ? FolderIdentity.TryResolve(path) : location == null ? null : Path.Combine(location.Path, Path.GetFileName(path));
                var source = resolved ?? path;
                var info = new FileInfo(source);
                if (!info.Exists)
                    return null;

                var item = new PhotoItem(path)
                {
                    Location = location, ResolvedPath = resolved,
                    FileSize = info.Length,
                    DateModified = info.LastWriteTime,
                    IsFavorite = favorites.Contains(path)
                };

                if (cache.TryGetValue(source, out var cached) && cached.Size == info.Length && cached.Ticks == info.LastWriteTimeUtc.Ticks
                    && cached.Version == MetadataCacheEntry.CurrentVersion)
                {
                    item.PixelWidth = cached.Width;
                    item.PixelHeight = cached.Height;
                    item.DateTaken = cached.TakenTicks > 0 ? new DateTime(cached.TakenTicks) : null;
                    item.CameraModel = cached.Camera;
                    item.Details = cached.Details;
                    return item;
                }

                var probe = ImageMetadataReader.ProbeAsync(source).GetAwaiter().GetResult();
                if (probe is { } p)
                {
                    item.PixelWidth = p.Width;
                    item.PixelHeight = p.Height;
                    item.DateTaken = p.DateTaken;
                    item.CameraModel = p.CameraModel;
                    item.Details = p.Details;
                }

                // A failed probe can be a file that is still being written.
                if (probe == null) return item;
                cache[source] = new MetadataCacheEntry
                {
                    Version = MetadataCacheEntry.CurrentVersion,
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
        internal Task<List<PhotoItem>> GetOpenItemsAsync(IReadOnlyList<string> paths)
        {
            var favorites = new HashSet<string>(_favorites, PathComparer);
            var requests = paths.Select(path => (Path: path, Cache: CacheFor(path), Item: _byPath.GetValueOrDefault(path))).ToArray();
            return Task.Run(() => requests.Select(request =>
            {
                if (request.Item is { ResolvedPath: { } source } item &&
                    PathComparer.Equals(source, FolderIdentity.TryResolve(request.Path))) return item;
                return CreateItem(request.Path, favorites, request.Cache,
                    FolderIdentity.ReadFolder(Path.GetDirectoryName(request.Path) ?? ""), link: true);
            }).OfType<PhotoItem>().ToList());
        }

        private ConcurrentDictionary<string, MetadataCacheEntry> CacheFor(string path) =>
            IsBrowsed(path) && !_browseInLibrary ? _browseCache : _cache;

        public PhotoItem? FindByPath(string path) => _byPath.GetValueOrDefault(path);

        /// <summary>
        /// Re-reads metadata for a file after it changed (e.g. edited and saved) and returns the refreshed item.
        /// </summary>
        public async Task<PhotoItem?> RefreshMediaAsync(string path)
        {
            var cache = CacheFor(path);
            var favorites = new HashSet<string>(_favorites, PathComparer);
            var fresh = await Task.Run(() =>
            {
                if (FolderIdentity.TryResolve(path) is { } source) cache.TryRemove(source, out _);
                return CreateItem(path, favorites, cache, FolderIdentity.ReadFolder(Path.GetDirectoryName(path) ?? ""), link: true);
            });
            if (fresh == null)
                return null;
            fresh.IsFavorite = _favorites.Contains(path);

            if ((_byPath.GetValueOrDefault(path) ?? _browsed.Find(item => PathComparer.Equals(item.Path, path))) is { } existing)
            {
                existing.FileSize = fresh.FileSize;
                existing.DateModified = fresh.DateModified;
                existing.PixelWidth = fresh.PixelWidth;
                existing.PixelHeight = fresh.PixelHeight;
                existing.DateTaken = fresh.DateTaken;
                existing.CameraModel = fresh.CameraModel;
                existing.Details = fresh.Details;
                existing.Location = fresh.Location;
                existing.ResolvedPath = fresh.ResolvedPath;
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
            // A copy saved next to a browsed photo shows in the browsed folder.
            else if (IsBrowsed(path))
            {
                _browsed.Add(fresh);
                Changed?.Invoke(this, EventArgs.Empty);
            }
            return fresh;
        }

        public bool IsInLibrary(string path) => _folders.Any(folder => IsWithin(folder, path));

        /// <summary>True when the library reads a folder's photos: a source folder, or one inside it while subfolders count.</summary>
        public bool ScansFolder(string folder)
        {
            folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            return _folders.Any(root => PathComparer.Equals(root, folder) || PhotosSettings.IncludeSubfolders && IsWithin(root, folder));
        }

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
            _ = RescanAsync();
            return true;
        }

        /// <summary>The folder the gallery shows as <see cref="PhotoSourceKind.Browsed"/>.</summary>
        public string? BrowsedFolder { get; private set; }

        /// <summary>
        /// Prepares a folder for viewing without adding it to the library: its photos and videos, with those of its
        /// subfolders when the library includes subfolders. A folder the library reads shows its library photos; any other
        /// is read here without being saved. The next folder browsed replaces it.
        /// </summary>
        public Task<Exception?> BrowseAsync(string folder, CancellationToken token = default) =>
            ReadBrowsedFolderAsync(folder, preserveSelection: false, token);

        internal async Task RefreshAsync(PhotoQuery? query, CancellationToken token)
        {
            if (query is { Source: PhotoSourceKind.Browsed, SourceKey: { } browsed })
            {
                if (PathComparer.Equals(BrowsedFolder, browsed) &&
                    await ReadBrowsedFolderAsync(browsed, preserveSelection: true, token) is { } error)
                    throw error;
            }
            else if (query is { Source: PhotoSourceKind.Folder, SourceKey: { } folder })
            {
                if (ScansFolder(folder)) await RefreshLibraryAsync(folder, false, token);
            }
            else
                await RefreshLibraryAsync(null, false, token);
        }

        private async Task<Exception?> ReadBrowsedFolderAsync(string folder, bool preserveSelection, CancellationToken token)
        {
            if (_disposed) return null;
            folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            var version = ++_browseVersion;
            var recurse = PhotosSettings.IncludeSubfolders;
            var inLibrary = ScansFolder(folder);
            var sameBrowse = PathComparer.Equals(BrowsedFolder, folder) && recurse == _browseRecurse && inLibrary == _browseInLibrary;
            var cache = sameBrowse ? _browseCache : new ConcurrentDictionary<string, MetadataCacheEntry>(PathComparer);
            List<PhotoItem> items = [];
            var locations = new Dictionary<string, FolderLocation>(PathComparer);
            Exception? error = null;
            if (!inLibrary)
            {
                var favorites = new HashSet<string>(_favorites, PathComparer);
                var result = await Task.Run(() => ScanCore([folder], recurse, favorites, cache, token), token).ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
                if (_disposed || version != _browseVersion) return null;
                if (result.Failures.Count > 0)
                {
                    error = result.Failures[0].Error;
                    if (!preserveSelection && result.Failures.Any(failure => PathComparer.Equals(folder, failure.Folder)))
                        return error;
                    if (sameBrowse)
                    {
                        result.Items.AddRange(_browsed.Where(item => IsFailedFolder(item.FolderPath, result.Failures, recurse)));
                        foreach (var (path, location) in _browseLocations)
                            if (IsFailedFolder(path, result.Failures, recurse)) result.Locations.TryAdd(path, location);
                    }
                }
                items = result.Items;
                locations = result.Locations;
            }
            else
            {
                if (preserveSelection) await RefreshLibraryAsync(folder, recurse, token);
                if (await Task.Run(() => FolderIdentity.ReadFolder(folder), token) is { } location)
                    locations.Add(folder, location);
            }

            token.ThrowIfCancellationRequested();
            // A folder browsed later wins over this one if it is read faster.
            if (_disposed || version != _browseVersion) return null;
            if (preserveSelection)
            {
                var selected = BrowsedItems(folder).Where(item => item.IsSelected).Select(item => item.Path).ToHashSet(PathComparer);
                foreach (var item in items)
                {
                    item.IsFavorite = _favorites.Contains(item.Path);
                    item.IsSelected = selected.Contains(item.Path);
                }
            }
            var paths = items.Select(item => item.MediaPath).ToHashSet(PathComparer);
            foreach (var path in cache.Keys)
                if (!paths.Contains(path)) cache.TryRemove(path, out _);
            _browseCache = cache;
            (_browsed, _browseRecurse, _browseInLibrary, BrowsedFolder) = (items, recurse, inLibrary, folder);
            _browseLocations = locations;
            if (preserveSelection && (!inLibrary || !sameBrowse)) Changed?.Invoke(this, EventArgs.Empty);
            return error;
        }

        private bool IsBrowsed(string path) => BrowsedFolder != null && Path.GetDirectoryName(path) is { } directory
            && MatchesFolder(BrowsedFolder, directory, _browseRecurse);

        private static bool MatchesFolder(string folder, string directory, bool recurse) =>
            recurse ? IsWithin(folder, directory) : PathComparer.Equals(folder, directory);

        private IEnumerable<PhotoItem> BrowsedItems(string? folder) => !PathComparer.Equals(folder, BrowsedFolder) ? []
            : _browseInLibrary ? _items.Where(item => IsBrowsed(item.Path)) : _browsed;

        public void RemoveFolder(string folder)
        {
            if (_folders.RemoveAll(f => string.Equals(f, folder, PathComparison)) == 0)
                return;
            PhotosSettings.SaveFolders(_folders);
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
            _ = RescanAsync();
        }

        internal void SyncHiddenFolders()
        {
            if (_hiddenFolderStore == null) return;
            HashSet<string> folders;
            try { folders = _hiddenFolderStore.Read(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                ApplyHiddenFolders(null, ex);
                return;
            }
            ApplyHiddenFolders(folders, null);
        }

        private void ApplyHiddenFolders(HashSet<string>? folders, Exception? error)
        {
            var changed = folders == null ? _hiddenFolders != null : _hiddenFolders?.SetEquals(folders) != true;
            _hiddenFolders = folders;
            HiddenFoldersError = error;
            if (!changed) return;

            HiddenFoldersChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal bool IsFolderExplicitlyHidden(string folder) => _hiddenFolders is { Count: > 0 } &&
            _hiddenFolders.Contains(GetFolderLocation(folder)?.Path ?? FolderIdentity.TryResolve(folder) ??
                Path.TrimEndingDirectorySeparator(folder));

        internal async Task SetFolderHiddenAsync(string folder, bool hidden)
        {
            if (_hiddenFolderStore == null) return;
            var identity = await Task.Run(() => FolderIdentity.Resolve(folder));
            HashSet<string> folders;
            try { folders = _hiddenFolderStore.SetHidden(identity, hidden); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                SyncHiddenFolders();
                throw;
            }
            ApplyHiddenFolders(folders, null);
        }

        internal bool IsInHiddenFolder(string folder) => _hiddenFolders is { Count: > 0 } &&
            GetFolderLocation(folder)?.HiddenRoot(_hiddenFolders) != null;

        private FolderLocation? GetFolderLocation(string folder) =>
            _browseLocations.GetValueOrDefault(folder) ?? _libraryLocations.GetValueOrDefault(folder);

        internal IEnumerable<PhotoItem> GetVisibleItems(IEnumerable<PhotoItem> items, string? openFolder = null)
        {
            if (_hiddenFolders is not { } hiddenFolders) return [];
            if (hiddenFolders.Count == 0) return items;
            // A separately hidden subfolder has to be opened directly.
            var openRoot = openFolder == null ? null : GetFolderLocation(openFolder)?.HiddenRoot(hiddenFolders);
            return items.Where(item => AllowsMedia(item.ResolvedPath, item.Location, openRoot, hiddenFolders));
        }

        internal bool IsVisibleMedia(PhotoItem item, string? path, string? openFolder) =>
            _hiddenFolders is { } marked && (marked.Count == 0 ||
                AllowsMedia(path, item.Location, openFolder == null ? null : GetFolderLocation(openFolder)?.HiddenRoot(marked), marked));

        internal async Task<string?> ResolveVisiblePathAsync(PhotoItem item, string? openFolder, CancellationToken token = default)
        {
            if (_hiddenFolderStore == null) return item.MediaPath;
            var (path, location, opened) = await Task.Run(() =>
            {
                var mediaPath = FolderIdentity.TryResolve(item.Path);
                var itemFolder = FolderIdentity.ReadFolder(item.FolderPath);
                FolderLocation? openedFolder = null;
                if (openFolder != null)
                    openedFolder = openFolder == item.FolderPath ? itemFolder : FolderIdentity.ReadFolder(openFolder);
                return (mediaPath, itemFolder, openedFolder);
            }, token);
            token.ThrowIfCancellationRequested();
            SyncHiddenFolders();
            if (_hiddenFolders is not { } marked) return null;
            if (marked.Count == 0) return path ?? item.Path;
            return AllowsMedia(path, location, opened?.HiddenRoot(marked), marked) ? path : null;
        }

        private static bool AllowsMedia(string? path, FolderLocation? location, string? openRoot, HashSet<string> marked)
        {
            if (path == null || location == null) return false;
            var physical = FolderLocation.FindMarkedAncestor(Path.GetDirectoryName(path), marked);
            var logical = location.HiddenRoot(marked);
            return (physical == null || PathComparer.Equals(physical, openRoot)) &&
                (logical == null || PathComparer.Equals(logical, openRoot));
        }

        public List<PhotoFolderInfo> GetFolderSummaries()
        {
            var roots = _folders.Select(Path.TrimEndingDirectorySeparator).ToList();
            return [.. _items
                .GroupBy(i => i.FolderPath, PathComparer)
                .Select(g =>
                {
                    var cover = GetVisibleItems(g).MaxBy(i => i.SortDate);
                    var isRoot = roots.Contains(Path.TrimEndingDirectorySeparator(g.Key), PathComparer);
                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(g.Key));
                    return new PhotoFolderInfo(g.Key, string.IsNullOrEmpty(name) ? g.Key : name, g.Count(), cover, isRoot);
                })
                .OrderByDescending(f => f.IsRoot)
                .ThenBy(f => f.Path, PathComparer)];
        }

        public void ApplyIncludeSubfolders(bool include)
        {
            if (PhotosSettings.IncludeSubfolders == include)
                return;
            PhotosSettings.IncludeSubfolders = include;
            PhotosSettings.SaveOptions();
            _ = RescanAsync();
        }

        #endregion

        #region Favorites

        public void SetFavorite(IEnumerable<PhotoItem> items, bool favorite)
        {
            var browsedPaths = _browsed.Count == 0 ? null : new HashSet<string>(PathComparer);
            foreach (var item in items)
            {
                item.IsFavorite = favorite;
                if (_byPath.TryGetValue(item.Path, out var indexed)) indexed.IsFavorite = favorite;
                browsedPaths?.Add(item.Path);
                if (favorite)
                    _favorites.Add(item.Path);
                else
                    _favorites.Remove(item.Path);
            }
            if (browsedPaths != null)
                foreach (var item in _browsed.Where(item => browsedPaths.Contains(item.Path))) item.IsFavorite = favorite;
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
            GetVisibleItems(album.Paths.Select(p => _byPath.GetValueOrDefault(p)).OfType<PhotoItem>());

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
            _browsed = [.. _browsed.Where(i => !set.Contains(i.Path))];
            foreach (var path in deleted)
            {
                _byPath.Remove(path);
                _favorites.Remove(path);
                _cache.TryRemove(path, out _);
                _browseCache.TryRemove(path, out _);
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
            if (!Path.IsPathFullyQualified(root))
                throw new InvalidOperationException("The Photos import directory must be an absolute path.");
            var target = System.IO.Path.Combine(Path.GetFullPath(root), "Flowery Photos", "Imported " + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
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
                PhotoSourceKind.Favorites => VisibleLibraryItems.Where(i => i.IsFavorite),
                PhotoSourceKind.Album => FindAlbum(query.SourceKey) is { } album ? GetAlbumItems(album) : [],
                PhotoSourceKind.Folder => GetVisibleItems(_items.Where(i => string.Equals(i.FolderPath, query.SourceKey, PathComparison)), query.SourceKey),
                PhotoSourceKind.Browsed => GetVisibleItems(BrowsedItems(query.SourceKey), query.SourceKey),
                _ => VisibleLibraryItems
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
                PhotoTypeFilter.Other => source.Where(i => !i.IsVideo && i.Extension is not ("JPG" or "JPEG" or "JPE" or "JFIF" or "PNG")),
                PhotoTypeFilter.Video => source.Where(i => i.IsVideo),
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
                PhotoSortMode.DateModifiedOldest => [.. source.OrderBy(i => i.DateModified)],
                PhotoSortMode.NameAscending => [.. source.OrderBy(i => i.FileName, StringComparer.CurrentCultureIgnoreCase)],
                PhotoSortMode.NameDescending => [.. source.OrderByDescending(i => i.FileName, StringComparer.CurrentCultureIgnoreCase)],
                PhotoSortMode.SizeLargest => [.. source.OrderByDescending(i => i.FileSize)],
                PhotoSortMode.SizeSmallest => [.. source.OrderBy(i => i.FileSize)],
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
            _browseCache.Clear();
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
            _scanCts?.Cancel();
            await Task.WhenAll(_scans);
            _scans.Clear();
        }
    }

    public sealed class MetadataCacheEntry
    {
        // Version 2 reads EXIF from PNG, WebP, HEIC and AVIF and adds videos; older entries are read again once.
        public const int CurrentVersion = 2;
        public int Version { get; set; }
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
    [JsonSerializable(typeof(string[]))]
    internal partial class PhotosJsonContext : JsonSerializerContext
    {
    }
}
