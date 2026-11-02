namespace Flowery.NET.Kiosk.Slides;

public partial class LoginSlide : UserControl
{
    private DispatcherTimer? _featureTimer;
    private CancellationTokenSource? _entrance;
    private TaskCompletionSource _featuresShown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _featureIndex;
    private TextBlock[] _features = [];

    public LoginSlide()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    internal Task FeaturesShown => _featuresShown.Task;
    internal bool IsFeatureTimerRunning => _featureTimer is not null;

    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        StopEntrance();
        _features = [Feature1, Feature2, Feature3, Feature4, Feature5, Feature6, Feature7];
        _featuresShown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _entrance = new CancellationTokenSource();
        _featureIndex = 0;
        foreach (var feature in _features)
            feature.Opacity = 0;
        _featureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _featureTimer.Tick += OnFeatureTick;
        _featureTimer.Start();
    }

    private async void OnFeatureTick(object? sender, EventArgs args)
    {
        var index = _featureIndex++;
        if (index >= _features.Length || _entrance is null)
            return;
        var token = _entrance.Token;
        if (index == _features.Length - 1)
            StopTimer();
        try
        {
            await KioskAnimation.FadeAsync(_features[index], 1, TimeSpan.FromMilliseconds(300), token);
            if (index == _features.Length - 1)
                _featuresShown.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            _featuresShown.TrySetCanceled(token);
        }
        catch (Exception exception)
        {
            _featuresShown.TrySetException(exception);
            throw;
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args) => StopEntrance();

    private void StopTimer()
    {
        if (_featureTimer is null)
            return;
        _featureTimer.Stop();
        _featureTimer.Tick -= OnFeatureTick;
        _featureTimer = null;
    }

    private void StopEntrance()
    {
        StopTimer();
        _entrance?.Cancel();
        foreach (var feature in _features)
            KioskAnimation.StopFade(feature);
        _entrance?.Dispose();
        _entrance = null;
    }
}
