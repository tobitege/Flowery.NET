using Flowery.Controls;

namespace Flowery.NET.Kanban.Controls;

/// <summary>
/// Native Avalonia overlay host shared by the Kanban dialogs.
/// </summary>
public class FlowKanbanDialogBase : DaisyModal
{
    public const double AbsoluteMinWidth = 280;
    public const double AbsoluteMinHeight = 400;
    public const double PreferredWidth = 420;
    public const double PreferredHeight = 520;
    public const double MaxDialogWidth = 840;
    public const double MaxDialogHeight = 800;
    public const double DialogMargin = 40;
    public const double OverlayChromePadding = 48;
    private const double VerticalScrollBarContentClearance = 24;

    public static readonly StyledProperty<double> DialogWidthProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, double>(nameof(DialogWidth), PreferredWidth);

    public static readonly StyledProperty<double> DialogHeightProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, double>(nameof(DialogHeight), PreferredHeight);

    public static readonly StyledProperty<bool> IsOutsideClickDismissEnabledProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, bool>(nameof(IsOutsideClickDismissEnabled));

    public static readonly StyledProperty<bool> IsCloseOnEnterEnabledProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, bool>(nameof(IsCloseOnEnterEnabled));

    public static readonly StyledProperty<bool> IsDialogResizingEnabledProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, bool>(nameof(IsDialogResizingEnabled));

    public static readonly StyledProperty<bool> IsDraggableProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, bool>(nameof(IsDraggable));

    public static readonly StyledProperty<bool> IsTabNavigationCycleEnabledProperty =
        AvaloniaProperty.Register<FlowKanbanDialogBase, bool>(nameof(IsTabNavigationCycleEnabled), true);

    private readonly Border _dialogBorder;
    private readonly Grid _dialogHost;
    private readonly Border _resizeGrip;
    private readonly TranslateTransform _dialogTranslation;
    private TopLevel? _topLevel;
    private OverlayLayer? _overlayLayer;
    private Border? _backdrop;
    private IInputElement? _focusedElementBeforeOpen;
    private bool _isTopLevelClosing;
    private bool _isAutoHeight;
    private bool _isResizing;
    private bool _isDragging;
    private Point _resizeStart;
    private Point _dragStart;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private double _dragStartX;
    private double _dragStartY;

    static FlowKanbanDialogBase()
    {
        FlowKanbanLocalizationRegistration.EnsureRegistered();
    }

    public FlowKanbanDialogBase()
    {
        Focusable = true;
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Window);
        ApplyTabNavigationMode();

        _dialogBorder = new Border
        {
            Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush"),
            BorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 24,
                Color = Color.FromArgb(96, 0, 0, 0)
            }),
            Child = this
        };

        _resizeGrip = new Border
        {
            Width = 22,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner),
            IsVisible = false
        };
        _resizeGrip.PointerPressed += OnResizeGripPointerPressed;
        _resizeGrip.PointerMoved += OnResizeGripPointerMoved;
        _resizeGrip.PointerReleased += OnResizeGripPointerReleased;
        _resizeGrip.PointerCaptureLost += OnResizeGripPointerCaptureLost;

        _dialogTranslation = new TranslateTransform();
        _dialogHost = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = _dialogTranslation,
            Children = { _dialogBorder, _resizeGrip }
        };
        _dialogBorder.PointerPressed += OnDialogPointerPressed;
        _dialogBorder.PointerMoved += OnDialogPointerMoved;
        _dialogBorder.PointerReleased += OnDialogPointerReleased;
        _dialogBorder.PointerCaptureLost += OnDialogPointerCaptureLost;
    }

    protected override Type StyleKeyOverride => typeof(DaisyModal);

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new FlowKanbanDialogAutomationPeer(this);

    public double DialogWidth
    {
        get => GetValue(DialogWidthProperty);
        set => SetValue(DialogWidthProperty, value);
    }

    public double DialogHeight
    {
        get => GetValue(DialogHeightProperty);
        set => SetValue(DialogHeightProperty, value);
    }

    public bool IsOutsideClickDismissEnabled
    {
        get => GetValue(IsOutsideClickDismissEnabledProperty);
        set => SetValue(IsOutsideClickDismissEnabledProperty, value);
    }

    public bool IsCloseOnEnterEnabled
    {
        get => GetValue(IsCloseOnEnterEnabledProperty);
        set => SetValue(IsCloseOnEnterEnabledProperty, value);
    }

    public bool IsDialogResizingEnabled
    {
        get => GetValue(IsDialogResizingEnabledProperty);
        set => SetValue(IsDialogResizingEnabledProperty, value);
    }

    public bool IsDraggable
    {
        get => GetValue(IsDraggableProperty);
        set => SetValue(IsDraggableProperty, value);
    }

    public bool IsTabNavigationCycleEnabled
    {
        get => GetValue(IsTabNavigationCycleEnabledProperty);
        set => SetValue(IsTabNavigationCycleEnabledProperty, value);
    }

    protected virtual double DialogBoundsMargin => 16;

    protected Grid DialogHost => _dialogHost;

    public static (double Width, double Height) CalculateOptimalDialogSize(TopLevel? topLevel)
    {
        var size = topLevel?.Bounds.Size ?? default;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return (PreferredWidth, PreferredHeight);
        }

        var availableWidth = Math.Max(0, size.Width - DialogMargin);
        var availableHeight = Math.Max(0, size.Height - DialogMargin);
        var width = availableWidth * (availableWidth < 600 ? 0.9 : 0.6);
        var height = availableHeight * (availableHeight < 700 ? 0.85 : 0.65);

        width = Math.Clamp(width, Math.Min(AbsoluteMinWidth, availableWidth), Math.Min(MaxDialogWidth, availableWidth));
        height = Math.Clamp(height, Math.Min(AbsoluteMinHeight, availableHeight), Math.Min(MaxDialogHeight, availableHeight));

        var aspectRatio = width > 0 ? height / width : 1;
        if (aspectRatio < 1)
        {
            width = Math.Max(Math.Min(AbsoluteMinWidth, availableWidth), height / 1.2);
        }
        else if (aspectRatio > 1.8)
        {
            height = Math.Min(availableHeight, width * 1.5);
        }

        return (Math.Round(width), Math.Round(height));
    }

    public void ApplySmartSizing(TopLevel? topLevel)
    {
        _topLevel = topLevel;
        var (width, height) = CalculateOptimalDialogSize(topLevel);
        DialogWidth = width;
        DialogHeight = height;
        _isAutoHeight = false;
        ApplyDialogSize();
    }

    public void ApplySmartSizingWithAutoHeight(TopLevel? topLevel)
    {
        _topLevel = topLevel;
        var (width, height) = CalculateOptimalDialogSize(topLevel);
        DialogWidth = width;
        DialogHeight = height;
        _isAutoHeight = true;
        ApplyDialogSize();
    }

    protected void ClampDialogWidth(double maxWidth)
    {
        DialogWidth = Math.Min(DialogWidth, maxWidth);
        ApplyDialogSize();
    }

    protected void SetDialogSize(double width, double height)
    {
        DialogWidth = width;
        DialogHeight = height;
        _isAutoHeight = false;
        ApplyDialogSize();
    }

    public static Grid CreateDialogContent(
        TopLevel? topLevel,
        Control? headerContent,
        Control mainContent,
        Control? footerContent)
    {
        var (width, height) = CalculateOptimalDialogSize(topLevel);
        var container = new Grid
        {
            Width = width,
            Height = height,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        if (headerContent is { })
        {
            headerContent.Margin = new Thickness(0, 0, 0, 12);
            Grid.SetRow(headerContent, 0);
            container.Children.Add(headerContent);
        }

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = ReserveVerticalScrollBarSpace(mainContent)
        };
        Grid.SetRow(scrollViewer, 1);
        container.Children.Add(scrollViewer);

        if (footerContent is { })
        {
            footerContent.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetRow(footerContent, 2);
            container.Children.Add(footerContent);
        }

        return container;
    }

    internal static Control ReserveVerticalScrollBarSpace(Control content)
    {
        content.Margin = new Thickness(
            content.Margin.Left,
            content.Margin.Top,
            Math.Max(content.Margin.Right, VerticalScrollBarContentClearance),
            content.Margin.Bottom);
        return content;
    }

    public static StackPanel CreateStandardButtonFooter(
        out DaisyButton saveButton,
        out DaisyButton cancelButton,
        string? saveText = null,
        string? cancelText = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        saveButton = new DaisyButton
        {
            Content = saveText ?? FloweryLocalization.GetString("Common_Save"),
            Variant = DaisyButtonVariant.Success,
            Size = DaisySize.Medium,
            MinWidth = 80,
            Focusable = true
        };
        cancelButton = new DaisyButton
        {
            Content = cancelText ?? FloweryLocalization.GetString("Common_Cancel"),
            Variant = DaisyButtonVariant.Error,
            Size = DaisySize.Medium,
            MinWidth = 80,
            Focusable = true
        };
        panel.Children.Add(saveButton);
        panel.Children.Add(cancelButton);
        return panel;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty && change.NewValue is bool isOpen)
        {
            if (isOpen)
            {
                ShowOverlay();
            }
            else
            {
                HideOverlay();
            }

            OnDialogOpenChanged(isOpen);
        }
        else if (change.Property == IsDialogResizingEnabledProperty)
        {
            _resizeGrip.IsVisible = IsDialogResizingEnabled;
        }
        else if (change.Property == IsTabNavigationCycleEnabledProperty)
        {
            ApplyTabNavigationMode();
        }
        else if (change.Property == DialogWidthProperty || change.Property == DialogHeightProperty)
        {
            ApplyDialogSize();
        }
    }

    private void ApplyTabNavigationMode()
    {
        KeyboardNavigation.SetTabNavigation(
            this,
            IsTabNavigationCycleEnabled
                ? KeyboardNavigationMode.Cycle
                : KeyboardNavigationMode.Continue);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsOpen)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = OnEscapeKeyRequested();
        }
        else if (e.Key == Key.Enter && IsCloseOnEnterEnabled)
        {
            e.Handled = OnEnterKeyRequested();
        }
    }

    protected virtual bool OnEscapeKeyRequested()
    {
        IsOpen = false;
        return true;
    }

    protected virtual bool OnEnterKeyRequested()
    {
        IsOpen = false;
        return true;
    }

    protected virtual void OnDialogOpenChanged(bool isOpen)
    {
    }

    private void ShowOverlay()
    {
        if (_overlayLayer is { } || _topLevel is null)
        {
            return;
        }

        _overlayLayer = OverlayLayer.GetOverlayLayer(_topLevel);
        if (_overlayLayer is null)
        {
            throw new InvalidOperationException("No Avalonia overlay layer is available for the dialog host.");
        }

        _focusedElementBeforeOpen = _topLevel.FocusManager?.GetFocusedElement();
        _topLevel.Closed += OnTopLevelClosed;
        ApplyDialogSize();
        _dialogTranslation.X = 0;
        _dialogTranslation.Y = 0;
        _resizeGrip.IsVisible = IsDialogResizingEnabled;
        _backdrop = new Border
        {
            Width = _overlayLayer.Bounds.Width,
            Height = _overlayLayer.Bounds.Height,
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            Child = _dialogHost
        };
        _backdrop.PointerPressed += OnBackdropPointerPressed;
        _overlayLayer.LayoutUpdated += OnOverlayLayerLayoutUpdated;
        _overlayLayer.Children.Add(_backdrop);
        ApplyTheming();
        Focus();
    }

    private void HideOverlay()
    {
        if (_overlayLayer is { } overlayLayer && _backdrop is { } backdrop)
        {
            overlayLayer.LayoutUpdated -= OnOverlayLayerLayoutUpdated;
            backdrop.PointerPressed -= OnBackdropPointerPressed;
            overlayLayer.Children.Remove(backdrop);
        }

        if (_topLevel is { } topLevel)
        {
            topLevel.Closed -= OnTopLevelClosed;
        }

        _backdrop = null;
        _overlayLayer = null;

        var elementToRestore = _focusedElementBeforeOpen;
        _focusedElementBeforeOpen = null;
        if (!_isTopLevelClosing
            && elementToRestore is Visual visual
            && visual.IsAttachedToVisualTree())
        {
            elementToRestore.Focus(NavigationMethod.Unspecified, KeyModifiers.None);
        }
    }

    private void OnTopLevelClosed(object? sender, EventArgs e)
    {
        _isTopLevelClosing = true;
        try
        {
            if (IsOpen)
            {
                IsOpen = false;
            }
        }
        finally
        {
            _isTopLevelClosing = false;
        }
    }

    private void ApplyDialogSize()
    {
        _dialogHost.Width = DialogWidth;
        _dialogHost.MinWidth = Math.Min(AbsoluteMinWidth, DialogWidth);
        _dialogHost.MaxWidth = MaxDialogWidth;
        _dialogHost.Height = _isAutoHeight ? double.NaN : DialogHeight;
        _dialogHost.MinHeight = _isAutoHeight ? 0 : Math.Min(AbsoluteMinHeight, DialogHeight);
        _dialogHost.MaxHeight = DialogHeight;
    }

    private void ApplyTheming()
    {
        _dialogBorder.Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
        _dialogBorder.BorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
        Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
    }

    private void OnOverlayLayerLayoutUpdated(object? sender, EventArgs e)
    {
        if (_overlayLayer is null || _backdrop is null)
        {
            return;
        }

        _backdrop.Width = _overlayLayer.Bounds.Width;
        _backdrop.Height = _overlayLayer.Bounds.Height;
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOutsideClickDismissEnabled && ReferenceEquals(e.Source, _backdrop))
        {
            IsOpen = false;
            e.Handled = true;
        }
    }

    private void OnDialogPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDraggable || _overlayLayer is null || IsInteractiveSource(e.Source) ||
            !e.GetCurrentPoint(_overlayLayer).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _dragStart = e.GetPosition(_overlayLayer);
        _dragStartX = _dialogTranslation.X;
        _dragStartY = _dialogTranslation.Y;
        e.Pointer.Capture(_dialogBorder);
        e.Handled = true;
    }

    private void OnDialogPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _overlayLayer is null)
        {
            return;
        }

        const double visibleDialogEdge = 48;
        var point = e.GetPosition(_overlayLayer);
        var horizontalLimit = Math.Max(0, (_overlayLayer.Bounds.Width + _dialogHost.Bounds.Width) / 2 - visibleDialogEdge);
        var verticalLimit = Math.Max(0, (_overlayLayer.Bounds.Height + _dialogHost.Bounds.Height) / 2 - visibleDialogEdge);
        _dialogTranslation.X = Math.Clamp(_dragStartX + point.X - _dragStart.X, -horizontalLimit, horizontalLimit);
        _dialogTranslation.Y = Math.Clamp(_dragStartY + point.Y - _dragStart.Y, -verticalLimit, verticalLimit);
        e.Handled = true;
    }

    private void OnDialogPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopDialogDrag(e.Pointer);
    }

    private void OnDialogPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDragging = false;
    }

    private bool IsInteractiveSource(object? source)
    {
        for (var current = source as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Button or TextBox or SelectingItemsControl or ToggleButton or Slider or ScrollBar or DatePicker or DaisyPasswordBox)
            {
                return true;
            }

            if (ReferenceEquals(current, _dialogBorder))
            {
                break;
            }
        }

        return false;
    }

    private void StopDialogDrag(IPointer pointer)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        pointer.Capture(null);
    }

    private void OnResizeGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDialogResizingEnabled || _overlayLayer is null ||
            !e.GetCurrentPoint(_overlayLayer).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isResizing = true;
        _resizeStart = e.GetPosition(_overlayLayer);
        _resizeStartWidth = _dialogHost.Bounds.Width;
        _resizeStartHeight = _dialogHost.Bounds.Height;
        e.Pointer.Capture(_resizeGrip);
        e.Handled = true;
    }

    private void OnResizeGripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing || _overlayLayer is null)
        {
            return;
        }

        var point = e.GetPosition(_overlayLayer);
        var availableWidth = Math.Max(AbsoluteMinWidth, _overlayLayer.Bounds.Width - DialogBoundsMargin * 2);
        var availableHeight = Math.Max(AbsoluteMinHeight, _overlayLayer.Bounds.Height - DialogBoundsMargin * 2);
        DialogWidth = Math.Clamp(_resizeStartWidth + point.X - _resizeStart.X, AbsoluteMinWidth, availableWidth);
        DialogHeight = Math.Clamp(_resizeStartHeight + point.Y - _resizeStart.Y, AbsoluteMinHeight, availableHeight);
        _isAutoHeight = false;
        ApplyDialogSize();
        e.Handled = true;
    }

    private void OnResizeGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        StopResize(e.Pointer);
    }

    private void OnResizeGripPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isResizing = false;
    }

    private void StopResize(IPointer pointer)
    {
        if (!_isResizing)
        {
            return;
        }

        _isResizing = false;
        pointer.Capture(null);
    }
}

internal sealed class FlowKanbanDialogAutomationPeer : ControlAutomationPeer
{
    public FlowKanbanDialogAutomationPeer(FlowKanbanDialogBase owner)
        : base(owner)
    {
    }

    private new FlowKanbanDialogBase Owner => (FlowKanbanDialogBase)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Window;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override bool IsContentElementCore() =>
        FlowKanbanVisualTree.IsAutomationVisible(Owner);

    protected override bool IsControlElementCore() =>
        FlowKanbanVisualTree.IsAutomationVisible(Owner);
}
