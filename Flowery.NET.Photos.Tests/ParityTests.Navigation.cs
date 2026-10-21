using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public async Task GalleryScopesSelectionAndPreservesSourceSearchAndViewerReturn()
    {
        var oldState = PhotosSettings.StateDirectory;
        var stateRoot = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        var previousRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", stateRoot);
        PhotosSettings.Initialize(stateRoot);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 1100, Height = 850 };
        try
        {
            var first = CreateImage();
            var second = Path.Combine(Path.GetDirectoryName(first)!, "second.png");
            File.Copy(first, second);
            var other = CreateImage();
            library.AddFolder(Path.GetDirectoryName(first)!);
            library.AddFolder(Path.GetDirectoryName(other)!);
            await library.RescanAsync();
            library.SetFavorite(library.Items, true);
            var album = library.CreateAlbum("test album", library.Items);
            window.Show(); window.UpdateLayout();
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var search = shell.FindControl<TextBox>("Search")!;
            var favorites = shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Favorites");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(favorites)).Invoke();
            gallery.SelectAll();
            search.Text = "second";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.Equal(PhotoSourceKind.Favorites, gallery.Query.Source);
            Assert.Single(gallery.Selected);
            Assert.Equal(second, gallery.Selected[0].Path);
            Assert.All(library.Items.Where(item => item.Path != second), item => Assert.False(item.IsSelected));
            await shell.OpenFileAsync(second);
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            Invoke(viewer, "back");
            Assert.True(gallery.IsVisible);
            Assert.Equal(PhotoSourceKind.Favorites, gallery.Query.Source);
            Assert.Equal("second", gallery.Query.SearchText);
            Assert.Single(gallery.Items);

            gallery.Show(new PhotoQuery { Source = PhotoSourceKind.Album, SourceKey = album.Name }, album.Name);
            Assert.Empty(gallery.Selected);
            gallery.SelectAll();
            search.Text = "original";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.Equal(PhotoSourceKind.Album, gallery.Query.Source);
            Assert.Equal(2, gallery.Items.Count);
            Assert.Equal(2, gallery.Selected.Count);
            Assert.False(library.FindByPath(second)!.IsSelected);
            window.UpdateLayout();
            var tile = gallery.GetVisualDescendants().OfType<PhotoTile>().First();
            Assert.Equal(2, gallery.ContextItems(tile.Item).Count);
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(tile)).Invoke();
            Assert.False(tile.Item.IsSelected);
            Assert.False(viewer.IsVisible);
            Assert.Single(gallery.Selected);
            Assert.Single(gallery.ContextItems(tile.Item));
            gallery.Show(new PhotoQuery { Source = PhotoSourceKind.Folder, SourceKey = Path.GetDirectoryName(first) }, "folder");
            Assert.DoesNotContain(library.Items, item => item.IsSelected);
            Assert.Equal(2, gallery.Items.Count);
        }
        finally
        {
            window.Close();
            await library.DisposeAsync();
            Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", previousRoot);
            PhotosSettings.Initialize(oldState);
        }
    }

    [AvaloniaFact]
    public async Task MultipleExternalFilesKeepTheirProvidedOrderAndExcludeOtherSiblings()
    {
        var first = CreateImage();
        var excluded = Path.Combine(Path.GetDirectoryName(first)!, "excluded.png");
        File.Copy(first, excluded);
        var second = CreateImage();
        using var shell = new PhotosShell();
        var window = new Window { Content = shell, Width = 1100, Height = 850 };
        try
        {
            window.Show();
            await shell.OpenFilesAsync([first, second]);
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            Assert.Equal(first, viewer.Current!.Path);
            await viewer.NavigateAsync(1);
            Assert.Equal(second, viewer.Current!.Path);
            await viewer.NavigateAsync(1);
            Assert.Equal(second, viewer.Current!.Path);
            await viewer.NavigateAsync(-1);
            Assert.Equal(first, viewer.Current!.Path);
            await viewer.NavigateAsync(-1);
            Assert.Equal(first, viewer.Current!.Path);
        }
        finally { window.Close(); }
    }
}
