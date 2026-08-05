using System;
using System.Collections.Generic;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Interface for reversible Kanban operations.
    /// </summary>
    public interface IKanbanCommand
    {
        /// <summary>Executes the command.</summary>
        void Execute();

        /// <summary>Reverses the command.</summary>
        void Undo();

        /// <summary>Human-readable description of the command.</summary>
        string Description { get; }
    }

    /// <summary>
    /// Command for moving a card between columns or positions.
    /// </summary>
    public class MoveCardCommand(
        FlowTask task,
        FlowKanbanColumnData sourceColumn,
        int sourceIndex,
        FlowKanbanColumnData targetColumn,
        int targetIndex,
        bool updateCompletedAt = false,
        DateTime? newCompletedAt = null) : IKanbanCommand
    {
        private readonly FlowKanbanColumnData _sourceColumn = sourceColumn ?? throw new ArgumentNullException(nameof(sourceColumn));
        private readonly FlowKanbanColumnData _targetColumn = targetColumn ?? throw new ArgumentNullException(nameof(targetColumn));
        private readonly FlowTask _task = task ?? throw new ArgumentNullException(nameof(task));
        private readonly int _sourceIndex = sourceIndex;
        private readonly int _targetIndex = targetIndex;
        private readonly bool _updateCompletedAt = updateCompletedAt;
        private readonly DateTime? _oldCompletedAt = updateCompletedAt ? task.CompletedAt : null;
        private readonly DateTime? _newCompletedAt = newCompletedAt;

        public string Description => $"Move '{_task.Title}' to {_targetColumn.Title}";

        public void Execute()
        {
            var insertIndex = _targetIndex;
            if (ReferenceEquals(_sourceColumn, _targetColumn) && _sourceIndex < _targetIndex)
            {
                insertIndex--;
            }

            _sourceColumn.Tasks.Remove(_task);
            insertIndex = Math.Clamp(insertIndex, 0, _targetColumn.Tasks.Count);
            _targetColumn.Tasks.Insert(insertIndex, _task);
            if (_updateCompletedAt)
            {
                _task.CompletedAt = _newCompletedAt;
            }
        }

        public void Undo()
        {
            _targetColumn.Tasks.Remove(_task);
            var insertIndex = Math.Min(_sourceIndex, _sourceColumn.Tasks.Count);
            _sourceColumn.Tasks.Insert(insertIndex, _task);
            if (_updateCompletedAt)
            {
                _task.CompletedAt = _oldCompletedAt;
            }
        }
    }

    /// <summary>
    /// Command for adding a new card.
    /// </summary>
    public class AddCardCommand : IKanbanCommand
    {
        private readonly FlowKanbanColumnData _column;
        private readonly FlowTask _task;
        private readonly int _index;

        public AddCardCommand(FlowKanbanColumnData column, FlowTask task, int? index = null)
        {
            _column = column ?? throw new ArgumentNullException(nameof(column));
            _task = task ?? throw new ArgumentNullException(nameof(task));
            _index = index ?? column.Tasks.Count;
        }

        public string Description => $"Add '{_task.Title}' to {_column.Title}";

        public void Execute()
        {
            var insertIndex = Math.Min(_index, _column.Tasks.Count);
            _column.Tasks.Insert(insertIndex, _task);
        }

        public void Undo()
        {
            _column.Tasks.Remove(_task);
        }
    }

    /// <summary>
    /// Command for deleting a card.
    /// </summary>
    public class DeleteCardCommand : IKanbanCommand
    {
        private readonly FlowKanbanColumnData _column;
        private readonly FlowTask _task;
        private readonly int _index;

        public DeleteCardCommand(FlowKanbanColumnData column, FlowTask task)
        {
            _column = column ?? throw new ArgumentNullException(nameof(column));
            _task = task ?? throw new ArgumentNullException(nameof(task));
            _index = column.Tasks.IndexOf(task);
        }

        public string Description => $"Delete '{_task.Title}'";

        public void Execute()
        {
            _column.Tasks.Remove(_task);
        }

        public void Undo()
        {
            var insertIndex = Math.Min(_index, _column.Tasks.Count);
            _column.Tasks.Insert(insertIndex, _task);
        }
    }

    /// <summary>
    /// Command for editing a card's basic properties.
    /// </summary>
    public class EditCardCommand : IKanbanCommand
    {
        private readonly FlowTask _task;
        private readonly string _oldTitle;
        private readonly string _newTitle;
        private readonly string _oldDescription;
        private readonly string _newDescription;
        private readonly DaisyColor _oldPalette;
        private readonly DaisyColor _newPalette;

        public EditCardCommand(FlowTask task, string? newTitle = null, string? newDescription = null, DaisyColor? newPalette = null)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task));
            _oldTitle = task.Title;
            _oldDescription = task.Description;
            _oldPalette = task.Palette;
            _newTitle = newTitle ?? task.Title;
            _newDescription = newDescription ?? task.Description;
            _newPalette = newPalette ?? task.Palette;
        }

        public string Description => $"Edit '{_oldTitle}'";

        public void Execute()
        {
            _task.Title = _newTitle;
            _task.Description = _newDescription;
            _task.Palette = _newPalette;
        }

        public void Undo()
        {
            _task.Title = _oldTitle;
            _task.Description = _oldDescription;
            _task.Palette = _oldPalette;
        }
    }

    /// <summary>
    /// Command for setting blocked state.
    /// </summary>
    public class SetBlockedCommand : IKanbanCommand
    {
        private readonly FlowTask _task;
        private readonly bool _oldIsBlocked;
        private readonly string? _oldBlockedReason;
        private readonly DateTime? _oldBlockedSince;
        private readonly bool _newIsBlocked;
        private readonly string? _newBlockedReason;

        public SetBlockedCommand(FlowTask task, bool isBlocked, string? reason = null)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task));
            _oldIsBlocked = task.IsBlocked;
            _oldBlockedReason = task.BlockedReason;
            _oldBlockedSince = task.BlockedSince;
            _newIsBlocked = isBlocked;
            _newBlockedReason = reason;
        }

        public string Description => _newIsBlocked ? $"Block '{_task.Title}'" : $"Unblock '{_task.Title}'";

        public void Execute()
        {
            _task.IsBlocked = _newIsBlocked;
            if (_newIsBlocked && _newBlockedReason != null)
                _task.BlockedReason = _newBlockedReason;
        }

        public void Undo()
        {
            _task.IsBlocked = _oldIsBlocked;
            _task.BlockedReason = _oldBlockedReason;
            _task.BlockedSince = _oldBlockedSince;
        }
    }

    /// <summary>
    /// Command for archiving/unarchiving a task.
    /// </summary>
    public class ArchiveCardCommand : IKanbanCommand
    {
        private readonly FlowTask _task;
        private readonly FlowKanbanColumnData _sourceColumn;
        private readonly FlowKanbanColumnData _targetColumn;
        private readonly int _sourceIndex;
        private readonly int _targetIndex;
        private readonly bool _archive;
        private readonly bool _oldIsArchived;
        private readonly DateTime? _oldArchivedAt;
        private readonly string? _oldArchivedFromColumnId;
        private readonly int? _oldArchivedFromIndex;
        private readonly bool _updateCompletedAt;
        private readonly DateTime? _oldCompletedAt;
        private readonly DateTime? _newCompletedAt;

        public ArchiveCardCommand(
            FlowTask task,
            FlowKanbanColumnData sourceColumn,
            int sourceIndex,
            FlowKanbanColumnData targetColumn,
            int targetIndex,
            bool archive,
            bool updateCompletedAt = false,
            DateTime? newCompletedAt = null)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task));
            _sourceColumn = sourceColumn ?? throw new ArgumentNullException(nameof(sourceColumn));
            _targetColumn = targetColumn ?? throw new ArgumentNullException(nameof(targetColumn));
            _sourceIndex = sourceIndex;
            _targetIndex = targetIndex;
            _archive = archive;
            _oldIsArchived = task.IsArchived;
            _oldArchivedAt = task.ArchivedAt;
            _oldArchivedFromColumnId = task.ArchivedFromColumnId;
            _oldArchivedFromIndex = task.ArchivedFromIndex;
            _updateCompletedAt = updateCompletedAt;
            _oldCompletedAt = updateCompletedAt ? task.CompletedAt : null;
            _newCompletedAt = newCompletedAt;
        }

        public string Description => _archive ? $"Archive '{_task.Title}'" : $"Unarchive '{_task.Title}'";

        public void Execute()
        {
            MoveTask(_task, _sourceColumn, _targetColumn, _targetIndex);

            if (_archive)
            {
                _task.IsArchived = true;
                if (!_oldIsArchived && !ReferenceEquals(_sourceColumn, _targetColumn))
                {
                    _task.ArchivedFromColumnId = _sourceColumn.Id;
                    _task.ArchivedFromIndex = _sourceIndex >= 0 ? _sourceIndex : null;
                }
            }
            else
            {
                _task.IsArchived = false;
                _task.ArchivedFromColumnId = null;
                _task.ArchivedFromIndex = null;
            }

            if (_updateCompletedAt)
            {
                _task.CompletedAt = _newCompletedAt;
            }
        }

        public void Undo()
        {
            MoveTask(_task, _targetColumn, _sourceColumn, _sourceIndex);
            _task.IsArchived = _oldIsArchived;
            _task.ArchivedAt = _oldArchivedAt;
            _task.ArchivedFromColumnId = _oldArchivedFromColumnId;
            _task.ArchivedFromIndex = _oldArchivedFromIndex;
            if (_updateCompletedAt)
            {
                _task.CompletedAt = _oldCompletedAt;
            }
        }

        private static void MoveTask(
            FlowTask task,
            FlowKanbanColumnData sourceColumn,
            FlowKanbanColumnData targetColumn,
            int targetIndex)
        {
            if (ReferenceEquals(sourceColumn, targetColumn))
            {
                var currentIndex = sourceColumn.Tasks.IndexOf(task);
                if (currentIndex < 0)
                    return;

                var insertIndex = Math.Clamp(targetIndex, 0, sourceColumn.Tasks.Count - 1);
                if (currentIndex == insertIndex)
                    return;

                sourceColumn.Tasks.RemoveAt(currentIndex);
                if (currentIndex < insertIndex)
                    insertIndex--;

                sourceColumn.Tasks.Insert(insertIndex, task);
                return;
            }

            sourceColumn.Tasks.Remove(task);
            var clampedTarget = Math.Clamp(targetIndex, 0, targetColumn.Tasks.Count);
            targetColumn.Tasks.Insert(clampedTarget, task);
        }
    }
}
