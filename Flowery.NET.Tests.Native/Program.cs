using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Embedding;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Capture;
using Flowery.Capture.Platforms;
using Flowery.Services;
using SkiaSharp;

namespace Flowery.NET.Tests.Native;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => AppBuilder.Configure<ProbeApp>().UsePlatformDetect()
        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
}

internal sealed class ProbeApp : Application
{
    private readonly List<object> _results = new();
    private string _output = string.Empty;
    private int _failures;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new DaisyUITheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        var lifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        _output = lifetime.Args?.SingleOrDefault() ?? throw new InvalidOperationException("Pass an evidence output directory.");
        Directory.CreateDirectory(_output);
        var window = new Window { Width = 800, Height = 600, Title = "Flowery review probes", ShowActivated = false, ShowInTaskbar = false };
        lifetime.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            await Check("DaisyRange matches native Slider RangeValue behavior", () => Range(window));
            await Check("Native popup screen bounds and live offsets", () => PopupGeometry(window));
            await Check("Skia backdrop captures visible children", () => GlassPixels(window));
            foreach (var order in new[] { "child-order", "higher-z", "lower-z", "nested" })
                await Check($"Glass backdrop respects {order}", () => GlassPaintOrder(window, order));
            await Check("Glass capture stays bounded in a large parent", () => GlassCaptureBounds(window));
            foreach (var presenter in new[] { false, true })
                await Check($"Glass includes outside-layout shadows, presenter={presenter}", () => GlassShadowBounds(window, presenter));
            foreach (var kind in new[] { "templated", "presenter", "border" })
                await Check($"Glass preserves {kind} clipping", () => GlassClipBounds(window, kind));
            foreach (var (blur, position) in new[]
            {
                (0d, new Point(120, 60)), (10d, new Point(120, 60)),
                (20d, new Point(4, 4)), (20d, new Point(196, 116))
            })
                await Check($"Displayed glass aligns with the backdrop, blur={blur}, position={position}",
                    () => GlassDisplayAlignment(window, blur, position));
            await Check("Skia cache notices tint, radius and lighting changes", SkiaCacheIdentity);
            await Check("Non-window TopLevel releases scaling tracker", ScaleLifetime);
            var json = JsonSerializer.Serialize(new { failures = _failures, results = _results }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(_output, "results.json"), json);
            Console.WriteLine(json);
            window.Close();
            lifetime.Shutdown(_failures == 0 ? 0 : 1);
        };
    }

    private async Task Check(string name, Func<Task> run)
    {
        try { await run(); _results.Add(new { name, passed = true }); }
        catch (Exception error) { _failures++; _results.Add(new { name, passed = false, error = error.ToString() }); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task Layout(Window window)
    {
        window.UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);
    }

    private static async Task Range(Window window)
    {
        var slider = new DaisyRange { Minimum = 10, Maximum = 20, Value = 12 };
        var native = new Slider { Minimum = 10, Maximum = 20, Value = 12 };
        window.Content = new StackPanel { Children = { slider, native } };
        await Layout(window);
        foreach (var enabled in new[] { true, false })
        foreach (var value in new[] { 17d, 9d, 21d, double.NaN })
        {
            slider.IsEnabled = native.IsEnabled = enabled;
            Require(RangeOutcome(slider, value) == RangeOutcome(native, value),
                $"DaisyRange differs from native Slider for {value}, enabled={enabled}.");
        }
    }

    private static (Type? Error, double Value) RangeOutcome(Slider slider, double value)
    {
        slider.Value = 12;
        var range = ControlAutomationPeer.CreatePeerForElement(slider) as IRangeValueProvider;
        Require(range is not null, "Missing RangeValue provider.");
        try { range!.SetValue(value); return (null, slider.Value); }
        catch (Exception error) { return (error.GetType(), slider.Value); }
    }

    private static async Task PopupGeometry(Window window)
    {
        var popover = new ProbePopover
        {
            TriggerContent = "Open", PopoverContent = new Border { Width = 240, Height = 160, Background = Brushes.Coral },
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
            IsLightDismissEnabled = false
        };
        window.Content = popover;
        await Layout(window);
        popover.IsOpen = true;
        await Layout(window);
        var popup = popover.Popup;
        try
        {
            var root = TopLevel.GetTopLevel(popup.Child)!;
            Require(root is not Window && root.TryGetPlatformHandle() is not null, "Expected a native popup root.");
            AssertInsideScreen(window, root);
            var before = root.PointToScreen(default);
            popover.HorizontalOffset = -80;
            await Layout(window);
            var after = root.PointToScreen(default);
            Require(after != before, "Changing the offset did not reposition the popup.");
            window.Width = 650;
            window.Height = 470;
            await Layout(window);
            AssertInsideScreen(window, root);
        }
        finally { popover.IsOpen = false; }
    }

    private static void AssertInsideScreen(Window window, TopLevel popup)
    {
        var area = window.Screens.ScreenFromWindow(window)!.WorkingArea;
        var origin = popup.PointToScreen(default);
        var right = origin.X + popup.Bounds.Width * popup.RenderScaling;
        var bottom = origin.Y + popup.Bounds.Height * popup.RenderScaling;
        Require(origin.X >= area.X && origin.Y >= area.Y && right <= area.Right + 1 && bottom <= area.Bottom + 1,
            $"Popup {origin}, {popup.Bounds.Size} outside screen {area}.");
    }

    private async Task GlassPixels(Window window)
    {
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue };
        var red = new Border { Width = 90, Height = 100, Background = Brushes.Red };
        Canvas.SetLeft(red, 160); Canvas.SetTop(red, 80);
        canvas.Children.Add(red);
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 10, GlassTintOpacity = 0 };
        Canvas.SetLeft(glass, 120); Canvas.SetTop(glass, 60);
        canvas.Children.Add(glass);
        window.Content = canvas;
        await Layout(window);
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Require(capture is not null, "No backdrop image was captured.");
        var file = Path.Combine(_output, "glass-backdrop.png");
        capture!.Save(file, PngBitmapEncoderOptions.Default);
        using var pixels = SKBitmap.Decode(file);
        // Capture starts 10 DIPs before the glass and uses 0.75 pixels per DIP.
        var center = pixels.GetPixel((int)(80 * .75), (int)(60 * .75));
        Require(center.Red > 200 && center.Blue < 40, $"Visible red child missing from backdrop: {center}.");
        var captured = new TaskCompletionSource();
        glass.PropertyChanged += (_, change) =>
        {
            if (change.Property == DaisyGlass.BlurredBackgroundProperty && glass.BlurredBackground is not null)
                captured.TrySetResult();
        };
        glass.EnableBackdropBlur = true;
        await captured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var image = glass.GetVisualDescendants().OfType<Image>().Single(control => control.Name == "PART_BlurImage");
        Require(image.Effect is BlurEffect { Radius: 10 }, $"Unexpected blur effect: {image.Effect}.");
        var renderedFile = Path.Combine(_output, "glass-rendered.png");
        window.Topmost = true;
        try
        {
            var result = await new WindowsScreenCapture().CaptureControlAsync(canvas,
                new ScreenCaptureOptions { CaptureMargin = new Thickness(0), ScrollSettleDelayMs = 100 });
            Require(result.Success, result.ErrorMessage ?? "Screen capture failed.");
            await File.WriteAllBytesAsync(renderedFile, result.Chunks.Single());
        }
        finally { window.Topmost = false; }
        using var actual = SKBitmap.Decode(renderedFile);
        var mixedPixels = 0;
        for (var x = 140; x < 185; x++)
        {
            var pixel = actual.GetPixel((int)(x * window.RenderScaling), (int)(130 * window.RenderScaling));
            if (pixel.Red > 15 && pixel.Blue > 15 && pixel.Green < 10) mixedPixels++;
        }
        Require(mixedPixels > 2, "The displayed glass did not blur the red/blue edge.");
    }

    private async Task GlassPaintOrder(Window window, string order)
    {
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue };
        var red = new Border { Width = 90, Height = 100, Background = Brushes.Red };
        Canvas.SetLeft(red, 160); Canvas.SetTop(red, 80);
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 10 };
        Canvas.SetLeft(glass, 120); Canvas.SetTop(glass, 60);
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
        else if (order == "lower-z") glass.ZIndex = 2;
        window.Content = canvas;
        await Layout(window);
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Require(capture is not null, "Missing paint-order capture.");
        var file = Path.Combine(_output, $"glass-order-{order}.png");
        capture!.Save(file, PngBitmapEncoderOptions.Default);
        using var pixels = SKBitmap.Decode(file);
        var center = pixels.GetPixel(60, 45);
        var expectsRed = order == "lower-z";
        Require(expectsRed ? center.Red > 200 && center.Blue < 40 : center.Blue > 200 && center.Red < 40,
            $"Wrong backdrop for {order}: {center}.");
        Require(red.Opacity == 1 && glass.Opacity == 1, "Capture changed a visual's opacity.");
    }

    private async Task GlassCaptureBounds(Window window)
    {
        var canvas = new Canvas
        {
            Width = 800, Height = 20000, Background = Brushes.Blue,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        var outside = new RenderCounter { Width = 100, Height = 100 };
        Canvas.SetLeft(outside, 500); Canvas.SetTop(outside, 15000);
        canvas.Children.Add(outside);
        var red = new RenderCounter { Width = 40, Height = 40 };
        Canvas.SetLeft(red, 450); Canvas.SetTop(red, 130);
        canvas.Children.Add(red);
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 10 };
        Canvas.SetLeft(glass, 400); Canvas.SetTop(glass, 100);
        canvas.Children.Add(glass);
        window.Content = canvas;
        await Layout(window);
        outside.RenderCount = red.RenderCount = 0;
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Require(capture is not null, "Missing bounded capture.");
        Require(capture!.PixelSize == new PixelSize(165, 120), $"Unexpected capture size: {capture.PixelSize}.");
        Require(outside.RenderCount == 0, "The capture rendered content outside its source rectangle.");
        Require(red.RenderCount > 0, "The capture skipped content inside its source rectangle.");
        var file = Path.Combine(_output, "glass-bounded.png");
        capture.Save(file, PngBitmapEncoderOptions.Default);
        using var pixels = SKBitmap.Decode(file);
        var center = pixels.GetPixel(60, 45);
        Require(center.Red > 200 && center.Blue < 40, $"Incorrect capture translation: {center}.");
    }

    private async Task GlassShadowBounds(Window window, bool presenter)
    {
        var shadow = new BoxShadows(new BoxShadow { OffsetX = 144, Blur = 8, Spread = 4, Color = Colors.Red });
        Control caster = presenter
            ? new ContentPresenter { Background = Brushes.Lime, BoxShadow = shadow }
            : new Border { Background = Brushes.Lime, BoxShadow = shadow };
        caster.Width = 48; caster.Height = 80;
        Canvas.SetLeft(caster, 24); Canvas.SetTop(caster, 88);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { caster } };
        // The caster ends at x=72; the capture starts at x=120. Only its shadow overlaps.
        await AssertGlassCapture(window, canvas, $"shadow-{(presenter ? "presenter" : "border")}",
            new Point(192, 120), SKColors.Red);
    }

    private async Task GlassClipBounds(Window window, string kind)
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
                CornerRadius = new CornerRadius(40), Content = new Border { Background = Brushes.Red },
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch
            },
            _ => new Border { CornerRadius = new CornerRadius(40), Child = new Border { Background = Brushes.Red } }
        };
        control.Width = 120; control.Height = 100; control.ClipToBounds = true;
        Canvas.SetLeft(control, 144); Canvas.SetTop(control, 80);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { control } };
        // Only Border clips its child to rounded corners; template properties alone must not do so.
        await AssertGlassCapture(window, canvas, $"clip-{kind}", new Point(148, 84),
            kind == "border" ? SKColors.Blue : SKColors.Red);
    }

    private async Task AssertGlassCapture(Window window, Canvas canvas, string name, Point sample, SKColor expectedColor)
    {
        window.Content = canvas;
        await Layout(window);
        var referenceFile = Path.Combine(_output, $"glass-{name}-reference.png");
        // Full native Avalonia rendering is the oracle; the glass is added only after this image is saved.
        using (var reference = new RenderTargetBitmap(new PixelSize(300, 195), new Vector(72, 72)))
        {
            reference.Render(canvas);
            reference.Save(referenceFile, PngBitmapEncoderOptions.Default);
        }
        var glass = new CaptureGlass { Width = 200, Height = 140, GlassBlur = 0, GlassTintOpacity = 0, CornerRadius = default };
        Canvas.SetLeft(glass, 120); Canvas.SetTop(glass, 60);
        canvas.Children.Add(glass);
        await Layout(window);
        using var capture = (RenderTargetBitmap?)await glass.Capture();
        Require(capture is not null, $"Missing {name} capture.");
        Require(capture!.PixelSize == new PixelSize(150, 105), $"Unexpected {name} capture size: {capture.PixelSize}.");
        var file = Path.Combine(_output, $"glass-{name}-capture.png");
        capture.Save(file, PngBitmapEncoderOptions.Default);

        var published = new TaskCompletionSource();
        glass.PropertyChanged += (_, change) =>
        {
            if (change.Property == DaisyGlass.BlurredBackgroundProperty && glass.BlurredBackground is not null)
                published.TrySetResult();
        };
        glass.EnableBackdropBlur = true;
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        window.Topmost = true;
        try
        {
            var screenshot = await new WindowsScreenCapture().CaptureControlAsync(canvas,
                new ScreenCaptureOptions { CaptureMargin = new Thickness(0), ScrollSettleDelayMs = 100 });
            Require(screenshot.Success, screenshot.ErrorMessage ?? "Screen capture failed.");
            await File.WriteAllBytesAsync(Path.Combine(_output, $"glass-{name}-screen.png"), screenshot.Chunks.Single());
        }
        finally { window.Topmost = false; }

        using var expected = SKBitmap.Decode(referenceFile);
        using var actual = SKBitmap.Decode(file);
        var sampleX = (int)(sample.X * .75);
        var sampleY = (int)(sample.Y * .75);
        Require(expected.GetPixel(sampleX, sampleY) == expectedColor, $"The {name} reference does not contain the expected feature.");
        Require(actual.GetPixel(sampleX - 90, sampleY - 45) == expectedColor, $"The {name} capture lost the expected feature.");
        // Skia clip-edge coverage differed by 8/255 at two pixels when rendering at the translated device origin.
        const int channelTolerance = 8;
        var differences = 0;
        for (var y = 0; y < actual.Height; y++)
        for (var x = 0; x < actual.Width; x++)
        {
            var a = actual.GetPixel(x, y);
            var b = expected.GetPixel(x + 90, y + 45);
            if (Math.Abs(a.Red - b.Red) > channelTolerance || Math.Abs(a.Green - b.Green) > channelTolerance
                || Math.Abs(a.Blue - b.Blue) > channelTolerance || Math.Abs(a.Alpha - b.Alpha) > channelTolerance) differences++;
        }
        Require(differences == 0, $"The {name} capture differs from native rendering at {differences} pixels.");
    }

    private async Task GlassDisplayAlignment(Window window, double blur, Point position)
    {
        var name = $"glass-alignment-{blur}-{position.X}-{position.Y}";
        var marker = new Border { Width = 160, Height = 60, Background = Brushes.Red };
        Canvas.SetLeft(marker, position.X - 20); Canvas.SetTop(marker, position.Y + 60);
        var canvas = new Canvas { Width = 400, Height = 260, Background = Brushes.Blue, Children = { marker } };
        window.Content = canvas;
        await Layout(window);
        using var reference = SKBitmap.Decode(await SaveScreen(window, canvas, name + "-reference"));
        var glass = new CaptureGlass
        {
            Width = 200, Height = 140, GlassBlur = blur, GlassTintOpacity = 0, CornerRadius = default
        };
        Canvas.SetLeft(glass, position.X); Canvas.SetTop(glass, position.Y);
        canvas.Children.Add(glass);
        await Layout(window);
        var published = new TaskCompletionSource();
        glass.PropertyChanged += (_, change) =>
        {
            if (change.Property == DaisyGlass.BlurredBackgroundProperty && glass.BlurredBackground is not null)
                published.TrySetResult();
        };
        glass.EnableBackdropBlur = true;
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var actual = SKBitmap.Decode(await SaveScreen(window, canvas, name + "-screen"));
        var image = glass.GetVisualDescendants().OfType<Image>().Single(control => control.Name == "PART_BlurImage");
        foreach (var horizontal in new[] { true, false })
        {
            var expected = RedSpan(reference, position, window.RenderScaling, horizontal);
            var displayed = RedSpan(actual, position, window.RenderScaling, horizontal);
            Require(expected.First >= 0 && expected.Last > expected.First, "Reference marker is missing.");
            Require(Math.Abs(expected.First - displayed.First) <= 2 && Math.Abs(expected.Last - displayed.Last) <= 2,
                $"Displayed marker is shifted or scaled, horizontal={horizontal}: expected {expected}, actual {displayed}; " +
                $"image bounds={image.Bounds}, source size={image.Source?.Size}, bitmap DPI={(image.Source as Bitmap)?.Dpi}.");
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
            if (color.Red <= color.Blue) continue;
            if (first < 0) first = offset;
            last = offset;
        }
        return (first, last);
    }

    private async Task<string> SaveScreen(Window window, Control control, string name)
    {
        window.Topmost = true;
        try
        {
            var screenshot = await new WindowsScreenCapture().CaptureControlAsync(control,
                new ScreenCaptureOptions { CaptureMargin = new Thickness(0), ScrollSettleDelayMs = 100 });
            Require(screenshot.Success, screenshot.ErrorMessage ?? "Screen capture failed.");
            var path = Path.Combine(_output, name + ".png");
            await File.WriteAllBytesAsync(path, screenshot.Chunks.Single());
            return path;
        }
        finally { window.Topmost = false; }
    }

    private static Task SkiaCacheIdentity()
    {
        var type = typeof(DaisyGlass).Assembly.GetType("Flowery.Controls.SkiaGlassDrawOperation", throwOnError: true)!;
        object[] values = { new Rect(0, 0, 100, 100), 10d, Colors.Red, .5d, 5d, 1d, 45d, .2d, .6d, .6d, .3d, .2d };
        using var original = (ICustomDrawOperation)Activator.CreateInstance(type, values)!;
        foreach (var (index, value) in new (int, object)[] { (2, Colors.Blue), (4, 12d), (6, 90d) })
        {
            var changed = (object[])values.Clone();
            changed[index] = value;
            using var operation = (ICustomDrawOperation)Activator.CreateInstance(type, changed)!;
            Require(!original.Equals(operation), $"Skia operation ignored visual parameter {index}.");
        }
        return Task.CompletedTask;
    }

    private static async Task ScaleLifetime()
    {
        var button = new DaisyButton();
        FloweryScaleManager.SetEnableScaling(button, true);
        var root = new EmbeddableControlRoot { Content = button };
        var trackers = (IDictionary)typeof(FloweryScaleManager)
            .GetField("_topLevelTrackers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try
        {
            root.Prepare();
            root.UpdateLayout();
            Require(trackers.Contains(root), "Embedded TopLevel was not registered for scaling.");
        }
        finally { root.Dispose(); }
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Require(!trackers.Contains(root), "Disposed embedded TopLevel is retained by the scaling registry.");
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

    private sealed class CaptureGlass : DaisyGlass
    {
        public Task<IImage?> Capture() => CaptureBackdropAsync(CancellationToken.None);
    }

    private sealed class ProbePopover : DaisyPopover
    {
        public Popup Popup { get; private set; } = null!;
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Popup = e.NameScope.Find<Popup>("PART_Popup")!;
        }
    }

}
