using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Services;
using SkiaSharp;

namespace Flowery.Controls
{
    /// <summary>
    /// Blur rendering mode for DaisyGlass.
    /// </summary>
    public enum GlassBlurMode
    {
        /// <summary>
        /// Simulated glass using gradient overlays (no real blur).
        /// </summary>
        Simulated,

        /// <summary>
        /// Captures bitmap and applies BlurEffect (one-time capture).
        /// </summary>
        BitmapCapture,

        /// <summary>
        /// Uses SkiaSharp for GPU-accelerated blur (experimental).
        /// </summary>
        SkiaSharp
    }

    /// <summary>
    /// A glass/frosted effect container control styled after DaisyUI's glass effect.
    /// Supports multiple blur modes: Simulated, BitmapCapture, and SkiaSharp.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyGlass : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyGlass);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private bool _isCapturing;
        private RenderTargetBitmap? _capturedBitmap;
        private bool _needsUpdate = true;

        /// <summary>
        /// Gets or sets the blur amount for the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassBlurProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBlur), 40.0);

        public double GlassBlur
        {
            get => GetValue(GlassBlurProperty);
            set => SetValue(GlassBlurProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassOpacity), 0.25);

        public double GlassOpacity
        {
            get => GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the tint color for the glass effect.
        /// </summary>
        public static readonly StyledProperty<Color> GlassTintProperty =
            AvaloniaProperty.Register<DaisyGlass, Color>(nameof(GlassTint), Colors.White);

        public Color GlassTint
        {
            get => GetValue(GlassTintProperty);
            set => SetValue(GlassTintProperty, value);
        }

        /// <summary>
        /// Gets or sets the tint opacity for the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassTintOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassTintOpacity), 0.5);

        public double GlassTintOpacity
        {
            get => GetValue(GlassTintOpacityProperty);
            set => SetValue(GlassTintOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass border.
        /// </summary>
        public static readonly StyledProperty<double> GlassBorderOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBorderOpacity), 0.2);

        public double GlassBorderOpacity
        {
            get => GetValue(GlassBorderOpacityProperty);
            set => SetValue(GlassBorderOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the degree of the glass reflection.
        /// </summary>
        public static readonly StyledProperty<double> GlassReflectDegreeProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassReflectDegree), 100.0);

        public double GlassReflectDegree
        {
            get => GetValue(GlassReflectDegreeProperty);
            set => SetValue(GlassReflectDegreeProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass reflection.
        /// </summary>
        public static readonly StyledProperty<double> GlassReflectOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassReflectOpacity), 0.1);

        public double GlassReflectOpacity
        {
            get => GetValue(GlassReflectOpacityProperty);
            set => SetValue(GlassReflectOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the text shadow effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassTextShadowOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassTextShadowOpacity), 0.5);

        public double GlassTextShadowOpacity
        {
            get => GetValue(GlassTextShadowOpacityProperty);
            set => SetValue(GlassTextShadowOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the saturation of the glass background (0.0 = grayscale, 1.0 = normal).
        /// </summary>
        public static readonly StyledProperty<double> GlassSaturationProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassSaturation), 1.0);

        public double GlassSaturation
        {
            get => GetValue(GlassSaturationProperty);
            set => SetValue(GlassSaturationProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to enable real backdrop blur (performance intensive).
        /// When false, uses the simulated glass effect.
        /// </summary>
        public static readonly StyledProperty<bool> EnableBackdropBlurProperty =
            AvaloniaProperty.Register<DaisyGlass, bool>(nameof(EnableBackdropBlur), false);

        public bool EnableBackdropBlur
        {
            get => GetValue(EnableBackdropBlurProperty);
            set => SetValue(EnableBackdropBlurProperty, value);
        }

        /// <summary>
        /// Gets or sets the blur rendering mode.
        /// </summary>
        public static readonly StyledProperty<GlassBlurMode> BlurModeProperty =
            AvaloniaProperty.Register<DaisyGlass, GlassBlurMode>(nameof(BlurMode), GlassBlurMode.BitmapCapture);

        public GlassBlurMode BlurMode
        {
            get => GetValue(BlurModeProperty);
            set => SetValue(BlurModeProperty, value);
        }

        /// <summary>
        /// Internal property for the blurred background bitmap.
        /// </summary>
        public static readonly StyledProperty<IImage?> BlurredBackgroundProperty =
            AvaloniaProperty.Register<DaisyGlass, IImage?>(nameof(BlurredBackground));

        public IImage? BlurredBackground
        {
            get => GetValue(BlurredBackgroundProperty);
            private set => SetValue(BlurredBackgroundProperty, value);
        }

        static DaisyGlass()
        {
            AffectsRender<DaisyGlass>(EnableBackdropBlurProperty, GlassBlurProperty);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _needsUpdate = true;
            if (EnableBackdropBlur)
            {
                ScheduleBackdropCapture();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _capturedBitmap?.Dispose();
            _capturedBitmap = null;
            BlurredBackground = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == EnableBackdropBlurProperty)
            {
                if (EnableBackdropBlur && this.GetVisualRoot() != null)
                {
                    ScheduleBackdropCapture();
                }
                else
                {
                    BlurredBackground = null;
                }
            }
            else if (change.Property == GlassBlurProperty && EnableBackdropBlur)
            {
                _needsUpdate = true;
                ScheduleBackdropCapture();
            }
            else if (change.Property == BoundsProperty && EnableBackdropBlur)
            {
                _needsUpdate = true;
                ScheduleBackdropCapture();
            }
        }

        private void ScheduleBackdropCapture()
        {
            if (_isCapturing || !_needsUpdate)
                return;

            Dispatcher.UIThread.Post(CaptureAndBlurBackdrop, DispatcherPriority.Background);
        }

        private async void CaptureAndBlurBackdrop()
        {
            if (_isCapturing || !EnableBackdropBlur || this.GetVisualRoot() == null)
                return;

            _isCapturing = true;
            _needsUpdate = false;

            try
            {
                var blurredBitmap = await CaptureBackdropAsync();
                if (blurredBitmap != null)
                {
                    var oldBitmap = _capturedBitmap;
                    _capturedBitmap = blurredBitmap;
                    BlurredBackground = blurredBitmap;
                    oldBitmap?.Dispose();
                }
            }
            catch
            {
                // Silently handle capture failures
            }
            finally
            {
                _isCapturing = false;
            }
        }

        private async Task<RenderTargetBitmap?> CaptureBackdropAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return Dispatcher.UIThread.Invoke(() => CaptureBackdrop());
                }
                catch
                {
                    return null;
                }
            });
        }

        private RenderTargetBitmap? CaptureBackdrop()
        {
            // Find the background container (parent with background)
            var backgroundSource = FindBackgroundSource();
            if (backgroundSource == null)
                return null;

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            // Get our position relative to the background source
            var transform = this.TransformToVisual(backgroundSource);
            if (transform == null)
                return null;

            var topLeft = transform.Value.Transform(new Point(0, 0));

            // Calculate capture area with some padding for blur edge
            var blurPadding = Math.Min(GlassBlur, 20);
            var captureX = Math.Max(0, topLeft.X - blurPadding);
            var captureY = Math.Max(0, topLeft.Y - blurPadding);
            var captureWidth = Math.Min(bounds.Width + blurPadding * 2, backgroundSource.Bounds.Width - captureX);
            var captureHeight = Math.Min(bounds.Height + blurPadding * 2, backgroundSource.Bounds.Height - captureY);

            if (captureWidth <= 0 || captureHeight <= 0)
                return null;

            // Render at reduced resolution for performance (0.75 = good quality/performance balance)
            var scale = 0.75;
            var pixelWidth = (int)Math.Ceiling(captureWidth * scale);
            var pixelHeight = (int)Math.Ceiling(captureHeight * scale);

            if (pixelWidth <= 0 || pixelHeight <= 0)
                return null;

            // Temporarily hide this control during capture
            var originalOpacity = Opacity;
            Opacity = 0;

            try
            {
                var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96 * scale, 96 * scale));

                using (var ctx = bitmap.CreateDrawingContext())
                {
                    // Apply transform to capture the correct region
                    ctx.PushTransform(Matrix.CreateTranslation(-captureX, -captureY) * Matrix.CreateScale(scale, scale));

                    // Render the background source
                    backgroundSource.Render(ctx);
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
            finally
            {
                Opacity = originalOpacity;
            }
        }

        private Visual? FindBackgroundSource()
        {
            // Walk up the tree to find the FIRST parent with a background
            Visual? current = this.GetVisualParent();

            while (current != null)
            {
                // Stop at window/top level - don't use these
                if (current is TopLevel)
                    break;

                // Return the FIRST parent with a background (closest to us)
                if (current is Border border && border.Background != null)
                    return border;
                if (current is Panel panel && panel.Background != null)
                    return panel;

                current = current.GetVisualParent();
            }

            // Fallback: just use immediate parent
            return this.GetVisualParent();
        }

        /// <summary>
        /// Call this to manually refresh the backdrop blur (e.g., after content changes).
        /// </summary>
        public void RefreshBackdrop()
        {
            if (EnableBackdropBlur)
            {
                _needsUpdate = true;
                ScheduleBackdropCapture();
            }
        }

        /// <summary>
        /// Override render to support SkiaSharp blur mode.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            if (EnableBackdropBlur && BlurMode == GlassBlurMode.SkiaSharp)
            {
                // Use custom SkiaSharp draw operation for real-time blur
                var operation = new SkiaGlassDrawOperation(
                    new Rect(0, 0, Bounds.Width, Bounds.Height),
                    GlassBlur,
                    GlassTint,
                    GlassTintOpacity,
                    CornerRadius.TopLeft,
                    GlassSaturation);

                context.Custom(operation);
            }

            base.Render(context);
        }
    }

    /// <summary>
    /// Custom SkiaSharp draw operation for real-time glass blur effect.
    /// </summary>
    internal class SkiaGlassDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly float _blurSigma;
        private readonly SKColor _tintColor;
        private readonly float _cornerRadius;
        private readonly float _saturation;

        public SkiaGlassDrawOperation(
            Rect bounds,
            double blurRadius,
            Color tintColor,
            double tintOpacity,
            double cornerRadius,
            double saturation)
        {
            _bounds = bounds;
            // Use a divisor to make the blur scale more gradual.
            // Previous: / 2.0 (too strong). Removed: direct (way too strong).
            // New: / 4.0 - this makes GlassBlur=10 roughly Sigma=2.5, which is a soft start.
            // GlassBlur=100 becomes Sigma=25, which is very blurry but still distinct.
            _blurSigma = (float)(blurRadius / 10.0);
            _tintColor = new SKColor(
                tintColor.R,
                tintColor.G,
                tintColor.B,
                (byte)(255 * tintOpacity));
            _cornerRadius = (float)cornerRadius;
            _saturation = (float)saturation;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is SkiaGlassDrawOperation op &&
                   op._bounds == _bounds &&
                   Math.Abs(op._blurSigma - _blurSigma) < 0.1f &&
                   Math.Abs(op._saturation - _saturation) < 0.01f;
        }

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            if (canvas == null)
                return;

            var rect = new SKRect(
                (float)_bounds.X,
                (float)_bounds.Y,
                (float)_bounds.Right,
                (float)_bounds.Bottom);

            var roundedRect = new SKRoundRect(rect, _cornerRadius);

            // Save canvas state
            int saveCount = canvas.Save();

            // Clip to our bounds
            canvas.ClipRoundRect(roundedRect, SKClipOperation.Intersect, true);

            // Create blur filter
            using var blurFilter = SKImageFilter.CreateBlur(_blurSigma, _blurSigma, SKShaderTileMode.Clamp);

            SKImageFilter? backdropFilter = blurFilter;
            SKColorFilter? colorFilter = null;

            // Apply saturation if needed
            if (Math.Abs(_saturation - 1.0f) > 0.01f)
            {
                // Standard luminance weights
                float r = 0.2126f;
                float g = 0.7152f;
                float b = 0.0722f;

                float sr = (1 - _saturation) * r;
                float sg = (1 - _saturation) * g;
                float sb = (1 - _saturation) * b;

                float[] matrix = new float[]
                {
                    sr + _saturation, sr,              sr,              0, 0,
                    sg,               sg + _saturation, sg,              0, 0,
                    sb,               sb,              sb + _saturation, 0, 0,
                    0,                0,               0,                1, 0
                };

                colorFilter = SKColorFilter.CreateColorMatrix(matrix);
                backdropFilter = SKImageFilter.CreateColorFilter(colorFilter, blurFilter);
            }

            // Try to use SKSaveLayerRec if available (Standard way)
            // Note: If SKSaveLayerRec is missing in this binding, we fallback to Snapshot
            bool useSnapshot = true;

            if (!useSnapshot)
            {
                // Original attempt - failed compilation for user
                /*
                var saveLayerRec = new SKSaveLayerRec
                {
                    Bounds = rect,
                    Backdrop = backdropFilter
                };
                canvas.SaveLayer(saveLayerRec);
                canvas.Restore();
                */
            }

            // Fallback: Snapshot approach (Manual Backdrop Blur)
            // This is robust and works without SKSaveLayerRec
            if (lease.SkSurface is SKSurface surface)
            {
                using var snapshot = surface.Snapshot();
                using var paint = new SKPaint
                {
                    ImageFilter = backdropFilter,
                    IsAntialias = true
                };

                // IMPORTANT: The snapshot is in Device Coordinates (pixels).
                // The canvas is currently in Local Coordinates (with transforms/scaling).
                // To align the background perfectly, we must draw in Device Coordinates.

                canvas.Save();
                // Reset matrix to Identity (Device Coordinates) so 0,0 is top-left of window
                canvas.ResetMatrix();

                // Draw the snapshot at 0,0.
                // The previous clip (rounded rect) is preserved in device space, so it masks correctly.
                canvas.DrawImage(snapshot, 0, 0, paint);

                canvas.Restore();
            }

            // Dispose filters
            if (backdropFilter != blurFilter)
            {
                backdropFilter?.Dispose();
            }
            colorFilter?.Dispose();

            // Draw tint overlay
            using var tintPaint = new SKPaint
            {
                Color = _tintColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(roundedRect, tintPaint);

            // Draw highlight border (gradient from top to bottom to simulate light source)
            using var highlightPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };

            // Gradient: White (Top) -> Transparent (Bottom)
            var colors = new SKColor[]
            {
                new SKColor(255, 255, 255, 80),
                new SKColor(255, 255, 255, 0)
            };

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint((float)_bounds.Left, (float)_bounds.Top),
                new SKPoint((float)_bounds.Left, (float)_bounds.Bottom),
                colors,
                null,
                SKShaderTileMode.Clamp);

            highlightPaint.Shader = shader;

            canvas.DrawRoundRect(roundedRect, highlightPaint);

            // Restore canvas
            canvas.RestoreToCount(saveCount);
        }
    }
}
