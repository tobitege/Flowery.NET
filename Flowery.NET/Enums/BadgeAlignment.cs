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
        /// Badge sits fully outside the content bounds; its bounding box touches the
        /// content only at the corner point, so a round badge shows a small gap.
        /// Good for floating action indicators.
        /// </summary>
        Outside
    }
}
