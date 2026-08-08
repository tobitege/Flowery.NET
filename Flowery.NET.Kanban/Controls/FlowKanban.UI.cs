using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Flowery.Controls;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Partial class containing UI-related properties and methods for FlowKanban.
    /// </summary>
    public partial class FlowKanban
    {
        private const int ThemeRefreshDebounceMilliseconds = 120;
        private const double CompactLayoutColumnPadding = 16;
        private const double CompactLayoutColumnSpacing = 8;
        private const int CompactLayoutMinColumnCount = 2;
        private const double DefaultSwimlaneCellMaxHeight = 480;
        private const double MinSwimlaneCellMaxHeight = 240;
        private const double SwimlaneCellViewportFraction = 0.6;
        // Board header elements for hover behavior
        private Grid? _boardHeaderGrid;
        private Grid? _boardContentHost;
        private TextBlock? _boardTitleText;
        private DaisyButton? _renameBoardButton;
        private DaisyPopover? _boardMenuPopover;
        private DaisyMenu? _boardMenu;
        private DaisyInput? _boardSearchInput;
        private Border? _sidebarBorder;
        private Border? _statusBarBorder;
        private bool _isBoardHeaderPointerOver;
        private bool _isBoardHeaderFocused;
        private IDisposable? _boardMenuPopoverOpenSubscription;
        private DispatcherTimer? _statusMessageTimer;
        private DispatcherTimer? _themeRefreshTimer;
        private bool _isLocalizationSubscribed;
        private bool _isColumnReorderActive = true;
        private FlowKanbanColumnsHost? _standardColumnsHost;
        private FlowKanbanColumnsHost? _swimlaneColumnsHost;
        private Border? _compactColumnsHost;
        private DaisySelect? _compactColumnSelect;
        private ContentPresenter? _compactColumnPresenter;
        private int _compactScrollLockCount;
        private bool? _compactLayoutOverride;
        private bool _isColumnWidthDragActive;
        private bool _columnWidthSavePending;
        private bool _isClampingColumnWidth;
        private bool _compactScrollRefreshPending;
        private readonly List<FlowKanbanColumn> _cachedColumnControls = new();
        private ItemsControl? _laneRowsHost;
        private readonly Dictionary<(string ColumnId, string? LaneId), FlowKanbanColumn> _columnByKey = new();

        #region Layout DPs
        public static readonly StyledProperty<bool> IsSwimlaneLayoutEnabledProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsSwimlaneLayoutEnabled),
                false);

        public bool IsSwimlaneLayoutEnabled
        {
            get => (bool)GetValue(IsSwimlaneLayoutEnabledProperty);
            private set => SetValue(IsSwimlaneLayoutEnabledProperty, value);
        }

        public static readonly StyledProperty<bool> IsStandardLayoutEnabledProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsStandardLayoutEnabled),
                true);

        public bool IsStandardLayoutEnabled
        {
            get => (bool)GetValue(IsStandardLayoutEnabledProperty);
            private set => SetValue(IsStandardLayoutEnabledProperty, value);
        }

        public static readonly StyledProperty<bool> IsCompactLayoutEnabledProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(IsCompactLayoutEnabled),
                                false);

        public bool IsCompactLayoutEnabled
        {
            get => (bool)GetValue(IsCompactLayoutEnabledProperty);
            private set => SetValue(IsCompactLayoutEnabledProperty, value);
        }

        public static readonly StyledProperty<IEnumerable<FlowKanbanColumnData>?> StandardColumnsSourceProperty =
            AvaloniaProperty.Register<FlowKanban, IEnumerable<FlowKanbanColumnData>?>(
                nameof(StandardColumnsSource),
                default!);

        public IEnumerable<FlowKanbanColumnData>? StandardColumnsSource
        {
            get => (IEnumerable<FlowKanbanColumnData>?)GetValue(StandardColumnsSourceProperty);
            private set => SetValue(StandardColumnsSourceProperty, value);
        }

        public static readonly StyledProperty<IEnumerable<FlowKanbanColumnData>?> CompactColumnsSourceProperty =
            AvaloniaProperty.Register<FlowKanban, IEnumerable<FlowKanbanColumnData>?>(
                nameof(CompactColumnsSource),
                default!);

        public IEnumerable<FlowKanbanColumnData>? CompactColumnsSource
        {
            get => (IEnumerable<FlowKanbanColumnData>?)GetValue(CompactColumnsSourceProperty);
            private set => SetValue(CompactColumnsSourceProperty, value);
        }

        public static readonly StyledProperty<FlowKanbanColumnData?> CompactSelectedColumnProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanColumnData?>(
                                nameof(CompactSelectedColumn),
                                default!);

        public FlowKanbanColumnData? CompactSelectedColumn
        {
            get => (FlowKanbanColumnData?)GetValue(CompactSelectedColumnProperty);
            set => SetValue(CompactSelectedColumnProperty, value);
        }

        public static readonly StyledProperty<IEnumerable<FlowKanbanColumnData>?> SwimlaneColumnsSourceProperty =
            AvaloniaProperty.Register<FlowKanban, IEnumerable<FlowKanbanColumnData>?>(
                nameof(SwimlaneColumnsSource),
                default!);

        public IEnumerable<FlowKanbanColumnData>? SwimlaneColumnsSource
        {
            get => (IEnumerable<FlowKanbanColumnData>?)GetValue(SwimlaneColumnsSourceProperty);
            private set => SetValue(SwimlaneColumnsSourceProperty, value);
        }

        public static readonly StyledProperty<double> CompactColumnMaxHeightProperty =
            AvaloniaProperty.Register<FlowKanban, double>(
                nameof(CompactColumnMaxHeight),
                double.PositiveInfinity);

        public double CompactColumnMaxHeight
        {
            get => (double)GetValue(CompactColumnMaxHeightProperty);
            private set => SetValue(CompactColumnMaxHeightProperty, value);
        }

        public static readonly StyledProperty<double> SwimlaneCellMaxHeightProperty =
            AvaloniaProperty.Register<FlowKanban, double>(
                nameof(SwimlaneCellMaxHeight),
                DefaultSwimlaneCellMaxHeight);

        /// <summary>
        /// Upper bound for the tasks list inside a swimlane lane cell. Keeping the
        /// list bounded is required for ListView virtualization: inside the vertically
        /// scrolling swimlane panel the list would otherwise measure with infinite
        /// height and realize every container.
        /// </summary>
        public double SwimlaneCellMaxHeight
        {
            get => (double)GetValue(SwimlaneCellMaxHeightProperty);
            private set => SetValue(SwimlaneCellMaxHeightProperty, value);
        }

        public static readonly StyledProperty<bool> EnableStaggeredTaskRenderingProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(EnableStaggeredTaskRendering),
                FlowKanbanPlatformDefaults.Current.EnableStaggeredTaskRendering);

        public bool EnableStaggeredTaskRendering
        {
            get => (bool)GetValue(EnableStaggeredTaskRenderingProperty);
            set => SetValue(EnableStaggeredTaskRenderingProperty, value);
        }

        public static readonly StyledProperty<ObservableCollection<FlowKanbanLaneRowView>> LaneRowsProperty =
            AvaloniaProperty.Register<FlowKanban, ObservableCollection<FlowKanbanLaneRowView>>(
                nameof(LaneRows),
                default!);

        public ObservableCollection<FlowKanbanLaneRowView> LaneRows
        {
            get
            {
                if (GetValue(LaneRowsProperty) is not ObservableCollection<FlowKanbanLaneRowView> lanes)
                {
                    lanes = new ObservableCollection<FlowKanbanLaneRowView>();
                    SetValue(LaneRowsProperty, lanes);
                }

                return lanes;
            }
            private set => SetValue(LaneRowsProperty, value);
        }

        private static void OnCompactLayoutEnabledChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban && e.NewValue is bool isCompact)
            {
                if (isCompact)
                {
                    kanban.SeedCompactColumnMaxHeight();
                    kanban.EndColumnResizeSessionIfNeeded();
                    if (kanban.IsColumnResizeEnabled)
                        kanban.IsColumnResizeEnabled = false;
                }
                kanban.ApplyLayoutMode();
                kanban.UpdateLaneRows();
                kanban.UpdateColumnReorderState();
                kanban.ScheduleCompactColumnSizingUpdate();
                kanban.CompactLayoutChanged?.Invoke(kanban, isCompact);
            }
        }

        private static void OnCompactSelectedColumnChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.RequestCompactLayoutScrollRefresh();
                kanban.ScheduleCompactColumnSizingUpdate();
            }
        }
        #endregion

        #region Layout Methods
        /// <summary>
        /// Toggles between compact and regular layout modes.
        /// </summary>
        public void ToggleCompactLayout()
        {
            SetCompactLayout(!IsCompactLayoutEnabled);
        }

        /// <summary>
        /// Sets the layout mode to compact or regular.
        /// </summary>
        /// <param name="isCompact">When true, forces compact layout.</param>
        public void SetCompactLayout(bool isCompact)
        {
            _compactLayoutOverride = isCompact;
            UpdateCompactLayoutState();
        }

        /// <summary>
        /// Restores automatic layout selection based on available width.
        /// </summary>
        public void RestoreAutomaticLayout()
        {
            _compactLayoutOverride = null;
            UpdateCompactLayoutState();
        }

        /// <summary>
        /// Sets the compact layout to display a specific column by ID.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <returns>True if the column was found and selected.</returns>
        public bool TrySetCompactColumnById(string columnId)
        {
            if (string.IsNullOrWhiteSpace(columnId))
                return false;

            var column = Board.Columns.FirstOrDefault(c => string.Equals(c.Id, columnId, StringComparison.Ordinal));
            if (column == null)
                return false;

            CompactSelectedColumn = column;
            return true;
        }

        /// <summary>
        /// Sets the compact layout to display a specific column by title.
        /// </summary>
        /// <param name="title">The column title.</param>
        /// <returns>True if the column was found and selected.</returns>
        public bool TrySetCompactColumnByTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return false;

            var column = Board.Columns.FirstOrDefault(c => string.Equals(c.Title, title, StringComparison.Ordinal));
            if (column == null)
                return false;

            CompactSelectedColumn = column;
            return true;
        }

        /// <summary>
        /// Clears the compact column selection.
        /// </summary>
        public void ClearCompactColumnSelection()
        {
            CompactSelectedColumn = null;
        }

        private void ApplyCompactLayoutOptions(bool useCompactLayout, string? compactColumnId, string? compactColumnTitle)
        {
            SetCompactLayout(useCompactLayout);
            if (!useCompactLayout)
            {
                CompactSelectedColumn = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(compactColumnId))
            {
                TrySetCompactColumnById(compactColumnId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(compactColumnTitle))
            {
                TrySetCompactColumnByTitle(compactColumnTitle);
            }
        }
        #endregion

        #region Archive Column Visibility
        public static readonly StyledProperty<bool> CanShowArchiveColumnProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(CanShowArchiveColumn),
                false);

        public bool CanShowArchiveColumn
        {
            get => (bool)GetValue(CanShowArchiveColumnProperty);
            private set => SetValue(CanShowArchiveColumnProperty, value);
        }

        public static readonly StyledProperty<bool> CanHideArchiveColumnProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(CanHideArchiveColumn),
                false);

        public bool CanHideArchiveColumn
        {
            get => (bool)GetValue(CanHideArchiveColumnProperty);
            private set => SetValue(CanHideArchiveColumnProperty, value);
        }

        public static readonly StyledProperty<bool> HasArchiveColumnToggleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(HasArchiveColumnToggle),
                false);

        public bool HasArchiveColumnToggle
        {
            get => (bool)GetValue(HasArchiveColumnToggleProperty);
            private set => SetValue(HasArchiveColumnToggleProperty, value);
        }
        #endregion

        #region Bulk Move Columns
        public static readonly StyledProperty<ObservableCollection<FlowKanbanColumnData>> BulkMoveColumnsProperty =
            AvaloniaProperty.Register<FlowKanban, ObservableCollection<FlowKanbanColumnData>>(
                nameof(BulkMoveColumns),
                default!);

        public ObservableCollection<FlowKanbanColumnData> BulkMoveColumns
        {
            get
            {
                if (GetValue(BulkMoveColumnsProperty) is not ObservableCollection<FlowKanbanColumnData> columns)
                {
                    columns = new ObservableCollection<FlowKanbanColumnData>();
                    SetValue(BulkMoveColumnsProperty, columns);
                }

                return columns;
            }
            private set => SetValue(BulkMoveColumnsProperty, value);
        }
        #endregion

        #region Keyboard UI
        public static readonly StyledProperty<bool> IsKeyboardHelpVisibleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsKeyboardHelpVisible),
                FlowKanbanPlatformDefaults.Current.IsKeyboardHelpVisible);

        public bool IsKeyboardHelpVisible
        {
            get => (bool)GetValue(IsKeyboardHelpVisibleProperty);
            set => SetValue(IsKeyboardHelpVisibleProperty, value);
        }

        public static readonly StyledProperty<bool> IsColumnTooltipsEnabledProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsColumnTooltipsEnabled),
                FlowKanbanPlatformDefaults.Current.IsColumnTooltipsEnabled);

        public bool IsColumnTooltipsEnabled
        {
            get => (bool)GetValue(IsColumnTooltipsEnabledProperty);
            set => SetValue(IsColumnTooltipsEnabledProperty, value);
        }

        public static readonly StyledProperty<bool> ShowKeyboardAcceleratorTooltipsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(ShowKeyboardAcceleratorTooltips),
                false);

        /// <summary>
        /// When true, keyboard accelerator UI hints (e.g. "Ctrl+B") may be shown in tooltips.
        /// Default is false.
        /// </summary>
        public bool ShowKeyboardAcceleratorTooltips
        {
            get => (bool)GetValue(ShowKeyboardAcceleratorTooltipsProperty);
            set => SetValue(ShowKeyboardAcceleratorTooltipsProperty, value);
        }

        #endregion

        #region BoardSize
        public static readonly StyledProperty<DaisySize> BoardSizeProperty =
            AvaloniaProperty.Register<FlowKanban, DaisySize>(
                                nameof(BoardSize),
                                DaisySize.Medium);

        /// <summary>
        /// The size tier for all controls within the Kanban board.
        /// Does not affect the global FlowerySizeManager state.
        /// </summary>
        public DaisySize BoardSize
        {
            get => (DaisySize)GetValue(BoardSizeProperty);
            set => SetValue(BoardSizeProperty, value);
        }

        private static void OnBoardSizeChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban && e.NewValue is DaisySize newSize)
            {
                kanban.BoardSizeChanged?.Invoke(kanban, newSize);
                kanban.NotifyZoomCommandsChanged();
                kanban.RefreshBoardSizeLabel();
            }
        }
        #endregion

        #region ColumnWidth
        public static readonly StyledProperty<double> ColumnWidthProperty =
            AvaloniaProperty.Register<FlowKanban, double>(
                                nameof(ColumnWidth),
                                DefaultColumnWidth);

        /// <summary>
        /// Shared width for all Kanban columns.
        /// </summary>
        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        public static readonly StyledProperty<double> MinColumnWidthProperty =
            AvaloniaProperty.Register<FlowKanban, double>(
                                nameof(MinColumnWidth),
                                DefaultMinColumnWidth);

        /// <summary>
        /// Minimum width for Kanban columns.
        /// </summary>
        public double MinColumnWidth
        {
            get => (double)GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        public static readonly StyledProperty<double> MaxColumnWidthProperty =
            AvaloniaProperty.Register<FlowKanban, double>(
                                nameof(MaxColumnWidth),
                                DefaultMaxColumnWidth);

        /// <summary>
        /// Maximum width for Kanban columns.
        /// </summary>
        public double MaxColumnWidth
        {
            get => (double)GetValue(MaxColumnWidthProperty);
            set => SetValue(MaxColumnWidthProperty, value);
        }

        private static void OnColumnWidthChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban && e.NewValue is double newValue)
            {
                kanban.ApplyColumnWidthChange(newValue);
            }
        }

        private static void OnColumnWidthBoundsChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.ColumnWidth = kanban.ClampColumnWidth(kanban.ColumnWidth);
            }
        }

        private void ApplyColumnWidthChange(double value)
        {
            if (_isClampingColumnWidth)
                return;

            var clamped = ClampColumnWidth(value);
            if (Math.Abs(clamped - value) > 0.01)
            {
                _isClampingColumnWidth = true;
                SetValue(ColumnWidthProperty, clamped);
                _isClampingColumnWidth = false;
                return;
            }

            ColumnWidthChanged?.Invoke(this, clamped);

            if (_isColumnWidthDragActive)
            {
                _columnWidthSavePending = true;
            }
            else
            {
                TrySaveSettings(out _);
            }
        }

        private double ClampColumnWidth(double value)
        {
            var min = MinColumnWidth;
            var max = MaxColumnWidth;
            if (min > max)
            {
                (min, max) = (max, min);
            }

            return Math.Clamp(value, min, max);
        }
        #endregion

        #region ColumnResize
        public static readonly StyledProperty<bool> IsColumnResizeEnabledProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(IsColumnResizeEnabled),
                                false);

        public bool IsColumnResizeEnabled
        {
            get => (bool)GetValue(IsColumnResizeEnabledProperty);
            set => SetValue(IsColumnResizeEnabledProperty, value);
        }

        private static void OnColumnResizeEnabledChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban && e.NewValue is bool isEnabled)
            {
                if (!isEnabled)
                {
                    kanban.TrySaveSettings(out _);
                }
            }
        }
        #endregion


        #region File Picker
        public static readonly StyledProperty<bool> IsFilePickerAvailableProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsFilePickerAvailable),
                false);

        public bool IsFilePickerAvailable
        {
            get => (bool)GetValue(IsFilePickerAvailableProperty);
            private set => SetValue(IsFilePickerAvailableProperty, value);
        }
        #endregion

        #region BoardSizeLabel
        public static readonly StyledProperty<string> BoardSizeLabelProperty =
            AvaloniaProperty.Register<FlowKanban, string>(
                nameof(BoardSizeLabel),
                string.Empty);

        /// <summary>
        /// Gets the localized label for the current BoardSize.
        /// </summary>
        public string BoardSizeLabel
        {
            get => (string)GetValue(BoardSizeLabelProperty);
            private set => SetValue(BoardSizeLabelProperty, value);
        }

        private void RefreshBoardSizeLabel()
        {
            BoardSizeLabel = FloweryLocalization.GetString($"Size_{BoardSize}");
        }
        #endregion

        #region IsStatusBarVisible
        public static readonly StyledProperty<bool> IsStatusBarVisibleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(IsStatusBarVisible),
                                true);

        /// <summary>
        /// Controls visibility of the status bar at the bottom.
        /// </summary>
        public bool IsStatusBarVisible
        {
            get => (bool)GetValue(IsStatusBarVisibleProperty);
            set => SetValue(IsStatusBarVisibleProperty, value);
        }
        #endregion

        #region StatusMessage
        public static readonly StyledProperty<string> StatusMessageProperty =
            AvaloniaProperty.Register<FlowKanban, string>(
                nameof(StatusMessage),
                string.Empty);

        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            private set => SetValue(StatusMessageProperty, value);
        }

        public static readonly StyledProperty<bool> HasStatusMessageProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(HasStatusMessage),
                false);

        public bool HasStatusMessage
        {
            get => (bool)GetValue(HasStatusMessageProperty);
            private set => SetValue(HasStatusMessageProperty, value);
        }

        internal void ShowStatusMessage(string message, TimeSpan? duration = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ClearStatusMessage();
                return;
            }

            StatusMessage = message.Trim();
            HasStatusMessage = true;
            UpdateBoardStatusBarVisibility();

            if (duration.HasValue)
            {
                StartStatusMessageTimer(duration.Value);
            }
        }

        internal void ShowPersistenceError(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            ShowStatusMessage(error.Message, TimeSpan.FromSeconds(8));
        }

        private void ClearStatusMessage()
        {
            StatusMessage = string.Empty;
            HasStatusMessage = false;
            _statusMessageTimer?.Stop();
            UpdateBoardStatusBarVisibility();
        }

        private void StartStatusMessageTimer(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                return;

            _statusMessageTimer ??= FlowKanbanDispatcher.CreateTimer(OnStatusMessageTimerTick);
            _statusMessageTimer.Stop();
            _statusMessageTimer.Interval = duration;
            _statusMessageTimer.Start();
        }

        private void OnStatusMessageTimerTick(object? sender, EventArgs e)
        {
            _statusMessageTimer?.Stop();
            ClearStatusMessage();
        }
        #endregion

        protected override void OnThemeChanged(string themeName)
        {
            base.OnThemeChanged(themeName);
            // Refresh background to pick up new theme colors
            Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
            ApplySidebarAndStatusBarTheme();
            ApplySwimlaneLaneHeaderTheme();
            ScheduleThemeRefresh();
        }

        private void OnKanbanLoaded(object? sender, RoutedEventArgs e)
        {
            var storageProvider = TopLevel?.StorageProvider;
            IsFilePickerAvailable = storageProvider is { CanOpen: true } or { CanSave: true };
            AttachAssigneeAdapter(AssigneeAdapter);
            _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
            if (!TryLoadSettings(forceReload: false, out var settingsError) && settingsError is { } error)
            {
                ReportPersistenceFailure(FlowKanbanPersistenceOperation.LoadSettings, error);
            }
            RefreshBoards();
            if (CurrentView == FlowKanbanView.Board && Boards.Count == 0)
            {
                CurrentView = FlowKanbanView.Home;
            }
            UpdateViewState();
            FindBoardHeaderElements();
            AttachBoardTracking(Board);
            RefreshBoardSizeLabel();
            ApplySearchFilter();
            AttachBoardLayoutTracking();
            UpdateLaneGroupingState();
            UpdateColumnReorderState();
            AttachColumnResizeHandlers();
            AttachKeyboardSupport();
            RefreshDoneAgingState();
            if (!_isLocalizationSubscribed)
            {
                FloweryLocalization.CultureChanged += OnLocalizationCultureChanged;
                _isLocalizationSubscribed = true;
            }
        }

        private void OnKanbanUnloaded(object? sender, RoutedEventArgs e)
        {
            if (CurrentView == FlowKanbanView.Home)
            {
                ClearLastBoardId();
            }
            DetachColumnDragHandlers();
            DetachColumnResizeHandlers();
            DetachBoardLayoutTracking();
            if (_boardHeaderGrid != null)
            {
                _boardHeaderGrid.PointerEntered -= OnBoardHeaderPointerEntered;
                _boardHeaderGrid.PointerExited -= OnBoardHeaderPointerExited;
                _boardHeaderGrid.GotFocus -= OnBoardHeaderGotFocus;
                _boardHeaderGrid.LostFocus -= OnBoardHeaderLostFocus;
                _boardHeaderGrid.SizeChanged -= OnBoardHeaderSizeChanged;
            }
            _boardMenuPopoverOpenSubscription?.Dispose();
            _boardMenuPopoverOpenSubscription = null;
            DetachBoardMenuEvents();
            _boardTitleText = null;
            _boardMenuPopover = null;
            _boardMenu = null;
            _isBoardHeaderPointerOver = false;
            _isBoardHeaderFocused = false;
            HideColumnDropIndicator();
            DetachKeyboardSupport();
            CancelAssigneeRefresh();
            DetachAssigneeAdapter();
            StopDoneAgingTimer();
            DisposeDoneAgingTimer();
            _themeRefreshTimer?.Stop();
            if (_isLocalizationSubscribed)
            {
                FloweryLocalization.CultureChanged -= OnLocalizationCultureChanged;
                _isLocalizationSubscribed = false;
            }
            ClearVisualCaches();
        }

        private void OnLocalizationCultureChanged(object? sender, CultureInfo culture)
        {
            RefreshLocalizationState();
        }

        private void RefreshLocalizationState()
        {
            NotifyLaneGroupingChanged();
            RefreshTaskCardLocalization();
            RefreshBoardSizeLabel();
        }

        private void RefreshTaskCardLocalization()
        {
            foreach (var column in _cachedColumnControls)
            {
                if (!column.IsLoaded || !column.IsVisible)
                    continue;

                foreach (var card in column.GetRealizedTaskCards())
                {
                    card.RefreshLocalization();
                }
            }
        }

        // Column lookups are populated exclusively through Loaded/Unloaded
        // self-registration of FlowKanbanColumn; there is no tree-scan fallback.
        // _laneRowsHost is a template part resolved in OnApplyTemplate and
        // stays valid across Loaded/Unloaded cycles.
        private void ClearVisualCaches()
        {
            _cachedColumnControls.Clear();
            _columnByKey.Clear();
        }

        internal void RegisterColumn(FlowKanbanColumn column)
        {
            if (column.ColumnData == null)
                return;

            if (!ReferenceEquals(column.ParentKanban, this))
                return;

            if (!_cachedColumnControls.Contains(column))
                _cachedColumnControls.Add(column);

            var key = (column.ColumnData.Id, NormalizeLaneId(column.LaneFilterId));
            _columnByKey[key] = column;
        }

        internal void UnregisterColumn(FlowKanbanColumn column)
        {
            _cachedColumnControls.Remove(column);

            if (column.ColumnData == null)
                return;

            var key = (column.ColumnData.Id, NormalizeLaneId(column.LaneFilterId));
            if (_columnByKey.TryGetValue(key, out var existing) && ReferenceEquals(existing, column))
            {
                _columnByKey.Remove(key);
            }
        }

        private void FindBoardHeaderElements()
        {
            if (_boardHeaderGrid != null)
            {
                _boardHeaderGrid.PointerEntered -= OnBoardHeaderPointerEntered;
                _boardHeaderGrid.PointerExited -= OnBoardHeaderPointerExited;
                _boardHeaderGrid.GotFocus -= OnBoardHeaderGotFocus;
                _boardHeaderGrid.LostFocus -= OnBoardHeaderLostFocus;
                _boardHeaderGrid.SizeChanged -= OnBoardHeaderSizeChanged;
            }

            // Find the board header grid
            _boardHeaderGrid = FindChild<Grid>(this, g => g.Name == "PART_BoardHeader");

            // Find the rename button
            _renameBoardButton = FindChild<DaisyButton>(this, btn => btn.Name == "PART_RenameBoardBtn");

            var searchRoot = _boardHeaderGrid != null ? (AvaloniaObject)_boardHeaderGrid : this;
            _boardTitleText = FindChild<TextBlock>(searchRoot, text => text.Name == "PART_BoardTitle");
            _boardMenuPopover = FindChild<DaisyPopover>(searchRoot, popover => popover.Name == "PART_BoardMenuPopover");
            _boardMenu = FindChild<DaisyMenu>(searchRoot, menu => menu.Name == "PART_BoardMenu");
            _boardSearchInput = FindChild<DaisyInput>(searchRoot, input => input.Name == "PART_SearchInput");
            _sidebarBorder = FindChild<Border>(this, b => b.Name == "PART_SidebarBorder");
            _statusBarBorder = FindChild<Border>(this, b => b.Name == "PART_StatusBarBorder");
            ApplySidebarAndStatusBarTheme();
            UpdateBoardMenuPopoverOffset();
            AttachBoardMenuEvents();
            if (_boardMenuPopover != null)
            {
                _boardMenuPopoverOpenSubscription = FlowKanbanProperty.Observe<bool>(
                    _boardMenuPopover,
                    DaisyPopover.IsOpenProperty,
                    OnBoardMenuPopoverOpenChanged);
            }

            // Wire up hover events on board header grid
            if (_boardHeaderGrid != null)
            {
                _boardHeaderGrid.PointerEntered += OnBoardHeaderPointerEntered;
                _boardHeaderGrid.PointerExited += OnBoardHeaderPointerExited;
                _boardHeaderGrid.GotFocus += OnBoardHeaderGotFocus;
                _boardHeaderGrid.LostFocus += OnBoardHeaderLostFocus;
                _boardHeaderGrid.SizeChanged += OnBoardHeaderSizeChanged;
            }
        }
        private void OnBoardHeaderSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateBoardMenuPopoverOffset();
        }

        private void UpdateBoardMenuPopoverOffset()
        {
            if (_boardMenuPopover == null || _boardTitleText == null)
                return;

            if (!_boardMenuPopover.IsLoaded || !_boardTitleText.IsLoaded)
                return;

            var titleTop = FlowKanbanVisualTree.TransformPoint(
                _boardTitleText,
                _boardMenuPopover,
                new Point(0, 0)).Y;

            if (double.IsNaN(titleTop) || double.IsInfinity(titleTop))
                return;

            var triggerHeight = _boardMenuPopover.Bounds.Height;
            if (triggerHeight <= 0 || double.IsNaN(triggerHeight) || double.IsInfinity(triggerHeight))
                return;

            _boardMenuPopover.VerticalOffset = titleTop - triggerHeight;
        }

        private void OnBoardHeaderPointerEntered(object? sender, PointerEventArgs e)
        {
            _isBoardHeaderPointerOver = true;
            ShowBoardHeaderButtons();
        }

        private void OnBoardHeaderPointerExited(object? sender, PointerEventArgs e)
        {
            _isBoardHeaderPointerOver = false;
            if (!_isBoardHeaderFocused)
            {
                HideBoardHeaderButtons();
            }
        }

        private void OnBoardHeaderGotFocus(object? sender, RoutedEventArgs e)
        {
            _isBoardHeaderFocused = true;
            ShowBoardHeaderButtons();
        }

        private void OnBoardHeaderLostFocus(object? sender, RoutedEventArgs e)
        {
            _isBoardHeaderFocused = false;
            if (!_isBoardHeaderPointerOver)
            {
                HideBoardHeaderButtons();
            }
        }

        private void OnBoardMenuPopoverOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                AttachBoardMenuEvents();
                ShowBoardHeaderButtons();
                return;
            }

            if (!_isBoardHeaderPointerOver && !_isBoardHeaderFocused)
            {
                HideBoardHeaderButtons();
            }
        }

        private void AttachBoardMenuEvents()
        {
            DetachBoardMenuEvents();

            EnsureBoardMenu();

            if (_boardMenu != null)
            {
                _boardMenu.AddHandler(MenuItem.ClickEvent, OnBoardMenuItemClicked);
            }
        }

        private void DetachBoardMenuEvents()
        {
            if (_boardMenu != null)
            {
                _boardMenu.RemoveHandler(MenuItem.ClickEvent, OnBoardMenuItemClicked);
            }
        }

        private void EnsureBoardMenu()
        {
            if (_boardMenuPopover == null)
                return;

            _boardMenu ??= _boardMenuPopover.PopoverContent as DaisyMenu;
            _boardMenu ??= FindChild<DaisyMenu>(_boardMenuPopover, menu => menu.Name == "PART_BoardMenu");
        }

        private void OnBoardMenuItemClicked(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not MenuItem item)
                return;

            if (_boardMenuPopover != null)
            {
                _boardMenuPopover.IsOpen = false;
            }

            if (string.Equals(item.Name, "PART_BoardMenuResizeItem", StringComparison.Ordinal))
            {
                IsColumnResizeEnabled = !IsColumnResizeEnabled;
            }

            if (_boardMenu != null)
            {
                _boardMenu.SelectedItem = null;
            }
        }

        private void AttachBoardLayoutTracking()
        {
            DetachBoardLayoutTracking();

            _boardContentHost = FindChild<Grid>(this, g => g.Name == "PART_BoardContentHost");
            if (_boardContentHost != null)
            {
                _boardContentHost.SizeChanged += OnBoardContentSizeChanged;
                if (_isColumnReorderActive)
                {
                    AttachBoardDragHandlers();
                }
            }

            _compactColumnsHost = FindChild<Border>(this, b => b.Name == "PART_CompactColumnsHost");
            _compactColumnSelect = FindChild<DaisySelect>(this, s => s.Name == "PART_CompactColumnSelect");
            _compactColumnPresenter = FindChild<ContentPresenter>(this, p => p.Name == "PART_CompactColumnPresenter");
            if (_compactColumnsHost != null)
            {
                _compactColumnsHost.SizeChanged += OnCompactColumnsHostSizeChanged;
            }

            UpdateCompactLayoutState();
            ApplyCompactColumnsScrollLock();
            UpdateSwimlaneCellSizing();
        }

        private void DetachBoardLayoutTracking()
        {
            if (_boardContentHost != null)
            {
                _boardContentHost.SizeChanged -= OnBoardContentSizeChanged;
                DetachBoardDragHandlers();
                _boardContentHost = null;
            }

            if (_compactColumnsHost != null)
            {
                _compactColumnsHost.SizeChanged -= OnCompactColumnsHostSizeChanged;
            }
            _compactColumnsHost = null;
            _compactColumnSelect = null;
            _compactColumnPresenter = null;
        }

        private void OnBoardContentSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateCompactLayoutState();
            ScheduleCompactColumnSizingUpdate();
            UpdateSwimlaneCellSizing();
        }

        private void UpdateSwimlaneCellSizing()
        {
            var availableHeight = _boardContentHost?.Bounds.Height ?? 0;
            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                return;

            SwimlaneCellMaxHeight = Math.Max(MinSwimlaneCellMaxHeight, availableHeight * SwimlaneCellViewportFraction);
        }

        private void OnCompactColumnsHostSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            ScheduleCompactColumnSizingUpdate();
        }

        private void AttachColumnResizeHandlers()
        {
            DetachColumnResizeHandlers();

            _standardColumnsHost = FindChild<FlowKanbanColumnsHost>(this, host => host.Name == "PART_ColumnsItemsControl");
            _swimlaneColumnsHost = FindChild<FlowKanbanColumnsHost>(this, host => host.Name == "PART_SwimlaneColumnsItemsControl");
            AttachColumnResizeHandlers(_standardColumnsHost);
            AttachColumnResizeHandlers(_swimlaneColumnsHost);
        }

        private void AttachColumnResizeHandlers(FlowKanbanColumnsHost? host)
        {
            if (host == null)
                return;

            host.ResizeDragStarted += OnColumnResizeDragStarted;
            host.ResizeDragCompleted += OnColumnResizeDragCompleted;
        }

        private void DetachColumnResizeHandlers()
        {
            DetachColumnResizeHandlers(_standardColumnsHost);
            DetachColumnResizeHandlers(_swimlaneColumnsHost);

            _standardColumnsHost = null;
            _swimlaneColumnsHost = null;
        }

        private void DetachColumnResizeHandlers(FlowKanbanColumnsHost? host)
        {
            if (host == null)
                return;

            host.ResizeDragStarted -= OnColumnResizeDragStarted;
            host.ResizeDragCompleted -= OnColumnResizeDragCompleted;
        }

        private void OnColumnResizeDragStarted(object? sender, EventArgs e)
        {
            _isColumnWidthDragActive = true;
            _columnWidthSavePending = false;
        }

        private void OnColumnResizeDragCompleted(object? sender, EventArgs e)
        {
            _isColumnWidthDragActive = false;

            if (_columnWidthSavePending)
            {
                TrySaveSettings(out _);
                _columnWidthSavePending = false;
            }
        }

        private void EndColumnResizeSessionIfNeeded()
        {
            if (!_isColumnWidthDragActive && !_columnWidthSavePending)
                return;

            _isColumnWidthDragActive = false;
            if (_columnWidthSavePending)
            {
                TrySaveSettings(out _);
                _columnWidthSavePending = false;
            }
        }

        private void UpdateCompactLayoutState()
        {
            if (!IsBoardViewActive)
            {
                SetCompactLayoutEnabled(false);
                return;
            }

            if (_compactLayoutOverride.HasValue)
            {
                SetCompactLayoutEnabled(_compactLayoutOverride.Value);
                return;
            }

            var availableWidth = GetDeterministicAvailableWidth();
            if (availableWidth <= 0 || double.IsNaN(availableWidth))
            {
                if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
                {
                    SetCompactLayoutEnabled(true);
                }
                return;
            }

            var threshold = GetCompactLayoutThresholdWidth();
            SetCompactLayoutEnabled(availableWidth < threshold);
        }

        internal void SetCompactColumnsScrollLock(bool isLocked)
        {
            if (isLocked)
            {
                _compactScrollLockCount++;
            }
            else if (_compactScrollLockCount > 0)
            {
                _compactScrollLockCount--;
            }

            ApplyCompactColumnsScrollLock();
        }

        private void ApplyCompactColumnsScrollLock()
        {
            var column = TryGetCompactSizingColumn();
            if (column == null)
                return;

            var scrollViewer = FindChild<ScrollViewer>(column);
            if (scrollViewer == null)
                return;

            var locked = _compactScrollLockCount > 0;
            scrollViewer.VerticalScrollBarVisibility = locked ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        }

        private double GetDeterministicAvailableWidth()
        {
            var mainWindow = FlowerySizeManager.MainWindow;
            return FlowKanbanLayoutMetrics.GetFirstFinitePositive(
                _boardContentHost?.Bounds.Width,
                TopLevel?.Bounds.Width,
                mainWindow?.Bounds.Width);
        }

        private double GetDeterministicAvailableHeight()
        {
            var mainWindow = FlowerySizeManager.MainWindow;
            return FlowKanbanLayoutMetrics.GetFirstFinitePositive(
                _compactColumnsHost?.Bounds.Height,
                _boardContentHost?.Bounds.Height,
                TopLevel?.Bounds.Height,
                mainWindow?.Bounds.Height);
        }

        private void SeedCompactColumnMaxHeight()
        {
            var availableHeight = GetCompactAvailableHeight();
            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                return;
            CompactColumnMaxHeight = Math.Max(0, availableHeight);
        }

        private void RequestCompactLayoutScrollRefresh()
        {
            var compactColumnsHost = _compactColumnsHost;
            if (compactColumnsHost == null)
                return;

            if (_compactScrollRefreshPending)
                return;

            _compactScrollRefreshPending = true;

            FlowKanbanDispatcher.Post(() =>
            {
                _compactScrollRefreshPending = false;
                if (!IsLoaded || !ReferenceEquals(_compactColumnsHost, compactColumnsHost))
                    return;

                _compactColumnPresenter?.InvalidateMeasure();
                compactColumnsHost.InvalidateMeasure();
                compactColumnsHost.UpdateLayout();
                UpdateCompactColumnSizing();
            });
        }

        private bool _compactSizingUpdatePending;

        private void ScheduleCompactColumnSizingUpdate()
        {
            if (_compactSizingUpdatePending)
                return;

            _compactSizingUpdatePending = true;

            FlowKanbanDispatcher.Post(() =>
            {
                _compactSizingUpdatePending = false;
                UpdateCompactColumnSizing();
            });
        }

        internal void RefreshLayoutAfterSettingsChange()
        {
            ScheduleCompactColumnSizingUpdate();

            if (IsCompactLayoutEnabled)
            {
                RequestCompactLayoutScrollRefresh();
            }
            else
            {
                _standardColumnsHost?.InvalidateMeasure();
                _swimlaneColumnsHost?.InvalidateMeasure();
                _boardContentHost?.InvalidateMeasure();
                _boardContentHost?.UpdateLayout();
            }
        }

        private void ClampCompactManualCardCount()
        {
            var clamped = Math.Clamp(CompactManualCardCount, 1, 20);
            if (clamped != CompactManualCardCount)
            {
                SetValue(CompactManualCardCountProperty, clamped);
            }
        }

        private void UpdateCompactColumnSizing()
        {
            if (!IsCompactLayoutEnabled || _compactColumnsHost == null)
            {
                CompactColumnMaxHeight = double.PositiveInfinity;
                return;
            }

            var availableHeight = GetCompactAvailableHeight();
            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                return;

            var column = TryGetCompactSizingColumn();
            if (column != null)
            {
                availableHeight -= column.Margin.Top + column.Margin.Bottom;
            }

            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                return;

            var maxHeight = availableHeight;
            if (CompactColumnSizingMode == FlowKanbanCompactColumnSizingMode.Manual && column != null)
            {
                var count = Math.Clamp(CompactManualCardCount, 1, 20);
                var spacing = FlowKanbanResources.GetCardSpacing(column.ColumnSize);
                var baseHeight = EstimateCompactColumnBaseHeight(column);
                var cardHeight = EstimateCompactCardHeight(column, availableHeight, baseHeight, spacing, count);
                if (cardHeight > 0)
                {
                    var cardsHeight = (cardHeight * count) + (spacing * Math.Max(0, count - 1));
                    var desiredHeight = baseHeight + cardsHeight;
                    maxHeight = Math.Min(availableHeight, desiredHeight);
                }
            }

            CompactColumnMaxHeight = Math.Max(0, maxHeight);
        }

        private double GetCompactAvailableHeight()
        {
            var availableHeight = _compactColumnsHost?.Bounds.Height ?? 0;
            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                availableHeight = GetDeterministicAvailableHeight();

            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                return 0;

            var padding = _compactColumnsHost?.Padding ?? new Thickness(CompactLayoutColumnPadding);
            availableHeight -= padding.Top + padding.Bottom;
            if (availableHeight <= 0 || double.IsNaN(availableHeight))
                return 0;

            if (_compactColumnSelect != null && _compactColumnSelect.Bounds.Height > 0 && !double.IsNaN(_compactColumnSelect.Bounds.Height))
            {
                var margin = _compactColumnSelect.Margin;
                availableHeight -= _compactColumnSelect.Bounds.Height + margin.Top + margin.Bottom;
            }

            return availableHeight;
        }

        private FlowKanbanColumn? TryGetCompactSizingColumn()
        {
            var compactColumn = TryGetCompactSizingColumnFromHost();
            if (compactColumn != null)
                return compactColumn;

            foreach (var column in _cachedColumnControls)
            {
                if (!column.IsLoaded || !column.IsVisible)
                    continue;

                if (!ReferenceEquals(column.ParentKanban, this))
                    continue;

                return column;
            }

            return null;
        }

        private FlowKanbanColumn? TryGetCompactSizingColumnFromHost()
        {
            if (_compactColumnPresenter != null)
            {
                var column = FindChild<FlowKanbanColumn>(_compactColumnPresenter);
                if (column is { IsLoaded: true, IsVisible: true })
                    return column;
            }

            if (_compactColumnsHost != null)
            {
                var column = FindChild<FlowKanbanColumn>(_compactColumnsHost);
                if (column is { IsLoaded: true, IsVisible: true })
                    return column;
            }

            return null;
        }

        private double EstimateCompactColumnBaseHeight(FlowKanbanColumn column)
        {
            var height = column.Padding.Top + column.Padding.Bottom;

            var header = FindChild<Grid>(column, g => g.Name == "PART_ColumnHeaderGrid");
            if (header is { IsVisible: true })
            {
                height += header.Bounds.Height + header.Margin.Top + header.Margin.Bottom;
            }

            var addTop = FindChild<FlowKanbanAddCard>(column, c => c.Name == "PART_AddCardTop");
            if (addTop is { IsVisible: true })
            {
                height += addTop.Bounds.Height + addTop.Margin.Top + addTop.Margin.Bottom;
            }

            var addBottom = FindChild<FlowKanbanAddCard>(column, c => c.Name == "PART_AddCardBottom");
            if (addBottom is { IsVisible: true })
            {
                height += addBottom.Bounds.Height + addBottom.Margin.Top + addBottom.Margin.Bottom;
            }

            return height;
        }

        private double EstimateCompactCardHeight(
            FlowKanbanColumn column,
            double availableHeight,
            double baseHeight,
            double spacing,
            int count)
        {
            var heights = new List<double>();
            foreach (var card in column.GetRealizedTaskCards())
            {
                if (card.Bounds.Height > 0 && !double.IsNaN(card.Bounds.Height))
                    heights.Add(card.Bounds.Height);

                if (heights.Count >= 6)
                    break;
            }

            if (heights.Count > 0)
                return heights.Average();

            var usable = availableHeight - baseHeight - (spacing * Math.Max(0, count - 1));
            if (usable <= 0)
                return 0;

            return usable / count;
        }

        private void SetCompactLayoutEnabled(bool value)
        {
            if (IsCompactLayoutEnabled == value)
                return;

            SetValue(IsCompactLayoutEnabledProperty, value);
        }

        private static double GetCompactLayoutThresholdWidth()
        {
            return (DefaultColumnWidth * CompactLayoutMinColumnCount)
                   + CompactLayoutColumnSpacing
                   + (CompactLayoutColumnPadding * 2);
        }

        private void ApplyLayoutMode()
        {
            if (IsCompactLayoutEnabled)
            {
                IsSwimlaneLayoutEnabled = false;
                IsStandardLayoutEnabled = false;
            }
            else
            {
                var enabled = IsLaneGroupingEnabled;
                IsSwimlaneLayoutEnabled = enabled;
                IsStandardLayoutEnabled = !enabled;
            }

            UpdateLayoutVisibilityState();
        }

        private void UpdateLayoutItemsSources()
        {
            var columns = _trackedBoard?.Columns;
            if (columns == null)
            {
                StandardColumnsSource = null;
                CompactColumnsSource = null;
                SwimlaneColumnsSource = null;
                CompactSelectedColumn = null;
                return;
            }

            StandardColumnsSource = IsStandardLayoutVisible ? columns : null;
            SwimlaneColumnsSource = IsSwimlaneLayoutVisible ? columns : null;

            var compactColumns = new List<FlowKanbanColumnData>();
            foreach (var column in columns)
            {
                if (column.IsArchiveColumnVisible)
                {
                    compactColumns.Add(column);
                }
            }

            CompactColumnsSource = IsCompactLayoutVisible ? compactColumns : null;
            EnsureCompactSelectedColumn(compactColumns);
        }

        private void EnsureCompactSelectedColumn(IReadOnlyList<FlowKanbanColumnData> columns)
        {
            if (columns.Count == 0)
            {
                if (CompactSelectedColumn != null)
                {
                    CompactSelectedColumn = null;
                }
                return;
            }

            var current = CompactSelectedColumn;
            if (current != null)
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    if (ReferenceEquals(columns[i], current))
                        return;
                }
            }

            CompactSelectedColumn = columns[0];
        }

        private void UpdateColumnReorderState()
        {
            var shouldEnable = !IsCompactLayoutEnabled;
            if (_isColumnReorderActive == shouldEnable)
                return;

            _isColumnReorderActive = shouldEnable;
            if (shouldEnable)
            {
                AttachColumnDragHandlers();
                AttachBoardDragHandlers();
            }
            else
            {
                DetachColumnDragHandlers();
                DetachBoardDragHandlers();
                HideColumnDropIndicator();
            }
        }

        private void UpdateLaneGroupingState()
        {
            ApplyLayoutMode();
            UpdateLaneRows();
        }

        private void UpdateLaneRows()
        {
            var groupingEnabled = IsLaneGroupingEnabled;

            if (_trackedBoard == null || !groupingEnabled || IsCompactLayoutEnabled)
            {
                if (LaneRows.Count > 0)
                {
                    LaneRows = new ObservableCollection<FlowKanbanLaneRowView>();
                }
                return;
            }

            var columns = _trackedBoard.Columns;
            var desiredLanes = new List<FlowKanbanLane>();

            if (HasUnassignedTasks(_trackedBoard))
            {
                desiredLanes.Add(new FlowKanbanLane
                {
                    Id = UnassignedLaneId,
                    Title = FloweryLocalization.GetString("Kanban_Lanes_Unassigned")
                });
            }

            foreach (var lane in _trackedBoard.Lanes)
            {
                if (string.IsNullOrWhiteSpace(lane.Id))
                    continue;

                desiredLanes.Add(lane);
            }

            // Task moves don't change the lane/column structure. Keeping the
            // existing row/cell views avoids re-templating the whole swimlane
            // board on every collection change; only the WIP badges need a poke.
            if (LaneRowsMatch(LaneRows, desiredLanes, columns))
            {
                foreach (var row in LaneRows)
                {
                    foreach (var cell in row.Cells)
                    {
                        cell.RefreshWipState();
                    }
                }
                return;
            }

            var rows = new ObservableCollection<FlowKanbanLaneRowView>();
            foreach (var lane in desiredLanes)
            {
                rows.Add(BuildLaneRow(lane, columns, lane.Id));
            }

            LaneRows = rows;
        }

        private static bool LaneRowsMatch(
            ObservableCollection<FlowKanbanLaneRowView> currentRows,
            List<FlowKanbanLane> desiredLanes,
            ObservableCollection<FlowKanbanColumnData> columns)
        {
            if (currentRows.Count != desiredLanes.Count)
                return false;

            for (var i = 0; i < desiredLanes.Count; i++)
            {
                var row = currentRows[i];
                var lane = desiredLanes[i];

                var laneMatches = IsUnassignedLaneId(lane.Id)
                    ? IsUnassignedLaneId(row.Lane.Id)
                    : ReferenceEquals(row.Lane, lane);
                if (!laneMatches)
                    return false;

                if (row.Cells.Count != columns.Count)
                    return false;

                for (var j = 0; j < columns.Count; j++)
                {
                    var cell = row.Cells[j];
                    if (!ReferenceEquals(cell.Column, columns[j])
                        || !string.Equals(cell.LaneId, lane.Id, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static FlowKanbanLaneRowView BuildLaneRow(
            FlowKanbanLane lane,
            ObservableCollection<FlowKanbanColumnData> columns,
            string? laneId)
        {
            var cells = new ObservableCollection<FlowKanbanLaneCellView>();
            foreach (var column in columns)
            {
                cells.Add(new FlowKanbanLaneCellView(column, laneId));
            }

            return new FlowKanbanLaneRowView(lane, cells);
        }

        private static bool HasUnassignedTasks(FlowKanbanData board)
        {
            var validLaneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lane in board.Lanes)
            {
                if (!string.IsNullOrWhiteSpace(lane.Id))
                    validLaneIds.Add(lane.Id);
            }

            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (IsUnassignedLaneId(task.LaneId))
                        return true;

                    if (!string.IsNullOrWhiteSpace(task.LaneId) && !validLaneIds.Contains(task.LaneId))
                        return true;
                }
            }

            return false;
        }

        private void ApplySidebarAndStatusBarTheme()
        {
            var base100 = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
            var base300 = DaisyResourceLookup.GetBrush("DaisyBase300Brush");

            if (_sidebarBorder != null)
            {
                _sidebarBorder.Background = base100;
                _sidebarBorder.BorderBrush = base300;
            }

            if (_statusBarBorder != null)
            {
                _statusBarBorder.Background = base100;
                _statusBarBorder.BorderBrush = base300;
            }
        }

        internal void ResolveThemeRefreshTemplateParts(INameScope nameScope)
        {
            _laneRowsHost = nameScope.Find<ItemsControl>("PART_LaneRowsHost");
        }

        private void ApplySwimlaneLaneHeaderTheme()
        {
            if (_laneRowsHost == null)
                return;

            var base200 = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            var base300 = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
            var baseContent = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");

            var count = _laneRowsHost.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (_laneRowsHost.ContainerFromIndex(i) is not AvaloniaObject container)
                    continue;

                var border = FindLaneHeaderBorder(container, maxDepth: 4);
                if (border == null || !border.IsLoaded)
                    continue;

                if (base200 != null)
                    border.Background = base200;
                if (base300 != null)
                    border.BorderBrush = base300;

                if (border.Child is TextBlock textBlock && baseContent != null)
                    textBlock.Foreground = baseContent;
            }
        }

        private static Border? FindLaneHeaderBorder(AvaloniaObject root, int maxDepth)
        {
            // Container -> PART_LaneRow grid -> PART_LaneHeaderBorder; the depth limit
            // keeps the lookup from descending into the lane's task cards.
            return FlowKanbanVisualTree.FindDescendant<Border>(
                root,
                border => string.Equals(border.Name, "PART_LaneHeaderBorder", StringComparison.Ordinal),
                maxDepth);
        }

        private void RefreshTaskCardThemes()
        {
            foreach (var column in _cachedColumnControls)
            {
                if (!column.IsLoaded || !column.IsVisible)
                    continue;

                foreach (var card in column.GetRealizedTaskCards())
                {
                    card.RefreshTheme();
                }
            }
        }

        private void ScheduleThemeRefresh()
        {
            _themeRefreshTimer ??= FlowKanbanDispatcher.CreateTimer(OnThemeRefreshTimerTick);
            _themeRefreshTimer.Stop();
            _themeRefreshTimer.Interval = TimeSpan.FromMilliseconds(ThemeRefreshDebounceMilliseconds);
            _themeRefreshTimer.Start();
        }

        private void OnThemeRefreshTimerTick(object? sender, EventArgs e)
        {
            _themeRefreshTimer?.Stop();
            RefreshTaskCardThemes();
        }

        private void ShowBoardHeaderButtons()
        {
            const double targetOpacity = 0.7;
            if (_renameBoardButton != null)
            {
                _renameBoardButton.IsHitTestVisible = true;
                _renameBoardButton.Opacity = targetOpacity;
            }
        }

        private void HideBoardHeaderButtons()
        {
            if (_renameBoardButton != null)
            {
                _renameBoardButton.Opacity = 0;
                _renameBoardButton.IsHitTestVisible = false;
            }
        }

        private T? FindChild<T>(AvaloniaObject parent, Func<T, bool>? predicate = null) where T : AvaloniaObject
        {
            return FlowKanbanVisualTree.FindDescendant(parent, predicate);
        }

        #region Zoom Methods
        private static readonly DaisySize[] SizeOrder =
        {
            DaisySize.ExtraSmall,
            DaisySize.Small,
            DaisySize.Medium,
            DaisySize.Large,
            DaisySize.ExtraLarge
        };

        private bool CanZoomIn() => Array.IndexOf(SizeOrder, BoardSize) < SizeOrder.Length - 1;

        private bool CanZoomOut() => Array.IndexOf(SizeOrder, BoardSize) > 0;

        private void ExecuteZoomIn()
        {
            var currentIndex = Array.IndexOf(SizeOrder, BoardSize);
            if (currentIndex < SizeOrder.Length - 1)
            {
                BoardSize = SizeOrder[currentIndex + 1];
            }
        }

        private void ExecuteZoomOut()
        {
            var currentIndex = Array.IndexOf(SizeOrder, BoardSize);
            if (currentIndex > 0)
            {
                BoardSize = SizeOrder[currentIndex - 1];
            }
        }

        private void NotifyZoomCommandsChanged()
        {
            if (ZoomInCommand is RelayCommand zoomIn)
                zoomIn.RaiseCanExecuteChanged();
            if (ZoomOutCommand is RelayCommand zoomOut)
                zoomOut.RaiseCanExecuteChanged();
        }
        #endregion
    }
}
