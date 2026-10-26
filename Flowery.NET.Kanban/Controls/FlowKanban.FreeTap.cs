namespace Flowery.NET.Kanban.Controls
{
    // As in DaisyScheduler, a tap or click on a free place of the board (between or inside the columns, not on a card or
    // a control) clears the card selection. It is decided on release, so a swipe through the board clears nothing.
    public partial class FlowKanban
    {
        private IPointer? _freeTapPointer;
        private Point _freeTapStart;

        private void AttachFreeTapHandlers()
        {
            if (_boardContentHost is not { } host)
                return;

            DetachFreeTapHandlers();
            // Handled too: the scroll gesture and the columns mark these events handled.
            host.AddHandler(PointerPressedEvent, OnBoardPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
            host.AddHandler(PointerReleasedEvent, OnBoardPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void DetachFreeTapHandlers()
        {
            if (_boardContentHost is not { } host)
                return;

            host.RemoveHandler(PointerPressedEvent, OnBoardPointerPressed);
            host.RemoveHandler(PointerReleasedEvent, OnBoardPointerReleased);
            _freeTapPointer = null;
        }

        private void OnBoardPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var isFree = IsFreePlace(e.Source) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
            _freeTapPointer = isFree ? e.Pointer : null;
            _freeTapStart = e.GetPosition(this);
        }

        private void OnBoardPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!ReferenceEquals(_freeTapPointer, e.Pointer))
                return;

            _freeTapPointer = null;
            var tapSize = Application.Current?.PlatformSettings?.GetTapSize(e.Pointer.Type) ?? new Size(10, 10);
            var moved = e.GetPosition(this) - _freeTapStart;
            // Without HasSelection: it misses a selection the board was loaded with.
            if (Math.Abs(moved.X) <= tapSize.Width / 2 && Math.Abs(moved.Y) <= tapSize.Height / 2)
                DeselectAllTasks();
        }

        // Free: neither a card nor a control that takes the press itself (a ComboBox, such as the column choice of the
        // compact layout, has no button in its template).
        private bool IsFreePlace(object? source) =>
            source is Visual visual && !visual.GetSelfAndVisualAncestors()
                .TakeWhile(ancestor => !ReferenceEquals(ancestor, this))
                .Any(static ancestor => ancestor is FlowTaskCard or Button or TextBox or ComboBox or ScrollBar or Thumb);
    }
}
