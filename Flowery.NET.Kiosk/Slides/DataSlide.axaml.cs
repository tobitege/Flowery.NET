using Avalonia.Animation.Easings;

namespace Flowery.NET.Kiosk.Slides;

public partial class DataSlide : UserControl
{
    private readonly Random _random = new();
    private DispatcherTimer? _updates;
    private CancellationTokenSource? _entrance;
    private TaskCompletionSource _firstUpdate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DataSlide()
    {
        InitializeComponent();
        SessionFlow.Value = 1280m;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    internal Task FirstUpdate => _firstUpdate.Task;
    internal bool IsUpdateTimerRunning => _updates is not null;

    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        StopAnimations();
        _firstUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RunEntranceAnimation();
        _updates = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _updates.Tick += OnUpdate;
        _updates.Start();
    }

    private void OnUpdate(object? sender, EventArgs args)
    {
        var cpu = _random.Next(35, 85);
        var ram = _random.Next(60, 92);
        var io = _random.Next(10, 45);
        CpuProgress.Value = cpu;
        CpuText.Text = $"{cpu}%";
        RamProgress.Value = ram;
        RamText.Text = $"{ram}%";
        IoProgress.Value = io;
        IoText.Text = $"{io}%";
        SessionFlow.Value += _random.Next(1, 15);
        HealthProgress.Value = 100 - ((cpu + ram) / 10);
        _firstUpdate.TrySetResult();
    }

    private void RunEntranceAnimation()
    {
        _entrance = new CancellationTokenSource();
        AddPopIn(HealthProgress.Parent as Control, -40, 0, _entrance.Token);
        AddPopIn(ResourceCard, 40, 0.2, _entrance.Token);
    }

    private static void AddPopIn(Control? element, double offset, double delay, CancellationToken token)
    {
        if (element is null)
            return;
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        var start = TimeSpan.FromSeconds(delay);
        _ = KioskAnimation.RunAsync(element, TranslateTransform.XProperty, offset, 0d, TimeSpan.FromSeconds(1.2), token,
            start, new ElasticEaseOut(), transform);
        _ = KioskAnimation.RunAsync(element, Visual.OpacityProperty, 0d, 1d, TimeSpan.FromSeconds(0.8), token, start);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        StopAnimations();
        _firstUpdate.TrySetCanceled();
    }

    private void StopAnimations()
    {
        if (_updates is not null)
        {
            _updates.Stop();
            _updates.Tick -= OnUpdate;
            _updates = null;
        }
        _entrance?.Cancel();
        _entrance?.Dispose();
        _entrance = null;
    }
}
