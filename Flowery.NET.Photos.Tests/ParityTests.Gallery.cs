using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaTheory]
    [InlineData(DaisySize.ExtraSmall, 10)]
    [InlineData(DaisySize.Small, 12)]
    [InlineData(DaisySize.Medium, 14)]
    [InlineData(DaisySize.Large, 18)]
    [InlineData(DaisySize.ExtraLarge, 20)]
    public void TimelineYearRendersAtBodyTextSizeAndFitsTheRail(DaisySize size, double fontSize)
    {
        var previousSize = FlowerySizeManager.CurrentSize;
        var timeline = new GalleryTimeline { Width = 65, HorizontalAlignment = HorizontalAlignment.Right };
        timeline.Resources["DaisyBaseContentBrush"] = Brushes.White;
        timeline.Resources["DaisyPrimaryBrush"] = Brushes.Blue;
        var reference = new TextBlock { Text = "2025", FontSize = fontSize, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 50, 0, 0) };
        var window = new Window { Width = 300, Height = 200, Background = Brushes.Black,
            Content = new Grid { Children = { reference, timeline } } };
        try
        {
            FlowerySizeManager.ApplySize(size);
            timeline.Update([new GalleryPhotoRow { Date = new DateTime(2025, 12, 1), Top = 50, Height = 150, Tiles = [] }], 0, 200);
            window.Show(); window.UpdateLayout();
            using var frame = Capture(window);
            var expected = TextBounds(frame, 0, 150);
            var actual = TextBounds(frame, 235, 300);
            Assert.InRange(Math.Abs(actual.Height - expected.Height), 0, 1);
            Assert.InRange(Math.Abs(actual.Width - expected.Width), 0, 1);
            Assert.True(actual.Left >= timeline.Bounds.Left + 2 && actual.Right <= timeline.Bounds.Right - 2);
        }
        finally { window.Close(); FlowerySizeManager.ApplySize(previousSize); }

        static Rect TextBounds(SKBitmap frame, int left, int right)
        {
            var pixels = new List<Point>();
            for (var y = 20; y < 120; y++)
                for (var x = left; x < right; x++)
                {
                    var color = frame.GetPixel(x, y);
                    if (color.Red > 100 && color.Green > 100 && color.Blue > 100) pixels.Add(new Point(x, y));
                }
            Assert.NotEmpty(pixels);
            return new Rect(new Point(pixels.Min(p => p.X), pixels.Min(p => p.Y)),
                new Point(pixels.Max(p => p.X) + 1, pixels.Max(p => p.Y) + 1));
        }
    }

    [AvaloniaFact]
    public async Task GalleryTimelineGroupingResetAndResizePreserveTheVisiblePhoto()
    {
        var previousState = PhotosSettings.StateDirectory;
        var previousSize = FlowerySizeManager.CurrentSize;
        var path = CreateImage(); var directory = Path.GetDirectoryName(path)!;
        PhotosSettings.Initialize(directory);
        PhotosSettings.SaveFolders([directory]);
        var library = new PhotoLibrary(); library.Initialize(Dispatcher.UIThread);
        for (var index = 0; index < 60; index++) File.Copy(path, Path.Combine(directory, $"image{index:000}.png"));
        var gallery = new GalleryView(library);
        var window = new Window { Content = gallery, Width = 900, Height = 700 };
        try
        {
            await library.RescanAsync();
            for (var index = 0; index < library.Items.Count; index++) library.Items[index].DateTaken = new DateTime(2020 + index % 5, index % 12 + 1, 1);
            PhotosSettings.GroupMode = PhotoGroupMode.Month; PhotosSettings.SortMode = PhotoSortMode.DateTakenNewest;
            gallery.Show(new PhotoQuery(), "gallery"); window.Show(); window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); window.UpdateLayout();
            var timeline = gallery.FindControl<GalleryTimeline>("Timeline")!;
            var rows = gallery.FindControl<ListBox>("Rows")!;
            var scroller = rows.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var provider = Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(timeline));
            provider.SetValue(provider.Maximum / 2); window.UpdateLayout();
            // The topmost visible row and its first photo keep their place on screen.
            var order = gallery.Items.ToList();
            var (anchor, anchorTop) = rows.GetVisualDescendants().OfType<PhotoTile>().Where(tile => tile.IsEffectivelyVisible)
                .Select(tile => (tile.Item, Top: tile.TranslatePoint(default, scroller)!.Value.Y, tile.Bounds.Height))
                .Where(entry => entry.Top + entry.Height > 0).OrderBy(entry => entry.Top).ThenBy(entry => order.IndexOf(entry.Item))
                .Select(entry => (entry.Item, entry.Top)).First();
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge); window.Width = 700; window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); window.UpdateLayout();
            var resized = rows.GetVisualDescendants().OfType<PhotoTile>().Single(tile => tile.IsEffectivelyVisible && tile.Item == anchor);
            Assert.InRange(resized.TranslatePoint(default, scroller)!.Value.Y - anchorTop, -1, 1);
            timeline.Focus(); window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, ""); window.UpdateLayout();
            Assert.InRange(scroller.Offset.Y, scroller.Extent.Height - scroller.Viewport.Height - 1, scroller.Extent.Height);
            gallery.FindControl<DaisySelect>("Sort")!.SelectedIndex = (int)PhotoSortMode.NameAscending;
            Assert.False(timeline.IsVisible);
            Assert.DoesNotContain(rows.Items, item => item is GalleryHeaderRow);
            gallery.Search("no-matching-image"); window.UpdateLayout();
            var reset = gallery.FindControl<DaisyButton>("ResetFilters")!;
            Assert.True(reset.IsVisible);
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(reset)).Invoke();
            Assert.Equal(library.Items.Count, gallery.Items.Count);
        }
        finally { window.Close(); await library.DisposeAsync(); PhotosSettings.Initialize(previousState); FlowerySizeManager.ApplySize(previousSize); }
    }

    // A photo removed from an album or by a rescan rebuilds the gallery; the photo the user sees first stays where it is.
    [AvaloniaFact]
    public async Task GalleryRebuildKeepsTheTopmostVisiblePhotoInPlace()
    {
        var previousState = PhotosSettings.StateDirectory;
        var removed = CreateMixedLibrary();
        var library = new PhotoLibrary(); library.Initialize(Dispatcher.UIThread);
        var gallery = new GalleryView(library);
        // As PhotosShell does.
        library.Changed += (_, _) => gallery.Refresh();
        var window = new Window { Content = gallery, Width = 900, Height = 700 };
        try
        {
            await library.RescanAsync();
            PhotosSettings.GroupMode = PhotoGroupMode.Day; PhotosSettings.SortMode = PhotoSortMode.DateTakenNewest;
            var album = library.CreateAlbum("all", library.Items);
            gallery.Show(new PhotoQuery { Source = PhotoSourceKind.Album, SourceKey = album.Name }, album.Name);
            window.Show(); await Settle();
            var rows = gallery.FindControl<ListBox>("Rows")!;
            var scroller = rows.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.Equal(300, gallery.Items.Count);

            // A photo above the view leaves the album: the same instances, one row fewer or other rows above.
            var (anchor, top) = await ScrollTo(scroller.Extent.Height * .4);
            library.RemoveFromAlbum(album, [gallery.Items[IndexOf(anchor) - 20]]);
            await AssertAt(anchor, top, "a photo above left the album");

            // The topmost photo itself leaves the album: the next one takes its place.
            (anchor, top) = TopPhoto();
            var next = gallery.Items[IndexOf(anchor) + 1].Path;
            library.RemoveFromAlbum(album, [gallery.Items[IndexOf(anchor)]]);
            await AssertAt(next, top, "the topmost photo left the album");

            // A day heading is at the top, so the topmost photo is below it.
            var heading = rows.Items.Cast<GalleryRow>().Select((row, index) => (row, index))
                .First(entry => entry.row is GalleryHeaderRow && entry.row.Top > scroller.Offset.Y + 2000).index;
            rows.ScrollIntoView(heading); await Settle();
            var headingTop = rows.ContainerFromIndex(heading)!.TranslatePoint(default, scroller)!.Value.Y;
            (anchor, top) = await ScrollTo(scroller.Offset.Y + headingTop + 10);
            Assert.True(top > 0, "the heading is not above the topmost photo");
            library.RemoveFromAlbum(album, [gallery.Items[IndexOf(anchor) - 30]]);
            await AssertAt(anchor, top, "a photo above left the album below a heading");

            // A rescan creates new items: the last photo of a day above the view was deleted.
            (anchor, top) = await ScrollTo(scroller.Offset.Y + 1500);
            var lastOfDay = rows.Items.OfType<GalleryHeaderRow>().Last(row => row.Items.Count > 1 && IndexOf(row.Items[^1].Path) < IndexOf(anchor) - 5).Items[^1].Path;
            await MoveOut(lastOfDay);
            await AssertAt(anchor, top, "the last photo of a day above was deleted");

            // A rescan without the topmost photo.
            (anchor, top) = TopPhoto();
            next = gallery.Items[IndexOf(anchor) + 1].Path;
            await MoveOut(anchor);
            await AssertAt(next, top, "the topmost photo was deleted");

            // At the end of the list a photo above was deleted.
            (anchor, top) = await ScrollTo(scroller.Extent.Height);
            await MoveOut(gallery.Items[IndexOf(anchor) - 10].Path);
            await AssertAt(anchor, top, "a photo above was deleted at the end of the list");
            Assert.InRange(scroller.Offset.Y, scroller.Extent.Height - scroller.Viewport.Height - 1, scroller.Extent.Height);
            Assert.Equal(300 - 6, gallery.Items.Count);

            // Another source and a search are new sets of photos: they start at the top.
            await ScrollTo(scroller.Extent.Height / 2);
            gallery.Show(new PhotoQuery(), "all photos");
            await Settle();
            Assert.InRange(scroller.Offset.Y, 0, 1);
            await ScrollTo(scroller.Extent.Height / 2);
            gallery.Search("photo1");
            await Settle();
            Assert.InRange(scroller.Offset.Y, 0, 1);

            int IndexOf(string path) => gallery.Items.Select(item => item.Path).ToList().IndexOf(path);
            IEnumerable<(PhotoTile Tile, double Top)> Tiles() => rows.GetVisualDescendants().OfType<PhotoTile>()
                .Where(tile => tile.IsEffectivelyVisible).Select(tile => (tile, tile.TranslatePoint(default, scroller)!.Value.Y));
            // The topmost visible row and its first photo.
            (string Path, double Top) TopPhoto()
            {
                var visible = Tiles().Where(entry => entry.Top + entry.Tile.Bounds.Height > 0).ToList();
                var first = visible.Min(entry => entry.Top);
                var paths = visible.Where(entry => entry.Top == first).Select(entry => entry.Tile.Item.Path).ToHashSet();
                return (gallery.Items.First(item => paths.Contains(item.Path)).Path, first);
            }
            async Task<(string Path, double Top)> ScrollTo(double offset)
            {
                scroller.Offset = new Vector(0, offset);
                await Settle();
                return TopPhoto();
            }
            async Task AssertAt(string path, double expected, string change)
            {
                await Settle();
                var tiles = Tiles().Where(entry => entry.Tile.Item.Path == path).ToList();
                Assert.True(tiles.Count == 1, $"{Path.GetFileName(path)} is not shown after {change}");
                Assert.True(Math.Abs(tiles[0].Top - expected) <= 1,
                    $"{Path.GetFileName(path)} moved from {expected:F1} to {tiles[0].Top:F1} after {change}");
            }
            async Task MoveOut(string path)
            {
                File.Move(path, Path.Combine(removed, Path.GetFileName(path)));
                await library.RescanAsync();
            }
        }
        finally { window.Close(); await library.DisposeAsync(); PhotosSettings.Initialize(previousState); }

        async Task Settle()
        {
            window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.UpdateLayout();
        }
    }

    // A click on the timeline deep in the list puts the day there at the top, although the list only estimates where
    // the rows it has not shown lie.
    [AvaloniaFact]
    public async Task TimelineJumpPutsTheChosenDayAtTheTop()
    {
        var previousState = PhotosSettings.StateDirectory;
        CreateMixedLibrary();
        var library = new PhotoLibrary(); library.Initialize(Dispatcher.UIThread);
        var gallery = new GalleryView(library);
        var window = new Window { Content = gallery, Width = 900, Height = 700 };
        try
        {
            await library.RescanAsync();
            PhotosSettings.GroupMode = PhotoGroupMode.Day; PhotosSettings.SortMode = PhotoSortMode.DateTakenNewest;
            gallery.Show(new PhotoQuery(), "all photos");
            window.Show(); window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); window.UpdateLayout();
            var rows = gallery.FindControl<ListBox>("Rows")!;
            var scroller = rows.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var timeline = gallery.FindControl<GalleryTimeline>("Timeline")!;
            var provider = Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(timeline));
            var all = rows.Items.Cast<GalleryRow>().ToList();
            foreach (var share in new[] { .7, .3, .55 })
            {
                var day = all.FindIndex(row => row is GalleryHeaderRow && row.Top >= provider.Maximum * share);
                provider.SetValue(all[day].Top); window.UpdateLayout();
                var header = rows.ContainerFromIndex(day);
                Assert.NotNull(header);
                Assert.InRange(header.TranslatePoint(default, scroller)!.Value.Y, -1, 1);
            }
        }
        finally { window.Close(); await library.DisposeAsync(); PhotosSettings.Initialize(previousState); }
    }

    // 300 photos of five aspect ratios on days of one to about a dozen photos, each at a time of its own, so the order
    // is fixed. Returns a folder outside the library.
    private static string CreateMixedLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "library");
        var removed = Path.Combine(root, "removed");
        Directory.CreateDirectory(directory); Directory.CreateDirectory(removed);
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders([directory]);
        string[] sources = [CreateImage(240, 160), CreateImage(160, 240), CreateImage(200, 200), CreateImage(320, 120), CreateImage(150, 260)];
        var random = new Random(149);
        var taken = new DateTime(2024, 6, 30, 18, 0, 0);
        for (var index = 0; index < 300; index++)
        {
            var path = Path.Combine(directory, $"photo{index:000}.png");
            File.Copy(sources[random.Next(sources.Length)], path);
            taken = random.Next(6) == 0 ? taken.AddDays(-1 - random.Next(3)) : taken.AddMinutes(-7);
            File.SetLastWriteTime(path, taken);
        }
        return removed;
    }

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
