using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Flowery.Controls;
using Flowery.Theming;
using Flowery.Localization;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// A column container for the Kanban board that supports drop operations.
    /// </summary>
    public partial class FlowKanbanColumn : FlowKanbanContentControl
    {
        static FlowKanbanColumn()
        {
            LaneFilterIdProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnLaneFilterIdChanged);
            ShowColumnHeaderProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnShowColumnHeaderChanged);
            ShowAddCardProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnShowAddCardChanged);
            AddCardPlacementProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnAddCardPlacementChanged);
            IsCollapsedProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnIsCollapsedChanged);
            IsDropEnabledProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnIsDropEnabledChanged);
            ColumnDataProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnColumnDataChanged);
            ParentKanbanProperty.Changed.AddClassHandler<FlowKanbanColumn>(OnParentKanbanChanged);
            ColumnSizeProperty.Changed.AddClassHandler<FlowKanbanColumn>((control, _) => control.ApplySizing());
        }

        private ItemsControl? _tasksItemsControl;
        private ListBox? _tasksListView;
        private Panel? _tasksItemsHost;
        private Canvas? _tasksDropIndicatorHost;
        private Rectangle? _dropIndicator;
        private int _currentDropIndex = -1;
        private double _currentDropIndicatorY = double.NaN;
        private double _taskItemSpacing = double.NaN;
        private Style? _tasksItemContainerStyle;
        private string? _draggedTaskId;
        private const int TaskViewStaggerBatchSize = 5;
        private readonly ObservableCollection<FlowKanbanTaskView> _taskViews = new();
        private readonly HashSet<FlowTask> _trackedViewTasks = new();
        private readonly Dictionary<FlowKanbanTaskView, FlowTaskCard> _realizedCards = new();
        private int _taskViewBuildVersion;
        private bool _taskViewBuildPending;
        private TaskViewBuildState? _taskViewBuildState;
        private ObservableCollection<FlowTask>? _trackedTasksCollection;
        private FlowKanbanColumnData? _trackedColumnData;
        private FlowTask? _lastSelectedTask;
        private bool _isLoaded;

        // Child elements for sizing and hover
        private TextBlock? _headerText;
        private DaisyButton? _editButton;
        private DaisyButton? _collapseButton;
        private FlowKanbanAddCard? _addCardTop;
        private FlowKanbanAddCard? _addCardBottom;
        private Control? _columnGripElement;
        private Grid? _columnHeaderGrid;
        private Control? _headerIconsPanel;
        private Border? _taskCountBadge;
        private TextBlock? _taskCountText;
        private DaisyIconText? _collapseIcon;
        private bool? _baseShowTasks;
        private bool? _baseShowAddCard;
        private bool _isKeyboardFocusVisible;
        private bool _isDragHighlightActive;

        // Drag highlight border: reserve 2px always (transparent by default),
        // and show a green outline during drag operations.
        private static readonly Thickness s_dragHighlightBorderThickness = new(2);
        private static readonly SolidColorBrush s_dragHighlightTransparentBrush = new(Colors.Transparent);
        private static readonly SolidColorBrush s_dragHighlightFallbackBrush = new(Colors.LimeGreen);

        private static IBrush GetDragHighlightBrush()
        {
            return DaisyResourceLookup.GetBrush("DaisySuccessBrush") ?? s_dragHighlightFallbackBrush;
        }

        private void SetDragHighlightBorder(bool isActive)
        {
            BorderThickness = s_dragHighlightBorderThickness;
            BorderBrush = isActive ? GetDragHighlightBrush() : s_dragHighlightTransparentBrush;

            // Ensure the focus VisualState doesn't override the drag highlight brush.
            if (isActive)
            {
                _isKeyboardFocusVisible = false;
                Classes.Set("keyboard-focus", false);
            }
        }

        public ObservableCollection<FlowKanbanTaskView> TaskViews => _taskViews;

        private bool IsEffectiveCollapsed => IsCollapsed || ColumnData?.IsCollapsed == true;

        internal bool IsCollapseAutomationAvailable =>
            ShowColumnHeader && ParentKanban?.IsCompactLayoutEnabled != true;

        public FlowKanbanColumn()
        {
            Focusable = true;
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble, true);
            AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble, true);
            AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, RoutingStrategies.Bubble, true);
            Loaded += OnColumnLoaded;
            Unloaded += OnColumnUnloaded;
            GotFocus += OnColumnGotFocus;
            LostFocus += OnColumnLostFocus;
            DoubleTapped += OnColumnDoubleTapped;

            SetDragHighlightBorder(isActive: false);
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            IsCollapseAutomationAvailable
                ? new FlowKanbanExpandableColumnAutomationPeer(this)
                : new FlowKanbanColumnAutomationPeer(this);

        private static bool IsUnassignedLaneId(string? laneId) => FlowKanban.IsUnassignedLaneId(laneId);

        protected override void OnThemeChanged(string themeName)
        {
            base.OnThemeChanged(themeName);
            // Refresh background and border to pick up new theme colors
            Background = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            ApplyTaskCountBadgeTheme();
            if (_dropIndicator != null)
            {
                _dropIndicator.Fill = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush");
            }

            SetDragHighlightBorder(_isDragHighlightActive);
        }

        private void OnColumnLoaded(object? sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            SetDragHighlightBorder(isActive: false);
            var searchRoot = Content as AvaloniaObject ?? this;

            // Find the ItemsControl that contains the tasks
            _tasksItemsControl = FindTasksItemsControl(searchRoot);
            _tasksListView = _tasksItemsControl as ListBox;
            _tasksItemsHost = _tasksItemsControl?.ItemsPanelRoot as Panel;
            _tasksDropIndicatorHost = FindChild<Canvas>(searchRoot, c => c.Name == "PART_TaskDropIndicatorHost");
            _realizedCards.Clear();
            if (_tasksListView != null)
            {
                _tasksListView.ContainerPrepared -= OnTaskContainerPrepared;
                _tasksListView.ContainerPrepared += OnTaskContainerPrepared;
                _tasksListView.ContainerClearing -= OnTaskContainerClearing;
                _tasksListView.ContainerClearing += OnTaskContainerClearing;
                DragDrop.SetAllowDrop(_tasksListView, IsDropEnabled);
            }

            if (_tasksItemsControl is Control tasksControl)
            {
                tasksControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            if (_tasksListView != null)
            {
                DragDrop.SetAllowDrop(_tasksListView, IsDropEnabled);
            }

            // Find header and close button for sizing
            FindHeaderElements();
            FindAddCardElements();
            UpdateTabStop();

            // Find parent FlowKanban if not already set
            if (ParentKanban == null)
            {
                var foundParent = FindParentKanban();
                if (foundParent != null)
                {
                    ParentKanban = foundParent;
                }
            }
            if (ParentKanban != null)
            {
                ParentKanban.RegisterColumn(this);
            }

            // Apply initial sizing
            if (ParentKanban != null)
            {
                ColumnSize = ParentKanban.BoardSize;
                ApplySizing();
            }
            else
            {
                // Even without a parent board, apply sizing once so task spacing/styles are initialized.
                ApplySizing();
            }

            AttachColumnData(ColumnData);
            UpdateCollapsedState();
            UpdateEditButtonVisibilityForPlatform();
            UpdateAddCardVisibility();
            UpdateFocusVisualState();
        }

        private void EnsureTasksParts()
        {
            if (_tasksItemsControl != null && _tasksDropIndicatorHost != null)
                return;

            var searchRoot = Content as AvaloniaObject ?? this;
            _tasksItemsControl ??= FindTasksItemsControl(searchRoot);
            _tasksListView = _tasksItemsControl as ListBox;
            _tasksItemsHost = _tasksItemsControl?.ItemsPanelRoot as Panel;
            _tasksDropIndicatorHost ??= FindChild<Canvas>(searchRoot, c => c.Name == "PART_TaskDropIndicatorHost");

            if (_tasksItemsControl is Control tasksControl)
            {
                tasksControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            if (_tasksListView != null && !double.IsNaN(_taskItemSpacing))
            {
                ApplyTaskItemSpacing(_taskItemSpacing);
            }
        }

        private void FindHeaderElements()
        {
            var searchRoot = Content as AvaloniaObject ?? this;

            // Find the column header grid
            _columnHeaderGrid = FindChild<Grid>(searchRoot, g => g.Name == "PART_ColumnHeaderGrid");

            // Find the first TextBlock with FontWeight=Bold (the header)
            _headerText = FindChild<TextBlock>(searchRoot, tb => tb.Name == "PART_ColumnTitle");

            // Find the edit button (PART_RenameBtn)
            _editButton = FindChild<DaisyButton>(searchRoot, btn => btn.Name == "PART_RenameBtn");

            _collapseButton = FindChild<DaisyButton>(searchRoot, btn => btn.Name == "PART_CollapseBtn");
            _collapseIcon = FindChild<DaisyIconText>(searchRoot, icon => icon.Name == "PART_CollapseIcon");

            _columnGripElement = FindChild<Border>(searchRoot, b => b.Name == "PART_ColumnGrip");
            if (_columnGripElement != null)
            {
                _columnGripElement.PointerPressed += OnColumnGripDragPointerPressed;
            }

            _taskCountBadge = FindChild<Border>(searchRoot, b => b.Name == "PART_TaskCountBadge");
            _taskCountText = FindChild<TextBlock>(searchRoot, tb => tb.Name == "PART_TaskCountText");
            _headerIconsPanel = FindChild<Control>(searchRoot, panel => panel.Name == "PART_ColumnHeaderIcons");
            ApplyTaskCountBadgeTheme();
            UpdateTaskCountDisplay();
            UpdateCollapseIcon();

            // Wire up hover events on column header grid
            if (_columnHeaderGrid != null)
            {
                _columnHeaderGrid.PointerEntered += OnHeaderPointerEntered;
                _columnHeaderGrid.PointerExited += OnHeaderPointerExited;
                _columnHeaderGrid.PointerPressed += OnHeaderPointerPressed;
                _columnHeaderGrid.GotFocus += OnHeaderGotFocus;
                _columnHeaderGrid.LostFocus += OnHeaderLostFocus;
            }
        }

        private void FindAddCardElements()
        {
            var searchRoot = Content as AvaloniaObject ?? this;
            _addCardTop = FindChild<FlowKanbanAddCard>(searchRoot, card => card.Name == "PART_AddCardTop");
            _addCardBottom = FindChild<FlowKanbanAddCard>(searchRoot, card => card.Name == "PART_AddCardBottom");
        }

        internal FlowKanbanAddCard? GetVisibleAddCardControl()
        {
            if (_addCardTop == null && _addCardBottom == null)
            {
                FindAddCardElements();
            }

            if (_addCardTop?.IsVisible == true)
                return _addCardTop;

            if (_addCardBottom?.IsVisible == true)
                return _addCardBottom;

            return _addCardTop ?? _addCardBottom;
        }

        private void OnHeaderPointerEntered(object? sender, PointerEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
                ShowHeaderButtons();
        }

        private void OnHeaderPointerExited(object? sender, PointerEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
                HideHeaderButtons();
        }

        private void OnHeaderPointerPressed(object? sender, PointerEventArgs e)
        {
            if (e.Pointer.Type == PointerType.Mouse)
                return;

            ShowHeaderButtons();
            Focus();
        }

        private void OnHeaderGotFocus(object? sender, RoutedEventArgs e)
        {
            ShowHeaderButtons();
        }

        private void OnHeaderLostFocus(object? sender, RoutedEventArgs e)
        {
            HideHeaderButtons();
        }

        private void ShowHeaderButtons()
        {
            if (IsMobilePlatform())
            {
                UpdateEditButtonVisibilityForPlatform();
                return;
            }

            if (_editButton != null)
            {
                _editButton.IsHitTestVisible = true;
                _editButton.Opacity = 0.7;
            }
        }

        private void HideHeaderButtons()
        {
            if (IsMobilePlatform())
                return;

            if (_editButton != null)
            {
                _editButton.Opacity = 0;
                _editButton.IsHitTestVisible = false;
            }
        }

        private async void OnColumnGripDragPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (ColumnData is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var item = DataTransferItem.CreateText(ColumnData.Id);
            item.Set(FlowKanban.ColumnDragFormat, ColumnData.Id);
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }

        private void ApplyTaskCountBadgeTheme()
        {
            if (_taskCountBadge != null)
            {
                _taskCountBadge.Background = DaisyResourceLookup.GetBrush("DaisySecondaryBrush");
            }

            if (_taskCountText != null)
            {
                _taskCountText.Foreground = DaisyResourceLookup.GetBrush("DaisySecondaryContentBrush");
            }
        }

        private void UpdateTaskCountDisplay()
        {
            if (_taskCountText == null || _trackedColumnData == null)
                return;

            var totalCount = _trackedColumnData.Tasks.Count;
            var isFiltering = ParentKanban?.IsFilterActive == true;
            var display = isFiltering
                ? $"{GetFilteredTaskCount(_trackedColumnData.Tasks)}/{totalCount}"
                : _trackedColumnData.WipDisplay;

            if (!string.Equals(_taskCountText.Text, display, StringComparison.Ordinal))
            {
                _taskCountText.Text = display;
            }
        }

        private static int GetFilteredTaskCount(IEnumerable<FlowTask> tasks)
        {
            var count = 0;
            foreach (var task in tasks)
            {
                if (task.IsSearchMatch)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnColumnUnloaded(object? sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            if (_tasksListView != null)
            {
                _tasksListView.ContainerPrepared -= OnTaskContainerPrepared;
                _tasksListView.ContainerClearing -= OnTaskContainerClearing;
            }
            _realizedCards.Clear();
            // Clean up drop indicator
            HideDropIndicator();
            _isDragHighlightActive = false;
            SetDragHighlightBorder(isActive: false);

            // Unsubscribe from parent when unloaded
            if (ParentKanban != null)
            {
                ParentKanban.UnregisterColumn(this);
                ParentKanban.BoardSizeChanged -= OnBoardSizeChanged;
                ParentKanban.ColumnWidthChanged -= OnColumnWidthChanged;
                ParentKanban.DragEnded -= OnParentDragEnded;
                ParentKanban.LaneGroupingChanged -= OnLaneGroupingChanged;
                ParentKanban.CompactLayoutChanged -= OnCompactLayoutChanged;
            }

            if (_columnGripElement != null)
            {
                _columnGripElement.PointerPressed -= OnColumnGripDragPointerPressed;
            }

            if (_columnHeaderGrid != null)
            {
                _columnHeaderGrid.PointerEntered -= OnHeaderPointerEntered;
                _columnHeaderGrid.PointerExited -= OnHeaderPointerExited;
                _columnHeaderGrid.PointerPressed -= OnHeaderPointerPressed;
                _columnHeaderGrid.GotFocus -= OnHeaderGotFocus;
                _columnHeaderGrid.LostFocus -= OnHeaderLostFocus;
            }

            DetachColumnData();
            _tasksItemsControl = null;
            _tasksListView = null;
            _tasksItemsHost = null;
            _tasksDropIndicatorHost = null;
            _addCardTop = null;
            _addCardBottom = null;
        }

        private FlowKanban? FindParentKanban()
        {
            return FlowKanbanVisualTree.FindAncestor<FlowKanban>(this, includeSelf: false);
        }

        private T? FindChild<T>(AvaloniaObject parent) where T : AvaloniaObject
        {
            return FindChild<T>(parent, null);
        }

        private T? FindChild<T>(AvaloniaObject parent, Func<T, bool>? predicate) where T : AvaloniaObject
        {
            return FlowKanbanVisualTree.FindDescendant(parent, predicate);
        }

        private ItemsControl? FindTasksItemsControl(AvaloniaObject parent)
        {
            return FlowKanbanVisualTree.FindDescendant<ItemsControl>(
                parent,
                itemsControl => itemsControl.Name == "PART_TasksItemsControl");
        }

        #region LaneFilterId
        public static readonly StyledProperty<string?> LaneFilterIdProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, string?>(
                                nameof(LaneFilterId),
                                default!);

        public string? LaneFilterId
        {
            get => (string?)GetValue(LaneFilterIdProperty);
            set => SetValue(LaneFilterIdProperty, value);
        }

        private static void OnLaneFilterIdChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                column.RebuildTaskViews();
            }
        }
        #endregion

        #region ShowColumnHeader
        public static readonly StyledProperty<bool> ShowColumnHeaderProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                                nameof(ShowColumnHeader),
                                true);

        public bool ShowColumnHeader
        {
            get => (bool)GetValue(ShowColumnHeaderProperty);
            set => SetValue(ShowColumnHeaderProperty, value);
        }

        private static void OnShowColumnHeaderChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                column.UpdateTabStop();
            }
        }
        #endregion

        #region ShowTasks
        public static readonly StyledProperty<bool> ShowTasksProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                nameof(ShowTasks),
                true);

        public bool ShowTasks
        {
            get => (bool)GetValue(ShowTasksProperty);
            set => SetValue(ShowTasksProperty, value);
        }
        #endregion

        #region TaskListMaxHeight
        public static readonly StyledProperty<double> TaskListMaxHeightProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, double>(
                nameof(TaskListMaxHeight),
                double.PositiveInfinity);

        /// <summary>
        /// Maximum height of the shared task list surface.
        /// </summary>
        public double TaskListMaxHeight
        {
            get => (double)GetValue(TaskListMaxHeightProperty);
            set => SetValue(TaskListMaxHeightProperty, value);
        }
        #endregion


        #region ShowAddCard
        public static readonly StyledProperty<bool> ShowAddCardProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                                nameof(ShowAddCard),
                                true);

        public bool ShowAddCard
        {
            get => (bool)GetValue(ShowAddCardProperty);
            set => SetValue(ShowAddCardProperty, value);
        }

        private static void OnShowAddCardChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                column.UpdateAddCardVisibility();
            }
        }
        #endregion

        #region AddCardPlacement
        public static readonly StyledProperty<FlowKanbanAddCardPlacement> AddCardPlacementProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, FlowKanbanAddCardPlacement>(
                                nameof(AddCardPlacement),
                                FlowKanbanAddCardPlacement.Bottom);

        public FlowKanbanAddCardPlacement AddCardPlacement
        {
            get => (FlowKanbanAddCardPlacement)GetValue(AddCardPlacementProperty);
            set => SetValue(AddCardPlacementProperty, value);
        }

        private static void OnAddCardPlacementChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                column.UpdateAddCardVisibility();
            }
        }
        #endregion

        #region AddCardVisibility
        public static readonly StyledProperty<bool> ShowAddCardTopProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                nameof(ShowAddCardTop),
                false);

        public bool ShowAddCardTop
        {
            get => (bool)GetValue(ShowAddCardTopProperty);
            private set => SetValue(ShowAddCardTopProperty, value);
        }

        public static readonly StyledProperty<bool> ShowAddCardBottomProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                nameof(ShowAddCardBottom),
                true);

        public bool ShowAddCardBottom
        {
            get => (bool)GetValue(ShowAddCardBottomProperty);
            private set => SetValue(ShowAddCardBottomProperty, value);
        }

        private void UpdateAddCardVisibility()
        {
            var placement = AddCardPlacement;
            var show = ShowAddCard;
            var showTop = show && (placement == FlowKanbanAddCardPlacement.Top || placement == FlowKanbanAddCardPlacement.Both);
            var showBottom = show && (placement == FlowKanbanAddCardPlacement.Bottom || placement == FlowKanbanAddCardPlacement.Both);

            ShowAddCardTop = showTop;
            ShowAddCardBottom = showBottom;
        }
        #endregion

        #region IsCollapsed
        public static readonly StyledProperty<bool> IsCollapsedProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                                nameof(IsCollapsed),
                                false);

        public bool IsCollapsed
        {
            get => (bool)GetValue(IsCollapsedProperty);
            set => SetValue(IsCollapsedProperty, value);
        }

        private static void OnIsCollapsedChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                column.UpdateCollapsedState();
            }
        }
        #endregion

        #region IsDropEnabled
        public static readonly StyledProperty<bool> IsDropEnabledProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, bool>(
                                nameof(IsDropEnabled),
                                true);

        public bool IsDropEnabled
        {
            get => (bool)GetValue(IsDropEnabledProperty);
            set => SetValue(IsDropEnabledProperty, value);
        }

        private static void OnIsDropEnabledChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column && e.NewValue is bool isEnabled)
            {
                DragDrop.SetAllowDrop(column, isEnabled);
                if (column._tasksListView != null)
                {
                    DragDrop.SetAllowDrop(column._tasksListView, isEnabled);
                }
                if (!isEnabled)
                {
                    column.HideDropIndicator();
                }
            }
        }
        #endregion

        #region ColumnData
        public static readonly StyledProperty<FlowKanbanColumnData?> ColumnDataProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, FlowKanbanColumnData?>(
                                nameof(ColumnData),
                                default!);

        public FlowKanbanColumnData? ColumnData
        {
            get => (FlowKanbanColumnData?)GetValue(ColumnDataProperty);
            set => SetValue(ColumnDataProperty, value);
        }

        private static void OnColumnDataChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                if (!column._isLoaded)
                    return;

                column.AttachColumnData(e.NewValue as FlowKanbanColumnData);
            }
        }
        #endregion

        #region ParentKanban
        public static readonly StyledProperty<FlowKanban?> ParentKanbanProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, FlowKanban?>(
                                nameof(ParentKanban),
                                default!);

        public FlowKanban? ParentKanban
        {
            get => (FlowKanban?)GetValue(ParentKanbanProperty);
            set => SetValue(ParentKanbanProperty, value);
        }

        private static void OnParentKanbanChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanColumn column)
            {
                // Unsubscribe from old parent
                if (e.OldValue is FlowKanban oldKanban)
                {
                    oldKanban.UnregisterColumn(column);
                    oldKanban.BoardSizeChanged -= column.OnBoardSizeChanged;
                    oldKanban.ColumnWidthChanged -= column.OnColumnWidthChanged;
                    oldKanban.DragEnded -= column.OnParentDragEnded;
                    oldKanban.LaneGroupingChanged -= column.OnLaneGroupingChanged;
                    oldKanban.CompactLayoutChanged -= column.OnCompactLayoutChanged;
                    oldKanban.SearchFilterChanged -= column.OnSearchFilterChanged;
                }

                // Subscribe to new parent
                if (e.NewValue is FlowKanban newKanban)
                {
                    newKanban.BoardSizeChanged += column.OnBoardSizeChanged;
                    newKanban.ColumnWidthChanged += column.OnColumnWidthChanged;
                    newKanban.DragEnded += column.OnParentDragEnded;
                    newKanban.LaneGroupingChanged += column.OnLaneGroupingChanged;
                    newKanban.CompactLayoutChanged += column.OnCompactLayoutChanged;
                    newKanban.SearchFilterChanged += column.OnSearchFilterChanged;
                    column.ColumnSize = newKanban.BoardSize;
                    column.ApplySizing();
                    column.UpdateCollapsedState();
                    column.UpdateColumnReorderState();
                    column.RebuildTaskViews();
                    if (column._isLoaded)
                    {
                        newKanban.RegisterColumn(column);
                    }
                }
            }
        }

        private void OnBoardSizeChanged(object? sender, DaisySize newSize)
        {
            ColumnSize = newSize;
            ApplySizing();
        }

        private void OnColumnWidthChanged(object? sender, double newWidth)
        {
            ApplySizing();
        }

        private void OnParentDragEnded(object? sender, EventArgs e)
        {
            // Ensure drop indicator is cleaned up when any drag ends
            HideDropIndicator();

            _isDragHighlightActive = false;
            SetDragHighlightBorder(isActive: false);
            UpdateFocusVisualState();
        }

        private void OnLaneGroupingChanged(object? sender, EventArgs e)
        {
            RebuildTaskViews();
        }

        private void OnSearchFilterChanged(object? sender, EventArgs e)
        {
            RebuildTaskViews();
            UpdateTaskCountDisplay();
        }

        private void OnCompactLayoutChanged(object? sender, bool isCompact)
        {
            ApplySizing();
            UpdateColumnReorderState();
            UpdateCollapseIcon();
            UpdateCollapsedHeaderVisibility();
        }
        #endregion

        #region ColumnSize
        public static readonly StyledProperty<DaisySize> ColumnSizeProperty =
            AvaloniaProperty.Register<FlowKanbanColumn, DaisySize>(
                                nameof(ColumnSize),
                                DaisySize.Medium);

        /// <summary>
        /// The size tier for this column's visual elements.
        /// </summary>
        public DaisySize ColumnSize
        {
            get => (DaisySize)GetValue(ColumnSizeProperty);
            set => SetValue(ColumnSizeProperty, value);
        }
        #endregion

        private void AttachColumnData(FlowKanbanColumnData? columnData)
        {
            if (ReferenceEquals(_trackedColumnData, columnData))
                return;

            DetachColumnData();
            _trackedColumnData = columnData;

            if (_trackedColumnData == null)
            {
                _taskViews.Clear();
                return;
            }

            _trackedColumnData.PropertyChanged += OnColumnDataPropertyChanged;
            AttachTasksCollection(_trackedColumnData.Tasks);
            RebuildTaskViews();
            UpdateAutomationProperties();
            UpdateTaskCountDisplay();
        }

        private void DetachColumnData()
        {
            if (_trackedColumnData == null)
                return;

            _trackedColumnData.PropertyChanged -= OnColumnDataPropertyChanged;
            DetachTasksCollection();
            _trackedColumnData = null;
            _taskViews.Clear();
            UpdateAutomationProperties();
        }

        private void OnColumnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.Tasks), StringComparison.Ordinal))
            {
                AttachTasksCollection(_trackedColumnData?.Tasks);
                RebuildTaskViews();
                UpdateAutomationProperties();
                UpdateTaskCountDisplay();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.IsCollapsed), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.DisplayTitle), StringComparison.Ordinal))
            {
                UpdateCollapsedState();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.Title), StringComparison.Ordinal))
            {
                UpdateAutomationProperties();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.WipDisplay), StringComparison.Ordinal))
            {
                UpdateTaskCountDisplay();
            }
        }

        private void UpdateAutomationProperties()
        {
            if (_trackedColumnData == null)
            {
                ClearValue(AutomationProperties.NameProperty);
                ClearValue(AutomationProperties.AutomationIdProperty);
                return;
            }

            AutomationProperties.SetName(this, _trackedColumnData.Title);
            AutomationProperties.SetAutomationId(this, $"kanban-column-{_trackedColumnData.Id}");
        }

        private void UpdateTabStop()
        {
            Focusable = ShowColumnHeader;
        }

        private void AttachTasksCollection(ObservableCollection<FlowTask>? tasks)
        {
            if (ReferenceEquals(_trackedTasksCollection, tasks))
                return;

            DetachTasksCollection();
            _trackedTasksCollection = tasks;

            if (_trackedTasksCollection == null)
                return;

            _trackedTasksCollection.CollectionChanged += OnTasksCollectionChanged;

            foreach (var task in _trackedTasksCollection)
            {
                TrackViewTask(task);
            }
        }

        private void DetachTasksCollection()
        {
            if (_trackedTasksCollection != null)
            {
                _trackedTasksCollection.CollectionChanged -= OnTasksCollectionChanged;
            }

            foreach (var task in _trackedViewTasks)
            {
                task.PropertyChanged -= OnTaskPropertyChanged;
            }

            _trackedViewTasks.Clear();
            _trackedTasksCollection = null;
        }

        private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                var tasks = _trackedTasksCollection;
                DetachTasksCollection();
                AttachTasksCollection(tasks);
                RebuildTaskViews();
                UpdateTaskCountDisplay();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (FlowTask task in e.OldItems)
                {
                    UntrackViewTask(task);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowTask task in e.NewItems)
                {
                    TrackViewTask(task);
                }
            }

            if (!TryApplyIncrementalTaskViewChange(e))
            {
                RebuildTaskViews();
            }

            UpdateTaskCountDisplay();
        }

        /// <summary>
        /// Applies a single Add/Remove/Replace/Move to the existing task views
        /// instead of rebuilding them all, so the ListView keeps its realized
        /// containers and a card move doesn't visibly rebuild the whole column.
        /// </summary>
        private bool TryApplyIncrementalTaskViewChange(NotifyCollectionChangedEventArgs e)
        {
            if (!SupportsIncrementalTaskViewUpdates())
                return false;

            // A staggered rebuild is still draining; let the full rebuild win.
            if (_taskViewBuildPending || _taskViewBuildState != null)
                return false;

            if (e.OldItems != null)
            {
                foreach (FlowTask task in e.OldItems)
                {
                    RemoveTaskViewForTask(task);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowTask task in e.NewItems)
                {
                    InsertTaskViewForTask(task);
                }
            }

            return true;
        }

        /// <summary>
        /// Inline lane headers (grouping enabled without a lane filter) depend on
        /// neighbor order, so those columns still rebuild wholesale.
        /// </summary>
        private bool SupportsIncrementalTaskViewUpdates()
        {
            if (ParentKanban?.IsLaneGroupingEnabled != true)
                return true;

            return !string.IsNullOrWhiteSpace(LaneFilterId);
        }

        private void RemoveTaskViewForTask(FlowTask task)
        {
            for (var i = 0; i < _taskViews.Count; i++)
            {
                if (ReferenceEquals(_taskViews[i].Task, task))
                {
                    _taskViews.RemoveAt(i);
                    return;
                }
            }
        }

        private void InsertTaskViewForTask(FlowTask task)
        {
            if (_trackedTasksCollection == null || !_trackedTasksCollection.Contains(task))
                return;

            if (!TaskPassesCellFilter(task, out var lane))
                return;

            // Guard against duplicates (LaneId sync and the Add event can both fire).
            for (var i = 0; i < _taskViews.Count; i++)
            {
                if (ReferenceEquals(_taskViews[i].Task, task))
                    return;
            }

            _taskViews.Insert(FindTaskViewInsertIndex(task), new FlowKanbanTaskView(task, lane, showLaneHeader: false));
        }

        /// <summary>
        /// Mirrors the visibility rules of <see cref="BuildTaskViewsBatch"/> for a
        /// single task in the incremental update path.
        /// </summary>
        private bool TaskPassesCellFilter(FlowTask task, out FlowKanbanLane? lane)
        {
            lane = null;

            if (!task.IsSearchMatch)
                return false;

            var laneFilterId = string.IsNullOrWhiteSpace(LaneFilterId) ? null : LaneFilterId;
            if (ParentKanban?.IsLaneGroupingEnabled != true || laneFilterId == null)
                return true;

            var rawLaneId = task.LaneId;
            if (IsUnassignedLaneId(laneFilterId))
            {
                var missingLane = !string.IsNullOrWhiteSpace(rawLaneId) && FindBoardLane(rawLaneId) == null;
                if (!IsUnassignedLaneId(rawLaneId) && !missingLane)
                    return false;

                lane = new FlowKanbanLane
                {
                    Id = FlowKanban.UnassignedLaneId,
                    Title = FloweryLocalization.GetString("Kanban_Lanes_Unassigned")
                };
                return true;
            }

            if (!string.Equals(rawLaneId, laneFilterId, StringComparison.Ordinal))
                return false;

            lane = FindBoardLane(laneFilterId);
            return true;
        }

        private FlowKanbanLane? FindBoardLane(string laneId)
        {
            var lanes = ParentKanban?.Board?.Lanes;
            if (lanes == null)
                return null;

            foreach (var lane in lanes)
            {
                if (string.Equals(lane.Id, laneId, StringComparison.Ordinal))
                    return lane;
            }

            return null;
        }

        /// <summary>
        /// Task views are kept in task-list order, so the insert position is just
        /// before the first view whose task sits after the new task in the column.
        /// </summary>
        private int FindTaskViewInsertIndex(FlowTask task)
        {
            var tasks = _trackedTasksCollection;
            if (tasks == null)
                return _taskViews.Count;

            var positions = new Dictionary<FlowTask, int>(tasks.Count);
            for (var i = 0; i < tasks.Count; i++)
            {
                positions[tasks[i]] = i;
            }

            if (!positions.TryGetValue(task, out var taskPosition))
                return _taskViews.Count;

            for (var i = 0; i < _taskViews.Count; i++)
            {
                if (positions.TryGetValue(_taskViews[i].Task, out var position) && position > taskPosition)
                    return i;
            }

            return _taskViews.Count;
        }

        private void TrackViewTask(FlowTask task)
        {
            if (!_trackedViewTasks.Add(task))
                return;

            task.PropertyChanged += OnTaskPropertyChanged;
        }

        private void UntrackViewTask(FlowTask task)
        {
            if (!_trackedViewTasks.Remove(task))
                return;

            task.PropertyChanged -= OnTaskPropertyChanged;
        }

        private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowTask.LaneId), StringComparison.Ordinal))
            {
                if (sender is not FlowTask task || !TrySyncTaskViewAfterLaneChange(task))
                {
                    RebuildTaskViews();
                }
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.IsSearchMatch), StringComparison.Ordinal))
            {
                UpdateTaskCountDisplay();
            }
        }

        /// <summary>
        /// Adds or removes the single affected task view when a task's lane
        /// changes, instead of rebuilding the whole column.
        /// </summary>
        private bool TrySyncTaskViewAfterLaneChange(FlowTask task)
        {
            if (ParentKanban?.IsLaneGroupingEnabled != true)
            {
                // Views don't render lane information in this mode.
                return true;
            }

            if (string.IsNullOrWhiteSpace(LaneFilterId))
                return false;

            if (_taskViewBuildPending || _taskViewBuildState != null)
                return false;

            if (_trackedTasksCollection == null || !_trackedTasksCollection.Contains(task))
            {
                // LaneId can change before a cross-column move; drop any stale view.
                RemoveTaskViewForTask(task);
                return true;
            }

            var passes = TaskPassesCellFilter(task, out var lane);
            var existingIndex = -1;
            for (var i = 0; i < _taskViews.Count; i++)
            {
                if (ReferenceEquals(_taskViews[i].Task, task))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (passes && existingIndex < 0)
            {
                _taskViews.Insert(FindTaskViewInsertIndex(task), new FlowKanbanTaskView(task, lane, showLaneHeader: false));
            }
            else if (!passes && existingIndex >= 0)
            {
                _taskViews.RemoveAt(existingIndex);
            }

            return true;
        }

        private void ApplySizing()
        {
            var isCollapsed = IsEffectiveCollapsed;

            // Column sizing
            if (ParentKanban?.IsCompactLayoutEnabled == true)
            {
                ClearValue(WidthProperty);
                Padding = FlowKanbanResources.GetColumnPadding(ColumnSize);
                if (isCollapsed)
                {
                    var headerFontSize = FlowKanbanResources.GetColumnHeaderFontSize(ColumnSize);
                    var padding = Padding;
                    MinHeight = headerFontSize + padding.Top + padding.Bottom + 12;
                }
                else
                {
                    ClearValue(MinHeightProperty);
                }
            }
            else
            {
                Width = isCollapsed ? GetCollapsedColumnWidth(ColumnSize) : GetExpandedColumnWidth();
                Padding = isCollapsed ? GetCollapsedColumnPadding(ColumnSize) : FlowKanbanResources.GetColumnPadding(ColumnSize);
                ClearValue(MinHeightProperty);
            }
            CornerRadius = FlowKanbanResources.GetColumnCornerRadius(ColumnSize);

            // Header text sizing
            if (_headerText != null)
            {
                _headerText.FontSize = FlowKanbanResources.GetColumnHeaderFontSize(ColumnSize);
            }

            // Update task card spacing
            ApplyTaskItemSpacing(FlowKanbanResources.GetCardSpacing(ColumnSize));
        }

        private void ApplyTaskItemSpacing(double spacing)
        {
            if (_tasksListView == null)
            {
                _taskItemSpacing = spacing;
                return;
            }

            if (_tasksItemContainerStyle != null
                && _tasksListView.Styles.Contains(_tasksItemContainerStyle)
                && Math.Abs(_taskItemSpacing - spacing) < 0.1)
                return;

            _taskItemSpacing = spacing;

            if (_tasksItemContainerStyle is { } previousStyle)
            {
                _tasksListView.Styles.Remove(previousStyle);
            }

            var itemStyle = new Style(selector => selector.OfType<ListBoxItem>());
            itemStyle.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            itemStyle.Setters.Add(new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            itemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, spacing)));
            _tasksItemContainerStyle = itemStyle;
            _tasksListView.Styles.Add(itemStyle);
        }

        private void CacheCollapsedDefaults()
        {
            _baseShowTasks ??= ShowTasks;
            _baseShowAddCard ??= ShowAddCard;
        }

        private void UpdateCollapsedState()
        {
            if (!_isLoaded)
                return;

            CacheCollapsedDefaults();

            var isCollapsed = IsEffectiveCollapsed;

            if (isCollapsed)
            {
                ShowTasks = false;
                ShowAddCard = false;
            }
            else
            {
                if (_baseShowTasks.HasValue)
                    ShowTasks = _baseShowTasks.Value;
                if (_baseShowAddCard.HasValue)
                    ShowAddCard = _baseShowAddCard.Value;
            }

            ApplySizing();
            UpdateCollapseIcon();
            UpdateCollapsedHeaderVisibility();
        }

        private void UpdateHeaderLayout()
        {
            // Header layout is handled in XAML with separate expanded/collapsed containers.
        }

        private void UpdateCollapsedHeaderVisibility()
        {
            UpdateColumnReorderState();

            var isVisible = !IsEffectiveCollapsed;

            if (_editButton != null)
            {
                _editButton.IsVisible = isVisible;
            }

            if (_headerIconsPanel != null)
            {
                _headerIconsPanel.IsVisible = isVisible;
            }

            if (_taskCountBadge != null)
            {
                _taskCountBadge.IsVisible = isVisible;
            }

            UpdateEditButtonVisibilityForPlatform();

            if (IsEffectiveCollapsed && _columnGripElement != null)
            {
                _columnGripElement.IsVisible = false;
                _columnGripElement.IsHitTestVisible = false;
            }
        }

        private void UpdateEditButtonVisibilityForPlatform()
        {
            if (_editButton == null)
                return;

            if (!IsMobilePlatform())
                return;

            if (!_editButton.IsVisible)
                return;

            _editButton.IsHitTestVisible = true;
            _editButton.Opacity = 0.7;
        }

        private static bool IsMobilePlatform()
        {
            return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
        }

        private void UpdateColumnReorderState()
        {
            if (_columnGripElement == null)
                return;

            var reorderEnabled = ParentKanban?.IsCompactLayoutEnabled != true;
            var isVisible = !IsEffectiveCollapsed && reorderEnabled;
            _columnGripElement.IsVisible = isVisible;
            _columnGripElement.IsHitTestVisible = isVisible;
        }

        private void UpdateCollapseIcon()
        {
            if (_collapseIcon == null)
                return;

            var isCompact = ParentKanban?.IsCompactLayoutEnabled == true;
            var isCollapsed = IsEffectiveCollapsed;
            var iconKey = isCompact
                ? (isCollapsed ? "DaisyIconChevronDown" : "DaisyIconChevronUp")
                : (isCollapsed ? "DaisyIconChevronRight" : "DaisyIconChevronLeft");
            _collapseIcon.IconData = FlowKanbanControlFactory.GetIconGeometry(iconKey) ?? _collapseIcon.IconData;
        }

        private double GetExpandedColumnWidth()
        {
            return ParentKanban?.ColumnWidth ?? FlowKanban.DefaultColumnWidth;
        }

        private static double GetCollapsedColumnWidth(DaisySize size)
        {
            // Ensure the collapsed rail is wide enough to show the expand button + border/padding.
            return size switch
            {
                DaisySize.ExtraSmall => 40,
                DaisySize.Small => 44,
                DaisySize.Medium => 48,
                DaisySize.Large => 52,
                DaisySize.ExtraLarge => 56,
                _ => 48
            };
        }

        private static Thickness GetCollapsedColumnPadding(DaisySize size)
        {
            return new Thickness(2);
        }

        private void RebuildTaskViews()
        {
            _taskViewBuildVersion++;
            _taskViewBuildState = null;

            if (!ShouldStaggerTaskViewBuild())
            {
                BuildTaskViewsImmediate();
                return;
            }

            ScheduleTaskViewRebuild();
        }

        private bool ShouldStaggerTaskViewBuild()
        {
            return ParentKanban?.EnableStaggeredTaskRendering == true;
        }

        private void BuildTaskViewsImmediate()
        {
            _taskViews.Clear();

            if (!TryCreateTaskViewBuildState(out var state) || state == null)
                return;

            BuildTaskViewsBatch(state, int.MaxValue);
        }

        private void ScheduleTaskViewRebuild()
        {
            if (_taskViewBuildPending)
                return;

            _taskViewBuildPending = true;
            var version = _taskViewBuildVersion;
            FlowKanbanDispatcher.Post(() =>
            {
                _taskViewBuildPending = false;
                if (version != _taskViewBuildVersion)
                {
                    ScheduleTaskViewRebuild();
                    return;
                }

                _taskViews.Clear();

                if (!TryCreateTaskViewBuildState(out var state) || state == null)
                    return;

                _taskViewBuildState = state;
                EnqueueTaskViewBatch(version);
            });
        }

        private void EnqueueTaskViewBatch(int version)
        {
            FlowKanbanDispatcher.Post(() => DrainTaskViewBatches(version));
        }

        private void DrainTaskViewBatches(int version)
        {
            if (version != _taskViewBuildVersion)
                return;

            if (_taskViewBuildState == null)
                return;

            var state = _taskViewBuildState;
            var isComplete = BuildTaskViewsBatch(state, TaskViewStaggerBatchSize);
            if (version != _taskViewBuildVersion)
                return;

            if (isComplete)
            {
                _taskViewBuildState = null;
                return;
            }

            EnqueueTaskViewBatch(version);
        }

        private bool TryCreateTaskViewBuildState(out TaskViewBuildState? state)
        {
            state = null;
            if (_trackedColumnData == null)
                return false;

            var groupingEnabled = ParentKanban?.IsLaneGroupingEnabled == true;
            var laneFilterId = string.IsNullOrWhiteSpace(LaneFilterId) ? null : LaneFilterId;
            var filteringEnabled = groupingEnabled && laneFilterId != null;
            var filterIsUnassigned = filteringEnabled && IsUnassignedLaneId(laneFilterId);
            Dictionary<string, FlowKanbanLane>? laneLookup = null;
            if (groupingEnabled && ParentKanban != null)
            {
                laneLookup = new Dictionary<string, FlowKanbanLane>(StringComparer.Ordinal);
                foreach (var lane in ParentKanban.Board.Lanes)
                {
                    if (string.IsNullOrWhiteSpace(lane.Id))
                        continue;

                    if (!laneLookup.ContainsKey(lane.Id))
                    {
                        laneLookup.Add(lane.Id, lane);
                    }
                }
            }

            var tasks = new List<FlowTask>(_trackedColumnData.Tasks);
            state = new TaskViewBuildState(tasks, groupingEnabled, filteringEnabled, filterIsUnassigned, laneFilterId, laneLookup);
            return true;
        }

        private bool BuildTaskViewsBatch(TaskViewBuildState state, int maxItems)
        {
            var added = 0;
            while (state.TaskIndex < state.Tasks.Count)
            {
                var task = state.Tasks[state.TaskIndex];
                state.TaskIndex++;

                if (!task.IsSearchMatch)
                    continue;

                FlowKanbanLane? lane = null;
                string? laneId = null;

                if (state.FilteringEnabled)
                {
                    var filterId = state.LaneFilterId!;
                    var rawLaneId = task.LaneId;
                    if (state.FilterIsUnassigned)
                    {
                        var missingLane = !string.IsNullOrWhiteSpace(rawLaneId)
                                          && state.LaneLookup != null
                                          && !state.LaneLookup.ContainsKey(rawLaneId);
                        if (!IsUnassignedLaneId(rawLaneId) && !missingLane)
                            continue;

                        laneId = FlowKanban.UnassignedLaneId;
                        state.UnassignedLane ??= new FlowKanbanLane
                        {
                            Id = FlowKanban.UnassignedLaneId,
                            Title = FloweryLocalization.GetString("Kanban_Lanes_Unassigned")
                        };
                        lane = state.UnassignedLane;
                    }
                    else
                    {
                        if (!string.Equals(rawLaneId, filterId, StringComparison.Ordinal))
                            continue;

                        laneId = filterId;
                        if (state.LaneLookup != null && state.LaneLookup.TryGetValue(filterId, out var matchedLane))
                        {
                            lane = matchedLane;
                        }
                    }

                    _taskViews.Add(new FlowKanbanTaskView(task, lane, showLaneHeader: false));
                }
                else
                {
                    if (state.GroupingEnabled)
                    {
                        var rawLaneId = task.LaneId;
                        if (IsUnassignedLaneId(rawLaneId))
                        {
                            laneId = FlowKanban.UnassignedLaneId;
                            state.UnassignedLane ??= new FlowKanbanLane
                            {
                                Id = FlowKanban.UnassignedLaneId,
                                Title = FloweryLocalization.GetString("Kanban_Lanes_Unassigned")
                            };
                            lane = state.UnassignedLane;
                        }
                        else if (state.LaneLookup != null && !string.IsNullOrWhiteSpace(rawLaneId)
                                 && state.LaneLookup.TryGetValue(rawLaneId, out var matchedLane))
                        {
                            lane = matchedLane;
                            laneId = matchedLane.Id;
                        }
                        else
                        {
                            laneId = FlowKanban.UnassignedLaneId;
                            state.UnassignedLane ??= new FlowKanbanLane
                            {
                                Id = FlowKanban.UnassignedLaneId,
                                Title = FloweryLocalization.GetString("Kanban_Lanes_Unassigned")
                            };
                            lane = state.UnassignedLane;
                        }
                    }

                    var showHeader = state.GroupingEnabled
                        && laneId != null
                        && !string.Equals(state.PreviousLaneId, laneId, StringComparison.Ordinal);

                    _taskViews.Add(new FlowKanbanTaskView(task, lane, showHeader));
                    state.PreviousLaneId = laneId;
                }

                added++;
                if (added >= maxItems)
                    break;
            }

            return state.TaskIndex >= state.Tasks.Count;
        }

        private sealed class TaskViewBuildState
        {
            public TaskViewBuildState(
                IReadOnlyList<FlowTask> tasks,
                bool groupingEnabled,
                bool filteringEnabled,
                bool filterIsUnassigned,
                string? laneFilterId,
                Dictionary<string, FlowKanbanLane>? laneLookup)
            {
                Tasks = tasks;
                GroupingEnabled = groupingEnabled;
                FilteringEnabled = filteringEnabled;
                FilterIsUnassigned = filterIsUnassigned;
                LaneFilterId = laneFilterId;
                LaneLookup = laneLookup;
            }

            public IReadOnlyList<FlowTask> Tasks { get; }
            public int TaskIndex { get; set; }
            public bool GroupingEnabled { get; }
            public bool FilteringEnabled { get; }
            public bool FilterIsUnassigned { get; }
            public string? LaneFilterId { get; }
            public Dictionary<string, FlowKanbanLane>? LaneLookup { get; }
            public FlowKanbanLane? UnassignedLane { get; set; }
            public string? PreviousLaneId { get; set; }
        }

        private void EnsureDropIndicator()
        {
            if (_dropIndicator == null)
            {
                _dropIndicator = new Rectangle
                {
                    Height = 4,
                    RadiusX = 2,
                    RadiusY = 2,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    IsHitTestVisible = false
                };
                _dropIndicator.Fill = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush");
            }
        }

        private Panel? GetTasksItemsHost()
        {
            if (_tasksItemsControl == null)
                return null;

            _tasksItemsHost = _tasksItemsControl.ItemsPanelRoot as Panel;
            return _tasksItemsHost;
        }

        private Canvas? GetDropIndicatorHost()
        {
            if (_tasksDropIndicatorHost == null)
            {
                _tasksDropIndicatorHost = FindChild<Canvas>(this, c => c.Name == "PART_TaskDropIndicatorHost");
            }

            return _tasksDropIndicatorHost;
        }

        private void ShowDropIndicator(int index, double indicatorY)
        {
            var dropHost = GetDropIndicatorHost();
            if (dropHost == null)
                return;

            EnsureDropIndicator();
            if (_dropIndicator == null) return;

            // Remove from current position if already in tree
            if (_dropIndicator.Parent is Panel oldParent)
            {
                oldParent.Children.Remove(_dropIndicator);
            }

            var hostWidth = dropHost.Bounds.Width;
            if (hostWidth <= 0 && _tasksItemsControl != null)
            {
                hostWidth = _tasksItemsControl.Bounds.Width;
            }

            _dropIndicator.Width = Math.Max(0, hostWidth);
            Canvas.SetLeft(_dropIndicator, 0);
            Canvas.SetTop(_dropIndicator, Math.Max(0, indicatorY - (_dropIndicator.Height / 2)));

            if (!dropHost.Children.Contains(_dropIndicator))
            {
                dropHost.Children.Add(_dropIndicator);
            }

            _currentDropIndex = index;
            _currentDropIndicatorY = indicatorY;
        }

        private void HideDropIndicator()
        {
            if (_dropIndicator != null)
            {
                // Try removing from parent
                if (_dropIndicator.Parent is Panel parent)
                {
                    parent.Children.Remove(_dropIndicator);
                }

                _tasksDropIndicatorHost?.Children.Remove(_dropIndicator);
            }

            _currentDropIndex = -1;
            _currentDropIndicatorY = double.NaN;
            _draggedTaskId = null;
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (!IsDropEnabled)
                return;
            if (!IsTaskDragData(e.DataTransfer))
                return;

            EnsureTasksParts();

            e.DragEffects = DragDropEffects.Move;
            _isDragHighlightActive = true;
            SetDragHighlightBorder(isActive: true);

            // Get the dragged task ID if not already cached
            _draggedTaskId ??= GetTaskIdFromDataTransfer(e.DataTransfer);

            // Calculate and show drop indicator
            if (_tasksItemsControl != null && ColumnData != null)
            {
                var dropPosition = e.GetPosition(_tasksItemsControl);
                int insertIndex = CalculateInsertIndex(dropPosition.Y, out var indicatorY);

                if (insertIndex != _currentDropIndex || Math.Abs(indicatorY - _currentDropIndicatorY) > 0.5)
                {
                    ShowDropIndicator(insertIndex, indicatorY);
                }
            }

            e.Handled = true;
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            if (!IsDropEnabled)
                return;
            if (!IsTaskDragData(e.DataTransfer))
                return;

            // Reset visual feedback
            HideDropIndicator();
            _isDragHighlightActive = false;
            SetDragHighlightBorder(isActive: false);
            UpdateFocusVisualState();

            e.Handled = true;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            if (!IsDropEnabled)
                return;
            if (!IsTaskDragData(e.DataTransfer))
                return;

            EnsureTasksParts();

            // Capture the drop index before hiding indicator
            int insertIndex = _currentDropIndex;

            // Reset visual feedback
            HideDropIndicator();
            _isDragHighlightActive = false;
            SetDragHighlightBorder(isActive: false);
            UpdateFocusVisualState();

            if (ColumnData == null || ParentKanban == null)
                return;

            // Get the dragged task ID from the data package
            var taskId = GetTaskIdFromDataTransfer(e.DataTransfer);

            if (!string.IsNullOrWhiteSpace(taskId))
            {
                // Find the task in any column
                FlowTask? draggedTask = null;
                FlowKanbanColumnData? sourceColumn = null;
                int sourceIndex = -1;

                foreach (var column in ParentKanban.Board.Columns)
                {
                    for (int i = 0; i < column.Tasks.Count; i++)
                    {
                        if (column.Tasks[i].Id == taskId)
                        {
                            draggedTask = column.Tasks[i];
                            sourceColumn = column;
                            sourceIndex = i;
                            break;
                        }
                    }
                    if (draggedTask != null) break;
                }

                if (draggedTask == null || sourceColumn == null)
                    return;

                // Use the calculated position, or recalculate if needed
                if (insertIndex < 0)
                {
                    var dropTarget = (Control?)_tasksItemsControl ?? this;
                    var dropPosition = e.GetPosition(dropTarget);
                    insertIndex = CalculateInsertIndex(dropPosition.Y, out _);
                }

                // The drop index is relative to this cell's (possibly lane-filtered)
                // task views; translate it into an index in the column's task list.
                var targetIndex = MapViewIndexToColumnIndex(insertIndex);

                var targetLaneId = LaneFilterId;
                var isLaneReassignment = ParentKanban.IsLaneGroupingEnabled
                    && !string.IsNullOrWhiteSpace(targetLaneId)
                    && !AreLaneIdsEquivalent(draggedTask.LaneId, targetLaneId);

                if (sourceColumn == ColumnData && targetIndex == sourceIndex && !isLaneReassignment)
                    return;

                var manager = new FlowKanbanManager(ParentKanban, autoAttach: false);
                int? moveIndex = targetIndex >= 0 ? targetIndex : null;
                var result = manager.TryMoveTaskWithWipEnforcement(draggedTask, ColumnData, moveIndex, targetLaneId, enforceHard: false);
                if (result == MoveResult.AllowedWithWipWarning)
                {
                    ParentKanban.ShowWipWarning(ColumnData, targetLaneId);
                }
            }

            e.Handled = true;
        }

        private int MapViewIndexToColumnIndex(int viewIndex)
        {
            if (ColumnData == null || viewIndex < 0)
                return viewIndex;

            // Unfiltered cells: the view list mirrors the column's task list.
            var laneFilterId = string.IsNullOrWhiteSpace(LaneFilterId) ? null : LaneFilterId;
            if (laneFilterId == null || ParentKanban?.IsLaneGroupingEnabled != true)
                return viewIndex;

            if (viewIndex < _taskViews.Count)
            {
                var anchorIndex = ColumnData.Tasks.IndexOf(_taskViews[viewIndex].Task);
                if (anchorIndex >= 0)
                    return anchorIndex;
            }
            else if (_taskViews.Count > 0)
            {
                var lastIndex = ColumnData.Tasks.IndexOf(_taskViews[_taskViews.Count - 1].Task);
                if (lastIndex >= 0)
                    return lastIndex + 1;
            }

            return ColumnData.Tasks.Count;
        }

        private static bool AreLaneIdsEquivalent(string? left, string? right)
        {
            var leftUnassigned = IsUnassignedLaneId(left);
            var rightUnassigned = IsUnassignedLaneId(right);
            if (leftUnassigned || rightUnassigned)
                return leftUnassigned && rightUnassigned;

            return string.Equals(FlowKanban.NormalizeLaneId(left), FlowKanban.NormalizeLaneId(right), StringComparison.Ordinal);
        }

        private static bool IsTaskDragData(IDataTransfer dataTransfer)
        {
            return !string.IsNullOrWhiteSpace(GetTaskIdFromDataTransfer(dataTransfer));
        }

        private static string? GetTaskIdFromDataTransfer(IDataTransfer dataTransfer)
        {
            return dataTransfer.TryGetValue(FlowKanban.TaskDragFormat)
                ?? dataTransfer.TryGetText();
        }

        private void OnColumnGotFocus(object? sender, RoutedEventArgs e)
        {
            UpdateFocusVisualState();
        }

        private void OnColumnDoubleTapped(object? sender, TappedEventArgs e)
        {
            // Expand-only gesture: never collapse via double tap/click.
            if (!IsEffectiveCollapsed)
                return;

            SetCollapsedFromAutomation(false);
            e.Handled = true;
        }

        internal void SetCollapsedFromAutomation(bool isCollapsed)
        {
            SetCurrentValue(IsCollapsedProperty, isCollapsed);
            if (ColumnData is { } columnData)
            {
                columnData.IsCollapsed = isCollapsed;
            }
        }

        private void OnColumnLostFocus(object? sender, RoutedEventArgs e)
        {
            if (IsFocusWithin())
                return;

            HideHeaderButtons();
            UpdateFocusVisualState();
        }

        internal bool HandleTaskSelection(FlowTask task, bool isRangeSelection, bool isToggleSelection)
        {
            if (ParentKanban == null)
                return false;

            var selectableTasks = GetSelectableTasks();
            if (selectableTasks.Count == 0)
                return false;

            if (isRangeSelection && _lastSelectedTask != null)
            {
                var startIndex = selectableTasks.IndexOf(_lastSelectedTask);
                var endIndex = selectableTasks.IndexOf(task);
                if (startIndex >= 0 && endIndex >= 0)
                {
                    if (!isToggleSelection)
                    {
                        ParentKanban.DeselectAllTasks();
                    }

                    var from = Math.Min(startIndex, endIndex);
                    var to = Math.Max(startIndex, endIndex);
                    for (var i = from; i <= to; i++)
                    {
                        selectableTasks[i].IsSelected = true;
                    }

                    _lastSelectedTask = task;
                    return true;
                }
            }

            if (!isToggleSelection)
            {
                ParentKanban.DeselectAllTasks();
                task.IsSelected = true;
            }
            else
            {
                task.IsSelected = !task.IsSelected;
            }

            _lastSelectedTask = task;
            return true;
        }

        private List<FlowTask> GetSelectableTasks()
        {
            var tasks = new List<FlowTask>();
            if (!ShowTasks)
                return tasks;

            foreach (var view in TaskViews)
            {
                if (view.Task != null && view.Task.IsSearchMatch)
                {
                    tasks.Add(view.Task);
                }
            }

            return tasks;
        }

        private void UpdateFocusVisualState()
        {
            if (_isDragHighlightActive)
                return;

            var shouldHighlight = IsFocusWithin() && IsKeyboardFocusActive();
            if (shouldHighlight == _isKeyboardFocusVisible)
                return;

            _isKeyboardFocusVisible = shouldHighlight;
            Classes.Set("keyboard-focus", shouldHighlight);
        }

        private bool IsKeyboardFocusActive()
        {
            if (TopLevel == null)
                return false;

            var focused = FlowKanbanVisualTree.GetFocusedElement(this) as Control;
            return focused?.Classes.Contains(":focus-visible") == true;
        }

        private bool IsFocusWithin()
        {
            if (TopLevel == null)
                return false;

            var focused = FlowKanbanVisualTree.GetFocusedElement(this);
            if (focused == null)
                return false;

            return FindAncestor<FlowKanbanColumn>(focused) == this;
        }

        private static T? FindAncestor<T>(AvaloniaObject? element) where T : AvaloniaObject
        {
            return FlowKanbanVisualTree.FindAncestor<T>(element);
        }

        private FlowKanbanTaskView? FindTaskView(FlowTask task)
        {
            foreach (var view in _taskViews)
            {
                if (ReferenceEquals(view.Task, task))
                    return view;
            }

            return null;
        }

        private void OnTaskContainerPrepared(object? sender, ContainerPreparedEventArgs args)
        {
            if (args.Index < 0 || args.Index >= _taskViews.Count)
                return;

            var view = _taskViews[args.Index];
            var container = args.Container;
            container.GotFocus -= OnTaskContainerGotFocus;
            container.GotFocus += OnTaskContainerGotFocus;
            Dispatcher.UIThread.Post(() =>
            {
                var card = container.GetVisualDescendants().OfType<FlowTaskCard>().FirstOrDefault();
                if (card is { })
                {
                    _realizedCards[view] = card;
                }
            }, DispatcherPriority.Loaded);
        }

        private void OnTaskContainerClearing(object? sender, ContainerClearingEventArgs args)
        {
            args.Container.GotFocus -= OnTaskContainerGotFocus;
            if (args.Container.DataContext is FlowKanbanTaskView view)
            {
                _realizedCards.Remove(view);
            }
        }

        private void OnTaskContainerGotFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control container)
                return;

            var card = container.GetVisualDescendants().OfType<FlowTaskCard>().FirstOrDefault();
            if (card == null)
                return;

            var focused = TopLevel == null
                ? null
                : FlowKanbanVisualTree.GetFocusedElement(this);
            if (focused != null && FindAncestor<FlowTaskCard>(focused) == card)
                return;

            card.Focus();
        }

        internal FlowTaskCard? TryGetTaskCard(FlowTask task)
        {
            var view = FindTaskView(task);
            if (view == null)
                return null;

            return _realizedCards.TryGetValue(view, out var card) ? card : null;
        }

        internal bool ScrollTaskIntoView(FlowTask task)
        {
            if (_tasksListView == null)
                return false;

            var view = FindTaskView(task);
            if (view == null)
                return false;

            _tasksListView.ScrollIntoView(view);
            return true;
        }

        internal IEnumerable<FlowTaskCard> GetRealizedTaskCards()
        {
            return _realizedCards.Values;
        }

        /// <summary>
        /// Calculate the insertion index based on the Y position of the drop.
        /// Uses edge-based detection - if you're past the top half of a task, insert after it.
        /// </summary>
        internal int CalculateInsertIndex(double dropY, out double indicatorY)
        {
            indicatorY = 0;
            if (ColumnData == null)
                return 0;

            if (_tasksItemsControl == null)
            {
                EnsureTasksParts();
            }

            if (_tasksItemsControl == null)
                return ColumnData.Tasks.Count;

            var panel = GetTasksItemsHost();
            if (panel == null || panel.Children.Count == 0)
                return 0;

            // With container recycling the panel's child order is not guaranteed to
            // match item order, so collect realized containers first and sort by index.
            var containers = new List<(int Index, double Top, double Height)>(panel.Children.Count);
            foreach (var child in panel.Children)
            {
                if (child is not Control fe)
                    continue;

                var index = _tasksListView?.IndexFromContainer(fe) ?? -1;
                if (index < 0)
                    continue;

                var topLeft = FlowKanbanVisualTree.TransformPoint(fe, _tasksItemsControl, new Point(0, 0));
                containers.Add((index, topLeft.Y, fe.Bounds.Height));
            }

            if (containers.Count == 0)
                return ColumnData.Tasks.Count;

            return CalculateInsertIndexFromLayout(
                dropY,
                ColumnData.Tasks.Count,
                containers,
                out indicatorY);
        }

        internal static int CalculateInsertIndexFromLayout(
            double dropY,
            int itemCount,
            List<(int Index, double Top, double Height)> containers,
            out double indicatorY)
        {
            indicatorY = 0;
            if (containers.Count == 0)
                return itemCount;

            containers.Sort((a, b) => a.Index.CompareTo(b.Index));

            foreach (var container in containers)
            {
                var midpoint = container.Top + (container.Height / 2);
                if (dropY < midpoint)
                {
                    indicatorY = container.Top;
                    return container.Index;
                }
            }

            var last = containers[containers.Count - 1];
            indicatorY = last.Top + last.Height;
            return Math.Min(last.Index + 1, itemCount);
        }
    }

    internal class FlowKanbanColumnAutomationPeer : ControlAutomationPeer
    {
        public FlowKanbanColumnAutomationPeer(FlowKanbanColumn owner)
            : base(owner)
        {
        }

        protected FlowKanbanColumn Column => (FlowKanbanColumn)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(FlowKanbanColumn);

        protected override bool IsContentElementCore() =>
            FlowKanbanVisualTree.IsAutomationVisible(Column);

        protected override bool IsControlElementCore() =>
            FlowKanbanVisualTree.IsAutomationVisible(Column);
    }

    internal sealed class FlowKanbanExpandableColumnAutomationPeer :
        FlowKanbanColumnAutomationPeer,
        IExpandCollapseProvider
    {
        private ExpandCollapseState _lastState;

        public FlowKanbanExpandableColumnAutomationPeer(FlowKanbanColumn owner)
            : base(owner)
        {
            _lastState = ExpandCollapseState;
            owner.PropertyChanged += OnOwnerPropertyChanged;
        }

        public ExpandCollapseState ExpandCollapseState =>
            Column.IsCollapsed || Column.ColumnData?.IsCollapsed == true
                ? ExpandCollapseState.Collapsed
                : ExpandCollapseState.Expanded;

        public bool ShowsMenu => false;

        public void Expand() => SetCollapsed(false);

        public void Collapse() => SetCollapsed(true);

        private void SetCollapsed(bool isCollapsed)
        {
            if (!Column.IsEnabled || !Column.IsCollapseAutomationAvailable)
            {
                throw new InvalidOperationException("The Kanban column collapse action is unavailable.");
            }

            Column.SetCollapsedFromAutomation(isCollapsed);
        }

        private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property != FlowKanbanColumn.IsCollapsedProperty)
            {
                return;
            }

            var state = ExpandCollapseState;
            if (_lastState != state)
            {
                RaisePropertyChangedEvent(
                    ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    _lastState,
                    state);
                _lastState = state;
            }
        }
    }
}
