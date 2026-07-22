using System.Collections.Generic;

namespace Flowery.Theming
{
    /// <summary>
    /// Parsed theme data from a DaisyUI CSS file.
    /// Contains all extracted colors and design tokens.
    /// </summary>
    public class DaisyUiTheme
    {
        /// <summary>
        /// Theme name (e.g., "corporate", "dracula", "synthwave").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is a dark theme (based on color-scheme CSS property).
        /// </summary>
        public bool IsDark { get; set; }

        /// <summary>
        /// Color values keyed by DaisyUI CSS variable name (e.g., "color-primary" -> "#570df8").
        /// All values are converted to hex format.
        /// </summary>
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Border radius values keyed by DaisyUI CSS variable name (e.g., "radius-btn" -> "0.5rem").
        /// </summary>
        public Dictionary<string, string> Radii { get; set; } = new Dictionary<string, string>();
    }
}
