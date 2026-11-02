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
        /// Draws the same generated tiles as <see cref="Tiled"/>; there are no pattern asset files.
        /// Kept for compatibility.
        /// </summary>
        SvgAsset
    }
}
