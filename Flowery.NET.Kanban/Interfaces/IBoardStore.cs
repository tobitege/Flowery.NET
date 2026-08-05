using System;
using System.Collections.Generic;
using Flowery.NET.Kanban.Controls;

namespace Flowery.NET.Kanban.Interfaces
{
    public interface IBoardStore
    {
        IReadOnlyList<FlowBoardMetadata> ListBoards();
        bool TryLoadBoard(string boardId, out FlowKanbanData? board, out Exception? error);
        bool TrySaveBoard(FlowKanbanData board, out Exception? error);
        bool TryDeleteBoard(string boardId, out Exception? error);
        bool TryExportBoard(string boardId, out string? json, out Exception? error);
    }
}
