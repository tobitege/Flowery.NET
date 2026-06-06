using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Services;

namespace Flowery.NET.Gallery.Examples;

// This section is intentionally implemented in C# instead of inline AXAML because it doubles as
// a small tutorial for embedding Flowery controls in a larger gallery page without paying the full
// construction cost up front.
//
// Pattern shown here:
// - Keep the section in its normal parent page so category navigation remains continuous.
// - Render a cheap placeholder immediately so layout and scrolling stay responsive.
// - Find the containing ScrollViewer when loaded and watch for the section entering the viewport.
// - Build real DaisyProgress controls only when the user is close to seeing them.
// - Add rows one dispatcher tick at a time so animations and input are not blocked by control creation.
public sealed class ProgressSection : UserControl
{
    private const double LazyLoadMargin = 240;

    private readonly StackPanel _contentPanel;
    private readonly Queue<Func<Control>> _rowFactories = new();
    private Border? _contentBorder;
    private bool _hasStartedLoading;
    private bool _isCheckingViewport;
    private bool _isLoaded;
    private bool _isLoadingRows;
    private WrapPanel? _rowPanel;
    private ScrollViewer? _scrollViewer;

    public ProgressSection()
    {
        _contentPanel = new StackPanel { Spacing = 12 };
        _contentPanel.Children.Add(new SectionHeader { SectionId = "progress", Title = "Progress (Linear)" });
        _contentPanel.Children.Add(CreatePlaceholder());

        _rowFactories.Enqueue(CreateVariantsAndSizesRow);
        _rowFactories.Enqueue(CreateInteractiveRow);

        Content = _contentPanel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _scrollViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged += OnScrollChanged;

        QueueViewportCheck(DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged -= OnScrollChanged;

        _scrollViewer = null;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        QueueViewportCheck(DispatcherPriority.Background);
    }

    private void QueueViewportCheck(DispatcherPriority priority)
    {
        if (!_isLoaded || _isCheckingViewport || _hasStartedLoading)
            return;

        _isCheckingViewport = true;
        Dispatcher.UIThread.Post(LoadIfNearViewport, priority);
    }

    private void LoadIfNearViewport()
    {
        _isCheckingViewport = false;

        if (!_isLoaded || _hasStartedLoading || !IsNearViewport())
            return;

        BeginLoading();
        QueueRowLoad();
    }

    private bool IsNearViewport()
    {
        if (_scrollViewer == null || _scrollViewer.Bounds.Height <= 0)
            return false;

        var transform = this.TransformToVisual(_scrollViewer);
        if (!transform.HasValue)
            return false;

        var point = transform.Value.Transform(new Point(0, 0));
        return point.Y < _scrollViewer.Bounds.Height + LazyLoadMargin &&
               point.Y + Bounds.Height > -LazyLoadMargin;
    }

    private void BeginLoading()
    {
        _hasStartedLoading = true;
        _rowPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        FlowerySizeManager.SetIgnoreGlobalSize(_rowPanel, true);

        if (_contentBorder != null)
            _contentBorder.Child = CreateContentPanel(_rowPanel);
    }

    private void QueueRowLoad()
    {
        if (!_isLoaded || _isLoadingRows || _rowFactories.Count == 0)
            return;

        _isLoadingRows = true;
        Dispatcher.UIThread.Post(LoadNextRow, DispatcherPriority.Background);
    }

    private void LoadNextRow()
    {
        _isLoadingRows = false;

        if (!_isLoaded || _rowPanel == null || _rowFactories.Count == 0)
            return;

        var factory = _rowFactories.Dequeue();
        _rowPanel.Children.Add(factory());
        QueueRowLoad();
    }

    private Border CreatePlaceholder()
    {
        _contentBorder = CreateSectionBorder(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Core Progress",
                    FontWeight = FontWeight.Bold,
                    FontSize = 16,
                    Opacity = 0.95
                },
                new TextBlock
                {
                    Text = "Common linear progress examples and an interactive value demo",
                    Opacity = 0.6
                },
                new TextBlock
                {
                    Text = "Scroll to load these progress examples",
                    Opacity = 0.5
                }
            }
        });

        return _contentBorder;
    }

    private static StackPanel CreateContentPanel(Control content)
    {
        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Core Progress",
                    FontWeight = FontWeight.Bold,
                    FontSize = 16,
                    Opacity = 0.95
                },
                new TextBlock
                {
                    Text = "Common linear progress examples and an interactive value demo",
                    Opacity = 0.6,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                content
            }
        };
    }

    private static Border CreateSectionBorder(Control child)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 8, 0, 0),
            MinHeight = 150,
            Child = child
        };

        if (Application.Current?.TryFindResource("DaisyBase200Brush", out var resource) == true && resource is IBrush brush)
            border.Background = brush;

        return border;
    }

    private static StackPanel CreateVariantsAndSizesRow()
    {
        return new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 40, 20),
            Width = 200,
            Children =
            {
                new TextBlock
                {
                    Text = "Variants and sizes",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    Opacity = 0.8
                },
                new DaisyProgress { Value = 40 },
                new DaisyProgress { Value = 70, Variant = DaisyProgressVariant.Primary },
                new DaisyProgress { Value = 100, Variant = DaisyProgressVariant.Accent, Size = DaisySize.Small },
                new DaisyProgress { IsIndeterminate = true, Variant = DaisyProgressVariant.Secondary }
            }
        };
    }

    private static StackPanel CreateInteractiveRow()
    {
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 40
        };

        var valueText = new TextBlock { Opacity = 0.7 };
        valueText.Bind(TextBlock.TextProperty, new Binding("Value")
        {
            Source = slider,
            StringFormat = "{0:0}%"
        });
        FlowerySizeManager.SetResponsiveFont(valueText, ResponsiveFontTier.Secondary);

        var progress = new DaisyProgress();
        progress.Bind(RangeBase.ValueProperty, new Binding("Value") { Source = slider });

        var primaryProgress = new DaisyProgress { Variant = DaisyProgressVariant.Primary };
        primaryProgress.Bind(RangeBase.ValueProperty, new Binding("Value") { Source = slider });

        return new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 40, 20),
            Width = 260,
            Children =
            {
                new TextBlock
                {
                    Text = "Animated value updates",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    Opacity = 0.8
                },
                slider,
                valueText,
                progress,
                primaryProgress
            }
        };
    }
}
