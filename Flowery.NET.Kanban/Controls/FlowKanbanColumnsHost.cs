namespace Flowery.NET.Kanban.Controls;

public enum FlowKanbanGripVisibility
{
    Auto,
    Visible,
    Hidden
}

/// <summary>
/// Horizontal Kanban column host with shared-width pointer and keyboard resizing.
/// </summary>
public sealed class FlowKanbanColumnsHost : ItemsControl
{
    private const double GapHitRadius = 16;
    internal const double KeyboardStep = 8;
    internal const double LargeKeyboardStep = 32;

    public static readonly StyledProperty<double> ColumnWidthProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, double>(nameof(ColumnWidth), 250);

    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, double>(nameof(MinColumnWidth), 100);

    public static readonly StyledProperty<double> MaxColumnWidthProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, double>(nameof(MaxColumnWidth), 1000);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, double>(nameof(ColumnSpacing));

    public static readonly StyledProperty<bool> IsGripEnabledProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, bool>(nameof(IsGripEnabled), true);

    public static readonly StyledProperty<FlowKanbanGripVisibility> GripVisibilityProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, FlowKanbanGripVisibility>(
            nameof(GripVisibility),
            FlowKanbanGripVisibility.Auto);

    public static readonly StyledProperty<string?> AccessibleTextProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, string?>(nameof(AccessibleText));

    public static readonly StyledProperty<string?> AccessibleHelpTextProperty =
        AvaloniaProperty.Register<FlowKanbanColumnsHost, string?>(nameof(AccessibleHelpText));

    private int _activeGapIndex = -1;
    private bool _isDragging;
    private Point _dragStart;
    private double _dragStartWidth;
    private StackPanel? _itemsPanel;

    public FlowKanbanColumnsHost()
    {
        ItemsPanel = new FuncTemplate<Panel?>(CreateItemsPanel);
        Focusable = true;
    }

    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new FlowKanbanColumnsHostAutomationPeer(this);

    public event EventHandler<double>? ColumnWidthChanged;

    public event EventHandler? ResizeDragStarted;

    public event EventHandler? ResizeDragCompleted;

    public double ColumnWidth
    {
        get => GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, Math.Clamp(value, MinColumnWidth, MaxColumnWidth));
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public double MaxColumnWidth
    {
        get => GetValue(MaxColumnWidthProperty);
        set => SetValue(MaxColumnWidthProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public bool IsGripEnabled
    {
        get => GetValue(IsGripEnabledProperty);
        set => SetValue(IsGripEnabledProperty, value);
    }

    public FlowKanbanGripVisibility GripVisibility
    {
        get => GetValue(GripVisibilityProperty);
        set => SetValue(GripVisibilityProperty, value);
    }

    public string? AccessibleText
    {
        get => GetValue(AccessibleTextProperty);
        set => SetValue(AccessibleTextProperty, value);
    }

    public string? AccessibleHelpText
    {
        get => GetValue(AccessibleHelpTextProperty);
        set => SetValue(AccessibleHelpTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColumnWidthProperty)
        {
            ColumnWidthChanged?.Invoke(this, ColumnWidth);
        }
        else if (change.Property == MinColumnWidthProperty || change.Property == MaxColumnWidthProperty)
        {
            var clamped = Math.Clamp(ColumnWidth, MinColumnWidth, MaxColumnWidth);
            if (!ColumnWidth.Equals(clamped))
            {
                SetCurrentValue(ColumnWidthProperty, clamped);
            }
        }
        else if (change.Property == ColumnSpacingProperty)
        {
            ApplyPanelSpacing();
        }
        else if (change.Property == AccessibleTextProperty)
        {
            AutomationProperties.SetName(this, AccessibleText ?? "Resizable Kanban columns");
        }
        else if (change.Property == AccessibleHelpTextProperty)
        {
            AutomationProperties.SetHelpText(this, AccessibleHelpText);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!CanResize())
        {
            Cursor = null;
            return;
        }

        var point = e.GetPosition(this);
        if (_isDragging)
        {
            var divisor = Math.Max(1, _activeGapIndex + 1);
            ColumnWidth = _dragStartWidth + (point.X - _dragStart.X) / divisor;
            e.Handled = true;
            return;
        }

        _activeGapIndex = FindGapIndex(point);
        Cursor = _activeGapIndex >= 0
            ? new Cursor(StandardCursorType.SizeWestEast)
            : null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_activeGapIndex < 0 || !CanResize() ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _dragStart = e.GetPosition(this);
        _dragStartWidth = ColumnWidth;
        e.Pointer.Capture(this);
        ResizeDragStarted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        CompleteDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_isDragging)
        {
            _isDragging = false;
            ResizeDragCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_isDragging)
        {
            _activeGapIndex = -1;
            Cursor = null;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !CanResize())
        {
            return;
        }

        var handled = e.Key switch
        {
            Key.Left => AdjustColumnWidth(-KeyboardStep),
            Key.Right => AdjustColumnWidth(KeyboardStep),
            Key.Home => SetColumnWidth(MinColumnWidth),
            Key.End => SetColumnWidth(MaxColumnWidth),
            Key.PageDown => AdjustColumnWidth(-LargeKeyboardStep),
            Key.PageUp => AdjustColumnWidth(LargeKeyboardStep),
            _ => false
        };
        e.Handled = handled;
    }

    private Panel? CreateItemsPanel()
    {
        _itemsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ColumnSpacing
        };

        return _itemsPanel;
    }

    private void ApplyPanelSpacing()
    {
        if (_itemsPanel is { } && !_itemsPanel.Spacing.Equals(ColumnSpacing))
        {
            _itemsPanel.SetCurrentValue(StackPanel.SpacingProperty, ColumnSpacing);
        }
    }

    private int FindGapIndex(Point point)
    {
        var containers = GetRealizedContainers().ToList();
        for (var index = 0; index < containers.Count - 1; index++)
        {
            var container = containers[index];
            var transform = container.TransformToVisual(this);
            if (transform is null)
            {
                continue;
            }

            var position = transform.Value.Transform(default);
            var gap = position.X + container.Bounds.Width + ColumnSpacing / 2;
            if (Math.Abs(point.X - gap) <= GapHitRadius)
            {
                return index;
            }
        }

        return -1;
    }

    private bool CanResize()
    {
        return IsEnabled && IsGripEnabled && GripVisibility != FlowKanbanGripVisibility.Hidden;
    }

    private bool AdjustColumnWidth(double delta)
    {
        return SetColumnWidth(ColumnWidth + delta);
    }

    private bool SetColumnWidth(double value)
    {
        var clamped = Math.Clamp(value, MinColumnWidth, MaxColumnWidth);
        if (ColumnWidth.Equals(clamped))
        {
            return false;
        }

        ColumnWidth = clamped;
        return true;
    }

    private void CompleteDrag(IPointer pointer)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        pointer.Capture(null);
        ResizeDragCompleted?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FlowKanbanColumnsHostAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    private bool _wasReadOnly;

    public FlowKanbanColumnsHostAutomationPeer(FlowKanbanColumnsHost owner)
        : base(owner)
    {
        _wasReadOnly = IsReadOnly;
        owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    private new FlowKanbanColumnsHost Owner => (FlowKanbanColumnsHost)base.Owner;

    public bool IsReadOnly =>
        !Owner.IsEnabled ||
        !Owner.IsGripEnabled ||
        Owner.GripVisibility == FlowKanbanGripVisibility.Hidden;

    public double Minimum => Owner.MinColumnWidth;

    public double Maximum => Owner.MaxColumnWidth;

    public double Value => Owner.ColumnWidth;

    public double LargeChange => FlowKanbanColumnsHost.LargeKeyboardStep;

    public double SmallChange => FlowKanbanColumnsHost.KeyboardStep;

    public void SetValue(double value)
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("The Kanban column width is read-only.");
        }

        if (!double.IsFinite(value) || value < Minimum || value > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Owner.SetCurrentValue(FlowKanbanColumnsHost.ColumnWidthProperty, value);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Slider;

    protected override string GetClassNameCore() => nameof(FlowKanbanColumnsHost);

    protected override bool IsContentElementCore() =>
        FlowKanbanVisualTree.IsAutomationVisible(Owner);

    protected override bool IsControlElementCore() =>
        FlowKanbanVisualTree.IsAutomationVisible(Owner);

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == FlowKanbanColumnsHost.ColumnWidthProperty)
        {
            RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                change.OldValue,
                change.NewValue);
        }
        else if (change.Property == FlowKanbanColumnsHost.MinColumnWidthProperty)
        {
            RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.MinimumProperty,
                change.OldValue,
                change.NewValue);
        }
        else if (change.Property == FlowKanbanColumnsHost.MaxColumnWidthProperty)
        {
            RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.MaximumProperty,
                change.OldValue,
                change.NewValue);
        }

        if (change.Property == InputElement.IsEnabledProperty ||
            change.Property == FlowKanbanColumnsHost.IsGripEnabledProperty ||
            change.Property == FlowKanbanColumnsHost.GripVisibilityProperty)
        {
            var isReadOnly = IsReadOnly;
            if (_wasReadOnly != isReadOnly)
            {
                RaisePropertyChangedEvent(
                    RangeValuePatternIdentifiers.IsReadOnlyProperty,
                    _wasReadOnly,
                    isReadOnly);
                _wasReadOnly = isReadOnly;
            }
        }
    }
}
