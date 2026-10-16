using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Controls
{
    /// <summary>
    /// Controls when the resize grip of a <see cref="DaisyResizableColumnsHost"/> appears.
    /// </summary>
    public enum DaisyGripVisibility
    {
        /// <summary>The grip appears when the mouse is near a gap between columns.</summary>
        Auto,
        /// <summary>Same as <see cref="Auto"/>; reserved for an always-visible grip.</summary>
        Visible,
        /// <summary>No grip; columns can still be resized with the keyboard.</summary>
        Hidden
    }

    /// <summary>
    /// A multi-column host with a shared resize grip between columns. Dragging any grip changes
    /// <see cref="ColumnWidth"/>, which all columns share, within <see cref="MinColumnWidth"/> and
    /// <see cref="MaxColumnWidth"/>.
    /// </summary>
    /// <remarks>
    /// Bind the width of each column (the item or the root of the <c>ItemTemplate</c>) to
    /// <see cref="ColumnWidth"/>. With keyboard focus, the arrow keys, Page Up/Down, Home and End resize the columns.
    /// </remarks>
    [TemplatePart(PART_GripCanvas, typeof(Canvas))]
    [TemplatePart(PART_GripVisual, typeof(Control))]
    [TemplatePart(PART_GripThumb, typeof(Thumb))]
    public class DaisyResizableColumnsHost : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyResizableColumnsHost);

        private const string PART_GripCanvas = "PART_GripCanvas";
        private const string PART_GripVisual = "PART_GripVisual";
        private const string PART_GripThumb = "PART_GripThumb";
        private const double KeyboardStep = 8.0;
        private const double LargeKeyboardStep = 32.0;
        private const double HoverThreshold = 20.0;
        private const double GripSize = 12.0;
        private const double ThumbSize = 24.0;
        private const string DefaultAccessibleText = "Resizable columns";

        private Canvas? _gripCanvas;
        private Control? _gripVisual;
        private Thumb? _gripThumb;
        private DispatcherTimer? _hoverUpdateTimer;
        private int _currentGripGapIndex = -1;
        private bool _isDragging;
        private bool _isPointerHovering;
        private Point _lastPointerPosition;
        private Point _lastAppliedPointerPosition;
        private bool _hasAppliedPointerPosition;

        /// <summary>
        /// Defines the <see cref="ColumnWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ColumnWidthProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, double>(
                nameof(ColumnWidth), 250.0, coerce: CoerceColumnWidth);

        /// <summary>
        /// Gets or sets the shared width of all columns, kept between <see cref="MinColumnWidth"/> and <see cref="MaxColumnWidth"/>.
        /// </summary>
        public double ColumnWidth
        {
            get => GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MinColumnWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> MinColumnWidthProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, double>(nameof(MinColumnWidth), 100.0);

        /// <summary>
        /// Gets or sets the smallest column width.
        /// </summary>
        public double MinColumnWidth
        {
            get => GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MaxColumnWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> MaxColumnWidthProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, double>(nameof(MaxColumnWidth), 1000.0);

        /// <summary>
        /// Gets or sets the largest column width.
        /// </summary>
        public double MaxColumnWidth
        {
            get => GetValue(MaxColumnWidthProperty);
            set => SetValue(MaxColumnWidthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ColumnSpacing"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ColumnSpacingProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, double>(nameof(ColumnSpacing));

        /// <summary>
        /// Gets or sets the gap between columns. The grip sits in the middle of the gap.
        /// </summary>
        public double ColumnSpacing
        {
            get => GetValue(ColumnSpacingProperty);
            set => SetValue(ColumnSpacingProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GripVisibility"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyGripVisibility> GripVisibilityProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, DaisyGripVisibility>(nameof(GripVisibility), DaisyGripVisibility.Auto);

        /// <summary>
        /// Gets or sets when the resize grip appears.
        /// </summary>
        public DaisyGripVisibility GripVisibility
        {
            get => GetValue(GripVisibilityProperty);
            set => SetValue(GripVisibilityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsGripEnabled"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsGripEnabledProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, bool>(nameof(IsGripEnabled), true);

        /// <summary>
        /// Gets or sets whether the columns can be resized, with the grip or the keyboard.
        /// </summary>
        public bool IsGripEnabled
        {
            get => GetValue(IsGripEnabledProperty);
            set => SetValue(IsGripEnabledProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Orientation"/> property.
        /// </summary>
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, Orientation>(nameof(Orientation), Orientation.Horizontal);

        /// <summary>
        /// Gets or sets the direction in which the columns are stacked.
        /// </summary>
        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="AccessibleText"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> AccessibleTextProperty =
            AvaloniaProperty.Register<DaisyResizableColumnsHost, string?>(nameof(AccessibleText));

        /// <summary>
        /// Gets or sets the name announced by screen readers. Defaults to "Resizable columns".
        /// </summary>
        public string? AccessibleText
        {
            get => GetValue(AccessibleTextProperty);
            set => SetValue(AccessibleTextProperty, value);
        }

        /// <summary>
        /// Occurs when <see cref="ColumnWidth"/> changes. The argument is the new width.
        /// </summary>
        public event EventHandler<double>? ColumnWidthChanged;

        /// <summary>
        /// Occurs when the user starts dragging the resize grip.
        /// </summary>
        public event EventHandler? ResizeDragStarted;

        /// <summary>
        /// Occurs when the user stops dragging the resize grip.
        /// </summary>
        public event EventHandler? ResizeDragCompleted;

        public DaisyResizableColumnsHost()
        {
            UpdateInteractionState();
            UpdateAutomationName();
        }

        private static double CoerceColumnWidth(AvaloniaObject sender, double value)
        {
            var host = (DaisyResizableColumnsHost)sender;
            var min = host.MinColumnWidth;
            var max = Math.Max(min, host.MaxColumnWidth);
            return double.IsNaN(value) ? min : Math.Clamp(value, min, max);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            if (_gripThumb != null)
            {
                _gripThumb.DragStarted -= OnGripDragStarted;
                _gripThumb.DragDelta -= OnGripDragDelta;
                _gripThumb.DragCompleted -= OnGripDragCompleted;
            }

            base.OnApplyTemplate(e);

            _gripCanvas = e.NameScope.Find<Canvas>(PART_GripCanvas);
            _gripVisual = e.NameScope.Find<Control>(PART_GripVisual);
            _gripThumb = e.NameScope.Find<Thumb>(PART_GripThumb);

            if (_gripThumb != null)
            {
                _gripThumb.DragStarted += OnGripDragStarted;
                _gripThumb.DragDelta += OnGripDragDelta;
                _gripThumb.DragCompleted += OnGripDragCompleted;
            }

            HideGrip();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ColumnWidthProperty)
            {
                UpdateGripPosition();
                ColumnWidthChanged?.Invoke(this, change.GetNewValue<double>());
            }
            else if (change.Property == MinColumnWidthProperty || change.Property == MaxColumnWidthProperty)
            {
                CoerceValue(ColumnWidthProperty);
            }
            else if (change.Property == ColumnSpacingProperty || change.Property == OrientationProperty)
            {
                HideGrip();
            }
            else if (change.Property == GripVisibilityProperty || change.Property == ItemCountProperty)
            {
                if (!CanShowGrip)
                    HideGrip();
            }
            else if (change.Property == IsGripEnabledProperty || change.Property == IsEnabledProperty)
            {
                UpdateInteractionState();
                if (!CanShowGrip)
                    HideGrip();
            }
            else if (change.Property == AccessibleTextProperty)
            {
                UpdateAutomationName();
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateGripPosition();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _hoverUpdateTimer?.Stop();
            HideGrip();
            base.OnDetachedFromVisualTree(e);
        }

        private bool CanShowGrip =>
            IsEnabled && IsGripEnabled && GripVisibility != DaisyGripVisibility.Hidden && ItemCount >= 2;

        #region Keyboard

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled || !IsEnabled || !IsGripEnabled)
                return;

            var isHorizontal = Orientation == Orientation.Horizontal;
            var handled = e.Key switch
            {
                Key.Left when isHorizontal => AdjustColumnWidth(-KeyboardStep),
                Key.Right when isHorizontal => AdjustColumnWidth(KeyboardStep),
                Key.Up when !isHorizontal => AdjustColumnWidth(-KeyboardStep),
                Key.Down when !isHorizontal => AdjustColumnWidth(KeyboardStep),
                Key.PageUp => AdjustColumnWidth(LargeKeyboardStep),
                Key.PageDown => AdjustColumnWidth(-LargeKeyboardStep),
                Key.Home => SetColumnWidth(MinColumnWidth),
                Key.End => SetColumnWidth(MaxColumnWidth),
                _ => false
            };

            if (handled)
                e.Handled = true;
        }

        private bool AdjustColumnWidth(double delta) => SetColumnWidth(ColumnWidth + delta);

        private bool SetColumnWidth(double value)
        {
            var before = ColumnWidth;
            SetCurrentValue(ColumnWidthProperty, value);
            return Math.Abs(ColumnWidth - before) > double.Epsilon;
        }

        #endregion

        #region Hover tracking

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDragging || !CanShowGrip || e.Pointer.Type != PointerType.Mouse)
                return;

            _isPointerHovering = true;
            _lastPointerPosition = e.GetPosition(this);
            ScheduleHoverUpdate();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            _isPointerHovering = false;
            _hoverUpdateTimer?.Stop();
            if (!_isDragging)
                HideGrip();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            // Clicking the grip gives the host keyboard focus, so the arrow keys continue the resize.
            if (e.Source is Visual source && _gripThumb != null && (ReferenceEquals(source, _gripThumb) || _gripThumb.IsVisualAncestorOf(source)))
                Focus(NavigationMethod.Pointer);
        }

        private void ScheduleHoverUpdate()
        {
            if (_hoverUpdateTimer == null)
            {
                _hoverUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                _hoverUpdateTimer.Tick += OnHoverUpdateTimerTick;
            }

            _hoverUpdateTimer.Stop();
            _hoverUpdateTimer.Start();
        }

        private void OnHoverUpdateTimerTick(object? sender, EventArgs e)
        {
            _hoverUpdateTimer?.Stop();
            ApplyHoverUpdate();
        }

        private void ApplyHoverUpdate()
        {
            if (!_isPointerHovering || _isDragging || !CanShowGrip)
            {
                HideGrip();
                return;
            }

            if (_hasAppliedPointerPosition)
            {
                var delta = _lastPointerPosition - _lastAppliedPointerPosition;
                if ((delta.X * delta.X) + (delta.Y * delta.Y) < 4)
                    return;
            }

            _lastAppliedPointerPosition = _lastPointerPosition;
            _hasAppliedPointerPosition = true;
            UpdateHoverGrip(_lastPointerPosition);
        }

        private void UpdateHoverGrip(Point position)
        {
            var nearestDistance = double.MaxValue;
            var nearestGap = -1;
            var nearestGapCenter = 0.0;

            for (var i = 0; i < ItemCount - 1; i++)
            {
                if (GetGapCenter(i) is not { } gapCenter)
                    continue;

                var distance = Orientation == Orientation.Horizontal
                    ? Math.Abs(position.X - gapCenter)
                    : Math.Abs(position.Y - gapCenter);

                if (distance < HoverThreshold && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestGap = i;
                    nearestGapCenter = gapCenter;
                }
            }

            if (nearestGap >= 0)
                ShowGrip(nearestGap, nearestGapCenter);
            else
                HideGrip();
        }

        private double? GetGapCenter(int gapIndex)
        {
            if (ContainerFromIndex(gapIndex) is not { } container || container.Bounds.Width <= 0)
                return null;

            if (container.TranslatePoint(default, this) is not { } origin)
                return null;

            return Orientation == Orientation.Horizontal
                ? origin.X + container.Bounds.Width + (ColumnSpacing / 2)
                : origin.Y + container.Bounds.Height + (ColumnSpacing / 2);
        }

        #endregion

        #region Grip

        private void ShowGrip(int gapIndex, double position)
        {
            if (_gripCanvas == null || _gripVisual == null || _gripThumb == null)
                return;

            _currentGripGapIndex = gapIndex;
            _gripVisual.Opacity = 1;
            _gripCanvas.IsHitTestVisible = true;

            if (Orientation == Orientation.Horizontal)
            {
                PlaceGripPart(_gripVisual, position - (GripSize / 2), 0, GripSize, Bounds.Height);
                PlaceGripPart(_gripThumb, position - (ThumbSize / 2), 0, ThumbSize, Bounds.Height);
            }
            else
            {
                PlaceGripPart(_gripVisual, 0, position - (GripSize / 2), Bounds.Width, GripSize);
                PlaceGripPart(_gripThumb, 0, position - (ThumbSize / 2), Bounds.Width, ThumbSize);
            }
        }

        private static void PlaceGripPart(Control part, double left, double top, double width, double height)
        {
            Canvas.SetLeft(part, left);
            Canvas.SetTop(part, top);
            part.Width = width;
            part.Height = height;
        }

        private void HideGrip()
        {
            if (_gripVisual != null)
                _gripVisual.Opacity = 0;
            if (_gripCanvas != null)
                _gripCanvas.IsHitTestVisible = false;

            _currentGripGapIndex = -1;
            _hasAppliedPointerPosition = false;
        }

        private void UpdateGripPosition()
        {
            if (_isDragging || _currentGripGapIndex < 0)
                return;

            if (_currentGripGapIndex >= ItemCount - 1 || GetGapCenter(_currentGripGapIndex) is not { } gapCenter)
            {
                HideGrip();
                return;
            }

            ShowGrip(_currentGripGapIndex, gapCenter);
        }

        private void OnGripDragStarted(object? sender, VectorEventArgs e)
        {
            _isDragging = true;
            ResizeDragStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnGripDragDelta(object? sender, VectorEventArgs e)
        {
            if (_currentGripGapIndex < 0 || ItemCount < 2)
                return;

            // The grip after column n moves by the combined growth of the n + 1 columns before it,
            // so each column grows by a 1 / (n + 1) share of the drag distance.
            var change = Orientation == Orientation.Horizontal ? e.Vector.X : e.Vector.Y;
            var columnsBefore = _currentGripGapIndex + 1;
            var before = ColumnWidth;

            SetCurrentValue(ColumnWidthProperty, before + (change / columnsBefore));

            var appliedChange = (ColumnWidth - before) * columnsBefore;
            MoveGripBy(appliedChange);
        }

        private void OnGripDragCompleted(object? sender, VectorEventArgs e)
        {
            _isDragging = false;
            ResizeDragCompleted?.Invoke(this, EventArgs.Empty);
            UpdateGripPosition();
        }

        private void MoveGripBy(double change)
        {
            if (_gripVisual == null || _gripThumb == null || change == 0)
                return;

            if (Orientation == Orientation.Horizontal)
            {
                Canvas.SetLeft(_gripVisual, GetCanvasOffset(Canvas.GetLeft(_gripVisual)) + change);
                Canvas.SetLeft(_gripThumb, GetCanvasOffset(Canvas.GetLeft(_gripThumb)) + change);
            }
            else
            {
                Canvas.SetTop(_gripVisual, GetCanvasOffset(Canvas.GetTop(_gripVisual)) + change);
                Canvas.SetTop(_gripThumb, GetCanvasOffset(Canvas.GetTop(_gripThumb)) + change);
            }
        }

        private static double GetCanvasOffset(double value) => double.IsNaN(value) ? 0 : value;

        #endregion

        private void UpdateInteractionState()
        {
            Focusable = IsEnabled && IsGripEnabled;
        }

        private void UpdateAutomationName()
        {
            AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(AccessibleText) ? DefaultAccessibleText : AccessibleText);
        }
    }
}
