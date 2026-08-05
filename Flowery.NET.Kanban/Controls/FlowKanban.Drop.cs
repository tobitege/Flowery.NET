namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Partial class containing drag and drop support for FlowKanban.
    /// </summary>
    public partial class FlowKanban
    {
        #region Column Drag and Drop Support

        private EventHandler<DragEventArgs>? _boardDragOverHandler;
        private EventHandler<DragEventArgs>? _boardDropHandler;
        private EventHandler<DragEventArgs>? _boardDragLeaveHandler;

        private void AttachColumnDragHandlers()
        {
            DetachColumnDragHandlers();

            var standardColumns = FindChild<ItemsControl>(this, ic => ic.Name == "PART_ColumnsItemsControl");
            var standardDropLayer = FindChild<Canvas>(this, c => c.Name == "PART_ColumnDropLayer");
            AttachColumnDragHandlers(standardColumns, standardDropLayer);

            var swimlaneColumns = FindChild<ItemsControl>(this, ic => ic.Name == "PART_SwimlaneColumnsItemsControl");
            var swimlaneDropLayer = FindChild<Canvas>(this, c => c.Name == "PART_SwimlaneColumnDropLayer");
            AttachColumnDragHandlers(swimlaneColumns, swimlaneDropLayer);
        }

        private void AttachColumnDragHandlers(ItemsControl? itemsControl, Canvas? dropLayer)
        {
            if (itemsControl == null || _columnItemsControls.Contains(itemsControl))
                return;

            DragDrop.SetAllowDrop(itemsControl, true);
            itemsControl.AddHandler(DragDrop.DragOverEvent, OnColumnDragOver, RoutingStrategies.Bubble, true);
            itemsControl.AddHandler(DragDrop.DropEvent, OnColumnDrop, RoutingStrategies.Bubble, true);
            itemsControl.AddHandler(DragDrop.DragLeaveEvent, OnColumnDragLeave, RoutingStrategies.Bubble, true);
            _columnItemsControls.Add(itemsControl);
            if (dropLayer != null)
            {
                dropLayer.IsHitTestVisible = false;
                _columnDropLayers[itemsControl] = dropLayer;
            }
        }

        private void DetachColumnDragHandlers()
        {
            foreach (var itemsControl in _columnItemsControls)
            {
                itemsControl.RemoveHandler(DragDrop.DragOverEvent, OnColumnDragOver);
                itemsControl.RemoveHandler(DragDrop.DropEvent, OnColumnDrop);
                itemsControl.RemoveHandler(DragDrop.DragLeaveEvent, OnColumnDragLeave);
            }

            _columnItemsControls.Clear();
            _columnDropLayers.Clear();
        }

        private void AttachBoardDragHandlers()
        {
            if (_boardContentHost == null)
                return;

            _boardDragOverHandler ??= OnBoardDragOver;
            _boardDropHandler ??= OnBoardDrop;
            _boardDragLeaveHandler ??= OnBoardDragLeave;

            DragDrop.SetAllowDrop(_boardContentHost, true);
            if (_boardDragOverHandler != null)
                _boardContentHost.RemoveHandler(DragDrop.DragOverEvent, _boardDragOverHandler);
            if (_boardDropHandler != null)
                _boardContentHost.RemoveHandler(DragDrop.DropEvent, _boardDropHandler);
            if (_boardDragLeaveHandler != null)
                _boardContentHost.RemoveHandler(DragDrop.DragLeaveEvent, _boardDragLeaveHandler);

            if (_boardDragOverHandler != null)
                _boardContentHost.AddHandler(DragDrop.DragOverEvent, _boardDragOverHandler, RoutingStrategies.Bubble, true);
            if (_boardDropHandler != null)
                _boardContentHost.AddHandler(DragDrop.DropEvent, _boardDropHandler, RoutingStrategies.Bubble, true);
            if (_boardDragLeaveHandler != null)
                _boardContentHost.AddHandler(DragDrop.DragLeaveEvent, _boardDragLeaveHandler, RoutingStrategies.Bubble, true);
        }

        private void DetachBoardDragHandlers()
        {
            if (_boardContentHost == null)
                return;

            if (_boardDragOverHandler != null)
                _boardContentHost.RemoveHandler(DragDrop.DragOverEvent, _boardDragOverHandler);
            if (_boardDropHandler != null)
                _boardContentHost.RemoveHandler(DragDrop.DropEvent, _boardDropHandler);
            if (_boardDragLeaveHandler != null)
                _boardContentHost.RemoveHandler(DragDrop.DragLeaveEvent, _boardDragLeaveHandler);
        }

        private static bool HasColumnDragData(IDataTransfer dataTransfer)
        {
            return !string.IsNullOrWhiteSpace(GetDraggedColumnId(dataTransfer));
        }

        private void OnColumnDragOver(object? sender, DragEventArgs e)
        {
            if (!HasColumnDragData(e.DataTransfer))
                return;

            if (sender is not ItemsControl itemsControl)
                return;

            var panel = itemsControl.ItemsPanelRoot as StackPanel;
            if (panel == null)
                return;

            var position = e.GetPosition(panel);
            var draggedColumnId = GetDraggedColumnId(e.DataTransfer);
            if (!TryGetColumnInsertIndex(panel, position.X, draggedColumnId, out var insertIndex))
            {
                e.DragEffects = DragDropEffects.None;
                HideColumnDropIndicator();
                return;
            }

            e.DragEffects = DragDropEffects.Move;

            if (insertIndex != _currentColumnDropIndex)
            {
                HideColumnDropIndicator();
                ShowColumnDropIndicator(itemsControl, panel, insertIndex, draggedColumnId);
            }
        }

        private void OnColumnDragLeave(object? sender, DragEventArgs e)
        {
            if (HasColumnDragData(e.DataTransfer))
            {
                HideColumnDropIndicator();
            }
        }

        private void OnColumnDrop(object? sender, DragEventArgs e)
        {
            if (!HasColumnDragData(e.DataTransfer))
                return;

            var columnId = GetDraggedColumnId(e.DataTransfer);

            if (string.IsNullOrWhiteSpace(columnId))
            {
                HideColumnDropIndicator();
                return;
            }

            var column = FindColumnById(columnId);
            if (column == null)
            {
                HideColumnDropIndicator();
                return;
            }

            if (sender is not ItemsControl itemsControl)
            {
                HideColumnDropIndicator();
                return;
            }

            var panel = itemsControl.ItemsPanelRoot as StackPanel;
            if (panel == null)
            {
                HideColumnDropIndicator();
                return;
            }

            var draggedColumnId = GetDraggedColumnId(e.DataTransfer);
            var insertIndex = _currentColumnDropIndex;
            if (insertIndex < 0)
            {
                var position = e.GetPosition(panel);
                if (!TryGetColumnInsertIndex(panel, position.X, draggedColumnId, out insertIndex))
                {
                    HideColumnDropIndicator();
                    return;
                }
            }

            HideColumnDropIndicator();

            var currentIndex = Board.Columns.IndexOf(column);
            if (currentIndex < 0)
                return;

            insertIndex = Math.Clamp(insertIndex, 0, Board.Columns.Count);

            if (insertIndex == currentIndex)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.MoveColumn(column, insertIndex);
        }

        private void OnBoardDragOver(object? sender, DragEventArgs e)
        {
            if (!HasColumnDragData(e.DataTransfer))
                return;

            if (!TryGetActiveColumnsContext(out var itemsControl, out var panel, out var dropLayer))
                return;

            if (panel == null)
                return;

            e.Handled = true;
            e.DragEffects = DragDropEffects.Move;

            var position = e.GetPosition(panel);
            var draggedColumnId = GetDraggedColumnId(e.DataTransfer);
            if (!TryGetColumnInsertIndex(panel, position.X, draggedColumnId, out var insertIndex))
            {
                HideColumnDropIndicator();
                return;
            }

            if (dropLayer == null)
                return;

            if (insertIndex != _currentColumnDropIndex)
            {
                HideColumnDropIndicator();
                ShowColumnDropIndicator(itemsControl, panel, insertIndex, draggedColumnId);
            }
        }

        private void OnBoardDragLeave(object? sender, DragEventArgs e)
        {
            if (HasColumnDragData(e.DataTransfer))
            {
                e.Handled = true;
                HideColumnDropIndicator();
            }
        }

        private void OnBoardDrop(object? sender, DragEventArgs e)
        {
            if (!HasColumnDragData(e.DataTransfer))
                return;

            if (!TryGetActiveColumnsContext(out var itemsControl, out var panel, out _))
                return;

            if (panel == null)
                return;

            e.Handled = true;

            var columnId = GetDraggedColumnId(e.DataTransfer);

            if (string.IsNullOrWhiteSpace(columnId))
            {
                HideColumnDropIndicator();
                return;
            }

            var column = FindColumnById(columnId);
            if (column == null)
            {
                HideColumnDropIndicator();
                return;
            }

            var draggedColumnId = GetDraggedColumnId(e.DataTransfer);
            var insertIndex = _currentColumnDropIndex;
            if (insertIndex < 0)
            {
                var position = e.GetPosition(panel);
                if (!TryGetColumnInsertIndex(panel, position.X, draggedColumnId, out insertIndex))
                {
                    HideColumnDropIndicator();
                    return;
                }
            }

            HideColumnDropIndicator();

            var currentIndex = Board.Columns.IndexOf(column);
            if (currentIndex < 0)
                return;

            insertIndex = Math.Clamp(insertIndex, 0, Board.Columns.Count);

            if (insertIndex == currentIndex)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            manager.MoveColumn(column, insertIndex);
        }

        private bool TryGetActiveColumnsContext(
            out ItemsControl itemsControl,
            out StackPanel? panel,
            out Canvas? dropLayer)
        {
            var targetItemsControl = IsSwimlaneLayoutVisible && _swimlaneColumnsHost != null
                ? _swimlaneColumnsHost
                : _standardColumnsHost;

            if (targetItemsControl == null)
            {
                itemsControl = null!;
                panel = null;
                dropLayer = null;
                return false;
            }

            itemsControl = targetItemsControl;
            panel = itemsControl.ItemsPanelRoot as StackPanel;
            if (panel == null)
            {
                dropLayer = null;
                return false;
            }

            dropLayer = GetDropLayerForItemsControl(itemsControl);
            return true;
        }

        private Canvas? GetDropLayerForItemsControl(ItemsControl? itemsControl)
        {
            if (itemsControl == null)
                return null;

            if (_columnDropLayers.TryGetValue(itemsControl, out var dropLayer))
                return dropLayer;

            var targetName = string.Equals(itemsControl.Name, "PART_SwimlaneColumnsItemsControl", StringComparison.Ordinal)
                ? "PART_SwimlaneColumnDropLayer"
                : "PART_ColumnDropLayer";

            dropLayer = FindChild<Canvas>(this, c => c.Name == targetName);
            if (dropLayer != null)
            {
                dropLayer.IsHitTestVisible = false;
                _columnDropLayers[itemsControl] = dropLayer;
            }

            return dropLayer;
        }

        private int CalculateColumnInsertIndex(StackPanel panel, double dropX, string? excludeColumnId)
        {
            var children = GetColumnElements(panel, excludeColumnId);
            if (children.Count == 0)
                return 0;

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var left = FlowKanbanVisualTree.TransformPoint(child, panel, new Point(0, 0)).X;
                var midpoint = left + (child.Bounds.Width / 2);
                if (dropX < midpoint)
                    return i;
            }

            return children.Count;
        }

        private bool TryGetColumnInsertIndex(
            StackPanel panel,
            double dropX,
            string? draggedColumnId,
            out int insertIndex)
        {
            insertIndex = -1;

            var layouts = GetColumnLayouts(panel);
            if (layouts.Count == 0)
                return false;

            if (string.IsNullOrWhiteSpace(draggedColumnId))
            {
                insertIndex = CalculateColumnInsertIndex(panel, dropX, null);
                return true;
            }

            var draggedIndex = layouts.FindIndex(info =>
                string.Equals(info.ColumnId, draggedColumnId, StringComparison.Ordinal));

            if (draggedIndex < 0)
            {
                insertIndex = CalculateColumnInsertIndex(panel, dropX, draggedColumnId);
                return true;
            }

            var dragged = layouts[draggedIndex];
            if (dropX >= dragged.Left && dropX <= dragged.Right)
                return false;

            if (draggedIndex > 0)
            {
                var prev = layouts[draggedIndex - 1];
                if (dropX > prev.Right && dropX < dragged.Left)
                    return false;
            }

            if (draggedIndex < layouts.Count - 1)
            {
                var next = layouts[draggedIndex + 1];
                if (dropX > dragged.Right && dropX < next.Left)
                    return false;
            }

            ColumnLayoutInfo? hovered = null;
            foreach (var info in layouts)
            {
                if (dropX >= info.Left && dropX <= info.Right)
                {
                    hovered = info;
                    break;
                }
            }

            if (hovered != null)
            {
                if (hovered.Index == draggedIndex - 1)
                {
                    if (dropX >= hovered.Midpoint)
                        return false;

                    insertIndex = hovered.Index;
                    return true;
                }

                if (hovered.Index == draggedIndex + 1)
                {
                    if (dropX <= hovered.Midpoint)
                        return false;

                    insertIndex = hovered.Index;
                    return true;
                }
            }

            insertIndex = CalculateColumnInsertIndex(panel, dropX, draggedColumnId);
            return true;
        }

        private void EnsureColumnDropIndicator()
        {
            if (_columnDropIndicator != null)
                return;

            _columnDropIndicator = new Rectangle
            {
                Width = 4,
                RadiusX = 2,
                RadiusY = 2,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(2, 0, 2, 0)
            };
            _columnDropIndicator.Fill = DaisyResourceLookup.GetBrush("DaisyPrimaryBrush");
        }

        private void ShowColumnDropIndicator(
            ItemsControl itemsControl,
            StackPanel panel,
            int index,
            string? draggedColumnId)
        {
            EnsureColumnDropIndicator();
            if (_columnDropIndicator == null)
                return;

            if (!_columnDropLayers.TryGetValue(itemsControl, out var dropLayer))
                return;

            if (_columnDropIndicator.Parent is Panel oldParent)
            {
                oldParent.Children.Remove(_columnDropIndicator);
            }

            var visualIndex = Math.Clamp(index, 0, panel.Children.Count);
            var panelOrigin = FlowKanbanVisualTree.TransformPoint(panel, dropLayer, new Point(0, 0));
            var offset = CalculateColumnInsertOffset(panel, visualIndex, draggedColumnId);
            _columnDropIndicator.Height = Math.Max(1, panel.Bounds.Height - 8);
            Canvas.SetLeft(_columnDropIndicator, panelOrigin.X + offset);
            Canvas.SetTop(_columnDropIndicator, panelOrigin.Y + 4);
            if (!dropLayer.Children.Contains(_columnDropIndicator))
                dropLayer.Children.Add(_columnDropIndicator);
            _currentColumnDropIndex = index;
        }

        private static double CalculateColumnInsertOffset(StackPanel panel, int index, string? excludeColumnId)
        {
            var children = GetColumnElements(panel, excludeColumnId);
            if (children.Count == 0)
                return 0;

            if (index <= 0)
                return FlowKanbanVisualTree.TransformPoint(children[0], panel, new Point(0, 0)).X;

            if (index >= children.Count)
            {
                var last = children[^1];
                var lastLeft = FlowKanbanVisualTree.TransformPoint(last, panel, new Point(0, 0)).X;
                return lastLeft + last.Bounds.Width;
            }

            return FlowKanbanVisualTree.TransformPoint(children[index], panel, new Point(0, 0)).X;
        }

        private sealed class ColumnLayoutInfo
        {
            public required string ColumnId { get; init; }
            public required int Index { get; init; }
            public required double Left { get; init; }
            public required double Right { get; init; }
            public required double Midpoint { get; init; }
        }

        private static List<ColumnLayoutInfo> GetColumnLayouts(StackPanel panel)
        {
            var layouts = new List<ColumnLayoutInfo>();
            var index = 0;

            foreach (var child in panel.Children)
            {
                if (child is not Control fe)
                    continue;

                if (!TryGetColumnDataId(fe, out var columnId) || string.IsNullOrWhiteSpace(columnId))
                    continue;

                var left = FlowKanbanVisualTree.TransformPoint(fe, panel, new Point(0, 0)).X;
                var right = left + fe.Bounds.Width;
                layouts.Add(new ColumnLayoutInfo
                {
                    ColumnId = columnId,
                    Index = index,
                    Left = left,
                    Right = right,
                    Midpoint = left + (fe.Bounds.Width / 2)
                });

                index++;
            }

            return layouts;
        }

        private static List<Control> GetColumnElements(StackPanel panel, string? excludeColumnId)
        {
            var children = new List<Control>();
            foreach (var child in panel.Children)
            {
                if (child is Control fe)
                {
                    if (excludeColumnId != null
                        && TryGetColumnDataId(fe, out var columnId)
                        && string.Equals(columnId, excludeColumnId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    children.Add(fe);
                }
            }

            return children;
        }

        private static bool TryGetColumnDataId(Control element, out string? columnId)
        {
            // 1. Check if the element itself is a FlowKanbanColumn
            if (element is FlowKanbanColumn columnControl && columnControl.ColumnData != null)
            {
                columnId = columnControl.ColumnData.Id;
                return true;
            }

            // 2. Check if it's a ContentPresenter wrapping a FlowKanbanColumn
            if (element is ContentPresenter cp && cp.Content is FlowKanbanColumn wrappedColumn && wrappedColumn.ColumnData != null)
            {
                columnId = wrappedColumn.ColumnData.Id;
                return true;
            }

            // 3. Check DataContext (works for direct bindings or if inherited)
            if (element.DataContext is FlowKanbanColumnData dataContext)
            {
                columnId = dataContext.Id;
                return true;
            }

            // 4. Check if it's a ContentPresenter wrapping the data itself
            if (element is ContentPresenter presenter && presenter.Content is FlowKanbanColumnData content)
            {
                columnId = content.Id;
                return true;
            }

            columnId = null;
            return false;
        }

        private static string? GetDraggedColumnId(IDataTransfer dataTransfer)
        {
            return dataTransfer.TryGetValue(ColumnDragFormat)
                ?? dataTransfer.TryGetText();
        }

        private void HideColumnDropIndicator()
        {
            if (_columnDropIndicator?.Parent is Panel parent)
            {
                parent.Children.Remove(_columnDropIndicator);
            }

            _currentColumnDropIndex = -1;
        }

        private FlowKanbanColumnData? FindColumnById(string columnId)
        {
            if (string.IsNullOrWhiteSpace(columnId))
                return null;

            foreach (var column in Board.Columns)
            {
                if (string.Equals(column.Id, columnId, StringComparison.Ordinal))
                    return column;
            }

            return null;
        }

        #endregion

        #region Drag and Drop Support

        // Store the currently dragged task and its source column
        private FlowTask? _draggedTask;
        private FlowKanbanColumnData? _dragSourceColumn;

        /// <summary>
        /// Called when a task card starts being dragged.
        /// </summary>
        public void OnTaskDragStarting(FlowTask task, FlowKanbanColumnData sourceColumn)
        {
            _draggedTask = task;
            _dragSourceColumn = sourceColumn;
        }

        /// <summary>
        /// Called when a task is dropped onto a column.
        /// Moves the task from its source column to the target column.
        /// </summary>
        public void OnTaskDropped(FlowKanbanColumnData targetColumn, int insertIndex = -1)
        {
            if (_draggedTask == null || _dragSourceColumn == null)
                return;

            var manager = new FlowKanbanManager(this, autoAttach: false);
            int? targetIndex = insertIndex >= 0 ? insertIndex : null;
            var result = manager.TryMoveTaskWithWipEnforcement(_draggedTask, targetColumn, targetIndex, enforceHard: false);
            if (result == MoveResult.AllowedWithWipWarning)
            {
                ShowWipWarning(targetColumn, _draggedTask.LaneId);
            }

            // Clear drag state and notify
            _draggedTask = null;
            _dragSourceColumn = null;
            DragEnded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when drag is cancelled.
        /// </summary>
        public void OnTaskDragCancelled()
        {
            _draggedTask = null;
            _dragSourceColumn = null;
            DragEnded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the currently dragged task, if any.
        /// </summary>
        public FlowTask? DraggedTask => _draggedTask;

        #endregion
    }
}
