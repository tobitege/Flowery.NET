using System;
using System.Runtime.InteropServices;
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
            Volatile.Write(ref _measureDue, 1);
            if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                _changeQueued = 0;
                _liveUntil = DateTime.UtcNow + LiveTime;
                RequestFrame();
            }, DispatcherPriority.Render);
        }

        // A measurement reads pixels back from the GPU, which stalls the frame; a few per second follow a change closely enough.
        private const long MeasureInterval = 300;

        private long _lastMeasure = long.MinValue / 2;
        private int _measureLaterQueued;
        private int _measureDue = 1;

        /// <summary>Set on the UI thread for each render: whether the glass wants its backdrop color measured.</summary>
        public volatile bool MeasureBackdrop;

        /// <summary>
        /// Called on the render thread with the whole backdrop at hand: returns whether to measure it now, at most every
        /// <see cref="MeasureInterval"/>. After the backdrop changed, a render too soon schedules a render for later, so
        /// the last state behind the glass is measured too; without a change nothing is scheduled, since a scheduled
        /// render redraws the glass, which could make a glass around it redraw this one again.
        /// </summary>
        public bool TryStartMeasure()
        {
            if (!MeasureBackdrop)
                return false;
            var now = Environment.TickCount64;
            var wait = Interlocked.Read(ref _lastMeasure) + MeasureInterval - now;
            if (wait > 0)
            {
                if (Volatile.Read(ref _measureDue) == 1)
                    MeasureLater(wait);
                return false;
            }
            Interlocked.Exchange(ref _lastMeasure, now);
            Volatile.Write(ref _measureDue, 0);
            return true;
        }

        /// <summary>Called on the render thread with a measured backdrop color.</summary>
        public void ReportBackdropColor(Color color) =>
            Dispatcher.UIThread.Post(() => SkiaGlassRenderer.ReportMeasuredColor(owner, color), DispatcherPriority.Background);

        private void MeasureLater(long delay)
        {
            if (Interlocked.Exchange(ref _measureLaterQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() => DispatcherTimer.RunOnce(() =>
            {
                _measureLaterQueued = 0;
                owner.InvalidateVisual();
            }, TimeSpan.FromMilliseconds(delay)));
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
        private SKRectI[]? _seenCells;
        private bool _redrawsAll;

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

            using var roundedRect = new SKRoundRect(rect, _cornerRadius);

            // Taken before the rounded clip below: the clip on entry bounds the area this frame redraws.
            var backdrop = lease.SkSurface is SKSurface surface ? GetBackdrop(surface, canvas, rect) : null;
            // Only a render of the whole glass has the bare backdrop under all of it; elsewhere a partial render still
            // shows the last frame's glass and text, and measuring those would change the text color for nothing.
            if (backdrop is { } measured && _redrawsAll && _redraw.TryStartMeasure()
                && MeasureBackdropColor(lease.GrContext, measured.Image, measured.Bounds, SKRectI.Ceiling(canvas.TotalMatrix.MapRect(rect))) is { } color)
                _redraw.ReportBackdropColor(color);

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
            using var rimRect = new SKRoundRect(rect, _cornerRadius);
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
                // A paint does not dispose its shader.
                using var rimShader = SKShader.CreateLinearGradient(
                    new SKPoint(rect.MidX - light.X * rect.Width * 0.5f, rect.MidY - light.Y * rect.Height * 0.5f),
                    new SKPoint(rect.MidX + light.X * rect.Width * 0.5f, rect.MidY + light.Y * rect.Height * 0.5f),
                    colors,
                    points,
                    SKShaderTileMode.Clamp);
                rimPaint.Shader = rimShader;

                canvas.DrawRoundRect(rimRect, rimPaint);
            }

            if (_curvature > 0.001f || _bend > 0.001f)
            {
                var glowCenter = new SKPoint(
                    rect.MidX - light.X * rect.Width * 0.22f,
                    rect.MidY - light.Y * rect.Height * 0.22f);
                var glowRadius = Math.Max(8f, minSide * (0.28f + _depth * 0.2f));
                using var glowShader = SKShader.CreateRadialGradient(
                    glowCenter,
                    glowRadius,
                    new[]
                    {
                        new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * (0.25f + _curvature * 0.25f))),
                        new SKColor(255, 255, 255, 0)
                    },
                    new[] { 0f, 1f },
                    SKShaderTileMode.Clamp);
                using var glowPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                    Shader = glowShader
                };

                canvas.DrawRoundRect(rimRect, glowPaint);
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
                _seenCells = ProbeSeenCells(canvas, rect);
            }

            // A partial render follows a change behind the glass. The blur spreads that change beyond the redrawn
            // area, where this frame cannot draw, so the glass redraws in full for a moment.
            _redrawsAll = first || CoversSeen(canvas, rect, _seen!.Value, _seenCells);
            if (!_redrawsAll)
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

        /// <summary>
        /// Returns the average color of the glass area in the backdrop snapshot, or <c>null</c> when nothing opaque is
        /// there. A GPU image cannot be read directly, so the area is drawn small into a surface on the same GPU
        /// context, or a raster surface without one, and those few pixels are read back.
        /// </summary>
        internal static Color? MeasureBackdropColor(GRContext? context, SKImage image, SKRectI imageBounds, SKRectI glass)
        {
            var area = glass;
            area.Intersect(imageBounds);
            if (area.IsEmpty)
                return null;

            var width = Math.Clamp(area.Width / 4, 1, 64);
            var height = Math.Clamp(area.Height / 4, 1, 16);
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var small = context != null ? SKSurface.Create(context, false, info) : SKSurface.Create(info);
            if (small == null)
                return null;
            small.Canvas.DrawImage(
                image,
                SKRect.Create(area.Left - imageBounds.Left, area.Top - imageBounds.Top, area.Width, area.Height),
                SKRect.Create(width, height),
                new SKSamplingOptions(SKFilterMode.Linear));

            var pixels = new byte[width * height * 4];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                if (!small.ReadPixels(info, handle.AddrOfPinnedObject(), width * 4, 0, 0))
                    return null;
            }
            finally
            {
                handle.Free();
            }

            // Premultiplied channels summed and divided by the summed alpha give the alpha-weighted average.
            long red = 0, green = 0, blue = 0, alpha = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                red += pixels[i];
                green += pixels[i + 1];
                blue += pixels[i + 2];
                alpha += pixels[i + 3];
            }
            if (alpha == 0)
                return null;
            return Color.FromRgb(
                (byte)Math.Min(255, red * 255 / alpha), (byte)Math.Min(255, green * 255 / alpha), (byte)Math.Min(255, blue * 255 / alpha));
        }

        /// <summary>
        /// Returns the device bounds of the clip in each cell of the glass, when the clip is not a rectangle, such as
        /// the rounded clip of an ancestor; for a rectangular clip the visible area is a rectangle and needs no cells.
        /// </summary>
        internal static SKRectI[]? ProbeSeenCells(SKCanvas canvas, SKRect rect) =>
            canvas.IsClipRect ? null : ProbeCells(canvas, Cells(rect));

        /// <summary>
        /// Returns whether the clip of this render covers all of the glass that the first render could see.
        /// The bounds of the clip are not enough when it is a region, as with
        /// <c>CompositionOptions.UseRegionDirtyRectClipping</c>: two redrawn areas apart from each other can span the
        /// glass while the part between them is not redrawn. The glass is then checked cell by cell.
        /// </summary>
        internal static bool CoversSeen(SKCanvas canvas, SKRect rect, SKRectI seen, SKRectI[]? seenCells)
        {
            var bounds = canvas.DeviceClipBounds;
            bounds.Inflate(1, 1);
            if (!bounds.Contains(seen))
                return false;
            if (canvas.IsClipRect)
                return true;

            var cells = Cells(rect);
            var probed = ProbeCells(canvas, cells);
            for (var i = 0; i < cells.Length; i++)
            {
                SKRectI expected;
                if (seenCells != null)
                {
                    expected = seenCells[i];
                }
                else
                {
                    expected = SKRectI.Ceiling(canvas.TotalMatrix.MapRect(cells[i]));
                    expected.Intersect(seen);
                }
                if (expected.IsEmpty)
                    continue;
                var covered = probed[i];
                covered.Inflate(1, 1);
                if (!covered.Contains(expected))
                    return false;
            }
            return true;
        }

        // A cell narrower than the gap between two redrawn areas shows the gap in its clip bounds.
        private const float CellSize = 16f;

        private static SKRect[] Cells(SKRect rect)
        {
            var columns = Math.Max(1, (int)MathF.Ceiling(rect.Width / CellSize));
            var rows = Math.Max(1, (int)MathF.Ceiling(rect.Height / CellSize));
            var cells = new SKRect[columns * rows];
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    cells[row * columns + column] = new SKRect(
                        rect.Left + rect.Width * column / columns,
                        rect.Top + rect.Height * row / rows,
                        rect.Left + rect.Width * (column + 1) / columns,
                        rect.Top + rect.Height * (row + 1) / rows);
                }
            }
            return cells;
        }

        private static SKRectI[] ProbeCells(SKCanvas canvas, SKRect[] cells)
        {
            var bounds = new SKRectI[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                canvas.Save();
                canvas.ClipRect(cells[i]);
                bounds[i] = canvas.DeviceClipBounds;
                canvas.Restore();
            }
            return bounds;
        }

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

        private static byte ToAlpha(float opacity) => (byte)Math.Clamp(opacity * 255f, 0f, 255f);
    }
}
