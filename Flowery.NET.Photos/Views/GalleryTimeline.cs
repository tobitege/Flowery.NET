using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Flowery.NET.Photos.Localization;

namespace Flowery.NET.Photos.Views;

public sealed class GalleryTimeline : Control
{
    private IReadOnlyList<GalleryRow> _rows = [];
    private double _offset;
    private double _viewport;
    private double? _pointerY;
    private bool _dragging;
    public event Action<double>? ScrollRequested;
    private double Total => _rows.Count == 0 ? 1 : Math.Max(1, _rows[^1].Top + _rows[^1].Height);
    private double Maximum => Math.Max(0, Total - _viewport);

    public GalleryTimeline()
    {
        Focusable = true;
        PointerPressed += (_, e) =>
        {
            Focus(); _dragging = true; e.Pointer.Capture(this);
            MovePointer(e.GetPosition(this).Y); e.Handled = true;
        };
        PointerMoved += (_, e) => MovePointer(e.GetPosition(this).Y);
        PointerReleased += (_, e) => { _dragging = false; e.Pointer.Capture(null); };
        PointerCaptureLost += (_, _) => _dragging = false;
        PointerExited += (_, _) => { if (!_dragging) { _pointerY = null; InvalidateVisual(); } };
        KeyDown += (_, e) =>
        {
            var offset = e.Key switch
            {
                Key.Home => 0, Key.End => Maximum,
                Key.Up => _offset - 40, Key.Down => _offset + 40,
                Key.PageUp => _offset - _viewport, Key.PageDown => _offset + _viewport,
                _ => double.NaN
            };
            if (double.IsNaN(offset)) return;
            SetOffset(offset); e.Handled = true;
        };
    }

    public void Update(IReadOnlyList<GalleryRow> rows, double offset, double viewport)
    {
        _rows = rows; _offset = offset; _viewport = viewport; InvalidateVisual();
    }

    private void SetOffset(double offset)
    {
        _offset = Math.Clamp(offset, 0, Maximum);
        ScrollRequested?.Invoke(_offset); InvalidateVisual();
    }

    private void MovePointer(double y)
    {
        _pointerY = y;
        if (_dragging) SetOffset(y / Math.Max(1, Bounds.Height) * Total);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_rows.Count == 0) return;
        var content = this.FindResource("DaisyBaseContentBrush") as IBrush ?? Brushes.Gray;
        var accent = this.FindResource("DaisyPrimaryBrush") as IBrush ?? Brushes.DodgerBlue;
        var lastLabel = double.NegativeInfinity;
        var lastDot = double.NegativeInfinity;
        int? year = null, month = null;
        foreach (var row in _rows.OfType<GalleryPhotoRow>())
        {
            var y = row.Top / Total * Bounds.Height;
            if (year != row.Date.Year)
            {
                year = row.Date.Year; month = row.Date.Month;
                var label = Text(year.Value.ToString(CultureInfo.InvariantCulture), content);
                var labelY = Math.Clamp(y, 0, Math.Max(0, Bounds.Height - label.Height));
                if (labelY - lastLabel < 6) continue;
                context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, labelY));
                lastLabel = labelY + label.Height;
                lastDot = y;
            }
            else if (month != row.Date.Month)
            {
                month = row.Date.Month;
                if (y - lastDot < 8 || y - lastLabel < 4) continue;
                context.DrawEllipse(content, null, new Point(Bounds.Width / 2, y), 2, 2);
                lastDot = y;
            }
        }
        var thumbY = Maximum > 0 ? Math.Clamp(_offset / Maximum, 0, 1) * Math.Max(0, Bounds.Height - 4) : 0;
        context.DrawRectangle(accent, null, new Rect((Bounds.Width - 26) / 2, thumbY, 26, 4), 2, 2);
        if (_pointerY is not { } pointer) return;
        var index = GalleryLayout.FindRowAt(_rows, Math.Clamp(pointer / Math.Max(1, Bounds.Height), 0, 1) * Total);
        var date = _rows[index].Date;
        var text = Text(date.ToString("Y", PhotosLocalization.CurrentCulture),
            this.FindResource("DaisyPrimaryContentBrush") as IBrush ?? Brushes.White);
        var bubbleHeight = text.Height + 10;
        var bubble = new Rect(-text.Width - 24, Math.Clamp(pointer - bubbleHeight / 2, 0, Math.Max(0, Bounds.Height - bubbleHeight)), text.Width + 20, bubbleHeight);
        context.DrawRectangle(accent, null, bubble, 12, 12);
        context.DrawText(text, new Point(bubble.X + 10, bubble.Y + 5));
    }

    private FormattedText Text(string value, IBrush brush) => new(value, PhotosLocalization.CurrentCulture,
        FlowDirection.LeftToRight, new Typeface(TextElement.GetFontFamily(this)),
        FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, FlowerySizeManager.CurrentSize), brush);

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelinePeer(this);
    private sealed class TimelinePeer(GalleryTimeline owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ScrollBar;
        public bool IsReadOnly => false;
        public double Minimum => 0;
        public double Maximum => owner.Maximum;
        public double Value => owner._offset;
        public double SmallChange => 40;
        public double LargeChange => owner._viewport;
        public void SetValue(double value) { EnsureEnabled(); owner.SetOffset(value); }
    }
}
