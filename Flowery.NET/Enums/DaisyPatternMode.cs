namespace Flowery.Enums
{
    /// <summary>
    /// Specifies how card patterns are rendered.
    /// </summary>
    public enum FloweryPatternMode
    {
        /// <summary>
        /// Use seamless tile geometry that repeats, generated at run time in the brush of the card.
        /// </summary>
        Tiled,

        /// <summary>
        /// Tile the SVG pattern assets of the library, drawn with Svg.Skia: white patterns on dark themes, black ones on
        /// light themes. The default. A build of Flowery.NET with <c>FlowerySvgPatterns=false</c> has no assets and draws
        /// the generated tiles of <see cref="Tiled"/> instead.
        /// </summary>
        SvgAsset
    }
}
