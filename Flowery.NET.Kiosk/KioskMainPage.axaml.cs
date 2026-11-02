using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.NET.Kiosk;

public partial class KioskMainPage : UserControl
{
    private readonly FlowerySlideshowController _controller;
    private readonly KioskSlideInfo[] _slides;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private CancellationTokenSource? _navigation;
    private CancellationTokenSource? _progress;
    private Task? _shutdownTask;
    private bool _manualMode;
    private bool _usingA;
    private bool _started;
    private bool _shutdown;
    private int _currentIndex = -1;

    public KioskMainPage()
    {
        InitializeComponent();
        _slides =
        [
            new("Authentication", "Login • Forms • Validation", () => new Slides.LoginSlide(), "Halloween"),
            new("Data Visualization", "Stats • Progress • Indicators", () => new Slides.DataSlide(), "Synthwave"),
            new("Communication", "Chat • Badges • Avatars", () => new Slides.FeedbackSlide(), "Nord"),
            new("Enhanced Controls", "Weather • Color Picker • Data", () => new Slides.ShowcaseSlide(), "Dark"),
            new("Runtime Scaling", "Zoom • Scale • Responsive", () => new Slides.ScalingSlide(), "Dracula"),
            new("Background Patterns", "Textures • Cards • Surfaces", () => new Slides.PatternsSlide(), "Coffee")
        ];
        _controller = new FlowerySlideshowController(NavigateFromController, () => _slides.Length, () => _currentIndex)
        {
            Interval = 8,
            WrapAround = true,
            Mode = FlowerySlideshowMode.Manual
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;
    }

    public event EventHandler<Exception>? NavigationFailed;
    internal event EventHandler<int>? SlideShown;
    internal Task Ready => _ready.Task;
    internal int CurrentIndex => _currentIndex;
    internal int SlideCount => _slides.Length;
    internal Control? CurrentSlide => (_usingA ? SlidePresenterA : SlidePresenterB).Content as Control;
    internal string CurrentTitle => SlideTitle.Text ?? string.Empty;
    internal string CurrentKeywords => KeywordsView.Text ?? string.Empty;
    internal double ProgressValue => SlideProgress.Value;
    internal bool IsAutoRunning => _controller.IsRunning;
    internal double Interval => _controller.Interval;
    internal KioskSlideInfo SlideAt(int index) => _slides[index];

    internal void ConfigureManualMode(bool manual)
    {
        _manualMode = manual;
        if (_ready.Task.IsCompletedSuccessfully && !_shutdown)
        {
            _controller.Mode = manual ? FlowerySlideshowMode.Manual : FlowerySlideshowMode.Slideshow;
            if (manual)
                _controller.Stop();
            else
                StartProgress();
        }
    }

    /// <summary>Reports a failure that happened outside a navigation, such as during the shutdown.</summary>
    internal void ReportFailure(Exception exception) => NavigationFailed?.Invoke(this, exception);

    private async void OnLoaded(object? sender, RoutedEventArgs args)
    {
        if (_started || _shutdown)
            return;
        _started = true;
        try
        {
            await NavigateToAsync(0, _lifetime.Token);
            _ready.TrySetResult();
            ConfigureManualMode(_manualMode);
            Focus();
        }
        catch (OperationCanceledException) when (_shutdown)
        {
            _ready.TrySetCanceled();
        }
        catch (Exception exception)
        {
            _ready.TrySetException(exception);
            NavigationFailed?.Invoke(this, exception);
        }
    }

    private async void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        try
        {
            await ShutdownAsync();
        }
        catch (Exception exception)
        {
            NavigationFailed?.Invoke(this, exception);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (!_ready.Task.IsCompletedSuccessfully || _shutdown)
            return;
        switch (args.Key)
        {
            case Key.Right:
            case Key.Space:
            case Key.N:
                NextSlide();
                args.Handled = true;
                break;
            case Key.Left:
            case Key.P:
                PreviousSlide();
                args.Handled = true;
                break;
        }
    }

    public void NextSlide() => _controller.Next();
    public void PreviousSlide() => _controller.Previous();

    private async void NavigateFromController(int index)
    {
        try
        {
            await NavigateToAsync(index, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer navigation or shutdown cancelled this transition.
        }
        catch (Exception exception)
        {
            _controller.Stop();
            NavigationFailed?.Invoke(this, exception);
        }
    }

    internal async Task NavigateToAsync(int index, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_shutdown, this);
        if (index < 0 || index >= _slides.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index == _currentIndex && _navigation is null)
            return;

        _navigation?.Cancel();
        using var navigation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        _navigation = navigation;
        var token = navigation.Token;
        var gateHeld = false;
        try
        {
            await _navigationGate.WaitAsync(token);
            gateHeld = true;
            _controller.Stop();
            StopProgress();
            var outgoingContainer = _usingA ? SlideContentA : SlideContentB;
            var incomingContainer = _usingA ? SlideContentB : SlideContentA;
            var outgoingPresenter = _usingA ? SlidePresenterA : SlidePresenterB;
            var incomingPresenter = _usingA ? SlidePresenterB : SlidePresenterA;
            var slide = _slides[index];
            incomingPresenter.Content = null;
            incomingContainer.IsVisible = false;
            outgoingContainer.Opacity = 1;
            OverlayLayer.Opacity = 1;
            if (_currentIndex >= 0)
                await Task.WhenAll(
                    KioskAnimation.FadeAsync(outgoingContainer, 0, TimeSpan.FromMilliseconds(250), token),
                    KioskAnimation.FadeAsync(OverlayLayer, 0, TimeSpan.FromMilliseconds(200), token));

            token.ThrowIfCancellationRequested();
            outgoingContainer.IsVisible = false;
            outgoingPresenter.Content = null;
            if (!DaisyThemeManager.ApplyTheme(slide.Theme))
                throw new InvalidOperationException($"Unable to apply kiosk theme '{slide.Theme}'.");

            var content = slide.ContentFactory();
            incomingPresenter.Content = content;
            incomingContainer.IsVisible = true;
            incomingContainer.Opacity = _currentIndex < 0 ? 1 : 0;
            SlideTitle.Text = slide.Title.ToUpperInvariant();
            KeywordsView.Text = slide.Keywords;
            await KioskAnimation.WaitForLayoutAsync(content, token);
            if (_currentIndex >= 0)
                await Task.WhenAll(
                    KioskAnimation.FadeAsync(incomingContainer, 1, TimeSpan.FromMilliseconds(400), token),
                    KioskAnimation.FadeAsync(OverlayLayer, 1, TimeSpan.FromMilliseconds(400), token));

            token.ThrowIfCancellationRequested();
            _usingA = !_usingA;
            _currentIndex = index;
            incomingContainer.Opacity = 1;
            OverlayLayer.Opacity = 1;
            StartProgress();
            if (!_manualMode)
            {
                _controller.Mode = FlowerySlideshowMode.Slideshow;
                _controller.Start();
            }
            SlideShown?.Invoke(this, index);
            Focus();
        }
        finally
        {
            if (ReferenceEquals(_navigation, navigation))
                _navigation = null;
            if (gateHeld)
                _navigationGate.Release();
        }
    }

    internal async Task WaitForSlideAsync(int index, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnShown(object? sender, int shown)
        {
            if (shown == index)
                completion.TrySetResult();
        }
        void OnFailed(object? sender, Exception exception) => completion.TrySetException(exception);
        SlideShown += OnShown;
        NavigationFailed += OnFailed;
        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        finally
        {
            SlideShown -= OnShown;
            NavigationFailed -= OnFailed;
        }
    }

    private void StartProgress()
    {
        StopProgress();
        var progress = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _progress = progress;
        _ = RunProgressAsync(KioskAnimation.RunAsync(SlideProgress, RangeBase.ValueProperty, 0d, 100d,
            TimeSpan.FromSeconds(_controller.Interval), progress.Token));
    }

    private async Task RunProgressAsync(Task animation)
    {
        try
        {
            await animation;
        }
        catch (Exception exception)
        {
            NavigationFailed?.Invoke(this, exception);
        }
    }

    private void StopProgress()
    {
        if (_progress is not null)
        {
            _progress.Cancel();
            _progress.Dispose();
            _progress = null;
        }
        SlideProgress.Value = 0;
    }

    internal Task ShutdownAsync() => _shutdownTask ??= ShutdownCoreAsync();

    private async Task ShutdownCoreAsync()
    {
        _shutdown = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        KeyDown -= OnKeyDown;
        // Cancel runs the callbacks on the UI thread, which the animations need; CancelAsync would not.
        _lifetime.Cancel();
        _navigation?.Cancel();
        KioskAnimation.StopFade(SlideContentA);
        KioskAnimation.StopFade(SlideContentB);
        KioskAnimation.StopFade(OverlayLayer);
        _controller.Dispose();
        StopProgress();
        SlidePresenterA.Content = null;
        SlidePresenterB.Content = null;
        await _navigationGate.WaitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        _navigationGate.Dispose();
        _lifetime.Dispose();
    }
}

internal sealed record KioskSlideInfo(string Title, string Keywords, Func<Control> ContentFactory, string Theme);
