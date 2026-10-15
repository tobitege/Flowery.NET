using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class GlassRegressionTests
{
    [AvaloniaTheory]
    [InlineData("card")]
    [InlineData("pattern")]
    [InlineData("expandable")]
    public void CardVariantsUseLiveGlassMaterial(string variant)
    {
        DaisyCard card = variant switch
        {
            "pattern" => new DaisyPatternedCard(),
            "expandable" => new DaisyExpandableCard(),
            _ => new DaisyCard()
        };
        card.Content = "Readable content";
        card.IsGlass = true;
        var window = Show(card);
        try
        {
            var glass = card.GetVisualDescendants().OfType<DaisyGlass>().Single();
            Assert.True(glass.IsVisible);
            Assert.True(glass.EnableBackdropBlur);
            card.GlassBlur = 17;
            card.GlassOpacity = .6;
            card.GlassTint = Colors.Red;
            card.GlassTintOpacity = .3;
            card.GlassBorderOpacity = .4;
            Flush(window);
            Assert.Equal(17, glass.GlassBlur);
            Assert.Equal(.6, glass.GlassOpacity);
            Assert.Equal(Colors.Red, glass.GlassTint);
            Assert.Equal(.3, glass.GlassTintOpacity);
            Assert.Equal(.4, glass.GlassBorderOpacity);
            card.IsGlass = false;
            Flush(window);
            Assert.False(glass.IsVisible);
            Assert.False(glass.EnableBackdropBlur);
            Assert.Null(glass.BlurredBackground);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("disable")]
    [InlineData("mode")]
    [InlineData("detach")]
    public async Task ObsoleteCaptureIsDisposedAndCannotRepublish(string change)
    {
        var glass = new GatedGlass { Width = 100, Height = 100 };
        var window = Show(glass);
        try
        {
            var capture = Assert.Single(glass.Captures);
            switch (change)
            {
                case "disable": glass.EnableBackdropBlur = false; break;
                case "mode": glass.BlurMode = GlassBlurMode.Simulated; break;
                case "detach": window.Content = null; break;
            }
            Assert.True(capture.Token.IsCancellationRequested);
            var image = new OwnedImage();
            capture.Completion.SetResult(image);
            await image.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(glass.BlurredBackground);
            Assert.Single(glass.Captures);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ChangesDuringCaptureScheduleLatestCaptureAndReleaseReplacedImage()
    {
        var glass = new GatedGlass { Width = 100, Height = 100 };
        var window = Show(glass);
        try
        {
            var original = Assert.Single(glass.Captures);
            glass.GlassBlur = 12;
            glass.RefreshBackdrop();
            var old = new OwnedImage();
            original.Completion.SetResult(old);
            await old.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, glass.Captures.Count);
            Assert.Null(glass.BlurredBackground);
            var current = new OwnedImage();
            var published = WaitForImage(glass, current);
            glass.Captures[1].Completion.SetResult(current);
            await published.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(current, glass.BlurredBackground);
            glass.EnableBackdropBlur = false;
            await current.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(glass.BlurredBackground);
            glass.EnableBackdropBlur = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, glass.Captures.Count);
            glass.Captures[2].Completion.SetResult(null);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ReattachmentRejectsOldHostGenerationAndCapturesAgain()
    {
        var glass = new GatedGlass { Width = 100, Height = 100 };
        var window = Show(glass);
        try
        {
            var pending = Assert.Single(glass.Captures);
            window.Content = null;
            window.Content = glass;
            Flush(window);
            var old = new OwnedImage();
            pending.Completion.SetResult(old);
            await old.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, glass.Captures.Count);
            var current = new OwnedImage();
            var published = WaitForImage(glass, current);
            glass.Captures[1].Completion.SetResult(current);
            await published.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(current, glass.BlurredBackground);
        }
        finally { window.Close(); }
    }

    private static Task WaitForImage(DaisyGlass glass, IImage expected)
    {
        var completion = new TaskCompletionSource();
        EventHandler<AvaloniaPropertyChangedEventArgs>? changed = null;
        changed = (_, args) =>
        {
            if (args.Property != DaisyGlass.BlurredBackgroundProperty || !ReferenceEquals(glass.BlurredBackground, expected)) return;
            glass.PropertyChanged -= changed;
            completion.TrySetResult();
        };
        glass.PropertyChanged += changed;
        return completion.Task;
    }

    private static Window Show(Control control)
    {
        var window = new Window { Width = 400, Height = 300, Content = control };
        window.Show();
        Flush(window);
        return window;
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class GatedGlass : DaisyGlass
    {
        public GatedGlass() => EnableBackdropBlur = true;
        public List<Capture> Captures { get; } = [];
        protected override Task<IImage?> CaptureBackdropAsync(CancellationToken cancellationToken)
        {
            var capture = new Capture(cancellationToken);
            Captures.Add(capture);
            return capture.Completion.Task;
        }
    }

    private sealed class Capture(CancellationToken token)
    {
        public CancellationToken Token { get; } = token;
        public TaskCompletionSource<IImage?> Completion { get; } = new();
    }

    private sealed class OwnedImage : IImage, IDisposable
    {
        public Size Size => new(1, 1);
        public TaskCompletionSource Disposed { get; } = new();
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
        public void Dispose() => Disposed.TrySetResult();
    }
}
