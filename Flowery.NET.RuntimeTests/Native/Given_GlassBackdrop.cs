using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Flowery.Capture;
using Flowery.Capture.Platforms;
using Flowery.Controls;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

/// <summary>
/// Backdrop capture and display of <see cref="DaisyGlass"/> against native Skia rendering and screen captures.
/// </summary>
public class Given_GlassBackdrop
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

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
    [InlineData("child-order")]
    [InlineData("higher-z")]
    [InlineData("lower-z")]
    [InlineData("nested")]
    public async Task When_Capturing_The_Backdrop_The_Paint_Order_Is_Respected(string order)
    {
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue };
        var red = new Border { Width = 90, Height = 100, Background = Brushes.Red };
        Canvas.SetLeft(red, 160);
        Canvas.SetTop(red, 80);
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 10 };
        Canvas.SetLeft(glass, 120);
        Canvas.SetTop(glass, 60);
        if (order == "nested")
            canvas.Children.Add(new Canvas { Width = 400, Height = 260, Children = { glass } });
        else
            canvas.Children.Add(glass);
        canvas.Children.Add(red);
        if (order == "higher-z")
        {
            red.ZIndex = 2;
            canvas.Children.Move(1, 0);
        }
        else if (order == "lower-z")
        {
            glass.ZIndex = 2;
        }

        await RuntimeTestHelpers.AttachAsync(canvas);
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Assert.NotNull(capture);
        using var pixels = Decode(capture, $"glass-order-{order}");
        var center = pixels.GetPixel(60, 45);
        var expectsRed = order == "lower-z";
        Assert.True(expectsRed ? center.Red > 200 && center.Blue < 40 : center.Blue > 200 && center.Red < 40,
            $"Wrong backdrop for {order}: {center}.");
        Assert.True(red.Opacity == 1 && glass.Opacity == 1, "Capture changed a visual's opacity.");
    }

    [Fact]
    public async Task When_The_Parent_Is_Large_The_Capture_Stays_Bounded()
    {
        var canvas = new Canvas
        {
            Width = 800,
            Height = 20000,
            Background = Brushes.Blue,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        var outside = new RenderCounter { Width = 100, Height = 100 };
        Canvas.SetLeft(outside, 500);
        Canvas.SetTop(outside, 15000);
        canvas.Children.Add(outside);
        var red = new RenderCounter { Width = 40, Height = 40 };
        Canvas.SetLeft(red, 450);
        Canvas.SetTop(red, 130);
        canvas.Children.Add(red);
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 10 };
        Canvas.SetLeft(glass, 400);
        Canvas.SetTop(glass, 100);
        canvas.Children.Add(glass);
        await RuntimeTestHelpers.AttachAsync(canvas);

        outside.RenderCount = red.RenderCount = 0;
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Assert.NotNull(capture);
        Assert.True(capture.PixelSize == new PixelSize(165, 120), $"Unexpected capture size: {capture.PixelSize}.");
        Assert.True(outside.RenderCount == 0, "The capture rendered content outside its source rectangle.");
        Assert.True(red.RenderCount > 0, "The capture skipped content inside its source rectangle.");
        using var pixels = Decode(capture, "glass-bounded");
        var center = pixels.GetPixel(60, 45);
        Assert.True(center.Red > 200 && center.Blue < 40, $"Incorrect capture translation: {center}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_A_Shadow_Reaches_Outside_Its_Layout_The_Capture_Includes_It(bool presenter)
    {
        var shadow = new BoxShadows(new BoxShadow { OffsetX = 144, Blur = 8, Spread = 4, Color = Colors.Red });
        Control caster = presenter
            ? new ContentPresenter { Background = Brushes.Lime, BoxShadow = shadow }
            : new Border { Background = Brushes.Lime, BoxShadow = shadow };
        caster.Width = 48;
        caster.Height = 80;
        Canvas.SetLeft(caster, 24);
        Canvas.SetTop(caster, 88);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { caster } };
        // The caster ends at x=72; the capture starts at x=120. Only its shadow overlaps.
        await AssertGlassCaptureAsync(canvas, $"shadow-{(presenter ? "presenter" : "border")}", new Point(192, 120), SKColors.Red);
    }

    [Theory]
    [InlineData("templated")]
    [InlineData("presenter")]
    [InlineData("border")]
    public async Task When_A_Child_Is_Clipped_The_Capture_Preserves_The_Clip(string kind)
    {
        Control control = kind switch
        {
            "templated" => new ContentControl
            {
                CornerRadius = new CornerRadius(40),
                Template = new FuncControlTemplate<ContentControl>((_, _) => new Border { Background = Brushes.Red })
            },
            "presenter" => new ContentPresenter
            {
                CornerRadius = new CornerRadius(40),
                Content = new Border { Background = Brushes.Red },
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            },
            _ => new Border { CornerRadius = new CornerRadius(40), Child = new Border { Background = Brushes.Red } }
        };
        control.Width = 120;
        control.Height = 100;
        control.ClipToBounds = true;
        Canvas.SetLeft(control, 144);
        Canvas.SetTop(control, 80);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { control } };
        // Only Border clips its child to rounded corners; template properties alone must not do so.
        await AssertGlassCaptureAsync(canvas, $"clip-{kind}", new Point(148, 84), kind == "border" ? SKColors.Blue : SKColors.Red);
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

    private static async Task AssertGlassCaptureAsync(Canvas canvas, string name, Point sample, SKColor expectedColor)
    {
        var window = RuntimeTestHelpers.GetWindow();
        await RuntimeTestHelpers.AttachAsync(canvas);
        var referenceFile = RuntimeTestHelpers.GetArtifactPath($"glass-{name}-reference.png");
        // Full native Avalonia rendering is the oracle; the glass is added only after this image is saved.
        using (var reference = new RenderTargetBitmap(new PixelSize(300, 195), new Vector(72, 72)))
        {
            reference.Render(canvas);
            reference.Save(referenceFile, PngBitmapEncoderOptions.Default);
        }

        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 0, GlassTintOpacity = 0, CornerRadius = default };
        Canvas.SetLeft(glass, 120);
        Canvas.SetTop(glass, 60);
        canvas.Children.Add(glass);
        await RuntimeTestHelpers.WaitForRenderAsync();
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Assert.NotNull(capture);
        Assert.True(capture.PixelSize == new PixelSize(150, 105), $"Unexpected {name} capture size: {capture.PixelSize}.");
        using var actual = Decode(capture, $"glass-{name}-capture");

        await EnableBackdropBlurAsync(glass);
        await SaveScreenAsync(window, canvas, $"glass-{name}-screen");

        using var expected = SKBitmap.Decode(referenceFile);
        var sampleX = (int)(sample.X * .75);
        var sampleY = (int)(sample.Y * .75);
        Assert.True(expected.GetPixel(sampleX, sampleY) == expectedColor, $"The {name} reference does not contain the expected feature.");
        Assert.True(actual.GetPixel(sampleX - 90, sampleY - 45) == expectedColor, $"The {name} capture lost the expected feature.");
        // Skia clip-edge coverage differed by 8/255 at two pixels when rendering at the translated device origin.
        const int channelTolerance = 8;
        var differences = 0;
        for (var py = 0; py < actual.Height; py++)
        for (var px = 0; px < actual.Width; px++)
        {
            var a = actual.GetPixel(px, py);
            var b = expected.GetPixel(px + 90, py + 45);
            if (Math.Abs(a.Red - b.Red) > channelTolerance || Math.Abs(a.Green - b.Green) > channelTolerance
                || Math.Abs(a.Blue - b.Blue) > channelTolerance || Math.Abs(a.Alpha - b.Alpha) > channelTolerance)
            {
                differences++;
            }
        }

        Assert.True(differences == 0, $"The {name} capture differs from native rendering at {differences} pixels.");
    }

    private static async Task EnableBackdropBlurAsync(DaisyGlass glass)
    {
        var waiter = new BlurPublishedWaiter();
        glass.PropertyChanged += waiter.OnPropertyChanged;
        try
        {
            glass.EnableBackdropBlur = true;
            await waiter.Published.WaitAsync(PublishTimeout);
        }
        finally
        {
            glass.PropertyChanged -= waiter.OnPropertyChanged;
        }
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

    private static SKBitmap Decode(RenderTargetBitmap capture, string name)
    {
        var file = RuntimeTestHelpers.GetArtifactPath(name + ".png");
        capture.Save(file, PngBitmapEncoderOptions.Default);
        return SKBitmap.Decode(file);
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

    private sealed class RenderCounter : Control
    {
        public int RenderCount { get; set; }

        public override void Render(DrawingContext context)
        {
            RenderCount++;
            context.DrawRectangle(Brushes.Red, null, new Rect(Bounds.Size));
        }
    }

    /// <summary>Completes when a glass publishes its blurred backdrop.</summary>
    private sealed class BlurPublishedWaiter
    {
        private readonly TaskCompletionSource _published = new();

        public Task Published => _published.Task;

        public void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == DaisyGlass.BlurredBackgroundProperty && sender is DaisyGlass { BlurredBackground: not null })
                _published.TrySetResult();
        }
    }

    private sealed class CaptureGlass : DaisyGlass
    {
        public Task<IImage?> Capture() => CaptureBackdropAsync(CancellationToken.None);
    }
}
