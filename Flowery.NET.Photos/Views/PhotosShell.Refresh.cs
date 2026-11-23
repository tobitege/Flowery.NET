using System.ComponentModel;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotosShell
{
    private const int FolderQuietPeriodMs = 1500;
    private const int FolderMaxDelayMs = 3000;
    private readonly DispatcherTimer _folderRefreshTimer = new();
    private long? _firstPendingChange;
    private FileSystemWatcher? _folderWatcher;
    private CancellationTokenSource? _refresh;
    private bool _refreshFromWatcher;
    private DaisyButton? _homeRefresh;
    private readonly Dictionary<string, (long Order, bool IsDirectory)> _pendingArrivals = new(PhotoLibrary.PathComparer);
    private long _arrivalSequence;
    private (string Path, bool IsDirectory)[]? _refreshArrivals;

    private async void RefreshClick(object? sender, RoutedEventArgs e) => await RefreshCurrentAsync();

    private bool CanRefresh => _refresh == null && !_browsing && !Viewer.IsVisible;

    private void UpdateRefreshButtons()
    {
        var enabled = CanRefresh;
        if (_homeRefresh != null) _homeRefresh.IsEnabled = enabled;
        RefreshCollections.IsEnabled = enabled;
        Gallery.SetRefreshEnabled(enabled);
    }

    private async Task RefreshCurrentAsync(bool fromWatcher = false)
    {
        if (!CanRefresh) return;
        using var refresh = new CancellationTokenSource();
        _refresh = refresh;
        _refreshFromWatcher = fromWatcher;
        UpdateRefreshButtons();
        try
        {
            if (!fromWatcher) UpdateFolderWatcher(rebind: true);
            Status.Text = L("Empty_ScanningTitle");
            await _library.RefreshAsync(Gallery.IsVisible ? Gallery.Query : null, refresh.Token);
        }
        catch (OperationCanceledException) when (refresh.IsCancellationRequested) { }
        catch (Exception ex) { ShowToast(ex.Message, true); }
        finally
        {
            _refresh = null;
            _refreshFromWatcher = false;
            _refreshArrivals = null;
            UpdateRefreshButtons();
            if (_topLevel != null && !_browsing) UpdateSelection();
        }
    }

    private async void RefreshWatchedFolder(object? sender, EventArgs e)
    {
        _folderRefreshTimer.Stop();
        if (_folderWatcher == null || _firstPendingChange == null) return;
        if (_refresh != null || _browsing || _library.IsScanning)
        {
            StartFolderRefreshTimer(FolderQuietPeriodMs);
            return;
        }
        _firstPendingChange = null;
        _refreshArrivals = _pendingArrivals.OrderByDescending(pair => pair.Value.Order)
            .Select(pair => (pair.Key, pair.Value.IsDirectory)).ToArray();
        _pendingArrivals.Clear();
        await RefreshCurrentAsync(fromWatcher: true);
    }

    private void UpdateFolderWatcher(bool rebind = false)
    {
        var folder = _topLevel != null && Gallery.IsVisible && PhotosSettings.WatchOpenFolder ? Gallery.FolderPath : null;
        var recurse = Gallery.Query.Source == PhotoSourceKind.Browsed && PhotosSettings.IncludeSubfolders;
        if (!rebind && _folderWatcher is { } current && PhotoLibrary.PathComparer.Equals(current.Path, folder) &&
            current.IncludeSubdirectories == recurse) return;

        StopFolderWatcher();
        if (folder == null)
        {
            Gallery.SetFolderWatchState(false);
            return;
        }
        try
        {
            var watcher = _folderWatcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = recurse,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                    | (recurse ? NotifyFilters.DirectoryName : 0)
            };
            watcher.Created += OnWatchedFileChanged;
            watcher.Changed += OnWatchedFileChanged;
            watcher.Deleted += OnWatchedFileChanged;
            watcher.Renamed += OnWatchedFileChanged;
            watcher.Error += OnFolderWatcherError;
            watcher.EnableRaisingEvents = true;
            Gallery.SetFolderWatchState(true);
            // Catch files that arrived between opening the folder and starting the watcher.
            if (!rebind) QueueWatchedRefresh(watcher);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception or PlatformNotSupportedException)
        {
            FailFolderWatcher(ex);
        }
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        var watcher = (FileSystemWatcher)sender;
        var supported = ImageMetadataReader.IsSupported(e.FullPath);
        var isArrival = e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Renamed;
        var directory = watcher.IncludeSubdirectories && isArrival && Directory.Exists(e.FullPath);
        if (supported
            || e is RenamedEventArgs renamed && ImageMetadataReader.IsSupported(renamed.OldFullPath)
            || directory || watcher.IncludeSubdirectories && e.ChangeType == WatcherChangeTypes.Deleted)
        {
            var arrival = isArrival && (supported || directory) ? e.FullPath : null;
            Dispatcher.UIThread.Post(() => QueueWatchedRefresh(watcher, arrival, directory));
        }
    }

    private void QueueWatchedRefresh(FileSystemWatcher watcher, string? arrival = null, bool directory = false)
    {
        if (_folderWatcher != watcher) return;
        if (arrival != null) _pendingArrivals[arrival] = (++_arrivalSequence, directory);
        var now = Environment.TickCount64;
        _firstPendingChange ??= now;
        var remaining = _firstPendingChange.Value + FolderMaxDelayMs - now;
        if (remaining <= 0 && _folderRefreshTimer.IsEnabled) return;
        StartFolderRefreshTimer(Math.Clamp(remaining, 1, FolderQuietPeriodMs));
    }

    private void StartFolderRefreshTimer(long delay)
    {
        _folderRefreshTimer.Stop();
        _folderRefreshTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _folderRefreshTimer.Start();
    }

    private void OnFolderWatcherError(object sender, ErrorEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_folderWatcher != sender) return;
            if (e.GetException() is InternalBufferOverflowException)
            {
                QueueWatchedRefresh((FileSystemWatcher)sender);
                return;
            }
            FailFolderWatcher(e.GetException());
        });
    }

    private void FailFolderWatcher(Exception error)
    {
        StopFolderWatcher();
        var message = L("FolderWatchFailed") + "\n" + error.Message;
        Gallery.SetFolderWatchState(false, message);
        ShowToast(message, true);
    }

    private void StopFolderWatcher()
    {
        _folderRefreshTimer.Stop();
        _firstPendingChange = null;
        _pendingArrivals.Clear();
        if (_refreshFromWatcher) _refresh?.Cancel();
        var watcher = _folderWatcher;
        _folderWatcher = null;
        watcher?.Dispose();
    }
}
