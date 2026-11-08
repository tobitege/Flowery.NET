using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    private static string CreateTransparentImage()
    {
        var path = CreateImage();
        using var bitmap = new SKBitmap(240, 160);
        bitmap.Erase(new SKColor(200, 100, 50, 100));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        encoded.SaveTo(stream);
        return path;
    }

    [AvaloniaFact]
    public async Task SaveCopyPreservesPngAlphaAndInsertsResultAfterOriginal()
    {
        var path = CreateTransparentImage();
        var originalBytes = File.ReadAllBytes(path);
        var item = new PhotoItem(path);
        var next = new PhotoItem(CreateImage());
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show();
            await viewer.OpenAsync(item, [item, next]);
            // Turned in the view, the photo has nothing to save until the editor takes the turn over.
            Invoke(viewer, "right");
            Assert.False(await viewer.SaveAsync(false));
            viewer.BeginEditing();
            Assert.True(await viewer.SaveAsync(false));
            var copy = viewer.Current!;
            Assert.NotEqual(path, copy.Path);
            Assert.Equal(".png", Path.GetExtension(copy.Path));
            Assert.Equal(originalBytes, File.ReadAllBytes(path));
            using var decoded = PhotoDecoder.Load(copy.Path);
            Assert.Equal(160, decoded.Width);
            Assert.Equal(240, decoded.Height);
            Assert.InRange(decoded.GetPixel(20, 20).Alpha, 99, 101);
            await viewer.NavigateAsync(1);
            Assert.Equal(next.Path, viewer.Current!.Path);
            await viewer.NavigateAsync(-1);
            Assert.Equal(copy.Path, viewer.Current!.Path);
            await viewer.NavigateAsync(-1);
            Assert.Equal(path, viewer.Current!.Path);
        }
        finally { window.Close(); }
    }
}
