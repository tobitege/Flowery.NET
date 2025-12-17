using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Flowery.Services;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// A circular color wheel control for selecting hue and saturation values.
    /// Displays colors in HSL color space with the selected color indicated by a marker.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyColorWheel : Control, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyColorWheel);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            TextElement.SetFontSize(this, FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor));
        }

        private WriteableBitmap? _wheelBitmap;
        private bool _isDragging;
        private Point _centerPoint;
        private double _radius;
        private bool _lockUpdates;

        #region Styled Properties

        /// <summary>
        /// Gets or sets the selected color.
        /// </summary>
        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyColorWheel, Color>(nameof(Color), Colors.Red);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the HSL color representation.
        /// </summary>
        public static readonly StyledProperty<HslColor> HslColorProperty =
            AvaloniaProperty.Register<DaisyColorWheel, HslColor>(nameof(HslColor), new HslColor(0, 1, 0.5));

        public HslColor HslColor
        {
            get => GetValue(HslColorProperty);
            set => SetValue(HslColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the lightness value (0-1). The wheel displays colors at this lightness level.
        /// </summary>
        public static readonly StyledProperty<double> LightnessProperty =
            AvaloniaProperty.Register<DaisyColorWheel, double>(nameof(Lightness), 0.5);

        public double Lightness
        {
            get => GetValue(LightnessProperty);
            set => SetValue(LightnessProperty, Math.Min(1, Math.Max(0, value)));
        }

        /// <summary>
        /// Gets or sets the size of the selection marker.
        /// </summary>
        public static readonly StyledProperty<double> SelectionSizeProperty =
            AvaloniaProperty.Register<DaisyColorWheel, double>(nameof(SelectionSize), 10);

        public double SelectionSize
        {
            get => GetValue(SelectionSizeProperty);
            set => SetValue(SelectionSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the selection marker outline.
        /// </summary>
        public static readonly StyledProperty<Color> SelectionOutlineColorProperty =
            AvaloniaProperty.Register<DaisyColorWheel, Color>(nameof(SelectionOutlineColor), Colors.White);

        public Color SelectionOutlineColor
        {
            get => GetValue(SelectionOutlineColorProperty);
            set => SetValue(SelectionOutlineColorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show center crosshairs.
        /// </summary>
        public static readonly StyledProperty<bool> ShowCenterLinesProperty =
            AvaloniaProperty.Register<DaisyColorWheel, bool>(nameof(ShowCenterLines), false);

        public bool ShowCenterLines
        {
            get => GetValue(ShowCenterLinesProperty);
            set => SetValue(ShowCenterLinesProperty, value);
        }

        /// <summary>
        /// Gets or sets the color step for generating the wheel (lower = smoother, higher = faster).
        /// </summary>
        public static readonly StyledProperty<int> ColorStepProperty =
            AvaloniaProperty.Register<DaisyColorWheel, int>(nameof(ColorStep), 4);

        public int ColorStep
        {
            get => GetValue(ColorStepProperty);
            set => SetValue(ColorStepProperty, Math.Max(1, value));
        }

        /// <summary>
        /// Gets or sets an optional callback invoked when the color changes.
        /// This provides a simpler alternative to the ColorChanged event.
        /// </summary>
        public static readonly StyledProperty<Action<Color>?> OnColorChangedProperty =
            AvaloniaProperty.Register<DaisyColorWheel, Action<Color>?>(nameof(OnColorChanged));

        public Action<Color>? OnColorChanged
        {
            get => GetValue(OnColorChangedProperty);
            set => SetValue(OnColorChangedProperty, value);
        }

        #endregion

        /// <summary>
        /// Occurs when the color changes.
        /// </summary>
        public event EventHandler<ColorChangedEventArgs>? ColorChanged;

        static DaisyColorWheel()
        {
            AffectsRender<DaisyColorWheel>(
                ColorProperty,
                HslColorProperty,
                LightnessProperty,
                SelectionSizeProperty,
                SelectionOutlineColorProperty,
                ShowCenterLinesProperty);

            ColorProperty.Changed.AddClassHandler<DaisyColorWheel>((x, e) => x.OnColorPropertyChanged(e));
            HslColorProperty.Changed.AddClassHandler<DaisyColorWheel>((x, e) => x.OnHslColorPropertyChanged(e));
            LightnessProperty.Changed.AddClassHandler<DaisyColorWheel>((x, _) => x.InvalidateWheel());
            ColorStepProperty.Changed.AddClassHandler<DaisyColorWheel>((x, _) => x.InvalidateWheel());
        }

        public DaisyColorWheel()
        {
            ClipToBounds = true;
        }

        private void OnColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                var color = (Color)e.NewValue!;
                var hsl = new HslColor(color);
                hsl.L = Lightness; // Keep the wheel's lightness
                HslColor = hsl;
                OnColorChangedRaised(new ColorChangedEventArgs(color));
            }
            finally
            {
                _lockUpdates = false;
            }
            InvalidateVisual();
        }

        private void OnHslColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                var hsl = (HslColor)e.NewValue!;
                Color = hsl.ToRgbColor();
            }
            finally
            {
                _lockUpdates = false;
            }
            InvalidateVisual();
        }

        private void InvalidateWheel()
        {
            _wheelBitmap?.Dispose();
            _wheelBitmap = null;
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = Math.Min(availableSize.Width, availableSize.Height);
            if (double.IsInfinity(size) || double.IsNaN(size))
                size = 200;
            return new Size(size, size);
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateWheel();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            var size = Math.Min(bounds.Width, bounds.Height);
            if (size <= 0) return;

            _centerPoint = new Point(bounds.Width / 2, bounds.Height / 2);
            _radius = (size / 2) - (SelectionSize / 2) - 2;

            if (_radius <= 0) return;

            // Create wheel bitmap if needed
            EnsureWheelBitmap((int)size);

            // Draw the color wheel
            if (_wheelBitmap != null)
            {
                var destRect = new Rect(
                    _centerPoint.X - size / 2,
                    _centerPoint.Y - size / 2,
                    size, size);
                context.DrawImage(_wheelBitmap, destRect);
            }

            // Draw center lines if enabled
            if (ShowCenterLines)
            {
                var linePen = new Pen(new SolidColorBrush(Color.FromArgb(128, 128, 128, 128)), 1);
                context.DrawLine(linePen, new Point(_centerPoint.X - _radius, _centerPoint.Y), new Point(_centerPoint.X + _radius, _centerPoint.Y));
                context.DrawLine(linePen, new Point(_centerPoint.X, _centerPoint.Y - _radius), new Point(_centerPoint.X, _centerPoint.Y + _radius));
            }

            // Draw selection marker
            DrawSelectionMarker(context);
        }

        private void EnsureWheelBitmap(int size)
        {
            if (_wheelBitmap != null && _wheelBitmap.PixelSize.Width == size)
                return;

            _wheelBitmap?.Dispose();
            _wheelBitmap = new WriteableBitmap(
                new PixelSize(size, size),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            var center = size / 2.0;
            var radius = center - (SelectionSize / 2) - 2;

            using (var fb = _wheelBitmap.Lock())
            {
                var pixels = new byte[size * size * 4];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var dx = x - center;
                        var dy = y - center;
                        var distance = Math.Sqrt(dx * dx + dy * dy);
                        var offset = (y * size + x) * 4;

                        if (distance <= radius)
                        {
                            // Calculate hue from angle
                            var angle = Math.Atan2(dy, dx);
                            var hue = (angle * 180 / Math.PI + 360) % 360;

                            // Calculate saturation from distance
                            var saturation = distance / radius;

                            // Convert HSL to RGB
                            var color = HslColor.HslToRgb(hue, saturation, Lightness);

                            // BGRA format
                            pixels[offset] = color.B;
                            pixels[offset + 1] = color.G;
                            pixels[offset + 2] = color.R;
                            pixels[offset + 3] = color.A;
                        }
                        else
                        {
                            // Transparent
                            pixels[offset] = 0;
                            pixels[offset + 1] = 0;
                            pixels[offset + 2] = 0;
                            pixels[offset + 3] = 0;
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
            }
        }

        private void DrawSelectionMarker(DrawingContext context)
        {
            var hsl = HslColor;
            var angle = hsl.H * Math.PI / 180;
            var distance = hsl.S * _radius;

            var markerX = _centerPoint.X + Math.Cos(angle) * distance;
            var markerY = _centerPoint.Y + Math.Sin(angle) * distance;
            var markerPoint = new Point(markerX, markerY);

            var halfSize = SelectionSize / 2;

            // Draw outer circle (white outline)
            var outerPen = new Pen(new SolidColorBrush(SelectionOutlineColor), 2);
            context.DrawEllipse(null, outerPen, markerPoint, halfSize + 1, halfSize + 1);

            // Draw inner circle (black outline for contrast)
            var innerPen = new Pen(Brushes.Black, 1);
            context.DrawEllipse(null, innerPen, markerPoint, halfSize - 1, halfSize - 1);

            // Fill with current color
            context.DrawEllipse(new SolidColorBrush(Color), null, markerPoint, halfSize - 2, halfSize - 2);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                e.Pointer.Capture(this);
                UpdateColorFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDragging)
            {
                UpdateColorFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isDragging)
            {
                _isDragging = false;
                e.Pointer.Capture(null);
            }
        }

        private void UpdateColorFromPoint(Point point)
        {
            var dx = point.X - _centerPoint.X;
            var dy = point.Y - _centerPoint.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            // Calculate hue from angle
            var angle = Math.Atan2(dy, dx);
            var hue = (angle * 180 / Math.PI + 360) % 360;

            // Calculate saturation from distance (clamped to radius)
            var saturation = Math.Min(1, distance / _radius);

            _lockUpdates = true;
            try
            {
                var hsl = new HslColor(hue, saturation, Lightness);
                HslColor = hsl;
                Color = hsl.ToRgbColor();
                OnColorChangedRaised(new ColorChangedEventArgs(Color));
            }
            finally
            {
                _lockUpdates = false;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// Gets the color at the specified point on the wheel.
        /// </summary>
        public Color GetColorAtPoint(Point point)
        {
            var dx = point.X - _centerPoint.X;
            var dy = point.Y - _centerPoint.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance > _radius)
                return Colors.Transparent;

            var angle = Math.Atan2(dy, dx);
            var hue = (angle * 180 / Math.PI + 360) % 360;
            var saturation = distance / _radius;

            return HslColor.HslToRgb(hue, saturation, Lightness);
        }

        protected virtual void OnColorChangedRaised(ColorChangedEventArgs e)
        {
            ColorChanged?.Invoke(this, e);
            OnColorChanged?.Invoke(e.Color);
        }
    }

    /// <summary>
    /// Event arguments for color change events.
    /// </summary>
    public class ColorChangedEventArgs : EventArgs
    {
        public ColorChangedEventArgs(Color color)
        {
            Color = color;
        }

        public Color Color { get; }
    }
}
