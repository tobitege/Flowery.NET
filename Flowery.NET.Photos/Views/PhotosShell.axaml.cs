using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotosShell : UserControl, IDisposable
{
    private string _page = "Home";
    private bool _returnToGallery;
    private bool _browsing;
    private bool _deleting;
    private int _openVersion;
    private CancellationTokenSource? _browse;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _hiddenFoldersTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly PhotoLibrary _library;
    public event Action<bool>? TitleViewChanged;
    public void ShowViewOptions() => Viewer.ShowViewOptions();
    private Window Owner => (Window)TopLevel.GetTopLevel(this)!;
    private IReadOnlyList<PhotoItem> Selected => Gallery.IsVisible ? Gallery.Selected : [];
    private string? OpenFolder => Gallery.IsVisible || Viewer.IsVisible && _returnToGallery ? Gallery.FolderPath : null;
    private static string L(string key) => PhotosLocalization.GetString("Photos_" + key);

    public PhotosShell() : this(PhotoLibrary.Instance) { }
    public PhotosShell(PhotoLibrary library)
    {
        _library = library;
        InitializeComponent();
        Collections.ItemTemplate = new FuncDataTemplate<CollectionRow>((row, _) => BuildCollectionRow(row));
        AttachedToVisualTree += (_, _) =>
        {
            FlowerySizeManager.SizeChanged += OnCardSizeChanged;
            AttachKeys();
            _library.SyncHiddenFolders();
            _hiddenFoldersTimer.Start();
            UpdateFolderWatcher();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            StopFolderWatcher();
            _hiddenFoldersTimer.Stop();
            _refresh?.Cancel();
            FlowerySizeManager.SizeChanged -= OnCardSizeChanged;
            DetachKeys();
        };
        Gallery.Library = Viewer.Library = library;
        DataContext = PhotosLocalization.Instance;
        Gallery.OpenRequested += item => _ = OpenAsync(item, Gallery.Items);
        Gallery.FullScreenRequested += item => _ = OpenFullScreenAsync(item);
        Gallery.SelectionChanged += UpdateSelection;
        Gallery.ResetSearchRequested += () => Search.Text = "";
        Gallery.ConfigureTile += ConfigureTile;
        Gallery.ImportRequested += () => _ = ImportAsync();
        Gallery.ImportFolderRequested += () => _ = AddFolderAsync();
        Gallery.RescanRequested += () => _ = RescanAsync();
        Gallery.RefreshRequested += () => _ = RefreshCurrentAsync();
        Gallery.FolderWatchChanged += () => UpdateFolderWatcher();
        Gallery.WatchOptionsSaveFailed += message => ShowToast(message, true);
        Gallery.HideFolderRequested += SetFolderHiddenAsync;
        _folderRefreshTimer.Tick += RefreshWatchedFolder;
        _hiddenFoldersTimer.Tick += (_, _) => _library.SyncHiddenFolders();
        Gallery.SlideshowRequested += () => _ = StartSlideshowAsync(Gallery.Items);
        Gallery.BackRequested += () => _ = NavigateAsync(Gallery.Query.Source switch
        {
            PhotoSourceKind.Album => "Albums", PhotoSourceKind.Browsed => "Home", _ => "Folders"
        });
        Viewer.BackRequested += () => _ = ReturnFromViewerAsync();
        Viewer.ConfirmLeave = CanLeaveViewerAsync;
        Viewer.SettingsRequested += () => _ = ShowSettingsAsync();
        Viewer.DeletePhoto = DeleteAsync;
        Viewer.AlbumMenu = item => CreateAlbumMenu([item]);
        Viewer.Notification += ShowToast;
        Viewer.MediaOpened += UpdateViewerVisibility;
        Viewer.TitleViewChanged += visible => TitleViewChanged?.Invoke(visible);
        _toastTimer.Tick += (_, _) => { Notifications.Items.Clear(); _toastTimer.Stop(); };
        Viewer.ImmersiveChanged += immersive =>
        {
            AppHeader.IsVisible = NavigationRail.IsVisible = AppStatus.IsVisible = !immersive;
            Margin = immersive ? new Thickness(0) : new Thickness(0, 42, 0, 0);
        };
        // The search box searches the pages; with one photo or video open it has nothing to search, so it steps aside.
        Viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty) return;
            Search.IsVisible = !Viewer.IsVisible;
            UpdateRefreshButtons();
        };
        Search.TextChanged += (_, _) =>
        {
            if (Viewer.IsVisible) return;
            if (Gallery.IsVisible) { Gallery.Search(Search.Text); return; }
            if (CollectionsScroll.IsVisible) { BuildCollections(); return; }
            _page = "Gallery"; ShowPage(); Gallery.Search(Search.Text);
        };
        HomeScroll.SizeChanged += (_, e) => { if (Math.Abs(_homeWidth - e.NewSize.Width) > 1) BuildHome(); };
        CollectionsScroll.SizeChanged += (_, e) => { if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 1) BuildCollections(); };
        _library.Changed += OnLibraryChanged;
        _library.HiddenFoldersChanged += OnHiddenFoldersChanged;
        _library.ScanningChanged += OnScanning;
        _library.ScanProgress += OnScanProgress;
        PhotosLocalization.CultureChanged += OnCultureChanged;
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
        // Before a scroll viewer scrolls, so Ctrl+wheel sizes the thumbnails on every page.
        AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (Viewer.IsVisible || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0) return;
            ResizeThumbnails(e.Delta.Y > 0);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        // Down or Enter in the search box goes on to what it found; only Tab, through every button on the way, did.
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is not (Key.Down or Key.Enter) || e.KeyModifiers != KeyModifiers.None || Viewer.IsVisible) return;
            e.Handled = Gallery.IsVisible ? Gallery.FocusFirstOnScreen()
                : CollectionsScroll.IsVisible ? FocusFirstCard() : FocusFirstOnHome();
        }, RoutingStrategies.Tunnel);
        ShowPage();
        UpdateSelection();
    }
    private void OnScanning(object? sender, bool value) { if (value) Status.Text = L("Empty_ScanningTitle"); else UpdateSelection(); }
    private void OnScanProgress(object? sender, (int Done, int Total) progress)
    {
        if (_library.IsScanning || _browsing || _refresh != null) Status.Text = $"{L("Empty_ScanningTitle")} {progress.Done} / {progress.Total}";
    }
    private void ShowToast(string message, bool error = false)
    {
        Notifications.Items.Clear();
        var alert = new DaisyAlert { Content = message, Variant = error ? DaisyAlertVariant.Error : DaisyAlertVariant.Success };
        AutomationProperties.SetName(alert, message);
        AutomationProperties.SetAutomationId(alert, "Photos.Notification");
        Notifications.Items.Add(alert);
        _toastTimer.Stop(); _toastTimer.Start();
    }
    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Gallery.Refresh(Gallery.IsVisible && _refreshFromWatcher && _folderWatcher != null ? _refreshArrivals : null);
        BuildHome(); BuildCollections(); UpdateSelection();
    }
    private void OnHiddenFoldersChanged(object? sender, EventArgs e) => UpdateViewerVisibility();

    private void UpdateViewerVisibility()
    {
        if (!Viewer.IsVisible || Viewer.Current is not { } current) return;
        var folder = OpenFolder;
        var visible = _library.GetVisibleItems(Viewer.Items, folder).ToList();
        var blocked = !visible.Any(item => PhotoLibrary.PathComparer.Equals(item.Path, current.Path)) ||
            !_library.IsVisibleMedia(current, Viewer.OpenedMediaPath, folder);
        Viewer.ApplyFolderVisibility(visible, blocked);
        HiddenViewerPanel.IsVisible = blocked;
        if (blocked)
            HiddenViewerMessage.Text = L(_library.HiddenFoldersError == null ? "HiddenOpenFolder" : "HiddenFoldersUnavailable");
    }

    private async void BackFromHiddenViewerClick(object? sender, RoutedEventArgs e) => await ReturnFromViewerAsync();
    private async Task SetFolderHiddenAsync(string folder, bool hidden)
    {
        try { await _library.SetFolderHiddenAsync(folder, hidden); }
        catch (Exception ex) { Gallery.Refresh(); ShowToast(ex.Message, true); }
    }
    private void OnCultureChanged(object? sender, string e)
    {
        FlowDirection = PhotosLocalization.CurrentCulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Gallery.RefreshLabels(); Viewer.RefreshLabels();
        if (Gallery.IsVisible) Gallery.Refresh();
        else if (!Viewer.IsVisible) { BuildHome(); BuildCollections(); }
    }
    private async Task NavigateAsync(string page)
    {
        if (!await CanLeaveViewerAsync()) return;
        _openVersion++;
        _browse?.Cancel();
        _refresh?.Cancel();
        Viewer.CloseMedia(); Gallery.ClearSelection(); _page = page; ShowPage();
    }
    private async Task ReturnFromViewerAsync()
    {
        if (!await CanLeaveViewerAsync()) return;
        _openVersion++;
        var last = Viewer.Current;
        var order = Viewer.Items;
        Viewer.CloseMedia(); Viewer.IsVisible = false; TitleViewChanged?.Invoke(false);
        HiddenViewerPanel.IsVisible = false;
        Gallery.IsVisible = _returnToGallery;
        HomeScroll.IsVisible = !_returnToGallery && _page == "Home";
        CollectionsScroll.IsVisible = !_returnToGallery && _page is "Albums" or "Folders";
        if (_returnToGallery)
        {
            Gallery.Refresh();
            FocusNearest(order, last);
        }
        else { BuildHome(); BuildCollections(); }
        UpdateFolderWatcher();
        UpdateSelection();
    }
    // Focuses a photo in the gallery, or the nearest one that is still there: a deleted photo is gone, the next one
    // takes its place, and at the end the previous one.
    private void FocusNearest(IReadOnlyList<PhotoItem> order, PhotoItem? photo, NavigationMethod method = NavigationMethod.Unspecified)
    {
        var at = photo == null ? -1 : order.Select(item => item.Path).ToList().IndexOf(photo.Path);
        if (at < 0) { Gallery.FocusPhoto(photo, method); return; }
        foreach (var item in order.Skip(at).Concat(order.Take(at).Reverse()))
        {
            if (Gallery.FocusPhoto(item, method)) break;
        }
    }
    private void ShowPage()
    {
        _openVersion++;
        Viewer.IsVisible = false;
        HiddenViewerPanel.IsVisible = false;
        TitleViewChanged?.Invoke(false);
        HomeScroll.IsVisible = _page == "Home";
        Gallery.IsVisible = _page is "Gallery" or "Favorites";
        CollectionsScroll.IsVisible = _page is "Albums" or "Folders";
        if (Gallery.IsVisible) Gallery.Show(new PhotoQuery { Source = _page == "Favorites" ? PhotoSourceKind.Favorites : PhotoSourceKind.All, SearchText = Search.Text }, L("Nav_" + _page));
        UpdateFolderWatcher();
        BuildHome(); BuildCollections(); UpdateNavigationSelection();
    }
    private void UpdateNavigationSelection()
    {
        foreach (var button in NavigationButtons.Children.OfType<DaisyButton>())
        {
            var selected = button.Tag as string == _page;
            button.IsActive = selected;
            button.Variant = selected ? DaisyButtonVariant.Primary : DaisyButtonVariant.Ghost;
            button.ButtonStyle = selected ? DaisyButtonStyle.Soft : DaisyButtonStyle.Default;
            // The side bar is one tab stop, entered at the open page.
            if (selected) KeyboardNavigation.SetTabOnceActiveElement(NavigationRail, button);
        }
    }
    private DaisyButton ActionButton(string key, Func<Task> action, Flowery.Enums.DaisyIconSymbol? icon = null)
    {
        var button = new DaisyButton { Content = L(key), IconSymbol = icon, Margin = new Thickness(0, 0, 8, 8) };
        AutomationProperties.SetAutomationId(button, "Photos.Action." + key);
        AutomationProperties.SetName(button, L(key));
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { ShowToast(ex.Message, true); } };
        return button;
    }
    private Task OpenCollectionAsync(PhotoSourceKind kind, string key, string title)
    {
        _openVersion++;
        _browse?.Cancel();
        _refresh?.Cancel();
        _page = kind switch { PhotoSourceKind.Album => "Albums", PhotoSourceKind.Browsed => "Gallery", _ => "Folders" };
        UpdateNavigationSelection();
        HomeScroll.IsVisible = false; CollectionsScroll.IsVisible = false; Gallery.IsVisible = true;
        Gallery.Show(new PhotoQuery { Source = kind, SourceKey = key }, title);
        UpdateFolderWatcher();
        return Task.CompletedTask;
    }
    public Task OpenFileAsync(string path) => OpenFilesAsync([path]);
    public async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        var supported = paths.Where(ImageMetadataReader.IsSupported).ToArray();
        if (supported.Length == 0) return;
        var version = ++_openVersion;
        _browse?.Cancel();
        _library.SyncHiddenFolders();
        var requested = await _library.GetOpenItemsAsync(supported);
        if (requested.Count == 0 || version != _openVersion) return;
        var items = _library.GetVisibleItems(requested, OpenFolder).ToList();
        if (items.Count == 0)
        {
            ShowToast(L(_library.HiddenFoldersError == null ? "HiddenOpenFolder" : "HiddenFoldersUnavailable"), true);
            return;
        }
        var first = items[0];
        if (items.Count == 1 && !_library.IsInLibrary(first.Path))
        {
            try
            {
                var siblings = await Task.Run(() => Directory.EnumerateFiles(first.FolderPath).Where(ImageMetadataReader.IsSupported)
                    .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).ToArray());
                items = await _library.GetOpenItemsAsync(siblings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { items = [first]; }
        }
        if (version == _openVersion) await OpenAsync(first, items);
    }
    private async Task OpenFullScreenAsync(PhotoItem item)
    {
        await OpenAsync(item, Gallery.Items);
        if (Viewer.IsVisible) Viewer.ShowFullScreen();
    }
    private async Task OpenAsync(PhotoItem item, IReadOnlyList<PhotoItem> items)
    {
        var version = ++_openVersion;
        if (!await CanLeaveViewerAsync() || version != _openVersion) return;
        _browse?.Cancel();
        var folder = OpenFolder;
        var source = await _library.ResolveVisiblePathAsync(item, folder);
        if (version != _openVersion) return;
        if (source == null)
        {
            ShowToast(L(_library.HiddenFoldersError == null ? "HiddenOpenFolder" : "HiddenFoldersUnavailable"), true);
            return;
        }
        var visible = _library.GetVisibleItems(items, folder).ToList();
        if (!visible.Any(candidate => PhotoLibrary.PathComparer.Equals(candidate.Path, item.Path))) visible.Insert(0, item);
        _refresh?.Cancel();
        if (!Viewer.IsVisible) _returnToGallery = Gallery.IsVisible;
        HomeScroll.IsVisible = CollectionsScroll.IsVisible = Gallery.IsVisible = false;
        Viewer.IsVisible = true;
        UpdateFolderWatcher();
        Viewer.VisibilityFolder = folder;
        await Viewer.OpenResolvedAsync(item, visible, source);
    }
    public async Task<bool> CanLeaveViewerAsync()
    {
        if (Viewer.IsVisible && Viewer.IsBusy) return false;
        if (!Viewer.IsVisible || !Viewer.HasChanges) return true;
        var prompt = new PromptDialog(L("UnsavedTitle"), L("UnsavedText"), alternative: L("Discard"), accept: L("SaveCopy"));
        var choice = await prompt.ShowDialog<int>(Owner);
        return choice == 2 || choice == 1 && await Viewer.SaveAsync(false);
    }
    private void ConfigureTile(PhotoTile tile)
    {
        tile.SelectionChanged += UpdateSelection;
        var menu = new ContextMenu();
        menu.Opening += (_, _) => BuildTileMenu(menu, tile.Item);
        tile.ContextMenu = menu;
    }
    private void BuildTileMenu(ContextMenu menu, PhotoItem photo)
    {
        menu.Items.Clear();
        var targets = Gallery.IsVisible ? Gallery.ContextItems(photo) : [photo];
        void Add(string key, Func<Task> action)
        {
            var item = new MenuItem { Header = L(key) };
            AutomationProperties.SetName(item, L(key));
            AutomationProperties.SetAutomationId(item, "Photos.Context." + key);
            item.Click += async (_, _) => { try { await action(); } catch (Exception ex) { ShowToast(ex.Message, true); } };
            menu.Items.Add(item);
        }
        Add("Open", () => OpenAsync(photo, Gallery.Items));
        if (!photo.IsVideo) Add("Edit", async () => { await OpenAsync(photo, Gallery.Items); Viewer.BeginEditing(); });
        var favorite = !targets.All(item => item.IsFavorite);
        Add(favorite ? "Favorite" : "Unfavorite", () => { _library.SetFavorite(targets, favorite); return Task.CompletedTask; });
        menu.Items.Add(CreateAlbumMenu(targets));
        if (Gallery.Query is { Source: PhotoSourceKind.Album, SourceKey: { } name })
            Add("RemoveFromAlbum", () =>
            {
                if (_library.FindAlbum(name) is { } album) _library.RemoveFromAlbum(album, targets);
                return Task.CompletedTask;
            });
        Add("Copy", () => CopyPhotosAsync(targets));
        Add("CopyPath", async () =>
        {
            if (Owner.Clipboard == null) return;
            await Owner.Clipboard.SetTextAsync(photo.Path); ShowToast(L("PathCopied"));
        });
        Add("ShowInExplorer", () => { FileOperations.ShowInExplorer(photo.Path); return Task.CompletedTask; });
        if (OperatingSystem.IsWindows())
        {
            Add("OpenWith", () => { FileOperations.OpenWith(photo.Path); return Task.CompletedTask; });
            if (!photo.IsVideo) Add("SetAsBackground", () =>
            {
                var success = FileOperations.SetAsWallpaper(photo.Path);
                ShowToast(L(success ? "BackgroundSet" : "ActionFailed"), !success);
                return Task.CompletedTask;
            });
            Add("Delete", () => DeleteAsync(targets));
        }
    }
    private MenuItem CreateAlbumMenu(IReadOnlyList<PhotoItem> items)
    {
        var menu = new MenuItem { Header = L("AddToAlbum") };
        AutomationProperties.SetAutomationId(menu, "Photos.AddToAlbum");
        AutomationProperties.SetName(menu, L("AddToAlbum"));
        var create = new MenuItem { Header = L("NewAlbum") };
        AutomationProperties.SetAutomationId(create, "Photos.Album.New");
        AutomationProperties.SetName(create, L("NewAlbum"));
        create.Click += async (_, _) => await CreateAlbumAsync(items);
        menu.Items.Add(create);
        foreach (var album in _library.Albums)
        {
            var entry = new MenuItem { Header = album.Name };
            AutomationProperties.SetAutomationId(entry, "Photos.Album." + album.Name);
            AutomationProperties.SetName(entry, album.Name);
            entry.Click += (_, _) => AddToAlbum(album, items);
            menu.Items.Add(entry);
        }
        return menu;
    }
    private void AddToAlbum(PhotoAlbum album, IReadOnlyList<PhotoItem> items)
    {
        var count = _library.AddToAlbum(album, items);
        ShowToast(PhotosLocalization.Format("Photos_AddedToAlbum", count, album.Name));
    }
    private async Task CreateAlbumAsync(IReadOnlyList<PhotoItem> items)
    {
        var prompt = new PromptDialog(L("NewAlbum"), L("AlbumNamePrompt"), L("AlbumDefaultName"), accept: L("Create"));
        if (await prompt.ShowDialog<int>(Owner) != 1) return;
        if (_library.FindAlbum(prompt.Value) != null) { ShowToast(L("AlbumNameTaken"), true); return; }
        var album = _library.CreateAlbum(prompt.Value!);
        if (items.Count > 0) AddToAlbum(album, items);
        else ShowToast(PhotosLocalization.Format("Photos_AlbumCreatedHint", album.Name));
    }
    private async Task DeleteAsync(IReadOnlyList<PhotoItem> items)
    {
        if (!OperatingSystem.IsWindows() || items.Count == 0) return;
        // One file is asked about by its name: Delete also takes the thumbnail with the focus, which shows no mark.
        if (PhotosSettings.ConfirmDelete && await new PromptDialog(L("DeleteTitle"),
            items.Count == 1 ? L("DeleteConfirmOne") + "\n" + items[0].FileName : PhotosLocalization.Format("Photos_DeleteConfirmMany", items.Count),
            accept: L("Delete"), destructive: true).ShowDialog<int>(Owner) != 1) return;
        // A video that plays in its thumbnail holds its file open, and the file would stay.
        await PhotoTile.StopPlaybackAsync(items.Select(item => item.Path));
        var deleted = await _library.DeleteAsync(items);
        ShowToast(PhotosLocalization.Format("Photos_DeletedCount", deleted), deleted != items.Count);
    }
    private async Task<bool> ConfirmAsync(string title, string message) => await new PromptDialog(title, message, accept: title, destructive: true).ShowDialog<int>(Owner) == 1;
    private static string ItemCount(int count) => PhotosLocalization.Format(count == 1 ? "Photos_ItemCountOne" : "Photos_ItemCount", count);
    private void UpdateSelection()
    {
        var count = Selected.Count;
        BatchFavorite.IsVisible = BatchAlbum.IsVisible = BatchCopy.IsVisible = ClearSelection.IsVisible = count > 0;
        BatchDelete.IsVisible = count > 0 && OperatingSystem.IsWindows();
        BatchRemoveAlbum.IsVisible = count > 0 && Gallery.Query.Source == PhotoSourceKind.Album;
        if (_library.HiddenFoldersError != null)
            Status.Text = L("HiddenFoldersUnavailable");
        else if (!_library.IsScanning)
            Status.Text = count > 0 ? PhotosLocalization.Format(count == 1 ? "Photos_SelectedOne" : "Photos_SelectedMany", count)
                : ItemCount(Gallery.IsVisible ? Gallery.Items.Count : _library.VisibleLibraryItems.Count());
    }
    private async Task AddFolderAsync()
    {
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L("AddFolder"), AllowMultiple = true });
        foreach (var folder in folders)
        {
            using (folder)
                if (folder.TryGetLocalPath() is { } path) AddFolder(path);
        }
    }
    private async Task BrowseAsync()
    {
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L("Browse") });
        var path = folders.Select(folder => folder.TryGetLocalPath()).FirstOrDefault();
        foreach (var folder in folders) folder.Dispose();
        // A place such as a Windows library is not one folder on disk.
        if (path != null) await BrowseFolderAsync(path);
        else if (folders.Count > 0) ShowToast(L("FolderNotOnDisk"), true);
    }
    /// <summary>Shows a folder, with its subfolders when the library includes them, in the gallery without adding it.</summary>
    internal async Task BrowseFolderAsync(string path)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var title = Path.GetFileName(folder) is { Length: > 0 } name ? name : folder;
        // A folder chosen later, another page or a photo opened meanwhile cancels this one, so it cannot push the
        // gallery over what the user went on to.
        var version = ++_openVersion;
        _browse?.Cancel();
        _refresh?.Cancel();
        var browse = _browse = new CancellationTokenSource();
        _browsing = true; UpdateRefreshButtons(); Status.Text = L("Empty_ScanningTitle");
        try
        {
            if (await _library.BrowseAsync(folder, browse.Token) is { } error && version == _openVersion)
                ShowToast(error.Message, true);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowToast(ex.Message, true);
            return;
        }
        finally
        {
            if (_browse == browse) { _browse = null; _browsing = false; UpdateRefreshButtons(); UpdateSelection(); }
            browse.Dispose();
        }
        if (version != _openVersion || Viewer.IsVisible || !PhotoLibrary.PathComparer.Equals(_library.BrowsedFolder, folder)) return;
        await OpenCollectionAsync(PhotoSourceKind.Browsed, folder, title);
        UpdateSelection();
    }
    private void AddFolder(string path)
    {
        var added = _library.AddFolder(path);
        ShowToast(added ? PhotosLocalization.Format("Photos_FolderAdded", path) : L("FolderAlreadyAdded"));
    }
    private async Task RescanAsync()
    {
        try { await _library.RefreshAsync(null, default); ShowToast(L("RescanDone")); }
        catch (Exception ex) { ShowToast(ex.Message, true); }
    }
    private async Task ImportAsync()
    {
        var media = new FilePickerFileType(L("PhotosAndVideos"))
        {
            Patterns = [.. ImageMetadataReader.SupportedExtensions.Select(extension => "*" + extension)]
        };
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = L("Import"), AllowMultiple = true, FileTypeFilter = [media]
        });
        try
        {
            if (files.Count == 0) return;
            var count = await _library.ImportFilesAsync(files.Select(f => f.TryGetLocalPath()).OfType<string>());
            ShowToast(PhotosLocalization.Format("Photos_ImportedCount", count), count == 0);
        }
        catch (Exception ex) { ShowToast(ex.Message, true); }
        finally { foreach (var file in files) file.Dispose(); }
    }
    private async Task ShowSettingsAsync()
    {
        var previousLabel = PhotosSettings.HiddenFolderLabel;
        await new SettingsView(_library).ShowDialog(Owner);
        if (previousLabel != PhotosSettings.HiddenFolderLabel)
        {
            Gallery.Refresh();
            BuildHome();
            BuildCollections();
        }
        UpdateFolderWatcher();
    }
    private async void NavigateClick(object? sender, RoutedEventArgs e) { if (sender is Control { Tag: string page }) await NavigateAsync(page); }
    private async void SettingsClick(object? sender, RoutedEventArgs e) => await ShowSettingsAsync();
    private async void AddCollectionFolderClick(object? sender, RoutedEventArgs e) => await AddFolderAsync();
    private async void NewAlbumClick(object? sender, RoutedEventArgs e) => await CreateAlbumAsync([]);
    private void FavoriteClick(object? sender, RoutedEventArgs e)
    {
        var selected = Selected;
        _library.SetFavorite(selected, !selected.All(item => item.IsFavorite));
    }
    private void AlbumClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control anchor) return;
        var menu = CreateAlbumMenu(Selected);
        var entries = menu.Items.Cast<MenuItem>().ToArray();
        menu.Items.Clear();
        new MenuFlyout { ItemsSource = entries }.ShowAt(anchor);
    }
    private void RemoveAlbumClick(object? sender, RoutedEventArgs e)
    {
        if (_library.FindAlbum(Gallery.Query.SourceKey) is { } album) _library.RemoveFromAlbum(album, Selected);
    }
    private async Task CopyPhotosAsync(IReadOnlyList<PhotoItem> items)
    {
        try
        {
            var count = await FileOperations.CopyFilesAsync(Owner, items.Select(item => item.Path).ToList());
            ShowToast(PhotosLocalization.Format("Photos_CopiedCount", count), count != items.Count);
        }
        catch (Exception ex) { ShowToast(ex.Message, true); }
    }
    private async void CopyClick(object? sender, RoutedEventArgs e) => await CopyPhotosAsync(Selected);
    private async void DeleteClick(object? sender, RoutedEventArgs e) => await DeleteAsync(Selected);
    private void ClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        foreach (var item in Selected) item.IsSelected = false;
        UpdateSelection();
    }
    private void DragOver(object? sender, DragEventArgs e) => e.DragEffects = DragDropEffects.Copy;
    private async void Drop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;
        var images = new List<string>();
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path == null) continue;
            if (Directory.Exists(path)) AddFolder(path);
            else if (ImageMetadataReader.IsSupported(path)) images.Add(path);
        }
        try { await OpenFilesAsync(images); } catch (Exception ex) { ShowToast(ex.Message, true); }
    }
    // One thumbnail size for the gallery rows, the home page and the album and folder cards.
    private void ResizeThumbnails(bool larger)
    {
        Gallery.SetTileHeight(PhotosSettings.StepTileHeight(larger, TopLevel.GetTopLevel(this)?.ClientSize.Height ?? Bounds.Height));
        BuildHome(); BuildCollections();
    }
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // A menu, a flyout or an open list keeps its keys.
        if (e.Handled || !IsEffectivelyVisible || KeyInput.IsInPopup(e)) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.F) { Search.Focus(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.OemComma) { await ShowSettingsAsync(); e.Handled = true; }
        else if (e.Source is TextBox) return;
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A && Gallery.IsVisible) { Gallery.SelectAll(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.C && Gallery.IsVisible)
        { await CopyPhotosAsync(Selected); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.Add or Key.OemPlus or Key.Subtract or Key.OemMinus && !Viewer.IsVisible)
        { ResizeThumbnails(e.Key is Key.Add or Key.OemPlus); e.Handled = true; }
        else if (e.Key == Key.Escape && Gallery.IsVisible) { Gallery.ClearSelection(); e.Handled = true; }
        else if (e.Key == Key.Delete && Gallery.IsVisible)
        {
            e.Handled = true;
            await DeleteWithKeyAsync();
        }
        else if (e.Key == Key.F5 && !Viewer.IsVisible)
        {
            var items = Gallery.IsVisible ? Gallery.Items : _library.VisibleLibraryItems.ToList();
            if (items.FirstOrDefault() is { } first) { await OpenAsync(first, items); Viewer.StartSlideshow(); }
            e.Handled = true;
        }
    }
    // Delete takes the selection or, without one, the thumbnail the keyboard has the focus on. The focus then goes to
    // the thumbnail that takes its place, so the keys go on from there.
    private async Task DeleteWithKeyAsync()
    {
        // A held key repeats; one deletion at a time.
        if (_deleting) return;
        var tile = FocusedElement as PhotoTile;
        IReadOnlyList<PhotoItem> targets = Selected is { Count: > 0 } selected ? selected
            : tile != null && KeyInput.ShowsFocus(tile) ? [tile.Item] : [];
        if (targets.Count == 0) return;
        var order = Gallery.Items;
        _deleting = true;
        try
        {
            await DeleteAsync(targets);
            if (tile != null && FocusedElement is not PhotoTile) FocusNearest(order, tile.Item, NavigationMethod.Directional);
        }
        catch (Exception ex) { ShowToast(ex.Message, true); }
        finally { _deleting = false; }
    }
    public void Dispose()
    {
        _openVersion++;
        StopFolderWatcher();
        _hiddenFoldersTimer.Stop();
        _refresh?.Cancel();
        _browse?.Cancel();
        FlowerySizeManager.SizeChanged -= OnCardSizeChanged;
        _library.Changed -= OnLibraryChanged; _library.ScanningChanged -= OnScanning;
        _library.HiddenFoldersChanged -= OnHiddenFoldersChanged;
        _library.ScanProgress -= OnScanProgress;
        _toastTimer.Stop(); Notifications.Items.Clear();
        Viewer.Notification -= ShowToast;
        Viewer.MediaOpened -= UpdateViewerVisibility;
        PhotosLocalization.CultureChanged -= OnCultureChanged; Viewer.Dispose();
    }
}
