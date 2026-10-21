using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public async Task PaletteExtractionAndSecondaryEyedropperUseTheEditedPixels()
    {
        var colors = PhotosSettings.CustomColors.ToArray();
        var secondaryBefore = PhotosSettings.SecondaryColor;
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show(); var photo = new PhotoItem(CreateImage());
            await viewer.OpenAsync(photo, [photo]); viewer.BeginEditing(); Invoke(viewer, "filters");
            await viewer.FilterPreviewUpdate;
            var mono = viewer.FindControl<WrapPanel>("Filters")!.Children.OfType<DaisyButton>().Single(button => button.Tag as string == "mono");
            mono.RaiseEvent(new RoutedEventArgs(DaisyButton.ClickEvent));
            Invoke(viewer, "markup"); Invoke(viewer, "extract");
            Assert.Contains(PhotosSettings.CustomColors, color => color != Colors.White);
            Assert.All(PhotosSettings.CustomColors, color => { Assert.Equal(color.R, color.G); Assert.Equal(color.G, color.B); });
            var primary = viewer.FindControl<DaisyColorEditor>("ColorEditor")!;
            var secondary = viewer.FindControl<DaisyColorEditor>("SecondaryColorEditor")!;
            primary.Color = Colors.Lime; secondary.Color = Colors.Purple;
            Invoke(viewer, "eyedropper"); window.UpdateLayout();
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            var point = canvas.TranslatePoint(new Rect(canvas.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right);
            Assert.Equal(Colors.Lime, primary.Color);
            Assert.Equal(secondary.Color.R, secondary.Color.G);
            Assert.Equal(secondary.Color.G, secondary.Color.B);
        }
        finally { window.Close(); colors.CopyTo(PhotosSettings.CustomColors, 0); PhotosSettings.SecondaryColor = secondaryBefore; PhotosSettings.SaveOptions(); }
    }

    [AvaloniaFact]
    public async Task PaletteUsesFreeThenSelectedSlotsAndExportsAllActiveColors()
    {
        var original = PhotosSettings.CustomColors.ToArray();
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            Array.Fill(PhotosSettings.CustomColors, Colors.White);
            viewer.RefreshLabels();
            window.Show();
            var path = CreateImage();
            var photo = new PhotoItem(path) { PixelWidth = 240, PixelHeight = 160 };
            await viewer.OpenAsync(photo, [photo]);
            viewer.BeginEditing(); Invoke(viewer, "markup");
            var primary = viewer.FindControl<DaisyColorEditor>("ColorEditor")!;
            var secondary = viewer.FindControl<DaisyColorEditor>("SecondaryColorEditor")!;
            primary.Color = Colors.Red; Invoke(viewer, "storeColor");
            window.UpdateLayout();
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            var point = canvas.TranslatePoint(new Rect(canvas.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Avalonia.Vector(20, 10));
            window.MouseUp(point + new Avalonia.Vector(20, 10), MouseButton.Left);
            Assert.Contains(Colors.Red, viewer.ExportPaletteColors());
            primary.Color = Colors.Blue; Invoke(viewer, "storeColor");
            Assert.Equal(Colors.Red, PhotosSettings.CustomColors[0]);
            Assert.Equal(Colors.Blue, PhotosSettings.CustomColors[1]);
            Array.Fill(PhotosSettings.CustomColors, Colors.Green);
            viewer.RefreshLabels(); window.UpdateLayout();
            var slot = (DaisyButton)viewer.FindControl<WrapPanel>("Palette")!.Children[5];
            slot.RaiseEvent(new RoutedEventArgs(DaisyButton.ClickEvent));
            primary.Color = Colors.Orange; Invoke(viewer, "storeColor");
            Assert.Equal(Colors.Orange, PhotosSettings.CustomColors[5]);
            Assert.Equal(Colors.Green, PhotosSettings.CustomColors[0]);
            secondary.Color = Colors.Purple;
            primary.Color = Colors.Yellow;
            Assert.Contains(Colors.Yellow, viewer.ExportPaletteColors());
            Assert.Contains(Colors.Purple, viewer.ExportPaletteColors());
            Assert.Contains(Colors.Orange, viewer.ExportPaletteColors());
            Assert.Contains(Colors.Red, viewer.ExportPaletteColors());
            var palettePath = Path.Combine(Path.GetDirectoryName(path)!, "short.pal");
            File.WriteAllText(palettePath, "JASC-PAL\n0100\n2\n255 0 0\n0 0 255\n");
            viewer.ApplyPalette(Flowery.NET.Photos.Editing.ColorTools.ReadPalette(palettePath));
            Assert.Equal(Colors.Red, PhotosSettings.CustomColors[0]);
            Assert.Equal(Colors.Blue, PhotosSettings.CustomColors[1]);
            Assert.All(PhotosSettings.CustomColors.Skip(2), color => Assert.Equal(Colors.White, color));
        }
        finally
        {
            window.Close();
            original.CopyTo(PhotosSettings.CustomColors, 0);
            PhotosSettings.SaveOptions();
        }
    }

    [AvaloniaFact]
    public async Task ViewerMoreMenuOffersExistingAlbumsAndReportsCopyPath()
    {
        var oldState = PhotosSettings.StateDirectory;
        var previousRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        var state = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", state);
        PhotosSettings.Initialize(state);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 1100, Height = 850 };
        try
        {
            var album = library.CreateAlbum("Existing album");
            var path = CreateImage();
            window.Show(); await shell.OpenFileAsync(path); window.UpdateLayout();
            var viewer = shell.FindControl<ViewerView>("Viewer")!;
            var button = viewer.FindControl<DaisyButton>("MoreButton")!;
            var menu = Assert.IsType<MenuFlyout>(button.Flyout);
            menu.ShowAt(button);
            var entries = menu.Items.OfType<MenuItem>().ToArray();
            Assert.Contains(entries, entry => entry.Tag as string == "openWith");
            Assert.Contains(entries, entry => entry.Tag as string == "reveal");
            var albums = entries.Single(entry => AutomationProperties.GetAutomationId(entry) == "Photos.AddToAlbum");
            var existing = albums.Items.OfType<MenuItem>().Single(entry => Equals(entry.Header, album.Name));
            existing.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Contains(path, album.Paths);
            Assert.Single(shell.FindControl<DaisyToast>("Notifications")!.Items);
            entries.Single(entry => entry.Tag as string == "copyPath").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.Equal(path, await window.Clipboard!.TryGetTextAsync());
            menu.Hide();
        }
        finally
        {
            window.Close(); await library.DisposeAsync();
            Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", previousRoot);
            PhotosSettings.Initialize(oldState);
        }
    }
}
