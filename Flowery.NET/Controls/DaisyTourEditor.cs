using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls.ColorPicker;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Localization;
using Flowery.Theming;
using HslColor = Flowery.Controls.ColorPicker.HslColor;

namespace Flowery.Controls;

/// <summary>Edits portable style overrides while the application's tour renders its live preview.</summary>
public sealed partial class DaisyTourEditor : ContentControl
{
    // The layout is built in code; the editor uses the plain ContentControl template.
    protected override Type StyleKeyOverride => typeof(ContentControl);

    // Icons are parsed once per editor; static Avalonia objects would be shared across UI threads.
    // Swap sides and the minus of a collapsible panel have no DaisyIconSymbol.
    private readonly Geometry _switchIcon = StreamGeometry.Parse("M21,9L17,5V8H10V10H17V13L21,9M7,11L3,15L7,19V16H14V14H7V11Z");
    private readonly Geometry _removeIcon = StreamGeometry.Parse(FloweryPathHelpers.GetIconPathData("DaisyIconMinus")!);
    private readonly Geometry _addIcon = DaisyIconSymbolData.GetGeometry(DaisyIconSymbol.Add);
    private readonly Geometry _moreIcon = DaisyIconSymbolData.GetGeometry(DaisyIconSymbol.More);
    private readonly Geometry _editIcon = DaisyIconSymbolData.GetGeometry(DaisyIconSymbol.Edit);
    private readonly Geometry _linkIcon = DaisyIconSymbolData.GetGeometry(DaisyIconSymbol.Link);

    // A path icon keeps the aspect of a flat geometry; DaisyButton.IconData stretches it to a square.
    private static PathIcon PanelIcon(Geometry data) => new() { Data = data, Width = 16, Height = 16 };

    private readonly DaisySelect _steps = new();
    private readonly DaisySelect _annotations = new();
    private readonly DaisySelect _scope = new();
    private readonly StackPanel _fieldsPanel = new() { Spacing = 8 };
    private readonly StackPanel _context = new() { Spacing = 8 };
    private readonly Grid _footer = new() { ColumnSpacing = 8 };
    private readonly Dictionary<string, (Control Input, DaisyButton Mode)> _fields = [];
    private readonly Dictionary<int, bool> _expandedSections = [];
    private readonly TextBlock _stepLabel = new() { FontSize = 12 };
    private readonly TextBlock _annotationLabel = new() { FontSize = 12 };
    private readonly DaisyExpander _scopeSection = new() { IsExpanded = false };
    private readonly ContentControl _toolbar = new();
    private readonly TextBlock _title = new() { FontSize = 16, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DaisyButton _undo = new() { IconSymbol = DaisyIconSymbol.Undo, Shape = DaisyButtonShape.Square };
    private readonly DaisyButton _redo = new() { IconSymbol = DaisyIconSymbol.Redo, Shape = DaisyButtonShape.Square };
    private readonly DaisyButton _apply = new() { Variant = DaisyButtonVariant.Primary };
    private readonly DaisyButton _discard = new();
    private readonly DaisyButton _dock = new();
    private readonly DaisyButton _collapse = new();
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Border _panel = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), BorderThickness = new Thickness(1) };
    private readonly ResizeGrip _resizeGrip = new() { Width = 12, HorizontalAlignment = HorizontalAlignment.Left };
    // Avalonia's Thumb has no default template and draws nothing, so the pointer could not hit it; the template
    // draws its transparent background.
    private readonly Thumb _resizeThumb = new()
    {
        Focusable = true, IsTabStop = true, Background = new SolidColorBrush(Colors.Transparent),
        Template = new FuncControlTemplate<Thumb>(static (thumb, _) =>
        {
            var border = new Border();
            border.Bind(Border.BackgroundProperty, thumb.GetObservable(BackgroundProperty));
            return border;
        })
    };
    private readonly Border _resizeMark = new() { Width = 3, Height = 48, CornerRadius = new CornerRadius(1.5), IsHitTestVisible = false };
    // Carries the theme variant of the Daisy theme; Avalonia sets a requested variant only on a scope or a top level.
    private readonly ThemeVariantScope _themeScope = new();
    private readonly List<string> _undoHistory = [];
    private readonly List<string> _redoHistory = [];
    private DaisyTour? _tour;
    private DaisyTourStyleProfile? _draft;
    private CancellationTokenSource? _cancellation;
    private string? _gestureSnapshot;
    private bool _refreshing;
    private bool _closing;
    private bool _dockLeft;
    private bool _collapsed;
    private bool _resizing;
    private double? _desiredWidth;
    private double? _resizeStartWidth;
    private (int PointerId, double X, double Width)? _resizePointerStart;
    // The pointer that drags the resize thumb; a canceled resize releases its capture, as WinUI's CancelDrag does.
    private IPointer? _resizePointer;
    private double _viewportWidth;
    private int _openingGeneration;

    public DaisyTourEditor()
    {
        Focusable = true;
        IsTabStop = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
        foreach (var select in new[] { _steps, _annotations, _scope }) Compact(select);
        foreach (var button in new[] { _undo, _redo, _apply, _discard, _dock, _collapse }) Compact(button);
        _dock.Content = PanelIcon(_switchIcon);
        _collapse.Content = PanelIcon(_removeIcon);
        _dock.Variant = _collapse.Variant = _undo.Variant = _redo.Variant = DaisyButtonVariant.Ghost;
        _dock.Shape = _collapse.Shape = DaisyButtonShape.Square;
        _context.Children.Add(_stepLabel);
        _context.Children.Add(_steps);
        _context.Children.Add(_annotationLabel);
        _context.Children.Add(_annotations);
        _scopeSection.HeaderPadding = new Thickness(0, 6, 0, 6);
        _scopeSection.BorderThickness = new Thickness(0, 1, 0, 0);
        _scopeSection.ContentPadding = new Thickness(0, 0, 0, 8);
        _scopeSection.ExpanderContent = _scope;
        _context.Children.Add(_scopeSection);
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 24, 0) };
        body.Children.Add(_context);
        body.Children.Add(_fieldsPanel);
        body.Children.Add(_error);
        _error.FontSize = 12;
        _error.IsVisible = false;
        _scroll.Content = body;
        var chrome = new Grid { ColumnSpacing = 4 };
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        chrome.Children.Add(_title);
        Grid.SetColumn(_dock, 1);
        Grid.SetColumn(_collapse, 2);
        chrome.Children.Add(_dock);
        chrome.Children.Add(_collapse);
        var tools = new Grid { ColumnSpacing = 8 };
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        history.Children.Add(_undo);
        history.Children.Add(_redo);
        tools.Children.Add(history);
        _toolbar.HorizontalContentAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_toolbar, 1);
        tools.Children.Add(_toolbar);
        _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _discard.HorizontalAlignment = _apply.HorizontalAlignment = HorizontalAlignment.Stretch;
        _footer.Children.Add(_discard);
        Grid.SetColumn(_apply, 1);
        _footer.Children.Add(_apply);
        var layout = new Grid { RowSpacing = 10 };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(tools, 1);
        Grid.SetRow(_scroll, 2);
        Grid.SetRow(_footer, 3);
        layout.Children.Add(chrome);
        layout.Children.Add(tools);
        layout.Children.Add(_scroll);
        layout.Children.Add(_footer);
        _panel.Child = layout;
        _resizeGrip.Children.Add(_resizeThumb);
        _resizeGrip.Children.Add(_resizeMark);
        var surface = new Grid();
        surface.Children.Add(_panel);
        surface.Children.Add(_resizeGrip);
        _themeScope.Child = surface;
        Content = _themeScope;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _steps.SelectionChanged += OnStepSelectionChanged;
        _annotations.SelectionChanged += OnAnnotationSelectionChanged;
        _scope.SelectionChanged += (_, _) => RefreshValue();
        _undo.Click += (_, _) => Undo();
        _redo.Click += (_, _) => Redo();
        _apply.Click += (_, _) => Apply();
        _discard.Click += (_, _) => Discard();
        _resizeThumb.DragStarted += OnResizeStarted;
        _resizeThumb.DragDelta += OnResizeDelta;
        _resizeThumb.DragCompleted += OnResizeCompleted;
        _resizeThumb.AddHandler(PointerPressedEvent, OnResizePointerPressed, RoutingStrategies.Bubble, true);
        _resizeThumb.AddHandler(PointerReleasedEvent, OnResizePointerFinished, RoutingStrategies.Bubble, true);
        // A lost capture is a direct event; a Bubble handler would never run.
        _resizeThumb.AddHandler(PointerCaptureLostEvent, OnResizePointerFinished, RoutingStrategies.Direct, true);
        _resizeThumb.KeyDown += OnResizeKeyDown;
        _dock.Click += (_, _) => { CancelResize(); _dockLeft = !_dockLeft; _tour?.RefreshEditorLayout(); };
        _collapse.Click += (_, _) =>
        {
            CancelResize();
            _collapsed = !_collapsed;
            _scroll.IsVisible = !_collapsed;
            _collapse.Content = PanelIcon(_collapsed ? _addIcon : _removeIcon);
            _title.IsVisible = tools.IsVisible = _footer.IsVisible = !_collapsed;
            _dock.IsVisible = !_collapsed;
            if (_collapsed)
            {
                chrome.Children.Remove(_collapse);
                _panel.Child = _collapse;
            }
            else
            {
                _panel.Child = layout;
                chrome.Children.Add(_collapse);
            }
            _tour?.RefreshEditorLayout();
        };
        AutomationProperties.SetAutomationId(this, "DaisyTourEditor");
        AutomationProperties.SetAutomationId(_steps, "DaisyTourEditorSteps");
        AutomationProperties.SetAutomationId(_scroll, "DaisyTourEditorScroll");
        AutomationProperties.SetAutomationId(_footer, "DaisyTourEditorFooter");
        AutomationProperties.SetAutomationId(_scope, "DaisyTourEditorScope");
        AutomationProperties.SetAutomationId(_apply, "DaisyTourEditorApply");
        AutomationProperties.SetAutomationId(_discard, "DaisyTourEditorDiscard");
        AutomationProperties.SetAutomationId(_dock, "DaisyTourEditorMovePanel");
        AutomationProperties.SetAutomationId(_collapse, "DaisyTourEditorTogglePanel");
        AutomationProperties.SetAutomationId(_resizeThumb, "DaisyTourEditorResize");
        AutomationProperties.SetAutomationId(_undo, "DaisyTourEditorUndo");
        AutomationProperties.SetAutomationId(_redo, "DaisyTourEditorRedo");
        HandleLayer = new DaisyTourHandleLayer(this);
    }

    public bool IsOpen => _tour is not null;
    public Exception? LastError { get; private set; }
    // The problem of the previewed step that the editor shows while it lasts.
    private Exception? _previewProblem;
    public bool HasChanges => _draft is not null && _initialSnapshot != DaisyTourStyleProfileSerializer.Serialize(_draft);
    public DaisyTourStyleProfile? DraftProfile => _draft is null ? null : DaisyTourStyleProfileSerializer.Clone(_draft);
    public Control? ToolbarContent { get => _toolbar.Content as Control; set => _toolbar.Content = value; }
    public event EventHandler? ProfileChanged;
    public event EventHandler? Closed;
    internal DaisyTourHandleLayer HandleLayer { get; }
    internal Rect InspectorBounds { get; private set; }
    internal bool IsExpanded => !_collapsed;
    internal DaisyTour? Tour => _tour;
    internal DaisyTourStep? SelectedStep => (_steps.SelectedItem as ComboBoxItem)?.Tag as DaisyTourStep;
    internal DaisyTourAnnotation? SelectedAnnotation => (_annotations.SelectedItem as ComboBoxItem)?.Tag as DaisyTourAnnotation;
    internal int SelectedAnnotationIndex => _annotations.SelectedIndex;
    internal void SelectAnnotation(int index) => _annotations.SelectedIndex = index;
    private string _initialSnapshot = string.Empty;
    private string _resolvedSource = string.Empty;
    private bool _automaticValue;
    private int ScopeIndex => Math.Max(0, _scope.SelectedIndex);

    public async Task OpenAsync(DaisyTour tour, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(tour);
        if (IsOpen) throw new InvalidOperationException("This editor is already open.");
        LastError = null;
        _previewProblem = null;
        if (this.GetVisualParent() is not null || Parent is not null) throw new InvalidOperationException("The editor must not have a parent before opening.");
        var draft = tour.StyleProfile is { } profile ? DaisyTourStyleProfileSerializer.Clone(profile) : new DaisyTourStyleProfile { TourId = tour.Id };
        DaisyTourStyleProfileSerializer.Validate(draft, tour);
        var openingGeneration = ++_openingGeneration;
        _draft = draft;
        _tour = tour;
        // CurrentStep is an Avalonia property; AvaloniaObject reports it by name through its explicit
        // INotifyPropertyChanged event, like WinUI's PropertyChanged. tour.PropertyChanged is the Avalonia event
        // with AvaloniaPropertyChangedEventArgs.
        ((INotifyPropertyChanged)tour).PropertyChanged += OnTourStateChanged;
        _initialSnapshot = DaisyTourStyleProfileSerializer.Serialize(_draft);
        _undoHistory.Clear();
        _redoHistory.Clear();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _steps.IsEnabled = false;
        _annotations.IsEnabled = false;
        FloweryLocalization.CultureChanged += OnCultureChanged;
        RefreshLabels();
        _refreshing = true;
        _steps.Items.Clear();
        foreach (var step in tour.Steps) _steps.Items.Add(new ComboBoxItem { Content = step.Id, Tag = step });
        _steps.SelectedItem = _steps.Items.FirstOrDefault();
        _scope.SelectedIndex = 4;
        _refreshing = false;
        RefreshAnnotations();
        try
        {
            await tour.BeginEditingAsync(this, _draft, _cancellation.Token);
            cancellationToken.ThrowIfCancellationRequested();
            if (openingGeneration != _openingGeneration) return;
            if (!IsOpen && LastError is null) return;
            if (!tour.IsOpen) throw LastError ?? new InvalidOperationException("The tour could not open its preview.");
            _steps.IsEnabled = true;
            _annotations.IsEnabled = true;
            RefreshValue();
            UpdateHistoryButtons();
        }
        catch
        {
            if (openingGeneration == _openingGeneration) CloseEditor();
            throw;
        }
    }

    public void ImportProfile(DaisyTourStyleProfile profile)
    {
        VerifyOpen();
        DaisyTourStyleProfileSerializer.Validate(profile, _tour);
        var replacement = DaisyTourStyleProfileSerializer.Clone(profile);
        CancelGesture();
        _tour!.ClearCardPositionPreview();
        RecordUndo();
        _draft = replacement;
        PublishDraft();
        RefreshValue();
    }

    public void ImportJson(string json) => ImportProfile(DaisyTourStyleProfileSerializer.Deserialize(json));
    public DaisyTourStyleProfile ExportProfile()
    {
        VerifyOpen();
        DaisyTourStyleProfileSerializer.Validate(_draft!, _tour);
        return DaisyTourStyleProfileSerializer.Clone(_draft!);
    }
    public string ExportJson() => DaisyTourStyleProfileSerializer.Serialize(ExportProfile());

    public void Apply()
    {
        VerifyOpen();
        CancelGesture();
        _tour!.StyleProfile = ExportProfile();
        CloseEditor();
    }

    public void Discard() { VerifyAccess(); if (IsOpen) CloseEditor(); }
    public void Undo() => RestoreHistory(_undoHistory, _redoHistory);
    public void Redo() => RestoreHistory(_redoHistory, _undoHistory);

    /// <summary>Changes one authored value at the selected scope; null restores the application setting.</summary>
    public void SetValue(string propertyName, object? value)
    {
        VerifyOpen();
        ValidateProperty(propertyName);
        if (value is not null) DaisyTourProfileResolver.ValidateValue(propertyName, value);
        var scope = CurrentOverride(false);
        var previous = DaisyTourProfileResolver.GetValue(scope, propertyName);
        var unchanged = value is double[] numbers && previous is double[] oldNumbers
            ? numbers.SequenceEqual(oldNumbers) : Equals(value, previous);
        if (unchanged && scope?.Inherit.Contains(propertyName) != true) return;
        if (propertyName is "cardX" or "cardY" or "cardPosition" or "placement") _tour!.ClearCardPositionPreview();
        RecordUndo();
        DaisyTourProfileResolver.SetValue(CurrentOverride(create: true)!, propertyName, value);
        PublishDraft();
    }

    public void InheritValue(string propertyName)
    {
        VerifyOpen();
        ValidateProperty(propertyName);
        if (CurrentOverride(false)?.Inherit.Contains(propertyName) == true) return;
        if (propertyName is "cardX" or "cardY" or "cardPosition" or "placement") _tour!.ClearCardPositionPreview();
        RecordUndo();
        DaisyTourProfileResolver.InheritValue(CurrentOverride(create: true)!, propertyName);
        PublishDraft();
    }

    internal void SetAnnotationValue(string name, object value, bool publish = true)
    {
        if (SelectedAnnotation is null) return;
        var scope = AnnotationOverride(create: true)!;
        DaisyTourProfileResolver.SetValue(scope, name, value);
        if (publish) PublishDraft();
    }

    internal void SetCardPosition(double x, double y)
    {
        if (_tour?.CurrentStep != SelectedStep || SelectedStep is null) return;
        RecordUndo();
        var step = StepProfile(create: true)!;
        var style = step.Style ??= new();
        DaisyTourProfileResolver.SetValue(style, "cardX", x);
        DaisyTourProfileResolver.SetValue(style, "cardY", y);
        PublishDraft();
        RefreshValue();
    }

    internal void BeginGesture()
    {
        if (_draft is not null && _gestureSnapshot is null)
            _gestureSnapshot = DaisyTourStyleProfileSerializer.Serialize(_draft);
    }

    internal void EndGesture()
    {
        if (_gestureSnapshot is not { } before) return;
        _gestureSnapshot = null;
        if (_draft is not null && before != DaisyTourStyleProfileSerializer.Serialize(_draft))
        {
            PushHistory(_undoHistory, before);
            _redoHistory.Clear();
        }
        UpdateHistoryButtons();
        RefreshValue();
    }

    internal void CancelGesture()
    {
        _tour?.CancelCardDrag();
        if (_gestureSnapshot is not { } before) return;
        HandleLayer.ReleaseGesture();
        _gestureSnapshot = null;
        _draft = DaisyTourStyleProfileSerializer.Deserialize(before);
        PublishDraft();
        RefreshValue();
    }

    internal void UpdateHandles()
    {
        HandleLayer.Update();
        if (_gestureSnapshot is null && _tour?.IsCardDragging != true) RefreshValueDescription();
    }

    internal string ErrorText => _error.Text ?? string.Empty;

    // A previewed step that cannot show everything stays open; the editor shows why while the problem lasts,
    // and the host hears nothing.
    internal void OnPreviewTargetUnavailable(DaisyTourStepTargetUnavailableEventArgs problem)
    {
        _previewProblem = problem.Error;
        ShowError(problem.Error);
    }

    internal void OnPreviewTargetAvailable()
    {
        if (_previewProblem is { } problem && _error.Text == problem.Message)
        {
            _error.Text = string.Empty;
            _error.IsVisible = false;
        }
        _previewProblem = null;
    }

    internal void OnPreviewClosed(Exception? error)
    {
        if (_closing || !IsOpen) return;
        LastError = error;
        if (error is not null) _error.Text = error.Message;
        CloseEditor();
    }

    internal void ArrangeInspector(Size size, Point origin = default)
    {
        _viewportWidth = size.Width;
        _panel.Padding = new Thickness(_collapsed ? 4 : 12);
        if (_collapsed) _collapse.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var collapsedSize = Math.Ceiling(Math.Max(_collapse.DesiredSize.Width, _collapse.DesiredSize.Height)) + 10;
        var width = _collapsed ? collapsedSize : Math.Clamp(_desiredWidth ?? Math.Min(300, Math.Max(180, size.Width / 3)), 180, MaximumInspectorWidth);
        Width = width;
        Height = _collapsed ? collapsedSize : Math.Max(1, size.Height - 24);
        Canvas.SetLeft(this, origin.X + (_dockLeft ? 12 : Math.Max(0, size.Width - width - 12)));
        Canvas.SetTop(this, origin.Y + 12);
        InspectorBounds = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), width, Height);
        _resizeGrip.HorizontalAlignment = _dockLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _resizeGrip.IsVisible = !_collapsed;
        HandleLayer.Width = size.Width;
        HandleLayer.Height = size.Height;
        RefreshAppearance();
    }

    private double MaximumInspectorWidth => Math.Max(180, Math.Min(600, _viewportWidth - 356));

    internal void ResizeInspectorBy(double horizontalDelta)
    {
        if (!IsOpen || _collapsed || !double.IsFinite(horizontalDelta)) return;
        var width = Math.Clamp(_desiredWidth ?? Width, 180, MaximumInspectorWidth);
        _desiredWidth = Math.Clamp(width + (_dockLeft ? horizontalDelta : -horizontalDelta), 180, MaximumInspectorWidth);
        _tour?.RefreshEditorLayout();
    }

    private void OnResizeStarted(object? sender, VectorEventArgs args)
    {
        _resizeStartWidth = _desiredWidth;
        _resizing = true;
    }

    private void OnResizeDelta(object? sender, VectorEventArgs args)
    {
        if (_resizing) ResizeInspectorBy(args.Vector.X);
    }

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!_resizing || _tour is not { } tour) return;
        _resizePointer = args.Pointer;
        // A press captures the element it hits, the border of the thumb's template. The thumb takes the capture,
        // so it receives the capture loss, and a canceled resize can release it.
        args.Pointer.Capture(_resizeThumb);
        var point = args.GetCurrentPoint(tour.EditingSurface);
        if (point.Properties.IsLeftButtonPressed)
            _resizePointerStart = (args.Pointer.Id, point.Position.X, Math.Clamp(_desiredWidth ?? Width, 180, MaximumInspectorWidth));
    }

    private void OnResizePointerFinished(object? sender, RoutedEventArgs args)
    {
        var pointer = args switch { PointerEventArgs released => released.Pointer, PointerCaptureLostEventArgs lost => lost.Pointer, _ => null };
        if (_resizePointerStart is not { } start || pointer is null || start.PointerId != pointer.Id || _tour is not { } tour) return;
        _resizePointerStart = null;
        // A lost capture carries no position in Avalonia; the width of the last drag delta stays.
        if (args is not PointerEventArgs finished) return;
        // Thumb may complete before these handlers, with no final PointerMoved event.
        // Use the final position in the stationary surface, once, rather than adding another drag delta.
        var delta = finished.GetPosition(tour.EditingSurface).X - start.X;
        _desiredWidth = Math.Clamp(start.Width + (_dockLeft ? delta : -delta), 180, MaximumInspectorWidth);
        tour.RefreshEditorLayout();
    }

    private void OnResizeCompleted(object? sender, VectorEventArgs args) => CompleteResize(canceled: false);

    private void CompleteResize(bool canceled)
    {
        if (!_resizing) return;
        _resizing = false;
        _resizePointer = null;
        if (canceled)
        {
            _resizePointerStart = null;
            _desiredWidth = _resizeStartWidth;
            _tour?.RefreshEditorLayout();
        }
        _resizeStartWidth = null;
    }

    private void CancelResize()
    {
        _resizePointerStart = null;
        if (!_resizing) return;
        // Avalonia's Thumb cannot cancel its drag: the resize ends as canceled, and releasing the thumb's capture
        // ends the drag of the thumb. Its DragCompleted then finds no resize to complete.
        var pointer = _resizePointer;
        CompleteResize(canceled: true);
        if (pointer?.Captured == _resizeThumb) pointer.Capture(null);
    }

    private void OnResizeKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key is not (Key.Left or Key.Right)) return;
        ResizeInspectorBy(args.Key == Key.Left ? -10 : 10);
        args.Handled = true;
    }

    internal void HandleKey(KeyEventArgs args)
    {
        if (args.Handled) return;
        if (args.Key == Key.Escape)
        {
            if (_resizing) CancelResize();
            else if (_tour?.IsCardDragging == true || _gestureSnapshot is not null) CancelGesture();
            else Discard();
            args.Handled = true;
        }
        // The undo and redo gestures of the platform, such as Cmd+Shift+Z on macOS; redo first, as it can include
        // the undo key. Without platform settings, Ctrl or Cmd with Z and Y.
        if (Application.Current?.PlatformSettings?.HotkeyConfiguration is { } hotkeys)
        {
            if (hotkeys.Redo.Any(gesture => gesture.Matches(args))) { Redo(); args.Handled = true; }
            else if (hotkeys.Undo.Any(gesture => gesture.Matches(args))) { Undo(); args.Handled = true; }
            return;
        }
        var control = (args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (control && args.Key is Key.Z or Key.Y)
        {
            if (args.Key == Key.Z) Undo(); else Redo();
            args.Handled = true;
        }
    }

    private void CloseEditor()
    {
        if (_tour is not { } tour || _closing) return;
        _closing = true;
        _openingGeneration++;
        try
        {
            CancelResize();
            tour.ClearCardPositionPreview();
            HandleLayer.ReleaseGesture();
            _gestureSnapshot = null;
            try { _cancellation?.Cancel(); }
            catch (AggregateException error) { LastError = error; }
            tour.EndEditing(this);
            ((INotifyPropertyChanged)tour).PropertyChanged -= OnTourStateChanged;
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            _cancellation?.Dispose();
            _cancellation = null;
            _tour = null;
            _draft = null;
            _refreshing = true;
            _steps.SelectedItem = null;
            _annotations.SelectedItem = null;
            _steps.Items.Clear();
            _annotations.Items.Clear();
            _fields.Clear();
            _fieldsPanel.Children.Clear();
            _refreshing = false;
            HandleLayer.Clear();
        }
        finally { _closing = false; }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void RecordUndo()
    {
        if (_gestureSnapshot is not null || _draft is null) return;
        PushHistory(_undoHistory, DaisyTourStyleProfileSerializer.Serialize(_draft));
        _redoHistory.Clear();
    }

    private static void PushHistory(List<string> history, string snapshot)
    {
        history.Add(snapshot);
        if (history.Count > 100) history.RemoveAt(0);
    }

    private void RestoreHistory(List<string> from, List<string> to)
    {
        VerifyOpen();
        CancelGesture();
        _tour!.ClearCardPositionPreview();
        if (from.Count == 0) { _tour.RefreshEditorLayout(); return; }
        PushHistory(to, DaisyTourStyleProfileSerializer.Serialize(_draft!));
        _draft = DaisyTourStyleProfileSerializer.Deserialize(from[^1]);
        from.RemoveAt(from.Count - 1);
        PublishDraft();
        RefreshValue();
    }

    private void PublishDraft()
    {
        if (_draft is null) return;
        _tour?.UpdatePreview(_draft, geometryOnly: _gestureSnapshot is not null);
        // A draft change clears an input error, not a problem of the previewed step that still lasts.
        _error.Text = _previewProblem?.Message ?? string.Empty;
        if (_gestureSnapshot is null)
        {
            RefreshValueDescription();
            UpdateHistoryButtons();
        }
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetTransitioning(bool transitioning)
    {
        _scroll.IsEnabled = !transitioning;
        _steps.IsEnabled = _annotations.IsEnabled = !transitioning;
    }

    private void OnTourStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(DaisyTour.CurrentStep) || _tour?.CurrentStep is not { } current) return;
        var index = _steps.Items.Cast<ComboBoxItem>().ToList().FindIndex(item => ReferenceEquals(item.Tag, current));
        if (index == _steps.SelectedIndex) return;
        _refreshing = true;
        _steps.SelectedIndex = index;
        _refreshing = false;
        RefreshAnnotations();
        UpdateHandles();
    }

    private void UpdateHistoryButtons()
    {
        _undo.IsEnabled = _undoHistory.Count > 0;
        _redo.IsEnabled = _redoHistory.Count > 0;
        _apply.IsEnabled = IsOpen;
    }

    private async void OnStepSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_refreshing || _tour is null) return;
        CancelGesture();
        HandleLayer.ReleaseGesture();
        RefreshAnnotations();
        _steps.IsEnabled = false;
        _annotations.IsEnabled = false;
        try { await _tour.EditStepAsync(_steps.SelectedIndex); }
        catch (Exception error) { ShowError(error); }
        finally { _steps.IsEnabled = IsOpen; _annotations.IsEnabled = IsOpen; }
        RefreshValue();
        UpdateHandles();
    }

    private void OnAnnotationSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_refreshing) return;
        CancelGesture();
        RefreshValue();
        UpdateHandles();
    }

    private void RefreshAnnotations()
    {
        _refreshing = true;
        _annotations.Items.Clear();
        if (SelectedStep is { } step)
        {
            if (step.Annotations.Count == 0)
                _annotations.Items.Add(new ComboBoxItem { Content = "default-pointer", Tag = new DaisyTourAnnotation { Id = "default-pointer" } });
            else foreach (var annotation in step.Annotations)
                _annotations.Items.Add(new ComboBoxItem { Content = annotation.Id, Tag = annotation });
        }
        _annotations.SelectedItem = _annotations.Items.FirstOrDefault();
        _refreshing = false;
        RefreshValue();
    }

    private bool IsPropertyAvailable(string name)
    {
        string[] tour = ["size", "cardPosition", "cardBorderBrush", "highlightPadding", "highlightCornerRadius"];
        string[] step = ["placement", "cardPosition", "cardX", "cardY"];
        string[] endpoints = ["sourceAnchor", "targetAnchor", "routeLimitSide", "routeLimitCoordinate", "routeLimitMargin"];
        return ScopeIndex switch
        {
            0 => tour.Contains(name),
            2 => step.Contains(name),
            4 => !tour.Contains(name) && !step.Contains(name),
            _ => !tour.Contains(name) && !step.Contains(name) && !endpoints.Contains(name)
        };
    }

    private static int PropertyGroup(string name) => name switch
        {
            "preset" or "kind" or "stroke" or "fill" or "foreground" or "opacity" => 0,
            "strokeThickness" or "strokeStyle" or "dashArray" or "dashOffset" or "startHead" or "endHead" or "arrowStyle" or "bodyWidth" or "headLength" or "headWidth" => 1,
            "sourceAnchor" or "targetAnchor" or "routeLimitSide" or "routeLimitCoordinate" or "routeLimitMargin" => 4,
            "fontSize" or "maxWidth" or "maxHeight" or "tail" or "tailWidth" or "tailLength" => 3,
            _ => 2
        };

    private bool IsRelevantProperty(string name)
    {
        if (ScopeIndex is 0 or 2) return true;
        var group = PropertyGroup(name);
        if (ScopeIndex != 4 || _tour is null || SelectedStep is not { } step || SelectedAnnotation is not { } annotation) return true;
        var layers = _tour.ProfileLayers(step, annotation);
        var kind = DaisyTourAnnotationPresenter.ResolveKind(annotation, _tour.AnnotationDefaults, step.AnnotationDefaults, layers);
        var connector = kind is DaisyTourAnnotationKind.Arrow or DaisyTourAnnotationKind.Line;
        if (group is 1 or 4 && !connector) return false;
        if (name is "tail" or "tailWidth" or "tailLength" && connector) return false;
        if (name is "width" or "height" && connector) return false;
        if (name is "path" or "curvature" || name.StartsWith("controlPoint", StringComparison.Ordinal))
        {
            if (!connector) return false;
            var path = DaisyTourAnnotationPresenter.ResolvePath(annotation, _tour.AnnotationDefaults, step.AnnotationDefaults, layers);
            if (name.StartsWith("controlPoint", StringComparison.Ordinal)) return Equals(path, DaisyTourAnnotationPath.Bezier);
            if (name == "curvature") return path is DaisyTourAnnotationPath.Curved or DaisyTourAnnotationPath.SCurve or DaisyTourAnnotationPath.Bezier;
        }
        return true;
    }

    private void RefreshValue()
    {
        // Fields need the active preview's resolved values; startup and selection prepare them first.
        if (_refreshing || !IsOpen || SelectedStep is not { } step || _tour?.CurrentStep != step) return;
        _refreshing = true;
        try
        {
            _fields.Clear();
            _fieldsPanel.Children.Clear();
            var names = DaisyTourProfileResolver.PropertyNames.Where(IsPropertyAvailable).Where(IsRelevantProperty).ToArray();
            foreach (var group in new[] { 0, 4, 1, 2, 3 })
            {
                var properties = names.Where(name => PropertyGroup(name) == group).ToArray();
                if (properties.Length == 0) continue;
                var rows = new StackPanel { Spacing = 10 };
                foreach (var name in properties) rows.Children.Add(BuildField(name));
                var key = group switch { 0 => "Appearance", 1 => "Line", 4 => "Connections", 2 => "Geometry", _ => "Text" };
                var section = new DaisyExpander
                {
                    Header = new TextBlock { Text = Local("TourEditor_" + key), FontSize = 13, FontWeight = FontWeight.SemiBold },
                    HeaderPadding = new Thickness(0, 8, 0, 8),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    BorderBrush = DaisyResourceLookup.GetBrush("DaisyAccentBrush"),
                    ContentPadding = new Thickness(0, 4, 0, 8),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    IsExpanded = _expandedSections.GetValueOrDefault(group, group is 0 or 1 or 4),
                    ExpanderContent = rows
                };
                section.PropertyChanged += (_, args) =>
                {
                    if (args.Property == DaisyExpander.IsExpandedProperty) _expandedSections[group] = section.IsExpanded;
                };
                _fieldsPanel.Children.Add(section);
            }
        }
        finally { _refreshing = false; }
        RefreshValueDescription();
    }

    private Control BuildField(string name)
    {
        var label = Local("TourEditor_Field_" + name);
        var mode = new DaisyButton
        {
            Content = new PathIcon { Data = _moreIcon, Width = 14, Height = 14 },
            Variant = DaisyButtonVariant.Ghost, Shape = DaisyButtonShape.Square
        };
        Compact(mode);
        mode.Width = mode.Height = 24;
        AutomationProperties.SetAutomationId(mode, "DaisyTourEditorMode_" + name);
        AutomationProperties.SetName(mode, label + " · " + Local("TourEditor_Scope"));
        var menu = new MenuFlyout();
        menu.Opening += (sender, _) =>
        {
            var opening = (MenuFlyout)sender!;
            ApplyPresenterTheme(opening, FlyoutPresenterStyle(typeof(MenuFlyoutPresenter), new Thickness(4)));
            foreach (var item in opening.Items.OfType<MenuItem>())
            {
                item.Foreground = Foreground;
                item.FontSize = 13;
                item.Padding = new Thickness(12, 8, 12, 8);
                item.Resources["MenuFlyoutItemBackgroundPointerOver"] = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
                item.Resources["MenuFlyoutItemBackgroundPressed"] = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
                item.Resources["MenuFlyoutItemForegroundPointerOver"] = Foreground;
                item.Resources["MenuFlyoutItemForegroundPressed"] = Foreground;
            }
        };
        foreach (var action in new[] { "Application", "Override", "Inherit" })
        {
            var item = new MenuItem { Header = Local("TourEditor_" + action) };
            AutomationProperties.SetAutomationId(item, "DaisyTourEditor" + action + "_" + name);
            item.Click += (_, _) => ChangeValueMode(name, action);
            menu.Items.Add(item);
        }
        mode.Flyout = menu;
        var value = DaisyTourProfileResolver.GetValue(CurrentOverride(false), name) ?? EffectiveValue(name);
        var input = BuildInput(name, value);
        AutomationProperties.SetName(input, label);
        AutomationProperties.SetAutomationId(input, "DaisyTourEditorValue_" + name);
        _fields.Add(name, (input, mode));
        var row = new Grid { ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var caption = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(caption, label);
        row.Children.Add(caption);
        Grid.SetColumn(input, 1);
        Grid.SetColumn(mode, 2);
        row.Children.Add(input);
        row.Children.Add(mode);
        return row;
    }

    private void RefreshValueDescription()
    {
        if (!IsOpen) return;
        foreach (var (name, field) in _fields)
        {
            var effective = EffectiveValue(name);
            ToolTip.SetTip(field.Mode,
                Local("TourEditor_ApplicationValue") + ": " + Describe(ApplicationValue(name)) + "\n" +
                Local("TourEditor_EffectiveValue") + ": " + (_automaticValue ? "Auto: " : string.Empty) +
                Describe(effective) + " (" + _resolvedSource + ")");
            var scope = CurrentOverride(false);
            if (field.Mode.Content is PathIcon icon)
                icon.Data = DaisyTourProfileResolver.GetValue(scope, name) is not null ? _editIcon :
                    scope?.Inherit.Contains(name) == true ? _linkIcon : _moreIcon;
        }
        _error.IsVisible = !string.IsNullOrEmpty(_error.Text);
    }

    private static void Compact(Control control)
    {
        FlowerySizeManager.SetIgnoreGlobalSize(control, true);
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        switch (control)
        {
            case DaisySelect select: select.Size = DaisySize.Small; break;
            case DaisyButton button: button.Size = DaisySize.Small; break;
            case DaisyNumericUpDown number: number.Size = DaisySize.Small; break;
            case DaisyInput input: input.Size = DaisySize.Small; break;
        }
    }

    private object? ApplicationValue(string name)
    {
        var property = DaisyTourProfileResolver.FindProperty(name);
        if (property is null) return null;
        AvaloniaObject? source = ScopeIndex switch
        {
            0 => _tour,
            1 => _tour?.AnnotationDefaults,
            2 => SelectedStep,
            3 => SelectedStep?.AnnotationDefaults,
            _ => SelectedAnnotation
        };
        if (ScopeIndex == 2 && name == "cardPosition") property = DaisyTourStep.CardPositionProperty;
        return source?.GetValue(property);
    }

    private object? EffectiveValue(string name)
    {
        _automaticValue = false;
        _resolvedSource = Local("TourEditor_Application");
        if (_tour is null || SelectedStep is not { } step || SelectedAnnotation is not { } annotation ||
            DaisyTourProfileResolver.FindProperty(name) is not { } property) return ApplicationValue(name);
        if (ScopeIndex is 0 or 2 || name is "sourceAnchor" or "targetAnchor" or "routeLimitSide" or "routeLimitCoordinate" or "routeLimitMargin")
        {
            if (DaisyTourProfileResolver.TryGetValue(CurrentOverride(false), property, out _, out var inherit))
                _resolvedSource = Local(inherit ? "TourEditor_Inherit" : "TourEditor_Override");
            return _tour.EditorSetting(step, annotation, name, ScopeIndex == 2);
        }
        var index = SelectedAnnotationIndex;
        if (_tour.CurrentStep != step || index < 0 || index >= _tour.AnnotationPresenters.Count ||
            !_tour.AnnotationPresenters[index].ResolvedValues.TryGetValue(property, out var resolved))
            return ApplicationValue(name);
        _automaticValue = resolved.Automatic;
        _resolvedSource = resolved.Scope switch
        {
            0 => Local("TourEditor_Annotation"), 1 => Local("TourEditor_StepDefaults"),
            2 => Local("TourEditor_TourDefaults"), _ => Local("TourEditor_Application")
        };
        if (resolved.IsOverride) _resolvedSource += ", " + Local("TourEditor_Override");
        if (resolved.HighContrast) _resolvedSource = "HighContrast";
        return resolved.Value;
    }

    private Control BuildInput(string name, object? value)
    {
        var type = DaisyTourProfileResolver.ValueType(name);
        if (type?.IsEnum == true)
        {
            var combo = new DaisySelect { ItemsSource = Enum.GetValues(type).Cast<object>().Where(item => item.ToString() != "Default").ToArray() };
            Compact(combo);
            combo.SelectedItem = value;
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is { } selected) EditInput(name, selected, combo); };
            return combo;
        }
        if (type == typeof(double))
        {
            var number = new DaisyNumericUpDown
            {
                Minimum = decimal.MinValue, Maximum = decimal.MaxValue, MaxDecimalPlaces = 4,
                Increment = name is "opacity" or "curvature" or "cardX" or "cardY" || name.StartsWith("controlPoint", StringComparison.Ordinal) ? 0.05m : 1m,
                Value = value is double d && double.IsFinite(d) && d > (double)decimal.MinValue && d < (double)decimal.MaxValue ? (decimal)d : null
            };
            Compact(number);
            if (name is "opacity" or "cardX" or "cardY") { number.Minimum = 0; number.Maximum = 1; }
            number.PropertyChanged += (_, args) =>
            {
                if (args.Property == DaisyNumericUpDown.ValueProperty && number.Value is { } edited) EditInput(name, (double)edited, number);
            };
            return number;
        }
        if (type == typeof(string))
        {
            var button = new DaisyButton { HorizontalContentAlignment = HorizontalAlignment.Left };
            Compact(button);
            var color = new DaisyColorEditor { ShowAlphaChannel = true, ShowRgbSliders = false, ShowHslSliders = false, Width = 240 };
            if (value is ISolidColorBrush brush) color.Color = brush.Color;
            else if (value is string text && text.Length == 9 && uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
                color.Color = Color.FromArgb((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
            var wheel = new DaisyColorWheel
            {
                Width = 208, Height = 208, HorizontalAlignment = HorizontalAlignment.Center,
                Lightness = new HslColor(color.Color).L, Color = color.Color
            };
            AutomationProperties.SetName(wheel, Local("ColorPicker_Title"));
            var lightness = new DaisyColorSlider
            {
                Channel = ColorSliderChannel.Lightness, Height = 22, Color = color.Color
            };
            AutomationProperties.SetName(lightness, Local("ColorPicker_Lightness"));
            var alpha = new DaisyColorSlider
            {
                Channel = ColorSliderChannel.Alpha, Height = 22, Color = color.Color
            };
            AutomationProperties.SetName(alpha, Local("TourEditor_Field_opacity"));
            AutomationProperties.SetAutomationId(wheel, "DaisyTourEditorColorWheel_" + name);
            AutomationProperties.SetAutomationId(lightness, "DaisyTourEditorLightness_" + name);
            AutomationProperties.SetAutomationId(alpha, "DaisyTourEditorAlpha_" + name);
            ToolTip.SetTip(wheel, Local("ColorPicker_ClickAndDrag"));
            ToolTip.SetTip(lightness, AutomationProperties.GetName(lightness));
            ToolTip.SetTip(alpha, AutomationProperties.GetName(alpha));
            var picker = new StackPanel { Width = 240, Spacing = 8 };
            picker.Children.Add(wheel);
            picker.Children.Add(new TextBlock { Text = AutomationProperties.GetName(lightness), FontSize = 12 });
            picker.Children.Add(lightness);
            picker.Children.Add(new TextBlock { Text = AutomationProperties.GetName(alpha), FontSize = 12 });
            picker.Children.Add(alpha);
            picker.Children.Add(color);
            var swatch = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(color.Color) };
            var caption = new TextBlock { Text = Describe(value), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(swatch);
            content.Children.Add(caption);
            button.Content = content;
            var flyout = new Flyout { Content = picker };
            flyout.Opening += (sender, _) => ApplyPresenterTheme((Flyout)sender!, FlyoutPresenterStyle(typeof(FlyoutPresenter), new Thickness(12)));
            button.Flyout = flyout;
            var synchronizing = false;
            wheel.ColorChanged += (_, args) => { if (!synchronizing) color.Color = args.Color; };
            lightness.ColorChanged += (_, args) => { if (!synchronizing) color.Color = args.Color; };
            alpha.ColorChanged += (_, args) => { if (!synchronizing) color.Color = args.Color; };
            color.ColorChanged += (_, args) =>
            {
                synchronizing = true;
                try
                {
                    wheel.Lightness = new HslColor(args.Color).L;
                    wheel.Color = args.Color;
                    lightness.Color = args.Color;
                    alpha.Color = args.Color;
                }
                finally { synchronizing = false; }
                var hex = $"#{args.Color.A:X2}{args.Color.R:X2}{args.Color.G:X2}{args.Color.B:X2}";
                EditInput(name, hex, button);
                caption.Text = hex;
                swatch.Background = new SolidColorBrush(args.Color);
            };
            return button;
        }
        var textBox = new DaisyInput { Text = value is IEnumerable<double> values ? string.Join(", ", values.Select(item => item.ToString(CultureInfo.InvariantCulture))) : string.Empty };
        Compact(textBox);
        textBox.LostFocus += (_, _) =>
        {
            if (_refreshing || !IsCurrentInput(name, textBox)) return;
            try { EditInput(name, (textBox.Text ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(item => double.Parse(item, CultureInfo.InvariantCulture)).ToArray(), textBox); }
            catch (FormatException error) { ShowError(error); }
        };
        return textBox;
    }

    private bool IsCurrentInput(string name, Control input) =>
        IsOpen && _fields.TryGetValue(name, out var field) && ReferenceEquals(field.Input, input);

    private void EditInput(string name, object value, Control input)
    {
        if (_refreshing || !IsCurrentInput(name, input)) return;
        try
        {
            SetValue(name, value);
            if (name is "path" or "kind" or "preset") RefreshValue();
        }
        catch (ArgumentException error) { ShowError(error); }
    }

    private void ChangeValueMode(string name, string mode)
    {
        if (_refreshing || !IsOpen || !_fields.ContainsKey(name)) return;
        try
        {
            if (mode == "Inherit") InheritValue(name);
            else if (mode == "Application") SetValue(name, null);
            else
            {
                var value = EffectiveValue(name);
                if (value is ISolidColorBrush brush) value = $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                else if (value is IBrush) value = null;
                if (value is DaisyTourDashCollection dash) value = dash.ToArray();
                if (value is double d && !double.IsFinite(d)) value = null;
                if (value is null || value is Enum e && e.ToString() == "Default") return;
                SetValue(name, value);
            }
            RefreshValue();
        }
        catch (ArgumentException error) { ShowError(error); }
    }

    private void ShowError(Exception error)
    {
        _error.Text = error.Message;
        _error.IsVisible = true;
    }

    private DaisyTourStepStyleProfile? StepProfile(bool create)
    {
        if (_draft is null || SelectedStep is not { } selected) return null;
        var step = DaisyTourProfileResolver.FindStep(_draft, selected.Id);
        if (step is null && create) { step = new() { Id = selected.Id }; _draft.Steps.Add(step); }
        return step;
    }

    private DaisyTourStyleOverride? AnnotationOverride(bool create)
    {
        if (StepProfile(create) is not { } step || SelectedAnnotation is not { } selected) return null;
        var annotation = DaisyTourProfileResolver.FindAnnotation(step, selected.Id);
        if (annotation is null && create) { annotation = new() { Id = selected.Id }; step.Annotations.Add(annotation); }
        if (annotation is null) return null;
        return create ? annotation.Style ??= new() : annotation.Style;
    }

    private DaisyTourStyleOverride? CurrentOverride(bool create)
    {
        if (_draft is null) return null;
        return ScopeIndex switch
        {
            0 => create ? _draft.Style ??= new() : _draft.Style,
            1 => create ? _draft.AnnotationDefaults ??= new() : _draft.AnnotationDefaults,
            2 => StepProfile(create) is { } step ? create ? step.Style ??= new() : step.Style : null,
            3 => StepProfile(create) is { } step ? create ? step.AnnotationDefaults ??= new() : step.AnnotationDefaults : null,
            _ => AnnotationOverride(create)
        };
    }

    private static string Describe(object? value) => value switch
    {
        null => "—", double number when double.IsNaN(number) => "—",
        ISolidColorBrush brush => $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}",
        IBrush brush => brush.GetType().Name,
        IEnumerable<double> values => string.Join(", ", values), _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? "—"
    };

    private static string Local(string key) => FloweryLocalization.GetStringInternal(key);
    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        if (Dispatcher.UIThread.CheckAccess()) RefreshLabels(); else Dispatcher.UIThread.Post(RefreshLabels);
    }

    private void RefreshLabels()
    {
        _refreshing = true;
        _title.Text = Local("TourEditor_Title");
        _stepLabel.Text = Local("TourEditor_Step");
        _annotationLabel.Text = Local("TourEditor_Annotation");
        AutomationProperties.SetName(_steps, _stepLabel.Text);
        AutomationProperties.SetName(_annotations, _annotationLabel.Text);
        _scopeSection.Header = new TextBlock { Text = Local("TourEditor_Scope"), FontSize = 13, FontWeight = FontWeight.SemiBold };
        AutomationProperties.SetName(_scope, Local("TourEditor_Scope"));
        var selectedScope = _scope.SelectedIndex;
        _scope.SelectedIndex = -1;
        _scope.ItemsSource = new[] { Local("TourEditor_Tour"), Local("TourEditor_TourDefaults"), Local("TourEditor_Step"), Local("TourEditor_StepDefaults"), Local("TourEditor_Annotation") };
        _scope.SelectedIndex = selectedScope < 0 ? 4 : selectedScope;
        AutomationProperties.SetName(_undo, Local("ColorPicker_Undo"));
        ToolTip.SetTip(_undo, Local("ColorPicker_Undo"));
        AutomationProperties.SetName(_redo, Local("ColorPicker_Redo"));
        ToolTip.SetTip(_redo, Local("ColorPicker_Redo"));
        AutomationProperties.SetName(_resizeThumb, Local("TourEditor_ResizePanel"));
        ToolTip.SetTip(_resizeThumb, Local("TourEditor_ResizePanel"));
        _apply.Content = Local("Common_Apply");
        _discard.Content = Local("TourEditor_Discard");
        AutomationProperties.SetName(_dock, Local("TourEditor_MovePanel"));
        ToolTip.SetTip(_dock, Local("TourEditor_MovePanel"));
        AutomationProperties.SetName(_collapse, Local("TourEditor_TogglePanel"));
        ToolTip.SetTip(_collapse, Local("TourEditor_TogglePanel"));
        _refreshing = false;
        RefreshValue();
    }

    // WinUI reads the presenter style after Opening. Avalonia creates the presenter and applies its
    // FlyoutPresenterTheme before it raises Opening, so the theme of each opening goes to the presenter itself.
    private static void ApplyPresenterTheme(PopupFlyoutBase flyout, ControlTheme? theme)
    {
        if (theme is not null && flyout.Popup.Child is { } presenter) presenter.Theme = theme;
    }

    // The presenter theme keeps the default template of the presenter and replaces its colors and shape.
    // Without a default theme there is no template to keep, and the presenter keeps its own theme.
    private static ControlTheme? FlyoutPresenterStyle(Type targetType, Thickness padding)
    {
        if (Application.Current?.FindResource(targetType) is not ControlTheme basedOn) return null;
        var style = new ControlTheme(targetType) { BasedOn = basedOn };
        style.Setters.Add(new Setter(BackgroundProperty, DaisyResourceLookup.GetBrush("DaisyBase100Brush")));
        style.Setters.Add(new Setter(ForegroundProperty, DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")));
        style.Setters.Add(new Setter(BorderBrushProperty, DaisyAccessibility.IsHighContrast
            ? DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") : DaisyResourceLookup.GetBrush("DaisyAccentBrush")));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(CornerRadiusProperty, new CornerRadius(8)));
        style.Setters.Add(new Setter(PaddingProperty, padding));
        // The flyout opens from a control inside the theme scope, so the presenter inherits its variant.
        return style;
    }

    private void RefreshAppearance()
    {
        if (DaisyThemeManager.CurrentThemeName is { } themeName)
            _themeScope.RequestedThemeVariant = DaisyThemeManager.IsDarkTheme(themeName) ? ThemeVariant.Dark : ThemeVariant.Light;
        _panel.Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
        _panel.BorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
        _resizeMark.Background = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
        Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
        // The theme of the scope sets the system text color, which would hide the Daisy text color inherited from here.
        _themeScope.SetValue(TextElement.ForegroundProperty, Foreground);
        var sectionBorder = DaisyResourceLookup.GetBrush("DaisyAccentBrush");
        _scopeSection.BorderBrush = sectionBorder;
        foreach (var section in _fieldsPanel.Children.OfType<DaisyExpander>())
            section.BorderBrush = sectionBorder;
    }

    private sealed class ResizeGrip : Grid
    {
        public ResizeGrip() => Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }
    private void VerifyOpen()
    {
        VerifyAccess();
        if (!IsOpen) throw new InvalidOperationException("The tour editor is not open.");
    }

    private void ValidateProperty(string name)
    {
        if (DaisyTourProfileResolver.FindProperty(name) is null || !IsPropertyAvailable(name))
            throw new ArgumentException("The property is not supported at the selected scope.", nameof(name));
    }
}
