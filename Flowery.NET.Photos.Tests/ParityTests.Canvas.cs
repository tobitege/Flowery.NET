using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public void ComparisonRendersBothHalvesWithTheSameRotationAndCrop()
    {
        var navigator = PhotosSettings.ShowNavigator; var rulers = PhotosSettings.ShowRulers; var grid = PhotosSettings.ShowGrid;
        PhotosSettings.ShowNavigator = PhotosSettings.ShowRulers = PhotosSettings.ShowGrid = false;
        using var canvas = new PhotoCanvas();
        var window = new Window { Content = canvas, Width = 800, Height = 600 };
        try
        {
            var bitmap = new SKBitmap(400, 400);
            using (var drawing = new SKCanvas(bitmap))
            {
                drawing.Clear(SKColors.Blue);
                using var paint = new SKPaint { Color = SKColors.Red };
                drawing.DrawRect(0, 0, 200, 400, paint);
            }
            canvas.SetPhoto(bitmap, new Size(400, 400));
            canvas.State = Flowery.NET.Photos.Editing.EditState.Original with { Rotation = 1, Crop = new Rect(.1, .2, .7, .6), Brightness = 20 };
            canvas.Compare = true;
            window.Show(); window.UpdateLayout(); canvas.SetZoom(100);
            using var frame = Capture(window);
            Assert.Equal(SKColors.Red, frame.GetPixel(360, 240));
            Assert.Equal(SKColors.Blue, frame.GetPixel(360, 360));
            Assert.Equal(255, frame.GetPixel(440, 240).Red);
            Assert.Equal(255, frame.GetPixel(440, 360).Blue);
            Assert.InRange(frame.GetPixel(440, 240).Green, 10, 15);
        }
        finally { window.Close(); PhotosSettings.ShowNavigator = navigator; PhotosSettings.ShowRulers = rulers; PhotosSettings.ShowGrid = grid; }
    }

    [AvaloniaTheory]
    [InlineData(400, 1600)]
    [InlineData(1600, 400)]
    public void NavigatorPreservesAspectAndDragsTheViewport(int width, int height)
    {
        var navigator = PhotosSettings.ShowNavigator; var rulers = PhotosSettings.ShowRulers;
        PhotosSettings.ShowNavigator = true; PhotosSettings.ShowRulers = false;
        using var canvas = new PhotoCanvas();
        var window = new Window { Content = canvas, Width = 800, Height = 600 };
        try
        {
            var bitmap = new SKBitmap(width, height);
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                    bitmap.SetPixel(x, y, new SKColor((byte)((width > height ? x / (double)width : y / (double)height) * 255), 60, 80));
            canvas.SetPhoto(bitmap, new Size(width, height));
            window.Show(); window.UpdateLayout(); canvas.SetZoom(100);
            var scale = 170d / Math.Max(width, height);
            var nav = new Rect(800 - width * scale - 14, 600 - height * scale - 14, width * scale, height * scale);
            using var before = Capture(window);
            var outside = before.GetPixel((int)nav.Left - 3, (int)nav.Top + 4);
            Assert.Equal(new SKColor(19, 21, 27), outside);
            Assert.NotEqual(outside, before.GetPixel((int)nav.Left + 4, (int)nav.Top + 4));
            var point = nav.Center;
            var target = point + (width > height ? new Vector(nav.Width * .4, 0) : new Vector(0, nav.Height * .4));
            window.MouseDown(point, MouseButton.Left); window.MouseMove(target); window.MouseUp(target, MouseButton.Left);
            using var after = Capture(window);
            Assert.True(after.GetPixel(400, 300).Red > before.GetPixel(400, 300).Red + 30);
        }
        finally { window.Close(); PhotosSettings.ShowNavigator = navigator; PhotosSettings.ShowRulers = rulers; }
    }

    private static SKBitmap Capture(Window window)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(stream.ToArray());
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void DetailZoomUsesPhysicalPixelsAndKeepsThePointerAnchor(double scaling)
    {
        var navigator = PhotosSettings.ShowNavigator; var rulers = PhotosSettings.ShowRulers; var grid = PhotosSettings.ShowGrid;
        PhotosSettings.ShowNavigator = PhotosSettings.ShowRulers = PhotosSettings.ShowGrid = false;
        using var canvas = new PhotoCanvas();
        var window = new Window { Content = canvas, Width = 800, Height = 600 };
        try
        {
            var bitmap = new SKBitmap(4000, 1000);
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    bitmap.SetPixel(x, y, y < 500 ? (x % 2 == 0 ? SKColors.Black : SKColors.White) : new SKColor((byte)(x * 255 / 4000), 60, 80));
            canvas.SetPhoto(bitmap, new Size(4000, 1000));
            window.Show(); window.SetRenderScaling(scaling); window.UpdateLayout(); canvas.SetZoom(100);
            using var actual = Capture(window);
            var centerX = actual.Width / 2; var centerY = actual.Height / 2;
            Assert.True(Math.Abs(actual.GetPixel(centerX, centerY - 20).Red - actual.GetPixel(centerX + 1, centerY - 20).Red) > 220);
            var anchor = new Point(500, 400);
            var before = actual.GetPixel((int)(anchor.X * scaling), (int)(anchor.Y * scaling));
            canvas.SetZoom(200, anchor);
            using var zoomed = Capture(window);
            var after = zoomed.GetPixel((int)(anchor.X * scaling), (int)(anchor.Y * scaling));
            Assert.InRange(Math.Abs(before.Red - after.Red), 0, 1);
            window.MouseDown(new Point(400, 300), MouseButton.Left);
            window.MouseMove(new Point(20000, 300)); window.MouseUp(new Point(20000, 300), MouseButton.Left);
            using var panned = Capture(window);
            Assert.Equal(60, panned.GetPixel(panned.Width / 2, (int)(400 * scaling)).Green);
        }
        finally { window.Close(); PhotosSettings.ShowNavigator = navigator; PhotosSettings.ShowRulers = rulers; PhotosSettings.ShowGrid = grid; }
    }
}
