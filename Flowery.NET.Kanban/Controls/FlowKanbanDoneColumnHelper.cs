using System;

namespace Flowery.NET.Kanban.Controls
{
    internal static class FlowKanbanDoneColumnHelper
    {
        public static void UpdateCompletedAtOnAdd(FlowKanbanData? board, FlowKanbanColumnData? column, FlowTask? task)
        {
            if (board is null || column is null || task is null)
                return;

            var doneId = board.DoneColumnId;
            if (string.IsNullOrWhiteSpace(doneId))
                return;

            if (string.Equals(column.Id, doneId, StringComparison.Ordinal))
            {
                task.CompletedAt = DateTime.Now;
            }
        }
    }
}
