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
        /// Registers the renderer for every SkiaSharp glass, or removes it with <c>null</c>. Register it at startup; a
        /// glass picks it up when it enters the visual tree, so one created earlier but shown later still uses it.
        /// </summary>
        public static void Register(DaisyGlassLiveRenderer? renderer)
        {
            Current = renderer;
        }

        /// <summary>
        /// Draws the backdrop of <paramref name="glass"/> into <paramref name="context"/>, below its content and layers.
        /// </summary>
        public abstract void Render(DaisyGlass glass, DrawingContext context);

        /// <summary>
        /// Creates a background that draws its effects with shaders, or returns <c>null</c> when the renderer has none.
        /// The renderer of Flowery.NET.Skia returns a <c>DaisyGlassShaderBackground</c>; <see cref="DaisyGlassButtonDesigner"/>
        /// offers shader backgrounds only while such a renderer is registered.
        /// </summary>
        public virtual DaisyGlassBackground? CreateShaderBackground() => null;

        /// <summary>
        /// Gets whether <see cref="CreateShaderBackground"/> creates a background. The renderer of Flowery.NET.Skia returns
        /// true.
        /// </summary>
        public virtual bool SupportsShaderBackground => false;

        /// <summary>
        /// Returns whether <paramref name="background"/> is a background <see cref="CreateShaderBackground"/> creates.
        /// </summary>
        public virtual bool IsShaderBackground(DaisyGlassBackground background) => false;

        /// <summary>
        /// Reports the average color behind <paramref name="glass"/>, measured by the renderer while
        /// <see cref="DaisyGlass.MeasureBackdropColor"/> is on, into <see cref="DaisyGlass.BackdropColor"/>.
        /// Call it on the UI thread.
        /// </summary>
        protected static void ReportBackdropColor(DaisyGlass glass, Color color) => glass.ReportBackdropColor(color);
    }
}
