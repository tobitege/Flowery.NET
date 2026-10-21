using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public sealed class PhotoCanvas : Control, IDisposable
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CanvasPeer(this);

    private sealed class CanvasPeer(PhotoCanvas owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(PhotoCanvas);
    }

    private SKBitmap? _bitmap;
    private SKBitmap? _previousBitmap;
    private Rect _previousRect;
    private EditState _previousState = EditState.Original;
    private readonly Stopwatch _fade = new();
    private readonly DispatcherTimer _fadeTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private EditState _state = EditState.Original;
    private Size _originalSize;
    private Point? _pressed;
    private Point _pan;
    private Point _panStart;
    private Rect _cropStart;
    private int _cropCorner;
    private readonly List<Vector2> _points = [];
    private bool _secondary;
    private double _zoom;
    public double Zoom => _zoom;
    public bool IsCropping { get; set; }
    public bool IsDrawing { get; set; }
    public bool Eyedropper { get; set; }
    public bool Highlighter { get; set; }
    public Color PrimaryColor { get; set; } = Colors.Red;
    public Color SecondaryColor { get; set; } = Colors.White;
    public double PenWidth { get; set; } = 5;
    public double AspectRatio { get; set; }
    public bool Compare { get; set; }
    public double Divider { get; set; } = 0.5;
    public SKBitmap? Bitmap => _bitmap;
    public EditState State { get => _state; set { _state = value; InvalidateVisual(); } }
    public event Action<EditState>? Edited;
    public event Action<Color>? ColorPicked;
    public event Action? ViewChanged;

    public PhotoCanvas()
    {
        _fadeTimer.Tick += (_, _) =>
        {
            if (_fade.ElapsedMilliseconds >= 250)
            {
                _fadeTimer.Stop(); _previousBitmap?.Dispose(); _previousBitmap = null;
            }
            InvalidateVisual();
        };
        ClipToBounds = true; Focusable = true;
        SizeChanged += (_, _) => { InvalidateVisual(); ViewChanged?.Invoke(); };
        PointerWheelChanged += (_, e) => { ZoomStep(e.Delta.Y > 0); e.Handled = true; };
        PointerPressed += Pressed;
        PointerMoved += Moved;
        PointerReleased += Released;
        DoubleTapped += (_, _) => SetZoom(100);
    }

    public void SetPhoto(SKBitmap bitmap, Size originalSize)
    {
        _fadeTimer.Stop(); _previousBitmap?.Dispose();
        _previousBitmap = _bitmap; _previousRect = ImageRect; _previousState = DisplayState;
        _bitmap = bitmap; _bitmap.SetImmutable(); _originalSize = originalSize;
        _state = EditState.Original; Fit();
        if (_previousBitmap != null) { _fade.Restart(); _fadeTimer.Start(); }
    }
    public void Fit() { _zoom = 0; _pan = default; InvalidateVisual(); ViewChanged?.Invoke(); }
    public void SetZoom(double percent) { _zoom = Math.Clamp(percent, 10, 800); InvalidateVisual(); ViewChanged?.Invoke(); }
    public double ActualZoom
    {
        get
        {
            var size = EditRenderer.GetOutputSize(_originalSize, DisplayState);
            return _zoom > 0 ? _zoom : Math.Min((Bounds.Width - 48) / Math.Max(1, size.Width),
                (Bounds.Height - 48) / Math.Max(1, size.Height)) * 100;
        }
    }
    public void ZoomStep(bool up)
    {
        var current = ActualZoom;
        var step = up ? current < 100 ? 10 : 25 : current <= 100 ? 10 : 25;
        SetZoom(up ? (Math.Floor(current / step) + 1) * step : (Math.Ceiling(current / step) - 1) * step);
    }
    private EditState DisplayState => IsCropping ? State with { Crop = EditState.FullCrop } : State;
    private Rect ImageRect
    {
        get
        {
            var size = EditRenderer.GetOutputSize(_originalSize, DisplayState);
            var scale = ActualZoom / 100;
            return new Rect((Bounds.Width - size.Width * scale) / 2 + _pan.X,
                (Bounds.Height - size.Height * scale) / 2 + _pan.Y, size.Width * scale, size.Height * scale);
        }
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(19, 21, 27)), new Rect(Bounds.Size));
        if (_bitmap == null) return;
        var state = DisplayState;
        if (_points.Count > 0)
            state = state with { Strokes = state.Strokes.Add(CurrentStroke()) };
        var rect = ImageRect;
        if (_previousBitmap != null)
            context.Custom(new ImageOperation(new Rect(Bounds.Size), _previousRect, SKImage.FromBitmap(_previousBitmap), _previousState, false, 0));
        var opacity = _previousBitmap == null ? 1 : Math.Clamp(_fade.Elapsed.TotalMilliseconds / 250, 0, 1);
        context.Custom(new ImageOperation(new Rect(Bounds.Size), rect, SKImage.FromBitmap(_bitmap), state, Compare, Divider, opacity));
        var line = new Pen(new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), 1);
        if (PhotosSettings.ShowGrid || IsCropping)
        {
            var gridRect = IsCropping ? CropRect(rect) : rect;
            for (var i = 1; i < 3; i++)
            {
                context.DrawLine(line, new Point(gridRect.X + gridRect.Width * i / 3, gridRect.Y), new Point(gridRect.X + gridRect.Width * i / 3, gridRect.Bottom));
                context.DrawLine(line, new Point(gridRect.X, gridRect.Y + gridRect.Height * i / 3), new Point(gridRect.Right, gridRect.Y + gridRect.Height * i / 3));
            }
            if (IsCropping)
            {
                context.DrawRectangle(null, new Pen(Brushes.White, 2), gridRect);
                foreach (var point in new[] { gridRect.TopLeft, gridRect.TopRight, gridRect.BottomLeft, gridRect.BottomRight })
                    context.FillRectangle(Brushes.White, new Rect(point.X - 5, point.Y - 5, 10, 10));
            }
        }
        if (Compare) context.DrawLine(new Pen(Brushes.White, 2), new Point(Bounds.Width * Divider, 0), new Point(Bounds.Width * Divider, Bounds.Height));
        if (PhotosSettings.ShowRulers) DrawRulers(context, rect);
        if (PhotosSettings.ShowNavigator && ActualZoom > 100)
        {
            var nav = NavigatorRect;
            context.Custom(new ImageOperation(new Rect(Bounds.Size), nav, SKImage.FromBitmap(_bitmap), State, false, 0));
            context.DrawRectangle(null, new Pen(Brushes.White, 1), nav);
            var view = new Rect(nav.X + Math.Clamp(-rect.X / rect.Width, 0, 1) * nav.Width,
                nav.Y + Math.Clamp(-rect.Y / rect.Height, 0, 1) * nav.Height,
                Math.Min(1, Bounds.Width / rect.Width) * nav.Width, Math.Min(1, Bounds.Height / rect.Height) * nav.Height);
            context.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 2), view);
        }
    }
    private Rect NavigatorRect => new(Math.Max(0, Bounds.Width - 175), Math.Max(0, Bounds.Height - 125), 160, 110);
    private Rect CropRect(Rect image) => new(image.X + State.Crop.X * image.Width,
        image.Y + State.Crop.Y * image.Height, State.Crop.Width * image.Width, State.Crop.Height * image.Height);
    private void DrawRulers(DrawingContext context, Rect rect)
    {
        var pen = new Pen(Brushes.LightGray, 1);
        for (var i = 0; i <= 10; i++)
        {
            var x = rect.X + rect.Width * i / 10;
            var y = rect.Y + rect.Height * i / 10;
            context.DrawLine(pen, new Point(x, 0), new Point(x, 8));
            context.DrawLine(pen, new Point(0, y), new Point(8, y));
            var text = new FormattedText(((int)(_originalSize.Width * i / 10)).ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.White);
            context.DrawText(text, new Point(x + 3, 8));
            context.DrawText(new FormattedText(((int)(_originalSize.Height * i / 10)).ToString(), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.White), new Point(8, y + 3));
        }
    }
    private Point Normalized(Point point)
    {
        var rect = ImageRect;
        return new Point(Math.Clamp((point.X - rect.X) / Math.Max(1, rect.Width), 0, 1),
            Math.Clamp((point.Y - rect.Y) / Math.Max(1, rect.Height), 0, 1));
    }
    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (_bitmap == null) return;
        Focus();
        var point = e.GetPosition(this);
        if (PhotosSettings.ShowNavigator && ActualZoom > 100 && NavigatorRect.Contains(point))
        {
            var nav = NavigatorRect; var image = ImageRect;
            _pan = new Point((0.5 - (point.X - nav.X) / nav.Width) * image.Width,
                (0.5 - (point.Y - nav.Y) / nav.Height) * image.Height);
            InvalidateVisual(); return;
        }
        if (Eyedropper)
        {
            using var rendered = EditRenderer.Render(_bitmap, State);
            var p = Normalized(point);
            var color = rendered.GetPixel(Math.Min(rendered.Width - 1, (int)(p.X * rendered.Width)),
                Math.Min(rendered.Height - 1, (int)(p.Y * rendered.Height)));
            ColorPicked?.Invoke(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
            Eyedropper = false; return;
        }
        _pressed = point; _panStart = _pan; _cropStart = State.Crop;
        _secondary = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (IsCropping)
        {
            var r = CropRect(ImageRect);
            var corners = new[] { r.TopLeft, r.TopRight, r.BottomLeft, r.BottomRight };
            _cropCorner = Array.FindIndex(corners, p => Math.Sqrt(Math.Pow(p.X - point.X, 2) + Math.Pow(p.Y - point.Y, 2)) < 24);
        }
        if (IsDrawing) _points.Add(ToStroke(point));
        e.Pointer.Capture(this); e.Handled = true;
    }
    private Vector2 ToStroke(Point point)
    {
        var p = Normalized(point);
        return new Vector2((float)(State.Crop.X + p.X * State.Crop.Width), (float)(State.Crop.Y + p.Y * State.Crop.Height));
    }
    private void Moved(object? sender, PointerEventArgs e)
    {
        if (_pressed is not { } start) return;
        var point = e.GetPosition(this);
        if (Compare && !IsDrawing && !IsCropping) Divider = Math.Clamp(point.X / Bounds.Width, 0.05, 0.95);
        else if (IsDrawing) _points.Add(ToStroke(point));
        else if (IsCropping)
        {
            var p = Normalized(point); var origin = Normalized(start);
            if (_cropCorner < 0)
                State = State with { Crop = new Rect(Math.Clamp(_cropStart.X + p.X - origin.X, 0, 1 - _cropStart.Width),
                    Math.Clamp(_cropStart.Y + p.Y - origin.Y, 0, 1 - _cropStart.Height), _cropStart.Width, _cropStart.Height) };
            else
            {
                var left = _cropCorner % 2 == 0 ? Math.Min(p.X, _cropStart.Right - 0.02) : _cropStart.X;
                var top = _cropCorner < 2 ? Math.Min(p.Y, _cropStart.Bottom - 0.02) : _cropStart.Y;
                var right = _cropCorner % 2 == 1 ? Math.Max(p.X, left + 0.02) : _cropStart.Right;
                var bottom = _cropCorner >= 2 ? Math.Max(p.Y, top + 0.02) : _cropStart.Bottom;
                if (AspectRatio > 0)
                {
                    var size = EditRenderer.GetTransformedSize(_originalSize, State);
                    var height = (right - left) * size.Width / (AspectRatio * size.Height);
                    bottom = Math.Min(1, top + height);
                    right = left + (bottom - top) * AspectRatio * size.Height / size.Width;
                }
                State = State with { Crop = new Rect(left, top, Math.Min(1, right) - left, bottom - top) };
            }
        }
        else _pan = _panStart + (point - start);
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    private MarkupStroke CurrentStroke() => new(_secondary ? SecondaryColor : PrimaryColor,
        (float)(PenWidth / Math.Max(1, Math.Max(_originalSize.Width, _originalSize.Height))), Highlighter, _points.ToImmutableArray());
    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressed == null) return;
        _pressed = null; e.Pointer.Capture(null);
        if (_points.Count > 0) { Edited?.Invoke(State with { Strokes = State.Strokes.Add(CurrentStroke()) }); _points.Clear(); }
        else if (IsCropping && State.Crop != _cropStart) Edited?.Invoke(State);
        InvalidateVisual();
    }
    public void Dispose()
    {
        _fadeTimer.Stop(); _previousBitmap?.Dispose(); _previousBitmap = null;
        _bitmap?.Dispose(); _bitmap = null;
    }

    private sealed class ImageOperation(Rect bounds, Rect target, SKImage image, EditState state, bool compare, double divider, double opacity = 1) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() => image.Dispose();
        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature == null) return;
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            using var layer = new SKPaint { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
            canvas.SaveLayer(layer);
            if (compare)
            {
                canvas.ClipRect(new SKRect(0, 0, (float)(bounds.Width * divider), (float)bounds.Height));
                Draw(canvas, EditState.Original);
                canvas.Restore(); canvas.Save();
                canvas.ClipRect(new SKRect((float)(bounds.Width * divider), 0, (float)bounds.Width, (float)bounds.Height));
            }
            Draw(canvas, state);
            canvas.Restore();
        }
        private void Draw(SKCanvas canvas, EditState value)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect((float)target.X, (float)target.Y, (float)target.Right, (float)target.Bottom));
            canvas.Translate((float)target.X, (float)target.Y);
            var size = EditRenderer.GetOutputSize(new Size(image.Width, image.Height), value);
            canvas.Scale((float)(target.Width / size.Width), (float)(target.Height / size.Height));
            EditRenderer.Draw(canvas, image, value);
            canvas.Restore();
        }
    }
}
