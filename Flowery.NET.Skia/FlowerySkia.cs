using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Flowery.Controls;

namespace Flowery.Skia
{
    /// <summary>
    /// Registers the SkiaSharp effects of Flowery.NET. Call <see cref="Register"/> once at startup, or
    /// <c>UseFlowerySkia()</c> on the <see cref="AppBuilder"/>; a DaisyGlass with <c>BlurMode="SkiaSharp"</c> then
    /// blurs its live backdrop instead of falling back to BitmapCapture.
    /// </summary>
    public static class FlowerySkia
    {
        /// <summary>
        /// Registers the live glass renderer. Calling it again has no further effect.
        /// </summary>
        public static void Register()
        {
            if (DaisyGlassLiveRenderer.Current is not SkiaGlassRenderer)
                DaisyGlassLiveRenderer.Register(new SkiaGlassRenderer());
        }

        /// <summary>
        /// Registers the SkiaSharp effects while the application is set up.
        /// </summary>
        public static AppBuilder UseFlowerySkia(this AppBuilder builder)
        {
            Register();
            return builder;
        }
    }

    /// <summary>
    /// Draws the live backdrop of every SkiaSharp glass with a <see cref="SkiaGlassDrawOperation"/>.
    /// </summary>
    internal sealed class SkiaGlassRenderer : DaisyGlassLiveRenderer
    {
        // One redraw controller per glass; it lives as long as the glass.
        private readonly ConditionalWeakTable<DaisyGlass, SkiaGlassRedraw> _redraws = new();

        public override void Render(DaisyGlass glass, DrawingContext context)
        {
            var redraw = _redraws.GetValue(glass, static owner => new SkiaGlassRedraw(owner));
            redraw.MeasureBackdrop = glass.MeasureBackdropColor;
            redraw.Register();
            context.Custom(new SkiaGlassDrawOperation(
                redraw,
                new Rect(0, 0, glass.Bounds.Width, glass.Bounds.Height),
                glass.GlassBlur,
                glass.GlassTint,
                glass.GlassTintOpacity,
                glass.CornerRadius.TopLeft,
                glass.GlassSaturation,
                glass.GlassReflectDegree,
                glass.GlassReflectOpacity,
                glass.GlassDepth,
                glass.GlassCurvature,
                glass.GlassBend,
                glass.GlassDispersion));
        }

        /// <summary>
        /// Hands a backdrop color measured on the render thread to its glass on the UI thread; returns whether the glass
        /// took it as a new backdrop color.
        /// </summary>
        internal static bool ReportMeasuredColor(Visual owner, Color color)
        {
            if (owner is not DaisyGlass glass)
                return false;
            var before = glass.BackdropColor;
            ReportBackdropColor(glass, color);
            return glass.BackdropColor != before;
        }
    }
}
