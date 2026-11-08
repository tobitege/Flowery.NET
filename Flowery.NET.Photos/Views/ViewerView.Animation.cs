using System.Diagnostics;
using Flowery.NET.Photos.Services;
using SkiaSharp;

namespace Flowery.NET.Photos.Views;

/// <summary>Plays animated GIF and WebP files while the photo is only viewed; editing shows the first frame.</summary>
public partial class ViewerView
{
    private AnimatedImage? _animation;
    private CancellationTokenSource? _animationRun;
    private int _shownFrame;

    private async Task LoadAnimationAsync(string path, CancellationToken token)
    {
        var animation = await Task.Run(() => AnimatedImage.TryOpen(path), token);
        if (token.IsCancellationRequested || _disposed) { animation?.Dispose(); return; }
        _animation = animation; _shownFrame = 0;
        UpdateAnimation();
    }

    private void CloseAnimation()
    {
        StopAnimation();
        _animation?.Dispose(); _animation = null;
    }

    // Runs while nothing but the information or view options panel is open and the photo has no edits. Otherwise the
    // first frame shows, which is the one the editor changes and saves.
    private void UpdateAnimation()
    {
        if (_animation is not { } animation) return;
        if (TopSaveGroup.IsVisible || HasChanges || Compare.IsChecked == true || _disposed)
        {
            StopAnimation();
            if (_shownFrame != 0 && Canvas.Bitmap != null) { Canvas.ReplaceFrame(animation.CopyFrame(0)); _shownFrame = 0; }
            return;
        }
        if (_animationRun != null) return;
        var run = new CancellationTokenSource();
        _animationRun = run;
        _ = PlayAnimationAsync(animation, run);
    }

    private void StopAnimation()
    {
        _animationRun?.Cancel(); _animationRun = null;
    }

    private async Task PlayAnimationAsync(AnimatedImage animation, CancellationTokenSource run)
    {
        var clock = Stopwatch.StartNew();
        var due = TimeSpan.Zero;
        var index = 0;
        var loops = 0;
        try
        {
            while (true)
            {
                due += animation.FrameDuration(index);
                index = (index + 1) % animation.FrameCount;
                if (index == 0 && animation.Repetitions >= 0 && ++loops > animation.Repetitions) return;
                SKBitmap? frame = await Task.Run(() => animation.CopyFrame(index), run.Token);
                try
                {
                    var wait = due - clock.Elapsed;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, run.Token);
                    // A slow machine skips the waits instead of falling further behind.
                    else if (-wait > TimeSpan.FromSeconds(1)) { clock.Restart(); due = TimeSpan.Zero; }
                    if (run.IsCancellationRequested) return;
                    Canvas.ReplaceFrame(frame);
                    frame = null; _shownFrame = index;
                }
                finally { frame?.Dispose(); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidDataException or ObjectDisposedException) { }
        finally
        {
            if (_animationRun == run) _animationRun = null;
            run.Dispose();
        }
    }
}
