using Avalonia.Media;

namespace Flowery.Controls
{
    /// <summary>
    /// Draws the live backdrop of a <see cref="DaisyGlass"/> whose blur mode is <see cref="GlassBlurMode.SkiaSharp"/>.
    /// Flowery.NET itself has no renderer, so such a glass uses <see cref="GlassBlurMode.BitmapCapture"/>; the
    /// Flowery.NET.Skia package registers one with <c>FlowerySkia.Register()</c>.
    /// </summary>
    public abstract class DaisyGlassLiveRenderer
    {
        /// <summary>
        /// Gets the registered renderer, or <c>null</c> when none is registered.
        /// </summary>
        public static DaisyGlassLiveRenderer? Current { get; private set; }

        /// <summary>
        /// Registers the renderer for every SkiaSharp glass. Register it at startup, before the first glass is created;
        /// a glass created earlier keeps the blur mode it started with.
        /// </summary>
        public static void Register(DaisyGlassLiveRenderer renderer)
        {
            Current = renderer;
        }

        /// <summary>
        /// Draws the backdrop of <paramref name="glass"/> into <paramref name="context"/>, below its content and layers.
        /// </summary>
        public abstract void Render(DaisyGlass glass, DrawingContext context);

        /// <summary>
        /// Reports the average color behind <paramref name="glass"/>, measured by the renderer while
        /// <see cref="DaisyGlass.MeasureBackdropColor"/> is on, into <see cref="DaisyGlass.BackdropColor"/>.
        /// Call it on the UI thread.
        /// </summary>
        protected static void ReportBackdropColor(DaisyGlass glass, Color color) => glass.ReportBackdropColor(color);
    }
}
