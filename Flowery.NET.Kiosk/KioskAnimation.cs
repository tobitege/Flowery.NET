using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using KeyFrameAnimation = Avalonia.Animation.Animation;

namespace Flowery.NET.Kiosk;

/// <summary>
/// The animations of the kiosk. Avalonia removes an animated value as soon as its animation ends or is cancelled, so
/// each animation sets the local end value first and covers it until the end; a cancelled animation leaves the end
/// value. During a delay, the animation already shows its start value.
/// </summary>
internal static class KioskAnimation
{
    private static readonly Dictionary<Visual, CancellationTokenSource> ActiveFades = [];

    internal static void StopFade(Visual target)
    {
        if (ActiveFades.TryGetValue(target, out var fade))
            fade.Cancel();
    }

    public static async Task FadeAsync(Visual target, double opacity, TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var fade = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            ActiveFades.Add(target, fade);
            var running = RunAsync(target, Visual.OpacityProperty, target.Opacity, opacity, duration, fade.Token);
            await running.WaitAsync(duration + TimeSpan.FromSeconds(10), cancellationToken);
            if (fade.IsCancellationRequested)
                throw new OperationCanceledException("The fade was stopped.", fade.Token);
        }
        finally
        {
            fade.Cancel();
            if (ActiveFades.TryGetValue(target, out var active) && ReferenceEquals(active, fade))
                ActiveFades.Remove(target);
        }
    }

    /// <summary>
    /// Sets <paramref name="property"/> to <paramref name="to"/> and animates it there from <paramref name="from"/>.
    /// The task ends when the animation ends or the token is cancelled. Avalonia runs animations on visuals only; for
    /// a property of the render transform, such as <see cref="TranslateTransform.XProperty"/>, the animation runs on
    /// the visual and <paramref name="valueOwner"/> names the transform that takes the end value.
    /// </summary>
    public static Task RunAsync<T>(Visual target, AvaloniaProperty<T> property, T from, T to, TimeSpan duration,
        CancellationToken cancellationToken, TimeSpan delay = default, Easing? easing = null, AvaloniaObject? valueOwner = null)
    {
        var animation = new KeyFrameAnimation
        {
            Duration = duration,
            Delay = delay,
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(property, to) } }
            }
        };
        if (easing is not null)
            animation.Easing = easing;
        var running = animation.RunAsync(target, cancellationToken);
        (valueOwner ?? target).SetValue(property, to);
        return running;
    }

    public static async Task WaitForLayoutAsync(Control element, CancellationToken cancellationToken)
    {
        if (IsLaidOut(element))
            return;
        // Avalonia raises Loaded after the layout pass, so a static element may get its last LayoutUpdated before it is
        // loaded; both events end the wait.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs args)
        {
            if (IsLaidOut(element))
                completion.TrySetResult();
        }
        element.LayoutUpdated += OnChanged;
        element.Loaded += OnChanged;
        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        finally
        {
            element.LayoutUpdated -= OnChanged;
            element.Loaded -= OnChanged;
        }
    }

    private static bool IsLaidOut(Control element) =>
        element.IsLoaded && element.Bounds.Width > 0 && element.Bounds.Height > 0;
}
