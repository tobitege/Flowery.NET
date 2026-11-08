using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    // Deleting the photo in the viewer goes back to the gallery at the photo that followed it.
    [AvaloniaFact]
    public async Task DeletingInTheViewerReturnsToTheNextPhoto()
    {
        var previousState = PhotosSettings.StateDirectory;
        var root = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "library");
        var removed = Path.Combine(root, "removed");
        Directory.CreateDirectory(directory); Directory.CreateDirectory(removed);
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([directory]);
        var source = CreateImage();
        for (var index = 0; index < 5; index++)
        {
            var path = Path.Combine(directory, $"photo{index}.png");
            File.Copy(source, path);
            File.SetLastWriteTime(path, new DateTime(2024, 6, 30, 18, 0, 0).AddMinutes(-index));
        }
        var library = new PhotoLibrary(); library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 1100, Height = 850 };
        try
        {
            await library.RescanAsync();
            PhotosSettings.SortMode = PhotoSortMode.DateTakenNewest;
            window.Show(); window.UpdateLayout();
            var navigation = shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Gallery");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(navigation)).Invoke();
            window.UpdateLayout();
            var gallery = shell.FindControl<GalleryView>("Gallery")!;
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            var shown = gallery.Items.ToList();
            Assert.Equal(shown[3].Path, await DeleteInViewer(shown[2]));
            // The last photo has no next one; the previous one takes its place.
            Assert.Equal(shown[3].Path, await DeleteInViewer(shown[4]));

            async Task<string> DeleteInViewer(Models.PhotoItem photo)
            {
                window.UpdateLayout();
                var tile = gallery.GetVisualDescendants().OfType<PhotoTile>().Single(candidate => candidate.Item.Path == photo.Path);
                Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(tile)).Invoke();
                for (var attempt = 0; attempt < 200 && (viewer.Current?.Path != photo.Path || viewer.IsBusy); attempt++)
                    await Task.Delay(25);
                Assert.False(viewer.IsBusy);

                // The shell asks and moves the file to the Recycle Bin; here it goes, and a rescan follows.
                Task? deletion = null;
                viewer.DeletePhoto = items => deletion = DeleteAsync(items);
                Invoke(viewer, "delete");
                await deletion!;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                window.UpdateLayout();

                Assert.True(gallery.IsVisible);
                Assert.DoesNotContain(gallery.Items, item => item.Path == photo.Path);
                return Assert.IsType<PhotoTile>(window.FocusManager!.GetFocusedElement()).Item.Path;
            }
        }
        finally { window.Close(); await library.DisposeAsync(); PhotosSettings.Initialize(previousState); }

        async Task DeleteAsync(IReadOnlyList<Models.PhotoItem> items)
        {
            foreach (var item in items) File.Move(item.Path, Path.Combine(removed, item.FileName));
            await library.RescanAsync();
        }
    }
}
