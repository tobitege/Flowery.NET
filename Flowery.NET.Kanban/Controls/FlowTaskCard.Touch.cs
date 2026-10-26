namespace Flowery.NET.Kanban.Controls
{
    // Touch on a phone or tablet follows the platforms (Android, iOS, the Kanban and calendar apps): a tap opens the card's
    // editor, or toggles its selection while cards are selected; holding the card selects it, and moving the finger then
    // drags it. A press may start a swipe through the board, so a tap is decided on release, and a swipe neither opens,
    // selects nor drags. Avalonia has no platform drag and drop on Android (DragDrop.DoDragDropAsync returns None there),
    // so the card moves itself: a copy follows the finger, the column under it shows where the card lands, and the
    // release drops it there through the same path as a drag.
    public partial class FlowTaskCard
    {
        private const double AutoScrollEdge = 40;
        private const double AutoScrollStep = 12;

        private IPointer? _tapPointer;
        private Point _tapStart;
        private DispatcherTimer? _holdTimer;
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

        private void BeginTouchPress(PointerPressedEventArgs e)
        {
            CancelTouchDrag();
            _tapPointer = e.Pointer;
            _tapStart = e.GetPosition(this);
            _holdTimer ??= new DispatcherTimer();
            _holdTimer.Stop();
            _holdTimer.Interval = Application.Current?.PlatformSettings?.HoldWaitDuration ?? TimeSpan.FromMilliseconds(500);
            _holdTimer.Tick -= OnHoldTimerTick;
            _holdTimer.Tick += OnHoldTimerTick;
            _holdTimer.Start();
        }

        // Held without moving: the card is selected, and the finger may drag it now.
        private void OnHoldTimerTick(object? sender, EventArgs e)
        {
            _holdTimer?.Stop();
            // A swipe ended the press before (OnCardPointerMoved), long before the scroll gesture takes the finger.
            if (_tapPointer is not { } pointer || CardTask is not { } task || string.IsNullOrWhiteSpace(task.Id))
                return;

            _tapPointer = null;
            if (!task.IsSelected)
                SelectTask(task, KeyModifiers.Control);
            _touchDrag = new TouchDrag(pointer, _tapStart, task.Id);
        }

        private void OnCardPointerMoved(object? sender, PointerEventArgs e)
        {
            if (ReferenceEquals(_tapPointer, e.Pointer))
            {
                // Moved before the hold: a swipe, which the board scrolls.
                if (!IsWithin(e.GetPosition(this), _tapStart, TapSize(e.Pointer)))
                    EndTouchPress();
                return;
            }

            if (_touchDrag is not { } drag || !ReferenceEquals(drag.Pointer, e.Pointer))
                return;

            // The held card keeps the finger; the scroll gesture must not take it.
            e.PreventGestureRecognition();
            if (!drag.IsMoving && (IsWithin(e.GetPosition(this), drag.Start, TapSize(e.Pointer)) || !StartTouchDrag(drag)))
                return;

            drag.Point = e.GetPosition(drag.TopLevel);
            UpdateTouchDrag(drag);
            e.Handled = true;
        }

        private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_touchDrag is { } drag && ReferenceEquals(drag.Pointer, e.Pointer))
            {
                // The hold selected the card, so its release is no tap; a moved card lands where the finger left it.
                EndTouchDrag(drag, e);
                e.Handled = true;
                return;
            }

            if (!ReferenceEquals(_tapPointer, e.Pointer))
                return;

            EndTouchPress();
            if (!IsWithin(e.GetPosition(this), _tapStart, TapSize(e.Pointer)) || CardTask is not { } task)
                return;

            e.Handled = true;
            if (IsSelectionActive())
                SelectTask(task, KeyModifiers.Control);
            else
                TryExecuteEdit();
        }

        private void EndTouchPress()
        {
            _tapPointer = null;
            _holdTimer?.Stop();
        }

        // While cards are selected, a tap adds a card to the selection or takes it out.
        private bool IsSelectionActive() =>
            _parentKanban?.Board.Columns.Any(static column => column.Tasks.Any(static task => task.IsSelected))
            ?? CardTask?.IsSelected == true;

        private static Size TapSize(IPointer pointer) =>
            Application.Current?.PlatformSettings?.GetTapSize(pointer.Type) ?? new Size(10, 10);

        private static bool IsWithin(Point point, Point origin, Size tapSize) =>
            Math.Abs(point.X - origin.X) <= tapSize.Width / 2 && Math.Abs(point.Y - origin.Y) <= tapSize.Height / 2;

        private bool StartTouchDrag(TouchDrag drag)
        {
            drag.TopLevel = TopLevel.GetTopLevel(this);
            drag.Layer = OverlayLayer.GetOverlayLayer(this);
            if (drag.TopLevel is null || drag.Layer is null)
                return false;

            drag.Ghost = new Border
            {
                Width = Bounds.Width,
                Height = Bounds.Height,
                Background = new VisualBrush(this) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                Opacity = 0.85,
                IsHitTestVisible = false
            };
            drag.Layer.Children.Add(drag.Ghost);
            // The card holds the pointer from here on, so it also learns when the system cancels the touch: the release
            // never comes then, and the lost capture ends the drag.
            drag.Pointer.Capture(this);

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

        private void EndTouchDrag(TouchDrag drag, PointerReleasedEventArgs e)
        {
            if (!drag.IsMoving)
            {
                CancelTouchDrag();
                return;
            }

            drag.Point = e.GetPosition(drag.TopLevel);
            var column = ColumnAt(drag);
            CancelTouchDrag();
            column?.DropTask(drag.TaskId, visual => PointIn(drag, visual));
        }

        private void OnCardPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_touchDrag is { IsMoving: true } drag && ReferenceEquals(drag.Pointer, e.Pointer))
                CancelTouchDrag();
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
            {
                drag.Layer?.Children.Remove(ghost);
                if (ReferenceEquals(drag.Pointer.Captured, this))
                    drag.Pointer.Capture(null);
            }
        }

        private static FlowKanbanColumn? ColumnAt(TouchDrag drag) =>
            (drag.TopLevel?.InputHitTest(drag.Point) as Visual)?.FindAncestorOfType<FlowKanbanColumn>(includeSelf: true);

        private static Point PointIn(TouchDrag drag, Visual visual) =>
            drag.TopLevel?.TranslatePoint(drag.Point, visual) ?? default;
    }
}
