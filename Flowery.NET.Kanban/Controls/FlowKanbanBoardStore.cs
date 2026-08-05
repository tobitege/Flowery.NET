using System;
using System.Collections.Generic;
using System.Text.Json;
using Flowery.Services;
using Flowery.NET.Kanban.Interfaces;

namespace Flowery.NET.Kanban.Controls
{
    public sealed class FlowKanbanBoardStore : Flowery.NET.Kanban.Interfaces.IBoardStore
    {
        private const string BoardStorageKeyPrefix = "kanban.board";
        private const string BoardStorageKeyPrefixWithSeparator = BoardStorageKeyPrefix + ".";
        private readonly IStateStorage _stateStorage;
        private readonly TimeProvider _timeProvider;

        public FlowKanbanBoardStore(IStateStorage stateStorage)
            : this(stateStorage, TimeProvider.System)
        {
        }

        internal FlowKanbanBoardStore(IStateStorage stateStorage, TimeProvider timeProvider)
        {
            _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public IReadOnlyList<FlowBoardMetadata> ListBoards()
        {
            var keys = _stateStorage.GetKeys(BoardStorageKeyPrefixWithSeparator);
            var results = new List<FlowBoardMetadata>();

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (key.EndsWith(".tmp", StringComparison.Ordinal))
                    continue;

                var boardId = TryGetBoardIdFromKey(key);
                if (string.IsNullOrWhiteSpace(boardId))
                    continue;

                var json = LoadStateText(key);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                if (FlowKanbanBoardSanitizer.TryBuildBoardMetadata(json, boardId, out var metadata))
                {
                    results.Add(metadata);
                }
            }

            return results;
        }

        public bool TryLoadBoard(string boardId, out FlowKanbanData? board, out Exception? error)
        {
            try
            {
                board = null;
                if (!FlowKanbanBoardSanitizer.IsValidId(boardId))
                {
                    error = null;
                    return false;
                }

                var json = LoadStateText(GetBoardStorageKey(boardId));
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = null;
                    return false;
                }

                if (!FlowKanbanBoardSanitizer.TryLoadFromJson(json, out board) || board == null)
                {
                    error = null;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(board.Id) || !string.Equals(board.Id, boardId, StringComparison.Ordinal))
                {
                    board.Id = boardId;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                board = null;
                return false;
            }
        }

        public bool TrySaveBoard(FlowKanbanData board, out Exception? error)
        {
            try
            {
                if (board == null)
                    throw new ArgumentNullException(nameof(board));

                if (!FlowKanbanBoardSanitizer.IsValidId(board.Id))
                {
                    board.Id = Guid.NewGuid().ToString();
                }

                board.SchemaVersion = FlowKanbanMigration.CurrentSchemaVersion;
                var previousUpdatedAt = board.UpdatedAt;
                board.UpdatedAt = _timeProvider.GetLocalNow().LocalDateTime;

                try
                {
                    var json = JsonSerializer.Serialize(board, FlowKanbanJsonContext.Default.FlowKanbanData);
                    var storageKey = GetBoardStorageKey(board.Id);
                    var tempKey = $"{storageKey}.tmp";

                    SaveStateText(tempKey, json);
                    _stateStorage.Rename(tempKey, storageKey);
                }
                catch
                {
                    board.UpdatedAt = previousUpdatedAt;
                    throw;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        public bool TryDeleteBoard(string boardId, out Exception? error)
        {
            try
            {
                if (!FlowKanbanBoardSanitizer.IsValidId(boardId))
                {
                    error = null;
                    return false;
                }

                _stateStorage.Delete(GetBoardStorageKey(boardId));
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        public bool TryExportBoard(string boardId, out string? json, out Exception? error)
        {
            try
            {
                json = null;
                if (!FlowKanbanBoardSanitizer.IsValidId(boardId))
                {
                    error = null;
                    return false;
                }

                var content = LoadStateText(GetBoardStorageKey(boardId));
                if (string.IsNullOrWhiteSpace(content) || !FlowKanbanBoardSanitizer.IsSafeJsonContent(content))
                {
                    error = null;
                    return false;
                }

                json = content;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                json = null;
                return false;
            }
        }

        private static string GetBoardStorageKey(string boardId)
        {
            return StateStorageKeyHelper.BuildScopedKey(BoardStorageKeyPrefix, boardId);
        }

        private static string? TryGetBoardIdFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (!key.StartsWith(BoardStorageKeyPrefixWithSeparator, StringComparison.Ordinal))
                return null;

            var boardId = key.Substring(BoardStorageKeyPrefixWithSeparator.Length);
            if (!FlowKanbanBoardSanitizer.IsValidId(boardId))
                return null;

            return boardId;
        }

        private string? LoadStateText(string key)
        {
            var lines = _stateStorage.LoadLines(key);
            if (lines.Count == 0)
                return null;

            return string.Join(Environment.NewLine, lines);
        }

        private void SaveStateText(string key, string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _stateStorage.SaveLines(key, lines);
        }
    }
}
