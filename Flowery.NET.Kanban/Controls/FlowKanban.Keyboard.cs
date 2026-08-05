using System;
using System.Collections.Generic;
using System.Linq;
using Flowery.Controls;

namespace Flowery.NET.Kanban.Controls
{
    public partial class FlowKanban
    {
        private const int InvalidColumnIndex = -1;
        private const Key OemPlusKey = Key.OemPlus;
        private const Key OemMinusKey = Key.OemMinus;
        private const int TabNavigationThrottleMs = 100;
        private int _keyboardColumnIndex = InvalidColumnIndex;
        private bool _keyboardSupportAttached;
        private int _lastTabNavigationTick = int.MinValue;

        private void AttachKeyboardSupport()
        {
            if (!_keyboardSupportAttached)
            {
                KeyDown += OnKanbanKeyDown;
                _keyboardSupportAttached = true;
            }
        }

        private void DetachKeyboardSupport()
        {
            if (_keyboardSupportAttached)
            {
                KeyDown -= OnKanbanKeyDown;
                _keyboardSupportAttached = false;
            }
        }

        private void OnKanbanKeyDown(object? sender, KeyEventArgs e)
        {
            if (!IsBoardViewActive)
                return;

            var focused = GetFocusedElement();
            if (focused == null || IsTextInputElement(focused))
                return;

            var isControlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var isShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var isAltDown = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            if (isControlDown)
            {
                if (TryHandleControlShortcut(e.Key, isAltDown, isShiftDown))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Tab)
            {
                if (!isControlDown && !isAltDown && ShouldThrottleTabNavigation())
                {
                    e.Handled = true;
                    return;
                }
                if (!isControlDown && !isAltDown && TryHandleTabNavigation(focused, isShiftDown))
                {
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                if (!isControlDown && !isShiftDown && !isAltDown)
                {
                    e.Handled = true;
                    return;
                }
            }

            if (!IsDirectionalKey(e.Key))
                return;

            // Only treat plain arrow keys as navigation. Modified arrows are reserved for shortcuts.
            if (isControlDown || isShiftDown || isAltDown)
                return;

            if (TryHandleDirectionalNavigation(focused, e.Key))
            {
                e.Handled = true;
            }
        }

        private bool ShouldThrottleTabNavigation()
        {
            var now = Environment.TickCount;
            var elapsed = unchecked(now - _lastTabNavigationTick);
            if (elapsed >= 0 && elapsed < TabNavigationThrottleMs)
                return true;

            _lastTabNavigationTick = now;
            return false;
        }

        private bool TryHandleControlShortcut(Key key, bool isAltDown, bool isShiftDown)
        {
            // Admin-only column operations.
            if (!isAltDown && !isShiftDown && _isCurrentUserGlobalAdmin)
            {
                switch (key)
                {
                    case Key.B:
                        ExecuteAddColumn();
                        return true;
                    case Key.D:
                        if (Board.Columns.Count > 0)
                        {
                            ExecuteRemoveColumn(Board.Columns[^1]);
                            return true;
                        }
                        break;
                    case Key.T:
                        var column = GetActiveColumnData();
                        if (column != null)
                        {
                            ExecuteEditColumn(column);
                            return true;
                        }
                        break;
                }
            }

            // Everyone shortcuts.
            if (!isAltDown && !isShiftDown)
            {
                switch (key)
                {
                    case Key.N:
                        var column = GetActiveColumnData();
                        if (column != null)
                        {
                            BeginInlineAddCard(column);
                            return true;
                        }
                        break;
                    case Key.F:
                        if (_boardSearchInput is { } searchInput)
                        {
                            searchInput.Focus();
                            searchInput.SelectAll();
                        }
                        return true;
                    case OemPlusKey:
                    case Key.Add:
                        ExecuteZoomIn();
                        return true;
                    case OemMinusKey:
                    case Key.Subtract:
                        ExecuteZoomOut();
                        return true;
                }
            }

            // Ctrl + Alt + Left/Right (switch columns).
            if (isAltDown && !isShiftDown)
            {
                switch (key)
                {
                    case Key.Left:
                        MoveActiveColumn(-1);
                        return true;
                    case Key.Right:
                        MoveActiveColumn(1);
                        return true;
                }
            }

            // Ctrl + Shift + Arrow (move card).
            if (!isAltDown && isShiftDown)
            {
                switch (key)
                {
                    case Key.Left:
                    case Key.Right:
                    case Key.Up:
                    case Key.Down:
                        return TryMoveFocusedCard(key);
                }
            }

            return false;
        }

        private bool TryMoveFocusedCard(Key direction)
        {
            var focused = GetFocusedElement();
            if (focused == null)
                return false;

            var card = TryResolveFocusedCard(focused);
            if (card?.Task == null)
                return false;

            var column = FindAncestor<FlowKanbanColumn>(card);
            if (column?.ColumnData == null)
                return false;

            switch (direction)
            {
                case Key.Left:
                    MoveCardHorizontal(card.Task, column, -1);
                    return true;
                case Key.Right:
                    MoveCardHorizontal(card.Task, column, 1);
                    return true;
                case Key.Up:
                    MoveCardVertical(card.Task, column, -1);
                    return true;
                case Key.Down:
                    MoveCardVertical(card.Task, column, 1);
                    return true;
            }

            return false;
        }

        private static bool IsDirectionalKey(Key key)
        {
            return key == Key.Left
                   || key == Key.Right
                   || key == Key.Up
                   || key == Key.Down;
        }

        private AvaloniaObject? GetFocusedElement()
        {
            return TopLevel?.FocusManager?.GetFocusedElement() as AvaloniaObject;
        }

        private bool TryHandleDirectionalNavigation(AvaloniaObject focused, Key key)
        {
            var card = TryResolveFocusedCard(focused);
            if (card != null)
                return HandleCardNavigation(card, key);

            var addCard = FindAncestor<FlowKanbanAddCard>(focused);
            if (addCard != null)
                return HandleAddCardNavigation(addCard, key);

            var column = FindAncestor<FlowKanbanColumn>(focused);
            if (column != null)
                return HandleColumnNavigation(column, key);

            return false;
        }

        private FlowTaskCard? TryResolveFocusedCard(AvaloniaObject focused)
        {
            var card = FindAncestor<FlowTaskCard>(focused);
            if (card != null)
                return card;

            var container = FindAncestor<ListBoxItem>(focused);
            if (container == null)
                return null;

            if (container.Content is FlowKanbanTaskView view && view.Task != null)
            {
                var column = FindAncestor<FlowKanbanColumn>(container);
                if (column != null)
                {
                    var realizedCard = column.TryGetTaskCard(view.Task);
                    if (realizedCard != null)
                        return realizedCard;
                }
            }

            return FindDescendant<FlowTaskCard>(container);
        }

        private bool TryHandleTabNavigation(AvaloniaObject focused, bool isShiftDown)
        {
            var card = TryResolveFocusedCard(focused);
            if (card != null)
                return HandleCardTabNavigation(card, isShiftDown);

            var addCard = FindAncestor<FlowKanbanAddCard>(focused);
            if (addCard != null)
                return HandleAddCardTabNavigation(addCard, isShiftDown);

            var column = FindAncestor<FlowKanbanColumn>(focused);
            if (column != null)
                return HandleColumnTabNavigation(column, isShiftDown);

            return false;
        }

        private bool HandleCardTabNavigation(FlowTaskCard card, bool isShiftDown)
        {
            var column = FindAncestor<FlowKanbanColumn>(card);
            if (column == null || column.ColumnData == null)
                return false;

            var tasks = GetVisibleTaskViews(column);
            if (tasks.Count == 0 || card.Task == null)
                return false;

            var index = tasks.FindIndex(view => ReferenceEquals(view.Task, card.Task));
            if (index < 0)
                return false;

            if (isShiftDown)
            {
                if (index > 0 && TryFocusTaskAtIndex(column, index - 1))
                    return true;
                return FocusColumnHeader(column);
            }

            if (index < tasks.Count - 1 && TryFocusTaskAtIndex(column, index + 1))
                return true;

            FocusAddCard(column);
            return true;
        }

        private bool HandleAddCardTabNavigation(FlowKanbanAddCard addCard, bool isShiftDown)
        {
            var column = FindAncestor<FlowKanbanColumn>(addCard);
            if (column == null || column.ColumnData == null)
                return false;

            if (!isShiftDown)
                return false;

            var tasks = GetVisibleTaskViews(column);
            var lastIndex = tasks.Count - 1;
            if (lastIndex >= 0 && TryFocusTaskAtIndex(column, lastIndex))
                return true;

            return FocusColumnHeader(column);
        }

        private bool HandleColumnTabNavigation(FlowKanbanColumn column, bool isShiftDown)
        {
            if (isShiftDown)
                return false;

            return FocusFirstCardOrAdd(column);
        }

        private bool HandleCardNavigation(FlowTaskCard card, Key key)
        {
            var column = FindAncestor<FlowKanbanColumn>(card);
            if (column == null || column.ColumnData == null)
                return false;

            var tasks = GetVisibleTaskViews(column);
            if (tasks.Count == 0 || card.Task == null)
                return false;

            var index = tasks.FindIndex(view => ReferenceEquals(view.Task, card.Task));
            if (index < 0)
                return false;

            switch (key)
            {
                case Key.Up:
                    if (index > 0 && TryFocusTaskAtIndex(column, index - 1))
                        return true;
                    return FocusColumnHeader(column);
                case Key.Down:
                    if (index < tasks.Count - 1 && TryFocusTaskAtIndex(column, index + 1))
                        return true;
                    return FocusAddCard(column);
                case Key.Left:
                    return MoveHorizontalFromIndex(column, index, -1);
                case Key.Right:
                    return MoveHorizontalFromIndex(column, index, 1);
                default:
                    return false;
            }
        }

        private bool HandleAddCardNavigation(FlowKanbanAddCard addCard, Key key)
        {
            var column = FindAncestor<FlowKanbanColumn>(addCard);
            if (column == null || column.ColumnData == null)
                return false;

            var tasks = GetVisibleTaskViews(column);
            var lastIndex = tasks.Count - 1;

            switch (key)
            {
                case Key.Up:
                    if (lastIndex >= 0 && TryFocusTaskAtIndex(column, lastIndex))
                        return true;
                    return FocusColumnHeader(column);
                case Key.Left:
                    return MoveHorizontalFromIndex(column, tasks.Count, -1);
                case Key.Right:
                    return MoveHorizontalFromIndex(column, tasks.Count, 1);
                default:
                    return false;
            }
        }

        private bool HandleColumnNavigation(FlowKanbanColumn column, Key key)
        {
            if (!column.ShowColumnHeader || column.ColumnData == null)
                return false;

            switch (key)
            {
                case Key.Left:
                    return FocusAdjacentColumnHeader(column, -1);
                case Key.Right:
                    return FocusAdjacentColumnHeader(column, 1);
                case Key.Down:
                    return FocusFirstCardOrAdd(column);
                default:
                    return false;
            }
        }

        private bool FocusAdjacentColumnHeader(FlowKanbanColumn column, int delta)
        {
            var target = GetAdjacentColumn(column.ColumnData, delta);
            if (target == null)
                return false;

            var targetColumn = FindColumnControl(target, laneId: null, requireHeader: true);
            return MoveFocusToElement(targetColumn);
        }

        private bool FocusFirstCardOrAdd(FlowKanbanColumn column)
        {
            if (!column.ShowTasks)
            {
                if (IsSwimlaneLayoutEnabled && LaneRows.Count > 0 && column.ColumnData != null)
                {
                    var laneId = NormalizeLaneId(LaneRows[0].Lane.Id);
                    var laneColumn = FindColumnControl(column.ColumnData, laneId, requireHeader: false);
                    if (laneColumn != null && laneColumn.ShowTasks)
                        return FocusFirstCardOrAdd(laneColumn);
                }

                return FocusAddCard(column);
            }

            var tasks = GetVisibleTaskViews(column);
            if (tasks.Count > 0)
                return TryFocusTaskAtIndex(column, 0);

            return FocusAddCard(column);
        }

        private bool MoveHorizontalFromIndex(FlowKanbanColumn column, int index, int delta)
        {
            var target = GetAdjacentColumn(column.ColumnData, delta);
            if (target == null)
                return false;

            var laneId = NormalizeLaneId(column.LaneFilterId);
            var targetColumn = FindColumnControl(target, laneId, requireHeader: false);
            if (targetColumn == null)
                return false;

            var tasks = GetVisibleTaskViews(targetColumn);
            if (tasks.Count > 0)
            {
                var targetIndex = Math.Clamp(index, 0, tasks.Count - 1);
                if (TryFocusTaskAtIndex(targetColumn, targetIndex))
                    return true;
            }

            if (FocusAddCard(targetColumn))
                return true;

            return MoveFocusToElement(targetColumn);
        }

        private bool TryFocusTaskAtIndex(FlowKanbanColumn column, int index)
        {
            var tasks = GetVisibleTaskViews(column);
            if (index < 0 || index >= tasks.Count)
                return false;

            var task = tasks[index].Task;
            if (task == null)
                return false;

            return FocusTaskOrColumn(column, task);
        }

        private bool FocusTaskOrColumn(FlowKanbanColumn column, FlowTask task)
        {
            var card = FindTaskCard(column, task);
            if (card != null)
            {
                return MoveFocusToElement(card);
            }

            if (column.ScrollTaskIntoView(task))
            {
                QueueFocusAction(() =>
                {
                    var realizedCard = FindTaskCard(column, task);
                    MoveFocusToElement(realizedCard ?? (AvaloniaObject)column);
                });
                return true;
            }

            return MoveFocusToElement(column);
        }

        private bool FocusColumnHeader(FlowKanbanColumn column)
        {
            if (column.ShowColumnHeader)
                return MoveFocusToElement(column);

            var fallback = FindColumnControl(column.ColumnData, laneId: null, requireHeader: true);
            return MoveFocusToElement(fallback);
        }

        private bool FocusColumnHeader(FlowKanbanColumnData? column)
        {
            var target = FindColumnControl(column, laneId: null, requireHeader: true);
            return MoveFocusToElement(target);
        }

        private bool FocusAddCard(FlowKanbanColumn column)
        {
            if (!column.ShowAddCard)
                return false;

            var laneId = NormalizeLaneId(column.LaneFilterId);
            var addCard = FindAddCardControl(column.ColumnData, laneId);
            return addCard?.FocusForKeyboard() == true;
        }

        private FlowKanbanColumnData? GetAdjacentColumn(FlowKanbanColumnData? column, int delta)
        {
            if (column == null)
                return null;

            var index = Board.Columns.IndexOf(column);
            if (index < 0)
                return null;

            var targetIndex = index + delta;
            if (targetIndex < 0 || targetIndex >= Board.Columns.Count)
                return null;

            return Board.Columns[targetIndex];
        }

        private static List<FlowKanbanTaskView> GetVisibleTaskViews(FlowKanbanColumn column)
        {
            if (!column.ShowTasks)
                return new List<FlowKanbanTaskView>();

            return column.TaskViews
                .Where(view => view.Task != null && view.Task.IsSearchMatch)
                .ToList();
        }

        private FlowKanbanColumn? FindColumnControl(
            FlowKanbanColumnData? column,
            string? laneId,
            bool requireHeader)
        {
            if (column == null)
                return null;

            var key = (column.Id, NormalizeLaneId(laneId));
            if (!_columnByKey.TryGetValue(key, out var columnControl))
                return null;

            if (!columnControl.IsLoaded || !columnControl.IsVisible)
                return null;

            if (requireHeader && !columnControl.ShowColumnHeader)
                return null;

            return columnControl;
        }

        private static FlowTaskCard? FindTaskCard(FlowKanbanColumn column, FlowTask task)
        {
            return column.TryGetTaskCard(task);
        }

        private FlowKanbanAddCard? FindAddCardControl(FlowKanbanColumnData? column, string? laneId)
        {
            if (column == null)
                return null;

            var columnControl = FindColumnControl(column, laneId, requireHeader: false);
            return columnControl?.GetVisibleAddCardControl();
        }

        private void BeginInlineAddCard(FlowKanbanColumnData column)
        {
            var laneId = NormalizeLaneId(GetFocusedLaneId());
            QueueFocusAction(() =>
            {
                var addCard = FindAddCardControl(column, laneId);
                if (addCard != null)
                {
                    addCard.BeginInlineAdd();
                    return;
                }

                ExecuteAddCard(column);
            });
        }

        private void MoveActiveColumn(int delta)
        {
            if (Board.Columns.Count == 0)
                return;

            var index = GetActiveColumnIndex();
            if (index == InvalidColumnIndex)
                return;

            var targetIndex = Math.Clamp(index + delta, 0, Board.Columns.Count - 1);
            _keyboardColumnIndex = targetIndex;
            var column = Board.Columns[targetIndex];
            QueueFocusAction(() => FocusColumnHeader(column));
        }

        private int GetActiveColumnIndex()
        {
            var focused = GetFocusedElement();
            var column = GetColumnDataFromElement(focused);
            if (column != null)
            {
                UpdateKeyboardColumnIndex(column);
            }

            if (_keyboardColumnIndex == InvalidColumnIndex || _keyboardColumnIndex >= Board.Columns.Count)
            {
                _keyboardColumnIndex = Board.Columns.Count - 1;
            }

            return _keyboardColumnIndex;
        }

        private FlowKanbanColumnData? GetActiveColumnData()
        {
            var focused = GetFocusedElement();
            var column = GetColumnDataFromElement(focused);
            if (column != null)
            {
                UpdateKeyboardColumnIndex(column);
                return column;
            }

            var index = GetActiveColumnIndex();
            return index >= 0 && index < Board.Columns.Count
                ? Board.Columns[index]
                : null;
        }

        private void UpdateKeyboardColumnIndex(FlowKanbanColumnData column)
        {
            var index = Board.Columns.IndexOf(column);
            if (index >= 0)
            {
                _keyboardColumnIndex = index;
            }
        }

        private FlowKanbanColumnData? GetColumnDataFromElement(AvaloniaObject? element)
        {
            var columnControl = FindAncestor<FlowKanbanColumn>(element);
            return columnControl?.ColumnData;
        }

        private string? GetFocusedLaneId()
        {
            var focused = GetFocusedElement();
            var column = FindAncestor<FlowKanbanColumn>(focused);
            if (column == null)
                return null;

            return NormalizeLaneId(column.LaneFilterId);
        }

        private void QueueFocusAction(Action focusAction)
        {
            FlowKanbanDispatcher.Post(focusAction);
        }

        private void MoveCardHorizontal(FlowTask task, FlowKanbanColumn column, int delta)
        {
            var targetColumn = GetAdjacentColumn(column.ColumnData, delta);
            if (targetColumn == null)
                return;

            var tasks = GetVisibleTaskViews(column);
            var index = tasks.FindIndex(view => ReferenceEquals(view.Task, task));
            if (index < 0)
                return;

            var targetLaneId = column.LaneFilterId;
            var targetInsertIndex = GetLaneInsertIndex(targetColumn, targetLaneId, index);

            var manager = new FlowKanbanManager(this, autoAttach: false);
            var result = manager.TryMoveTaskWithWipEnforcement(task, targetColumn, targetInsertIndex, targetLaneId, enforceHard: false);
            if (result == MoveResult.AllowedWithWipWarning)
            {
                ShowWipWarning(targetColumn, targetLaneId);
            }

            QueueFocusAction(() =>
            {
                var targetColumnControl = FindColumnControl(targetColumn, NormalizeLaneId(targetLaneId), requireHeader: false);
                if (targetColumnControl == null)
                    return;

                FocusTaskOrColumn(targetColumnControl, task);
            });
        }

        private void MoveCardVertical(FlowTask task, FlowKanbanColumn column, int delta)
        {
            var tasks = GetVisibleTaskViews(column);
            var currentIndex = tasks.FindIndex(view => ReferenceEquals(view.Task, task));
            if (currentIndex < 0)
                return;

            var targetIndex = currentIndex + delta;
            if (targetIndex < 0 || targetIndex >= tasks.Count)
                return;

            var targetTask = tasks[targetIndex].Task;
            if (targetTask == null || column.ColumnData == null)
                return;

            var baseIndex = column.ColumnData.Tasks.IndexOf(targetTask);
            if (baseIndex < 0)
                return;

            var insertIndex = delta > 0 ? baseIndex + 1 : baseIndex;
            var manager = new FlowKanbanManager(this, autoAttach: false);
            var targetLaneId = column.LaneFilterId;
            var result = manager.TryMoveTaskWithWipEnforcement(task, column.ColumnData, insertIndex, targetLaneId, enforceHard: false);
            if (result == MoveResult.AllowedWithWipWarning)
            {
                ShowWipWarning(column.ColumnData, targetLaneId);
            }

            QueueFocusAction(() =>
            {
                FocusTaskOrColumn(column, task);
            });
        }

        private static int GetLaneInsertIndex(FlowKanbanColumnData targetColumn, string? laneId, int desiredLaneIndex)
        {
            var laneTasks = new List<FlowTask>();
            foreach (var task in targetColumn.Tasks)
            {
                if (LaneIdsMatch(task.LaneId, laneId))
                {
                    laneTasks.Add(task);
                }
            }

            if (laneTasks.Count == 0)
                return targetColumn.Tasks.Count;

            if (desiredLaneIndex >= laneTasks.Count)
            {
                var lastTask = laneTasks[^1];
                var lastIndex = targetColumn.Tasks.IndexOf(lastTask);
                return lastIndex + 1;
            }

            var targetTask = laneTasks[Math.Clamp(desiredLaneIndex, 0, laneTasks.Count - 1)];
            var targetIndex = targetColumn.Tasks.IndexOf(targetTask);
            return targetIndex < 0 ? targetColumn.Tasks.Count : targetIndex;
        }

        private static bool LaneIdsMatch(string? left, string? right)
        {
            return string.Equals(NormalizeLaneId(left), NormalizeLaneId(right), StringComparison.Ordinal);
        }

        private static bool IsTextInputElement(AvaloniaObject element)
        {
            return FlowKanbanVisualTree.FindAncestor<Control>(
                element,
                predicate: static control => control is TextBox or DaisyPasswordBox or ComboBox or DatePicker or NumericUpDown) is not null;
        }

        private static bool MoveFocusToElement(AvaloniaObject? element)
        {
            if (element is not Control uiElement)
                return false;

            uiElement.Focus(NavigationMethod.Directional);
            uiElement.BringIntoView();
            return true;
        }

        private static T? FindAncestor<T>(AvaloniaObject? element) where T : AvaloniaObject
        {
            return FlowKanbanVisualTree.FindAncestor<T>(element);
        }

        private static T? FindDescendant<T>(AvaloniaObject? parent) where T : AvaloniaObject
        {
            return FlowKanbanVisualTree.FindDescendant<T>(parent);
        }

        private void ClampKeyboardColumnIndex()
        {
            if (Board.Columns.Count == 0)
            {
                _keyboardColumnIndex = InvalidColumnIndex;
                return;
            }

            if (_keyboardColumnIndex == InvalidColumnIndex)
            {
                _keyboardColumnIndex = Board.Columns.Count - 1;
                return;
            }

            _keyboardColumnIndex = Math.Clamp(_keyboardColumnIndex, 0, Board.Columns.Count - 1);
        }
    }
}
