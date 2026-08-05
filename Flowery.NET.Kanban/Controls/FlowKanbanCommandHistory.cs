using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Manages undo/redo history for Kanban operations.
    /// </summary>
    public class FlowKanbanCommandHistory : INotifyPropertyChanged
    {
        /// <summary>
        /// Maximum number of commands to keep in history.
        /// </summary>
        public const int MaxHistorySize = 50;

        private readonly Stack<IKanbanCommand> _undoStack = new();
        private readonly Stack<IKanbanCommand> _redoStack = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets whether there are commands to undo.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets whether there are commands to redo.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Gets the number of commands in the undo stack.
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Gets the number of commands in the redo stack.
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Gets the description of the next undo command, or null if none.
        /// </summary>
        public string? NextUndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;

        /// <summary>
        /// Gets the description of the next redo command, or null if none.
        /// </summary>
        public string? NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Executes a command and adds it to the undo stack.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        public void Execute(IKanbanCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Execute();

            // Trim stack if over limit
            if (_undoStack.Count >= MaxHistorySize)
            {
                // Convert to array, remove oldest, rebuild stack
                var commands = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = commands.Length - 2; i >= 0; i--)
                {
                    _undoStack.Push(commands[i]);
                }
            }

            _undoStack.Push(command);
            _redoStack.Clear();

            NotifyPropertyChanged(nameof(CanUndo));
            NotifyPropertyChanged(nameof(CanRedo));
            NotifyPropertyChanged(nameof(UndoCount));
            NotifyPropertyChanged(nameof(RedoCount));
            NotifyPropertyChanged(nameof(NextUndoDescription));
            NotifyPropertyChanged(nameof(NextRedoDescription));
        }

        /// <summary>
        /// Undoes the last command.
        /// </summary>
        /// <returns>True if a command was undone.</returns>
        public bool Undo()
        {
            if (_undoStack.Count == 0)
                return false;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            NotifyPropertyChanged(nameof(CanUndo));
            NotifyPropertyChanged(nameof(CanRedo));
            NotifyPropertyChanged(nameof(UndoCount));
            NotifyPropertyChanged(nameof(RedoCount));
            NotifyPropertyChanged(nameof(NextUndoDescription));
            NotifyPropertyChanged(nameof(NextRedoDescription));

            return true;
        }

        /// <summary>
        /// Redoes the last undone command.
        /// </summary>
        /// <returns>True if a command was redone.</returns>
        public bool Redo()
        {
            if (_redoStack.Count == 0)
                return false;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);

            NotifyPropertyChanged(nameof(CanUndo));
            NotifyPropertyChanged(nameof(CanRedo));
            NotifyPropertyChanged(nameof(UndoCount));
            NotifyPropertyChanged(nameof(RedoCount));
            NotifyPropertyChanged(nameof(NextUndoDescription));
            NotifyPropertyChanged(nameof(NextRedoDescription));

            return true;
        }

        /// <summary>
        /// Clears all undo/redo history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();

            NotifyPropertyChanged(nameof(CanUndo));
            NotifyPropertyChanged(nameof(CanRedo));
            NotifyPropertyChanged(nameof(UndoCount));
            NotifyPropertyChanged(nameof(RedoCount));
            NotifyPropertyChanged(nameof(NextUndoDescription));
            NotifyPropertyChanged(nameof(NextRedoDescription));
        }

        private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
