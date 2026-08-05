using System.Collections.Generic;

namespace Flowery.NET.Kanban.Controls
{
    internal static class FlowKanbanWorkItemNumberHelper
    {
        public static void EnsureTaskNumber(FlowKanbanData? board, FlowTask? task)
        {
            if (board is null || task is null)
                return;

            if (task.WorkItemNumber > 0)
                return;

            var next = GetNextWorkItemNumber(board);
            if (next <= 0)
                return;

            task.WorkItemNumber = next;
            if (next < int.MaxValue)
            {
                board.NextWorkItemNumber = next + 1;
            }
        }

        public static void EnsureBoardNumbers(FlowKanbanData? board)
        {
            if (board is null)
                return;

            var usedNumbers = new HashSet<int>();
            var tasksNeedingNumber = new List<FlowTask>();
            var max = 0;

            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (task.WorkItemNumber > 0)
                    {
                        if (usedNumbers.Add(task.WorkItemNumber))
                        {
                            if (task.WorkItemNumber > max)
                            {
                                max = task.WorkItemNumber;
                            }
                        }
                        else
                        {
                            tasksNeedingNumber.Add(task);
                        }
                    }
                    else
                    {
                        tasksNeedingNumber.Add(task);
                    }
                }
            }

            var next = board.NextWorkItemNumber;
            if (next <= 0)
            {
                next = 1;
            }

            if (next <= max)
            {
                next = max + 1;
            }

            foreach (var task in tasksNeedingNumber)
            {
                task.WorkItemNumber = next;
                usedNumbers.Add(next);
                if (next < int.MaxValue)
                {
                    next++;
                }
            }

            if (next <= 0)
            {
                next = 1;
            }

            board.NextWorkItemNumber = next;
        }

        private static int GetNextWorkItemNumber(FlowKanbanData board)
        {
            var next = board.NextWorkItemNumber;
            if (next <= 0)
            {
                next = 1;
            }

            var max = GetMaxWorkItemNumber(board);
            if (next <= max)
            {
                next = max + 1;
            }

            return next;
        }

        private static int GetMaxWorkItemNumber(FlowKanbanData board)
        {
            var max = 0;
            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (task.WorkItemNumber > max)
                    {
                        max = task.WorkItemNumber;
                    }
                }
            }

            return max;
        }
    }
}
