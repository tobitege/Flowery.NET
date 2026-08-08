using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Flowery.Helpers;
using Flowery.Localization;
using Flowery.Services;
using Flowery.NET.Kanban.Interfaces;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// A Kanban board container that manages columns and task cards.
    /// Supports JSON persistence and column rearrangement.
    /// </summary>
    public partial class FlowKanban : FlowKanbanContentControl
    {
        static FlowKanban()
        {
            FilterCriteriaProperty.Changed.AddClassHandler<FlowKanban>(OnFilterCriteriaChanged);
            FilterShowOverdueProperty.Changed.AddClassHandler<FlowKanban>(OnFilterShowOverdueChanged);
            FilterShowBlockedProperty.Changed.AddClassHandler<FlowKanban>(OnFilterShowBlockedChanged);
            FilterPriorityLowProperty.Changed.AddClassHandler<FlowKanban>(OnFilterPriorityLowChanged);
            FilterPriorityNormalProperty.Changed.AddClassHandler<FlowKanban>(OnFilterPriorityNormalChanged);
            FilterPriorityHighProperty.Changed.AddClassHandler<FlowKanban>(OnFilterPriorityHighChanged);
            FilterPriorityUrgentProperty.Changed.AddClassHandler<FlowKanban>(OnFilterPriorityUrgentChanged);
            FilterDateFromProperty.Changed.AddClassHandler<FlowKanban>(OnFilterDateFromChanged);
            FilterDateToProperty.Changed.AddClassHandler<FlowKanban>(OnFilterDateToChanged);
            FilterDueTodayProperty.Changed.AddClassHandler<FlowKanban>(OnFilterDueTodayChanged);
            FilterDueThisWeekProperty.Changed.AddClassHandler<FlowKanban>(OnFilterDueThisWeekChanged);
            SelectedAssigneeFilterProperty.Changed.AddClassHandler<FlowKanban>(OnSelectedAssigneeFilterChanged);
            AssigneeAdapterProperty.Changed.AddClassHandler<FlowKanban>(OnAssigneeAdapterChanged);
            IsCompactLayoutEnabledProperty.Changed.AddClassHandler<FlowKanban>(OnCompactLayoutEnabledChanged);
            CompactSelectedColumnProperty.Changed.AddClassHandler<FlowKanban>(OnCompactSelectedColumnChanged);
            BoardSizeProperty.Changed.AddClassHandler<FlowKanban>(OnBoardSizeChanged);
            ColumnWidthProperty.Changed.AddClassHandler<FlowKanban>(OnColumnWidthChanged);
            MinColumnWidthProperty.Changed.AddClassHandler<FlowKanban>(OnColumnWidthBoundsChanged);
            MaxColumnWidthProperty.Changed.AddClassHandler<FlowKanban>(OnColumnWidthBoundsChanged);
            IsColumnResizeEnabledProperty.Changed.AddClassHandler<FlowKanban>(OnColumnResizeEnabledChanged);
            IsStatusBarVisibleProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            BoardProperty.Changed.AddClassHandler<FlowKanban>(OnBoardChanged);
            EnableUndoRedoProperty.Changed.AddClassHandler<FlowKanban>(OnEnableUndoRedoChanged);
            SearchTextProperty.Changed.AddClassHandler<FlowKanban>(OnSearchTextChanged);
            SelectedBulkMoveColumnProperty.Changed.AddClassHandler<FlowKanban>(OnSelectedBulkMoveColumnChanged);
            ConfirmColumnRemovalsProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            ConfirmCardRemovalsProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            AutoSaveAfterEditsProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            AutoExpandCardDetailsProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            AddCardPlacementProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            CompactColumnSizingModeProperty.Changed.AddClassHandler<FlowKanban>(OnCompactColumnSizingChanged);
            CompactManualCardCountProperty.Changed.AddClassHandler<FlowKanban>(OnCompactColumnSizingChanged);
            ShowWelcomeMessageProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            WelcomeMessageTitleProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            WelcomeMessageSubtitleProperty.Changed.AddClassHandler<FlowKanban>(OnSettingChanged);
            CurrentViewProperty.Changed.AddClassHandler<FlowKanban>(OnCurrentViewChanged);
        }

        /// <summary>
        /// Raised when the board size changes.
        /// </summary>
        public event EventHandler<DaisySize>? BoardSizeChanged;

        /// <summary>
        /// Raised when the column width changes.
        /// </summary>
        public event EventHandler<double>? ColumnWidthChanged;

        /// <summary>
        /// Raised when the Kanban switches between compact and standard layouts.
        /// </summary>
        public event EventHandler<bool>? CompactLayoutChanged;

        /// <summary>
        /// Raised when a drag operation ends (completed or cancelled).
        /// Columns should clean up their drop indicators when this fires.
        /// </summary>
        public event EventHandler? DragEnded;

        /// <summary>
        /// Raised before a card is moved. Handlers can cancel the move by setting Cancel = true.
        /// </summary>
        public event EventHandler<CardMovingEventArgs>? CardMoving;

        /// <summary>
        /// Raised after a card has been moved to a new location.
        /// </summary>
        public event EventHandler<CardMovedEventArgs>? CardMoved;

        /// <summary>
        /// Raised when a card's properties are edited.
        /// </summary>
        public event EventHandler<FlowTask>? CardEdited;

        /// <summary>
        /// Raised when a column's properties are edited.
        /// </summary>
        public event EventHandler<FlowKanbanColumnData>? ColumnEdited;

        /// <summary>
        /// Raised when board-level properties are edited.
        /// </summary>
        public event EventHandler<BoardEditedEventArgs>? BoardEdited;

        /// <summary>
        /// Raised when lane grouping or lane definitions change.
        /// </summary>
        public event EventHandler? LaneGroupingChanged;

        /// <summary>
        /// Raised when the search filter changes (text or task match state).
        /// </summary>
        public event EventHandler? SearchFilterChanged;

        /// <summary>
        /// Raised when a persistence operation fails.
        /// </summary>
        public event EventHandler<FlowKanbanPersistenceFailedEventArgs>? PersistenceFailed;

        internal const double DefaultColumnWidth = 280;
        internal const double DefaultMinColumnWidth = 100;
        internal const double DefaultMaxColumnWidth = 380;
        internal const string UnassignedLaneId = "__unassigned__";

        internal void ReportPersistenceFailure(FlowKanbanPersistenceOperation operation, Exception error)
        {
            ShowPersistenceError(error);
            PersistenceFailed?.Invoke(this, new FlowKanbanPersistenceFailedEventArgs(operation, error));
        }

        internal static string? NormalizeLaneId(string? laneId)
        {
            return string.IsNullOrWhiteSpace(laneId) ? null : laneId.Trim();
        }

        internal static bool IsUnassignedLaneId(string? laneId)
        {
            if (string.IsNullOrWhiteSpace(laneId))
                return true;

            var trimmed = laneId.Trim();
            return string.Equals(trimmed, "0", StringComparison.Ordinal)
                   || string.Equals(trimmed, UnassignedLaneId, StringComparison.Ordinal);
        }

        internal bool RaiseCardMoving(CardMovingEventArgs args)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args));

            CardMoving?.Invoke(this, args);
            return args.Cancel;
        }

        internal void RaiseCardMoved(CardMovedEventArgs args)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args));

            CardMoved?.Invoke(this, args);
        }

        private ObservableCollection<string>? _trackedTags;
        private ObservableCollection<FlowKanbanLane>? _trackedLanes;
        private readonly FlowKanbanCommandHistory _commandHistory = new();
        private IBoardStore _boardStore = new FlowKanbanBoardStore(StateStorageProvider.Instance);
        private readonly List<ItemsControl> _columnItemsControls = new();
        private readonly Dictionary<ItemsControl, Canvas> _columnDropLayers = new();
        private Rectangle? _columnDropIndicator;
        private int _currentColumnDropIndex = -1;
        private bool _isApplyingSearchFilter;

        internal const string ColumnDragDataFormat = "flowery.kanban-column";
        internal const string ColumnDragDataPropertyKey = "flowery.kanban-column-id";
        internal const string TaskDragDataPropertyKey = "flowery.kanban-task-id";
        internal static readonly DataFormat<string> ColumnDragFormat =
            DataFormat.CreateStringApplicationFormat(ColumnDragDataPropertyKey);
        internal static readonly DataFormat<string> TaskDragFormat =
            DataFormat.CreateStringApplicationFormat(TaskDragDataPropertyKey);
        private static readonly FilePickerFileType JsonFileType = new("JSON")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"],
            AppleUniformTypeIdentifiers = ["public.json"]
        };

        public FloweryLocalization Localization { get; } = FloweryLocalization.Instance;
        public FlowKanbanCommandHistory CommandHistory => _commandHistory;
        public IBoardStore BoardStore
        {
            get => _boardStore;
            set => _boardStore = value ?? throw new ArgumentNullException(nameof(value));
        }
        internal bool IsLaneGroupingEnabled => Board.GroupBy == FlowKanbanGroupBy.Lane && Board.Lanes.Count > 0;

        public FlowKanban()
        {
            LaneRows = new ObservableCollection<FlowKanbanLaneRowView>();
            Boards = new ObservableCollection<FlowBoardMetadata>();
            SetCurrentValue(BoardProperty, new FlowKanbanData());

            AddColumnCommand = new RelayCommand(ExecuteAddColumn, CanExecuteColumnOperation);
            SaveCommand = new RelayCommand(ExecuteSave);
            LoadCommand = new RelayCommand(ExecuteLoad);
            SaveToFileCommand = new RelayCommand(ExecuteSaveToFile, CanExecuteFilePickerCommand);
            LoadFromFileCommand = new RelayCommand(ExecuteLoadFromFile, CanExecuteFilePickerCommand);
            SettingsCommand = new RelayCommand(ExecuteOpenSettings);
            ShowHomeCommand = new RelayCommand(ExecuteShowHome);
            OpenBoardCommand = new RelayCommand<FlowBoardMetadata>(ExecuteOpenBoard);
            CreateBoardCommand = new RelayCommand(ExecuteCreateBoard);
            CreateDemoBoardCommand = new RelayCommand(ExecuteCreateDemoBoard);
            RenameBoardHomeCommand = new RelayCommand<FlowBoardMetadata>(ExecuteRenameBoard);
            DeleteBoardCommand = new RelayCommand<FlowBoardMetadata>(ExecuteDeleteBoard);
            DuplicateBoardCommand = new RelayCommand<FlowBoardMetadata>(ExecuteDuplicateBoard);
            ExportBoardCommand = new RelayCommand<FlowBoardMetadata>(ExecuteExportBoard);
            AddCardCommand = new RelayCommand<FlowKanbanColumnData>(ExecuteAddCard);
            QuickAddCardCommand = new RelayCommand(ExecuteQuickAddCard);
            RemoveCardCommand = new RelayCommand<FlowTask>(ExecuteRemoveCard);
            RemoveColumnCommand = new RelayCommand<FlowKanbanColumnData>(
                ExecuteRemoveColumn,
                CanExecuteColumnOperation);
            EditColumnCommand = new RelayCommand<FlowKanbanColumnData>(
                ExecuteEditColumn,
                CanExecuteColumnOperation);
            ToggleColumnCollapseCommand = new RelayCommand<FlowKanbanColumnData>(ExecuteToggleColumnCollapse);
            ZoomInCommand = new RelayCommand(ExecuteZoomIn, CanZoomIn);
            ZoomOutCommand = new RelayCommand(ExecuteZoomOut, CanZoomOut);
            ToggleStatusBarCommand = new RelayCommand(() => IsStatusBarVisible = !IsStatusBarVisible);
            ToggleArchiveColumnVisibilityCommand = new RelayCommand(ExecuteToggleArchiveColumnVisibility);
            ShowKeyboardHelpCommand = new RelayCommand(ExecuteShowKeyboardHelp);
            ShowMetricsCommand = new RelayCommand(ExecuteShowMetrics);
            EditBoardCommand = new RelayCommand(ExecuteEditBoard);
            RenameBoardCommand = new RelayCommand(ExecuteRenameBoard);
            UndoCommand = new RelayCommand(() => _commandHistory.Undo(), () => EnableUndoRedo && _commandHistory.CanUndo);
            RedoCommand = new RelayCommand(() => _commandHistory.Redo(), () => EnableUndoRedo && _commandHistory.CanRedo);
            BulkMoveCommand = new RelayCommand<FlowKanbanColumnData?>(ExecuteBulkMove, CanExecuteBulkMove);
            BulkSetPriorityCommand = new RelayCommand<string?>(ExecuteBulkSetPriority, _ => HasSelection);
            BulkSetTagsCommand = new RelayCommand<string?>(ExecuteBulkSetTags, _ => HasSelection);
            BulkSetDueDateCommand = new RelayCommand<DateTimeOffset?>(ExecuteBulkSetDueDate, _ => HasSelection);
            BulkArchiveCommand = new RelayCommand(ExecuteBulkArchive, () => HasSelection);
            BulkDeleteCommand = new RelayCommand(ExecuteBulkDelete, () => HasSelection);
            ClearSelectionCommand = new RelayCommand(DeselectAllTasks, () => HasSelection);
            _commandHistory.PropertyChanged += OnCommandHistoryChanged;
            InitializeFilterbar();
            Loaded += OnKanbanLoaded;
            Unloaded += OnKanbanUnloaded;
        }

        #region Board
        public static readonly StyledProperty<FlowKanbanData> BoardProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanData>(
                nameof(Board),
                defaultValue: null!,
                coerce: static (_, value) => value ?? new FlowKanbanData());

        public FlowKanbanData Board
        {
            get => (FlowKanbanData)GetValue(BoardProperty);
            set => SetValue(BoardProperty, value ?? throw new ArgumentNullException(nameof(value)));
        }

        private static void OnBoardChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.AttachBoardTracking(e.NewValue as FlowKanbanData);
                _ = ObserveAssigneeRefreshAsync(kanban.RefreshAssigneesAsync());
            }
        }
        #endregion

        #region Commands
        public static readonly StyledProperty<ICommand> AddColumnCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(AddColumnCommand),
                default!);

        public ICommand AddColumnCommand
        {
            get => (ICommand)GetValue(AddColumnCommandProperty);
            set => SetValue(AddColumnCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> SaveCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(SaveCommand),
                default!);

        public ICommand SaveCommand
        {
            get => (ICommand)GetValue(SaveCommandProperty);
            set => SetValue(SaveCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> LoadCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(LoadCommand),
                default!);

        public ICommand LoadCommand
        {
            get => (ICommand)GetValue(LoadCommandProperty);
            set => SetValue(LoadCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> SaveToFileCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(SaveToFileCommand),
                default!);

        public ICommand SaveToFileCommand
        {
            get => (ICommand)GetValue(SaveToFileCommandProperty);
            set => SetValue(SaveToFileCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> LoadFromFileCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(LoadFromFileCommand),
                default!);

        public ICommand LoadFromFileCommand
        {
            get => (ICommand)GetValue(LoadFromFileCommandProperty);
            set => SetValue(LoadFromFileCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> SettingsCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(SettingsCommand),
                default!);

        public ICommand SettingsCommand
        {
            get => (ICommand)GetValue(SettingsCommandProperty);
            set => SetValue(SettingsCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> AddCardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(AddCardCommand),
                default!);

        public ICommand AddCardCommand
        {
            get => (ICommand)GetValue(AddCardCommandProperty);
            set => SetValue(AddCardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> QuickAddCardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(QuickAddCardCommand),
                default!);

        public ICommand QuickAddCardCommand
        {
            get => (ICommand)GetValue(QuickAddCardCommandProperty);
            set => SetValue(QuickAddCardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> RemoveCardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(RemoveCardCommand),
                default!);

        public ICommand RemoveCardCommand
        {
            get => (ICommand)GetValue(RemoveCardCommandProperty);
            set => SetValue(RemoveCardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> RemoveColumnCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(RemoveColumnCommand),
                default!);

        public ICommand RemoveColumnCommand
        {
            get => (ICommand)GetValue(RemoveColumnCommandProperty);
            set => SetValue(RemoveColumnCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> EditColumnCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(EditColumnCommand),
                default!);

        /// <summary>
        /// Command invoked when the edit column action is requested.
        /// The command parameter is the FlowKanbanColumnData to edit.
        /// </summary>
        public ICommand EditColumnCommand
        {
            get => (ICommand)GetValue(EditColumnCommandProperty);
            set => SetValue(EditColumnCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ToggleColumnCollapseCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ToggleColumnCollapseCommand),
                default!);

        public ICommand ToggleColumnCollapseCommand
        {
            get => (ICommand)GetValue(ToggleColumnCollapseCommandProperty);
            set => SetValue(ToggleColumnCollapseCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> EditCardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(EditCardCommand),
                default!);

        /// <summary>
        /// Command invoked when a card is double-tapped for editing.
        /// The command parameter is the FlowTask to edit.
        /// </summary>
        public ICommand EditCardCommand
        {
            get => (ICommand)GetValue(EditCardCommandProperty);
            set => SetValue(EditCardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ZoomInCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ZoomInCommand),
                default!);

        /// <summary>
        /// Command to zoom in (increase board size).
        /// </summary>
        public ICommand ZoomInCommand
        {
            get => (ICommand)GetValue(ZoomInCommandProperty);
            set => SetValue(ZoomInCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ZoomOutCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ZoomOutCommand),
                default!);

        /// <summary>
        /// Command to zoom out (decrease board size).
        /// </summary>
        public ICommand ZoomOutCommand
        {
            get => (ICommand)GetValue(ZoomOutCommandProperty);
            set => SetValue(ZoomOutCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ToggleStatusBarCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ToggleStatusBarCommand),
                default!);

        public ICommand ToggleStatusBarCommand
        {
            get => (ICommand)GetValue(ToggleStatusBarCommandProperty);
            set => SetValue(ToggleStatusBarCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ToggleArchiveColumnVisibilityCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ToggleArchiveColumnVisibilityCommand),
                default!);

        public ICommand ToggleArchiveColumnVisibilityCommand
        {
            get => (ICommand)GetValue(ToggleArchiveColumnVisibilityCommandProperty);
            set => SetValue(ToggleArchiveColumnVisibilityCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ShowKeyboardHelpCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ShowKeyboardHelpCommand),
                default!);

        public ICommand ShowKeyboardHelpCommand
        {
            get => (ICommand)GetValue(ShowKeyboardHelpCommandProperty);
            set => SetValue(ShowKeyboardHelpCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ShowMetricsCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ShowMetricsCommand),
                default!);

        public ICommand ShowMetricsCommand
        {
            get => (ICommand)GetValue(ShowMetricsCommandProperty);
            set => SetValue(ShowMetricsCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> EditBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(EditBoardCommand),
                default!);

        /// <summary>
        /// Command to open the board editor dialog.
        /// </summary>
        public ICommand EditBoardCommand
        {
            get => (ICommand)GetValue(EditBoardCommandProperty);
            set => SetValue(EditBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> RenameBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(RenameBoardCommand),
                default!);

        /// <summary>
        /// Command to rename the board title inline (similar to column rename).
        /// </summary>
        public ICommand RenameBoardCommand
        {
            get => (ICommand)GetValue(RenameBoardCommandProperty);
            set => SetValue(RenameBoardCommandProperty, value);
        }

        public static readonly StyledProperty<bool> EnableUndoRedoProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(EnableUndoRedo),
                                false);

        /// <summary>
        /// Enables undo/redo command history for Kanban operations.
        /// </summary>
        public bool EnableUndoRedo
        {
            get => (bool)GetValue(EnableUndoRedoProperty);
            set => SetValue(EnableUndoRedoProperty, value);
        }

        public static readonly StyledProperty<ICommand> UndoCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(UndoCommand),
                default!);

        /// <summary>
        /// Command to undo the last Kanban operation.
        /// </summary>
        public ICommand UndoCommand
        {
            get => (ICommand)GetValue(UndoCommandProperty);
            set => SetValue(UndoCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> RedoCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(RedoCommand),
                default!);

        /// <summary>
        /// Command to redo the last undone Kanban operation.
        /// </summary>
        public ICommand RedoCommand
        {
            get => (ICommand)GetValue(RedoCommandProperty);
            set => SetValue(RedoCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> BulkMoveCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(BulkMoveCommand),
                default!);

        public ICommand BulkMoveCommand
        {
            get => (ICommand)GetValue(BulkMoveCommandProperty);
            set => SetValue(BulkMoveCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> BulkSetPriorityCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(BulkSetPriorityCommand),
                default!);

        public ICommand BulkSetPriorityCommand
        {
            get => (ICommand)GetValue(BulkSetPriorityCommandProperty);
            set => SetValue(BulkSetPriorityCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> BulkSetTagsCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(BulkSetTagsCommand),
                default!);

        public ICommand BulkSetTagsCommand
        {
            get => (ICommand)GetValue(BulkSetTagsCommandProperty);
            set => SetValue(BulkSetTagsCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> BulkSetDueDateCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(BulkSetDueDateCommand),
                default!);

        public ICommand BulkSetDueDateCommand
        {
            get => (ICommand)GetValue(BulkSetDueDateCommandProperty);
            set => SetValue(BulkSetDueDateCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> BulkArchiveCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(BulkArchiveCommand),
                default!);

        public ICommand BulkArchiveCommand
        {
            get => (ICommand)GetValue(BulkArchiveCommandProperty);
            set => SetValue(BulkArchiveCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> BulkDeleteCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(BulkDeleteCommand),
                default!);

        public ICommand BulkDeleteCommand
        {
            get => (ICommand)GetValue(BulkDeleteCommandProperty);
            set => SetValue(BulkDeleteCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ClearSelectionCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ClearSelectionCommand),
                default!);

        public ICommand ClearSelectionCommand
        {
            get => (ICommand)GetValue(ClearSelectionCommandProperty);
            set => SetValue(ClearSelectionCommandProperty, value);
        }
        #endregion

        #region SearchText
        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<FlowKanban, string>(
                                nameof(SearchText),
                                string.Empty);

        /// <summary>
        /// Search text used to filter task cards.
        /// </summary>
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        private static void OnSearchTextChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.SyncCriteriaTextFromSearch();
                kanban.ApplySearchFilter();
            }
        }
        #endregion

        #region Selection
        public static readonly StyledProperty<int> SelectedCountProperty =
            AvaloniaProperty.Register<FlowKanban, int>(
                nameof(SelectedCount),
                0);

        public int SelectedCount
        {
            get => (int)GetValue(SelectedCountProperty);
            private set => SetValue(SelectedCountProperty, value);
        }

        public static readonly StyledProperty<bool> HasSelectionProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(HasSelection),
                false);

        public bool HasSelection
        {
            get => (bool)GetValue(HasSelectionProperty);
            private set => SetValue(HasSelectionProperty, value);
        }

        public static readonly StyledProperty<FlowKanbanColumnData?> SelectedBulkMoveColumnProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanColumnData?>(
                                nameof(SelectedBulkMoveColumn),
                                default!);

        public FlowKanbanColumnData? SelectedBulkMoveColumn
        {
            get => (FlowKanbanColumnData?)GetValue(SelectedBulkMoveColumnProperty);
            set => SetValue(SelectedBulkMoveColumnProperty, value);
        }

        private static void OnSelectedBulkMoveColumnChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.NotifySelectionCommandsChanged();
            }
        }
        #endregion

        #region Persistence Keys
        private const string SettingsStorageKey = "kanban.settings";
        #endregion

        #region Settings
        private const int AutoSaveDebounceMilliseconds = 800;
        private const int DoneAgingCheckIntervalMinutes = 60;

        private bool _settingsLoaded;
        private bool _isApplyingSettings;
        private bool _suppressAutoSave;
        private string? _lastBoardId;
        private FlowKanbanData? _trackedBoard;
        private ObservableCollection<FlowKanbanColumnData>? _trackedColumnsCollection;
        private readonly HashSet<FlowKanbanColumnData> _trackedColumns = new();
        private readonly HashSet<FlowTask> _trackedTasks = new();
        private readonly HashSet<FlowSubtask> _trackedSubtasks = new();
        private readonly HashSet<FlowKanbanLane> _trackedLaneItems = new();
        private readonly Dictionary<FlowKanbanColumnData, ObservableCollection<FlowTask>> _trackedTaskCollections = new();
        private readonly Dictionary<FlowTask, ObservableCollection<FlowSubtask>> _trackedSubtaskCollections = new();
        private DispatcherTimer? _autoSaveTimer;
        private ManagedTimer? _doneAgingTimer;

        /// <summary>
        /// Last board ID loaded or saved via persistence.
        /// </summary>
        public string? LastBoardId => _lastBoardId;

        public static readonly StyledProperty<bool> ConfirmColumnRemovalsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(ConfirmColumnRemovals),
                                true);

        /// <summary>
        /// Whether to confirm before removing columns.
        /// </summary>
        public bool ConfirmColumnRemovals
        {
            get => (bool)GetValue(ConfirmColumnRemovalsProperty);
            set => SetValue(ConfirmColumnRemovalsProperty, value);
        }

        public static readonly StyledProperty<bool> ConfirmCardRemovalsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(ConfirmCardRemovals),
                                true);

        /// <summary>
        /// Whether to confirm before removing cards.
        /// </summary>
        public bool ConfirmCardRemovals
        {
            get => (bool)GetValue(ConfirmCardRemovalsProperty);
            set => SetValue(ConfirmCardRemovalsProperty, value);
        }

        public static readonly StyledProperty<bool> AutoSaveAfterEditsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(AutoSaveAfterEdits),
                                true);

        /// <summary>
        /// Whether the board auto-saves after edits.
        /// </summary>
        public bool AutoSaveAfterEdits
        {
            get => (bool)GetValue(AutoSaveAfterEditsProperty);
            set => SetValue(AutoSaveAfterEditsProperty, value);
        }

        public static readonly StyledProperty<bool> AutoExpandCardDetailsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(AutoExpandCardDetails),
                                true);

        /// <summary>
        /// Whether the task editor expands all sections by default.
        /// </summary>
        public bool AutoExpandCardDetails
        {
            get => (bool)GetValue(AutoExpandCardDetailsProperty);
            set => SetValue(AutoExpandCardDetailsProperty, value);
        }

        public static readonly StyledProperty<FlowKanbanAddCardPlacement> AddCardPlacementProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanAddCardPlacement>(
                                nameof(AddCardPlacement),
                                FlowKanbanAddCardPlacement.Bottom);

        /// <summary>
        /// Controls where the inline "Add Card" control appears in a column.
        /// </summary>
        public FlowKanbanAddCardPlacement AddCardPlacement
        {
            get => (FlowKanbanAddCardPlacement)GetValue(AddCardPlacementProperty);
            set => SetValue(AddCardPlacementProperty, value);
        }

        public static readonly StyledProperty<FlowKanbanCompactColumnSizingMode> CompactColumnSizingModeProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanCompactColumnSizingMode>(
                                nameof(CompactColumnSizingMode),
                                FlowKanbanCompactColumnSizingMode.Adaptive);

        /// <summary>
        /// Controls how compact columns are sized vertically.
        /// </summary>
        public FlowKanbanCompactColumnSizingMode CompactColumnSizingMode
        {
            get => (FlowKanbanCompactColumnSizingMode)GetValue(CompactColumnSizingModeProperty);
            set => SetValue(CompactColumnSizingModeProperty, value);
        }

        public static readonly StyledProperty<int> CompactManualCardCountProperty =
            AvaloniaProperty.Register<FlowKanban, int>(
                                nameof(CompactManualCardCount),
                                3);

        /// <summary>
        /// Number of task cards to target when using manual compact sizing.
        /// </summary>
        public int CompactManualCardCount
        {
            get => (int)GetValue(CompactManualCardCountProperty);
            set => SetValue(CompactManualCardCountProperty, value);
        }

        public static readonly StyledProperty<bool> ShowWelcomeMessageProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(ShowWelcomeMessage),
                                true);

        /// <summary>
        /// Whether the home welcome message is visible.
        /// </summary>
        public bool ShowWelcomeMessage
        {
            get => (bool)GetValue(ShowWelcomeMessageProperty);
            set => SetValue(ShowWelcomeMessageProperty, value);
        }

        public static readonly StyledProperty<string> WelcomeMessageTitleProperty =
            AvaloniaProperty.Register<FlowKanban, string>(
                                nameof(WelcomeMessageTitle),
                                string.Empty);

        /// <summary>
        /// Custom title for the home welcome message. Leave empty to use the localized default.
        /// </summary>
        public string WelcomeMessageTitle
        {
            get => (string)GetValue(WelcomeMessageTitleProperty);
            set => SetValue(WelcomeMessageTitleProperty, value ?? string.Empty);
        }

        public static readonly StyledProperty<string> WelcomeMessageSubtitleProperty =
            AvaloniaProperty.Register<FlowKanban, string>(
                                nameof(WelcomeMessageSubtitle),
                                string.Empty);

        /// <summary>
        /// Custom subtitle for the home welcome message. Leave empty to use the localized default.
        /// </summary>
        public string WelcomeMessageSubtitle
        {
            get => (string)GetValue(WelcomeMessageSubtitleProperty);
            set => SetValue(WelcomeMessageSubtitleProperty, value ?? string.Empty);
        }

        private static void OnSettingChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.TrySaveSettings(out _);
                if (e.Property == IsStatusBarVisibleProperty)
                {
                    kanban.UpdateBoardStatusBarVisibility();
                }
                if (e.Property == AutoSaveAfterEditsProperty && !kanban.AutoSaveAfterEdits)
                {
                    kanban.StopAutoSaveTimer();
                }
            }
        }

        private static void OnCompactColumnSizingChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            OnSettingChanged(d, e);
            if (d is FlowKanban kanban)
            {
                kanban.ClampCompactManualCardCount();
                kanban.ScheduleCompactColumnSizingUpdate();
            }
        }
        #endregion

        private string? GetDefaultLaneId()
        {
            if (!IsLaneGroupingEnabled)
                return null;

            if (Board.Lanes.Count == 0)
                return null;

            var laneId = Board.Lanes[0].Id;
            return string.IsNullOrWhiteSpace(laneId) ? null : laneId;
        }

        internal void ShowWipWarning(FlowKanbanColumnData column, string? laneId)
        {
            if (column == null)
                return;

            var display = column.GetLaneWipDisplay(laneId);
            var message = string.Format(
                CultureInfo.CurrentUICulture,
                FloweryLocalization.GetString("Kanban_WipWarning", "WIP limit exceeded ({0})."),
                display);

            ShowStatusMessage(message, TimeSpan.FromSeconds(4));
        }

        private void ExecuteAddCard(FlowKanbanColumnData? column)
        {
            if (column != null)
            {
                var task = new FlowTask
                {
                    Title = FloweryLocalization.GetString("Kanban_Editor_NewTask"),
                    LaneId = GetDefaultLaneId()
                };
                FlowKanbanWorkItemNumberHelper.EnsureTaskNumber(Board, task);
                ExecuteCommand(new AddCardCommand(column, task));
                FlowKanbanDoneColumnHelper.UpdateCompletedAtOnAdd(Board, column, task);
            }
        }

        private void ExecuteQuickAddCard()
        {
            if (!IsBoardViewActive)
                return;

            var column = GetActiveColumnData();
            if (column == null)
                return;

            BeginInlineAddCard(column);
        }

        private void ExecuteToggleColumnCollapse(FlowKanbanColumnData? column)
        {
            if (column != null)
            {
                column.IsCollapsed = !column.IsCollapsed;
            }
        }

        private void ExecuteToggleArchiveColumnVisibility()
        {
            if (Board == null)
                return;

            if (string.IsNullOrWhiteSpace(Board.ArchiveColumnId) || Board.IsArchiveColumnHidden)
            {
                var manager = new FlowKanbanManager(this, autoAttach: false);
                manager.EnsureArchiveColumn();
                Board.IsArchiveColumnHidden = false;
            }
            else
            {
                Board.IsArchiveColumnHidden = true;
            }

            UpdateArchiveColumnState();
            ApplySearchFilter();
        }

        private bool CanExecuteBulkMove(FlowKanbanColumnData? column)
        {
            return HasSelection && column != null;
        }

        private void ExecuteBulkMove(FlowKanbanColumnData? column)
        {
            if (!HasSelection || column == null)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            var movedCount = manager.BulkMove(column);
            if (movedCount > 0)
            {
                DeselectAllTasks();
            }
            else
            {
                UpdateSelectionMetrics();
            }
        }

        private void ExecuteBulkSetPriority(string? priorityValue)
        {
            if (!HasSelection || string.IsNullOrWhiteSpace(priorityValue))
                return;

            if (!Enum.TryParse(priorityValue, out FlowTaskPriority priority))
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.BulkSetPriority(priority);
        }

        private void ExecuteBulkSetTags(string? tags)
        {
            if (!HasSelection)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.BulkSetTags(tags);
        }

        private void ExecuteBulkSetDueDate(DateTimeOffset? dueDate)
        {
            if (!HasSelection)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.BulkSetDueDate(dueDate?.Date);
        }

        private async void ExecuteBulkArchive()
        {
            if (!HasSelection || TopLevel == null)
                return;

            var confirmed = await FlowKanbanConfirmDialog.ShowAsync(
                FloweryLocalization.GetString("Kanban_Bulk_Archive"),
                FloweryLocalization.GetString("Kanban_Bulk_ArchiveConfirmMessage"),
                FloweryLocalization.GetString("Kanban_Bulk_Archive"),
                Flowery.Controls.DaisyButtonVariant.Primary,
                TopLevel);
            if (!confirmed)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.BulkArchive();
            UpdateSelectionMetrics();
        }

        private async void ExecuteBulkDelete()
        {
            if (!HasSelection || TopLevel == null)
                return;

            var confirmed = await FlowKanbanConfirmDialog.ShowAsync(
                FloweryLocalization.GetString("Common_ConfirmDelete"),
                FloweryLocalization.GetString("Kanban_Bulk_DeleteConfirmMessage"),
                FloweryLocalization.GetString("Common_Delete"),
                Flowery.Controls.DaisyButtonVariant.Error,
                TopLevel);
            if (!confirmed)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.BulkDelete();
            UpdateSelectionMetrics();
        }

        private void ExecuteSave()
        {
            if (!TrySaveBoard(out var error) && error != null)
            {
                ReportPersistenceFailure(FlowKanbanPersistenceOperation.SaveBoard, error);
            }
        }

        private void ExecuteLoad()
        {
            if (TryLoadLastBoard(out var error))
            {
                CurrentView = FlowKanbanView.Board;
                return;
            }

            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Kanban: {error.Message}");
            }
        }

        private async void ExecuteSaveToFile()
        {
            if (!IsFilePickerAvailable)
                return;

            try
            {
                var boardId = EnsureBoardId(Board);
                var json = JsonSerializer.Serialize(Board, FlowKanbanJsonContext.Default.FlowKanbanData);
                var payload = new FlowKanbanBoardExportRequestedEventArgs(boardId, Board.Title, json);
                if (!await TrySaveExportToFileAsync(payload))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to save board export.");
                }
            }
            catch (Exception ex) when (ex is COMException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is InvalidOperationException || ex is IOException || ex is ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save board export: {ex.Message}");
            }
        }

        private async void ExecuteLoadFromFile()
        {
            if (!IsFilePickerAvailable)
                return;

            try
            {
                var storageProvider = TopLevel?.StorageProvider;
                if (storageProvider?.CanOpen != true)
                    return;

                var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = FloweryLocalization.GetString("Kanban_Board_LoadFromFile"),
                    FileTypeFilter = [JsonFileType]
                });
                var file = files.FirstOrDefault();
                if (file is null)
                    return;

                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                if (!FlowKanbanBoardSanitizer.TryLoadFromJson(json, out var data) || data == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to import board data from file.");
                    return;
                }

                ApplyImportedBoard(data);
            }
            catch (Exception ex) when (ex is COMException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is InvalidOperationException || ex is IOException || ex is ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load board from file: {ex.Message}");
            }
        }

        private void ApplyImportedBoard(FlowKanbanData data)
        {
            _suppressAutoSave = true;
            try
            {
                Board = data;
            }
            finally
            {
                _suppressAutoSave = false;
            }

            _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
            TrySaveBoard(out _);
            RefreshBoards();
            CurrentView = FlowKanbanView.Board;
        }

        private bool CanExecuteFilePickerCommand()
        {
            var storageProvider = TopLevel?.StorageProvider;
            return IsFilePickerAvailable && storageProvider is { CanOpen: true } or { CanSave: true };
        }

        private void OnCommandHistoryChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyCommandHistoryChanged();
        }

        private static void OnEnableUndoRedoChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                if (!kanban.EnableUndoRedo)
                {
                    kanban._commandHistory.Clear();
                }

                kanban.NotifyCommandHistoryChanged();
            }
        }

        private void NotifyCommandHistoryChanged()
        {
            if (UndoCommand is RelayCommand undo)
                undo.RaiseCanExecuteChanged();
            if (RedoCommand is RelayCommand redo)
                redo.RaiseCanExecuteChanged();
        }

        private void NotifySelectionCommandsChanged()
        {
            if (BulkMoveCommand is RelayCommand<FlowKanbanColumnData?> bulkMove)
                bulkMove.RaiseCanExecuteChanged();
            if (BulkSetPriorityCommand is RelayCommand<string?> bulkPriority)
                bulkPriority.RaiseCanExecuteChanged();
            if (BulkSetTagsCommand is RelayCommand<string?> bulkTags)
                bulkTags.RaiseCanExecuteChanged();
            if (BulkSetDueDateCommand is RelayCommand<DateTimeOffset?> bulkDueDate)
                bulkDueDate.RaiseCanExecuteChanged();
            if (BulkArchiveCommand is RelayCommand bulkArchive)
                bulkArchive.RaiseCanExecuteChanged();
            if (BulkDeleteCommand is RelayCommand bulkDelete)
                bulkDelete.RaiseCanExecuteChanged();
            if (ClearSelectionCommand is RelayCommand clearSelection)
                clearSelection.RaiseCanExecuteChanged();
        }

        private void NotifyLaneGroupingChanged()
        {
            UpdateLaneGroupingState();
            LaneGroupingChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void ExecuteCommand(IKanbanCommand command)
        {
            if (EnableUndoRedo)
            {
                _commandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }
        }

        internal sealed class FlowKanbanSettingsState
        {
            public bool ConfirmColumnRemovals { get; set; } = true;
            public bool ConfirmCardRemovals { get; set; } = true;
            public bool AutoSaveAfterEdits { get; set; } = true;
            public bool AutoExpandCardDetails { get; set; } = true;
            public bool EnableUndoRedo { get; set; } = false;
            public FlowKanbanAddCardPlacement AddCardPlacement { get; set; } = FlowKanbanAddCardPlacement.Bottom;
            public FlowKanbanCompactColumnSizingMode CompactColumnSizingMode { get; set; } = FlowKanbanCompactColumnSizingMode.Adaptive;
            public int CompactManualCardCount { get; set; } = 3;
            public bool ShowWelcomeMessage { get; set; } = true;
            public string? WelcomeMessageTitle { get; set; } = string.Empty;
            public string? WelcomeMessageSubtitle { get; set; } = string.Empty;
            public bool IsStatusBarVisible { get; set; } = true;
            public double ColumnWidth { get; set; } = DefaultColumnWidth;
            public string? LastBoardId { get; set; }
            public FlowKanbanView LastView { get; set; } = FlowKanbanView.Board;
        }

        private FlowKanbanSettingsState BuildSettingsState()
        {
            return new FlowKanbanSettingsState
            {
                ConfirmColumnRemovals = ConfirmColumnRemovals,
                ConfirmCardRemovals = ConfirmCardRemovals,
                AutoSaveAfterEdits = AutoSaveAfterEdits,
                AutoExpandCardDetails = AutoExpandCardDetails,
                EnableUndoRedo = EnableUndoRedo,
                AddCardPlacement = AddCardPlacement,
                CompactColumnSizingMode = CompactColumnSizingMode,
                CompactManualCardCount = CompactManualCardCount,
                ShowWelcomeMessage = ShowWelcomeMessage,
                WelcomeMessageTitle = WelcomeMessageTitle,
                WelcomeMessageSubtitle = WelcomeMessageSubtitle,
                IsStatusBarVisible = IsStatusBarVisible,
                ColumnWidth = ColumnWidth,
                LastBoardId = _lastBoardId,
                LastView = CurrentView
            };
        }

        private void ApplySettingsState(FlowKanbanSettingsState state)
        {
            _isApplyingSettings = true;
            try
            {
                ConfirmColumnRemovals = state.ConfirmColumnRemovals;
                ConfirmCardRemovals = state.ConfirmCardRemovals;
                AutoSaveAfterEdits = state.AutoSaveAfterEdits;
                AutoExpandCardDetails = state.AutoExpandCardDetails;
                EnableUndoRedo = state.EnableUndoRedo;
                AddCardPlacement = state.AddCardPlacement;
                CompactColumnSizingMode = state.CompactColumnSizingMode;
                CompactManualCardCount = state.CompactManualCardCount;
                ShowWelcomeMessage = state.ShowWelcomeMessage;
                WelcomeMessageTitle = state.WelcomeMessageTitle ?? string.Empty;
                WelcomeMessageSubtitle = state.WelcomeMessageSubtitle ?? string.Empty;
                IsStatusBarVisible = state.IsStatusBarVisible;
                ColumnWidth = state.ColumnWidth;
                _lastBoardId = state.LastBoardId;
                CurrentView = Enum.IsDefined(typeof(FlowKanbanView), state.LastView)
                    ? state.LastView
                    : FlowKanbanView.Board;
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        /// <summary>
        /// Loads persisted settings if available.
        /// </summary>
        /// <param name="forceReload">When true, reloads even if settings were already loaded.</param>
        /// <param name="error">Exception details when the load fails.</param>
        /// <returns>True when the operation completed without throwing; otherwise false.</returns>
        public bool TryLoadSettings(bool forceReload, out Exception? error)
        {
            try
            {
                LoadSettingsCore(forceReload);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        /// <summary>
        /// Saves current settings to persistent storage.
        /// </summary>
        /// <param name="error">Exception details when the save fails.</param>
        /// <returns>True when the operation completed without throwing; otherwise false.</returns>
        public bool TrySaveSettings(out Exception? error)
        {
            try
            {
                SaveSettingsCore();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        private void LoadSettingsCore(bool forceReload)
        {
            if (_settingsLoaded && !forceReload)
                return;

            var json = LoadStateText(SettingsStorageKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                _settingsLoaded = true;
                return;
            }

            var data = JsonSerializer.Deserialize(json, FlowKanbanJsonContext.Default.FlowKanbanSettingsState);
            if (data != null)
                ApplySettingsState(data);

            _settingsLoaded = true;
        }

        private void SaveSettingsCore()
        {
            if (_isApplyingSettings)
                return;

            var json = JsonSerializer.Serialize(BuildSettingsState(), FlowKanbanJsonContext.Default.FlowKanbanSettingsState);
            SaveStateText(SettingsStorageKey, json);
        }

        /// <summary>
        /// Saves the current board to persistent storage.
        /// </summary>
        /// <param name="error">Exception details when the save fails.</param>
        /// <returns>True when the operation completed without throwing; otherwise false.</returns>
        public bool TrySaveBoard(out Exception? error)
        {
            try
            {
                SaveBoardStateCore(Board, updateLastBoardId: true);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        /// <summary>
        /// Loads the most recently saved board from persistent storage.
        /// </summary>
        /// <param name="error">Exception details when the load fails.</param>
        /// <param name="useCompactLayout">When true, forces compact layout for the loaded board.</param>
        /// <param name="compactColumnId">Optional column ID to display in compact layout.</param>
        /// <param name="compactColumnTitle">Optional column title to display in compact layout.</param>
        /// <returns>True when a board was loaded successfully; otherwise false.</returns>
        public bool TryLoadLastBoard(
            out Exception? error,
            bool useCompactLayout = false,
            string? compactColumnId = null,
            string? compactColumnTitle = null)
        {
            try
            {
                var loaded = LoadBoardStateCore();
                if (loaded)
                {
                    ApplyCompactLayoutOptions(useCompactLayout, compactColumnId, compactColumnTitle);
                }
                error = null;
                return loaded;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        private void SaveBoardStateCore(FlowKanbanData board, bool updateLastBoardId)
        {
            if (board == null)
                return;

            var boardId = EnsureBoardId(board);
            if (!BoardStore.TrySaveBoard(board, out var error))
            {
                if (error != null)
                    throw error;

                throw new InvalidOperationException("Failed to save board.");
            }
            if (updateLastBoardId)
            {
                UpdateLastBoardId(boardId);
            }
        }

        private bool LoadBoardStateCore()
        {
            var boardId = _lastBoardId;
            if (string.IsNullOrWhiteSpace(boardId))
                boardId = EnsureBoardId(Board);

            return LoadBoardStateCore(boardId);
        }

        private bool LoadBoardStateCore(string boardId)
        {
            if (string.IsNullOrWhiteSpace(boardId))
                return false;

            if (!BoardStore.TryLoadBoard(boardId, out var data, out var error))
            {
                if (error != null)
                    throw error;

                return false;
            }

            if (data == null)
                return false;

            if (string.IsNullOrWhiteSpace(data.Id) || !string.Equals(data.Id, boardId, StringComparison.Ordinal))
            {
                data.Id = boardId;
            }

            _suppressAutoSave = true;
            try
            {
                Board = data;
            }
            finally
            {
                _suppressAutoSave = false;
            }

            UpdateLastBoardId(data.Id);
            _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
            return true;
        }

        private static string? LoadStateText(string key)
        {
            var lines = StateStorageProvider.Instance.LoadLines(key);
            if (lines.Count == 0)
                return null;

            return string.Join(Environment.NewLine, lines);
        }

        private static void SaveStateText(string key, string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            StateStorageProvider.Instance.SaveLines(key, lines);
        }

        private void UpdateLastBoardId(string boardId)
        {
            if (string.IsNullOrWhiteSpace(boardId))
                return;

            if (string.Equals(_lastBoardId, boardId, StringComparison.Ordinal))
                return;

            _lastBoardId = boardId;
            TrySaveSettings(out _);
        }

        private static string EnsureBoardId(FlowKanbanData board)
        {
            if (!FlowKanbanBoardSanitizer.IsValidId(board.Id))
            {
                board.Id = Guid.NewGuid().ToString();
            }

            return board.Id;
        }

        private void RequestAutoSave()
        {
            if (_suppressAutoSave || !AutoSaveAfterEdits)
                return;

            if (!EnsureAutoSaveTimer())
            {
                ExecuteSave();
                return;
            }

            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Start();
        }

        private bool EnsureAutoSaveTimer()
        {
            if (_autoSaveTimer != null)
                return true;

            _autoSaveTimer = FlowKanbanDispatcher.CreateTimer(OnAutoSaveTimerTick);
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(AutoSaveDebounceMilliseconds);
            return true;
        }

        private void StopAutoSaveTimer()
        {
            _autoSaveTimer?.Stop();
        }

        private void OnAutoSaveTimerTick(object? sender, EventArgs e)
        {
            _autoSaveTimer?.Stop();
            ExecuteSave();
        }

        private void RefreshDoneAgingState()
        {
            if (_trackedBoard == null)
            {
                StopDoneAgingTimer();
                return;
            }

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.SyncDoneColumnTimestamps();

            if (!IsDoneAgingActive(manager))
            {
                StopDoneAgingTimer();
                return;
            }

            manager.AutoArchiveDoneTasks();
            EnsureDoneAgingTimer();
        }

        private bool IsDoneAgingActive(FlowKanbanManager manager)
        {
            if (_trackedBoard == null)
                return false;

            if (!_trackedBoard.AutoArchiveDoneEnabled || _trackedBoard.AutoArchiveDoneDays <= 0)
                return false;

            return manager.GetDoneColumn() != null;
        }

        private void EnsureDoneAgingTimer()
        {
            if (_doneAgingTimer == null)
            {
                _doneAgingTimer = new ManagedTimer(TimeSpan.FromMinutes(DoneAgingCheckIntervalMinutes));
                _doneAgingTimer.Tick += OnDoneAgingTimerTick;
            }
            else
            {
                _doneAgingTimer.Interval = TimeSpan.FromMinutes(DoneAgingCheckIntervalMinutes);
            }

            if (!_doneAgingTimer.IsRunning)
            {
                _doneAgingTimer.Start();
            }
        }

        private void StopDoneAgingTimer()
        {
            _doneAgingTimer?.Stop();
        }

        private void DisposeDoneAgingTimer()
        {
            if (_doneAgingTimer == null)
                return;

            _doneAgingTimer.Dispose();
            _doneAgingTimer = null;
        }

        private void OnDoneAgingTimerTick(object? sender, EventArgs e)
        {
            if (_trackedBoard == null)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            if (!IsDoneAgingActive(manager))
            {
                StopDoneAgingTimer();
                return;
            }

            manager.AutoArchiveDoneTasks();
        }

        private void AttachBoardTracking(FlowKanbanData? board)
        {
            if (ReferenceEquals(_trackedBoard, board))
                return;

            DetachBoardTracking();
            _trackedBoard = board;

            if (_trackedBoard == null)
                return;

            _trackedBoard.PropertyChanged += OnBoardPropertyChanged;
            AttachColumnsCollection(_trackedBoard.Columns);
            _trackedTags = _trackedBoard.Tags;
            _trackedTags.CollectionChanged += OnTagsCollectionChanged;
            _trackedLanes = _trackedBoard.Lanes;
            _trackedLanes.CollectionChanged += OnLanesCollectionChanged;

            foreach (var lane in _trackedBoard.Lanes)
            {
                TrackLane(lane);
            }

            UpdateArchiveColumnState();
            UpdateLayoutItemsSources();
            ApplySearchFilter();
            NotifyLaneGroupingChanged();
            ClampKeyboardColumnIndex();
            RefreshDoneAgingState();
        }

        private void DetachBoardTracking()
        {
            if (_trackedBoard == null)
                return;

            _trackedBoard.PropertyChanged -= OnBoardPropertyChanged;
            DetachColumnsCollection();
            if (_trackedTags != null)
            {
                _trackedTags.CollectionChanged -= OnTagsCollectionChanged;
                _trackedTags = null;
            }
            if (_trackedLanes != null)
            {
                _trackedLanes.CollectionChanged -= OnLanesCollectionChanged;
                _trackedLanes = null;
            }

            var lanes = new List<FlowKanbanLane>(_trackedLaneItems);
            foreach (var lane in lanes)
            {
                UntrackLane(lane);
            }

            _trackedColumns.Clear();
            _trackedTasks.Clear();
            _trackedSubtasks.Clear();
            _trackedTaskCollections.Clear();
            _trackedSubtaskCollections.Clear();
            _trackedLaneItems.Clear();
            _trackedBoard = null;
            UpdateLayoutItemsSources();
        }

        private void AttachColumnsCollection(ObservableCollection<FlowKanbanColumnData> columns)
        {
            if (ReferenceEquals(_trackedColumnsCollection, columns))
                return;

            DetachColumnsCollection();
            _trackedColumnsCollection = columns;
            _trackedColumnsCollection.CollectionChanged += OnColumnsCollectionChanged;

            foreach (var column in _trackedColumnsCollection)
            {
                TrackColumn(column);
            }
        }

        private void DetachColumnsCollection()
        {
            if (_trackedColumnsCollection != null)
            {
                _trackedColumnsCollection.CollectionChanged -= OnColumnsCollectionChanged;
                _trackedColumnsCollection = null;
            }

            foreach (var column in new List<FlowKanbanColumnData>(_trackedColumns))
            {
                UntrackColumn(column);
            }
        }

        private void TrackColumn(FlowKanbanColumnData column)
        {
            if (!_trackedColumns.Add(column))
                return;

            column.PropertyChanged += OnColumnPropertyChanged;
            AttachTasksCollection(column, column.Tasks);
        }

        private void UntrackColumn(FlowKanbanColumnData column)
        {
            if (!_trackedColumns.Remove(column))
                return;

            column.PropertyChanged -= OnColumnPropertyChanged;
            DetachTasksCollection(column);
        }

        private void AttachTasksCollection(
            FlowKanbanColumnData column,
            ObservableCollection<FlowTask> tasks)
        {
            if (_trackedTaskCollections.TryGetValue(column, out var trackedTasks)
                && ReferenceEquals(trackedTasks, tasks))
            {
                return;
            }

            DetachTasksCollection(column);
            _trackedTaskCollections[column] = tasks;
            tasks.CollectionChanged += OnTasksCollectionChanged;

            foreach (var task in tasks)
            {
                TrackTask(task);
            }
        }

        private void DetachTasksCollection(FlowKanbanColumnData column)
        {
            if (!_trackedTaskCollections.Remove(column, out var tasks))
                return;

            tasks.CollectionChanged -= OnTasksCollectionChanged;
            foreach (var task in tasks)
            {
                UntrackTask(task);
            }
        }

        private void TrackTask(FlowTask task)
        {
            if (!_trackedTasks.Add(task))
                return;

            task.PropertyChanged += OnTaskPropertyChanged;
            AttachSubtasksCollection(task, task.Subtasks);
            ApplySearchFilterToTask(task);
        }

        private void UntrackTask(FlowTask task)
        {
            if (!_trackedTasks.Remove(task))
                return;

            task.PropertyChanged -= OnTaskPropertyChanged;
            DetachSubtasksCollection(task);
        }

        private void AttachSubtasksCollection(
            FlowTask task,
            ObservableCollection<FlowSubtask> subtasks)
        {
            if (_trackedSubtaskCollections.TryGetValue(task, out var trackedSubtasks)
                && ReferenceEquals(trackedSubtasks, subtasks))
            {
                return;
            }

            DetachSubtasksCollection(task);
            _trackedSubtaskCollections[task] = subtasks;
            subtasks.CollectionChanged += OnSubtasksCollectionChanged;

            foreach (var subtask in subtasks)
            {
                TrackSubtask(subtask);
            }
        }

        private void DetachSubtasksCollection(FlowTask task)
        {
            if (!_trackedSubtaskCollections.Remove(task, out var subtasks))
                return;

            subtasks.CollectionChanged -= OnSubtasksCollectionChanged;
            foreach (var subtask in subtasks)
            {
                UntrackSubtask(subtask);
            }
        }

        private void TrackSubtask(FlowSubtask subtask)
        {
            if (!_trackedSubtasks.Add(subtask))
                return;

            subtask.PropertyChanged += OnSubtaskPropertyChanged;
        }

        private void UntrackSubtask(FlowSubtask subtask)
        {
            if (!_trackedSubtasks.Remove(subtask))
                return;

            subtask.PropertyChanged -= OnSubtaskPropertyChanged;
        }

        private void TrackLane(FlowKanbanLane lane)
        {
            if (!_trackedLaneItems.Add(lane))
                return;

            lane.PropertyChanged += OnLanePropertyChanged;
        }

        private void UntrackLane(FlowKanbanLane lane)
        {
            if (!_trackedLaneItems.Remove(lane))
                return;

            lane.PropertyChanged -= OnLanePropertyChanged;
        }

        private void RebuildTracking()
        {
            if (_trackedBoard == null)
                return;

            var columns = _trackedBoard.Columns;
            DetachColumnsCollection();
            AttachColumnsCollection(columns);
        }

        private void RebuildLaneTracking()
        {
            if (_trackedBoard == null)
                return;

            var lanes = new List<FlowKanbanLane>(_trackedLaneItems);
            foreach (var lane in lanes)
            {
                UntrackLane(lane);
            }

            _trackedLaneItems.Clear();

            foreach (var lane in _trackedBoard.Lanes)
            {
                TrackLane(lane);
            }
        }

        private void OnBoardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowKanbanData.Columns), StringComparison.Ordinal))
            {
                if (_trackedBoard != null)
                {
                    AttachColumnsCollection(_trackedBoard.Columns);
                }

                UpdateLaneRows();
                UpdateArchiveColumnState();
                UpdateLayoutItemsSources();
                UpdateSelectionMetrics();
                ApplySearchFilter();
                ClampKeyboardColumnIndex();
                RefreshDoneAgingState();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanData.Tags), StringComparison.Ordinal))
            {
                if (_trackedBoard != null)
                {
                    if (_trackedTags != null)
                    {
                        _trackedTags.CollectionChanged -= OnTagsCollectionChanged;
                    }

                    _trackedTags = _trackedBoard.Tags;
                    _trackedTags.CollectionChanged += OnTagsCollectionChanged;
                }
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanData.Lanes), StringComparison.Ordinal))
            {
                if (_trackedBoard != null)
                {
                    if (_trackedLanes != null)
                    {
                        _trackedLanes.CollectionChanged -= OnLanesCollectionChanged;
                    }

                    _trackedLanes = _trackedBoard.Lanes;
                    _trackedLanes.CollectionChanged += OnLanesCollectionChanged;
                    RebuildLaneTracking();
                }

                NotifyLaneGroupingChanged();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanData.GroupBy), StringComparison.Ordinal))
            {
                NotifyLaneGroupingChanged();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanData.ArchiveColumnId), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(FlowKanbanData.IsArchiveColumnHidden), StringComparison.Ordinal))
            {
                UpdateArchiveColumnState();
                ApplySearchFilter();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowKanbanData.DoneColumnId), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(FlowKanbanData.AutoArchiveDoneEnabled), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(FlowKanbanData.AutoArchiveDoneDays), StringComparison.Ordinal))
            {
                RefreshDoneAgingState();
            }

            if (_trackedBoard != null)
            {
                BoardEdited?.Invoke(this, new BoardEditedEventArgs(_trackedBoard, e.PropertyName));
            }

            RequestAutoSave();
        }

        private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildTracking();
                UpdateLaneRows();
                UpdateArchiveColumnState();
                UpdateLayoutItemsSources();
                UpdateSelectionMetrics();
                ApplySearchFilter();
                ClampKeyboardColumnIndex();
                RefreshDoneAgingState();
                RequestAutoSave();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (FlowKanbanColumnData column in e.OldItems)
                {
                    UntrackColumn(column);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowKanbanColumnData column in e.NewItems)
                {
                    TrackColumn(column);
                }
            }

            UpdateLaneRows();
            UpdateArchiveColumnState();
            UpdateLayoutItemsSources();
            RefreshDoneAgingState();
            RequestAutoSave();
            ClampKeyboardColumnIndex();
        }

        private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildTracking();
                ApplySearchFilter();
                UpdateLaneRows();
                UpdateSelectionMetrics();
                _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
                RequestAutoSave();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (FlowTask task in e.OldItems)
                {
                    UntrackTask(task);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowTask task in e.NewItems)
                {
                    TrackTask(task);
                    ApplySearchFilterToTask(task);
                }
            }

            UpdateLaneRows();
            UpdateSelectionMetrics();
            _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
            RequestAutoSave();
        }

        private void OnSubtasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildTracking();
                RequestAutoSave();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (FlowSubtask subtask in e.OldItems)
                {
                    UntrackSubtask(subtask);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowSubtask subtask in e.NewItems)
                {
                    TrackSubtask(subtask);
                }
            }

            RequestAutoSave();
        }

        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is FlowKanbanColumnData column)
            {
                ColumnEdited?.Invoke(this, column);

                if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.Tasks), StringComparison.Ordinal))
                {
                    AttachTasksCollection(column, column.Tasks);
                    ApplySearchFilter();
                    UpdateLaneRows();
                    UpdateSelectionMetrics();
                    RefreshDoneAgingState();
                }
            }

            if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.WipLimit), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.LaneWipLimits), StringComparison.Ordinal))
            {
                UpdateLaneRows();
            }

            if (string.Equals(e.PropertyName, nameof(FlowKanbanColumnData.IsCollapsed), StringComparison.Ordinal))
            {
                RequestCompactLayoutScrollRefresh();
            }

            RequestAutoSave();
        }

        private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowTask.IsSearchMatch), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(FlowTask.AssigneeAvatarSource), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(FlowTask.AssigneeRoles), StringComparison.Ordinal))
                return;

            if (sender is FlowTask task)
            {
                if (string.Equals(e.PropertyName, nameof(FlowTask.Subtasks), StringComparison.Ordinal))
                {
                    AttachSubtasksCollection(task, task.Subtasks);
                }

                if (string.Equals(e.PropertyName, nameof(FlowTask.AssigneeId), StringComparison.Ordinal))
                {
                    _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
                }

                var isSearchRelevant = IsSearchRelevantProperty(e.PropertyName)
                                       || string.Equals(e.PropertyName, nameof(FlowTask.IsArchived), StringComparison.Ordinal);
                if (isSearchRelevant)
                {
                    ApplySearchFilterToTask(task);
                }
            }

            if (sender is FlowTask editedTask)
            {
                CardEdited?.Invoke(this, editedTask);
            }

            if (string.Equals(e.PropertyName, nameof(FlowTask.LaneId), StringComparison.Ordinal))
            {
                UpdateLaneRows();
            }

            if (string.Equals(e.PropertyName, nameof(FlowTask.IsSelected), StringComparison.Ordinal))
            {
                UpdateSelectionMetrics();
            }

            RequestAutoSave();
        }

        internal void DeselectAllTasks()
        {
            foreach (var column in Board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (task.IsSelected)
                        task.IsSelected = false;
                }
            }

            UpdateSelectionMetrics();
        }

        private void UpdateSelectionMetrics()
        {
            var count = 0;
            foreach (var column in Board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (task.IsSelected)
                        count++;
                }
            }

            SelectedCount = count;
            HasSelection = count > 0;
            if (count >= 2 && !IsStatusBarVisible)
            {
                IsStatusBarVisible = true;
            }
            NotifySelectionCommandsChanged();
        }

        private void OnSubtaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RequestAutoSave();
        }

        private void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RequestAutoSave();
        }

        private void OnLanesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildLaneTracking();
                RequestAutoSave();
                NotifyLaneGroupingChanged();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (FlowKanbanLane lane in e.OldItems)
                {
                    UntrackLane(lane);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowKanbanLane lane in e.NewItems)
                {
                    TrackLane(lane);
                }
            }

            RequestAutoSave();
            NotifyLaneGroupingChanged();
        }

        private void OnLanePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RequestAutoSave();
            NotifyLaneGroupingChanged();
        }

        private bool IsSearchRelevantProperty(string? propertyName)
        {
            return string.Equals(propertyName, nameof(FlowTask.Title), StringComparison.Ordinal) ||
                   string.Equals(propertyName, nameof(FlowTask.Description), StringComparison.Ordinal) ||
                   string.Equals(propertyName, nameof(FlowTask.Tags), StringComparison.Ordinal) ||
                   string.Equals(propertyName, nameof(FlowTask.Assignee), StringComparison.Ordinal) ||
                   string.Equals(propertyName, nameof(FlowTask.AssigneeId), StringComparison.Ordinal);
        }

        private void ApplySearchFilter()
        {
            if (_trackedBoard == null)
                return;

            _isApplyingSearchFilter = true;
            try
            {
                foreach (var column in _trackedBoard.Columns)
                {
                    foreach (var task in column.Tasks)
                    {
                        ApplySearchFilterToTask(task);
                    }
                }
            }
            finally
            {
                _isApplyingSearchFilter = false;
            }

            SearchFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplySearchFilterToTask(FlowTask task)
        {
            var isInArchiveColumn = IsTaskInArchiveColumn(task);
            if (isInArchiveColumn)
            {
                var matches = string.IsNullOrWhiteSpace(SearchText);
                if (task.IsSearchMatch != matches)
                {
                    task.IsSearchMatch = matches;
                    if (!_isApplyingSearchFilter)
                    {
                        SearchFilterChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                return;
            }

            if (task.IsArchived)
            {
                if (task.IsSearchMatch)
                {
                    task.IsSearchMatch = false;
                    if (!_isApplyingSearchFilter)
                    {
                        SearchFilterChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                return;
            }

            var isMatch = IsTaskMatch(task, SearchText);
            if (task.IsSearchMatch == isMatch)
                return;

            task.IsSearchMatch = isMatch;
            if (!_isApplyingSearchFilter)
            {
                SearchFilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool IsTaskInArchiveColumn(FlowTask task)
        {
            if (_trackedBoard == null)
                return false;

            var archiveId = _trackedBoard.ArchiveColumnId;
            if (string.IsNullOrWhiteSpace(archiveId))
                return false;

            foreach (var column in _trackedBoard.Columns)
            {
                if (!string.Equals(column.Id, archiveId, StringComparison.Ordinal))
                    continue;

                return column.Tasks.Contains(task);
            }

            return false;
        }

        private void UpdateArchiveColumnState()
        {
            if (_trackedBoard == null)
                return;

            var archiveId = _trackedBoard.ArchiveColumnId;
            FlowKanbanColumnData? archiveColumn = null;
            if (!string.IsNullOrWhiteSpace(archiveId))
            {
                foreach (var column in _trackedBoard.Columns)
                {
                    if (string.Equals(column.Id, archiveId, StringComparison.Ordinal))
                    {
                        archiveColumn = column;
                        break;
                    }
                }
            }

            var hasArchiveColumn = archiveColumn != null;
            var isHidden = _trackedBoard.IsArchiveColumnHidden;

            foreach (var column in _trackedBoard.Columns)
            {
                var isArchive = ReferenceEquals(column, archiveColumn);
                column.IsArchiveColumn = isArchive;
                column.IsArchiveColumnVisible = !isArchive || !isHidden;
            }

            CanShowArchiveColumn = !hasArchiveColumn || isHidden;
            CanHideArchiveColumn = hasArchiveColumn && !isHidden;
            HasArchiveColumnToggle = CanShowArchiveColumn || CanHideArchiveColumn;
            UpdateBulkMoveColumns();
            UpdateLayoutItemsSources();
        }

        private void UpdateBulkMoveColumns()
        {
            var columns = BulkMoveColumns;
            columns.Clear();

            if (_trackedBoard == null)
            {
                SelectedBulkMoveColumn = null;
                return;
            }

            foreach (var column in _trackedBoard.Columns)
            {
                if (!column.IsArchiveColumnVisible)
                    continue;

                columns.Add(column);
            }

            if (SelectedBulkMoveColumn != null && !columns.Contains(SelectedBulkMoveColumn))
            {
                SelectedBulkMoveColumn = null;
            }
        }

        private bool IsTaskMatch(FlowTask task, string? searchText)
        {
            // Use enhanced filter criteria if available
            if (FilterCriteria != null && FilterCriteria.HasAnyFilter)
            {
                return IsTaskMatchWithCriteria(task, FilterCriteria);
            }

            // Legacy text-only search mode
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            var filter = searchText.Trim();
            var comparison = StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrWhiteSpace(task.Title) && task.Title.Contains(filter, comparison))
                return true;

            if (!string.IsNullOrWhiteSpace(task.Description) && task.Description.Contains(filter, comparison))
                return true;

            if (!string.IsNullOrWhiteSpace(task.Tags) && task.Tags.Contains(filter, comparison))
                return true;

            if (!string.IsNullOrWhiteSpace(task.Assignee) && task.Assignee.Contains(filter, comparison))
                return true;

            return false;
        }

    }

    /// <summary>
    /// View model for a swimlane row with per-column lane cells.
    /// </summary>
    public sealed class FlowKanbanLaneRowView
    {
        public FlowKanbanLaneRowView(FlowKanbanLane lane, ObservableCollection<FlowKanbanLaneCellView> cells)
        {
            Lane = lane ?? throw new ArgumentNullException(nameof(lane));
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public FlowKanbanLane Lane { get; }
        public ObservableCollection<FlowKanbanLaneCellView> Cells { get; }
    }

    /// <summary>
    /// View model for a swimlane cell, bound to a column and lane filter.
    /// </summary>
    public sealed class FlowKanbanLaneCellView : INotifyPropertyChanged
    {
        public FlowKanbanLaneCellView(FlowKanbanColumnData column, string? laneId)
        {
            Column = column ?? throw new ArgumentNullException(nameof(column));
            LaneId = laneId;
        }

        public FlowKanbanColumnData Column { get; }
        public string? LaneId { get; }

        public string LaneWipDisplay => Column.GetLaneWipDisplay(LaneId);
        public bool IsLaneWipExceeded => Column.IsLaneWipExceeded(LaneId);
        public bool HasLaneWipLimit => Column.GetLaneWipLimit(LaneId).HasValue;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Re-evaluates the computed WIP properties. Used when lane rows are kept
        /// alive across task moves instead of being rebuilt wholesale.
        /// </summary>
        internal void RefreshWipState()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LaneWipDisplay)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLaneWipExceeded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLaneWipLimit)));
        }
    }

    /// <summary>
    /// Event arguments for the CardMoving event, which fires before a card is moved.
    /// </summary>
    public class CardMovingEventArgs : EventArgs
    {
        public CardMovingEventArgs(FlowTask card, FlowKanbanColumnData sourceColumn, FlowKanbanColumnData targetColumn, int targetIndex)
        {
            Card = card;
            SourceColumn = sourceColumn;
            TargetColumn = targetColumn;
            TargetIndex = targetIndex;
        }

        /// <summary>The card being moved.</summary>
        public FlowTask Card { get; }

        /// <summary>The column the card is moving from.</summary>
        public FlowKanbanColumnData SourceColumn { get; }

        /// <summary>The column the card is moving to.</summary>
        public FlowKanbanColumnData TargetColumn { get; }

        /// <summary>The target index within the destination column.</summary>
        public int TargetIndex { get; }

        /// <summary>The target lane ID when moving into a swimlane.</summary>
        public string? TargetLaneId { get; internal set; }

        /// <summary>True when the move would exceed the effective WIP limit.</summary>
        public bool WouldExceedWip { get; internal set; }

        /// <summary>The effective WIP limit for the move target.</summary>
        public int? TargetWipLimit { get; internal set; }

        /// <summary>The effective WIP count after the move.</summary>
        public int TargetWipCount { get; internal set; }

        /// <summary>Set to true to cancel the move operation.</summary>
        public bool Cancel { get; set; }

        /// <summary>Optional reason for cancellation.</summary>
        public string? CancelReason { get; set; }
    }

    /// <summary>
    /// Event arguments for the CardMoved event, which fires after a card has been moved.
    /// </summary>
    public class CardMovedEventArgs : EventArgs
    {
        public CardMovedEventArgs(FlowTask card, FlowKanbanColumnData sourceColumn, FlowKanbanColumnData targetColumn)
        {
            Card = card;
            SourceColumn = sourceColumn;
            TargetColumn = targetColumn;
        }

        /// <summary>The card that was moved.</summary>
        public FlowTask Card { get; }

        /// <summary>The column the card was moved from.</summary>
        public FlowKanbanColumnData SourceColumn { get; }

        /// <summary>The column the card was moved to.</summary>
        public FlowKanbanColumnData TargetColumn { get; }
    }

    /// <summary>
    /// Event arguments for board-level edits.
    /// </summary>
    public sealed class BoardEditedEventArgs : EventArgs
    {
        public BoardEditedEventArgs(FlowKanbanData board, string? propertyName)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            PropertyName = propertyName;
        }

        public FlowKanbanData Board { get; }
        public string? PropertyName { get; }
    }

    /// <summary>
    /// Result of a card move operation with validation.
    /// </summary>
    public enum MoveResult
    {
        /// <summary>The move completed successfully.</summary>
        Success,

        /// <summary>The move was canceled by a CardMoving event handler.</summary>
        CanceledByEvent,

        /// <summary>The move was blocked due to WIP limit (hard enforcement).</summary>
        BlockedByWip,

        /// <summary>The move succeeded but exceeded WIP limit (soft warning).</summary>
        AllowedWithWipWarning,

        /// <summary>The source task or column was not found.</summary>
        NotFound
    }

    /// <summary>
    /// Placement options for the inline add card control.
    /// </summary>
    public enum FlowKanbanAddCardPlacement
    {
        Bottom,
        Top,
        Both
    }

    /// <summary>
    /// Compact layout sizing modes for columns.
    /// </summary>
    public enum FlowKanbanCompactColumnSizingMode
    {
        Adaptive,
        Manual
    }
}
