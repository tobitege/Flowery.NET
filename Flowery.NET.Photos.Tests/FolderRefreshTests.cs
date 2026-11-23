using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia;
using System.Security.AccessControl;
using System.Security.Principal;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class FolderRefreshTests
{
    [AvaloniaFact]
    public async Task RefreshUpdatesEveryMultiItemPageAndReadsOnlyTheCurrentFolderInFolderViews()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var source = Directory.CreateDirectory(Path.Combine(root, "library")).FullName;
        var secondSource = Directory.CreateDirectory(Path.Combine(root, "other-source")).FullName;
        var browsed = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([source, secondSource]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        try
        {
            window.Show();
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var path = CreatePhoto(source, "library.png");
            await AfterChangeAsync(library, () => Invoke(Button(shell, "Photos.Action.Refresh")), () => library.FindByPath(path) != null);

            var album = library.CreateAlbum("Refresh album");
            var collections = shell.FindControl<Grid>("CollectionsScroll")!;
            foreach (var page in new[] { "Gallery", "Favorites", "Albums", "Folders" })
            {
                Invoke(Button(shell, "PhotosShell." + page));
                window.UpdateLayout();
                var button = Button(shell, page is "Gallery" or "Favorites" ? "GalleryView.Refresh" : "PhotosShell.RefreshCollections");
                Assert.True(button.IsEffectivelyVisible);
                Assert.False(gallery.FindControl<DaisyButton>("FolderOptions")!.IsEffectivelyVisible);

                var addedToPage = CreatePhoto(page == "Folders" ? secondSource : source, page + ".png");
                if (page == "Favorites") library.SetFavorite([new PhotoItem(addedToPage)], true);
                if (page == "Albums") library.AddToAlbum(album, [new PhotoItem(addedToPage)]);
                Assert.Null(library.FindByPath(addedToPage));
                await AfterChangeAsync(library, () => Invoke(button), () => library.FindByPath(addedToPage) != null);
                if (page is "Gallery" or "Favorites") Assert.Contains(gallery.Items, item => item.Path == addedToPage);
                if (page == "Albums")
                {
                    Assert.Equal(addedToPage, Assert.Single(library.GetAlbumItems(album)).Path);
                    window.UpdateLayout();
                    Invoke(Assert.Single(collections.GetVisualDescendants().OfType<PhotoCard>()));
                    window.UpdateLayout();
                    Assert.Equal(PhotoSourceKind.Album, gallery.Query.Source);
                    var addedToAlbum = CreatePhoto(source, "album-added.png");
                    library.AddToAlbum(album, [new PhotoItem(addedToAlbum)]);
                    await AfterChangeAsync(library, () => Invoke(Button(shell, "GalleryView.Refresh")),
                        () => gallery.Items.Any(item => item.Path == addedToAlbum));
                }
            }

            window.UpdateLayout();
            Invoke(collections.GetVisualDescendants().OfType<PhotoCard>().Single(card => Equals(ToolTip.GetTip(card), source)));
            Assert.Equal(PhotoSourceKind.Folder, gallery.Query.Source);
            var addedToSource = CreatePhoto(source, "source-added.png");
            var addedToOtherSource = CreatePhoto(secondSource, "other-source-added.png");
            await AfterChangeAsync(library, () => Invoke(Button(shell, "GalleryView.Refresh")),
                () => gallery.Items.Any(item => item.Path == addedToSource));
            Assert.Null(library.FindByPath(addedToOtherSource));

            var first = CreatePhoto(browsed, "first.png");
            await shell.BrowseFolderAsync(browsed);
            window.UpdateLayout();
            gallery.SelectAll();
            var added = CreatePhoto(browsed, "second.png");
            var outsideScope = CreatePhoto(source, "other.png");
            Assert.Single(gallery.Items);
            await AfterChangeAsync(library, () => Invoke(Button(shell, "GalleryView.Refresh")), () => gallery.Items.Any(item => item.Path == added));

            Assert.Equal(first, Assert.Single(gallery.Selected).Path);
            Assert.Null(library.FindByPath(outsideScope));
            Assert.Equal(new[] { source, secondSource }, PhotosSettings.LoadFolders());
            Assert.Equal(PhotoSourceKind.Browsed, gallery.Query.Source);
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WatchingUsesItsOwnSortAndCanKeepTheTopRowOrRevealTheNewestArrival(bool inLibrary)
    {
        var previousState = PhotosSettings.StateDirectory;
        var previousSort = PhotosSettings.SortMode;
        var previousRecursion = PhotosSettings.IncludeSubfolders;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        var date = new DateTime(2024, 6, 1, 12, 0, 0);
        for (var index = 0; index < 36; index++)
            File.SetLastWriteTime(CreatePhoto(folder, $"photo-{index:00}.png"), date.AddMinutes(index));
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders(inLibrary ? [folder] : []);
        PhotosSettings.SortMode = PhotoSortMode.NameDescending;
        PhotosSettings.IncludeSubfolders = true;
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 800, Height = 570 };
        try
        {
            await library.RescanAsync();
            window.Show();
            window.UpdateLayout();
            if (inLibrary)
                Invoke(shell.GetVisualDescendants().OfType<PhotoCard>().Single(card =>
                    AutomationProperties.GetAutomationId(card)?.StartsWith("Photos.Collection.Folder.", StringComparison.Ordinal) == true));
            else
                await shell.BrowseFolderAsync(folder);

            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var watch = gallery.FindControl<DaisyCheckBox>("WatchOpenFolder")!;
            var sort = gallery.FindControl<DaisySelect>("WatchedFolderSort")!;
            var position = gallery.FindControl<DaisySelect>("WatchPosition")!;
            Assert.Equal(PhotoSortMode.DateTakenNewest, (PhotoSortMode)sort.SelectedIndex);
            Assert.Equal(0, position.SelectedIndex);
            await AfterChangeAsync(library, () => watch.IsChecked = true, () => true);
            sort.SelectedIndex = (int)PhotoSortMode.NameAscending;
            Assert.Equal("photo-00.png", gallery.Items[0].FileName);
            Assert.Equal(PhotoSortMode.NameDescending, PhotosSettings.SortMode);

            gallery.FocusPhoto(gallery.Items[18]);
            window.UpdateLayout();
            var scroller = gallery.FindControl<ListBox>("Rows")!.GetVisualDescendants().OfType<ScrollViewer>().First();
            var top = VisibleTiles().MinBy(tile => tile.TranslatePoint(default, scroller)!.Value.Y)!;
            var topY = top.TranslatePoint(default, scroller)!.Value.Y;
            var added = Path.Combine(folder, "first-added.png");
            await AfterChangeAsync(library, () => File.Move(CreatePhoto(folder, "first.part"), added),
                () => gallery.Items.Any(item => item.Path == added));
            window.UpdateLayout();
            var anchored = Assert.Single(VisibleTiles(), tile => tile.Item.Path == top.Item.Path);
            Assert.InRange(anchored.TranslatePoint(default, scroller)!.Value.Y, topY - 2, topY + 2);

            position.SelectedIndex = 1;
            Assert.True(PhotosSettings.RevealNewestArrival);
            var latest = Path.Combine(folder, "zzz-latest.png");
            var earlier = Path.Combine(folder, "aaa-earlier.png");
            var incoming = CreatePhoto(folder, "earlier.part");
            File.SetLastWriteTime(incoming, date.AddDays(4));
            await AfterChangeAsync(library, () =>
            {
                File.Move(incoming, earlier);
                File.SetLastWriteTime(CreatePhoto(folder, Path.GetFileName(latest)), date.AddDays(2));
            }, () => gallery.Items.Any(item => item.Path == latest) && gallery.Items.Any(item => item.Path == earlier));
            window.UpdateLayout();
            Assert.Equal(date.AddDays(2), gallery.Items.Single(item => item.Path == latest).SortDate);
            Assert.Contains(VisibleTiles(), tile => tile.Item.Path == latest);
            Assert.DoesNotContain(VisibleTiles(), tile => tile.Item.Path == earlier);

            if (!inLibrary)
            {
                var staging = Directory.CreateDirectory(Path.Combine(root, "staging")).FullName;
                CreatePhoto(staging, "000-moved.png");
                var moved = Path.Combine(folder, "moved-folder");
                var movedPhoto = Path.Combine(moved, "000-moved.png");
                await AfterChangeAsync(library, () => Directory.Move(staging, moved), () => gallery.Items.Any(item => item.Path == movedPhoto));
                window.UpdateLayout();
                Assert.Contains(VisibleTiles(), tile => tile.Item.Path == movedPhoto);
                Assert.DoesNotContain(VisibleTiles(), tile => tile.Item.Path == latest);
            }

            watch.IsChecked = false;
            Assert.True(gallery.FindControl<DaisySelect>("Sort")!.IsEnabled);
            Assert.Equal("zzz-latest.png", gallery.Items[0].FileName);

            IEnumerable<PhotoTile> VisibleTiles() => gallery.GetVisualDescendants().OfType<PhotoTile>().Where(tile =>
                tile.TranslatePoint(default, scroller) is { } point && point.Y < scroller.Viewport.Height && point.Y + tile.Bounds.Height > 0);
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
            PhotosSettings.SortMode = previousSort;
            PhotosSettings.IncludeSubfolders = previousRecursion;
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedFolderRefreshKeepsTheLastPublishedContents(bool inLibrary)
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        var photo = CreatePhoto(folder, "photo.png");
        var moved = Path.Combine(root, "disconnected");
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders(inLibrary ? [folder] : []);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        try
        {
            if (inLibrary) await library.RescanAsync();
            else await library.BrowseAsync(folder);
            var query = new PhotoQuery { Source = inLibrary ? PhotoSourceKind.Folder : PhotoSourceKind.Browsed, SourceKey = folder };

            var before = Assert.Single(library.Query(query));
            Directory.Move(folder, moved);
            await Assert.ThrowsAnyAsync<IOException>(() => library.RefreshAsync(query, TestContext.Current.CancellationToken));
            Assert.Same(before, Assert.Single(library.Query(query)));

            Directory.Move(moved, folder);
            File.Move(photo, Path.Combine(folder, "renamed.png"));
            await library.RefreshAsync(query, TestContext.Current.CancellationToken);
            Assert.Equal("renamed.png", Assert.Single(library.Query(query)).FileName);
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task FolderRefreshWaitsForAFullScanThenReadsOnlyItsOwnFolder()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(root, "other")).FullName;
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([folder, other]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        var wanted = Path.Combine(folder, "wanted.png");
        var unrelated = Path.Combine(other, "unrelated.png");
        var publications = 0;
        library.Changed += (_, _) =>
        {
            if (++publications != 1) return;
            CreatePhoto(folder, Path.GetFileName(wanted));
            CreatePhoto(other, Path.GetFileName(unrelated));
        };
        try
        {
            var full = library.RescanAsync();
            var query = new PhotoQuery { Source = PhotoSourceKind.Folder, SourceKey = folder };
            await Task.WhenAll(full, library.RefreshAsync(query, TestContext.Current.CancellationToken));
            Assert.Equal(2, publications);
            Assert.Equal(wanted, Assert.Single(library.Query(query)).Path);
            Assert.Null(library.FindByPath(unrelated));
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task BrowsedRefreshReusesUnchangedMetadataWithoutSavingItToTheLibraryCache()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        var first = CreatePhoto(folder, "first.png");
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        try
        {
            await library.BrowseAsync(folder);
            var query = new PhotoQuery { Source = PhotoSourceKind.Browsed, SourceKey = folder };
            Assert.Equal(64, Assert.Single(library.Query(query)).PixelWidth);
            using (var locked = File.Open(first, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var added = CreatePhoto(folder, "added.png");
                await library.RefreshAsync(query, TestContext.Current.CancellationToken);
                var items = library.Query(query);
                Assert.Contains(items, item => item.Path == first && item.PixelWidth == 64);
                Assert.Contains(items, item => item.Path == added && item.PixelWidth == 64);
            }

            CreatePhoto(folder, "first.png", width: 96);
            await library.RefreshAsync(query, TestContext.Current.CancellationToken);
            Assert.Contains(library.Query(query), item => item.Path == first && item.PixelWidth == 96);
            await library.RescanAsync();
            Assert.Equal("{}", await File.ReadAllTextAsync(Path.Combine(root, "metadata-cache.json"), TestContext.Current.CancellationToken));
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task WatcherRefreshesWhileFilesAreStillArriving()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task? writer = null;
        try
        {
            window.Show();
            await shell.BrowseFolderAsync(folder);
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var watch = gallery.FindControl<DaisyCheckBox>("WatchOpenFolder")!;
            await AfterChangeAsync(library, () => watch.IsChecked = true, () => true);
            await AfterChangeAsync(library, () => writer = ProduceFilesAsync(), () => gallery.Items.Count > 0);
            Assert.False(writer!.IsCompleted);
        }
        finally
        {
            await stop.CancelAsync();
            if (writer != null) await writer;
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }

        async Task ProduceFilesAsync()
        {
            var index = 0;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            try
            {
                while (await timer.WaitForNextTickAsync(stop.Token))
                    CreatePhoto(folder, $"arrived-{index++}.png");
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    [AvaloniaFact]
    public async Task RefreshRestartsAnUnavailableWatcherAndClearsTheError()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        var moved = Path.Combine(root, "disconnected");
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        try
        {
            window.Show();
            await shell.BrowseFolderAsync(folder);
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var options = gallery.FindControl<DaisyButton>("FolderOptions")!;
            var error = gallery.FindControl<TextBlock>("FolderWatchError")!;
            var watch = gallery.FindControl<DaisyCheckBox>("WatchOpenFolder")!;
            Directory.Move(folder, moved);
            watch.IsChecked = true;
            Assert.False(options.IsActive);
            Assert.True(error.IsVisible);

            Directory.Move(moved, folder);
            window.UpdateLayout();
            await AfterChangeAsync(library, () => Invoke(Button(shell, "GalleryView.Refresh")), () => true);
            Assert.True(options.IsActive);
            Assert.False(error.IsVisible);
            var path = Path.Combine(folder, "after-reconnect.png");
            await AfterChangeAsync(library, () => CreatePhoto(folder, Path.GetFileName(path)),
                () => gallery.Items.Any(item => item.Path == path));
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeniedSubtreeKeepsItsPreviousItemsAndStillPublishesReadableSiblings(bool inLibrary)
    {
        var previousState = PhotosSettings.StateDirectory;
        var previousRecursion = PhotosSettings.IncludeSubfolders;
        var root = NewTestRoot();
        var opened = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        var restricted = Directory.CreateDirectory(Path.Combine(opened, "restricted"));
        var readable = Directory.CreateDirectory(Path.Combine(opened, "readable")).FullName;
        var previous = CreatePhoto(restricted.FullName, "previous.png");
        PhotosSettings.Initialize(root);
        PhotosSettings.IncludeSubfolders = true;
        PhotosSettings.SaveFolders(inLibrary ? [opened] : []);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        try
        {
            if (inLibrary) await library.RescanAsync();
            await library.BrowseAsync(opened, TestContext.Current.CancellationToken);
            var query = new PhotoQuery { Source = PhotoSourceKind.Browsed, SourceKey = opened };
            Assert.Equal(previous, Assert.Single(library.Query(query)).Path);
            var sibling = CreatePhoto(readable, "sibling.png");

            var restore = DenyListing(restricted);
            try
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => library.RefreshAsync(query, TestContext.Current.CancellationToken));
                Assert.Equal(new[] { previous, sibling }.Order(), library.Query(query).Select(item => item.Path).Order());
                if (inLibrary)
                {
                    await using var reopened = new PhotoLibrary();
                    reopened.Initialize(Dispatcher.UIThread);
                    await reopened.RescanAsync();
                    Assert.Equal(sibling, Assert.Single(reopened.Items).Path);
                }
            }
            finally { restore(); }
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
            PhotosSettings.IncludeSubfolders = previousRecursion;
        }
    }

    private static Action DenyListing(DirectoryInfo directory)
    {
        if (OperatingSystem.IsWindows())
        {
            using var user = WindowsIdentity.GetCurrent();
            var original = directory.GetAccessControl(AccessControlSections.Access);
            var denied = directory.GetAccessControl(AccessControlSections.Access);
            denied.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.ListDirectory, AccessControlType.Deny));
            directory.SetAccessControl(denied);
            return () => directory.SetAccessControl(original);
        }
        var mode = File.GetUnixFileMode(directory.FullName);
        File.SetUnixFileMode(directory.FullName, UnixFileMode.None);
        return () =>
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory.FullName, mode);
        };
    }

    [AvaloniaFact]
    public async Task RefreshBindsTheWatcherToAReplacementDirectory()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        var folder = Directory.CreateDirectory(Path.Combine(root, "open")).FullName;
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        try
        {
            window.Show();
            await shell.BrowseFolderAsync(folder);
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var watch = gallery.FindControl<DaisyCheckBox>("WatchOpenFolder")!;
            await AfterChangeAsync(library, () => watch.IsChecked = true, () => true);

            Directory.Move(folder, Path.Combine(root, "archived"));
            Directory.CreateDirectory(folder);
            window.UpdateLayout();
            await AfterChangeAsync(library, () => Invoke(Button(shell, "GalleryView.Refresh")), () => true);

            var afterRefresh = Path.Combine(folder, "after-refresh.png");
            await AfterChangeAsync(library, () => CreatePhoto(folder, Path.GetFileName(afterRefresh)),
                () => gallery.Items.Any(item => item.Path == afterRefresh));
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task RefreshButtonOnTheNewPageStaysDisabledUntilTheOldScanEnds()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([root]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            window.Show();
            window.UpdateLayout();
            library.ScanningChanged += ChangePageDuringScan;
            Invoke(Button(shell, "Photos.Action.Refresh"));
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var refresh = Button(shell, "GalleryView.Refresh");
            Assert.True(refresh.IsEffectivelyEnabled);
            var path = CreatePhoto(root, "visible.png");
            await AfterChangeAsync(library, () => Invoke(refresh), () => library.FindByPath(path) != null);
        }
        finally
        {
            library.ScanningChanged -= ChangePageDuringScan;
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }

        void ChangePageDuringScan(object? sender, bool scanning)
        {
            if (!scanning)
            {
                library.ScanningChanged -= ChangePageDuringScan;
                cancelled.TrySetResult();
                return;
            }
            Invoke(Button(shell, "PhotosShell.Gallery"));
            window.UpdateLayout();
            Assert.False(Button(shell, "GalleryView.Refresh").IsEffectivelyEnabled);
        }
    }

    [AvaloniaFact]
    public async Task WatchOptionsRollBackWhenTheirStateFileIsLocked()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = NewTestRoot();
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([]);
        PhotosSettings.SaveOptions();
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        try
        {
            window.Show();
            await shell.BrowseFolderAsync(root);
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var watch = gallery.FindControl<DaisyCheckBox>("WatchOpenFolder")!;
            var sort = gallery.FindControl<DaisySelect>("WatchedFolderSort")!;
            var position = gallery.FindControl<DaisySelect>("WatchPosition")!;
            var options = Path.Combine(root, "photos.options.state");
            var previous = File.ReadAllText(options);

            if (OperatingSystem.IsWindows())
            {
                using var locked = File.Open(options, FileMode.Open, FileAccess.Read, FileShare.None);
                CheckFailedSave();
            }
            else
            {
                var previousOptions = options + ".original";
                File.Move(options, previousOptions);
                Directory.CreateDirectory(options);
                try { CheckFailedSave(); }
                finally
                {
                    Directory.Move(options, options + ".unwritable");
                    File.Move(previousOptions, options);
                }
            }
            Assert.Equal(previous, File.ReadAllText(options));
            await AfterChangeAsync(library, () => watch.IsChecked = true, () => true);
            Assert.True(gallery.FindControl<DaisyButton>("FolderOptions")!.IsActive);

            void CheckFailedSave()
            {
                watch.IsChecked = true;
                sort.SelectedIndex = (int)PhotoSortMode.NameAscending;
                position.SelectedIndex = 1;
                Assert.False(watch.IsChecked);
                Assert.Equal((int)PhotoSortMode.DateTakenNewest, sort.SelectedIndex);
                Assert.Equal(0, position.SelectedIndex);
                Assert.False(PhotosSettings.WatchOpenFolder);
                Assert.False(gallery.FindControl<DaisyButton>("FolderOptions")!.IsActive);
                Assert.Equal(DaisyAlertVariant.Error, Assert.Single(shell.FindControl<DaisyToast>("Notifications")!.Items.OfType<DaisyAlert>()).Variant);
            }
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    private static DaisyButton Button(PhotosShell shell, string id) => shell.GetVisualDescendants().OfType<DaisyButton>()
        .Single(button => AutomationProperties.GetAutomationId(button) == id);

    private static void Invoke(Control control) =>
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(control)).Invoke();

    private static string NewTestRoot() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "flowery-photos-refresh-tests", Guid.NewGuid().ToString("N"))).FullName;

    private static string CreatePhoto(string folder, string name, int width = 64)
    {
        var path = Path.Combine(folder, name);
        using var bitmap = new SKBitmap(width, 36);
        bitmap.Erase(SKColors.Red);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
        return path;
    }

    private static async Task AfterChangeAsync(PhotoLibrary library, Action trigger, Func<bool> published)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void CheckChange(object? sender, EventArgs e)
        {
            if (published()) changed.TrySetResult();
        }
        void CheckSettled(object? sender, bool scanning)
        {
            if (!scanning) settled.TrySetResult();
        }
        library.Changed += CheckChange;
        library.ScanningChanged += CheckSettled;
        try
        {
            trigger();
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (library.IsScanning)
                await settled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        finally
        {
            library.Changed -= CheckChange;
            library.ScanningChanged -= CheckSettled;
        }
    }
}
