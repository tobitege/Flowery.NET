using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// Specifies the color channel to display in a color slider.
    /// </summary>
    public enum ColorSliderChannel
    {
        Red,
        Green,
        Blue,
        Alpha,
        Hue,
        Saturation,
        Lightness
    }

    /// <summary>
    /// A slider control for selecting individual color channel values.
    /// </summary>
    public class DaisyColorSlider : RangeBase
    {
        protected override Type StyleKeyOverride => typeof(DaisyColorSlider);

        private WriteableBitmap? _gradientBitmap;
        private bool _isDragging;
        private bool _lockUpdates;

        #region Styled Properties

        /// <summary>
        /// Gets or sets the color channel this slider controls.
        /// </summary>
        public static readonly StyledProperty<ColorSliderChannel> ChannelProperty =
            AvaloniaProperty.Register<DaisyColorSlider, ColorSliderChannel>(nameof(Channel), ColorSliderChannel.Hue);

        public ColorSliderChannel Channel
        {
            get => GetValue(ChannelProperty);
            set => SetValue(ChannelProperty, value);
        }

        /// <summary>
        /// Gets or sets the current color.
        /// </summary>
        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyColorSlider, Color>(nameof(Color), Colors.Red);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the orientation of the slider.
        /// </summary>
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyColorSlider, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>
        /// Gets or sets the size of the thumb/nub.
        /// </summary>
        public static readonly StyledProperty<double> NubSizeProperty =
            AvaloniaProperty.Register<DaisyColorSlider, double>(nameof(NubSize), 8);

        public double NubSize
        {
            get => GetValue(NubSizeProperty);
            set => SetValue(NubSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the checkerboard pattern for alpha channel.
        /// </summary>
        public static readonly StyledProperty<bool> ShowCheckerboardProperty =
            AvaloniaProperty.Register<DaisyColorSlider, bool>(nameof(ShowCheckerboard), true);

        public bool ShowCheckerboard
        {
            get => GetValue(ShowCheckerboardProperty);
            set => SetValue(ShowCheckerboardProperty, value);
        }

        /// <summary>
        /// Gets or sets an optional callback invoked when the color changes.
        /// This provides a simpler alternative to the ColorChanged event.
        /// </summary>
        public static readonly StyledProperty<Action<Color>?> OnColorChangedProperty =
            AvaloniaProperty.Register<DaisyColorSlider, Action<Color>?>(nameof(OnColorChanged));

        public Action<Color>? OnColorChanged
        {
            get => GetValue(OnColorChangedProperty);
            set => SetValue(OnColorChangedProperty, value);
        }

        #endregion

        /// <summary>
        /// Occurs when the color value changes.
        /// </summary>
        public event EventHandler<ColorChangedEventArgs>? ColorChanged;

        static DaisyColorSlider()
        {
            AffectsRender<DaisyColorSlider>(
                ChannelProperty,
                ColorProperty,
                OrientationProperty,
                NubSizeProperty,
                ValueProperty);

            ChannelProperty.Changed.AddClassHandler<DaisyColorSlider>((x, _) => x.InvalidateGradient());
            ColorProperty.Changed.AddClassHandler<DaisyColorSlider>((x, e) => x.OnColorPropertyChanged(e));
            OrientationProperty.Changed.AddClassHandler<DaisyColorSlider>((x, _) => x.InvalidateGradient());
            ValueProperty.Changed.AddClassHandler<DaisyColorSlider>((x, e) => x.OnValuePropertyChanged(e));
        }

        public DaisyColorSlider()
        {
            Minimum = 0;
            UpdateMaximum();
            Value = 0;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        private void UpdateMaximum()
        {
            Maximum = Channel switch
            {
                ColorSliderChannel.Hue => 359,
                ColorSliderChannel.Saturation or ColorSliderChannel.Lightness => 100,
                _ => 255
            };
        }

        private void OnColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                var color = (Color)e.NewValue!;
                Value = GetChannelValue(color);
                InvalidateGradient();
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void OnValuePropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                var newColor = SetChannelValue(Color, (double)e.NewValue!);
                Color = newColor;
                OnColorChangedRaised(new ColorChangedEventArgs(newColor));
                InvalidateGradient();
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private double GetChannelValue(Color color)
        {
            return Channel switch
            {
                ColorSliderChannel.Red => color.R,
                ColorSliderChannel.Green => color.G,
                ColorSliderChannel.Blue => color.B,
                ColorSliderChannel.Alpha => color.A,
                ColorSliderChannel.Hue => new HslColor(color).H,
                ColorSliderChannel.Saturation => new HslColor(color).S * 100,
                ColorSliderChannel.Lightness => new HslColor(color).L * 100,
                _ => 0
            };
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private Color SetChannelValue(Color color, double value)
        {
            return Channel switch
            {
                ColorSliderChannel.Red => Color.FromArgb(color.A, (byte)Clamp(value, 0, 255), color.G, color.B),
                ColorSliderChannel.Green => Color.FromArgb(color.A, color.R, (byte)Clamp(value, 0, 255), color.B),
                ColorSliderChannel.Blue => Color.FromArgb(color.A, color.R, color.G, (byte)Clamp(value, 0, 255)),
                ColorSliderChannel.Alpha => Color.FromArgb((byte)Clamp(value, 0, 255), color.R, color.G, color.B),
                ColorSliderChannel.Hue => GetHslModifiedColor(color, h: value),
                ColorSliderChannel.Saturation => GetHslModifiedColor(color, s: value / 100),
                ColorSliderChannel.Lightness => GetHslModifiedColor(color, l: value / 100),
                _ => color
            };
        }

        private Color GetHslModifiedColor(Color color, double? h = null, double? s = null, double? l = null)
        {
            var hsl = new HslColor(color);
            if (h.HasValue) hsl.H = h.Value;
            if (s.HasValue) hsl.S = s.Value;
            if (l.HasValue) hsl.L = l.Value;
            return hsl.ToRgbColor(color.A);
        }

        private void InvalidateGradient()
        {
            UpdateMaximum();
            _gradientBitmap?.Dispose();
            _gradientBitmap = null;
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Orientation == Orientation.Horizontal)
            {
                var width = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
                return new Size(width, 20);
            }
            else
            {
                var height = double.IsInfinity(availableSize.Height) ? 200 : availableSize.Height;
                return new Size(20, height);
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateGradient();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            var isHorizontal = Orientation == Orientation.Horizontal;
            var trackLength = isHorizontal ? bounds.Width : bounds.Height;
            var trackThickness = isHorizontal ? bounds.Height : bounds.Width;

            if (trackLength <= 0 || trackThickness <= 0) return;

            var trackRect = new Rect(0, 0, bounds.Width, bounds.Height);

            // Draw checkerboard for alpha channel
            if (Channel == ColorSliderChannel.Alpha && ShowCheckerboard)
            {
                DrawCheckerboard(context, trackRect);
            }

            // Draw gradient
            EnsureGradientBitmap((int)trackLength, (int)trackThickness);
            if (_gradientBitmap != null)
            {
                context.DrawImage(_gradientBitmap, trackRect);
            }

            // Draw border
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 160, 160)), 1);
            context.DrawRectangle(borderPen, trackRect);

            // Draw thumb/nub
            DrawNub(context, bounds);
        }

        private void EnsureGradientBitmap(int length, int thickness)
        {
            if (_gradientBitmap != null && _gradientBitmap.PixelSize.Width == length)
                return;

            var isHorizontal = Orientation == Orientation.Horizontal;
            var width = isHorizontal ? length : thickness;
            var height = isHorizontal ? thickness : length;

            _gradientBitmap?.Dispose();
            _gradientBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var fb = _gradientBitmap.Lock())
            {
                var pixels = new byte[width * height * 4];

                for (int i = 0; i < length; i++)
                {
                    var ratio = (double)i / (length - 1);
                    var value = Minimum + ratio * (Maximum - Minimum);
                    var color = SetChannelValue(Color, value);

                    // Premultiply alpha
                    var a = color.A;
                    var r = (byte)(color.R * a / 255);
                    var g = (byte)(color.G * a / 255);
                    var b = (byte)(color.B * a / 255);

                    if (isHorizontal)
                    {
                        for (int j = 0; j < thickness; j++)
                        {
                            var offset = (j * width + i) * 4;
                            pixels[offset] = b;
                            pixels[offset + 1] = g;
                            pixels[offset + 2] = r;
                            pixels[offset + 3] = a;
                        }
                    }
                    else
                    {
                        for (int j = 0; j < thickness; j++)
                        {
                            var offset = (i * width + j) * 4;
                            pixels[offset] = b;
                            pixels[offset + 1] = g;
                            pixels[offset + 2] = r;
                            pixels[offset + 3] = a;
                        }
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
            }
        }

        private void DrawCheckerboard(DrawingContext context, Rect rect)
        {
            var checkSize = 4;
            var light = new SolidColorBrush(Colors.White);
            var dark = new SolidColorBrush(Color.FromRgb(204, 204, 204));

            using (context.PushClip(rect))
            {
                for (int y = 0; y < rect.Height; y += checkSize)
                {
                    for (int x = 0; x < rect.Width; x += checkSize)
                    {
                        var brush = ((x / checkSize + y / checkSize) % 2 == 0) ? light : dark;
                        context.FillRectangle(brush, new Rect(rect.X + x, rect.Y + y, checkSize, checkSize));
                    }
                }
            }
        }

        private void DrawNub(DrawingContext context, Rect bounds)
        {
            var isHorizontal = Orientation == Orientation.Horizontal;
            var trackLength = isHorizontal ? bounds.Width : bounds.Height;
            var position = (Value - Minimum) / (Maximum - Minimum) * trackLength;

            var nubRect = isHorizontal
                ? new Rect(position - NubSize / 2, 0, NubSize, bounds.Height)
                : new Rect(0, position - NubSize / 2, bounds.Width, NubSize);

            // Draw nub outline
            var outerPen = new Pen(Brushes.White, 2);
            var innerPen = new Pen(Brushes.Black, 1);

            if (isHorizontal)
            {
                var x = position;
                context.DrawLine(outerPen, new Point(x, 0), new Point(x, bounds.Height));
                context.DrawLine(innerPen, new Point(x - 1, 0), new Point(x - 1, bounds.Height));
                context.DrawLine(innerPen, new Point(x + 1, 0), new Point(x + 1, bounds.Height));
            }
            else
            {
                var y = position;
                context.DrawLine(outerPen, new Point(0, y), new Point(bounds.Width, y));
                context.DrawLine(innerPen, new Point(0, y - 1), new Point(bounds.Width, y - 1));
                context.DrawLine(innerPen, new Point(0, y + 1), new Point(bounds.Width, y + 1));
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                e.Pointer.Capture(this);
                UpdateValueFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDragging)
            {
                UpdateValueFromPoint(e.GetPosition(this));
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

        private void UpdateValueFromPoint(Point point)
        {
            var isHorizontal = Orientation == Orientation.Horizontal;
            var trackLength = isHorizontal ? Bounds.Width : Bounds.Height;
            var position = isHorizontal ? point.X : point.Y;

            var ratio = Clamp(position / trackLength, 0, 1);
            Value = Minimum + ratio * (Maximum - Minimum);
        }

        protected virtual void OnColorChangedRaised(ColorChangedEventArgs e)
        {
            ColorChanged?.Invoke(this, e);
            OnColorChanged?.Invoke(e.Color);
        }
    }
}

