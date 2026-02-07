namespace Flowery.Enums
{
    /// <summary>
    /// Specifies how card patterns are rendered.
    /// </summary>
    public enum FloweryPatternMode
    {
        /// <summary>
        /// Use seamless tile geometry that repeats.
        /// Default mode - efficient runtime geometry rendering.
        /// </summary>
        Tiled,

        /// <summary>
        /// Load pattern from pre-built PNG asset files (generated from SVG).
        /// Experimental - monochrome patterns with fixed size.
        /// </summary>
        SvgAsset
    }
}
