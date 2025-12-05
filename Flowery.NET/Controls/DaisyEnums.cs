namespace Flowery.Controls
{
    /// <summary>
    /// Shared color variants matching DaisyUI theme colors.
    /// Used across multiple controls for consistent theming.
    /// </summary>
    public enum DaisyColor
    {
        /// <summary>Default color (typically base content or neutral)</summary>
        Default,
        /// <summary>Primary theme color</summary>
        Primary,
        /// <summary>Secondary theme color</summary>
        Secondary,
        /// <summary>Accent theme color</summary>
        Accent,
        /// <summary>Neutral theme color</summary>
        Neutral,
        /// <summary>Info theme color (informational messages)</summary>
        Info,
        /// <summary>Success theme color (positive/success states)</summary>
        Success,
        /// <summary>Warning theme color (caution/warning states)</summary>
        Warning,
        /// <summary>Error theme color (error/danger states)</summary>
        Error
    }

    /// <summary>
    /// Shared size variants for DaisyUI controls.
    /// </summary>
    public enum DaisySize
    {
        /// <summary>Extra small size</summary>
        ExtraSmall,
        /// <summary>Small size</summary>
        Small,
        /// <summary>Medium size (default)</summary>
        Medium,
        /// <summary>Large size</summary>
        Large,
        /// <summary>Extra large size</summary>
        ExtraLarge
    }

    /// <summary>
    /// Navigation arrow placement for DaisyStack.
    /// </summary>
    public enum DaisyStackNavigation
    {
        /// <summary>Left/Right arrows for horizontal navigation</summary>
        Horizontal,

        /// <summary>Up/Down arrows for vertical navigation</summary>
        Vertical
    }

    /// <summary>
    /// Placement options for UI elements like counters and labels.
    /// </summary>
    public enum DaisyPlacement
    {
        /// <summary>Place at the top</summary>
        Top,

        /// <summary>Place at the bottom</summary>
        Bottom,

        /// <summary>Place at the start (left in LTR)</summary>
        Start,

        /// <summary>Place at the end (right in LTR)</summary>
        End
    }
}
