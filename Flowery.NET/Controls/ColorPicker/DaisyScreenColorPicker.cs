using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// A control that allows picking colors from anywhere on the screen using an eyedropper tool.
    /// Click and hold, drag anywhere on screen, release to pick the color.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyScreenColorPicker : Control, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyScreenColorPicker);

        private const double BaseTextFontSize = 10.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            TextElement.SetFontSize(this, FloweryScaleManager.ApplyScale(BaseTextFontSize, 9.0, scaleFactor));
        }

        private bool _isCapturing;
        private Color _previewColor = Colors.Black;

        #region Styled Properties

        /// <summary>
        /// Gets or sets the selected color.
        /// </summary>
        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyScreenColorPicker, Color>(nameof(Color), Colors.Black);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the picker is currently capturing.
        /// </summary>
        public static readonly StyledProperty<bool> IsCapturingProperty =
            AvaloniaProperty.Register<DaisyScreenColorPicker, bool>(nameof(IsCapturing), false);

        public bool IsCapturing
        {
            get => GetValue(IsCapturingProperty);
            private set => SetValue(IsCapturingProperty, value);
        }

        /// <summary>
        /// Gets or sets an optional callback invoked when the color changes.
        /// </summary>
        public static readonly StyledProperty<Action<Color>?> OnColorChangedProperty =
            AvaloniaProperty.Register<DaisyScreenColorPicker, Action<Color>?>(nameof(OnColorChanged));

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

        /// <summary>
        /// Occurs when color picking is completed.
        /// </summary>
        public event EventHandler? PickingCompleted;

        /// <summary>
        /// Occurs when color picking is cancelled.
        /// </summary>
        public event EventHandler? PickingCancelled;

        static DaisyScreenColorPicker()
        {
            AffectsRender<DaisyScreenColorPicker>(ColorProperty, IsCapturingProperty);
        }

        public DaisyScreenColorPicker()
        {
            Width = 140;
            Height = 80;
            Cursor = new Cursor(StandardCursorType.Hand);
            Focusable = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 140 : Math.Min(availableSize.Width, 140),
                double.IsInfinity(availableSize.Height) ? 80 : Math.Min(availableSize.Height, 80));
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
            var displayColor = _isCapturing ? _previewColor : Color;

            // Background
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(45, 45, 48)), bounds);

            // Color preview (large)
            var previewRect = new Rect(8, 8, 50, 40);
            
            // Checkerboard for transparency
            DrawCheckerboard(context, previewRect);
            context.FillRectangle(new SolidColorBrush(displayColor), previewRect);
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 1), previewRect);

            // Hex value
            var hexText = $"#{displayColor.R:X2}{displayColor.G:X2}{displayColor.B:X2}";
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
            var hexFormatted = new FormattedText(hexText, System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, typeface, 12, Brushes.White);
            context.DrawText(hexFormatted, new Point(66, 10));

            // RGB values
            var rgbText = $"R:{displayColor.R} G:{displayColor.G} B:{displayColor.B}";
            var rgbTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            var rgbFormatted = new FormattedText(rgbText, System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, rgbTypeface, 9, new SolidColorBrush(Color.FromRgb(180, 180, 180)));
            context.DrawText(rgbFormatted, new Point(66, 28));

            // Status/instruction text
            var statusText = _isCapturing ? "Release to pick" : "Click & drag to pick";
            var statusTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            var statusFormatted = new FormattedText(statusText, System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, statusTypeface, 10, 
                _isCapturing ? new SolidColorBrush(Color.FromRgb(100, 200, 100)) : new SolidColorBrush(Color.FromRgb(150, 150, 150)));
            context.DrawText(statusFormatted, new Point(8, bounds.Height - 22));

            // Eyedropper icon (small)
            if (!_isCapturing)
            {
                DrawEyedropperIcon(context, new Point(bounds.Width - 24, bounds.Height - 24), 16);
            }

            // Border
            var borderColor = _isCapturing ? Color.FromRgb(100, 200, 100) : Color.FromRgb(80, 80, 80);
            context.DrawRectangle(new Pen(new SolidColorBrush(borderColor), _isCapturing ? 2 : 1), bounds);
        }

        private void DrawCheckerboard(DrawingContext context, Rect rect)
        {
            var checkSize = 6;
            var light = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            var dark = new SolidColorBrush(Color.FromRgb(200, 200, 200));

            for (int y = 0; y < rect.Height; y += checkSize)
            {
                for (int x = 0; x < rect.Width; x += checkSize)
                {
                    var isLight = ((x / checkSize) + (y / checkSize)) % 2 == 0;
                    var checkRect = new Rect(rect.X + x, rect.Y + y, 
                        Math.Min(checkSize, rect.Width - x), 
                        Math.Min(checkSize, rect.Height - y));
                    context.FillRectangle(isLight ? light : dark, checkRect);
                }
            }
        }

        private void DrawEyedropperIcon(DrawingContext context, Point center, double size)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 1.5);
            
            // Simple eyedropper shape
            var half = size / 2;
            
            // Tip
            context.DrawLine(pen, 
                new Point(center.X - half * 0.6, center.Y + half * 0.6),
                new Point(center.X - half * 0.1, center.Y + half * 0.1));
            
            // Body
            context.DrawLine(pen,
                new Point(center.X - half * 0.1, center.Y + half * 0.1),
                new Point(center.X + half * 0.4, center.Y - half * 0.4));
            
            // Bulb
            context.DrawEllipse(null, pen,
                new Point(center.X + half * 0.5, center.Y - half * 0.5),
                half * 0.3, half * 0.3);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                StartCapture();
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isCapturing)
            {
                UpdateColorFromScreen();
                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isCapturing)
            {
                CompleteCapture();
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);

            if (_isCapturing)
            {
                CancelCapture();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_isCapturing && e.Key == Key.Escape)
            {
                CancelCapture();
                e.Handled = true;
            }
        }

        private void StartCapture()
        {
            if (_isCapturing) return;

            _isCapturing = true;
            IsCapturing = true;
            _previewColor = Color;
            Cursor = new Cursor(StandardCursorType.Cross);
            Focus();
            InvalidateVisual();
        }

        private void UpdateColorFromScreen()
        {
            if (!_isCapturing) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var pos = GetCursorPosWin32();
                if (pos.HasValue)
                {
                    _previewColor = GetScreenPixelColorWin32((int)pos.Value.X, (int)pos.Value.Y);
                    InvalidateVisual();
                }
            }
        }

        private void CompleteCapture()
        {
            if (!_isCapturing) return;

            Color = _previewColor;
            StopCapture();

            ColorChanged?.Invoke(this, new ColorChangedEventArgs(Color));
            OnColorChanged?.Invoke(Color);
            PickingCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void CancelCapture()
        {
            if (!_isCapturing) return;

            StopCapture();
            PickingCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void StopCapture()
        {
            _isCapturing = false;
            IsCapturing = false;
            Cursor = new Cursor(StandardCursorType.Hand);
            InvalidateVisual();
        }

        // Windows P/Invoke
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int x, int y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static Point? GetCursorPosWin32()
        {
            if (GetCursorPos(out POINT pt))
            {
                return new Point(pt.X, pt.Y);
            }
            return null;
        }

        private static Color GetScreenPixelColorWin32(int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                uint pixel = GetPixel(hdc, x, y);
                if (pixel == 0xFFFFFFFF)
                {
                    return Colors.Black;
                }
                byte r = (byte)(pixel & 0xFF);
                byte g = (byte)((pixel >> 8) & 0xFF);
                byte b = (byte)((pixel >> 16) & 0xFF);
                return Color.FromRgb(r, g, b);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
    }
}
