namespace Flowery.NET.Kanban.Controls
{
    // Avalonia has no platform drag and drop on Android (DragDrop.DoDragDropAsync returns None there), so with touch
    // selection a finger on a selected card moves it inside the board itself: a copy of the card follows the finger, the
    // column under it shows where the card lands, and the release drops it there through the same path as a drag.
    // Swipes over unselected cards keep scrolling the board.
    public partial class FlowTaskCard
    {
        private const double AutoScrollEdge = 40;
        private const double AutoScrollStep = 12;

        private TouchDrag? _touchDrag;

        private sealed class TouchDrag(IPointer pointer, Point start, string taskId)
        {
            public IPointer Pointer { get; } = pointer;
            // Where the finger pressed, in the card.
            public Point Start { get; } = start;
            public string TaskId { get; } = taskId;
            public TopLevel? TopLevel { get; set; }
            public OverlayLayer? Layer { get; set; }
            public Border? Ghost { get; set; }
            public FlowKanbanColumn? Column { get; set; }
            // The finger, in the top level.
            public Point Point { get; set; }
            public DispatcherTimer? ScrollTimer { get; set; }
            public bool IsMoving => Ghost is not null;
        }

        private FlowTask? CardTask => CloseCommandParameter as FlowTask ?? Task ?? _trackedTask;

        // A press on a selected card keeps the scroll gesture away; the drag itself starts once the finger moves.
        private void BeginTouchDrag(PointerPressedEventArgs e)
        {
            CancelTouchDrag();
            if (CardTask is not { IsSelected: true } task || string.IsNullOrWhiteSpace(task.Id))
                return;

            e.PreventGestureRecognition();
            _touchDrag = new TouchDrag(e.Pointer, e.GetPosition(this), task.Id);
        }

        private void OnCardPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_touchDrag is not { } drag || !ReferenceEquals(drag.Pointer, e.Pointer))
                return;

            if (!drag.IsMoving)
            {
                var tapSize = Application.Current?.PlatformSettings?.GetTapSize(e.Pointer.Type) ?? new Size(10, 10);
                if (IsWithin(e.GetPosition(this), drag.Start, tapSize) || !StartTouchDrag(drag))
                    return;
            }

            drag.Point = e.GetPosition(drag.TopLevel);
            UpdateTouchDrag(drag);
            e.Handled = true;
        }

        private bool StartTouchDrag(TouchDrag drag)
        {
            drag.TopLevel = TopLevel.GetTopLevel(this);
            drag.Layer = OverlayLayer.GetOverlayLayer(this);
            if (drag.TopLevel is null || drag.Layer is null)
                return false;

            // The finger moves the card, so it neither edits it nor counts as a tap.
            CancelTitleLongPress();
            _tapPointer = null;
            _lastTapTime = 0;

            drag.Ghost = new Border
            {
                Width = Bounds.Width,
                Height = Bounds.Height,
                Background = new VisualBrush(this) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                Opacity = 0.85,
                IsHitTestVisible = false
            };
            drag.Layer.Children.Add(drag.Ghost);

            drag.ScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            drag.ScrollTimer.Tick += OnTouchDragScrollTick;
            drag.ScrollTimer.Start();
            return true;
        }

        private void UpdateTouchDrag(TouchDrag drag)
        {
            if (drag.Ghost is null || drag.Layer is null || drag.TopLevel is null)
                return;

            var inLayer = drag.TopLevel.TranslatePoint(drag.Point, drag.Layer) ?? drag.Point;
            Canvas.SetLeft(drag.Ghost, inLayer.X - drag.Start.X);
            Canvas.SetTop(drag.Ghost, inLayer.Y - drag.Start.Y);

            var column = ColumnAt(drag);
            if (!ReferenceEquals(column, drag.Column))
            {
                drag.Column?.EndTaskDropPreview();
                drag.Column = column;
            }
            column?.PreviewTaskDrop(visual => PointIn(drag, visual));
        }

        // Near an edge of a scroll viewer under the finger (the board, the column's cards), the view scrolls along.
        private void OnTouchDragScrollTick(object? sender, EventArgs e)
        {
            if (_touchDrag is not { IsMoving: true } drag || drag.TopLevel is null)
                return;

            var scrolled = false;
            var hit = drag.TopLevel.InputHitTest(drag.Point) as Visual;
            foreach (var viewer in hit?.GetSelfAndVisualAncestors().OfType<ScrollViewer>() ?? [])
            {
                var point = PointIn(drag, viewer);
                var dx = Step(point.X, viewer.Bounds.Width) * (viewer.Extent.Width > viewer.Viewport.Width ? 1 : 0);
                var dy = Step(point.Y, viewer.Bounds.Height) * (viewer.Extent.Height > viewer.Viewport.Height ? 1 : 0);
                if (dx == 0 && dy == 0)
                    continue;

                var offset = viewer.Offset;
                viewer.Offset = new Vector(offset.X + dx, offset.Y + dy);
                scrolled |= viewer.Offset != offset;
            }

            if (scrolled)
                UpdateTouchDrag(drag);

            static double Step(double position, double length) =>
                position < AutoScrollEdge ? -AutoScrollStep : position > length - AutoScrollEdge ? AutoScrollStep : 0;
        }

        // Ends a touch drag; returns whether the finger moved a card, so the release is no tap.
        private bool EndTouchDrag(PointerReleasedEventArgs e)
        {
            if (_touchDrag is not { } drag || !ReferenceEquals(drag.Pointer, e.Pointer))
                return false;

            if (!drag.IsMoving)
            {
                _touchDrag = null;
                return false;
            }

            drag.Point = e.GetPosition(drag.TopLevel);
            var column = ColumnAt(drag);
            CancelTouchDrag();
            column?.DropTask(drag.TaskId, visual => PointIn(drag, visual));
            e.Handled = true;
            return true;
        }

        private void CancelTouchDrag()
        {
            if (_touchDrag is not { } drag)
                return;

            _touchDrag = null;
            if (drag.ScrollTimer is { } timer)
            {
                timer.Stop();
                timer.Tick -= OnTouchDragScrollTick;
            }
            drag.Column?.EndTaskDropPreview();
            if (drag.Ghost is { } ghost)
                drag.Layer?.Children.Remove(ghost);
        }

        private static FlowKanbanColumn? ColumnAt(TouchDrag drag) =>
            (drag.TopLevel?.InputHitTest(drag.Point) as Visual)?.FindAncestorOfType<FlowKanbanColumn>(includeSelf: true);

        private static Point PointIn(TouchDrag drag, Visual visual) =>
            drag.TopLevel?.TranslatePoint(drag.Point, visual) ?? default;
    }
}
