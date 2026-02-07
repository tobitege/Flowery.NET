namespace Flowery.Enums
{
    /// <summary>
    /// Defines how the badge aligns to the corner/edge.
    /// </summary>
    public enum BadgeAlignment
    {
        /// <summary>
        /// Badge sits fully inside the content bounds at the corner.
        /// Good for product cards with "NEW", "SALE" labels.
        /// </summary>
        Inside,

        /// <summary>
        /// Badge straddles the edge (half inside, half outside).
        /// Good for notification counts and status indicators.
        /// </summary>
        Edge,

        /// <summary>
        /// Badge sits mostly outside, just touching the corner.
        /// Good for floating action indicators.
        /// </summary>
        Outside
    }
}
