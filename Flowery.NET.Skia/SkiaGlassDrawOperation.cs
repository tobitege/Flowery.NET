using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace Flowery.Skia
{
    /// <summary>
    /// Keeps a SkiaSharp glass redrawing in full for a moment after its backdrop changed.
    /// A frame redraws only the changed areas, but the blur spreads each change a few pixels further, so a partial
    /// redraw would leave the old blur at the edges of the changed area. While the backdrop keeps changing,
    /// the whole glass is redrawn every frame; afterwards it returns to partial redraws.
    /// </summary>
    internal sealed class SkiaGlassRedraw(Visual owner)
    {
        private static readonly TimeSpan LiveTime = TimeSpan.FromMilliseconds(300);

        private int _changeQueued;
        private DateTime _liveUntil;
        private bool _frameRequested;

        /// <summary>Incremented on the UI thread for each full redraw, so the new draw operation differs from the last.</summary>
        public int Version { get; private set; }

        /// <summary>Called on the render thread when a frame redrew only part of the glass.</summary>
        public void BackdropChanged()
        {
            if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                _changeQueued = 0;
                _liveUntil = DateTime.UtcNow + LiveTime;
                RequestFrame();
            }, DispatcherPriority.Render);
        }

        private void RequestFrame()
        {
            if (_frameRequested || TopLevel.GetTopLevel(owner) is not { } topLevel)
                return;
            _frameRequested = true;
            topLevel.RequestAnimationFrame(_ =>
            {
                _frameRequested = false;
                if (DateTime.UtcNow > _liveUntil)
                    return;
                Version++;
                owner.InvalidateVisual();
                RequestFrame();
            });
        }
    }

    /// <summary>
    /// Custom SkiaSharp draw operation for real-time glass blur effect.
    /// </summary>
    internal sealed class SkiaGlassDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly float _blurSigma;
        private readonly SKColor _tintColor;
        private readonly float _cornerRadius;
        private readonly float _saturation;
        private readonly float _sheenAngle;
        private readonly float _reflectOpacity;
        private readonly float _depth;
        private readonly float _curvature;
        private readonly float _bend;
        private readonly float _dispersion;
        private readonly SkiaGlassRedraw _redraw;
        private readonly int _redrawVersion;
        private SKRectI? _seen;

        public SkiaGlassDrawOperation(
            SkiaGlassRedraw redraw,
            Rect bounds,
            double blurRadius,
            Color tintColor,
            double tintOpacity,
            double cornerRadius,
            double saturation,
            double sheenAngle,
            double reflectOpacity,
            double depth,
            double curvature,
            double bend,
            double dispersion)
        {
            _redraw = redraw;
            _redrawVersion = redraw.Version;
            _bounds = bounds;
            _blurSigma = (float)(blurRadius / 10.0);
            _tintColor = new SKColor(
                tintColor.R,
                tintColor.G,
                tintColor.B,
                ToAlpha(Clamp01((float)tintOpacity)));
            _cornerRadius = (float)cornerRadius;
            _saturation = Math.Max(0, (float)saturation);
            _sheenAngle = (float)sheenAngle;
            _reflectOpacity = Clamp01((float)reflectOpacity);
            _depth = Clamp01((float)depth);
            _curvature = Clamp01((float)curvature);
            _bend = Clamp01((float)bend);
            _dispersion = Clamp01((float)dispersion);
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            // A full redraw must differ from the last operation, or the renderer would keep the old one.
            return other is SkiaGlassDrawOperation op &&
                   op._redrawVersion == _redrawVersion &&
                   op._bounds == _bounds &&
                   op._tintColor == _tintColor &&
                   op._cornerRadius == _cornerRadius &&
                   op._sheenAngle == _sheenAngle &&
                   Math.Abs(op._blurSigma - _blurSigma) < 0.1f &&
                   Math.Abs(op._saturation - _saturation) < 0.01f &&
                   Math.Abs(op._reflectOpacity - _reflectOpacity) < 0.01f &&
                   Math.Abs(op._depth - _depth) < 0.01f &&
                   Math.Abs(op._curvature - _curvature) < 0.01f &&
                   Math.Abs(op._bend - _bend) < 0.01f &&
                   Math.Abs(op._dispersion - _dispersion) < 0.01f;
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

            // Taken before the rounded clip below: the clip on entry bounds the area this frame redraws.
            var backdrop = lease.SkSurface is SKSurface surface ? GetBackdrop(surface, canvas, rect) : null;

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
            if (backdrop is { } snapshot)
            {
                using var paint = new SKPaint
                {
                    ImageFilter = backdropFilter,
                    IsAntialias = true
                };

                // The backdrop is in device pixels; draw it with the identity matrix so it aligns with the surface.
                // The rounded clip is kept in device space, so it masks correctly.
                canvas.Save();
                canvas.ResetMatrix();
                canvas.DrawImage(snapshot.Image, snapshot.Bounds.Left, snapshot.Bounds.Top, paint);
                canvas.Restore();
                snapshot.Image.Dispose();
            }

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

            DrawLiquidOptics(canvas, rect);

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

        private void DrawLiquidOptics(SKCanvas canvas, SKRect rect)
        {
            var minSide = Math.Min(rect.Width, rect.Height);
            if (minSide <= 0)
                return;

            var rimWidth = Math.Clamp(minSide * (0.035f + _depth * 0.055f + _bend * 0.04f), 2f, minSide * 0.2f);

            // The rim is stroked on the glass edge at twice its width and blurred; the rounded clip keeps the inner
            // half. It fades inward and follows the corner radius, with no inner edge that would read as a second,
            // squarer rectangle on small controls.
            var rimRect = new SKRoundRect(rect, _cornerRadius);
            using var rimBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, rimWidth * 0.5f);
            var angle = _sheenAngle * MathF.PI / 180f;
            var light = new SKPoint(MathF.Cos(angle), MathF.Sin(angle));
            var specularAlpha = ToAlpha(Math.Clamp(_reflectOpacity + _curvature * 0.12f, 0f, 0.75f));

            if (_dispersion > 0.001f)
            {
                var offset = Math.Clamp(_dispersion * 4f, 0.5f, 4f);
                DrawChromaticRim(canvas, rimRect, offset, rimWidth, rimBlur);
            }

            using (var rimPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = rimWidth * 2,
                MaskFilter = rimBlur,
                IsAntialias = true
            })
            {
                var colors = new[]
                {
                    new SKColor(255, 255, 255, specularAlpha),
                    new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * 0.28f)),
                    new SKColor(0, 0, 0, ToAlpha(_bend * 0.18f)),
                    new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * 0.36f))
                };
                var points = new[] { 0f, 0.38f, 0.72f, 1f };
                rimPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(rect.MidX - light.X * rect.Width * 0.5f, rect.MidY - light.Y * rect.Height * 0.5f),
                    new SKPoint(rect.MidX + light.X * rect.Width * 0.5f, rect.MidY + light.Y * rect.Height * 0.5f),
                    colors,
                    points,
                    SKShaderTileMode.Clamp);

                canvas.DrawRoundRect(rimRect, rimPaint);
            }

            if (_curvature > 0.001f || _bend > 0.001f)
            {
                var glowCenter = new SKPoint(
                    rect.MidX - light.X * rect.Width * 0.22f,
                    rect.MidY - light.Y * rect.Height * 0.22f);
                var glowRadius = Math.Max(8f, minSide * (0.28f + _depth * 0.2f));
                using var glowPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                    Shader = SKShader.CreateRadialGradient(
                        glowCenter,
                        glowRadius,
                        new[]
                        {
                            new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * (0.25f + _curvature * 0.25f))),
                            new SKColor(255, 255, 255, 0)
                        },
                        new[] { 0f, 1f },
                        SKShaderTileMode.Clamp)
                };

                canvas.DrawRoundRect(new SKRoundRect(rect, _cornerRadius), glowPaint);
            }
        }

        private static void DrawChromaticRim(SKCanvas canvas, SKRoundRect rimRect, float offset, float rimWidth, SKMaskFilter blur)
        {
            canvas.Save();
            canvas.Translate(-offset, 0);
            using (var redPaint = new SKPaint
            {
                Color = new SKColor(255, 80, 80, 34),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(2f, rimWidth * 0.7f),
                MaskFilter = blur,
                IsAntialias = true
            })
            {
                canvas.DrawRoundRect(rimRect, redPaint);
            }
            canvas.Restore();

            canvas.Save();
            canvas.Translate(offset, 0);
            using (var bluePaint = new SKPaint
            {
                Color = new SKColor(80, 150, 255, 34),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(2f, rimWidth * 0.7f),
                MaskFilter = blur,
                IsAntialias = true
            })
            {
                canvas.DrawRoundRect(rimRect, bluePaint);
            }
            canvas.Restore();
        }

        /// <summary>
        /// Returns the backdrop of the glass and its blur margin from the surface, in device pixels.
        /// The caller disposes the image after drawing it; no surface image outlives the frame.
        /// </summary>
        private (SKImage Image, SKRectI Bounds)? GetBackdrop(SKSurface surface, SKCanvas canvas, SKRect rect)
        {
            var glass = SKRectI.Ceiling(canvas.TotalMatrix.MapRect(rect));
            var clipBounds = canvas.DeviceClipBounds;

            // The first render of an operation follows an invalidation of the glass, which redraws all of it that can
            // be seen; a scroll viewer or another clipping ancestor may hide the rest, which is never redrawn.
            // A later render that misses part of that visible area redraws only part of the glass.
            var first = _seen == null;
            if (first)
            {
                var seen = glass;
                seen.Intersect(clipBounds);
                _seen = seen;
            }
            clipBounds.Inflate(1, 1);
            var partial = !clipBounds.Contains(_seen!.Value);

            // A partial render follows a change behind the glass. The blur spreads that change beyond the redrawn
            // area, where this frame cannot draw, so the glass redraws in full for a moment.
            if (!first && partial)
                _redraw.BackdropChanged();

            var margin = (int)Math.Ceiling(_blurSigma * 3) + 1;
            var sample = glass;
            sample.Inflate(margin, margin);
            sample = new SKRectI(Math.Max(0, sample.Left), Math.Max(0, sample.Top), sample.Right, sample.Bottom);
            if (sample.Width <= 0 || sample.Height <= 0 || surface.Snapshot(sample) is not { } fresh)
                return null;
            // The surface clips the snapshot at its right and bottom edges.
            sample = new SKRectI(sample.Left, sample.Top, sample.Left + fresh.Width, sample.Top + fresh.Height);

            return (fresh, sample);
        }

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

        private static byte ToAlpha(float opacity) => (byte)Math.Clamp(opacity * 255f, 0f, 255f);
    }
}
