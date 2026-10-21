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
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public sealed partial class PhotoCanvas : Control, IDisposable
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CanvasPeer(this);

    private sealed class CanvasPeer(PhotoCanvas owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(PhotoCanvas);
    }

    private SKBitmap? _bitmap;
    private SKBitmap? _preview;
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
    private CropHandle _cropHandle;
    private bool _draggingDivider;
    private bool _draggingNavigator;
    private Point? _lastPointer;
    private double _wheelDelta;
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
    public bool IsSlideshow { get; set; }
    public double Divider { get; set; } = 0.5;
    public SKBitmap? Bitmap => _bitmap;
    public EditState State { get => _state; set { _state = value; ClampPan(); InvalidateVisual(); } }
    public event Action<EditState>? Edited;
    public event Action<Color, bool>? ColorPicked;
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
        SizeChanged += (_, _) => { ClampPan(); InvalidateVisual(); ViewChanged?.Invoke(); };
        PointerWheelChanged += (_, e) =>
        {
            if (e.Handled || IsCropping || IsSlideshow) return;
            _wheelDelta += e.Delta.Y;
            while (Math.Abs(_wheelDelta) >= 1)
            {
                var direction = Math.Sign(_wheelDelta);
                _wheelDelta -= direction;
                ZoomStep(direction > 0, e.GetPosition(this));
            }
            e.Handled = true;
        };
        PointerPressed += Pressed;
        PointerMoved += Moved;
        PointerReleased += Released;
        PointerCaptureLost += (_, _) => EndInteraction();
        PointerExited += (_, _) => { _lastPointer = null; InvalidateVisual(); };
        DoubleTapped += (_, e) =>
        {
            if (IsCropping || IsDrawing || IsSlideshow) return;
            if (_zoom == 0) SetZoom(100, e.GetPosition(this)); else Fit();
        };
    }

    public void SetPhoto(SKBitmap bitmap, Size originalSize)
    {
        _points.Clear(); _pressed = null; _cropHandle = CropHandle.None;
        _draggingDivider = _draggingNavigator = Eyedropper = false;
        _wheelDelta = 0;
        _fadeTimer.Stop(); _previousBitmap?.Dispose();
        _previousBitmap = _preview ?? _bitmap; _previousRect = ImageRect; _previousState = DisplayState;
        if (_bitmap != _previousBitmap) _bitmap?.Dispose();
        _bitmap = bitmap; _bitmap.SetImmutable(); _originalSize = originalSize;
        var scale = Math.Min(1d, 2560d / Math.Max(bitmap.Width, bitmap.Height));
        _preview = scale < 1 ? bitmap.Resize(new SKImageInfo(Math.Max(1, (int)(bitmap.Width * scale)),
            Math.Max(1, (int)(bitmap.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear)) : null;
        _preview?.SetImmutable();
        _state = EditState.Original; Fit();
        if (_previousBitmap != null) { _fade.Restart(); _fadeTimer.Start(); }
    }
    public void Fit() { _zoom = 0; _pan = default; InvalidateVisual(); ViewChanged?.Invoke(); }
    private double RasterScale => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
    private double RulerInset => PhotosSettings.ShowRulers && !IsSlideshow ? 22 : 0;
    public void SetZoom(double percent, Point? anchor = null)
    {
        if (_bitmap == null || IsCropping) return;
        var before = ImageRect;
        var point = anchor ?? new Point((Bounds.Width + RulerInset) / 2, (Bounds.Height + RulerInset) / 2);
        _zoom = Math.Clamp(percent, 2 * RasterScale, 1600);
        var after = ImageRect;
        if (before.Width > 0 && before.Height > 0)
            _pan += new Avalonia.Vector(point.X - after.X - (point.X - before.X) * after.Width / before.Width,
                point.Y - after.Y - (point.Y - before.Y) * after.Height / before.Height);
        ClampPan(); InvalidateVisual(); ViewChanged?.Invoke();
    }
    public double ActualZoom
    {
        get
        {
            var size = EditRenderer.GetOutputSize(_originalSize, DisplayState);
            var margin = IsCropping ? 40 : IsSlideshow ? 0 : 16;
            return _zoom > 0 && !IsCropping ? _zoom : Math.Max(.001,
                Math.Min((Bounds.Width - RulerInset - 2 * margin) / Math.Max(1, size.Width),
                    (Bounds.Height - RulerInset - 2 * margin) / Math.Max(1, size.Height))) * RasterScale * 100;
        }
    }
    public void ZoomStep(bool up, Point? anchor = null)
    {
        var current = ActualZoom;
        static double Grid(double value) => value < 10 ? 1 : value < 100 ? 10 : 25;
        var step = Grid(up ? current : current - .000001);
        var next = up ? (Math.Floor(current / step + .000001) + 1) * step
            : (Math.Ceiling(current / step - .000001) - 1) * step;
        if (up ? current >= 100 : current > 100)
        {
            var scaled = up ? current * 1.2 : current / 1.2;
            var snapped = Math.Round(scaled / Grid(scaled), MidpointRounding.AwayFromZero) * Grid(scaled);
            if (up ? snapped > current : snapped < current) next = snapped;
        }
        SetZoom(next, anchor);
    }
    private EditState DisplayState => IsCropping ? State with { Crop = EditState.FullCrop } : State;
    private Rect ImageRect
    {
        get
        {
            var size = EditRenderer.GetOutputSize(_originalSize, DisplayState);
            var scale = ActualZoom / 100 / RasterScale;
            return new Rect((Bounds.Width + RulerInset - size.Width * scale) / 2 + (IsCropping ? 0 : _pan.X),
                (Bounds.Height + RulerInset - size.Height * scale) / 2 + (IsCropping ? 0 : _pan.Y), size.Width * scale, size.Height * scale);
        }
    }
    private void ClampPan()
    {
        var rect = ImageRect;
        var width = Bounds.Width - RulerInset;
        var height = Bounds.Height - RulerInset;
        var maxX = Math.Max(0, (rect.Width - width) / 2 + 40);
        var maxY = Math.Max(0, (rect.Height - height) / 2 + 40);
        _pan = new Point(rect.Width <= width ? 0 : Math.Clamp(_pan.X, -maxX, maxX),
            rect.Height <= height ? 0 : Math.Clamp(_pan.Y, -maxY, maxY));
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(IsSlideshow ? Brushes.Black : new SolidColorBrush(Color.FromRgb(19, 21, 27)), new Rect(Bounds.Size));
        if (_bitmap == null) return;
        var state = DisplayState;
        if (_points.Count > 0)
            state = state with { Strokes = state.Strokes.Add(CurrentStroke()) };
        var rect = ImageRect;
        if (_previousBitmap != null)
            context.Custom(new ImageOperation(new Rect(Bounds.Size), _previousRect, SKImage.FromBitmap(_previousBitmap), _previousState, false, 0));
        var opacity = _previousBitmap == null ? 1 : Math.Clamp(_fade.Elapsed.TotalMilliseconds / 250, 0, 1);
        var source = _preview != null && (_zoom == 0 || ActualZoom / 100 * _bitmap.Width / _preview.Width <= 1.05)
            ? _preview : _bitmap;
        context.Custom(new ImageOperation(new Rect(Bounds.Size), rect, SKImage.FromBitmap(source), state, Compare && !IsCropping, Divider, opacity));
        var line = new Pen(new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), 1);
        if (IsCropping)
        {
            var gridRect = CropRect(rect);
            ShadeOutside(context, rect, gridRect);
            for (var i = 1; i < 3; i++)
            {
                context.DrawLine(line, new Point(gridRect.X + gridRect.Width * i / 3, gridRect.Y), new Point(gridRect.X + gridRect.Width * i / 3, gridRect.Bottom));
                context.DrawLine(line, new Point(gridRect.X, gridRect.Y + gridRect.Height * i / 3), new Point(gridRect.Right, gridRect.Y + gridRect.Height * i / 3));
            }
            context.DrawRectangle(null, new Pen(Brushes.White, 2), gridRect);
            var handle = new Pen(Brushes.White, 4);
            foreach (var (point, dx, dy) in new[] { (gridRect.TopLeft, 1, 1), (gridRect.TopRight, -1, 1), (gridRect.BottomLeft, 1, -1), (gridRect.BottomRight, -1, -1) })
            {
                context.DrawLine(handle, point, point + new Avalonia.Vector(dx * 20, 0));
                context.DrawLine(handle, point, point + new Avalonia.Vector(0, dy * 20));
            }
            foreach (var y in new[] { gridRect.Top, gridRect.Bottom })
                context.DrawLine(handle, new Point(gridRect.Center.X - 10, y), new Point(gridRect.Center.X + 10, y));
            foreach (var x in new[] { gridRect.Left, gridRect.Right })
                context.DrawLine(handle, new Point(x, gridRect.Center.Y - 10), new Point(x, gridRect.Center.Y + 10));
        }
        else if (PhotosSettings.ShowGrid && !IsSlideshow) DrawGridlines(context, rect);
        if (Compare && !IsCropping)
        {
            var x = rect.X + rect.Width * Divider;
            var top = Math.Max(0, rect.Y);
            var bottom = Math.Min(Bounds.Height, rect.Bottom);
            context.DrawLine(new Pen(Brushes.White, 2), new Point(x, top), new Point(x, bottom));
            context.DrawEllipse(Brushes.White, null, new Point(x, (top + bottom) / 2), 12, 12);
            var before = Label(PhotosLocalization.GetString("Photos_Before"));
            context.DrawText(before, new Point(x - before.Width - 12, top + 12));
            context.DrawText(Label(PhotosLocalization.GetString("Photos_After")), new Point(x + 12, top + 12));
        }
        if (NavigatorVisible)
        {
            var nav = NavigatorRect;
            context.Custom(new ImageOperation(new Rect(Bounds.Size), nav, SKImage.FromBitmap(_preview ?? _bitmap), State, false, 0));
            context.DrawRectangle(null, new Pen(Brushes.White, 1), nav);
            var left = Math.Clamp((RulerInset - rect.X) / rect.Width, 0, 1);
            var top = Math.Clamp((RulerInset - rect.Y) / rect.Height, 0, 1);
            var right = Math.Clamp((Bounds.Width - rect.X) / rect.Width, 0, 1);
            var bottom = Math.Clamp((Bounds.Height - rect.Y) / rect.Height, 0, 1);
            var view = new Rect(nav.X + left * nav.Width, nav.Y + top * nav.Height,
                Math.Max(0, right - left) * nav.Width, Math.Max(0, bottom - top) * nav.Height);
            ShadeOutside(context, nav, view);
            context.DrawRectangle(null, new Pen(ThemeBrush("PhotosAccentBrush", Brushes.DodgerBlue), 2), view);
        }
        if (RulerInset > 0) DrawRulers(context, rect);
    }
    private bool NavigatorVisible => PhotosSettings.ShowNavigator && !IsCropping && !IsSlideshow;
    private Rect NavigatorRect
    {
        get
        {
            var size = EditRenderer.GetOutputSize(_originalSize, State);
            var scale = 170 / Math.Max(size.Width, size.Height);
            var width = size.Width * scale; var height = size.Height * scale;
            return new Rect(Math.Max(0, Bounds.Width - width - 14), Math.Max(0, Bounds.Height - height - 14), width, height);
        }
    }
    private void PanFromNavigator(Point point)
    {
        var nav = NavigatorRect; var image = ImageRect;
        if (_zoom == 0) _zoom = ActualZoom;
        _pan = new Point((.5 - Math.Clamp((point.X - nav.X) / nav.Width, 0, 1)) * image.Width,
            (.5 - Math.Clamp((point.Y - nav.Y) / nav.Height, 0, 1)) * image.Height);
        ClampPan(); InvalidateVisual(); ViewChanged?.Invoke();
    }
    private Rect CropRect(Rect image) => new(image.X + State.Crop.X * image.Width,
        image.Y + State.Crop.Y * image.Height, State.Crop.Width * image.Width, State.Crop.Height * image.Height);
    private void DrawRulers(DrawingContext context, Rect rect)
    {
        var background = ThemeBrush("DaisyBase200Brush", Brushes.DimGray);
        var foreground = ThemeBrush("DaisyBaseContentBrush", Brushes.White);
        context.FillRectangle(background, new Rect(0, 0, Bounds.Width, RulerInset));
        context.FillRectangle(background, new Rect(0, 0, RulerInset, Bounds.Height));
        var scale = ActualZoom / 100 / RasterScale;
        var major = NiceStep(70 / scale);
        var minor = major / 5;
        var pen = new Pen(foreground, 1);
        for (var value = Math.Floor((RulerInset - rect.X) / scale / minor) * minor;
             rect.X + value * scale < Bounds.Width; value += minor)
        {
            var x = rect.X + value * scale;
            if (x < RulerInset) continue;
            var isMajor = Math.Abs(value / major - Math.Round(value / major)) < .000001;
            context.DrawLine(pen, new Point(x, RulerInset - (isMajor ? 10 : 4)), new Point(x, RulerInset));
            if (isMajor) context.DrawText(Label(Math.Round(value).ToString(CultureInfo.InvariantCulture), foreground), new Point(x + 2, 1));
        }
        for (var value = Math.Floor((RulerInset - rect.Y) / scale / minor) * minor;
             rect.Y + value * scale < Bounds.Height; value += minor)
        {
            var y = rect.Y + value * scale;
            if (y < RulerInset) continue;
            var isMajor = Math.Abs(value / major - Math.Round(value / major)) < .000001;
            context.DrawLine(pen, new Point(RulerInset - (isMajor ? 10 : 4), y), new Point(RulerInset, y));
            if (!isMajor) continue;
            using (context.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(1, y - 2)))
                context.DrawText(Label(Math.Round(value).ToString(CultureInfo.InvariantCulture), foreground), default);
        }
        if (_lastPointer is not { } pointer) return;
        var marker = new Pen(ThemeBrush("PhotosAccentBrush", Brushes.DodgerBlue), 1.5);
        context.DrawLine(marker, new Point(pointer.X, 0), new Point(pointer.X, RulerInset));
        context.DrawLine(marker, new Point(0, pointer.Y), new Point(RulerInset, pointer.Y));
    }
    private IBrush ThemeBrush(string key, IBrush fallback) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : fallback;
    private static FormattedText Label(string text, IBrush? foreground = null) => new(text, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, Typeface.Default, 10, foreground ?? Brushes.White);
    private static double NiceStep(double minimum)
    {
        if (!double.IsFinite(minimum) || minimum <= 0) return 100;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(minimum)));
        return new[] { 1d, 2, 5, 10 }.First(factor => factor * magnitude >= minimum) * magnitude;
    }
    private void DrawGridlines(DrawingContext context, Rect rect)
    {
        var scale = ActualZoom / 100 / RasterScale;
        var step = NiceStep(48 / scale) * scale;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 1);
        for (var x = rect.X + step; x < rect.Right; x += step)
            context.DrawLine(pen, new Point(x, rect.Y), new Point(x, rect.Bottom));
        for (var y = rect.Y + step; y < rect.Bottom; y += step)
            context.DrawLine(pen, new Point(rect.X, y), new Point(rect.Right, y));
    }
    private static void ShadeOutside(DrawingContext context, Rect outer, Rect inner)
    {
        var shade = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
        context.FillRectangle(shade, new Rect(outer.X, outer.Y, outer.Width, Math.Max(0, inner.Y - outer.Y)));
        context.FillRectangle(shade, new Rect(outer.X, inner.Bottom, outer.Width, Math.Max(0, outer.Bottom - inner.Bottom)));
        context.FillRectangle(shade, new Rect(outer.X, inner.Y, Math.Max(0, inner.X - outer.X), inner.Height));
        context.FillRectangle(shade, new Rect(inner.Right, inner.Y, Math.Max(0, outer.Right - inner.Right), inner.Height));
    }
    private Point Normalized(Point point)
    {
        var rect = ImageRect;
        return new Point(Math.Clamp((point.X - rect.X) / Math.Max(1, rect.Width), 0, 1),
            Math.Clamp((point.Y - rect.Y) / Math.Max(1, rect.Height), 0, 1));
    }
    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (_bitmap == null || IsSlideshow || e.Handled) return;
        Focus();
        var point = e.GetPosition(this);
        _secondary = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (NavigatorVisible && NavigatorRect.Contains(point))
        {
            _draggingNavigator = true; _pressed = point;
            PanFromNavigator(point); e.Pointer.Capture(this); e.Handled = true; return;
        }
        if (Eyedropper)
        {
            if (!ImageRect.Contains(point)) return;
            using var rendered = EditRenderer.Render(_bitmap, State);
            var p = Normalized(point);
            var color = rendered.GetPixel(Math.Min(rendered.Width - 1, (int)(p.X * rendered.Width)),
                Math.Min(rendered.Height - 1, (int)(p.Y * rendered.Height)));
            ColorPicked?.Invoke(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue), _secondary);
            Eyedropper = false; e.Handled = true; return;
        }
        _pressed = point; _panStart = _pan; _cropStart = State.Crop;
        if (IsCropping)
        {
            _cropHandle = HitTestCrop(CropRect(ImageRect), point);
            if (_cropHandle == CropHandle.None) { _pressed = null; return; }
        }
        _draggingDivider = Compare && !IsCropping && !IsDrawing && Math.Abs(point.X - (ImageRect.X + ImageRect.Width * Divider)) < 20;
        if (IsDrawing)
        {
            if (!ImageRect.Contains(point)) { _pressed = null; return; }
            _points.Add(ToStroke(point));
        }
        e.Pointer.Capture(this); e.Handled = true;
    }
    private Vector2 ToStroke(Point point)
    {
        var p = Normalized(point);
        return new Vector2((float)(State.Crop.X + p.X * State.Crop.Width), (float)(State.Crop.Y + p.Y * State.Crop.Height));
    }
    private void Moved(object? sender, PointerEventArgs e)
    {
        _lastPointer = e.GetPosition(this);
        if (PhotosSettings.ShowRulers) InvalidateVisual();
        if (_pressed is not { } start) return;
        var point = e.GetPosition(this);
        if (_draggingNavigator) { PanFromNavigator(point); return; }
        if (_draggingDivider) Divider = Math.Clamp((point.X - ImageRect.X) / ImageRect.Width, 0.02, 0.98);
        else if (IsDrawing) _points.Add(ToStroke(point));
        else if (_cropHandle != CropHandle.None)
        {
            State = State with { Crop = ResizeCrop(_cropStart, _cropHandle,
                (point.X - start.X) / ImageRect.Width, (point.Y - start.Y) / ImageRect.Height) };
        }
        else { _pan = _panStart + (point - start); ClampPan(); }
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    private MarkupStroke CurrentStroke() => new(_secondary ? SecondaryColor : PrimaryColor,
        (float)(PenWidth / 1000), Highlighter, _points.ToImmutableArray());
    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        EndInteraction(); e.Pointer.Capture(null);
    }
    private void EndInteraction()
    {
        if (_pressed == null) return;
        _pressed = null;
        if (_points.Count > 0) { Edited?.Invoke(State with { Strokes = State.Strokes.Add(CurrentStroke()) }); _points.Clear(); }
        else if (_cropHandle != CropHandle.None && State.Crop != _cropStart) Edited?.Invoke(State);
        _cropHandle = CropHandle.None; _draggingDivider = _draggingNavigator = false;
        InvalidateVisual();
    }
    public void Dispose()
    {
        _fadeTimer.Stop(); _previousBitmap?.Dispose(); _previousBitmap = null;
        _preview?.Dispose(); _preview = null;
        _bitmap?.Dispose(); _bitmap = null;
        _points.Clear(); _pressed = null;
        _cropHandle = CropHandle.None; _draggingDivider = _draggingNavigator = false;
        InvalidateVisual();
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
                var split = (float)(target.X + target.Width * divider);
                canvas.Save();
                canvas.ClipRect(new SKRect(0, 0, split, (float)bounds.Height));
                Draw(canvas, state.GeometryOnly());
                canvas.Restore();
                canvas.ClipRect(new SKRect(split, 0, (float)bounds.Width, (float)bounds.Height));
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
