using System;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// View-only wrapper for lane-aware task rendering.
    /// </summary>
    public sealed class FlowKanbanTaskView
    {
        public FlowKanbanTaskView(FlowTask task, FlowKanbanLane? lane, bool showLaneHeader)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Lane = lane;
            ShowLaneHeader = showLaneHeader;
        }

        public FlowTask Task { get; }

        public FlowKanbanLane? Lane { get; }

        public bool ShowLaneHeader { get; }
    }
}
