using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotosShell : UserControl, IDisposable
{
    private string _page = "Home";
    private readonly PhotoLibrary _library = PhotoLibrary.Instance;
    private Window Owner => (Window)TopLevel.GetTopLevel(this)!;
    private List<PhotoItem> Selected => _library.Items.Where(i => i.IsSelected).ToList();
    private static string L(string key) => PhotosLocalization.GetString("Photos_" + key);

    public PhotosShell()
    {
        InitializeComponent();
        DataContext = PhotosLocalization.Instance;
        Gallery.OpenRequested += item => _ = OpenAsync(item, Gallery.Items);
        Gallery.SelectionChanged += UpdateSelection;
        Gallery.ConfigureTile += ConfigureTile;
        Gallery.ImportRequested += () => _ = ImportAsync();
        Gallery.SlideshowRequested += () => _ = StartSlideshowAsync(Gallery.Items);
        Gallery.BackRequested += () => _ = NavigateAsync(Gallery.Query.Source == PhotoSourceKind.Album ? "Albums" : "Folders");
        Viewer.BackRequested += () => _ = NavigateAsync("Gallery");
        Viewer.ConfirmLeave = CanLeaveViewerAsync;
        Viewer.SettingsRequested += () => _ = ShowSettingsAsync();
        Viewer.DeletePhoto = DeleteAsync;
        Viewer.ImmersiveChanged += immersive =>
        {
            AppHeader.IsVisible = NavigationRail.IsVisible = AppStatus.IsVisible = !immersive;
            Margin = immersive ? new Thickness(0) : new Thickness(0, 42, 0, 0);
        };
        Search.TextChanged += (_, _) =>
        {
            if (Viewer.IsVisible) return;
            if (_page is "Albums" or "Folders") { BuildCollections(); return; }
            _page = "Gallery"; ShowPage(); Gallery.Search(Search.Text);
        };
        HomeScroll.SizeChanged += (_, e) => { if (Math.Abs(_homeWidth - e.NewSize.Width) > 1) BuildHome(); };
        CollectionsScroll.SizeChanged += (_, e) => { if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 1) BuildCollections(); };
        _library.Changed += OnLibraryChanged;
        _library.ScanningChanged += OnScanning;
        PhotosLocalization.CultureChanged += OnCultureChanged;
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
        DragDrop.SetAllowDrop(this, true);
        KeyDown += OnKeyDown;
        ShowPage();
    }
    private void OnScanning(object? sender, bool value) => Status.Text = value ? L("Empty_ScanningTitle") : PhotosLocalization.Format("Photos_ItemCount", _library.Items.Count);
    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Gallery.Refresh(); BuildHome(); BuildCollections(); UpdateSelection();
    }
    private void OnCultureChanged(object? sender, string e)
    {
        FlowDirection = PhotosLocalization.CurrentCulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Gallery.RefreshLabels(); Viewer.RefreshLabels();
        if (!Viewer.IsVisible) ShowPage();
    }
    private async Task NavigateAsync(string page)
    {
        if (!await CanLeaveViewerAsync()) return;
        Viewer.Stop(); _page = page; ShowPage();
    }
    private void ShowPage()
    {
        Viewer.IsVisible = false;
        HomeScroll.IsVisible = _page == "Home";
        Gallery.IsVisible = _page is "Gallery" or "Favorites";
        CollectionsScroll.IsVisible = _page is "Albums" or "Folders";
        if (Gallery.IsVisible) Gallery.Show(new PhotoQuery { Source = _page == "Favorites" ? PhotoSourceKind.Favorites : PhotoSourceKind.All, SearchText = Search.Text }, L("Nav_" + _page));
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
        }
    }
    private DaisyButton ActionButton(string key, Func<Task> action, bool localize = true)
    {
        var button = new DaisyButton { Content = localize ? L(key) : key, Margin = new Thickness(0, 0, 8, 8) };
        AutomationProperties.SetAutomationId(button, "Photos.Action." + key);
        AutomationProperties.SetName(button, localize ? L(key) : key);
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } };
        return button;
    }
    private Task OpenCollectionAsync(PhotoSourceKind kind, string key, string title)
    {
        _page = kind == PhotoSourceKind.Folder ? "Folders" : "Albums";
        UpdateNavigationSelection();
        HomeScroll.IsVisible = false; CollectionsScroll.IsVisible = false; Gallery.IsVisible = true;
        Gallery.Show(new PhotoQuery { Source = kind, SourceKey = key }, title);
        return Task.CompletedTask;
    }
    public async Task OpenFileAsync(string path)
    {
        var item = _library.GetOrCreateItem(path);
        if (item == null) return;
        var folder = Directory.EnumerateFiles(item.FolderPath).Where(ImageMetadataReader.IsSupported)
            .Select(_library.GetOrCreateItem).OfType<PhotoItem>().ToList();
        await OpenAsync(item, folder);
    }
    private async Task OpenAsync(PhotoItem item, IReadOnlyList<PhotoItem> items)
    {
        if (!await CanLeaveViewerAsync()) return;
        HomeScroll.IsVisible = CollectionsScroll.IsVisible = Gallery.IsVisible = false;
        Viewer.IsVisible = true;
        await Viewer.OpenAsync(item, items);
    }
    public async Task<bool> CanLeaveViewerAsync()
    {
        if (Viewer.IsVisible && Viewer.IsBusy) return false;
        if (!Viewer.IsVisible || !Viewer.HasChanges) return true;
        var prompt = new PromptDialog(L("UnsavedTitle"), L("UnsavedText"), alternative: L("Discard"));
        var choice = await prompt.ShowDialog<int>(Owner);
        return choice == 2 || choice == 1 && await Viewer.SaveAsync(false);
    }
    private void ConfigureTile(PhotoTile tile)
    {
        tile.SelectionChanged += UpdateSelection;
        var menu = new ContextMenu();
        void Add(string key, Func<Task> action)
        {
            var item = new MenuItem { Header = L(key) };
            AutomationProperties.SetName(item, L(key));
            AutomationProperties.SetAutomationId(item, "Photos.Context." + key);
            item.Click += async (_, _) => { try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } };
            menu.Items.Add(item);
        }
        Add("Open", () => OpenAsync(tile.Item, Gallery.IsVisible ? Gallery.Items : _library.Items));
        Add("Edit", async () => { await OpenAsync(tile.Item, _library.Items); Viewer.BeginEditing(); });
        Add(tile.Item.IsFavorite ? "Unfavorite" : "Favorite", () => { _library.ToggleFavorite(tile.Item); return Task.CompletedTask; });
        Add("AddToAlbum", () => AddToAlbumAsync([tile.Item]));
        if (Gallery.Query is { Source: PhotoSourceKind.Album, SourceKey: { } name })
            Add("RemoveFromAlbum", () =>
            {
                if (_library.FindAlbum(name) is { } album) _library.RemoveFromAlbum(album, [tile.Item]);
                return Task.CompletedTask;
            });
        Add("Copy", () => FileOperations.CopyFilesAsync(Owner, [tile.Item.Path]));
        Add("ShowInExplorer", () => { FileOperations.ShowInExplorer(tile.Item.Path); return Task.CompletedTask; });
        if (OperatingSystem.IsWindows())
        {
            Add("OpenWith", () => { FileOperations.OpenWith(tile.Item.Path); return Task.CompletedTask; });
            Add("SetAsBackground", () => { FileOperations.SetAsWallpaper(tile.Item.Path); return Task.CompletedTask; });
            Add("Delete", () => DeleteAsync([tile.Item]));
        }
        tile.ContextMenu = menu;
    }
    private async Task AddToAlbumAsync(IReadOnlyList<PhotoItem> items)
    {
        var prompt = new PromptDialog(L("AddToAlbum"), L("AlbumNamePrompt"), _library.Albums.FirstOrDefault()?.Name ?? L("AlbumDefaultName"));
        if (await prompt.ShowDialog<int>(Owner) != 1) return;
        var album = _library.FindAlbum(prompt.Value) ?? _library.CreateAlbum(prompt.Value ?? L("AlbumDefaultName"));
        _library.AddToAlbum(album, items);
    }
    private async Task DeleteAsync(IReadOnlyList<PhotoItem> items)
    {
        if (!OperatingSystem.IsWindows() || items.Count == 0) return;
        if (PhotosSettings.ConfirmDelete && !await ConfirmAsync(L("DeleteTitle"), PhotosLocalization.Format("Photos_DeleteConfirmMany", items.Count))) return;
        Status.Text = PhotosLocalization.Format("Photos_DeletedCount", await _library.DeleteAsync(items));
    }
    private async Task<bool> ConfirmAsync(string title, string message) => await new PromptDialog(title, message).ShowDialog<int>(Owner) == 1;
    private void UpdateSelection()
    {
        var count = Selected.Count;
        BatchFavorite.IsVisible = BatchAlbum.IsVisible = BatchCopy.IsVisible = ClearSelection.IsVisible = count > 0;
        BatchDelete.IsVisible = count > 0 && OperatingSystem.IsWindows();
        Status.Text = PhotosLocalization.Format(count == 1 ? "Photos_SelectedOne" : "Photos_SelectedMany", count);
    }
    private async Task AddFolderAsync()
    {
        var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L("AddFolder"), AllowMultiple = true });
        foreach (var folder in folders) { using (folder) { if (folder.TryGetLocalPath() is { } path) _library.AddFolder(path); } }
    }
    private async Task ImportAsync()
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = L("Import"), AllowMultiple = true, FileTypeFilter = [FilePickerFileTypes.ImageAll] });
        try { await _library.ImportFilesAsync(files.Select(f => f.TryGetLocalPath()).OfType<string>()); }
        finally { foreach (var file in files) file.Dispose(); }
    }
    private async Task ShowSettingsAsync() => await new SettingsView().ShowDialog(Owner);
    private async void NavigateClick(object? sender, RoutedEventArgs e) { if (sender is Control { Tag: string page }) await NavigateAsync(page); }
    private async void SettingsClick(object? sender, RoutedEventArgs e) => await ShowSettingsAsync();
    private async void AddCollectionFolderClick(object? sender, RoutedEventArgs e) => await AddFolderAsync();
    private async void NewAlbumClick(object? sender, RoutedEventArgs e)
    {
        var prompt = new PromptDialog(L("NewAlbum"), L("AlbumNamePrompt"), L("AlbumDefaultName"));
        if (await prompt.ShowDialog<int>(Owner) == 1) _library.CreateAlbum(prompt.Value ?? L("AlbumDefaultName"));
    }
    private void FavoriteClick(object? sender, RoutedEventArgs e)
    {
        var selected = Selected;
        _library.SetFavorite(selected, !selected.All(item => item.IsFavorite));
    }
    private async void AlbumClick(object? sender, RoutedEventArgs e) => await AddToAlbumAsync(Selected);
    private async void CopyClick(object? sender, RoutedEventArgs e) => await FileOperations.CopyFilesAsync(Owner, Selected.Select(i => i.Path).ToList());
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
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path == null) continue;
            if (Directory.Exists(path)) _library.AddFolder(path);
            else if (ImageMetadataReader.IsSupported(path)) await OpenFileAsync(path);
        }
    }
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.F) { Search.Focus(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.OemComma) { await ShowSettingsAsync(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A && Gallery.IsVisible) { Gallery.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Delete && Gallery.IsVisible) { await DeleteAsync(Selected); e.Handled = true; }
        else if (e.Key == Key.F5 && !Viewer.IsVisible)
        {
            var items = Gallery.IsVisible ? Gallery.Items : _library.Items;
            if (items.FirstOrDefault() is { } first) { await OpenAsync(first, items); Viewer.StartSlideshow(); }
            e.Handled = true;
        }
    }
    public void Dispose()
    {
        _library.Changed -= OnLibraryChanged; _library.ScanningChanged -= OnScanning;
        PhotosLocalization.CultureChanged -= OnCultureChanged; Viewer.Dispose();
    }
}
