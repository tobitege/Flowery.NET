using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Flowery.Capture;
using Flowery.Capture.Platforms;
using Flowery.Controls;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

// The tests that compare the glass with screen captures, which WindowsScreenCapture takes. The project builds this file
// only for Windows.
public partial class Given_GlassBackdrop
{
    [Fact]
    public async Task When_Capturing_The_Backdrop_Visible_Children_Are_Included_And_Displayed_Blurred()
    {
        var window = RuntimeTestHelpers.GetWindow();
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue };
        var red = new Border { Width = 90, Height = 100, Background = Brushes.Red };
        Canvas.SetLeft(red, 160);
        Canvas.SetTop(red, 80);
        canvas.Children.Add(red);
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 10, GlassTintOpacity = 0 };
        Canvas.SetLeft(glass, 120);
        Canvas.SetTop(glass, 60);
        canvas.Children.Add(glass);
        await RuntimeTestHelpers.AttachAsync(canvas);

        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Assert.NotNull(capture);
        using var pixels = Decode(capture, "glass-backdrop");
        // Capture starts 10 DIPs before the glass and uses 0.75 pixels per DIP.
        var center = pixels.GetPixel((int)(80 * .75), (int)(60 * .75));
        Assert.True(center.Red > 200 && center.Blue < 40, $"Visible red child missing from backdrop: {center}.");

        await EnableBackdropBlurAsync(glass);
        var image = glass.GetVisualDescendants().OfType<Image>().Single(control => control.Name == "PART_BlurImage");
        Assert.True(image.Effect is BlurEffect { Radius: 10 }, $"Unexpected blur effect: {image.Effect}.");
        using var actual = SKBitmap.Decode(await SaveScreenAsync(window, canvas, "glass-rendered"));
        var mixedPixels = 0;
        for (var x = 140; x < 185; x++)
        {
            var pixel = actual.GetPixel((int)(x * window.RenderScaling), (int)(130 * window.RenderScaling));
            if (pixel.Red > 15 && pixel.Blue > 15 && pixel.Green < 10)
                mixedPixels++;
        }

        Assert.True(mixedPixels > 2, "The displayed glass did not blur the red/blue edge.");
    }

    [Theory]
    [InlineData(0d, 120d, 60d)]
    [InlineData(10d, 120d, 60d)]
    [InlineData(20d, 4d, 4d)]
    [InlineData(20d, 196d, 116d)]
    public async Task When_The_Glass_Is_Displayed_It_Aligns_With_The_Backdrop(double blur, double x, double y)
    {
        var window = RuntimeTestHelpers.GetWindow();
        var position = new Point(x, y);
        var name = $"glass-alignment-{blur}-{x}-{y}";
        var marker = new Border { Width = 160, Height = 60, Background = Brushes.Red };
        Canvas.SetLeft(marker, position.X - 20);
        Canvas.SetTop(marker, position.Y + 60);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { marker } };
        await RuntimeTestHelpers.AttachAsync(canvas);
        using var reference = SKBitmap.Decode(await SaveScreenAsync(window, canvas, name + "-reference"));
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = blur, GlassTintOpacity = 0, CornerRadius = default };
        Canvas.SetLeft(glass, position.X);
        Canvas.SetTop(glass, position.Y);
        canvas.Children.Add(glass);
        await RuntimeTestHelpers.WaitForRenderAsync();
        await EnableBackdropBlurAsync(glass);
        using var actual = SKBitmap.Decode(await SaveScreenAsync(window, canvas, name + "-screen"));
        var image = glass.GetVisualDescendants().OfType<Image>().Single(control => control.Name == "PART_BlurImage");
        foreach (var horizontal in new[] { true, false })
        {
            var expected = RedSpan(reference, position, window.RenderScaling, horizontal);
            var displayed = RedSpan(actual, position, window.RenderScaling, horizontal);
            Assert.True(expected.First >= 0 && expected.Last > expected.First, "Reference marker is missing.");
            Assert.True(Math.Abs(expected.First - displayed.First) <= 2 && Math.Abs(expected.Last - displayed.Last) <= 2,
                $"Displayed marker is shifted or scaled, horizontal={horizontal}: expected {expected}, actual {displayed}; " +
                $"image bounds={image.Bounds}, source size={image.Source?.Size}, bitmap DPI={(image.Source as Bitmap)?.Dpi}.");
        }
    }

    [Fact]
    public async Task When_A_Glass_Button_Is_Rendered_The_Backdrop_Behind_It_Is_Blurred()
    {
        var window = RuntimeTestHelpers.GetWindow();
        var red = new Border { Width = 200, Height = 260, Background = Brushes.Red };
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { red } };
        await RuntimeTestHelpers.AttachAsync(canvas);
        using var reference = SKBitmap.Decode(await SaveScreenAsync(window, canvas, "glass-button-reference"));

        // Ghost and zero tints leave only the blurred backdrop, so the red/blue edge at x=200 shows the blur.
        var button = GlassButton(null, 200, 80, GlassBlurMode.SkiaSharp);
        button.Variant = DaisyButtonVariant.Ghost;
        DaisyGlassSurface.SetBlur(button, 30);
        DaisyGlassSurface.SetTintOpacity(button, 0);
        DaisyGlassSurface.SetReflectOpacity(button, 0);
        DaisyGlassSurface.SetCurvature(button, 0);
        DaisyGlassSurface.SetBend(button, 0);
        DaisyGlassSurface.SetDispersion(button, 0);
        Canvas.SetLeft(button, 100);
        Canvas.SetTop(button, 90);
        canvas.Children.Add(button);
        await RuntimeTestHelpers.WaitForRenderAsync();
        await RuntimeTestHelpers.WaitForRenderAsync();
        using var actual = SKBitmap.Decode(await SaveScreenAsync(window, canvas, "glass-button-screen"));

        var sharp = MixedPixels(reference, window.RenderScaling, 140);
        var blurred = MixedPixels(actual, window.RenderScaling, 140);
        var outside = MixedPixels(actual, window.RenderScaling, 40);
        Assert.True(sharp <= 2, $"The reference edge is not sharp: {sharp} mixed pixels.");
        Assert.True(outside <= 2, $"The edge outside the button is not sharp: {outside} mixed pixels.");
        Assert.True(blurred > 6, $"The glass button did not blur the red/blue edge behind it: {blurred} mixed pixels.");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task When_A_Glass_Bends_Its_Backdrop_Its_Rim_Shows_What_Lies_Beyond_The_Edge(double refraction, bool bends)
    {
        var window = RuntimeTestHelpers.GetWindow();
        var red = new Border { Width = 200, Height = 260, Background = Brushes.Red };
        // A square glass just right of the red/blue edge at x=200, without tint or highlights; its bezel is 30 DIPs wide.
        var glass = new DaisyGlass
        {
            Width = 120,
            Height = 120,
            CornerRadius = new CornerRadius(0),
            EnableBackdropBlur = true,
            BlurMode = GlassBlurMode.SkiaSharp,
            GlassBlur = 2,
            GlassTintOpacity = 0,
            GlassReflectOpacity = 0,
            GlassCurvature = 0,
            GlassBend = 0,
            GlassDispersion = 0,
            GlassRefraction = refraction
        };
        Canvas.SetLeft(glass, 204);
        Canvas.SetTop(glass, 70);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { red, glass } };
        await RuntimeTestHelpers.AttachAsync(canvas);
        await RuntimeTestHelpers.WaitForRenderAsync();
        await RuntimeTestHelpers.WaitForRenderAsync();
        using var actual = SKBitmap.Decode(await SaveScreenAsync(window, canvas, $"glass-lens-{refraction}"));

        // Three DIPs inside the left edge, below the top shine: the bent rim shows the red that lies 15 to 20 DIPs left.
        var scale = window.RenderScaling;
        var rim = actual.GetPixel((int)(207 * scale), (int)(160 * scale));
        var middle = actual.GetPixel((int)(264 * scale), (int)(160 * scale));
        Assert.True(middle.Blue > 150 && middle.Red < 80, $"The middle of the glass is not blue: {middle}.");
        Assert.True(bends == rim.Red > rim.Blue, $"The rim shows {rim} with GlassRefraction {refraction}.");
    }

    // Counts the pixels of the red/blue transition at x=200 that are neither nearly red nor nearly blue.
    private static int MixedPixels(SKBitmap pixels, double scaling, double y)
    {
        var mixed = 0;
        for (var x = 160; x < 240; x++)
        {
            var pixel = pixels.GetPixel((int)(x * scaling), (int)(y * scaling));
            if (pixel.Red is > 20 and < 235)
                mixed++;
        }

        return mixed;
    }

    private static (int First, int Last) RedSpan(SKBitmap pixels, Point position, double scaling, bool horizontal)
    {
        var first = -1;
        var last = -1;
        // Exclude the decorative glass border. Red/blue dominance locates the midpoint of blurred edges.
        for (var offset = 8; offset < (horizontal ? 192 : 132); offset++)
        {
            var x = position.X + (horizontal ? offset : 80);
            var y = position.Y + (horizontal ? 90 : offset);
            var color = pixels.GetPixel((int)(x * scaling), (int)(y * scaling));
            if (color.Red <= color.Blue)
                continue;
            if (first < 0)
                first = offset;
            last = offset;
        }

        return (first, last);
    }

    private static async Task<string> SaveScreenAsync(Window window, Control control, string name)
    {
        window.Topmost = true;
        try
        {
            var screenshot = await new WindowsScreenCapture().CaptureControlAsync(control,
                new ScreenCaptureOptions { CaptureMargin = new Thickness(0), ScrollSettleDelayMs = 100 });
            Assert.True(screenshot.Success, screenshot.ErrorMessage ?? "Screen capture failed.");
            var path = RuntimeTestHelpers.GetArtifactPath(name + ".png");
            await File.WriteAllBytesAsync(path, screenshot.Chunks.Single());
            return path;
        }
        finally
        {
            window.Topmost = false;
        }
    }
}
