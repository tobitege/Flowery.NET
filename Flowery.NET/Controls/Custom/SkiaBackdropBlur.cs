using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Flowery.Controls.Custom
{
    /// <summary>
    /// A custom draw operation that applies real-time backdrop blur using SkiaSharp.
    /// This provides GPU-accelerated blur without needing to capture bitmaps.
    /// </summary>
    public class SkiaBackdropBlurDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly float _blurRadius;
        private readonly SKColor _tintColor;
        private readonly float _tintOpacity;
        private readonly float _cornerRadius;

        public SkiaBackdropBlurDrawOperation(
            Rect bounds,
            double blurRadius,
            Color tintColor,
            double tintOpacity,
            double cornerRadius)
        {
            _bounds = bounds;
            _blurRadius = (float)blurRadius;
            _tintColor = new SKColor(tintColor.R, tintColor.G, tintColor.B, (byte)(tintColor.A * tintOpacity));
            _tintOpacity = (float)tintOpacity;
            _cornerRadius = (float)cornerRadius;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is SkiaBackdropBlurDrawOperation op &&
                   op._bounds == _bounds &&
                   Math.Abs(op._blurRadius - _blurRadius) < 0.01 &&
                   op._tintColor == _tintColor;
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

            // Save the current canvas state
            canvas.Save();

            // Create a rounded rect for clipping
            var rect = new SKRect(
                (float)_bounds.X,
                (float)_bounds.Y,
                (float)_bounds.Right,
                (float)_bounds.Bottom);

            var roundedRect = new SKRoundRect(rect, _cornerRadius);

            // Clip to rounded rect
            canvas.ClipRoundRect(roundedRect, SKClipOperation.Intersect, true);

            // Create blur image filter
            using var blurFilter = SKImageFilter.CreateBlur(_blurRadius, _blurRadius);

            // Create paint with blur filter
            using var blurPaint = new SKPaint
            {
                ImageFilter = blurFilter,
                IsAntialias = true
            };

            // Save layer with blur - this captures everything beneath and blurs it
            canvas.SaveLayer(blurPaint);

            // We need to "punch through" to see the background
            // The SaveLayer captured what was behind, now restore to apply blur
            canvas.Restore();

            // Draw tint overlay
            if (_tintOpacity > 0)
            {
                using var tintPaint = new SKPaint
                {
                    Color = _tintColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRoundRect(roundedRect, tintPaint);
            }

            // Draw subtle border
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 50),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            canvas.DrawRoundRect(roundedRect, borderPaint);

            // Restore canvas state
            canvas.Restore();
        }
    }

    /// <summary>
    /// Alternative approach: Renders a blurred snapshot of a source visual using SkiaSharp.
    /// </summary>
    public static class SkiaBlurHelper
    {
        /// <summary>
        /// Creates a blurred SKBitmap from a source bitmap.
        /// </summary>
        public static SKBitmap? CreateBlurredBitmap(SKBitmap source, float blurRadius)
        {
            if (source == null || source.Width <= 0 || source.Height <= 0)
                return null;

            var blurred = new SKBitmap(source.Width, source.Height);

            using var canvas = new SKCanvas(blurred);
            using var blurFilter = SKImageFilter.CreateBlur(blurRadius, blurRadius);
            using var paint = new SKPaint
            {
                ImageFilter = blurFilter,
                IsAntialias = true
            };

            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(source, 0, 0, paint);

            return blurred;
        }

        /// <summary>
        /// Applies blur directly to an SKCanvas at the specified rect.
        /// </summary>
        public static void DrawBlurredRect(
            SKCanvas canvas,
            SKRect rect,
            float blurRadius,
            SKColor tintColor,
            float cornerRadius = 0)
        {
            canvas.Save();

            // Clip to the rect
            if (cornerRadius > 0)
            {
                var roundedRect = new SKRoundRect(rect, cornerRadius);
                canvas.ClipRoundRect(roundedRect, SKClipOperation.Intersect, true);
            }
            else
            {
                canvas.ClipRect(rect);
            }

            // Create blur filter and apply
            using var blurFilter = SKImageFilter.CreateBlur(blurRadius, blurRadius);
            using var blurPaint = new SKPaint
            {
                ImageFilter = blurFilter
            };

            // SaveLayer captures the current canvas content and applies the filter
            var layerRect = rect;
            canvas.SaveLayer(new SKRect(layerRect.Left, layerRect.Top, layerRect.Right, layerRect.Bottom), blurPaint);
            canvas.Restore();

            // Draw tint overlay
            if (tintColor.Alpha > 0)
            {
                using var tintPaint = new SKPaint
                {
                    Color = tintColor,
                    Style = SKPaintStyle.Fill
                };

                if (cornerRadius > 0)
                {
                    canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, tintPaint);
                }
                else
                {
                    canvas.DrawRect(rect, tintPaint);
                }
            }

            canvas.Restore();
        }
    }
}
