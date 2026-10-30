namespace Flowery.NET.Kanban.Controls
{
    // Two fingers zoom the board through its sizes, as the zoom buttons and Ctrl+Plus or Minus do: spreading them zooms in,
    // pinching zooms out, one size per PinchStep of their distance. The pinch recognizer of the board (PART_BoardContentHost)
    // takes both fingers from the cards and the scroll viewers once the second one lands, so a pinch neither selects, drags
    // nor scrolls (see FlowTaskCard.Touch).
    public partial class FlowKanban
    {
        // The factor of the fingers' distance for one size.
        private const double PinchStep = 1.25;

        // The size the fingers started at; null between pinches.
        private int? _pinchStartSize;

        private void InitializePinch()
        {
            AddHandler(PinchEvent, OnBoardPinch);
            AddHandler(PinchEndedEvent, OnBoardPinchEnded, handledEventsToo: true);
        }

        private void OnBoardPinch(object? sender, PinchEventArgs e)
        {
            // Two fingers that land on one spot have no distance to scale from (an infinite scale would overflow the steps).
            if (!double.IsFinite(e.Scale))
                return;

            _pinchStartSize ??= Array.IndexOf(SizeOrder, BoardSize);
            // Truncated: a size changes only once the fingers moved a whole step.
            var steps = (int)(Math.Log(e.Scale) / Math.Log(PinchStep));
            BoardSize = SizeOrder[Math.Clamp(_pinchStartSize.Value + steps, 0, SizeOrder.Length - 1)];
            e.Handled = true;
        }

        private void OnBoardPinchEnded(object? sender, PinchEndedEventArgs e) => _pinchStartSize = null;
    }
}
