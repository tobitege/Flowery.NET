using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DaisyUI.Avalonia.Controls;

namespace DaisyUI.Avalonia.Gallery.Examples;

public partial class FeedbackExamples : UserControl, IScrollableExample
{
    private DispatcherTimer? _toastTimer;
    private int _toastStep;
    private DaisyToast? _demoToast;

    private static readonly List<(DaisyAlertVariant Variant, string Message)> ToastMessages = new()
    {
        (DaisyAlertVariant.Info, "New message arrived!"),
        (DaisyAlertVariant.Success, "File saved."),
        (DaisyAlertVariant.Warning, "Low disk space."),
        (DaisyAlertVariant.Error, "Connection lost!"),
        (DaisyAlertVariant.Success, "Reconnected."),
        (DaisyAlertVariant.Info, "Update available.")
    };

    public FeedbackExamples()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _demoToast = this.FindControl<DaisyToast>("DemoToast");
        StartToastDemo();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopToastDemo();
    }

    private void StartToastDemo()
    {
        _toastStep = 0;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _toastTimer.Tick += OnToastTimerTick;
        _toastTimer.Start();
        ShowNextToast();
    }

    private void StopToastDemo()
    {
        if (_toastTimer != null)
        {
            _toastTimer.Tick -= OnToastTimerTick;
            _toastTimer.Stop();
            _toastTimer = null;
        }
    }

    private void OnToastTimerTick(object? sender, EventArgs e)
    {
        ShowNextToast();
    }

    private void ShowNextToast()
    {
        if (_demoToast == null) return;

        if (_demoToast.Items.Count >= 3)
        {
            _demoToast.Items.RemoveAt(0);
        }

        var (variant, message) = ToastMessages[_toastStep % ToastMessages.Count];
        var alert = new DaisyAlert { Variant = variant, Content = message };
        _demoToast.Items.Add(alert);

        _toastStep++;
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var sectionHeader = this.GetVisualDescendants()
            .OfType<SectionHeader>()
            .FirstOrDefault(h => h.Title.StartsWith(sectionName, System.StringComparison.OrdinalIgnoreCase));

        if (sectionHeader?.Parent is Visual parent)
        {
            var transform = parent.TransformToVisual(scrollViewer);
            if (transform.HasValue)
            {
                var point = transform.Value.Transform(new Point(0, 0));
                scrollViewer.Offset = new Vector(0, point.Y);
            }
        }
    }
}
