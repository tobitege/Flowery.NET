using Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
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

/// <summary>Videos, animations and container metadata, with real files from ffmpeg and ImageMagick that Media/make_media.py writes.</summary>
public sealed class MediaTests
{
    private static string Media(string name) => Path.Combine(AppContext.BaseDirectory, "Media", name);

    // The EXIF block that make_media.py writes into every photo.
    [Theory]
    [InlineData("photo.avif")]
    public async Task PhotoContainersGiveTheirExifDateCameraAndPlace(string name)
    {
        var probe = await ImageMetadataReader.ProbeAsync(Media(name));

        Assert.NotNull(probe);
        Assert.Equal((64, 36), (probe.Value.Width, probe.Value.Height));
        Assert.Equal(new DateTime(2023, 4, 5, 6, 7, 8), probe.Value.DateTaken);
        Assert.Equal("Flowery Test Camera", probe.Value.CameraModel);
        Assert.Equal(52.52, probe.Value.Details?.Latitude ?? 0, 6);
        Assert.Equal(13.405, probe.Value.Details?.Longitude ?? 0, 6);
    }

    [Fact]
    public async Task RotatedMp4IsUprightAndGivesItsQuickTimeMetadata()
    {
        var probe = await ImageMetadataReader.ProbeAsync(Media("clip.mp4"));

        Assert.NotNull(probe);
        // The display matrix turns the 64 × 36 frames a quarter turn, as phones record upright videos.
        Assert.Equal((36, 64), (probe.Value.Width, probe.Value.Height));
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc).ToLocalTime(), probe.Value.DateTaken);
        var details = Assert.IsType<PhotoDetails>(probe.Value.Details);
        Assert.Equal(("H.264", 10d, 1d), (details.Codec, details.FrameRate, details.DurationSeconds));
        Assert.Equal((52.52, 13.405), (details.Latitude, details.Longitude));
    }

    [Theory]
    [InlineData("clip.mkv", "H.264", true)]
    public async Task MatroskaHeadersGiveSizeCodecFrameRateDurationAndDate(string name, string codec, bool dated)
    {
        var probe = await ImageMetadataReader.ProbeAsync(Media(name));

        Assert.NotNull(probe);
        Assert.Equal((64, 36), (probe.Value.Width, probe.Value.Height));
        var details = Assert.IsType<PhotoDetails>(probe.Value.Details);
        Assert.Equal((codec, 10d), (details.Codec, details.FrameRate));
        Assert.Equal(1, details.DurationSeconds ?? 0, 1);
        Assert.Equal(dated ? new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc).ToLocalTime() : null, probe.Value.DateTaken);
    }

    [AvaloniaFact]
    public async Task LibraryFindsVideosFiltersThemAndRereadsEntriesCachedBeforeTheNewReaders()
    {
        var root = Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "library");
        Directory.CreateDirectory(folder);
        var photo = Path.Combine(folder, "photo.png");
        File.Copy(Media("photo.png"), photo);
        File.Copy(Media("clip.mp4"), Path.Combine(folder, "clip.mp4"));
        // Before the PNG reader the cache held this photo without its date and without a version.
        var info = new FileInfo(photo);
        await File.WriteAllTextAsync(Path.Combine(root, "metadata-cache.json"), System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, object> { [photo] = new { Size = info.Length, Ticks = info.LastWriteTimeUtc.Ticks, Width = 64, Height = 36 } }),
            TestContext.Current.CancellationToken);
        var oldRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", root);
        PhotosSettings.Initialize(root);
        var library = new PhotoLibrary();
        try
        {
            library.Initialize(Dispatcher.UIThread);
            library.AddFolder(folder);
            await library.RescanAsync();

            Assert.Equal(new DateTime(2023, 4, 5, 6, 7, 8), library.Items.Single(item => item.Path == photo).DateTaken);
            var video = Assert.Single(library.Query(new PhotoQuery { TypeFilter = PhotoTypeFilter.Video }));
            Assert.True(video.IsVideo);
            Assert.Equal((36, 64, 1d), (video.PixelWidth, video.PixelHeight, video.Details?.DurationSeconds));
            Assert.Empty(library.Query(new PhotoQuery { TypeFilter = PhotoTypeFilter.Other }));
            Assert.Equal(photo, Assert.Single(library.Query(new PhotoQuery { TypeFilter = PhotoTypeFilter.Png })).Path);
        }
        finally { await library.DisposeAsync(); Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", oldRoot); }
    }

    [Fact]
    public void AnimatedGifGivesEachFrameOverThePreviousWithItsDuration()
    {
        using var animation = AnimatedImage.TryOpen(Media("animated.gif"));

        Assert.NotNull(animation);
        Assert.Equal((3, -1), (animation.FrameCount, animation.Repetitions));
        Assert.Equal(TimeSpan.FromMilliseconds(300), animation.TotalDuration);
        SKColor[] colors = [SKColors.Red, SKColors.Lime, SKColors.Blue];
        // Backwards, so every frame is decoded on top of a frame that is not the one before it in the cache.
        for (var index = 2; index >= 0; index--)
        {
            using var frame = animation.CopyFrame(index);
            Assert.Equal(colors[index], frame.GetPixel(8, 8));
        }
        Assert.Null(AnimatedImage.TryOpen(Media("photo.png")));
    }

    [AvaloniaFact]
    public async Task ViewerOffersPlaybackAndKeepingButNoEditingForAVideo()
    {
        var item = new PhotoItem(Media("clip.mp4")) { PixelWidth = 36, PixelHeight = 64 };
        item.Details = VideoMetadataReader.Probe(item.Path)?.Details;
        var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 900, Height = 700 };
        try
        {
            window.Show();
            await viewer.OpenAsync(item, [item]);
            window.UpdateLayout();

            Assert.False(viewer.FindControl<TextBlock>("ErrorText")!.IsVisible, viewer.FindControl<TextBlock>("ErrorText")!.Text);
            Assert.True(viewer.FindControl<VideoPlayer>("Video")!.IsVisible);
            if (VideoPlayer.CanPlay)
            {
                // The bar follows the playing video; it stopped following when the player did not report playing at once.
                var play = viewer.GetVisualDescendants().OfType<DaisyButton>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == "ViewerView.Video.play");
                await WaitUntilAsync(() => AutomationProperties.GetName(play) == Localization.PhotosLocalization.GetString("Photos_Pause"));
            }
            var actions = viewer.GetVisualDescendants().OfType<DaisyButton>().Where(button => button.Tag is string)
                .ToDictionary(button => (string)button.Tag!, button => button.IsEnabled);
            Assert.False(actions["edit"] || actions["slideshow"]);
            Assert.True(actions["favorite"] && actions["info"] && actions["delete"] && actions["left"] && actions["right"]);
            Assert.True(actions["zoomIn"] && actions["zoomOut"] && actions["fit"] && actions["actual"]);
            Assert.True(viewer.FindControl<Control>("ZoomSlider")!.IsVisible);
            void Run(string action) => Invoke(viewer.GetVisualDescendants().OfType<DaisyButton>().First(button => button.Tag as string == action));
            // A video turns for the view only.
            Run("right");
            var player = viewer.FindControl<VideoPlayer>("Video")!;
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            Assert.Equal(1, player.Turns);
            // Stopping belongs to thumbnails; the viewer goes back instead.
            Assert.False(player.GetVisualDescendants().OfType<DaisyButton>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "ViewerView.Video.stop").IsVisible);
            Assert.Equal(1, canvas.State.Rotation);
            Assert.False(viewer.HasChanges);
            if (VideoPlayer.CanPlay)
            {
                // The frame lies where the canvas shows the still, turned and zoomed with it. It was turned around its own
                // corner and moved out of the player before.
                var frame = player.GetVisualDescendants().OfType<Image>().First();
                await WaitUntilAsync(() => frame.Source != null);
                Rect Shown()
                {
                    window.UpdateLayout();
                    var first = frame.TranslatePoint(default, canvas)!.Value;
                    var last = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), canvas)!.Value;
                    var shown = new Rect(first, last).Normalize();
                    Assert.Equal(canvas.ImageBounds.Center.X, shown.Center.X, tolerance: 1);
                    Assert.Equal(canvas.ImageBounds.Center.Y, shown.Center.Y, tolerance: 1);
                    Assert.True(shown.Width <= canvas.ImageBounds.Width + 1 && shown.Height <= canvas.ImageBounds.Height + 1, $"{shown} in {canvas.ImageBounds}");
                    return shown;
                }
                var fitted = Shown();
                Assert.True(fitted.Width > fitted.Height, $"{fitted}");
                Run("zoomIn"); Run("zoomIn");
                Assert.True(Shown().Width > fitted.Width * 1.1, $"{Shown()} after {fitted}");
                Assert.True(canvas.Zoom > 0);

                // A click on the picture pauses it; dragging and the wheel are the canvas's.
                var play = player.GetVisualDescendants().OfType<DaisyButton>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == "ViewerView.Video.play");
                var center = canvas.TranslatePoint(canvas.ImageBounds.Center, window)!.Value;
                window.MouseDown(center, Avalonia.Input.MouseButton.Left); window.MouseUp(center, Avalonia.Input.MouseButton.Left);
                await WaitUntilAsync(() => AutomationProperties.GetName(play) == Localization.PhotosLocalization.GetString("Photos_Play"));
                Run("fit");
                Assert.Equal(fitted, Shown());

                // A double click zooms in, as on a photo, and leaves the video paused; its first click played it before.
                var aside = center + new Point(10, 0);
                for (var click = 0; click < 2; click++)
                {
                    window.MouseDown(aside, Avalonia.Input.MouseButton.Left); window.MouseUp(aside, Avalonia.Input.MouseButton.Left);
                }
                Assert.Equal(100, canvas.Zoom);
                await Task.Delay(300, TestContext.Current.CancellationToken);
                Assert.Equal(Localization.PhotosLocalization.GetString("Photos_Play"), AutomationProperties.GetName(play));
                Run("fit");
            }
            viewer.BeginEditing();
            Assert.False(viewer.FindControl<Border>("SidePanel")!.IsVisible);
            Assert.EndsWith("0:01", viewer.FindControl<TextBlock>("ImageMetadata")!.Text);
        }
        finally { viewer.Dispose(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task VideoTilePlaysInPlaceAndOpensTheViewerFromItsButtonOrInFullScreenOnEnter()
    {
        await using var gallery = await GalleryWindow.ShowAsync(("clip.mp4", "clip.mp4"), ("photo.png", "photo.png"));
        var (shell, window) = (gallery.Shell, gallery.Window);
        var video = Path.Combine(gallery.Folder, "clip.mp4");
        var viewer = shell.FindControl<ViewerView>("Viewer")!;
        PhotoTile Tile() => shell.FindControl<GalleryView>("Gallery")!.GetVisualDescendants().OfType<PhotoTile>().Single(tile => tile.Item.Path == video);
        bool Playing() => Tile().GetVisualDescendants().OfType<VideoPlayer>().Any(player => player.IsVisible);

        // The name shows in the bottom row while the pointer is over the thumbnail, not as a tooltip.
        bool NameShows() => Tile().GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "clip.mp4" && text.IsEffectivelyVisible);
        Assert.Null(ToolTip.GetTip(Tile()));
        Assert.False(NameShows());
        window.MouseMove(Tile().TranslatePoint(new Point(Tile().Bounds.Width / 2, Tile().Bounds.Height / 2), window)!.Value);
        Assert.True(NameShows());

        Invoke(Tile());
        if (VideoPlayer.CanPlay)
        {
            Assert.True(Playing());
            Assert.False(viewer.IsVisible);
            // A second click pauses in place.
            Invoke(Tile());
            Assert.True(Playing());
            Assert.False(viewer.IsVisible);
            var open = Tile().GetVisualDescendants().OfType<DaisyButton>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "Photos.Tile.Open");
            Invoke(open);
        }
        await WaitUntilAsync(() => viewer is { IsVisible: true, IsBusy: false } && viewer.Current?.Path == video);
        Assert.NotEqual(WindowState.FullScreen, window.WindowState);
        Assert.Null(PlayingTile());
        // The search box has nothing to search while one file is open.
        var search = shell.FindControl<TextBox>("Search")!;
        Assert.False(search.IsVisible);

        // A video opens without waiting for a still, before the viewer's first layout.
        window.UpdateLayout();
        Invoke(viewer.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "back"));
        await WaitUntilAsync(() => !viewer.IsVisible);
        Assert.True(search.IsVisible);
        window.UpdateLayout();
        Tile().Focus();
        window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Enter, null);
        await WaitUntilAsync(() => viewer is { IsVisible: true, IsBusy: false });
        Assert.Equal(WindowState.FullScreen, window.WindowState);
        viewer.Stop();

        VideoPlayer? PlayingTile() => shell.FindControl<GalleryView>("Gallery")!.GetVisualDescendants().OfType<VideoPlayer>().FirstOrDefault();
    }

#if WINDOWS
    [Theory]
    [InlineData("clip.mp4")]
    public void AVideoStillComesFromThePlayerUprightAndAtItsSize(string name)
    {
        // The player, not the shell, whose thumbnails waited twenty seconds after the folder dialog of Browse… on a
        // network drive.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var still = PhotoDecoder.Load(Media(name), 400);
        var details = VideoMetadataReader.Probe(Media(name))!.Value;
        Assert.Equal(details.Width > details.Height, still.Width > still.Height);
        Assert.Equal(details.Width / (double)details.Height, still.Width / (double)still.Height, 1);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"{watch.Elapsed}");
        // A frame, not an empty or black picture: the test clips are coloured bars.
        var row = Enumerable.Range(0, 8).Select(step => still.GetPixel(step * still.Width / 8, still.Height / 2)).ToList();
        Assert.True(row.Any(pixel => pixel.Red + pixel.Green + pixel.Blue > 200), $"{still.Width}x{still.Height} {string.Join(" ", row)}");
    }

    [Theory]
    [InlineData("a#b", "c#d.mp4")]
    public async Task AVideoOpensFromAPathWithCharactersThatMeanSomethingInAUri(string folderName, string fileName)
    {
        var folder = Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N"), folderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.Copy(Media("clip.mp4"), path);
        Assert.Equal(path, WindowsVideoPlayback.FileUri(path).LocalPath);

        // The player opens it by URI, which skips the shell that a folder dialog can keep busy for seconds.
        var playback = new WindowsVideoPlayback(64);
        var opened = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        playback.Opened += error => opened.TrySetResult(error);
        try
        {
            await playback.OpenAsync(path);
            Assert.Null(await opened.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        }
        finally { await playback.DisposeAsync(); }
    }

    [Fact]
    public async Task PlaybackNeverMakesItsCallerWaitForTheWindowsPlayer()
    {
        var playback = new WindowsVideoPlayback(64);
        using var release = new ManualResetEventSlim();
        // The player is busy, as it can be for seconds while the system reads the stills of many videos on a network
        // drive. Clicking a video then made the app stop answering until the gallery had loaded.
        var busy = playback.Run(_ => release.Wait());
        var watch = System.Diagnostics.Stopwatch.StartNew();
        playback.IsMuted = true;
        playback.Play();
        Assert.True(playback.IsPlaying);
        _ = playback.Position + playback.Duration;
        playback.Position = TimeSpan.FromSeconds(1);
        var opening = playback.OpenAsync(Media("clip.mp4"));
        playback.Pause();
        var closing = playback.DisposeAsync();
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
        Assert.False(closing.IsCompleted);

        // Once the player is free, the calls run in order and closing lets go of it.
        release.Set();
        await busy.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await closing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(opening.IsCompleted);
    }
#endif

    // A copy of a medium in a folder of its own, so no thumbnail of it is cached yet.
    private static string Fresh(string medium, string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.Copy(Media(medium), path);
        return path;
    }

    [AvaloniaFact]
    public async Task ThumbnailsComeFromTheDiskCacheOnceMadeAndTheCacheKeepsItsSize()
    {
        var path = Fresh("photo.png", "cached.png");
        var item = new PhotoItem(path) { DateModified = File.GetLastWriteTime(path), FileSize = new FileInfo(path).Length };
        var token = TestContext.Current.CancellationToken;
        using (var made = await ThumbnailLoader.LoadAsync(item, 180, token)) Assert.NotNull(made);

        // The file no longer decodes, so a thumbnail can only come from the cache on disk, as after a restart.
        await File.WriteAllTextAsync(path, "not a picture any more", token);
        ThumbnailLoader.ClearMemory();
        using (var cached = await ThumbnailLoader.LoadAsync(item, 180, token)) Assert.NotNull(cached);

        // Pruning keeps the most recently used thumbnails within the size.
        var previous = PhotosSettings.StateDirectory;
        PhotosSettings.Initialize(Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N")));
        try
        {
            foreach (var key in new[] { "old", "middle", "new" })
            {
                ThumbnailDiskCache.Write(key, new byte[1000]);
                foreach (var file in Directory.GetFiles(ThumbnailDiskCache.Folder))
                    File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddMinutes(-1));
            }
            Assert.NotNull(ThumbnailDiskCache.Read("old"));
            ThumbnailDiskCache.Prune(2500);
            Assert.NotNull(ThumbnailDiskCache.Read("old"));
            Assert.NotNull(ThumbnailDiskCache.Read("new"));
            Assert.Null(ThumbnailDiskCache.Read("middle"));
        }
        finally { PhotosSettings.Initialize(previous); }
    }

    [Fact]
    public async Task TheDiskCacheIsPrunedAgainDuringALongSessionAndDropsWritesACrashCutShort()
    {
        var previous = PhotosSettings.StateDirectory;
        PhotosSettings.Initialize(Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N")));
        try
        {
            Directory.CreateDirectory(ThumbnailDiskCache.Folder);
            var stale = Path.Combine(ThumbnailDiskCache.Folder, "cut-short.webp.0.tmp");
            var writing = Path.Combine(ThumbnailDiskCache.Folder, "writing.webp.1.tmp");
            await File.WriteAllBytesAsync(stale, new byte[10], TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(writing, new byte[10], TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-2));

            // The cache was pruned once a session, however much a session wrote, and left such halves for good.
            ThumbnailDiskCache.Write("large", new byte[ThumbnailDiskCache.PruneEvery]);
            for (var waited = 0; File.Exists(stale); waited += 20)
            {
                Assert.True(waited < 5000, "The cache was not pruned.");
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            Assert.True(File.Exists(writing));
            Assert.NotNull(ThumbnailDiskCache.Read("large"));
        }
        finally { PhotosSettings.Initialize(previous); }
    }

    [AvaloniaFact]
    public async Task AThumbnailCutShortInTheDiskCacheIsMadeAgainFromItsFile()
    {
        var path = Fresh("photo.png", "cut-short.png");
        var item = new PhotoItem(path) { DateModified = File.GetLastWriteTime(path), FileSize = new FileInfo(path).Length };
        var token = TestContext.Current.CancellationToken;
        var previous = PhotosSettings.StateDirectory;
        PhotosSettings.Initialize(Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N")));
        try
        {
            using (var made = await ThumbnailLoader.LoadAsync(item, 180, token)) Assert.NotNull(made);
            var cached = Assert.Single(Directory.GetFiles(ThumbnailDiskCache.Folder, "*.webp"));

            // A crash or a full disk left half a file behind; it once took the app down when its thumbnail came up.
            var bytes = await File.ReadAllBytesAsync(cached, token);
            await File.WriteAllBytesAsync(cached, bytes[..16], token);
            ThumbnailLoader.ClearMemory();
            using (var again = await ThumbnailLoader.LoadAsync(item, 180, token)) Assert.NotNull(again);
            Assert.Equal(bytes.Length, new FileInfo(cached).Length);
        }
        finally
        {
            ThumbnailLoader.ClearMemory();
            PhotosSettings.Initialize(previous);
        }
    }

    [AvaloniaFact]
    public async Task ABrowseThatEndsWhileAPhotoIsOpenLeavesTheViewerInFront()
    {
        await using var gallery = await GalleryWindow.ShowAsync(("photo.png", "photo.png"));
        var shell = gallery.Shell;
        var elsewhere = Path.GetDirectoryName(Fresh("exif.jpg", "elsewhere.jpg"))!;
        // On a network drive reading a folder can take minutes; the user opens a photo meanwhile.
        var browsing = shell.BrowseFolderAsync(elsewhere);
        await shell.OpenFileAsync(Path.Combine(gallery.Folder, "photo.png"));
        await browsing;
        Assert.True(shell.FindControl<ViewerView>("Viewer")!.IsVisible);
        Assert.False(shell.FindControl<GalleryView>("Gallery")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task APlayingThumbnailPlaysOnWhenTheGalleryBuildsItsRowsAnew()
    {
        if (!VideoPlayer.CanPlay) return;
        await using var gallery = await GalleryWindow.ShowAsync(("clip.mp4", "clip.mp4"), ("photo.png", "photo.png"));
        var view = gallery.Shell.FindControl<GalleryView>("Gallery")!;
        PhotoTile? Tile() => view.GetVisualDescendants().OfType<PhotoTile>().SingleOrDefault(tile => tile.Item.IsVideo);
        void Settle() { gallery.Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        static TimeSpan PositionOf(VideoPlayer player) => TimeSpan.FromSeconds(player.GetVisualDescendants().OfType<DaisyRange>()
            .Single(range => AutomationProperties.GetAutomationId(range) == "ViewerView.Video.Position").Value);
        var before = Tile()!;
        Invoke(before);
        var player = before.GetVisualDescendants().OfType<VideoPlayer>().Single();

        // The end of a scan builds the rows anew; the playing thumbnail left the tree, and its video stopped, which on
        // a network drive happened to every video started while the library was still being read.
        await gallery.Library.RescanAsync();
        Settle();
        var after = Tile()!;
        Assert.NotSame(before, after);
        Assert.Same(player, after.GetVisualDescendants().OfType<VideoPlayer>().SingleOrDefault());
        Assert.True(player.IsVisible);

        // Deleting another file from the list builds the rows anew as well.
        var position = TimeSpan.Zero;
        await WaitUntilAsync(() => (position = PositionOf(player)) > TimeSpan.Zero);
        var photo = gallery.Library.Items.Single(item => !item.IsVideo);
        Assert.Equal(1, await gallery.Library.DeleteAsync([photo]));
        Settle();
        Assert.Same(player, Tile()!.GetVisualDescendants().OfType<VideoPlayer>().SingleOrDefault());
        Assert.True(player.IsVisible);
        Assert.True(PositionOf(player) >= position, $"{PositionOf(player)} after {position}");

        // A thumbnail that leaves for good takes its video along.
        view.Search("photo.png");
        Settle();
        Assert.Null(Tile());
        Assert.False(player.IsVisible);
    }

    [AvaloniaFact]
    public async Task DeletingAVideoThatPlaysInItsThumbnailStopsItAndDeletesTheFile()
    {
        if (!VideoPlayer.CanPlay || !OperatingSystem.IsWindows()) return;
        await using var gallery = await GalleryWindow.ShowAsync(("clip.mp4", "clip.mp4"), ("photo.png", "photo.png"));
        var view = gallery.Shell.FindControl<GalleryView>("Gallery")!;
        var tile = view.GetVisualDescendants().OfType<PhotoTile>().Single(tile => tile.Item.IsVideo);
        Invoke(tile);
        var player = tile.GetVisualDescendants().OfType<VideoPlayer>().Single();
        var position = player.GetVisualDescendants().OfType<DaisyRange>()
            .Single(range => AutomationProperties.GetAutomationId(range) == "ViewerView.Video.Position");
        await WaitUntilAsync(() => position.Value > 0);

        // The video stops before its file goes, so the player lets go of it first; it used to play on from a deleted
        // file, which on some drives also keeps the file from going.
        var confirm = PhotosSettings.ConfirmDelete;
        PhotosSettings.ConfirmDelete = false;
        try
        {
            tile.Item.IsSelected = true;
            Invoke(gallery.Shell.FindControl<DaisyButton>("BatchDelete")!);
            Assert.False(player.IsVisible);
            Assert.True(File.Exists(tile.Item.Path));
            await WaitUntilAsync(() => !File.Exists(tile.Item.Path) && !gallery.Library.Items.Any(item => item.IsVideo));
        }
        finally { PhotosSettings.ConfirmDelete = confirm; }
    }

    private static void Invoke(Control control) =>
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(control)).Invoke();

    /// <summary>The app on its gallery page, over a library folder with copies of the media files.</summary>
    private sealed class GalleryWindow : IAsyncDisposable
    {
        private readonly string _previousState = PhotosSettings.StateDirectory;
        private readonly PhotoLibrary _library = new();
        public PhotoLibrary Library => _library;
        public string Folder { get; }
        public PhotosShell Shell { get; }
        public Window Window { get; }

        private GalleryWindow((string Name, string Source)[] files)
        {
            var root = Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N"));
            Folder = Path.Combine(root, "library");
            Directory.CreateDirectory(Folder);
            foreach (var (name, source) in files) File.Copy(Media(source), Path.Combine(Folder, name));
            PhotosSettings.Initialize(root);
            PhotosSettings.SaveFolders([Folder]);
            _library.Initialize(Dispatcher.UIThread);
            Shell = new PhotosShell(_library);
            Window = new Window { Content = Shell, Width = 1400, Height = 1100 };
        }

        public static async Task<GalleryWindow> ShowAsync(params (string Name, string Source)[] files)
        {
            var gallery = new GalleryWindow(files);
            await gallery._library.RescanAsync();
            gallery.Window.Show(); gallery.Window.UpdateLayout();
            Invoke(gallery.Shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Gallery"));
            gallery.Window.UpdateLayout();
            return gallery;
        }

        public async ValueTask DisposeAsync()
        {
            Window.Close(); Shell.Dispose();
            await _library.DisposeAsync();
            PhotosSettings.Initialize(_previousState);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 20)
        {
            Assert.True(waited < 5000, "The condition did not come true within five seconds.");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TruncatedVideoGivesNoMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flowery-truncated-{Guid.NewGuid():N}.mp4");
        try
        {
            var bytes = await File.ReadAllBytesAsync(Media("clip.mp4"), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(path, bytes[..(bytes.Length / 2)], TestContext.Current.CancellationToken);

            Assert.Null(await ImageMetadataReader.ProbeAsync(path));
        }
        finally { File.Delete(path); }
    }
}
