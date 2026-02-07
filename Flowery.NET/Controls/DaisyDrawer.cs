using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Flowery.Enums;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A drawer/sidebar control styled after DaisyUI's Drawer component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDrawer : SplitView, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDrawer);

        private const double BaseTextFontSize = 14.0;
        private readonly DaisyControlLifecycle _lifecycle;
        private Control? _paneRoot;
        private Control? _overlay;
        private Point _dragStart;
        private IPointer? _activePointer;
        private bool _pendingEdgeSwipe;
        private bool _pendingPaneSwipe;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public DaisyDrawer()
        {
            // Defaults for DaisyUI Drawer
            DisplayMode = SplitViewDisplayMode.Inline;
            PanePlacement = SplitViewPanePlacement.Left;
            OpenPaneLength = 300; // Typical drawer width
            CompactPaneLength = 0;

            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyTheme,
                () => DaisySize.Medium,
                _ => { },
                handleLifecycleEvents: false,
                subscribeSizeChanges: false);

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;

            AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerMovedEvent, OnRootPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnRootPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerCaptureLostEvent, OnRootPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        /// <summary>
        /// Gets or sets the width of the drawer pane when open.
        /// </summary>
        public static readonly StyledProperty<double> DrawerWidthProperty =
            AvaloniaProperty.Register<DaisyDrawer, double>(nameof(DrawerWidth), 300.0);

        public double DrawerWidth
        {
            get => GetValue(DrawerWidthProperty);
            set => SetValue(DrawerWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets which side the drawer appears on.
        /// </summary>
        public static readonly StyledProperty<SplitViewPanePlacement> DrawerSideProperty =
            AvaloniaProperty.Register<DaisyDrawer, SplitViewPanePlacement>(nameof(DrawerSide), SplitViewPanePlacement.Left);

        public SplitViewPanePlacement DrawerSide
        {
            get => GetValue(DrawerSideProperty);
            set => SetValue(DrawerSideProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the drawer is currently open.
        /// </summary>
        public static readonly StyledProperty<bool> IsDrawerOpenProperty =
            AvaloniaProperty.Register<DaisyDrawer, bool>(nameof(IsDrawerOpen), false);

        public bool IsDrawerOpen
        {
            get => GetValue(IsDrawerOpenProperty);
            set => SetValue(IsDrawerOpenProperty, value);
        }

        /// <summary>
        /// When true, the drawer overlays content instead of pushing it aside.
        /// </summary>
        public static readonly StyledProperty<bool> OverlayModeProperty =
            AvaloniaProperty.Register<DaisyDrawer, bool>(nameof(OverlayMode), false);

        public bool OverlayMode
        {
            get => GetValue(OverlayModeProperty);
            set => SetValue(OverlayModeProperty, value);
        }

        /// <summary>
        /// When true, the drawer automatically opens/closes based on available width.
        /// </summary>
        public static readonly StyledProperty<bool> ResponsiveModeProperty =
            AvaloniaProperty.Register<DaisyDrawer, bool>(nameof(ResponsiveMode), false);

        public bool ResponsiveMode
        {
            get => GetValue(ResponsiveModeProperty);
            set => SetValue(ResponsiveModeProperty, value);
        }

        /// <summary>
        /// The width threshold for responsive mode. Drawer opens when width >= threshold.
        /// </summary>
        public static readonly StyledProperty<double> ResponsiveThresholdProperty =
            AvaloniaProperty.Register<DaisyDrawer, double>(nameof(ResponsiveThreshold), 500.0);

        public double ResponsiveThreshold
        {
            get => GetValue(ResponsiveThresholdProperty);
            set => SetValue(ResponsiveThresholdProperty, value);
        }

        /// <summary>
        /// Gets or sets the drag distance required to trigger swipe open/close.
        /// </summary>
        public static readonly StyledProperty<double> SwipeThresholdProperty =
            AvaloniaProperty.Register<DaisyDrawer, double>(nameof(SwipeThreshold), 48.0);

        public double SwipeThreshold
        {
            get => GetValue(SwipeThresholdProperty);
            set => SetValue(SwipeThresholdProperty, value);
        }

        /// <summary>
        /// Gets or sets the edge size (in pixels) that can start a swipe open gesture.
        /// </summary>
        public static readonly StyledProperty<double> EdgeSwipeZoneProperty =
            AvaloniaProperty.Register<DaisyDrawer, double>(nameof(EdgeSwipeZone), 24.0);

        public double EdgeSwipeZone
        {
            get => GetValue(EdgeSwipeZoneProperty);
            set => SetValue(EdgeSwipeZoneProperty, value);
        }

        /// <summary>
        /// Opens the drawer.
        /// </summary>
        public void Open()
        {
            SetCurrentValue(IsDrawerOpenProperty, true);
        }

        /// <summary>
        /// Closes the drawer.
        /// </summary>
        public void Close()
        {
            SetCurrentValue(IsDrawerOpenProperty, false);
        }

        /// <summary>
        /// Toggles the drawer open/closed state.
        /// </summary>
        public void Toggle()
        {
            SetCurrentValue(IsDrawerOpenProperty, !IsDrawerOpen);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DrawerWidthProperty && change.NewValue is double width)
            {
                if (!OpenPaneLength.Equals(width))
                {
                    SetCurrentValue(OpenPaneLengthProperty, width);
                }
            }
            else if (change.Property == OpenPaneLengthProperty && change.NewValue is double openPaneLength)
            {
                if (!DrawerWidth.Equals(openPaneLength))
                {
                    SetCurrentValue(DrawerWidthProperty, openPaneLength);
                }
            }
            else if (change.Property == DrawerSideProperty && change.NewValue is SplitViewPanePlacement placement)
            {
                if (PanePlacement != placement)
                {
                    SetCurrentValue(PanePlacementProperty, placement);
                }
            }
            else if (change.Property == PanePlacementProperty && change.NewValue is SplitViewPanePlacement panePlacement)
            {
                if (DrawerSide != panePlacement)
                {
                    SetCurrentValue(DrawerSideProperty, panePlacement);
                }
            }
            else if (change.Property == IsDrawerOpenProperty && change.NewValue is bool isOpen)
            {
                if (IsPaneOpen != isOpen)
                {
                    SetCurrentValue(IsPaneOpenProperty, isOpen);
                }
            }
            else if (change.Property == IsPaneOpenProperty && change.NewValue is bool paneOpen)
            {
                if (IsDrawerOpen != paneOpen)
                {
                    SetCurrentValue(IsDrawerOpenProperty, paneOpen);
                }
            }
            else if (change.Property == OverlayModeProperty && change.NewValue is bool overlay)
            {
                var targetMode = overlay ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
                if (DisplayMode != targetMode)
                {
                    SetCurrentValue(DisplayModeProperty, targetMode);
                }

                SetCurrentValue(IsPaneOpenProperty, IsDrawerOpen);
            }
            else if (change.Property == DisplayModeProperty && change.NewValue is SplitViewDisplayMode displayMode)
            {
                var isOverlay = displayMode == SplitViewDisplayMode.Overlay;
                if (OverlayMode != isOverlay)
                {
                    SetCurrentValue(OverlayModeProperty, isOverlay);
                }
            }
            else if (change.Property == ResponsiveModeProperty && change.NewValue is bool responsive && responsive)
            {
                UpdateResponsiveState(Bounds.Width);
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            DetachSwipeHandlers();

            _paneRoot = e.NameScope.Find<Control>("PART_PaneRoot");
            _overlay = e.NameScope.Find<Control>("Overlay");

            AttachSwipeHandlers();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            if (ResponsiveMode)
            {
                UpdateResponsiveState(e.NewSize.Width);
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _lifecycle.HandleLoaded();

            if (IsSet(DrawerWidthProperty))
            {
                SetCurrentValue(OpenPaneLengthProperty, DrawerWidth);
            }
            else if (IsSet(OpenPaneLengthProperty))
            {
                SetCurrentValue(DrawerWidthProperty, OpenPaneLength);
            }

            if (IsSet(DrawerSideProperty))
            {
                SetCurrentValue(PanePlacementProperty, DrawerSide);
            }
            else if (IsSet(PanePlacementProperty))
            {
                SetCurrentValue(DrawerSideProperty, PanePlacement);
            }

            if (IsSet(OverlayModeProperty))
            {
                SetCurrentValue(DisplayModeProperty, OverlayMode ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline);
            }
            else if (IsSet(DisplayModeProperty))
            {
                SetCurrentValue(OverlayModeProperty, DisplayMode == SplitViewDisplayMode.Overlay);
            }

            if (ResponsiveMode)
            {
                UpdateResponsiveState(Bounds.Width);
            }
            else if (IsSet(IsDrawerOpenProperty))
            {
                SetCurrentValue(IsPaneOpenProperty, IsDrawerOpen);
            }
            else
            {
                SetCurrentValue(IsDrawerOpenProperty, IsPaneOpen);
            }
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _lifecycle.HandleUnloaded();
            ResetSwipeState();
        }

        private void UpdateResponsiveState(double width)
        {
            var shouldBeOpen = width >= ResponsiveThreshold;

            if (IsDrawerOpen != shouldBeOpen)
            {
                SetCurrentValue(IsDrawerOpenProperty, shouldBeOpen);
            }

            if (IsPaneOpen != shouldBeOpen)
            {
                SetCurrentValue(IsPaneOpenProperty, shouldBeOpen);
            }
        }

        private void AttachSwipeHandlers()
        {
            if (_paneRoot != null)
            {
                _paneRoot.AddHandler(PointerPressedEvent, OnPanePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
                _paneRoot.AddHandler(PointerMovedEvent, OnPanePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
                _paneRoot.AddHandler(PointerReleasedEvent, OnPanePointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
                _paneRoot.AddHandler(PointerCaptureLostEvent, OnPanePointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
            }

            if (_overlay != null)
            {
                _overlay.AddHandler(PointerPressedEvent, OnOverlayPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }

        private void DetachSwipeHandlers()
        {
            if (_paneRoot != null)
            {
                _paneRoot.RemoveHandler(PointerPressedEvent, OnPanePointerPressed);
                _paneRoot.RemoveHandler(PointerMovedEvent, OnPanePointerMoved);
                _paneRoot.RemoveHandler(PointerReleasedEvent, OnPanePointerReleased);
                _paneRoot.RemoveHandler(PointerCaptureLostEvent, OnPanePointerCaptureLost);
            }

            if (_overlay != null)
            {
                _overlay.RemoveHandler(PointerPressedEvent, OnOverlayPointerPressed);
            }
        }

        private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DisplayMode is SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (IsPaneOpen || _pendingPaneSwipe || _pendingEdgeSwipe)
            {
                return;
            }

            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (!IsWithinEdgeSwipeZone(point.Position))
            {
                return;
            }

            _activePointer = e.Pointer;
            _dragStart = point.Position;
            _pendingEdgeSwipe = true;
        }

        private void OnRootPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_pendingEdgeSwipe || !ReferenceEquals(_activePointer, e.Pointer))
            {
                return;
            }

            var delta = e.GetPosition(this) - _dragStart;
            if (IsSwipeInDirection(delta, opening: true))
            {
                Open();
                ResetSwipeState();
            }
        }

        private void OnRootPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (ReferenceEquals(_activePointer, e.Pointer))
            {
                ResetSwipeState();
            }
        }

        private void OnRootPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            ResetSwipeState();
        }

        private void OnPanePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsPaneOpen || _pendingPaneSwipe || _pendingEdgeSwipe)
            {
                return;
            }

            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            _activePointer = e.Pointer;
            _dragStart = point.Position;
            _pendingPaneSwipe = true;
        }

        private void OnPanePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_pendingPaneSwipe || !ReferenceEquals(_activePointer, e.Pointer))
            {
                return;
            }

            var delta = e.GetPosition(this) - _dragStart;
            if (IsSwipeInDirection(delta, opening: false))
            {
                Close();
                ResetSwipeState();
            }
        }

        private void OnPanePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (ReferenceEquals(_activePointer, e.Pointer))
            {
                ResetSwipeState();
            }
        }

        private void OnPanePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            ResetSwipeState();
        }

        private void ResetSwipeState()
        {
            _activePointer = null;
            _pendingEdgeSwipe = false;
            _pendingPaneSwipe = false;
        }

        private bool IsWithinEdgeSwipeZone(Point position)
        {
            var edgeSwipeZone = Math.Max(0, EdgeSwipeZone);
            return PanePlacement switch
            {
                SplitViewPanePlacement.Left => position.X <= edgeSwipeZone,
                SplitViewPanePlacement.Right => position.X >= Math.Max(0, Bounds.Width - edgeSwipeZone),
                SplitViewPanePlacement.Top => position.Y <= edgeSwipeZone,
                SplitViewPanePlacement.Bottom => position.Y >= Math.Max(0, Bounds.Height - edgeSwipeZone),
                _ => false
            };
        }

        private bool IsSwipeInDirection(Vector delta, bool opening)
        {
            var isHorizontal = PanePlacement is SplitViewPanePlacement.Left or SplitViewPanePlacement.Right;
            var swipeThreshold = Math.Max(0, SwipeThreshold);

            if (isHorizontal)
            {
                if (Math.Abs(delta.X) <= swipeThreshold || Math.Abs(delta.X) <= Math.Abs(delta.Y))
                {
                    return false;
                }

                return PanePlacement == SplitViewPanePlacement.Left
                    ? opening ? delta.X > 0 : delta.X < 0
                    : opening ? delta.X < 0 : delta.X > 0;
            }

            if (Math.Abs(delta.Y) <= swipeThreshold || Math.Abs(delta.Y) <= Math.Abs(delta.X))
            {
                return false;
            }

            return PanePlacement == SplitViewPanePlacement.Top
                ? opening ? delta.Y > 0 : delta.Y < 0
                : opening ? delta.Y < 0 : delta.Y > 0;
        }

        private void ApplyTheme()
        {
            if (PaneBackground != null)
            {
                return;
            }

            if (TryGetResource("DaisyDrawerBackground", ThemeVariant.Default, out var value) && value is IBrush brush)
            {
                PaneBackground = brush;
                return;
            }

            if (TryGetResource("DaisyBase200Brush", ThemeVariant.Default, out var fallback) && fallback is IBrush fallbackBrush)
            {
                PaneBackground = fallbackBrush;
            }
        }
    }
}
