using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Flowery.Controls;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

/// <summary>
/// The backdrop of a theme with the native Skia renderer: drawn backdrops and decoded images.
/// </summary>
public class Given_ThemeBackdrop
{
    [Fact]
    public async Task When_Glassy_Is_Active_The_Backdrop_Draws_Its_Violet_Sky_And_Blue_Waves()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Glassy"));
            var backdrop = new DaisyBackdrop { Width = 320, Height = 180 };
            await RuntimeTestHelpers.AttachAsync(backdrop);
            await RuntimeTestHelpers.WaitForRenderAsync();

            using var pixels = RuntimeTestHelpers.Render(backdrop, "glassy-backdrop");
            var sky = pixels.GetPixel(4, 4);
            var wave = pixels.GetPixel(pixels.Width - 4, pixels.Height - 4);
            Assert.True(sky.Red > 50 && sky.Blue > 180 && sky.Green < 70, $"The upper left is not the violet sky: {sky}.");
            Assert.True(wave.Red < 60 && wave.Blue > 110, $"The lower right is not the blue wave: {wave}.");
        }
        finally
        {
            DaisyThemeManager.ApplyTheme(previous ?? "Dark");
        }
    }

    [Fact]
    public void When_An_Image_Backdrop_Is_Wider_Than_A_Screen_It_Is_Decoded_At_Screen_Width()
    {
        try
        {
            DaisyThemeManager.BackdropOverride = "image:" + Png("flowery-backdrop-wide.png", 4000, 8);
            Assert.Equal(3840, Decoded().Width);

            DaisyThemeManager.BackdropOverride = "image:" + Png("flowery-backdrop-small.png", 40, 20);
            Assert.Equal(new PixelSize(40, 20), Decoded());
        }
        finally
        {
            DaisyThemeManager.BackdropOverride = null;
        }
    }

    private static PixelSize Decoded()
    {
        var app = Application.Current!;
        Assert.True(app.TryFindResource("DaisyBackdropBrush", app.ActualThemeVariant, out var brush));
        return Assert.IsType<Bitmap>(Assert.IsType<ImageBrush>(brush).Source).PixelSize;
    }

    // A PNG of the given size in the temp folder, written again on each run under the same name.
    private static string Png(string name, int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), name);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
        return path;
    }
}
