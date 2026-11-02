namespace Flowery.NET.Kiosk.Slides;

public partial class ScalingSlide : UserControl
{
    private const double TargetZoomPercent = 200;
    private const double ZoomStep = (TargetZoomPercent - 50) / 60;
    private DispatcherTimer? _zoomTimer;
    private TaskCompletionSource _zoomFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private double _currentZoomPercent;
    private bool _active;
    private bool _previousEnabled;
    private double _previousMinimum;
    private double _previousMaximum;
    private double? _previousOverride;

    public ScalingSlide()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    internal Task ZoomFinished => _zoomFinished.Task;
    internal bool IsZoomTimerRunning => _zoomTimer is not null;

    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        if (_active)
            return;
        _active = true;
        _previousEnabled = FloweryScaleManager.IsEnabled;
        _previousMinimum = FloweryScaleManager.MinScaleFactor;
        _previousMaximum = FloweryScaleManager.MaxScaleFactor;
        _previousOverride = FloweryScaleManager.OverrideScaleFactor;
        FloweryScaleManager.IsEnabled = true;
        FloweryScaleManager.MinScaleFactor = 0.5;
        FloweryScaleManager.MaxScaleFactor = 2;
        _zoomFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _currentZoomPercent = 50;
        ApplyZoom();
        _zoomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _zoomTimer.Tick += OnZoomTick;
        _zoomTimer.Start();
    }

    private void OnZoomTick(object? sender, EventArgs args)
    {
        _currentZoomPercent = Math.Min(TargetZoomPercent, _currentZoomPercent + ZoomStep);
        ApplyZoom();
        if (_currentZoomPercent >= TargetZoomPercent)
        {
            StopTimer();
            _zoomFinished.TrySetResult();
        }
    }

    private void ApplyZoom()
    {
        ZoomProgress.Value = _currentZoomPercent;
        var factor = _currentZoomPercent / 100;
        FloweryScaleManager.OverrideScaleFactor = factor;
        ApplyScalingToControls(factor);
    }

    private void ApplyScalingToControls(double factor)
    {
        DemoCard.BodyPadding = ScaleHelper.GetScaledThickness(FloweryScalePreset.SpacingSmall, factor);
        DemoCardContent.Spacing = ScaleHelper.SpacingSmall(factor);
        DemoButtonRow.ItemSpacing = DemoButtonRow.LineSpacing = ScaleHelper.SpacingXS(factor);
        DemoBadgeRow.ItemSpacing = DemoBadgeRow.LineSpacing = ScaleHelper.SpacingXS(factor);
        DemoTitle.FontSize = ScaleHelper.FontSubheading(factor);
        // The size styles set the padding of each size tier.
        var size = ScaleFactorToSize(factor);
        DemoInput.Size = size;
        DemoButton1.Size = size;
        DemoButton2.Size = size;
        DemoButton3.Size = size;
        DemoBadge1.Size = size;
        DemoBadge2.Size = size;
        DemoBadge3.Size = size;
    }

    internal static DaisySize ScaleFactorToSize(double factor) => factor switch
    {
        < 0.65 => DaisySize.ExtraSmall,
        < 0.90 => DaisySize.Small,
        < 1.20 => DaisySize.Medium,
        < 1.60 => DaisySize.Large,
        _ => DaisySize.ExtraLarge
    };

    internal void SetZoomForVerification(double percent)
    {
        if (!_active)
            throw new InvalidOperationException("The scaling slide must be loaded before setting its zoom.");
        if (!double.IsFinite(percent) || percent < 50 || percent > TargetZoomPercent)
            throw new ArgumentOutOfRangeException(nameof(percent));
        StopTimer();
        _currentZoomPercent = percent;
        ApplyZoom();
    }

    private void StopTimer()
    {
        if (_zoomTimer is null)
            return;
        _zoomTimer.Stop();
        _zoomTimer.Tick -= OnZoomTick;
        _zoomTimer = null;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        if (!_active)
            return;
        _active = false;
        StopTimer();
        _zoomFinished.TrySetCanceled();
        FloweryScaleManager.OverrideScaleFactor = _previousOverride;
        FloweryScaleManager.MinScaleFactor = _previousMinimum;
        FloweryScaleManager.MaxScaleFactor = _previousMaximum;
        FloweryScaleManager.IsEnabled = _previousEnabled;
    }
}
