using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class PhotoTests
{
    private static string NewFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "flowery-photos-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static string CreatePhoto(string folder, string name = "sample.png", int width = 120, int height = 80)
    {
        var path = Path.Combine(folder, name);
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
            using var paint = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(width / 2, 0, width / 2, height, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(name.EndsWith(".jpg") ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path); data.SaveTo(file);
        return path;
    }

    [Fact]
    public void HeaderProbeAndSampledDecodeUseRealImageDimensions()
    {
        var file = CreatePhoto(NewFolder(), width: 1200, height: 800);
        var probe = ImageMetadataReader.ProbeFast(file);
        Assert.Equal(1200, probe?.Width); Assert.Equal(800, probe?.Height);
        using var bitmap = PhotoDecoder.Load(file, 300);
        Assert.InRange(bitmap.Width, 1, 300);
        Assert.InRange(bitmap.Height, 1, 300);
    }

    [Fact]
    public void JpegExifOrientationSwapsHeaderAndDecodedDimensions()
    {
        var file = CreatePhoto(NewFolder(), "oriented.jpg", 120, 80);
        var original = File.ReadAllBytes(file);
        byte[] exif = [0xff,0xe1,0,34, 69,120,105,102,0,0, 73,73,42,0,8,0,0,0,
            1,0, 0x12,1,3,0,1,0,0,0,6,0,0,0, 0,0,0,0];
        File.WriteAllBytes(file, original[..2].Concat(exif).Concat(original[2..]).ToArray());
        var probe = ImageMetadataReader.ProbeFast(file);
        Assert.Equal(80, probe?.Width); Assert.Equal(120, probe?.Height);
        using var bitmap = PhotoDecoder.Load(file);
        Assert.Equal(80, bitmap.Width); Assert.Equal(120, bitmap.Height);
    }

    [Fact]
    public void ExifGpsReadsRationalsAndHemisphereSigns()
    {
        var file = CreatePhoto(NewFolder(), "gps.jpg");
        var original = File.ReadAllBytes(file);
        var tiff = new byte[128];
        using (var writer = new BinaryWriter(new MemoryStream(tiff)))
        {
            writer.Write((byte)'I'); writer.Write((byte)'I'); writer.Write((ushort)42); writer.Write(8u);
            writer.Write((ushort)1);
            Entry(writer, 0x8825, 4, 1, 26);
            writer.Write(0u);
            writer.Write((ushort)4);
            Entry(writer, 1, 2, 2, (uint)'S');
            Entry(writer, 2, 5, 3, 80);
            Entry(writer, 3, 2, 2, (uint)'W');
            Entry(writer, 4, 5, 3, 104);
            writer.Write(0u);
            foreach (var value in new uint[] { 30, 15, 0, 10, 30, 0 })
            { writer.Write(value); writer.Write(1u); }
        }
        var payload = new byte[] { 69, 120, 105, 102, 0, 0 }.Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var segment = new byte[] { 0xff, 0xe1, (byte)(length >> 8), (byte)length }.Concat(payload);
        File.WriteAllBytes(file, original[..2].Concat(segment).Concat(original[2..]).ToArray());
        var probe = ImageMetadataReader.ProbeFast(file);
        Assert.Equal(-30.25, probe?.Details?.Latitude);
        Assert.Equal(-10.5, probe?.Details?.Longitude);

        static void Entry(BinaryWriter writer, ushort tag, ushort type, uint count, uint offset)
        { writer.Write(tag); writer.Write(type); writer.Write(count); writer.Write(offset); }
    }

    [Fact]
    public async Task CorruptImagesDoNotProduceMetadata()
    {
        var file = Path.Combine(NewFolder(), "broken.jpg");
        await File.WriteAllBytesAsync(file, [1, 2, 3, 4], TestContext.Current.CancellationToken);
        Assert.Null(await ImageMetadataReader.ProbeAsync(file));
    }

    [Fact]
    public void ColorMatricesPreserveAlphaAndUseSkiaChannelOrder()
    {
        var matrix = ColorMatrix.Saturation(0).ToSkiaMatrix();
        Assert.Equal(.2126f, matrix[0], 5);
        Assert.Equal(.7152f, matrix[1], 5);
        Assert.Equal(.0722f, matrix[2], 5);
        Assert.Equal(1, matrix[18]);
        Assert.Equal(.1f, ColorMatrix.Brightness(.1f).ToSkiaMatrix()[4], 4);
    }

    [Fact]
    public void FilterOffsetsPreserveExpectedMidtones()
    {
        using var source = new SKBitmap(8, 8);
        source.Erase(new SKColor(128, 128, 128));
        foreach (var preset in FilterPresets.All)
        {
            using var rendered = EditRenderer.Render(source, EditState.Original with { Filter = preset.Id });
            var pixel = rendered.GetPixel(4, 4);
            byte[] channels = [pixel.Red, pixel.Green, pixel.Blue];
            for (var channel = 0; channel < 3; channel++)
            {
                var expected = Math.Clamp(128 * (preset.Matrix[0, channel] + preset.Matrix[1, channel] +
                    preset.Matrix[2, channel]) + 255 * preset.Matrix[4, channel], 0, 255);
                Assert.True(Math.Abs(channels[channel] - expected) <= 2,
                    $"{preset.Id} channel {channel}: expected {expected:0.0}, actual {channels[channel]}");
            }
            Assert.Equal(255, pixel.Alpha);
        }
    }

    [AvaloniaFact]
    public async Task ViewerKeyboardZoomAndEditingUseTheRealSurface()
    {
        var path = CreatePhoto(NewFolder(), width: 800, height: 600);
        var item = new PhotoItem(path) { PixelWidth = 800, PixelHeight = 600 };
        var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 720, Height = 1100 };
        try
        {
            window.Show();
            await viewer.OpenAsync(item, [item]);
            window.UpdateLayout();
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            var canvasPeer = ControlAutomationPeer.CreatePeerForElement(canvas)!;
            Assert.Equal(AutomationControlType.Image, canvasPeer.GetAutomationControlType());
            Assert.Equal("ViewerView.Canvas", canvasPeer.GetAutomationId());
            Assert.False(string.IsNullOrWhiteSpace(canvasPeer.GetName()));
            canvas.Focus();
            window.KeyPress(Avalonia.Input.Key.E, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.E, "e");
            Assert.False(canvas.IsCropping);
            Assert.True(viewer.FindControl<StackPanel>("AdjustPanel")!.IsVisible);
            window.KeyPress(Avalonia.Input.Key.R, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.R, "r");
            Assert.Equal(1, canvas.State.Rotation);
            window.KeyPress(Avalonia.Input.Key.Z, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.Z, "z");
            Assert.Equal(0, canvas.State.Rotation);
            window.KeyPress(Avalonia.Input.Key.Y, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.Y, "y");
            Assert.Equal(1, canvas.State.Rotation);
            canvas.SetZoom(100); canvas.ZoomStep(true); Assert.Equal(125, canvas.Zoom);
            canvas.ZoomStep(false); Assert.Equal(100, canvas.Zoom);
            canvas.SetZoom(100); canvas.ZoomStep(false); Assert.Equal(90, canvas.Zoom);
            canvas.Fit(); Assert.True(canvas.ActualZoom > 0);
            var filtersAction = viewer.GetVisualDescendants().OfType<DaisyButton>().Single(b => b.Tag as string == "filters");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(filtersAction)).Invoke();
            window.UpdateLayout();
            var filtersPanel = viewer.FindControl<WrapPanel>("Filters")!;
            Assert.Equal(FilterPresets.All.Count, filtersPanel.Children.Count);
            foreach (var filterButton in filtersPanel.Children.Cast<DaisyButton>())
            {
                var content = Assert.IsType<StackPanel>(filterButton.Content);
                Assert.True(filterButton.Bounds.Height >= content.Bounds.Height + filterButton.Padding.Top + filterButton.Padding.Bottom);
                Assert.True(content.Bounds.Height > 75);
            }
            Assert.True(filtersPanel.Bounds.Height >= filtersPanel.Children.Max(child => child.Bounds.Bottom));
            var adjust = viewer.GetVisualDescendants().OfType<DaisyButton>().Single(b => b.Tag as string == "adjust");
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(adjust)));
            Assert.False(string.IsNullOrWhiteSpace(ControlAutomationPeer.CreatePeerForElement(adjust)!.GetName()));
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(adjust)).Invoke();
            window.UpdateLayout();
            var brightness = viewer.GetVisualDescendants().OfType<DaisyRange>()
                .Single(range => AutomationProperties.GetAutomationId(range) == "Viewer.Adjust.Brightness");
            var range = Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(brightness));
            Assert.False(string.IsNullOrWhiteSpace(ControlAutomationPeer.CreatePeerForElement(brightness)!.GetName()));
            range.SetValue(35);
            Assert.Equal(35, canvas.State.Brightness);
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
        }
        finally { viewer.Dispose(); window.Close(); }
    }

    [AvaloniaFact]
    public void PhotoTileAutomationInvokesAndSelectsTheRealItem()
    {
        var item = new PhotoItem(CreatePhoto(NewFolder()));
        var tile = new PhotoTile(item, 150, 100);
        var window = new Window { Content = tile };
        var opened = 0;
        tile.Activated += (_, _, _) => opened++;
        try
        {
            window.Show();
            var peer = ControlAutomationPeer.CreatePeerForElement(tile)!;
            Assert.Equal(item.FileName, peer.GetName());
            Assert.StartsWith("Photos.Tile.", peer.GetAutomationId());
            Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType());
            Assert.IsAssignableFrom<IInvokeProvider>(peer).Invoke();
            Assert.Equal(1, opened);
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer);
            toggle.Toggle(); Assert.True(item.IsSelected); Assert.Equal(ToggleState.On, toggle.ToggleState);
            toggle.Toggle(); Assert.False(item.IsSelected); Assert.Equal(ToggleState.Off, toggle.ToggleState);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ViewerArrowKeysAdjustTheFocusedSliderWithoutNavigating()
    {
        var folder = NewFolder();
        var first = new PhotoItem(CreatePhoto(folder, "first.png"));
        var second = new PhotoItem(CreatePhoto(folder, "second.png"));
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 900, Height = 900 };
        var leaveRequests = 0;
        viewer.ConfirmLeave = () => { leaveRequests++; return Task.FromResult(false); };
        try
        {
            window.Show();
            await viewer.OpenAsync(first, [first, second]);
            viewer.BeginEditing();
            window.UpdateLayout();
            var crop = viewer.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "crop");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(crop)).Invoke();
            window.UpdateLayout();
            var slider = viewer.FindControl<DaisyRange>("Straighten")!;
            Assert.True(slider.Focus());
            window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None,
                Avalonia.Input.PhysicalKey.ArrowRight, "");
            Assert.True(slider.Value > 0);
            Assert.Equal(0, leaveRequests);
            Assert.Equal("first.png", viewer.FindControl<TextBlock>("FileName")!.Text);
            window.KeyPress(Avalonia.Input.Key.Left, Avalonia.Input.RawInputModifiers.None,
                Avalonia.Input.PhysicalKey.ArrowLeft, "");
            Assert.Equal(0, slider.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ViewerFavoriteStateTracksAutomationKeyboardAndExternalChanges()
    {
        var culture = FloweryLocalization.CurrentCulture.Name;
        var item = new PhotoItem(CreatePhoto(NewFolder()));
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 900, Height = 900 };
        try
        {
            window.Show();
            await viewer.OpenAsync(item, [item]);
            var button = viewer.GetVisualDescendants().OfType<DaisyButton>().Single(b => b.Tag as string == "favorite");
            var peer = ControlAutomationPeer.CreatePeerForElement(button)!;
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer);
            var states = new List<ToggleState>();
            peer.PropertyChanged += (_, e) =>
            {
                if (e.Property == TogglePatternIdentifiers.ToggleStateProperty)
                    states.Add((ToggleState)e.NewValue!);
            };
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            toggle.Toggle();
            Assert.True(item.IsFavorite);
            Assert.True(button.IsActive);
            Assert.Equal(ToggleState.On, toggle.ToggleState);
            Assert.Equal(PhotosLocalization.GetString("Photos_Unfavorite"), peer.GetName());
            FloweryLocalization.SetCulture("de");
            viewer.RefreshLabels();
            Assert.Equal(PhotosLocalization.GetString("Photos_Unfavorite"), peer.GetName());
            item.IsFavorite = false;
            Assert.False(button.IsActive);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            Assert.Equal(new[] { ToggleState.On, ToggleState.Off }, states);
            viewer.FindControl<PhotoCanvas>("Canvas")!.Focus();
            window.KeyPress(Avalonia.Input.Key.F, Avalonia.Input.RawInputModifiers.None,
                Avalonia.Input.PhysicalKey.F, "f");
            Assert.True(item.IsFavorite);
            var next = new PhotoItem(CreatePhoto(NewFolder(), "next.png"));
            await viewer.OpenAsync(next, [next]);
            item.IsFavorite = false;
            Assert.False(button.IsActive);
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
        }
        finally { window.Close(); FloweryLocalization.SetCulture(culture); }
    }

    [AvaloniaFact]
    public async Task LanguagePreviewKeepsTheOpenEditorAndItsEdits()
    {
        var originalCulture = FloweryLocalization.CurrentCulture.Name;
        var shell = new PhotosShell();
        var window = new Window { Content = shell, Width = 900, Height = 1200 };
        try
        {
            window.Show();
            await shell.OpenFileAsync(CreatePhoto(NewFolder()));
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            viewer.BeginEditing();
            window.UpdateLayout();
            var crop = viewer.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "crop");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(crop)).Invoke();
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            canvas.State = EditState.Original with { Crop = new Rect(.1, .1, .8, .8) };
            FloweryLocalization.SetCulture("de");
            Assert.True(viewer.IsVisible);
            Assert.True(canvas.IsCropping);
            Assert.Equal(new Rect(.1, .1, .8, .8), canvas.State.Crop);
        }
        finally { shell.Dispose(); window.Close(); FloweryLocalization.SetCulture(originalCulture); }
    }

    [AvaloniaTheory]
    [InlineData("en")] [InlineData("de")] [InlineData("fr")] [InlineData("es")]
    [InlineData("it")] [InlineData("zh-CN")] [InlineData("ko")] [InlineData("ja")]
    [InlineData("ar")] [InlineData("tr")] [InlineData("uk")] [InlineData("he")]
    public void RuntimeLanguageSwitchResolvesTranslatedKeys(string language)
    {
        var previous = FloweryLocalization.CurrentCulture.Name;
        try
        {
            FloweryLocalization.SetCulture(language);
            Assert.Equal(language, PhotosLocalization.CurrentCultureName);
            Assert.NotEqual("Photos_Home_WelcomeTitle", PhotosLocalization.GetString("Photos_Home_WelcomeTitle"));
        }
        finally { FloweryLocalization.SetCulture(previous); }
    }

    [Fact]
    public void RenderAppliesRotationCropFilterAndMarkup()
    {
        var file = CreatePhoto(NewFolder());
        using var source = PhotoDecoder.Load(file);
        var state = EditState.Original with { Rotation = 1, Crop = new Rect(0, 0, .5, .5), Filter = "mono" };
        using var rendered = EditRenderer.Render(source, state);
        Assert.Equal(40, rendered.Width); Assert.Equal(60, rendered.Height);
        var pixel = rendered.GetPixel(20, 30);
        Assert.InRange(Math.Abs(pixel.Red - pixel.Green), 0, 2);
        Assert.InRange(Math.Abs(pixel.Blue - pixel.Green), 0, 2);
        var stroke = new MarkupStroke(Colors.Lime, .15f, false,
            [new System.Numerics.Vector2(.1f, .5f), new System.Numerics.Vector2(.9f, .5f)]);
        using var marked = EditRenderer.Render(source, EditState.Original with { Strokes = [stroke] });
        Assert.True(marked.GetPixel(60, 40).Green > 200);
    }

    [Theory]
    [InlineData("vivid")]
    [InlineData("warm")]
    [InlineData("cool")]
    [InlineData("sepia")]
    [InlineData("noir")]
    public void FilterChangesRealPixelValues(string filter)
    {
        using var source = PhotoDecoder.Load(CreatePhoto(NewFolder()));
        source.Erase(new SKColor(110, 100, 150));
        using var rendered = EditRenderer.Render(source, EditState.Original with { Filter = filter });
        Assert.NotEqual(source.GetPixel(20, 20), rendered.GetPixel(20, 20));
    }

    [Fact]
    public async Task SaveCopyKeepsSourceAndPublishesCompleteImage()
    {
        var root = NewFolder(); var source = CreatePhoto(root);
        var original = SHA256.HashData(await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));
        var target = Path.Combine(root, "edited.png");
        await EditRenderer.SaveAsync(source, target, EditState.Original with { Rotation = 1 }, 92);
        Assert.Equal(original, SHA256.HashData(await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken)));
        using var result = PhotoDecoder.Load(target);
        Assert.Equal(80, result.Width); Assert.Equal(120, result.Height);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        await EditRenderer.SaveAsync(target, target, EditState.Original with { Rotation = 1 }, 92);
        using var replaced = PhotoDecoder.Load(target);
        Assert.Equal(120, replaced.Width); Assert.Equal(80, replaced.Height);
    }

    [Fact]
    public void UndoCoalescesAndTruncatesRedoAfterNewEdit()
    {
        var history = new EditHistory();
        history.Apply(history.Current with { Brightness = 10 }, "Brightness");
        history.Apply(history.Current with { Brightness = 20 }, "Brightness");
        history.Undo(); Assert.Equal(EditState.Original, history.Current);
        history.Redo(); Assert.Equal(20, history.Current.Brightness);
        history.Undo(); history.Apply(history.Current with { Rotation = 1 });
        Assert.False(history.CanRedo);
        history.Reset(); Assert.False(history.Current.HasChanges);
    }

    [Fact]
    public async Task FailedExportRecyclesItsTemporaryFileAndPreservesOtherFiles()
    {
        var root = NewFolder();
        var source = CreatePhoto(root);
        var sourceBytes = await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken);
        var target = Path.Combine(root, "blocked.png");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep", TestContext.Current.CancellationToken);
        var unrelated = Path.Combine(root, "keep.tmp");
        await File.WriteAllTextAsync(unrelated, "unrelated", TestContext.Current.CancellationToken);

        var error = await Record.ExceptionAsync(() => EditRenderer.SaveAsync(source, target, EditState.Original, 92));
        Assert.True(error is IOException or UnauthorizedAccessException);

        Assert.Empty(Directory.GetFiles(root, "blocked.png.*.tmp"));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));
        Assert.Equal("keep", await File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken));
        Assert.Equal("unrelated", await File.ReadAllTextAsync(unrelated, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(360)]
    [InlineData(720)]
    [InlineData(1200)]
    public void JustifiedRowsKeepAllPhotosAndLastRowHeight(double width)
    {
        var items = Enumerable.Range(0, 35).Select(i => new PhotoItem(i + ".jpg")
            { PixelWidth = 120, PixelHeight = 80, DateModified = new DateTime(2026, 10, 21) }).ToList();
        var rows = GalleryLayout.Build(items, width, 150, 6, PhotoGroupMode.None, false).OfType<GalleryPhotoRow>().ToList();
        Assert.Equal(items.Count, rows.Sum(r => r.Tiles.Count));
        foreach (var row in rows) Assert.InRange(row.Tiles.Sum(t => t.Width) + (row.Tiles.Count - 1) * 6, 1, width + 1);
        Assert.Equal(rows[^2].TileHeight, rows[^1].TileHeight);
    }

    [Fact]
    public void PaletteRoundTripAndHarmonyAreDeterministic()
    {
        var colors = ColorTools.CreateHarmony(Colors.Coral, ColorTools.Harmony.Triad);
        Assert.Equal(9, colors.Length);
        var file = Path.Combine(NewFolder(), "palette.pal");
        ColorTools.WriteJascPalette(file, colors);
        Assert.Equal(colors, ColorTools.ReadPalette(file));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("relative-state")]
    public void StateDirectoryRejectsNonAbsolutePathsBeforeChangingStorage(string path)
    {
        var previous = PhotosSettings.StateDirectory;
        var storage = Flowery.Services.StateStorageProvider.Instance;
        Assert.Throws<ArgumentException>(() => PhotosSettings.Initialize(path));
        Assert.Equal(previous, PhotosSettings.StateDirectory);
        Assert.Same(storage, Flowery.Services.StateStorageProvider.Instance);
    }

    [Fact]
    public void StateDirectoryNormalizesTheChosenRootAndKeepsSettingsInsideIt()
    {
        var previous = PhotosSettings.StateDirectory;
        var root = NewFolder();
        var chosen = Path.Combine(root, "unused", "..", "state");
        var expected = Path.Combine(root, "state");
        try
        {
            PhotosSettings.Initialize(chosen);
            Assert.Equal(expected, PhotosSettings.StateDirectory);
            PhotosSettings.SaveTheme("Dark");
            Assert.Equal("Dark", File.ReadAllText(Path.Combine(expected, "theme.state")).Trim());
            Assert.False(Directory.Exists(Path.Combine(root, "unused")));
            Assert.Equal("Dark", PhotosSettings.LoadTheme());
        }
        finally { PhotosSettings.Initialize(previous); }
    }

    [AvaloniaFact]
    public async Task ImportRejectsAPersistedRelativeRootBeforeWriting()
    {
        var previous = PhotosSettings.StateDirectory;
        var root = NewFolder();
        var source = CreatePhoto(NewFolder());
        var sourceBytes = File.ReadAllBytes(source);
        PhotosSettings.Initialize(root);
        PhotosSettings.SaveFolders(["."]);
        var library = new PhotoLibrary();
        try
        {
            library.Initialize(Dispatcher.UIThread);
            await Assert.ThrowsAsync<InvalidOperationException>(() => library.ImportFilesAsync([source]));
            Assert.Equal(sourceBytes, File.ReadAllBytes(source));
            Assert.Empty(library.Items);
            Assert.False(Directory.Exists(Path.Combine(root, "Flowery Photos")));
        }
        finally { await library.DisposeAsync(); PhotosSettings.Initialize(previous); }
    }

    [AvaloniaFact]
    public async Task ImportUsesOnlySourceBasenamesAndPreservesDuplicateSources()
    {
        var previous = PhotosSettings.StateDirectory;
        var oldRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        var root = NewFolder();
        var external = NewFolder();
        Directory.CreateDirectory(Path.Combine(external, "nested"));
        var first = CreatePhoto(external, "imported.png");
        var second = CreatePhoto(NewFolder(), "imported.png", width: 96);
        var firstBytes = File.ReadAllBytes(first);
        var secondBytes = File.ReadAllBytes(second);
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", root);
        PhotosSettings.Initialize(root);
        var library = new PhotoLibrary();
        try
        {
            library.Initialize(Dispatcher.UIThread);
            var dottedSource = Path.Combine(external, "nested", "..", "imported.png");
            Assert.Equal(2, await library.ImportFilesAsync([dottedSource, second]));
            await library.RescanAsync();
            Assert.Equal(2, library.Items.Count);
            var imported = library.Items.Single(item => item.FileName == "imported.png");
            var duplicate = library.Items.Single(item => item.FileName == "imported (1).png");
            Assert.True(PhotoLibrary.IsWithin(root, imported.Path));
            Assert.Equal(imported.FolderPath, duplicate.FolderPath);
            Assert.Equal(firstBytes, File.ReadAllBytes(imported.Path));
            Assert.Equal(secondBytes, File.ReadAllBytes(duplicate.Path));
            Assert.Equal(firstBytes, File.ReadAllBytes(first));
            Assert.Equal(secondBytes, File.ReadAllBytes(second));
        }
        finally
        {
            await library.DisposeAsync();
            Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", oldRoot);
            PhotosSettings.Initialize(previous);
        }
    }

    [AvaloniaFact]
    public async Task LibraryRescansImportQueryPersistenceAndCancellation()
    {
        var root = NewFolder();
        var oldRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", root);
        PhotosSettings.Initialize(root);
        var library = new PhotoLibrary();
        try
        {
            library.Initialize(Dispatcher.UIThread);
            Assert.Empty(library.Folders);
            var folder = Path.Combine(root, "library"); Directory.CreateDirectory(folder);
            CreatePhoto(folder, "red.png");
            library.AddFolder(folder);
            await Task.WhenAll(library.RescanAsync(), library.RescanAsync());
            Assert.Single(library.Items); Assert.False(library.IsScanning);
            library.SetFavorite(library.Items, true);
            var album = library.CreateAlbum("Favorites", library.Items);
            Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Favorites }));
            Assert.Single(library.Query(new PhotoQuery { Source = PhotoSourceKind.Album, SourceKey = album.Name }));
            Assert.Empty(library.Query(new PhotoQuery { SearchText = "missing" }));
            var external = CreatePhoto(NewFolder(), "imported.png");
            Assert.Equal(1, await library.ImportFilesAsync([external]));
            await library.RescanAsync(); Assert.Equal(2, library.Items.Count);
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            library.Changed += (_, _) => { if (library.Items.Count == 3) changed.TrySetResult(); };
            CreatePhoto(folder, "watched.png");
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
            Assert.Equal(3, library.Items.Count);
            Assert.True(File.Exists(external));
            Assert.Single(PhotosSettings.LoadFavorites());
            Assert.Single(PhotosSettings.LoadAlbums());
            Assert.True(File.Exists(Path.Combine(root, "metadata-cache.json")));
            Assert.False(PhotoLibrary.IsWithin(folder, folder + "-other" + Path.DirectorySeparatorChar + "red.png"));
        }
        finally { await library.DisposeAsync(); Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", oldRoot); }
    }

    [AvaloniaFact]
    public async Task GalleryRecyclesRowsAndSettingsCancelRestoresSnapshot()
    {
        var root = NewFolder();
        var oldRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", root);
        PhotosSettings.Initialize(root);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        var folder = Path.Combine(root, "library"); Directory.CreateDirectory(folder);
        for (var i = 0; i < 35; i++) CreatePhoto(folder, i + ".png");
        library.AddFolder(folder); await library.RescanAsync();
        var gallery = new GalleryView(library);
        var window = new Window { Content = gallery, Width = 720, Height = 900 };
        try
        {
            window.Show();
            gallery.Show(new PhotoQuery(), "Gallery");
            window.UpdateLayout();
            for (var i = 0; i < 4; i++)
            {
                window.Width = i % 2 == 0 ? 850 : 720;
                gallery.Refresh(); window.UpdateLayout();
            }
            Assert.Equal(35, gallery.Items.Count);
            gallery.SelectAll(); Assert.All(library.Items, item => Assert.True(item.IsSelected));
            gallery.Search("missing"); window.UpdateLayout(); Assert.Empty(gallery.Items);
            gallery.Search(""); window.UpdateLayout(); Assert.Equal(35, gallery.Items.Count);
            var snapshot = SettingsSnapshot.Capture(library);
            var settings = new SettingsView(library);
            var dialog = settings.ShowDialog(window);
            FloweryLocalization.SetCulture("de");
            FlowerySizeManager.ApplySize(DaisySize.Large);
            PhotosSettings.JpegQuality = 50;
            library.RemoveFolder(folder);
            settings.Close(); await dialog;
            Assert.Equal(snapshot.Culture, FloweryLocalization.CurrentCulture.Name);
            Assert.Equal(snapshot.Size, FlowerySizeManager.CurrentSize);
            Assert.Equal(snapshot.JpegQuality, PhotosSettings.JpegQuality);
            Assert.Equal(snapshot.Folders, library.Folders);
            await library.RescanAsync();

            using var shell = new PhotosShell(library);
            window.Content = shell;
            window.UpdateLayout();
            var home = shell.FindControl<StackPanel>("Home")!;
            Assert.DoesNotContain(home.GetVisualDescendants().OfType<StackPanel>(), panel =>
                AutomationProperties.GetAutomationId(panel) == "Photos.Section.Favorites");
            var folderCard = home.GetVisualDescendants().OfType<PhotoCard>().Single(card =>
                AutomationProperties.GetAutomationId(card)?.StartsWith("Photos.Collection.Folder.") == true);
            Assert.Contains(folderCard.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == Path.GetFileName(folder));
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(folderCard)).Invoke();
            var collection = shell.FindControl<GalleryView>("Gallery")!;
            Assert.True(collection.IsVisible);
            Assert.Equal(PhotoSourceKind.Folder, collection.Query.Source);
            Assert.Equal(folder, collection.Query.SourceKey);
            var homeButton = shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Home");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(homeButton)).Invoke();
            library.SetFavorite([library.Items[0]], true);
            window.UpdateLayout();
            Assert.Contains(home.GetVisualDescendants().OfType<PhotoCard>(), card =>
                AutomationProperties.GetAutomationId(card)?.StartsWith("Photos.Home.Favorites.") == true);
            Assert.Contains(home.GetVisualDescendants().OfType<StackPanel>(), panel =>
                AutomationProperties.GetAutomationId(panel) == "Photos.Section.Folders");

            await VerifySelectionAndFilterTransitionsAsync(shell, library);
        }
        finally { window.Close(); await library.DisposeAsync(); Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", oldRoot); }
    }

    [AvaloniaFact]
    public void GalleryShowSynchronizesExplicitFiltersWithOptionControls()
    {
        var gallery = new GalleryView();
        var window = new Window { Content = gallery, Width = 900, Height = 900 };
        try
        {
            window.Show();
            gallery.Show(new PhotoQuery
            {
                TypeFilter = PhotoTypeFilter.Png,
                OrientationFilter = PhotoOrientationFilter.Portrait
            }, "Filtered");
            var type = gallery.FindControl<DaisySelect>("Type")!;
            var orientation = gallery.FindControl<DaisySelect>("Orientation")!;
            Assert.Equal((int)PhotoTypeFilter.Png, type.SelectedIndex);
            Assert.Equal((int)PhotoOrientationFilter.Portrait, orientation.SelectedIndex);

            gallery.Show(new PhotoQuery { Source = PhotoSourceKind.Favorites }, "Favorites");
            Assert.Equal((int)PhotoTypeFilter.All, type.SelectedIndex);
            Assert.Equal((int)PhotoOrientationFilter.Any, orientation.SelectedIndex);
            gallery.FindControl<DaisyCheckBox>("Square")!.IsChecked = !PhotosSettings.SquareTiles;
            Assert.Equal(PhotoTypeFilter.All, gallery.Query.TypeFilter);
            Assert.Equal(PhotoOrientationFilter.Any, gallery.Query.OrientationFilter);
        }
        finally { window.Close(); }
    }

    private static async Task VerifySelectionAndFilterTransitionsAsync(PhotosShell shell, PhotoLibrary library)
    {
        var galleryButton = shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Gallery");
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(galleryButton)).Invoke();
        var gallery = shell.FindControl<GalleryView>("Gallery")!;
        gallery.SelectAll();
        shell.FindControl<TextBox>("Search")!.Text = "no matching photos";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Empty(gallery.Items);
        Assert.DoesNotContain(library.Items, item => item.IsSelected);
        var clear = shell.FindControl<DaisyButton>("ClearSelection")!;
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(clear)).Invoke();
        Assert.DoesNotContain(library.Items, item => item.IsSelected);
        Assert.False(shell.FindControl<DaisyButton>("BatchDelete")!.IsVisible);

        shell.FindControl<TextBox>("Search")!.Text = "";
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var type = gallery.FindControl<DaisySelect>("Type")!;
        var orientation = gallery.FindControl<DaisySelect>("Orientation")!;
        type.SelectedIndex = (int)PhotoTypeFilter.Jpeg;
        orientation.SelectedIndex = (int)PhotoOrientationFilter.Portrait;
        var favoriteButton = shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Favorites");
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(favoriteButton)).Invoke();
        Assert.Equal((int)gallery.Query.TypeFilter, type.SelectedIndex);
        Assert.Equal((int)gallery.Query.OrientationFilter, orientation.SelectedIndex);
        Assert.Single(gallery.Items);
        gallery.FindControl<DaisySelect>("Sort")!.SelectedIndex = (int)PhotoSortMode.NameAscending;
        Assert.Single(gallery.Items);
    }

    [Fact]
    public void AllTwelveLanguagesContainAllAppKeys()
    {
        var assembly = typeof(PhotosLocalization).Assembly;
        var english = Read("en");
        Assert.True(english.Count >= 316);
        foreach (var language in FloweryLocalization.SupportedLanguages)
        {
            var translated = Read(language);
            Assert.Equal(english.Keys.Order(), translated.Keys.Order());
            Assert.All(translated.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }
        Dictionary<string, string> Read(string language)
        {
            using var stream = assembly.GetManifestResourceStream("Flowery.NET.Photos.Localization." + language + ".json");
            Assert.NotNull(stream);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
    }
}
