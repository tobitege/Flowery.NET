using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.NET.Gallery.Examples;

public partial class ScalingExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;
    private Window? _hostWindow;
    private ScrollViewer? _mainScrollViewer;
    private Vector _scrollOffsetBeforeDeactivate;
    private bool _hasScrollOffsetBeforeDeactivate;

    /// <summary>
    /// Defines the <see cref="IsScalingEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsScalingEnabledProperty =
        AvaloniaProperty.Register<ScalingExamples, bool>(nameof(IsScalingEnabled), true);

    /// <summary>
    /// Gets or sets whether auto-scaling is enabled for this demo.
    /// </summary>
    public bool IsScalingEnabled
    {
        get => GetValue(IsScalingEnabledProperty);
        set => SetValue(IsScalingEnabledProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="MaxScalePresetIndex"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxScalePresetIndexProperty =
        AvaloniaProperty.Register<ScalingExamples, int>(nameof(MaxScalePresetIndex), 0);

    /// <summary>
    /// Gets or sets the max scale preset for this demo.
    /// 0=100%, 1=125%, 2=150%, 3=200%, 4=300%.
    /// </summary>
    public int MaxScalePresetIndex
    {
        get => GetValue(MaxScalePresetIndexProperty);
        set => SetValue(MaxScalePresetIndexProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="IsManualZoomEnabled"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsManualZoomEnabledProperty =
        AvaloniaProperty.Register<ScalingExamples, bool>(nameof(IsManualZoomEnabled), false);

    /// <summary>
    /// Gets or sets whether manual zoom override is enabled.
    /// </summary>
    public bool IsManualZoomEnabled
    {
        get => GetValue(IsManualZoomEnabledProperty);
        set => SetValue(IsManualZoomEnabledProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ManualZoomPercent"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ManualZoomPercentProperty =
        AvaloniaProperty.Register<ScalingExamples, double>(nameof(ManualZoomPercent), 100.0);

    /// <summary>
    /// Gets or sets the manual zoom percentage (e.g. 150 for 150%).
    /// </summary>
    public double ManualZoomPercent
    {
        get => GetValue(ManualZoomPercentProperty);
        set => SetValue(ManualZoomPercentProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ManualZoomMaxPercent"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ManualZoomMaxPercentProperty =
        AvaloniaProperty.Register<ScalingExamples, double>(nameof(ManualZoomMaxPercent), 300.0);

    /// <summary>
    /// Gets or sets the maximum zoom percentage for the slider (derived from Max Scale).
    /// </summary>
    public double ManualZoomMaxPercent
    {
        get => GetValue(ManualZoomMaxPercentProperty);
        set => SetValue(ManualZoomMaxPercentProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="ResolutionPresetIndex"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ResolutionPresetIndexProperty =
        AvaloniaProperty.Register<ScalingExamples, int>(nameof(ResolutionPresetIndex), -1);

    /// <summary>
    /// Gets or sets the resolution preset index for this demo.
    /// </summary>
    public int ResolutionPresetIndex
    {
        get => GetValue(ResolutionPresetIndexProperty);
        set => SetValue(ResolutionPresetIndexProperty, value);
    }

    /// <summary>
    /// Defines the <see cref="IsDesktopApp"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsDesktopAppProperty =
        AvaloniaProperty.Register<ScalingExamples, bool>(nameof(IsDesktopApp), false);

    /// <summary>
    /// Gets whether this demo is hosted in a desktop Window.
    /// </summary>
    public bool IsDesktopApp
    {
        get => GetValue(IsDesktopAppProperty);
        private set => SetValue(IsDesktopAppProperty, value);
    }

    public ScalingExamples()
    {
        InitializeComponent();

        // Initialize from global state
        IsScalingEnabled = FloweryScaleManager.IsEnabled;
        MaxScalePresetIndex = GetMaxScalePresetIndex(FloweryScaleManager.MaxScaleFactor);

        ManualZoomMaxPercent = FloweryScaleManager.MaxScaleFactor * 100.0;
        if (FloweryScaleManager.OverrideScaleFactor.HasValue)
        {
            IsManualZoomEnabled = true;
            ManualZoomPercent = FloweryScaleManager.OverrideScaleFactor.Value * 100.0;
            if (ManualZoomPercent > ManualZoomMaxPercent)
                ManualZoomPercent = ManualZoomMaxPercent;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var topLevel = TopLevel.GetTopLevel(this);
        IsDesktopApp = topLevel is Window;

        if (topLevel is Window window)
        {
            _mainScrollViewer ??= this.FindControl<ScrollViewer>("MainScrollViewer");

            if (!ReferenceEquals(_hostWindow, window))
            {
                if (_hostWindow != null)
                {
                    _hostWindow.Activated -= OnHostWindowActivated;
                    _hostWindow.Deactivated -= OnHostWindowDeactivated;
                }

                _hostWindow = window;
                _hostWindow.Activated += OnHostWindowActivated;
                _hostWindow.Deactivated += OnHostWindowDeactivated;
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_hostWindow != null)
        {
            _hostWindow.Activated -= OnHostWindowActivated;
            _hostWindow.Deactivated -= OnHostWindowDeactivated;
            _hostWindow = null;
        }
    }

    private void OnHostWindowDeactivated(object? sender, EventArgs e)
    {
        if (_mainScrollViewer == null)
            return;

        _scrollOffsetBeforeDeactivate = _mainScrollViewer.Offset;
        _hasScrollOffsetBeforeDeactivate = true;
    }

    private void OnHostWindowActivated(object? sender, EventArgs e)
    {
        if (!_hasScrollOffsetBeforeDeactivate || _mainScrollViewer == null)
            return;

        var offset = _scrollOffsetBeforeDeactivate;
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainScrollViewer != null)
                _mainScrollViewer.Offset = offset;
        }, DispatcherPriority.Background);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsScalingEnabledProperty)
        {
            // Sync to the global FloweryScaleManager
            FloweryScaleManager.IsEnabled = (bool)change.NewValue!;
        }
        else if (change.Property == MaxScalePresetIndexProperty)
        {
            FloweryScaleManager.MaxScaleFactor = GetMaxScaleFactorForPresetIndex((int)change.NewValue!);
            UpdateManualZoomMaximum();
            if (IsManualZoomEnabled)
            {
                ApplyManualZoomOverride();
            }
        }
        else if (change.Property == IsManualZoomEnabledProperty)
        {
            ApplyManualZoomOverride();
        }
        else if (change.Property == ManualZoomPercentProperty)
        {
            if (IsManualZoomEnabled)
            {
                ApplyManualZoomOverride();
            }
        }
        else if (change.Property == ResolutionPresetIndexProperty)
        {
            ApplyResolutionPreset((int)change.NewValue!);
        }
    }

    private static int GetMaxScalePresetIndex(double maxScaleFactor)
    {
        if (maxScaleFactor >= 3.0) return 4;
        if (maxScaleFactor >= 2.0) return 3;
        if (maxScaleFactor >= 1.5) return 2;
        if (maxScaleFactor >= 1.25) return 1;
        return 0;
    }

    private static double GetMaxScaleFactorForPresetIndex(int presetIndex)
    {
        return presetIndex switch
        {
            1 => 1.25,
            2 => 1.5,
            3 => 2.0,
            4 => 3.0,
            _ => 1.0
        };
    }

    private void UpdateManualZoomMaximum()
    {
        ManualZoomMaxPercent = GetMaxScaleFactorForPresetIndex(MaxScalePresetIndex) * 100.0;
        if (ManualZoomPercent > ManualZoomMaxPercent)
            ManualZoomPercent = ManualZoomMaxPercent;
    }

    private void ApplyManualZoomOverride()
    {
        if (!IsManualZoomEnabled)
        {
            FloweryScaleManager.OverrideScaleFactor = null;
            return;
        }

        var percent = ManualZoomPercent;
        if (percent > ManualZoomMaxPercent)
            percent = ManualZoomMaxPercent;

        FloweryScaleManager.OverrideScaleFactor = percent / 100.0;
    }

    private void ApplyResolutionPreset(int presetIndex)
    {
        if (presetIndex < 0)
            return;

        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null)
            return;

        var (width, height) = presetIndex switch
        {
            0 => (960.0, 540.0),
            1 => (1280.0, 720.0),
            2 => (1366.0, 768.0),
            3 => (1600.0, 900.0),
            4 => (1920.0, 1080.0),
            5 => (2560.0, 1440.0),
            6 => (3440.0, 1440.0),
            7 => (3840.0, 2160.0),
            _ => (0.0, 0.0)
        };

        if (width <= 0 || height <= 0)
            return;

        // Window sizing uses DIPs; presets are in physical pixels. Convert using DesktopScaling (Windows DPI).
        var scaling = window.DesktopScaling;
        if (scaling <= 0)
            scaling = 1.0;

        window.WindowState = WindowState.Normal;
        window.SizeToContent = SizeToContent.Manual;
        window.Width = width / scaling;
        window.Height = height / scaling;
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionName);
        if (target == null) return;

        var transform = target.TransformToVisual(scrollViewer);
        if (transform.HasValue)
        {
            var point = transform.Value.Transform(new Point(0, 0));
            // Add current scroll offset to get absolute position in content
            scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
        }
    }

    private Visual? GetSectionTarget(string sectionId)
    {
        if (_sectionTargetsById == null)
        {
            _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
            {
                if (!string.IsNullOrWhiteSpace(header.SectionId))
                    _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
            }
        }

        return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
    }
}
