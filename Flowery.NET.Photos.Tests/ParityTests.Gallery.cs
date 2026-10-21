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
            var oldRows = rows.Items.Cast<GalleryRow>().ToList();
            var row = oldRows[GalleryLayout.FindRowAt(oldRows, scroller.Offset.Y)];
            var anchor = row is GalleryPhotoRow photos ? photos.Tiles[0].Item : ((GalleryHeaderRow)row).Items[0];
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge); window.Width = 700; window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); window.UpdateLayout();
            var resizedRow = rows.Items.OfType<GalleryPhotoRow>().Single(candidate => candidate.Tiles.Any(tile => tile.Item == anchor));
            Assert.InRange(scroller.Offset.Y, resizedRow.Top - 1, resizedRow.Top + resizedRow.Height + 1);
            timeline.Focus(); window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, ""); window.UpdateLayout();
            Assert.InRange(Math.Abs(scroller.Offset.Y - provider.Maximum), 0, 1);
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
}
