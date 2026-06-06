using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Services;

namespace Flowery.NET.Gallery.Examples;

public sealed class LoadingExamples : UserControl, IScrollableExample
{
    private const double LazyLoadMargin = 240;

    private readonly ScrollViewer _scrollViewer;
    private readonly StackPanel _contentPanel;
    private readonly List<LazyLoadingSection> _lazySections = new();
    private readonly Queue<LazyLoadingSection> _rowLoadQueue = new();
    private bool _isLoaded;
    private bool _isCheckingLazySections;
    private bool _isLoadingRows;

    public LoadingExamples()
    {
        _contentPanel = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(24, 0, 24, 32)
        };

        _contentPanel.Children.Add(new SectionHeader { SectionId = "loading", Title = "Loading" });
        _contentPanel.Children.Add(CreateClassicSection());
        _contentPanel.Children.Add(CreateTerminalSection());

        AddLazySection(
            "Matrix Colon Patterns",
            "Colon-dot patterns with directional wave animations",
            CreateMatrixRows());
        AddLazySection(
            "Advanced Digital Variants",
            "Dense digital progress indicators for technical workflows",
            CreateDigitalRows());
        AddLazySection(
            "Instrumentation Variants",
            "Monitoring, terminal, and signal-style loaders",
            CreateInstrumentationRows());
        AddLazySection(
            "Business / Workflow Variants",
            "Document, cloud, approval, battery, and traffic-light progress indicators",
            CreateBusinessRows());
        AddLazySection(
            "Win95 Retro Variants",
            "Nostalgic Windows 95 style file operation animations",
            CreateWin95Rows());

        _scrollViewer = new ScrollViewer
        {
            Name = "MainScrollViewer",
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _contentPanel
        };

        Content = _scrollViewer;

        FloweryResponsive.SetIsEnabled(_scrollViewer, true);
        FloweryResponsive.SetBaseMaxWidth(_scrollViewer, 430);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    public void ScrollToSection(string sectionName)
    {
        _scrollViewer.Offset = new Vector(0, 0);
        QueueLazySectionCheck(DispatcherPriority.Loaded);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        QueueLazySectionCheck(DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = false;
    }

    private void AddLazySection(string title, string description, LoadingRow[] rows)
    {
        var section = new LazyLoadingSection(title, description, rows);
        _lazySections.Add(section);
        _contentPanel.Children.Add(section);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        QueueLazySectionCheck(DispatcherPriority.Background);
    }

    private void QueueLazySectionCheck(DispatcherPriority priority)
    {
        if (!_isLoaded || _isCheckingLazySections)
            return;

        _isCheckingLazySections = true;
        Dispatcher.UIThread.Post(LoadNextVisibleLazySection, priority);
    }

    private void LoadNextVisibleLazySection()
    {
        _isCheckingLazySections = false;

        if (!_isLoaded)
            return;

        foreach (var section in _lazySections)
        {
            if (section.HasLoadedContent || !IsNearViewport(section))
                continue;

            section.BeginLoading();
            _rowLoadQueue.Enqueue(section);
            QueueRowLoad();
            QueueLazySectionCheck(DispatcherPriority.Background);
            return;
        }
    }

    private bool IsNearViewport(Control control)
    {
        if (_scrollViewer.Bounds.Height <= 0)
            return false;

        var transform = control.TransformToVisual(_scrollViewer);
        if (!transform.HasValue)
            return false;

        var point = transform.Value.Transform(new Point(0, 0));
        return point.Y < _scrollViewer.Bounds.Height + LazyLoadMargin &&
               point.Y + control.Bounds.Height > -LazyLoadMargin;
    }

    private void QueueRowLoad()
    {
        if (!_isLoaded || _isLoadingRows || _rowLoadQueue.Count == 0)
            return;

        _isLoadingRows = true;
        Dispatcher.UIThread.Post(LoadNextLazyRow, DispatcherPriority.Background);
    }

    private void LoadNextLazyRow()
    {
        _isLoadingRows = false;

        if (!_isLoaded)
            return;

        while (_rowLoadQueue.Count > 0)
        {
            var section = _rowLoadQueue.Peek();
            if (section.AddNextRow())
            {
                QueueRowLoad();
                return;
            }

            _rowLoadQueue.Dequeue();
        }
    }

    private static Control CreateClassicSection()
    {
        return CreateSection(
            "Classic DaisyUI Variants",
            "Original loading animations from DaisyUI component library",
            new LoadingRow("Spinner", DaisyLoadingVariant.Spinner, AllSizes),
            new LoadingRow("Dots", DaisyLoadingVariant.Dots, AllSizes),
            new LoadingRow("Ring", DaisyLoadingVariant.Ring, AllSizes),
            new LoadingRow("Ball", DaisyLoadingVariant.Ball, AllSizes),
            new LoadingRow("Bars", DaisyLoadingVariant.Bars, AllSizes),
            new LoadingRow("Infinity", DaisyLoadingVariant.Infinity, AllSizes),
            new LoadingRow("Color Variants", DaisyLoadingVariant.Spinner, MediumSize, AllColors));
    }

    private static Control CreateTerminalSection()
    {
        return CreateSection(
            "Terminal-Style Variants",
            "CLI-inspired animations reminiscent of npm, yarn, and terminal progress indicators",
            new LoadingRow("Orbit (npm-style)", DaisyLoadingVariant.Orbit, AllSizes),
            new LoadingRow("Snake (centipede)", DaisyLoadingVariant.Snake, AllSizes),
            new LoadingRow("Pulse (breathing)", DaisyLoadingVariant.Pulse, AllSizes),
            new LoadingRow("Wave", DaisyLoadingVariant.Wave, AllSizes),
            new LoadingRow("Bounce (grid)", DaisyLoadingVariant.Bounce, AllSizes),
            new LoadingRow("Terminal Colors", TerminalColorVariants));
    }

    private static LoadingRow[] CreateMatrixRows()
    {
        return
        [
            new LoadingRow("Matrix (left to right)", DaisyLoadingVariant.Matrix, MediumLargeExtraLarge),
            new LoadingRow("MatrixInward (center to edges)", DaisyLoadingVariant.MatrixInward, MediumLargeExtraLarge),
            new LoadingRow("MatrixOutward (edges to center)", DaisyLoadingVariant.MatrixOutward, MediumLargeExtraLarge),
            new LoadingRow("MatrixVertical (top to bottom)", DaisyLoadingVariant.MatrixVertical, MediumLargeExtraLarge)
        ];
    }

    private static LoadingRow[] CreateDigitalRows()
    {
        return
        [
            new LoadingRow("MatrixRain", DaisyLoadingVariant.MatrixRain, MediumLargeExtraLarge),
            new LoadingRow("BitFlip", DaisyLoadingVariant.BitFlip, MediumLargeExtraLarge),
            new LoadingRow("PacketBurst", DaisyLoadingVariant.PacketBurst, MediumLargeExtraLarge),
            new LoadingRow("CometTrail", DaisyLoadingVariant.CometTrail, MediumLargeExtraLarge),
            new LoadingRow("RippleMatrix", DaisyLoadingVariant.RippleMatrix, MediumLargeExtraLarge),
            new LoadingRow("CountdownSpinner", DaisyLoadingVariant.CountdownSpinner, MediumLargeExtraLarge)
        ];
    }

    private static LoadingRow[] CreateInstrumentationRows()
    {
        return
        [
            new LoadingRow("Hourglass", DaisyLoadingVariant.Hourglass, MediumLargeExtraLarge),
            new LoadingRow("SignalSweep", DaisyLoadingVariant.SignalSweep, MediumLargeExtraLarge),
            new LoadingRow("Heartbeat", DaisyLoadingVariant.Heartbeat, MediumLargeExtraLarge),
            new LoadingRow("TunnelZoom", DaisyLoadingVariant.TunnelZoom, MediumLargeExtraLarge),
            new LoadingRow("GlitchReveal", DaisyLoadingVariant.GlitchReveal, MediumLargeExtraLarge),
            new LoadingRow("CursorBlink", DaisyLoadingVariant.CursorBlink, MediumLargeExtraLarge)
        ];
    }

    private static LoadingRow[] CreateBusinessRows()
    {
        return
        [
            new LoadingRow("DocumentFlipOn (opening)", DaisyLoadingVariant.DocumentFlipOn, MediumLargeExtraLarge),
            new LoadingRow("DocumentFlipOff (closing)", DaisyLoadingVariant.DocumentFlipOff, MediumLargeExtraLarge),
            new LoadingRow("MailSend", DaisyLoadingVariant.MailSend, MediumLargeExtraLarge),
            new LoadingRow("CloudUpload", DaisyLoadingVariant.CloudUpload, MediumLargeExtraLarge),
            new LoadingRow("CloudDownload", DaisyLoadingVariant.CloudDownload, MediumLargeExtraLarge),
            new LoadingRow("DocumentStamp (approval OK)", DaisyLoadingVariant.DocumentStamp, MediumLargeExtraLarge),
            new LoadingRow("DocumentReject (rejection X)", DaisyLoadingVariant.DocumentReject, MediumLargeExtraLarge),
            new LoadingRow("ChartPulse (analytics)", DaisyLoadingVariant.ChartPulse, MediumLargeExtraLarge),
            new LoadingRow("CalendarTick (scheduling)", DaisyLoadingVariant.CalendarTick, MediumLargeExtraLarge),
            new LoadingRow("ApprovalFlow (workflow)", DaisyLoadingVariant.ApprovalFlow, MediumLargeExtraLarge),
            new LoadingRow("BriefcaseSpin (business)", DaisyLoadingVariant.BriefcaseSpin, MediumLargeExtraLarge),
            new LoadingRow("Battery (charge/drain)", BatteryVariants),
            new LoadingRow("TrafficLight (directional)", TrafficLightVariants)
        ];
    }

    private static LoadingRow[] CreateWin95Rows()
    {
        return
        [
            new LoadingRow("Win95FileCopy (flying papers)", DaisyLoadingVariant.Win95FileCopy, MediumLargeExtraLarge),
            new LoadingRow("Win95Search (magnifying glass)", DaisyLoadingVariant.Win95Search, MediumLargeExtraLarge),
            new LoadingRow("Win95Delete (to recycle bin)", DaisyLoadingVariant.Win95Delete, MediumLargeExtraLarge),
            new LoadingRow("Win95EmptyRecycle (emptying bin)", DaisyLoadingVariant.Win95EmptyRecycle, MediumLargeExtraLarge)
        ];
    }

    private static Border CreateSection(string title, string description, params LoadingRow[] rows)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Bold,
            FontSize = 16,
            Opacity = 0.95
        });
        panel.Children.Add(new TextBlock
        {
            Text = description,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var rowPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        FlowerySizeManager.SetIgnoreGlobalSize(rowPanel, true);

        foreach (var row in rows)
            rowPanel.Children.Add(CreateRow(row));

        panel.Children.Add(rowPanel);

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 8, 0, 0),
            Child = panel
        };

        if (Application.Current?.TryFindResource("DaisyBase200Brush", out var resource) == true && resource is IBrush brush)
            border.Background = brush;

        return border;
    }

    private static StackPanel CreateRow(LoadingRow row)
    {
        var loadingPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center
        };

        foreach (var item in row.Items)
        {
            loadingPanel.Children.Add(new DaisyLoading
            {
                Variant = item.Variant,
                Size = item.Size,
                Color = item.Color
            });
        }

        return new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 40, 12),
            Children =
            {
                new TextBlock
                {
                    Text = row.Title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    Opacity = 0.8
                },
                loadingPanel
            }
        };
    }

    private static LoadingItem[] CreateItems(DaisyLoadingVariant variant, DaisySize[] sizes)
    {
        var items = new LoadingItem[sizes.Length];
        for (var index = 0; index < sizes.Length; index++)
            items[index] = new LoadingItem(variant, sizes[index], DaisyColor.Default);

        return items;
    }

    private static LoadingItem[] CreateItems(DaisyLoadingVariant variant, DaisySize[] sizes, DaisyColor[] colors)
    {
        var items = new LoadingItem[colors.Length];
        for (var index = 0; index < colors.Length; index++)
            items[index] = new LoadingItem(variant, sizes[Math.Min(index, sizes.Length - 1)], colors[index]);

        return items;
    }

    private static readonly DaisySize[] AllSizes =
    [
        DaisySize.ExtraSmall,
        DaisySize.Small,
        DaisySize.Medium,
        DaisySize.Large,
        DaisySize.ExtraLarge
    ];

    private static readonly DaisySize[] MediumSize = [DaisySize.Medium];

    private static readonly DaisySize[] MediumLargeExtraLarge =
    [
        DaisySize.Medium,
        DaisySize.Large,
        DaisySize.ExtraLarge
    ];

    private static readonly DaisyColor[] AllColors =
    [
        DaisyColor.Primary,
        DaisyColor.Secondary,
        DaisyColor.Accent,
        DaisyColor.Info,
        DaisyColor.Success,
        DaisyColor.Warning,
        DaisyColor.Error
    ];

    private static readonly LoadingItem[] TerminalColorVariants =
    [
        new(DaisyLoadingVariant.Orbit, DaisySize.Large, DaisyColor.Primary),
        new(DaisyLoadingVariant.Snake, DaisySize.Large, DaisyColor.Secondary),
        new(DaisyLoadingVariant.Pulse, DaisySize.Large, DaisyColor.Accent),
        new(DaisyLoadingVariant.Wave, DaisySize.Large, DaisyColor.Info),
        new(DaisyLoadingVariant.Bounce, DaisySize.Large, DaisyColor.Success)
    ];

    private static readonly LoadingItem[] BatteryVariants =
    [
        new(DaisyLoadingVariant.BatteryCharging, DaisySize.Medium, DaisyColor.Success),
        new(DaisyLoadingVariant.BatteryEmptying, DaisySize.Large, DaisyColor.Warning),
        new(DaisyLoadingVariant.BatteryCharging, DaisySize.ExtraLarge, DaisyColor.Primary)
    ];

    private static readonly LoadingItem[] TrafficLightVariants =
    [
        new(DaisyLoadingVariant.TrafficLightUp, DaisySize.Medium, DaisyColor.Default),
        new(DaisyLoadingVariant.TrafficLightRight, DaisySize.Large, DaisyColor.Default),
        new(DaisyLoadingVariant.TrafficLightDown, DaisySize.Large, DaisyColor.Default),
        new(DaisyLoadingVariant.TrafficLightLeft, DaisySize.ExtraLarge, DaisyColor.Default)
    ];

    private sealed record LoadingRow
    {
        public LoadingRow(string title, DaisyLoadingVariant variant, DaisySize[] sizes)
            : this(title, CreateItems(variant, sizes))
        {
        }

        public LoadingRow(string title, DaisyLoadingVariant variant, DaisySize[] sizes, DaisyColor[] colors)
            : this(title, CreateItems(variant, sizes, colors))
        {
        }

        public LoadingRow(string title, LoadingItem[] items)
        {
            Title = title;
            Items = items;
        }

        public string Title { get; }
        public LoadingItem[] Items { get; }
    }

    private readonly record struct LoadingItem(DaisyLoadingVariant Variant, DaisySize Size, DaisyColor Color);

    private sealed class LazyLoadingSection : Border
    {
        private readonly string _description;
        private readonly LoadingRow[] _rows;
        private readonly string _title;
        private int _loadedRowCount;
        private WrapPanel? _rowPanel;

        public LazyLoadingSection(string title, string description, LoadingRow[] rows)
        {
            _description = description;
            _rows = rows;
            _title = title;

            MinHeight = 190;
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(16);
            Margin = new Thickness(0, 8, 0, 0);
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.Bold,
                        FontSize = 16,
                        Opacity = 0.95
                    },
                    new TextBlock
                    {
                        Text = description,
                        Opacity = 0.6
                    },
                    new TextBlock
                    {
                        Text = "Scroll to load these animations",
                        Opacity = 0.5
                    }
                }
            };

            if (Application.Current?.TryFindResource("DaisyBase200Brush", out var resource) == true && resource is IBrush brush)
                Background = brush;
        }

        public bool HasLoadedContent { get; set; }

        public void BeginLoading()
        {
            if (HasLoadedContent)
                return;

            HasLoadedContent = true;
            _rowPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            FlowerySizeManager.SetIgnoreGlobalSize(_rowPanel, true);

            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = _title,
                        FontWeight = FontWeight.Bold,
                        FontSize = 16,
                        Opacity = 0.95
                    },
                    new TextBlock
                    {
                        Text = _description,
                        Opacity = 0.6,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    _rowPanel
                }
            };
        }

        public bool AddNextRow()
        {
            if (_rowPanel == null || _loadedRowCount >= _rows.Length)
                return false;

            _rowPanel.Children.Add(CreateRow(_rows[_loadedRowCount]));
            _loadedRowCount++;
            return _loadedRowCount < _rows.Length;
        }
    }
}
