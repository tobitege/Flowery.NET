using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class HiddenFolderTests
{
    [AvaloniaFact]
    public async Task FlagsPersistAndApplyToSubfoldersWithoutShowingThemInCombinedViews()
    {
        var previousState = PhotosSettings.StateDirectory;
        var previousRecursion = PhotosSettings.IncludeSubfolders;
        var state = NewState();
        var root = Path.Combine(state, "library");
        var marked = Path.Combine(root, "private");
        var nested = Path.Combine(marked, "nested");
        var separatelyMarked = Path.Combine(marked, "separate");
        var normal = CopyPhoto(Path.Combine(root, "private-next"), "visible.png");
        var first = CopyPhoto(marked, "restricted.png");
        var child = CopyPhoto(nested, "nested.png");
        var separate = CopyPhoto(separatelyMarked, "separate.png");
        PhotosSettings.Initialize(state);
        PhotosSettings.IncludeSubfolders = true;
        PhotosSettings.SaveFolders([root]);
        var library = new PhotoLibrary();
        var restarted = new PhotoLibrary();
        try
        {
            library.Initialize(Dispatcher.UIThread);
            await library.RescanAsync();
            library.SetFavorite(library.Items, true);
            var album = library.CreateAlbum("Mixed", library.Items);
            await library.SetFolderHiddenAsync(marked + Path.DirectorySeparatorChar, true);
            await library.SetFolderHiddenAsync(separatelyMarked, true);
            Assert.True(File.Exists(Path.Combine(state, "photos.hidden-folders.state")));

            Assert.Equal(normal, Assert.Single(library.Query(new PhotoQuery())).Path);
            Assert.Equal(normal, Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Favorites })).Path);
            Assert.Equal(normal, Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Album, SourceKey = album.Name })).Path);
            Assert.Empty(library.Query(new PhotoQuery { SearchText = "restricted" }));
            Assert.Null(library.GetFolderSummaries().Single(folder => folder.Path == marked).Cover);
            Assert.Null(library.GetFolderSummaries().Single(folder => folder.Path == nested).Cover);
            Assert.Equal(first, Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Folder, SourceKey = marked })).Path);
            Assert.Equal(child, Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Folder, SourceKey = nested })).Path);

            await library.BrowseAsync(root);
            Assert.Equal(normal, Assert.Single(Browsed(library, root)).Path);
            await library.BrowseAsync(marked);
            Assert.Equal(new[] { child, first }.Order(), Browsed(library, marked).Select(item => item.Path).Order());
            await library.BrowseAsync(separatelyMarked);
            Assert.Equal(separate, Assert.Single(Browsed(library, separatelyMarked)).Path);

            restarted.Initialize(Dispatcher.UIThread);
            await restarted.RescanAsync();
            Assert.True(restarted.IsFolderExplicitlyHidden(marked));
            Assert.Equal(normal, Assert.Single(restarted.VisibleLibraryItems).Path);
            await restarted.SetFolderHiddenAsync(marked, false);
            Assert.Equal(3, restarted.GetAlbumItems(restarted.FindAlbum(album.Name)!).Count());
            Assert.DoesNotContain(restarted.VisibleLibraryItems, item => item.Path == separate);
        }
        finally
        {
            await library.DisposeAsync();
            await restarted.DisposeAsync();
            PhotosSettings.Initialize(previousState);
            PhotosSettings.IncludeSubfolders = previousRecursion;
        }

        static List<Models.PhotoItem> Browsed(PhotoLibrary library, string folder) =>
            library.Query(new PhotoQuery { Source = PhotoSourceKind.Browsed, SourceKey = folder });
    }

    [AvaloniaFact]
    public async Task FolderFlagHidesHomeAndAlbumPreviewsAndRequiresOpeningTheFolderToViewItsPhoto()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var markedFolder = Path.Combine(state, "marked");
        var markedPhoto = CopyPhoto(markedFolder, "restricted.png");
        var normalFolder = Path.Combine(state, "regular");
        var normalPhoto = CopyPhoto(normalFolder, "visible.png");
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([markedFolder, normalFolder]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 1200, Height = 820 };
        try
        {
            await library.RescanAsync();
            var hiddenItem = library.FindByPath(markedPhoto)!;
            library.SetFavorite([hiddenItem], true);
            library.CreateAlbum("Only marked", [hiddenItem]);
            window.Show();
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            var home = shell.FindControl<StackPanel>("Home")!;
            await shell.BrowseFolderAsync(markedFolder);
            await AfterMarkChangeAsync(library, () => gallery.FindControl<DaisyCheckBox>("HideFolder")!.IsChecked = true);
            Assert.True(library.IsFolderExplicitlyHidden(markedFolder));
            Assert.True(gallery.FindControl<DaisyBadge>("HiddenFolderBadge")!.IsVisible);
            Assert.Equal(markedPhoto, Assert.Single(gallery.Items).Path);
            var defaultLabel = PhotosLocalization.GetString("Photos_Hidden");
            var folderLabel = gallery.FindControl<TextBlock>("HiddenFolderBadgeText")!;
            Assert.Equal(defaultLabel, folderLabel.Text);

            await EditHiddenLabelAsync("Discard this label", accept: false);
            Assert.Equal(defaultLabel, folderLabel.Text);
            var customLabel = new string('W', PhotosSettings.HiddenFolderLabelLimit);
            await EditHiddenLabelAsync(customLabel + " extra", accept: true);
            Assert.Equal(customLabel, folderLabel.Text);
            PhotosSettings.Initialize(state);
            Assert.Equal(customLabel, PhotosSettings.HiddenFolderLabel);

            GoTo("Home");
            Assert.DoesNotContain(home.GetVisualDescendants().OfType<PhotoCard>(), card =>
                AutomationProperties.GetName(card) == hiddenItem.FileName);
            var folderCard = FindMarkedFolderCard();
            Assert.Empty(folderCard.GetVisualDescendants().OfType<PhotoThumbnail>());
            var badge = Assert.Single(folderCard.GetVisualDescendants().OfType<DaisyBadge>(), item => Equals(ToolTip.GetTip(item), customLabel));
            Assert.InRange(badge.Bounds.Width, 1, folderCard.Bounds.Width);
            Assert.DoesNotContain(folderCard.ContextMenu!.Items.OfType<MenuItem>(), item =>
                AutomationProperties.GetAutomationId(item) == "Photos.CollectionAction.Slideshow");
            var albumCard = home.GetVisualDescendants().OfType<PhotoCard>().Single(card =>
                AutomationProperties.GetAutomationId(card)?.StartsWith("Photos.Collection.Album.", StringComparison.Ordinal) == true);
            Assert.Empty(albumCard.GetVisualDescendants().OfType<PhotoThumbnail>());

            await shell.OpenFileAsync(markedPhoto);
            Assert.False(viewer.IsVisible);
            await shell.OpenFilesAsync([markedPhoto, normalPhoto]);
            Assert.Equal(normalPhoto, viewer.Current?.Path);
            Assert.Equal(normalPhoto, Assert.Single(viewer.Items).Path);

            GoTo("Home");
            Invoke(FindMarkedFolderCard());
            Assert.Equal(markedPhoto, Assert.Single(gallery.Items).Path);
            await shell.OpenFileAsync(markedPhoto);
            Assert.Equal(markedPhoto, viewer.Current?.Path);
            Assert.NotNull(viewer.FindControl<PhotoCanvas>("Canvas")!.Bitmap);

            GoTo("Home");
            await EditHiddenLabelAsync(" ", accept: true);
            Assert.Equal(string.Empty, PhotosSettings.HiddenFolderLabel);
            Assert.Contains(FindMarkedFolderCard().GetVisualDescendants().OfType<DaisyBadge>(), item => Equals(ToolTip.GetTip(item), defaultLabel));
            var unmark = FindMarkedFolderCard().ContextMenu!.Items.OfType<MenuItem>().Single(item =>
                AutomationProperties.GetAutomationId(item) == "Photos.CollectionAction.UnhideFolder");
            await AfterMarkChangeAsync(library, () => unmark.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));
            Assert.False(library.IsFolderExplicitlyHidden(markedFolder));
            Assert.Contains(library.VisibleLibraryItems, item => item.Path == markedPhoto);

            void GoTo(string name)
            {
                var button = shell.GetVisualDescendants().OfType<DaisyButton>().Single(candidate =>
                    AutomationProperties.GetAutomationId(candidate) == "PhotosShell." + name);
                Invoke(button);
                window.UpdateLayout();
            }

            PhotoCard FindMarkedFolderCard() => home.GetVisualDescendants().OfType<PhotoCard>()
                .Single(card => Equals(ToolTip.GetTip(card), markedFolder));

            async Task EditHiddenLabelAsync(string value, bool accept)
            {
                GoTo("Photos_Nav_Settings");
                var dialog = Assert.Single(window.OwnedWindows.OfType<SettingsView>());
                var field = dialog.FindControl<TextBox>("HiddenFolderLabel")!;
                Assert.Equal(100, field.MaxLength);
                field.Text = value;
                var id = accept ? "SettingsView.Photos_Ok" : "SettingsView.Photos_Cancel";
                Invoke(dialog.GetVisualDescendants().OfType<DaisyButton>().Single(button => AutomationProperties.GetAutomationId(button) == id));
                await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
            }
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task UnreadableHiddenFolderStateKeepsPreviewsHiddenUntilItCanBeReadAgain()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var marked = Path.Combine(state, "marked");
        CopyPhoto(marked, "hidden.png");
        var normal = CopyPhoto(Path.Combine(state, "normal"), "visible.png");
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([marked, Path.GetDirectoryName(normal)!]);
        var writer = new PhotoLibrary();
        var reopened = new PhotoLibrary();
        try
        {
            writer.Initialize(Dispatcher.UIThread);
            await writer.SetFolderHiddenAsync(marked, true);

            var path = Path.Combine(state, "photos.hidden-folders.state");
            using (var locked = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                reopened.Initialize(Dispatcher.UIThread);
                await reopened.RescanAsync();
                Assert.Equal(2, reopened.Items.Count);
                Assert.NotNull(reopened.HiddenFoldersError);
                Assert.Empty(reopened.VisibleLibraryItems);
                Assert.All(reopened.GetFolderSummaries(), folder => Assert.Null(folder.Cover));
                await Assert.ThrowsAnyAsync<IOException>(() => reopened.SetFolderHiddenAsync(marked, false));
            }

            reopened.SyncHiddenFolders();
            Assert.Null(reopened.HiddenFoldersError);
            Assert.Equal(normal, Assert.Single(reopened.VisibleLibraryItems).Path);
        }
        finally
        {
            await writer.DisposeAsync();
            await reopened.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task HidingAnAliasFiltersTheSameFolderAndItsChildren()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var root = Path.Combine(state, "library");
        var marked = Path.Combine(root, "private");
        var child = Path.Combine(marked, "child");
        var photo = CopyPhoto(child, "hidden.png");
        var normal = CopyPhoto(Path.Combine(root, "public"), "visible.png");
        var alias = @"\\?\" + marked;
        if (!OperatingSystem.IsWindows())
        {
            alias = Path.Combine(state, "alias");
            Directory.CreateSymbolicLink(alias, marked);
        }
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([root, child, Path.GetDirectoryName(normal)!]);
        var library = new PhotoLibrary();
        try
        {
            library.Initialize(Dispatcher.UIThread);
            await library.RescanAsync();
            await library.SetFolderHiddenAsync(alias, true);
            Assert.True(library.IsFolderExplicitlyHidden(marked));
            Assert.Equal(normal, Assert.Single(library.VisibleLibraryItems).Path);
            Assert.Null(library.GetFolderSummaries().Single(folder => folder.Path == child).Cover);
            Assert.Equal(photo, Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Folder, SourceKey = child })).Path);
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task TwoOpenLibrariesMergeFolderChangesAndHideAnAlreadyOpenPreview()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var firstFolder = Path.Combine(state, "first");
        var secondFolder = Path.Combine(state, "second");
        CopyPhoto(firstFolder, "first.png");
        var secondPhoto = CopyPhoto(secondFolder, "second.png");
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([firstFolder, secondFolder]);
        var first = new PhotoLibrary();
        var second = new PhotoLibrary();
        first.Initialize(Dispatcher.UIThread);
        second.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(first);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        try
        {
            await first.RescanAsync();
            window.Show();
            await first.SetFolderHiddenAsync(firstFolder, true);
            await shell.OpenFileAsync(secondPhoto);
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            Assert.Equal(secondPhoto, viewer.Current?.Path);
            Assert.Equal(1d, viewer.Opacity);

            var synchronized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            first.HiddenFoldersChanged += (_, _) => synchronized.TrySetResult();
            await second.SetFolderHiddenAsync(secondFolder, true);
            Assert.True(second.IsFolderExplicitlyHidden(firstFolder));
            await synchronized.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(first.IsFolderExplicitlyHidden(firstFolder));
            Assert.True(first.IsFolderExplicitlyHidden(secondFolder));
            Assert.Empty(first.VisibleLibraryItems);
            Assert.Equal(0d, viewer.Opacity);
            Assert.True(shell.FindControl<StackPanel>("HiddenViewerPanel")!.IsVisible);
        }
        finally
        {
            window.Close();
            await first.DisposeAsync();
            await second.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task RescanExcludesFileLinksAndOpeningRechecksAReplacedDirectoryLink()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var visible = Path.Combine(state, "public");
        var hidden = Path.Combine(state, "private");
        var knownPhoto = CopyPhoto(visible, "photo.png");
        var hiddenPhoto = CopyPhoto(hidden, "photo.png");
        var alias = Path.Combine(state, "alias");
        Directory.CreateSymbolicLink(alias, visible);
        File.CreateSymbolicLink(Path.Combine(visible, "link.png"), hiddenPhoto);
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([visible, hidden, alias]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 900, Height = 650 };
        try
        {
            await library.RescanAsync();
            await library.SetFolderHiddenAsync(hidden, true);
            var aliasPhoto = Path.Combine(alias, "photo.png");
            var indexed = Assert.Single(library.VisibleLibraryItems, item => item.Path == aliasPhoto);
            Assert.Equal(FolderIdentity.Resolve(knownPhoto), indexed.ResolvedPath);
            Assert.DoesNotContain(library.VisibleLibraryItems, item => item.FileName == "link.png");

            window.Show();
            await shell.OpenFilesAsync([knownPhoto, aliasPhoto]);
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            Assert.Equal(knownPhoto, viewer.Current?.Path);
            Assert.Equal(2, viewer.Items.Count);
            Directory.Move(alias, Path.Combine(state, "archived-link"));
            Directory.CreateSymbolicLink(alias, hidden);
            Assert.Null(await library.ResolveVisiblePathAsync(indexed, null, TestContext.Current.CancellationToken));
            await viewer.NavigateAsync(1);
            Assert.Equal(knownPhoto, viewer.Current?.Path);

            Invoke(shell.GetVisualDescendants().OfType<DaisyButton>().Single(button =>
                AutomationProperties.GetAutomationId(button) == "PhotosShell.Home"));
            await shell.OpenFileAsync(aliasPhoto);
            Assert.False(viewer.IsVisible);

            await library.RescanAsync();
            Assert.Equal(knownPhoto, Assert.Single(library.VisibleLibraryItems).Path);
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task ApplyingMarkingsUsesTheLastScanEvenWhenAVisibleFolderIsUnreachable()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var visible = Path.Combine(state, "public");
        var hidden = Path.Combine(state, "private");
        var knownPhoto = CopyPhoto(visible, "visible.png");
        CopyPhoto(hidden, "private.png");
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([visible, hidden]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        try
        {
            await library.RescanAsync();
            Directory.Move(visible, Path.Combine(state, "offline"));
            await library.SetFolderHiddenAsync(hidden, true);
            Assert.Equal(knownPhoto, Assert.Single(library.Query(new PhotoQuery())).Path);
            Assert.NotNull(library.GetFolderSummaries().Single(folder => folder.Path == visible).Cover);
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    [AvaloniaFact]
    public async Task AReadableFileWithoutAnIdentityHasAPreviewOnlyWithoutHiddenFolders()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = NewState();
        var folder = Path.Combine(state, "media");
        CopyPhoto(folder, "photo.png");
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([folder]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        try
        {
            await library.RescanAsync();
            var photo = Assert.Single(library.Items);
            // Model a share that allows file reads but does not return a normalized path.
            photo.Location = null;
            photo.ResolvedPath = null;
            var visible = Assert.Single(library.VisibleLibraryItems);
            using var thumbnail = await ThumbnailLoader.LoadAsync(visible, 160, TestContext.Current.CancellationToken);
            Assert.NotNull(thumbnail);

            await library.SetFolderHiddenAsync(folder, true);
            Assert.Empty(library.VisibleLibraryItems);
        }
        finally
        {
            await library.DisposeAsync();
            PhotosSettings.Initialize(previousState);
        }
    }

    private static async Task AfterMarkChangeAsync(PhotoLibrary library, Action trigger)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs e) => changed.TrySetResult();
        library.Changed += OnChanged;
        try
        {
            trigger();
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally { library.Changed -= OnChanged; }
    }

    private static void Invoke(Control control) =>
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(control)).Invoke();

    private static string NewState() =>
        Path.Combine(Path.GetTempPath(), "flowery-photos-hidden-folder-tests", Guid.NewGuid().ToString("N"));

    private static string CopyPhoto(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, name);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Media", "photo.png"), target);
        return target;
    }
}
