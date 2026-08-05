using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Flowery.Localization;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Helper class that provides a full programmatic API for managing a FlowKanban board.
    /// </summary>
    public class FlowKanbanManager
    {
        private readonly FlowKanban _kanban;
        private bool _isAttached;
        private bool _hasInitialized;

        public event EventHandler? Initialized;
        public event EventHandler? Teardown;
        public event EventHandler? SettingsLoaded;
        public event EventHandler? SettingsSaved;
        public event EventHandler? BoardLoaded;
        public event EventHandler? BoardSaved;
        public event EventHandler<FlowKanbanPersistenceFailedEventArgs>? PersistenceFailed;

        /// <summary>
        /// Creates a new manager for the specified Kanban board.
        /// </summary>
        /// <param name="kanban">The FlowKanban control to manage.</param>
        /// <param name="autoAttach">When true, automatically hooks Loaded/Unloaded for init/teardown.</param>
        public FlowKanbanManager(FlowKanban kanban, bool autoAttach = true)
        {
            _kanban = kanban ?? throw new ArgumentNullException(nameof(kanban));

            if (autoAttach)
            {
                AttachLifecycle();
            }
        }

        /// <summary>
        /// Gets the underlying FlowKanban control.
        /// </summary>
        public FlowKanban Kanban => _kanban;

        /// <summary>
        /// Gets the board data model.
        /// </summary>
        public FlowKanbanData Board => _kanban.Board;

        /// <summary>
        /// Gets or sets the view-only grouping mode.
        /// </summary>
        public FlowKanbanGroupBy GroupBy
        {
            get => _kanban.Board.GroupBy;
            set => _kanban.Board.GroupBy = value;
        }

        /// <summary>
        /// Gets the command history for undo/redo.
        /// </summary>
        public FlowKanbanCommandHistory CommandHistory => _kanban.CommandHistory;

        /// <summary>
        /// Enables undo/redo command history for Kanban operations.
        /// </summary>
        public bool EnableUndoRedo
        {
            get => _kanban.EnableUndoRedo;
            set => _kanban.EnableUndoRedo = value;
        }

        /// <summary>
        /// Gets all columns in the board.
        /// </summary>
        public IReadOnlyList<FlowKanbanColumnData> Columns => _kanban.Board.Columns;

        /// <summary>
        /// Gets all lanes in the board.
        /// </summary>
        public IReadOnlyList<FlowKanbanLane> Lanes => _kanban.Board.Lanes;

        private void ExecuteCommand(IKanbanCommand command)
        {
            _kanban.ExecuteCommand(command);
        }

        /// <summary>
        /// Gets the current board size tier.
        /// </summary>
        public DaisySize BoardSize
        {
            get => _kanban.BoardSize;
            set => _kanban.BoardSize = value;
        }

        /// <summary>
        /// Gets or sets whether column removals require confirmation.
        /// </summary>
        public bool ConfirmColumnRemovals
        {
            get => _kanban.ConfirmColumnRemovals;
            set => _kanban.ConfirmColumnRemovals = value;
        }

        /// <summary>
        /// Gets or sets whether card removals require confirmation.
        /// </summary>
        public bool ConfirmCardRemovals
        {
            get => _kanban.ConfirmCardRemovals;
            set => _kanban.ConfirmCardRemovals = value;
        }

        /// <summary>
        /// Gets or sets whether the board auto-saves after edits.
        /// </summary>
        public bool AutoSaveAfterEdits
        {
            get => _kanban.AutoSaveAfterEdits;
            set => _kanban.AutoSaveAfterEdits = value;
        }

        /// <summary>
        /// Controls where the inline "Add Card" control appears in a column.
        /// </summary>
        public FlowKanbanAddCardPlacement AddCardPlacement
        {
            get => _kanban.AddCardPlacement;
            set => _kanban.AddCardPlacement = value;
        }

        /// <summary>
        /// Gets the last board ID loaded or saved by the Kanban control.
        /// </summary>
        public string? LastBoardId => _kanban.LastBoardId;

        #region Lifecycle and Persistence

        /// <summary>
        /// Attaches to the Kanban Loaded/Unloaded events for automatic init/teardown.
        /// </summary>
        public void AttachLifecycle()
        {
            if (_isAttached)
                return;

            _kanban.Loaded += OnKanbanLoaded;
            _kanban.Unloaded += OnKanbanUnloaded;
            _isAttached = true;
        }

        /// <summary>
        /// Detaches from the Kanban Loaded/Unloaded events.
        /// </summary>
        public void DetachLifecycle()
        {
            if (!_isAttached)
                return;

            _kanban.Loaded -= OnKanbanLoaded;
            _kanban.Unloaded -= OnKanbanUnloaded;
            _isAttached = false;
        }

        /// <summary>
        /// Loads persisted settings and the last saved board.
        /// </summary>
        /// <returns>True if a board was loaded; otherwise false.</returns>
        public bool Initialize()
        {
            LoadSettings();
            var loadedBoard = _kanban.CurrentView == FlowKanbanView.Board && LoadLastBoard();
            RefreshBoards();
            _hasInitialized = true;

            Initialized?.Invoke(this, EventArgs.Empty);
            return loadedBoard;
        }

        /// <summary>
        /// Saves the current board and settings.
        /// </summary>
        public void Shutdown()
        {
            SaveBoard();
            SaveSettings();
            _hasInitialized = false;
            Teardown?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves the current board to persistent storage.
        /// </summary>
        public bool SaveBoard()
        {
            var success = _kanban.TrySaveBoard(out var error);
            if (success)
            {
                BoardSaved?.Invoke(this, EventArgs.Empty);
            }
            else if (error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.SaveBoard, error);
            }

            return success;
        }

        /// <summary>
        /// Loads the last saved board from persistent storage.
        /// </summary>
        public bool LoadLastBoard()
        {
            var success = _kanban.TryLoadLastBoard(out var error);
            if (success)
            {
                _kanban.CurrentView = FlowKanbanView.Board;
                BoardLoaded?.Invoke(this, EventArgs.Empty);
            }
            else if (error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.LoadBoard, error);
            }

            return success;
        }

        /// <summary>
        /// Loads a specific board by its ID.
        /// </summary>
        public bool LoadBoard(string boardId)
        {
            var success = _kanban.TryLoadBoard(boardId, out var error);
            if (success)
            {
                _kanban.CurrentView = FlowKanbanView.Board;
                BoardLoaded?.Invoke(this, EventArgs.Empty);
            }
            else if (error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.LoadBoard, error);
            }

            return success;
        }

        /// <summary>
        /// Refreshes the in-memory list of available boards.
        /// </summary>
        public void RefreshBoards()
        {
            _kanban.RefreshBoards();
        }

        /// <summary>
        /// Lists available boards using lightweight metadata.
        /// </summary>
        public IReadOnlyList<FlowBoardMetadata> ListAvailableBoards()
        {
            try
            {
                return _kanban.ListAvailableBoards();
            }
            catch (Exception ex)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.ListBoards, ex);
                return Array.Empty<FlowBoardMetadata>();
            }
        }

        /// <summary>
        /// Renames a stored board without loading it as the active board.
        /// </summary>
        public bool RenameBoard(string boardId, string newTitle)
        {
            var success = _kanban.TryRenameBoard(boardId, newTitle, out var error);
            if (!success && error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.RenameBoard, error);
            }

            return success;
        }

        /// <summary>
        /// Deletes a stored board by ID.
        /// </summary>
        public bool DeleteBoard(string boardId)
        {
            var success = _kanban.TryDeleteBoard(boardId, out var error);
            if (!success && error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.DeleteBoard, error);
            }

            return success;
        }

        /// <summary>
        /// Duplicates a stored board by ID.
        /// </summary>
        public bool DuplicateBoard(string boardId, out string? newBoardId)
        {
            var success = _kanban.TryDuplicateBoard(boardId, out newBoardId, out var error);
            if (!success && error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.DuplicateBoard, error);
            }

            return success;
        }

        /// <summary>
        /// Exports a stored board to JSON without loading it as the active board.
        /// </summary>
        public string? ExportBoardToJson(string boardId)
        {
            if (_kanban.TryExportBoard(boardId, out var json, out var error))
            {
                return json;
            }

            if (error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.ExportBoard, error);
            }

            return null;
        }

        /// <summary>
        /// Saves the current Kanban settings.
        /// </summary>
        public bool SaveSettings()
        {
            var success = _kanban.TrySaveSettings(out var error);
            if (success)
            {
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
            else if (error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.SaveSettings, error);
            }

            return success;
        }

        /// <summary>
        /// Loads Kanban settings from persistent storage.
        /// </summary>
        public bool LoadSettings(bool forceReload = false)
        {
            var success = _kanban.TryLoadSettings(forceReload, out var error);
            if (success)
            {
                SettingsLoaded?.Invoke(this, EventArgs.Empty);
            }
            else if (error != null)
            {
                RaisePersistenceFailed(FlowKanbanPersistenceOperation.LoadSettings, error);
            }

            return success;
        }

        /// <summary>
        /// Forces a settings reload from persistent storage.
        /// </summary>
        public bool ReloadSettings()
        {
            return LoadSettings(forceReload: true);
        }

        /// <summary>
        /// Applies all settings in one call.
        /// </summary>
        public void ApplySettings(bool confirmColumnRemovals, bool confirmCardRemovals, bool autoSaveAfterEdits)
        {
            _kanban.ConfirmColumnRemovals = confirmColumnRemovals;
            _kanban.ConfirmCardRemovals = confirmCardRemovals;
            _kanban.AutoSaveAfterEdits = autoSaveAfterEdits;
        }

        /// <summary>
        /// Enables auto-save after edits.
        /// </summary>
        public void EnableAutoSave() => _kanban.AutoSaveAfterEdits = true;

        /// <summary>
        /// Disables auto-save after edits.
        /// </summary>
        public void DisableAutoSave() => _kanban.AutoSaveAfterEdits = false;

        private void OnKanbanLoaded(object? sender, RoutedEventArgs e)
        {
            if (_hasInitialized)
                return;

            Initialize();
        }

        private void OnKanbanUnloaded(object? sender, RoutedEventArgs e)
        {
            if (!_hasInitialized)
                return;

            Shutdown();
        }

        private void RaisePersistenceFailed(FlowKanbanPersistenceOperation operation, Exception error)
        {
            _kanban.ReportPersistenceFailure(operation, error);
            PersistenceFailed?.Invoke(this, new FlowKanbanPersistenceFailedEventArgs(operation, error));
        }

        #endregion

        #region Column Operations

        /// <summary>
        /// Adds a new column with the specified title.
        /// </summary>
        /// <param name="title">The column title.</param>
        /// <returns>The newly created column.</returns>
        public FlowKanbanColumnData AddColumn(string title)
        {
            var column = new FlowKanbanColumnData { Title = title };
            _kanban.Board.Columns.Add(column);
            return column;
        }

        /// <summary>
        /// Adds a new column with the specified title at the given index.
        /// </summary>
        /// <param name="title">The column title.</param>
        /// <param name="index">The index at which to insert the column.</param>
        /// <returns>The newly created column.</returns>
        public FlowKanbanColumnData InsertColumn(string title, int index)
        {
            var column = new FlowKanbanColumnData { Title = title };
            index = Math.Clamp(index, 0, _kanban.Board.Columns.Count);
            _kanban.Board.Columns.Insert(index, column);
            return column;
        }

        /// <summary>
        /// Removes the specified column and all its tasks.
        /// </summary>
        /// <param name="column">The column to remove.</param>
        /// <returns>True if the column was removed, false if not found.</returns>
        public bool RemoveColumn(FlowKanbanColumnData column)
        {
            return _kanban.Board.Columns.Remove(column);
        }

        /// <summary>
        /// Removes a column by title.
        /// </summary>
        /// <param name="title">The column title.</param>
        /// <returns>True if the column was removed, false if not found.</returns>
        public bool RemoveColumnByTitle(string title)
        {
            var matches = _kanban.Board.Columns.Where(c => c.Title == title).ToList();
            if (matches.Count != 1)
                return false;

            return RemoveColumn(matches[0]);
        }

        /// <summary>
        /// Removes the column at the specified index.
        /// </summary>
        /// <param name="index">The column index.</param>
        /// <returns>True if removed, false if index was out of range.</returns>
        public bool RemoveColumnAt(int index)
        {
            if (index < 0 || index >= _kanban.Board.Columns.Count)
                return false;
            _kanban.Board.Columns.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Finds a column by its title.
        /// </summary>
        /// <param name="title">The column title.</param>
        /// <returns>The column, or null if not found.</returns>
        public FlowKanbanColumnData? FindColumnByTitle(string title)
        {
            return _kanban.Board.Columns.FirstOrDefault(c => c.Title == title);
        }

        /// <summary>
        /// Gets the column at the specified index.
        /// </summary>
        /// <param name="index">The column index.</param>
        /// <returns>The column, or null if index is out of range.</returns>
        public FlowKanbanColumnData? GetColumnAt(int index)
        {
            if (index < 0 || index >= _kanban.Board.Columns.Count)
                return null;
            return _kanban.Board.Columns[index];
        }

        /// <summary>
        /// Gets the index of a column.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <returns>The index, or -1 if not found.</returns>
        public int GetColumnIndex(FlowKanbanColumnData column)
        {
            return _kanban.Board.Columns.IndexOf(column);
        }

        /// <summary>
        /// Finds a column by its ID.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <returns>The column, or null if not found.</returns>
        public FlowKanbanColumnData? GetColumnById(string columnId)
        {
            if (string.IsNullOrWhiteSpace(columnId))
                return null;

            return _kanban.Board.Columns.FirstOrDefault(c => string.Equals(c.Id, columnId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets the tasks for the specified column.
        /// </summary>
        /// <param name="column">The column.</param>
        public IEnumerable<FlowTask> GetTasksForColumn(FlowKanbanColumnData column)
        {
            return column.Tasks;
        }

        /// <summary>
        /// Gets the tasks for the column with the specified ID.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        public IEnumerable<FlowTask> GetTasksForColumn(string columnId)
        {
            var column = GetColumnById(columnId);
            return column != null ? column.Tasks : Array.Empty<FlowTask>();
        }

        /// <summary>
        /// Gets the tasks for the column at the specified index.
        /// </summary>
        /// <param name="columnIndex">The column index.</param>
        public IEnumerable<FlowTask> GetTasksForColumn(int columnIndex)
        {
            var column = GetColumnAt(columnIndex);
            return column != null ? column.Tasks : Array.Empty<FlowTask>();
        }

        /// <summary>
        /// Moves a column to a new index.
        /// </summary>
        /// <param name="column">The column to move.</param>
        /// <param name="newIndex">The target index.</param>
        /// <returns>True if moved, false if column not found or index invalid.</returns>
        public bool MoveColumn(FlowKanbanColumnData column, int newIndex)
        {
            var currentIndex = GetColumnIndex(column);
            if (currentIndex < 0) return false;

            newIndex = Math.Clamp(newIndex, 0, _kanban.Board.Columns.Count - 1);
            if (currentIndex == newIndex) return true;

            _kanban.Board.Columns.RemoveAt(currentIndex);
            _kanban.Board.Columns.Insert(newIndex, column);
            return true;
        }

        /// <summary>
        /// Renames a column.
        /// </summary>
        /// <param name="column">The column to rename.</param>
        /// <param name="newTitle">The new title.</param>
        public void RenameColumn(FlowKanbanColumnData column, string newTitle)
        {
            column.Title = newTitle;
        }

        /// <summary>
        /// Gets the number of columns.
        /// </summary>
        public int ColumnCount => _kanban.Board.Columns.Count;

        /// <summary>
        /// Clears all columns from the board.
        /// </summary>
        public void ClearColumns()
        {
            _kanban.Board.Columns.Clear();
        }

        #endregion

        #region Lane Operations

        /// <summary>
        /// Adds a new lane with the specified title.
        /// </summary>
        /// <param name="title">The lane title.</param>
        /// <returns>The newly created lane.</returns>
        public FlowKanbanLane AddLane(string title)
        {
            var lane = new FlowKanbanLane { Title = title };
            _kanban.Board.Lanes.Add(lane);
            return lane;
        }

        /// <summary>
        /// Inserts a new lane at the specified index.
        /// </summary>
        /// <param name="title">The lane title.</param>
        /// <param name="index">The index at which to insert the lane.</param>
        /// <returns>The newly created lane.</returns>
        public FlowKanbanLane InsertLane(string title, int index)
        {
            var lane = new FlowKanbanLane { Title = title };
            index = Math.Clamp(index, 0, _kanban.Board.Lanes.Count);
            _kanban.Board.Lanes.Insert(index, lane);
            return lane;
        }

        /// <summary>
        /// Removes the specified lane and reassigns tasks to the fallback lane if provided.
        /// </summary>
        /// <param name="lane">The lane to remove.</param>
        /// <param name="fallbackLane">Optional lane to reassign tasks to.</param>
        /// <returns>True if the lane was removed.</returns>
        public bool RemoveLane(FlowKanbanLane lane, FlowKanbanLane? fallbackLane = null)
        {
            if (!_kanban.Board.Lanes.Remove(lane))
                return false;

            var fallbackLaneId = fallbackLane?.Id;
            foreach (var task in AllTasks)
            {
                if (string.Equals(task.LaneId, lane.Id, StringComparison.Ordinal))
                {
                    task.LaneId = fallbackLaneId;
                }
            }

            return true;
        }

        /// <summary>
        /// Removes a lane by its ID.
        /// </summary>
        /// <param name="laneId">The lane ID.</param>
        public bool RemoveLaneById(string laneId)
        {
            var lane = FindLaneById(laneId);
            return lane != null && RemoveLane(lane);
        }

        /// <summary>
        /// Removes the lane at the specified index.
        /// </summary>
        /// <param name="index">The lane index.</param>
        public bool RemoveLaneAt(int index)
        {
            if (index < 0 || index >= _kanban.Board.Lanes.Count)
                return false;

            var lane = _kanban.Board.Lanes[index];
            return RemoveLane(lane);
        }

        /// <summary>
        /// Finds a lane by its ID.
        /// </summary>
        /// <param name="laneId">The lane ID.</param>
        public FlowKanbanLane? FindLaneById(string laneId)
        {
            return _kanban.Board.Lanes.FirstOrDefault(l => l.Id == laneId);
        }

        /// <summary>
        /// Finds a lane by its title.
        /// </summary>
        /// <param name="title">The lane title.</param>
        public FlowKanbanLane? FindLaneByTitle(string title)
        {
            return _kanban.Board.Lanes.FirstOrDefault(l => l.Title == title);
        }

        /// <summary>
        /// Gets the lane at the specified index.
        /// </summary>
        /// <param name="index">The lane index.</param>
        public FlowKanbanLane? GetLaneAt(int index)
        {
            if (index < 0 || index >= _kanban.Board.Lanes.Count)
                return null;

            return _kanban.Board.Lanes[index];
        }

        /// <summary>
        /// Gets the index of a lane.
        /// </summary>
        /// <param name="lane">The lane.</param>
        public int GetLaneIndex(FlowKanbanLane lane)
        {
            return _kanban.Board.Lanes.IndexOf(lane);
        }

        /// <summary>
        /// Moves a lane to a new index.
        /// </summary>
        /// <param name="lane">The lane to move.</param>
        /// <param name="newIndex">The target index.</param>
        public bool MoveLane(FlowKanbanLane lane, int newIndex)
        {
            var currentIndex = GetLaneIndex(lane);
            if (currentIndex < 0) return false;

            newIndex = Math.Clamp(newIndex, 0, _kanban.Board.Lanes.Count - 1);
            if (currentIndex == newIndex) return true;

            _kanban.Board.Lanes.RemoveAt(currentIndex);
            _kanban.Board.Lanes.Insert(newIndex, lane);
            return true;
        }

        /// <summary>
        /// Renames a lane.
        /// </summary>
        /// <param name="lane">The lane to rename.</param>
        /// <param name="newTitle">The new title.</param>
        public void RenameLane(FlowKanbanLane lane, string newTitle)
        {
            lane.Title = newTitle;
        }

        /// <summary>
        /// Clears all lanes from the board.
        /// </summary>
        public void ClearLanes()
        {
            _kanban.Board.Lanes.Clear();
        }

        /// <summary>
        /// Gets tasks assigned to a lane.
        /// </summary>
        /// <param name="lane">The lane.</param>
        public IEnumerable<FlowTask> GetTasksForLane(FlowKanbanLane lane)
        {
            return AllTasks.Where(task => string.Equals(task.LaneId, lane.Id, StringComparison.Ordinal));
        }

        /// <summary>
        /// Assigns a task to a lane (or clears assignment when lane is null).
        /// </summary>
        /// <param name="task">The task to update.</param>
        /// <param name="lane">Target lane or null.</param>
        public void SetTaskLane(FlowTask task, FlowKanbanLane? lane)
        {
            task.LaneId = lane?.Id;
        }

        #endregion

        #region Task Operations

        /// <summary>
        /// Adds a task to the specified column.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="title">The task title.</param>
        /// <param name="description">Optional task description.</param>
        /// <param name="palette">Optional color palette.</param>
        /// <returns>The newly created task.</returns>
        public FlowTask AddTask(
            FlowKanbanColumnData column,
            string title,
            string? description = null,
            DaisyColor palette = DaisyColor.Default,
            string? laneId = null)
        {
            var task = new FlowTask
            {
                Title = title,
                Description = description ?? string.Empty,
                Palette = palette,
                LaneId = laneId
            };
            FlowKanbanWorkItemNumberHelper.EnsureTaskNumber(Board, task);
            ExecuteCommand(new AddCardCommand(column, task));
            FlowKanbanDoneColumnHelper.UpdateCompletedAtOnAdd(Board, column, task);
            return task;
        }

        /// <summary>
        /// Adds a task to the column at the specified index.
        /// </summary>
        /// <param name="columnIndex">The column index.</param>
        /// <param name="title">The task title.</param>
        /// <param name="description">Optional task description.</param>
        /// <param name="palette">Optional color palette.</param>
        /// <returns>The newly created task, or null if column not found.</returns>
        public FlowTask? AddTask(
            int columnIndex,
            string title,
            string? description = null,
            DaisyColor palette = DaisyColor.Default,
            string? laneId = null)
        {
            var column = GetColumnAt(columnIndex);
            if (column == null) return null;
            return AddTask(column, title, description, palette, laneId);
        }

        /// <summary>
        /// Adds a task to the column with the specified ID.
        /// </summary>
        /// <param name="columnId">The target column ID.</param>
        /// <param name="title">The task title.</param>
        /// <param name="description">Optional task description.</param>
        /// <param name="palette">Optional color palette.</param>
        /// <returns>The newly created task, or null if column not found.</returns>
        public FlowTask? AddTaskToColumn(
            string columnId,
            string title,
            string? description = null,
            DaisyColor palette = DaisyColor.Default,
            string? laneId = null)
        {
            var column = GetColumnById(columnId);
            return column != null ? AddTask(column, title, description, palette, laneId) : null;
        }

        /// <summary>
        /// Inserts a task at a specific position in a column by ID.
        /// </summary>
        /// <param name="columnId">The target column ID.</param>
        /// <param name="index">The position to insert at.</param>
        /// <param name="title">The task title.</param>
        /// <param name="description">Optional task description.</param>
        /// <param name="palette">Optional color palette.</param>
        /// <returns>The newly created task, or null if column not found.</returns>
        public FlowTask? InsertTaskToColumn(
            string columnId,
            int index,
            string title,
            string? description = null,
            DaisyColor palette = DaisyColor.Default,
            string? laneId = null)
        {
            var column = GetColumnById(columnId);
            return column != null ? InsertTask(column, index, title, description, palette, laneId) : null;
        }

        /// <summary>
        /// Finds a task within a specific column by ID.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <param name="taskId">The task ID.</param>
        /// <returns>The task, or null if not found.</returns>
        public FlowTask? FindTaskInColumn(string columnId, string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                return null;

            var column = GetColumnById(columnId);
            if (column == null)
                return null;

            return column.Tasks.FirstOrDefault(t => string.Equals(t.Id, taskId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Updates a task within a specific column by ID.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <param name="taskId">The task ID.</param>
        /// <param name="title">New title, or null to keep current.</param>
        /// <param name="description">New description, or null to keep current.</param>
        /// <param name="palette">New palette, or null to keep current.</param>
        /// <returns>True if updated, false if task not found.</returns>
        public bool UpdateTaskInColumn(
            string columnId,
            string taskId,
            string? title = null,
            string? description = null,
            DaisyColor? palette = null)
        {
            var task = FindTaskInColumn(columnId, taskId);
            if (task == null)
                return false;

            UpdateTask(task, title, description, palette);
            return true;
        }

        /// <summary>
        /// Removes a task from the specified column.
        /// </summary>
        /// <param name="column">The column containing the task.</param>
        /// <param name="task">The task to remove.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveTaskFromColumn(FlowKanbanColumnData column, FlowTask task)
        {
            if (!column.Tasks.Contains(task))
                return false;

            ExecuteCommand(new DeleteCardCommand(column, task));
            return true;
        }

        /// <summary>
        /// Removes a task from a specific column by ID.
        /// </summary>
        /// <param name="columnId">The column ID.</param>
        /// <param name="taskId">The task ID.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveTaskFromColumn(string columnId, string taskId)
        {
            var column = GetColumnById(columnId);
            if (column == null || string.IsNullOrWhiteSpace(taskId))
                return false;

            var task = column.Tasks.FirstOrDefault(t => string.Equals(t.Id, taskId, StringComparison.Ordinal));
            if (task == null)
                return false;

            ExecuteCommand(new DeleteCardCommand(column, task));
            return true;
        }

        /// <summary>
        /// Inserts a task at a specific position in a column.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="index">The position to insert at.</param>
        /// <param name="title">The task title.</param>
        /// <param name="description">Optional task description.</param>
        /// <param name="palette">Optional color palette.</param>
        /// <returns>The newly created task.</returns>
        public FlowTask InsertTask(
            FlowKanbanColumnData column,
            int index,
            string title,
            string? description = null,
            DaisyColor palette = DaisyColor.Default,
            string? laneId = null)
        {
            var task = new FlowTask
            {
                Title = title,
                Description = description ?? string.Empty,
                Palette = palette,
                LaneId = laneId
            };
            FlowKanbanWorkItemNumberHelper.EnsureTaskNumber(Board, task);
            index = Math.Clamp(index, 0, column.Tasks.Count);
            ExecuteCommand(new AddCardCommand(column, task, index));
            FlowKanbanDoneColumnHelper.UpdateCompletedAtOnAdd(Board, column, task);
            return task;
        }

        /// <summary>
        /// Removes a task from the board.
        /// </summary>
        /// <param name="task">The task to remove.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveTask(FlowTask task)
        {
            var column = FindColumnForTask(task);
            if (column == null)
                return false;

            ExecuteCommand(new DeleteCardCommand(column, task));
            return true;
        }

        /// <summary>
        /// Removes a task by its ID.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveTaskById(string taskId)
        {
            var task = FindTaskById(taskId);
            return task != null && RemoveTask(task);
        }

        /// <summary>
        /// Finds a task by its ID.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <returns>The task, or null if not found.</returns>
        public FlowTask? FindTaskById(string taskId)
        {
            foreach (var column in _kanban.Board.Columns)
            {
                var task = column.Tasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                    return task;
            }
            return null;
        }

        /// <summary>
        /// Finds all tasks matching a predicate.
        /// </summary>
        /// <param name="predicate">The search condition.</param>
        /// <returns>Matching tasks.</returns>
        public IEnumerable<FlowTask> FindTasks(Func<FlowTask, bool> predicate)
        {
            return _kanban.Board.Columns.SelectMany(c => c.Tasks).Where(predicate);
        }

        /// <summary>
        /// Finds the column containing a task.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <returns>The column, or null if not found.</returns>
        public FlowKanbanColumnData? FindColumnForTask(FlowTask task)
        {
            return _kanban.Board.Columns.FirstOrDefault(c => c.Tasks.Contains(task));
        }

        /// <summary>
        /// Finds the column containing a task by task ID.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <returns>The column, or null if not found.</returns>
        public FlowKanbanColumnData? FindColumnForTaskById(string taskId)
        {
            return _kanban.Board.Columns.FirstOrDefault(c => c.Tasks.Any(t => t.Id == taskId));
        }

        /// <summary>
        /// Attempts to get the index of a task within a column.
        /// </summary>
        /// <param name="task">The task to locate.</param>
        /// <param name="column">The column to search.</param>
        /// <param name="index">The found index.</param>
        /// <returns>True if found, false otherwise.</returns>
        public bool TryGetTaskIndex(FlowTask task, FlowKanbanColumnData column, out int index)
        {
            if (task == null || column == null)
            {
                index = -1;
                return false;
            }

            index = column.Tasks.IndexOf(task);
            return index >= 0;
        }

        /// <summary>
        /// Moves a task to a different column.
        /// </summary>
        /// <param name="task">The task to move.</param>
        /// <param name="targetColumn">The target column.</param>
        /// <param name="index">Optional index in the target column. If null, adds to end.</param>
        /// <returns>True if moved, false if task not found.</returns>
        public bool MoveTask(FlowTask task, FlowKanbanColumnData targetColumn, int? index = null)
        {
            var sourceColumn = FindColumnForTask(task);
            if (sourceColumn == null) return false;

            var sourceIndex = sourceColumn.Tasks.IndexOf(task);
            var targetIndex = index ?? targetColumn.Tasks.Count;
            var doneUpdate = GetDoneTimestampUpdate(sourceColumn, targetColumn);
            ExecuteCommand(new MoveCardCommand(task, sourceColumn, sourceIndex, targetColumn, targetIndex, doneUpdate.updateCompletedAt, doneUpdate.newCompletedAt));
            return true;
        }

        /// <summary>
        /// Attempts to move a task, raising CardMoving (cancelable) and CardMoved events.
        /// </summary>
        /// <param name="task">The task to move.</param>
        /// <param name="targetColumn">Destination column.</param>
        /// <param name="targetIndex">Optional index within target column.</param>
        /// <returns>True if the move succeeded; false if canceled by event handler or not found.</returns>
        public bool TryMoveTask(FlowTask task, FlowKanbanColumnData targetColumn, int? targetIndex = null, string? targetLaneId = null)
        {
            var result = TryMoveTaskWithWipEnforcement(task, targetColumn, targetIndex, targetLaneId, enforceHard: false);
            return result == MoveResult.Success || result == MoveResult.AllowedWithWipWarning;
        }

        private static string? ResolveLaneIdForMove(FlowTask task, string? targetLaneId)
        {
            if (FlowKanban.IsUnassignedLaneId(targetLaneId))
                return null;

            if (!string.IsNullOrWhiteSpace(targetLaneId))
                return FlowKanban.NormalizeLaneId(targetLaneId);

            return FlowKanban.NormalizeLaneId(task.LaneId);
        }

        private static string? NormalizeLaneIdForWip(string? laneId)
        {
            if (FlowKanban.IsUnassignedLaneId(laneId))
                return FlowKanban.UnassignedLaneId;

            return FlowKanban.NormalizeLaneId(laneId);
        }

        private static bool LaneIdsMatchForWip(string? left, string? right)
        {
            return string.Equals(NormalizeLaneIdForWip(left), NormalizeLaneIdForWip(right), StringComparison.Ordinal);
        }

        private static int CountLaneTasks(FlowKanbanColumnData column, string? laneKey)
        {
            if (laneKey == null)
            {
                return column.Tasks.Count(task => NormalizeLaneIdForWip(task.LaneId) == null);
            }

            return column.Tasks.Count(task => string.Equals(NormalizeLaneIdForWip(task.LaneId), laneKey, StringComparison.Ordinal));
        }

        private static (int count, int? limit, bool usesLaneOverride) GetEffectiveWipContext(
            FlowKanbanColumnData column,
            string? laneKey)
        {
            if (!string.IsNullOrWhiteSpace(laneKey)
                && column.LaneWipLimits.TryGetValue(laneKey, out var overrideLimit))
            {
                var laneCount = CountLaneTasks(column, laneKey);
                return (laneCount, overrideLimit, true);
            }

            return (column.Tasks.Count, column.WipLimit, false);
        }

        private static (int targetCount, int? targetLimit, bool wouldExceed, string? targetLaneId) GetMoveWipContext(
            FlowTask task,
            FlowKanbanColumnData sourceColumn,
            FlowKanbanColumnData targetColumn,
            string? targetLaneId)
        {
            var resolvedLaneId = ResolveLaneIdForMove(task, targetLaneId);
            var laneKey = NormalizeLaneIdForWip(resolvedLaneId);
            var (currentCount, limit, usesLaneOverride) = GetEffectiveWipContext(targetColumn, laneKey);
            var isCurrentlyInTargetLane = LaneIdsMatchForWip(task.LaneId, laneKey);
            var requiresIncrement = !ReferenceEquals(sourceColumn, targetColumn)
                                    || (usesLaneOverride && !isCurrentlyInTargetLane);
            var targetCount = requiresIncrement ? currentCount + 1 : currentCount;
            var wouldExceed = limit.HasValue && targetCount > limit.Value;

            return (targetCount, limit, wouldExceed, resolvedLaneId);
        }

        /// <summary>
        /// Validates whether a move would exceed WIP limits without performing the move.
        /// </summary>
        /// <param name="task">The task to check.</param>
        /// <param name="targetColumn">Destination column.</param>
        /// <param name="targetLaneId">Optional lane ID override.</param>
        /// <returns>True if the move would exceed WIP.</returns>
        public bool ValidateMoveWouldExceedWip(FlowTask task, FlowKanbanColumnData targetColumn, string? targetLaneId = null)
        {
            var sourceColumn = FindColumnForTask(task);
            if (sourceColumn == null)
                return false;

            var wipContext = GetMoveWipContext(task, sourceColumn, targetColumn, targetLaneId);
            return wipContext.wouldExceed;
        }

        private bool IsForwardColumnMove(FlowKanbanColumnData sourceColumn, FlowKanbanColumnData targetColumn)
        {
            if (ReferenceEquals(sourceColumn, targetColumn))
                return false;

            var columns = Board.Columns;
            var sourceIndex = columns.IndexOf(sourceColumn);
            var targetIndex = columns.IndexOf(targetColumn);
            if (sourceIndex < 0 || targetIndex < 0)
                return false;

            return targetIndex > sourceIndex;
        }

        private bool ShouldBlockForwardMove(FlowTask task, FlowKanbanColumnData sourceColumn, FlowKanbanColumnData targetColumn)
        {
            if (!task.IsBlocked)
                return false;

            if (IsArchiveColumn(targetColumn))
                return true;

            return IsForwardColumnMove(sourceColumn, targetColumn);
        }

        private static string BuildBlockedMoveMessage(FlowTask task)
        {
            var blockedLabel = FloweryLocalization.GetString("Kanban_Blocked", "Blocked");
            if (string.IsNullOrWhiteSpace(task.BlockedReason))
                return blockedLabel;

            return string.Concat(blockedLabel, ": ", task.BlockedReason.Trim());
        }

        private (bool updateCompletedAt, DateTime? newCompletedAt) GetDoneTimestampUpdate(
            FlowKanbanColumnData sourceColumn,
            FlowKanbanColumnData targetColumn)
        {
            var doneColumn = GetDoneColumn();
            if (doneColumn == null)
                return (false, null);

            var targetIsArchive = IsArchiveColumn(targetColumn);
            if (targetIsArchive)
                return (false, null);

            var sourceIsArchive = IsArchiveColumn(sourceColumn);
            var sourceIsDone = ReferenceEquals(sourceColumn, doneColumn);
            var targetIsDone = ReferenceEquals(targetColumn, doneColumn);

            if (targetIsDone)
            {
                if (sourceIsDone)
                    return (false, null);

                return (true, DateTime.Now);
            }

            if (sourceIsDone || sourceIsArchive)
                return (true, null);

            return (false, null);
        }

        /// <summary>
        /// Attempts to move a task with automatic WIP enforcement.
        /// </summary>
        /// <param name="task">The task to move.</param>
        /// <param name="targetColumn">Destination column.</param>
        /// <param name="targetIndex">Optional index within target column.</param>
        /// <param name="targetLaneId">Optional lane ID override.</param>
        /// <param name="enforceHard">When true, blocks moves that exceed WIP; when false, allows with warning.</param>
        /// <returns>Result of the move operation.</returns>
        public MoveResult TryMoveTaskWithWipEnforcement(
            FlowTask task,
            FlowKanbanColumnData targetColumn,
            int? targetIndex = null,
            string? targetLaneId = null,
            bool enforceHard = false)
        {
            var sourceColumn = FindColumnForTask(task);
            if (sourceColumn == null)
                return MoveResult.NotFound;

            var insertIndex = targetIndex ?? targetColumn.Tasks.Count;
            insertIndex = Math.Clamp(insertIndex, 0, targetColumn.Tasks.Count);

            var wipContext = GetMoveWipContext(task, sourceColumn, targetColumn, targetLaneId);
            var isBlockedForwardMove = ShouldBlockForwardMove(task, sourceColumn, targetColumn);

            // Raise CardMoving event
            var movingArgs = new CardMovingEventArgs(task, sourceColumn, targetColumn, insertIndex);
            movingArgs.TargetLaneId = wipContext.targetLaneId;
            movingArgs.TargetWipCount = wipContext.targetCount;
            movingArgs.TargetWipLimit = wipContext.targetLimit;
            movingArgs.WouldExceedWip = wipContext.wouldExceed;
            if (isBlockedForwardMove)
            {
                movingArgs.Cancel = true;
                movingArgs.CancelReason = BuildBlockedMoveMessage(task);
            }
            if (_kanban.RaiseCardMoving(movingArgs))
            {
                if (!string.IsNullOrWhiteSpace(movingArgs.CancelReason))
                {
                    _kanban.ShowStatusMessage(movingArgs.CancelReason, TimeSpan.FromSeconds(4));
                }
                return MoveResult.CanceledByEvent;
            }

            // Check WIP limits
            if (wipContext.wouldExceed && enforceHard)
                return MoveResult.BlockedByWip;

            var isSourceArchive = IsArchiveColumn(sourceColumn);
            var isTargetArchive = IsArchiveColumn(targetColumn);

            if (!isTargetArchive && !string.Equals(task.LaneId, wipContext.targetLaneId, StringComparison.Ordinal))
            {
                task.LaneId = wipContext.targetLaneId;
            }

            // Perform the move
            var sourceIndex = sourceColumn.Tasks.IndexOf(task);
            var doneUpdate = GetDoneTimestampUpdate(sourceColumn, targetColumn);
            if (isTargetArchive && !isSourceArchive)
            {
                ExecuteCommand(new ArchiveCardCommand(task, sourceColumn, sourceIndex, targetColumn, insertIndex, archive: true, doneUpdate.updateCompletedAt, doneUpdate.newCompletedAt));
            }
            else if (isSourceArchive && !isTargetArchive)
            {
                ExecuteCommand(new ArchiveCardCommand(task, sourceColumn, sourceIndex, targetColumn, insertIndex, archive: false, doneUpdate.updateCompletedAt, doneUpdate.newCompletedAt));
            }
            else
            {
                ExecuteCommand(new MoveCardCommand(task, sourceColumn, sourceIndex, targetColumn, insertIndex, doneUpdate.updateCompletedAt, doneUpdate.newCompletedAt));
            }

            // Raise CardMoved event
            var movedArgs = new CardMovedEventArgs(task, sourceColumn, targetColumn);
            _kanban.RaiseCardMoved(movedArgs);

            return wipContext.wouldExceed ? MoveResult.AllowedWithWipWarning : MoveResult.Success;
        }

        /// <summary>
        /// Moves a task within its current column to a new position.
        /// </summary>
        /// <param name="task">The task to reorder.</param>
        /// <param name="newIndex">The new position.</param>
        /// <returns>True if reordered, false if task not found.</returns>
        public bool ReorderTask(FlowTask task, int newIndex)
        {
            var column = FindColumnForTask(task);
            if (column == null) return false;

            var currentIndex = column.Tasks.IndexOf(task);
            newIndex = Math.Clamp(newIndex, 0, column.Tasks.Count - 1);

            if (currentIndex == newIndex) return true;

            column.Tasks.RemoveAt(currentIndex);
            column.Tasks.Insert(newIndex, task);
            return true;
        }

        /// <summary>
        /// Updates a task's properties.
        /// </summary>
        /// <param name="task">The task to update.</param>
        /// <param name="title">New title, or null to keep current.</param>
        /// <param name="description">New description, or null to keep current.</param>
        /// <param name="palette">New palette, or null to keep current.</param>
        public void UpdateTask(FlowTask task, string? title = null, string? description = null, DaisyColor? palette = null)
        {
            if (title == null && description == null && !palette.HasValue)
                return;

            ExecuteCommand(new EditCardCommand(task, title, description, palette));
        }

        /// <summary>
        /// Gets the total number of tasks across all columns.
        /// </summary>
        public int TotalTaskCount => _kanban.Board.Columns.Sum(c => c.Tasks.Count);

        /// <summary>
        /// Gets all tasks across all columns.
        /// </summary>
        public IEnumerable<FlowTask> AllTasks => _kanban.Board.Columns.SelectMany(c => c.Tasks);

        /// <summary>
        /// Clears all tasks from all columns (keeps column structure).
        /// </summary>
        public void ClearAllTasks()
        {
            foreach (var column in _kanban.Board.Columns)
            {
                column.Tasks.Clear();
            }
        }

        /// <summary>
        /// Clears all tasks from a specific column.
        /// </summary>
        /// <param name="column">The column to clear.</param>
        public void ClearColumnTasks(FlowKanbanColumnData column)
        {
            column.Tasks.Clear();
        }

        #endregion

        #region Subtask Operations

        /// <summary>
        /// Adds a subtask to a task.
        /// </summary>
        /// <param name="task">The parent task.</param>
        /// <param name="title">The subtask title.</param>
        /// <param name="isCompleted">Whether the subtask is completed.</param>
        /// <returns>The newly created subtask.</returns>
        public FlowSubtask AddSubtask(FlowTask task, string title, bool isCompleted = false)
        {
            var subtask = new FlowSubtask
            {
                Title = title,
                IsCompleted = isCompleted
            };
            task.Subtasks.Add(subtask);
            return subtask;
        }

        /// <summary>
        /// Removes a subtask from a task.
        /// </summary>
        /// <param name="task">The parent task.</param>
        /// <param name="subtask">The subtask to remove.</param>
        /// <returns>True if removed, false if not found.</returns>
        public bool RemoveSubtask(FlowTask task, FlowSubtask subtask)
        {
            return task.Subtasks.Remove(subtask);
        }

        /// <summary>
        /// Toggles a subtask's completion status.
        /// </summary>
        /// <param name="subtask">The subtask to toggle.</param>
        public void ToggleSubtask(FlowSubtask subtask)
        {
            subtask.IsCompleted = !subtask.IsCompleted;
        }

        /// <summary>
        /// Sets all subtasks' completion status.
        /// </summary>
        /// <param name="task">The parent task.</param>
        /// <param name="isCompleted">The completion status to set.</param>
        public void SetAllSubtasksCompleted(FlowTask task, bool isCompleted)
        {
            foreach (var subtask in task.Subtasks)
            {
                subtask.IsCompleted = isCompleted;
            }
        }

        /// <summary>
        /// Clears all subtasks from a task.
        /// </summary>
        /// <param name="task">The task whose subtasks to clear.</param>
        public void ClearSubtasks(FlowTask task)
        {
            task.Subtasks.Clear();
        }

        /// <summary>
        /// Gets the completion progress of subtasks.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <returns>A tuple of (completed count, total count).</returns>
        public (int completed, int total) GetSubtaskProgress(FlowTask task)
        {
            return (task.Subtasks.Count(s => s.IsCompleted), task.Subtasks.Count);
        }

        #endregion

        #region Zoom Operations

        /// <summary>
        /// Zooms in (increases board size).
        /// </summary>
        /// <returns>True if zoomed in, false if already at maximum.</returns>
        public bool ZoomIn()
        {
            if (_kanban.ZoomInCommand?.CanExecute(null) == true)
            {
                _kanban.ZoomInCommand.Execute(null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Zooms out (decreases board size).
        /// </summary>
        /// <returns>True if zoomed out, false if already at minimum.</returns>
        public bool ZoomOut()
        {
            if (_kanban.ZoomOutCommand?.CanExecute(null) == true)
            {
                _kanban.ZoomOutCommand.Execute(null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the board to a specific size tier.
        /// </summary>
        /// <param name="size">The target size.</param>
        public void SetZoom(DaisySize size)
        {
            _kanban.BoardSize = size;
        }

        /// <summary>
        /// Gets whether zoom in is possible.
        /// </summary>
        public bool CanZoomIn => _kanban.ZoomInCommand?.CanExecute(null) == true;

        /// <summary>
        /// Gets whether zoom out is possible.
        /// </summary>
        public bool CanZoomOut => _kanban.ZoomOutCommand?.CanExecute(null) == true;

        #endregion

        #region Board Operations

        /// <summary>
        /// Clears the entire board (all columns and tasks).
        /// </summary>
        public void ClearBoard()
        {
            _kanban.Board.Columns.Clear();
        }

        /// <summary>
        /// Resets the board to an initial state with default columns.
        /// </summary>
        /// <param name="columnTitles">The titles for the default columns.</param>
        public void ResetBoard(params string[] columnTitles)
        {
            ClearBoard();
            foreach (var title in columnTitles)
            {
                AddColumn(title);
            }
        }

        /// <summary>
        /// Creates a typical Kanban board with To Do, In Progress, and Done columns.
        /// </summary>
        public void CreateDefaultBoard()
        {
            ResetBoard(
                FloweryLocalization.GetString("Kanban_Column_Backlog"),
                FloweryLocalization.GetString("Kanban_Column_ToDo"),
                FloweryLocalization.GetString("Kanban_Column_InProgress"),
                FloweryLocalization.GetString("Kanban_Column_Done"));
        }

        /// <summary>
        /// Gets the board data as a serializable model.
        /// </summary>
        public FlowKanbanData GetBoardData() => _kanban.Board;

        /// <summary>
        /// Replaces the current board with new data.
        /// </summary>
        /// <param name="data">The new board data.</param>
        public void SetBoardData(FlowKanbanData data)
        {
            _kanban.Board = data;
        }

        /// <summary>
        /// Exports the board to JSON.
        /// </summary>
        /// <returns>JSON string representation of the board.</returns>
        public string ExportToJson()
        {
            return JsonSerializer.Serialize(_kanban.Board, FlowKanbanJsonContext.Default.FlowKanbanData);
        }

        /// <summary>
        /// Imports board data from JSON.
        /// </summary>
        /// <param name="json">JSON string to import.</param>
        /// <returns>True if import succeeded, false otherwise.</returns>
        public bool ImportFromJson(string json)
        {
            if (!FlowKanbanBoardSanitizer.TryLoadFromJson(json, out var data) || data == null)
                return false;

            _kanban.Board = data;
            return true;
        }

        #endregion

        #region WIP Limit Operations

        /// <summary>
        /// Sets the WIP limit for a column.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="limit">The WIP limit (null for unlimited).</param>
        public void SetColumnWipLimit(FlowKanbanColumnData column, int? limit)
        {
            column.WipLimit = limit;
        }

        /// <summary>
        /// Sets the WIP limit for a specific lane in a column.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="laneId">The lane ID.</param>
        /// <param name="limit">The lane WIP limit (null for unlimited).</param>
        public void SetLaneWipLimit(FlowKanbanColumnData column, string? laneId, int? limit)
        {
            if (column == null)
                return;

            var laneKey = NormalizeLaneIdForWip(laneId);
            if (string.IsNullOrWhiteSpace(laneKey))
                return;

            var updated = new Dictionary<string, int?>(column.LaneWipLimits, StringComparer.Ordinal);
            if (limit.HasValue)
            {
                updated[laneKey] = Math.Max(0, limit.Value);
            }
            else
            {
                updated.Remove(laneKey);
            }

            column.LaneWipLimits = updated;
        }

        /// <summary>
        /// Clears the WIP limit override for a specific lane.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="laneId">The lane ID.</param>
        public void ClearLaneWipLimit(FlowKanbanColumnData column, string? laneId)
        {
            SetLaneWipLimit(column, laneId, null);
        }

        /// <summary>
        /// Gets the effective WIP limit for a lane in a column.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="laneId">The lane ID.</param>
        public int? GetLaneWipLimit(FlowKanbanColumnData column, string? laneId)
        {
            return column.GetLaneWipLimit(laneId);
        }

        /// <summary>
        /// Gets whether a column has exceeded its WIP limit.
        /// </summary>
        /// <param name="column">The column to check.</param>
        /// <returns>True if WIP is exceeded.</returns>
        public bool IsWipExceeded(FlowKanbanColumnData column)
        {
            return column.IsWipExceeded;
        }

        /// <summary>
        /// Gets all columns that are over their WIP limit.
        /// </summary>
        /// <returns>Columns exceeding WIP limits.</returns>
        public IEnumerable<FlowKanbanColumnData> GetOverWipColumns()
        {
            return _kanban.Board.Columns.Where(c => c.IsWipExceeded);
        }

        /// <summary>
        /// Gets the count of columns exceeding WIP limits.
        /// </summary>
        public int OverWipColumnCount => _kanban.Board.Columns.Count(c => c.IsWipExceeded);

        #endregion

        #region Blocked State Operations

        /// <summary>
        /// Marks a task as blocked with an optional reason.
        /// </summary>
        /// <param name="task">The task to block.</param>
        /// <param name="reason">Optional reason for blocking.</param>
        public void SetBlocked(FlowTask task, string? reason = null)
        {
            ExecuteCommand(new SetBlockedCommand(task, true, reason));
        }

        /// <summary>
        /// Clears the blocked state from a task.
        /// </summary>
        /// <param name="task">The task to unblock.</param>
        public void ClearBlocked(FlowTask task)
        {
            ExecuteCommand(new SetBlockedCommand(task, false));
        }

        /// <summary>
        /// Gets all currently blocked tasks.
        /// </summary>
        /// <returns>All blocked tasks.</returns>
        public IEnumerable<FlowTask> GetBlockedTasks()
        {
            return AllTasks.Where(t => t.IsBlocked);
        }

        /// <summary>
        /// Gets the count of blocked tasks.
        /// </summary>
        public int BlockedTaskCount => AllTasks.Count(t => t.IsBlocked);

        /// <summary>
        /// Gets blocked tasks that have been blocked for more than the specified days.
        /// </summary>
        /// <param name="days">Minimum days blocked.</param>
        /// <returns>Tasks blocked for longer than specified days.</returns>
        public IEnumerable<FlowTask> GetBlockedTasksOlderThan(int days)
        {
            return GetBlockedTasks().Where(t => t.BlockedDays.HasValue && t.BlockedDays.Value > days);
        }

        #endregion

        #region Column Policy Operations

        /// <summary>
        /// Sets the policy text for a column.
        /// </summary>
        /// <param name="column">The target column.</param>
        /// <param name="policyText">The policy text (DoD, entry criteria, etc.).</param>
        public void SetColumnPolicy(FlowKanbanColumnData column, string? policyText)
        {
            column.PolicyText = policyText;
        }

        /// <summary>
        /// Gets all columns that have policy text defined.
        /// </summary>
        /// <returns>Columns with policies.</returns>
        public IEnumerable<FlowKanbanColumnData> GetColumnsWithPolicies()
        {
            return _kanban.Board.Columns.Where(c => c.HasPolicy);
        }

        #endregion

        #region Archive Column Helpers

        /// <summary>
        /// Gets the archive column, if configured.
        /// </summary>
        public FlowKanbanColumnData? GetArchiveColumn()
        {
            var archiveId = Board.ArchiveColumnId;
            return string.IsNullOrWhiteSpace(archiveId) ? null : GetColumnById(archiveId);
        }

        /// <summary>
        /// Returns true if the column is the configured archive column.
        /// </summary>
        public bool IsArchiveColumn(FlowKanbanColumnData column)
        {
            if (column == null)
                return false;

            var archiveId = Board.ArchiveColumnId;
            return !string.IsNullOrWhiteSpace(archiveId)
                   && string.Equals(column.Id, archiveId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the archive column exists and returns it.
        /// </summary>
        public FlowKanbanColumnData EnsureArchiveColumn()
        {
            var existing = GetArchiveColumn();
            if (existing != null)
                return existing;

            var archiveColumn = new FlowKanbanColumnData
            {
                Title = FloweryLocalization.GetString("Kanban_ArchiveColumn", "Archive")
            };

            if (string.IsNullOrWhiteSpace(Board.ArchiveColumnId))
            {
                Board.ArchiveColumnId = archiveColumn.Id;
            }
            else
            {
                archiveColumn.Id = Board.ArchiveColumnId!;
            }

            Board.Columns.Add(archiveColumn);
            return archiveColumn;
        }

        /// <summary>
        /// Sets the archive column for the board.
        /// </summary>
        public void SetArchiveColumn(FlowKanbanColumnData column)
        {
            if (column == null)
                throw new ArgumentNullException(nameof(column));

            Board.ArchiveColumnId = column.Id;
        }

        private FlowKanbanColumnData? ResolveUnarchiveTarget(FlowTask task)
        {
            if (!string.IsNullOrWhiteSpace(task.ArchivedFromColumnId))
            {
                var archivedFrom = GetColumnById(task.ArchivedFromColumnId);
                if (archivedFrom != null)
                    return archivedFrom;
            }

            foreach (var column in Board.Columns)
            {
                if (!IsArchiveColumn(column))
                    return column;
            }

            return Board.Columns.FirstOrDefault();
        }

        private void MoveTaskToArchiveCore(
            FlowTask task,
            FlowKanbanColumnData sourceColumn,
            int sourceIndex,
            FlowKanbanColumnData archiveColumn,
            int? targetIndex = null)
        {
            if (!ReferenceEquals(sourceColumn, archiveColumn))
            {
                sourceColumn.Tasks.Remove(task);
                var insertIndex = targetIndex ?? archiveColumn.Tasks.Count;
                insertIndex = Math.Clamp(insertIndex, 0, archiveColumn.Tasks.Count);
                archiveColumn.Tasks.Insert(insertIndex, task);
            }

            task.ArchivedFromColumnId = sourceColumn.Id;
            task.ArchivedFromIndex = sourceIndex >= 0 ? sourceIndex : null;
            task.IsArchived = true;
        }

        #endregion

        #region Done Column Helpers

        /// <summary>
        /// Gets the Done column, if configured.
        /// </summary>
        public FlowKanbanColumnData? GetDoneColumn()
        {
            var doneId = Board.DoneColumnId;
            return string.IsNullOrWhiteSpace(doneId) ? null : GetColumnById(doneId);
        }

        /// <summary>
        /// Returns true if the column is the configured Done column.
        /// </summary>
        public bool IsDoneColumn(FlowKanbanColumnData column)
        {
            if (column == null)
                return false;

            var doneId = Board.DoneColumnId;
            return !string.IsNullOrWhiteSpace(doneId)
                   && string.Equals(column.Id, doneId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Sets the Done column for the board.
        /// </summary>
        public void SetDoneColumn(FlowKanbanColumnData column)
        {
            if (column == null)
                throw new ArgumentNullException(nameof(column));

            Board.DoneColumnId = column.Id;
        }

        #endregion

        #region Done Aging

        /// <summary>
        /// Ensures Done timestamps are populated for tasks in the Done column.
        /// Clears timestamps for active tasks outside Done.
        /// </summary>
        public void SyncDoneColumnTimestamps()
        {
            var doneColumn = GetDoneColumn();
            if (doneColumn == null)
                return;

            foreach (var column in Board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (task.IsArchived)
                        continue;

                    if (ReferenceEquals(column, doneColumn))
                    {
                        if (!task.CompletedAt.HasValue)
                        {
                            task.CompletedAt = task.ActualEndDate ?? DateTime.Now;
                        }
                    }
                    else if (task.CompletedAt.HasValue)
                    {
                        task.CompletedAt = null;
                    }
                }
            }
        }

        /// <summary>
        /// Archives Done tasks that exceed the configured aging window.
        /// </summary>
        /// <returns>Number of tasks archived.</returns>
        public int AutoArchiveDoneTasks()
        {
            if (!Board.AutoArchiveDoneEnabled || Board.AutoArchiveDoneDays <= 0)
                return 0;

            var doneColumn = GetDoneColumn();
            if (doneColumn == null)
                return 0;

            var cutoff = DateTime.Now.AddDays(-Board.AutoArchiveDoneDays);
            var candidates = doneColumn.Tasks
                .Where(task => !task.IsArchived && !task.IsBlocked)
                .ToList();

            var archivedCount = 0;
            foreach (var task in candidates)
            {
                var doneAt = EnsureDoneTimestamp(task);
                if (doneAt <= cutoff)
                {
                    ArchiveTask(task);
                    archivedCount++;
                }
            }

            return archivedCount;
        }

        private static DateTime EnsureDoneTimestamp(FlowTask task)
        {
            if (task.CompletedAt.HasValue)
                return task.CompletedAt.Value;

            if (task.ActualEndDate.HasValue)
            {
                task.CompletedAt = task.ActualEndDate.Value;
                return task.ActualEndDate.Value;
            }

            var now = DateTime.Now;
            task.CompletedAt = now;
            return now;
        }

        #endregion

        #region Archive Operations

        /// <summary>
        /// Archives a task.
        /// </summary>
        /// <param name="task">The task to archive.</param>
        public void ArchiveTask(FlowTask task)
        {
            var sourceColumn = FindColumnForTask(task);
            if (sourceColumn == null)
                return;

            var archiveColumn = EnsureArchiveColumn();
            if (ReferenceEquals(sourceColumn, archiveColumn) && task.IsArchived)
                return;
            if (task.IsBlocked)
            {
                _kanban.ShowStatusMessage(BuildBlockedMoveMessage(task), TimeSpan.FromSeconds(4));
                return;
            }

            var sourceIndex = sourceColumn.Tasks.IndexOf(task);
            var targetIndex = archiveColumn.Tasks.Count;
            var doneUpdate = GetDoneTimestampUpdate(sourceColumn, archiveColumn);
            ExecuteCommand(new ArchiveCardCommand(task, sourceColumn, sourceIndex, archiveColumn, targetIndex, archive: true, doneUpdate.updateCompletedAt, doneUpdate.newCompletedAt));

            if (!ReferenceEquals(sourceColumn, archiveColumn))
            {
                _kanban.RaiseCardMoved(new CardMovedEventArgs(task, sourceColumn, archiveColumn));
            }
        }

        /// <summary>
        /// Unarchives a task.
        /// </summary>
        /// <param name="task">The task to unarchive.</param>
        public void UnarchiveTask(FlowTask task)
        {
            var sourceColumn = FindColumnForTask(task);
            if (sourceColumn == null)
                return;

            var targetColumn = ResolveUnarchiveTarget(task) ?? sourceColumn;
            var targetIndex = task.ArchivedFromIndex.HasValue
                ? Math.Clamp(task.ArchivedFromIndex.Value, 0, targetColumn.Tasks.Count)
                : targetColumn.Tasks.Count;

            var doneUpdate = GetDoneTimestampUpdate(sourceColumn, targetColumn);
            ExecuteCommand(new ArchiveCardCommand(task, sourceColumn, sourceColumn.Tasks.IndexOf(task), targetColumn, targetIndex, archive: false, doneUpdate.updateCompletedAt, doneUpdate.newCompletedAt));

            if (!ReferenceEquals(sourceColumn, targetColumn))
            {
                _kanban.RaiseCardMoved(new CardMovedEventArgs(task, sourceColumn, targetColumn));
            }
        }

        /// <summary>
        /// Archives all completed tasks in a column (tasks with 100% progress).
        /// </summary>
        /// <param name="column">The column to process.</param>
        /// <returns>Number of tasks archived.</returns>
        public int ArchiveCompletedTasks(FlowKanbanColumnData column)
        {
            var completed = column.Tasks.Where(t => t.EffectiveProgress >= 100 && !t.IsArchived && !t.IsBlocked).ToList();
            foreach (var task in completed)
            {
                ArchiveTask(task);
            }
            return completed.Count;
        }

        /// <summary>
        /// Gets all archived tasks across the board.
        /// </summary>
        /// <returns>All archived tasks.</returns>
        public IEnumerable<FlowTask> GetArchivedTasks()
        {
            return AllTasks.Where(t => t.IsArchived);
        }

        /// <summary>
        /// Gets all non-archived (active) tasks.
        /// </summary>
        /// <returns>Active tasks.</returns>
        public IEnumerable<FlowTask> GetActiveTasks()
        {
            return AllTasks.Where(t => !t.IsArchived);
        }

        /// <summary>
        /// Gets the count of archived tasks.
        /// </summary>
        public int ArchivedTaskCount => AllTasks.Count(t => t.IsArchived);

        /// <summary>
        /// Permanently removes all archived tasks from the board.
        /// </summary>
        /// <returns>Number of tasks removed.</returns>
        public int PurgeArchivedTasks()
        {
            var count = 0;
            foreach (var column in _kanban.Board.Columns)
            {
                var archived = column.Tasks.Where(t => t.IsArchived).ToList();
                foreach (var task in archived)
                {
                    column.Tasks.Remove(task);
                    count++;
                }
            }
            return count;
        }

        #endregion

        #region Selection Operations

        /// <summary>
        /// Gets all currently selected tasks.
        /// </summary>
        public IReadOnlyList<FlowTask> SelectedTasks => AllTasks.Where(t => t.IsSelected).ToList();

        /// <summary>
        /// Gets the count of selected tasks.
        /// </summary>
        public int SelectedCount => AllTasks.Count(t => t.IsSelected);

        /// <summary>
        /// Gets whether any tasks are selected.
        /// </summary>
        public bool HasSelection => AllTasks.Any(t => t.IsSelected);

        /// <summary>
        /// Selects a task.
        /// </summary>
        /// <param name="task">The task to select.</param>
        public void SelectTask(FlowTask task)
        {
            task.IsSelected = true;
        }

        /// <summary>
        /// Deselects a task.
        /// </summary>
        /// <param name="task">The task to deselect.</param>
        public void DeselectTask(FlowTask task)
        {
            task.IsSelected = false;
        }

        /// <summary>
        /// Toggles a task's selection state.
        /// </summary>
        /// <param name="task">The task to toggle.</param>
        public void ToggleSelection(FlowTask task)
        {
            task.IsSelected = !task.IsSelected;
        }

        /// <summary>
        /// Selects all visible (non-archived) tasks.
        /// </summary>
        public void SelectAll()
        {
            foreach (var task in GetActiveTasks())
            {
                task.IsSelected = true;
            }
        }

        /// <summary>
        /// Deselects all tasks.
        /// </summary>
        public void DeselectAll()
        {
            foreach (var task in AllTasks.Where(t => t.IsSelected))
            {
                task.IsSelected = false;
            }
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Moves all selected tasks to a target column.
        /// </summary>
        /// <param name="targetColumn">Destination column.</param>
        /// <returns>Number of tasks moved.</returns>
        public int BulkMove(FlowKanbanColumnData targetColumn)
        {
            var selected = SelectedTasks.ToList();
            var movedCount = 0;
            foreach (var task in selected)
            {
                var result = TryMoveTaskWithWipEnforcement(task, targetColumn, null, enforceHard: false);
                if (result == MoveResult.Success || result == MoveResult.AllowedWithWipWarning)
                {
                    movedCount++;
                }
            }
            return movedCount;
        }

        /// <summary>
        /// Sets priority for all selected tasks.
        /// </summary>
        /// <param name="priority">The priority to set.</param>
        /// <returns>Number of tasks updated.</returns>
        public int BulkSetPriority(FlowTaskPriority priority)
        {
            var selected = SelectedTasks.ToList();
            foreach (var task in selected)
            {
                task.Priority = priority;
            }
            return selected.Count;
        }

        /// <summary>
        /// Sets tags for all selected tasks.
        /// </summary>
        /// <param name="tags">Tags string to apply (comma separated).</param>
        /// <returns>Number of tasks updated.</returns>
        public int BulkSetTags(string? tags)
        {
            var selected = SelectedTasks.ToList();
            var normalized = string.IsNullOrWhiteSpace(tags) ? null : tags;
            foreach (var task in selected)
            {
                task.Tags = normalized;
            }
            return selected.Count;
        }

        /// <summary>
        /// Sets due date for all selected tasks.
        /// </summary>
        /// <param name="dueDate">The due date to set (null clears).</param>
        /// <returns>Number of tasks updated.</returns>
        public int BulkSetDueDate(DateTime? dueDate)
        {
            var selected = SelectedTasks.ToList();
            foreach (var task in selected)
            {
                task.PlannedEndDate = dueDate;
            }
            return selected.Count;
        }

        /// <summary>
        /// Sets blocked state for all selected tasks.
        /// </summary>
        /// <param name="blocked">Whether to block or unblock.</param>
        /// <param name="reason">Optional reason for blocking.</param>
        /// <returns>Number of tasks updated.</returns>
        public int BulkSetBlocked(bool blocked, string? reason = null)
        {
            var selected = SelectedTasks.ToList();
            foreach (var task in selected)
            {
                if (blocked)
                    SetBlocked(task, reason);
                else
                    ClearBlocked(task);
            }
            return selected.Count;
        }

        /// <summary>
        /// Archives all selected tasks.
        /// </summary>
        /// <returns>Number of tasks archived.</returns>
        public int BulkArchive()
        {
            var selected = SelectedTasks.ToList();
            foreach (var task in selected)
            {
                ArchiveTask(task);
            }
            return selected.Count;
        }

        /// <summary>
        /// Deletes all selected tasks.
        /// </summary>
        /// <returns>Number of tasks deleted.</returns>
        public int BulkDelete()
        {
            var selected = SelectedTasks.ToList();
            foreach (var task in selected)
            {
                RemoveTask(task);
            }
            return selected.Count;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Gets statistics about the board.
        /// </summary>
        public BoardStatistics GetStatistics()
        {
            return new BoardStatistics
            {
                ColumnCount = ColumnCount,
                TotalTaskCount = TotalTaskCount,
                TasksPerColumn = _kanban.Board.Columns
                    .GroupBy(c => c.Title, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Sum(c => c.Tasks.Count), StringComparer.Ordinal),
                EmptyColumns = _kanban.Board.Columns.Count(c => c.Tasks.Count == 0)
            };
        }

        #endregion
    }

    /// <summary>
    /// Statistics about a Kanban board.
    /// </summary>
    public class BoardStatistics
    {
        /// <summary>
        /// Number of columns.
        /// </summary>
        public int ColumnCount { get; init; }

        /// <summary>
        /// Total number of tasks.
        /// </summary>
        public int TotalTaskCount { get; init; }

        /// <summary>
        /// Tasks grouped by column title.
        /// </summary>
        public Dictionary<string, int> TasksPerColumn { get; init; } = new();

        /// <summary>
        /// Number of columns with no tasks.
        /// </summary>
        public int EmptyColumns { get; init; }
    }

    /// <summary>
    /// Persistence operations supported by FlowKanbanManager.
    /// </summary>
    public enum FlowKanbanPersistenceOperation
    {
        LoadSettings,
        SaveSettings,
        LoadBoard,
        SaveBoard,
        ListBoards,
        RenameBoard,
        DeleteBoard,
        DuplicateBoard,
        ExportBoard
    }

    /// <summary>
    /// Provides details for persistence failures.
    /// </summary>
    public sealed class FlowKanbanPersistenceFailedEventArgs : EventArgs
    {
        public FlowKanbanPersistenceFailedEventArgs(FlowKanbanPersistenceOperation operation, Exception exception)
        {
            Operation = operation;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public FlowKanbanPersistenceOperation Operation { get; }

        public Exception Exception { get; }
    }
}
