using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// An animated glass background drawn with moving gradients. It needs no SkiaSharp and works with every blur mode;
    /// <c>DaisyGlassShaderBackground</c> in Flowery.NET.Skia draws the same effects with shaders.
    /// </summary>
    public class DaisyGlassGradientBackground : DaisyGlassBackground
    {
        /// <inheritdoc/>
        public override DaisyGlassAnimationLayer CreateLayer() => new GradientLayer(this);

        private sealed class GradientLayer(DaisyGlassBackground source) : DaisyGlassAnimationLayer(source)
        {
            public override void Render(DrawingContext context)
            {
                var size = Bounds.Size;
                if (size.Width <= 0 || size.Height <= 0)
                    return;
                var area = new Rect(size);
                var t = Time;
                switch (Source.Effect)
                {
                    case DaisyGlassAnimation.Aurora:
                        DrawAurora(context, area, t);
                        break;
                    case DaisyGlassAnimation.LiquidGold:
                        DrawLiquidGold(context, area, t);
                        break;
                    default:
                        DrawPlasma(context, area, t);
                        break;
                }
            }

            // Two hue-cycling linear gradients at slowly turning angles, with three colored blobs drifting on Lissajous paths.
            private static void DrawPlasma(DrawingContext context, Rect area, double t)
            {
                context.DrawRectangle(Linear(t * 18, (Hsv(t * 25, 0.75, 0.95), 0), (Hsv(t * 25 + 120, 0.75, 0.9), 0.5),
                    (Hsv(t * 25 + 240, 0.75, 0.95), 1)), null, area);
                context.DrawRectangle(Linear(-t * 11 + 90, (Hsv(t * 40 + 60, 0.8, 1, 0.55), 0), (Color.FromArgb(0, 0, 0, 0), 0.5),
                    (Hsv(t * 40 + 200, 0.8, 1, 0.55), 1)), null, area);
                Blob(context, area, 0.5 + 0.4 * Math.Sin(t * 0.9), 0.5 + 0.4 * Math.Cos(t * 1.3), 0.55, Color.FromArgb(200, 236, 72, 153));
                Blob(context, area, 0.5 + 0.4 * Math.Cos(t * 0.7 + 1), 0.5 + 0.4 * Math.Sin(t * 1.1 + 2), 0.5, Color.FromArgb(190, 34, 211, 238));
                Blob(context, area, 0.5 + 0.4 * Math.Sin(t * 1.2 + 3), 0.5 + 0.4 * Math.Cos(t * 0.8 + 1), 0.45, Color.FromArgb(170, 251, 146, 60));
            }

            // A night sky with three stretched light curtains that drift sideways, sway and pulse.
            private static void DrawAurora(DrawingContext context, Rect area, double t)
            {
                context.DrawRectangle(Linear(90, (Color.FromRgb(5, 12, 31), 0), (Color.FromRgb(8, 31, 46), 1)), null, area);
                Curtain(context, area, t, 0.0, 0.42, Color.FromArgb(230, 52, 211, 153));
                Curtain(context, area, t, 2.1, 0.58, Color.FromArgb(200, 34, 211, 238));
                Curtain(context, area, t, 4.2, 0.30, Color.FromArgb(180, 167, 139, 250));
            }

            // A gold gradient whose stops roll, a bright sheen band sweeping across, and soft glints.
            private static void DrawLiquidGold(DrawingContext context, Rect area, double t)
            {
                var roll = (Math.Sin(t * 0.6) + 1) * 0.15;
                context.DrawRectangle(Linear(25 + 10 * Math.Sin(t * 0.4),
                    (Color.FromRgb(92, 58, 4), 0), (Color.FromRgb(184, 134, 11), 0.25 + roll), (Color.FromRgb(255, 229, 138), 0.5 + roll),
                    (Color.FromRgb(212, 160, 23), 0.75 + roll * 0.5), (Color.FromRgb(122, 82, 0), 1)), null, area);
                var sweep = (t * 0.25) % 1.6 - 0.3;
                context.DrawRectangle(Linear(30, (Color.FromArgb(0, 255, 248, 220), Math.Clamp(sweep - 0.15, 0, 1)),
                    (Color.FromArgb(170, 255, 248, 220), Math.Clamp(sweep, 0, 1)),
                    (Color.FromArgb(0, 255, 248, 220), Math.Clamp(sweep + 0.15, 0, 1))), null, area);
                Blob(context, area, 0.3 + 0.2 * Math.Sin(t * 0.8), 0.4 + 0.2 * Math.Cos(t * 1.1), 0.25, Color.FromArgb(150, 255, 240, 180));
                Blob(context, area, 0.7 + 0.2 * Math.Cos(t * 0.6 + 2), 0.6 + 0.2 * Math.Sin(t * 0.9), 0.2, Color.FromArgb(120, 255, 215, 0));
            }

            private static void Curtain(DrawingContext context, Rect area, double t, double phase, double height, Color color)
            {
                var x = 0.5 + 0.35 * Math.Sin(t * 0.35 + phase);
                var y = height + 0.08 * Math.Sin(t * 0.9 + phase * 1.7);
                var pulse = 0.7 + 0.3 * Math.Sin(t * 1.3 + phase);
                var brush = new RadialGradientBrush
                {
                    Center = new RelativePoint(x, y, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(x, y, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.22, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb((byte)(color.A * pulse), color.R, color.G, color.B), 0),
                        new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                    }
                };
                context.DrawRectangle(brush.ToImmutable(), null, area);
            }

            private static void Blob(DrawingContext context, Rect area, double x, double y, double radius, Color color)
            {
                var brush = new RadialGradientBrush
                {
                    Center = new RelativePoint(x, y, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(x, y, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(radius, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(radius, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(color, 0),
                        new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                    }
                };
                context.DrawRectangle(brush.ToImmutable(), null, area);
            }

            // The angle runs clockwise from the left edge, so 0 is left to right and 90 is top to bottom. The brushes of a
            // frame are immutable: a mutable brush would become a compositor resource, created and freed every frame.
            private static ImmutableLinearGradientBrush Linear(double angle, params (Color Color, double Offset)[] stops)
            {
                var radians = angle * Math.PI / 180;
                var dx = Math.Cos(radians) / 2;
                var dy = Math.Sin(radians) / 2;
                var gradient = new ImmutableGradientStop[stops.Length];
                for (var i = 0; i < stops.Length; i++)
                    gradient[i] = new ImmutableGradientStop(stops[i].Offset, stops[i].Color);
                return new ImmutableLinearGradientBrush(gradient, 1, null, null, GradientSpreadMethod.Pad,
                    new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative), new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative));
            }

            private static Color Hsv(double hue, double saturation, double value, double alpha = 1)
            {
                hue = ((hue % 360) + 360) % 360;
                var c = value * saturation;
                var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
                var m = value - c;
                var (r, g, b) = (int)(hue / 60) switch
                {
                    0 => (c, x, 0.0),
                    1 => (x, c, 0.0),
                    2 => (0.0, c, x),
                    3 => (0.0, x, c),
                    4 => (x, 0.0, c),
                    _ => (c, 0.0, x)
                };
                return Color.FromArgb((byte)(alpha * 255), (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
            }
        }
    }
}
