using System;
using Avalonia.Controls;

namespace Flowery.Controls
{
    /// <summary>
    /// Describes one surface of a control that draws the neumorphic effect.
    /// Register it with <see cref="DaisyNeumorphic.RegisterSurface{TControl}(DaisyNeumorphicSurface{TControl})"/>.
    /// </summary>
    /// <typeparam name="TControl">The control type that owns the surface.</typeparam>
    public sealed class DaisyNeumorphicSurface<TControl> where TControl : Control
    {
        /// <summary>
        /// Returns the surface border, or null while it does not exist yet. A missing surface is retried
        /// once after the next layout pass.
        /// </summary>
        public required Func<TControl, Border?> Resolve { get; init; }

        /// <summary>
        /// Optional unclipped border behind a surface that clips its content (ClipToBounds clips a border's
        /// own outer shadow). Raised and Flat shadows go to this layer; Inset shadows stay on the surface.
        /// </summary>
        public Func<TControl, Border?>? OuterShadowLayer { get; init; }

        /// <summary>
        /// Optional border on top of the surface content for Inset shadows, for surfaces whose content paints
        /// opaque backgrounds that would hide inner shadows drawn on the surface itself.
        /// </summary>
        public Func<TControl, Border?>? InsetOverlay { get; init; }

        /// <summary>
        /// A mode this surface always uses instead of the control's effective mode.
        /// </summary>
        public DaisyNeumorphicMode? FixedMode { get; init; }

        /// <summary>
        /// Optional condition on the control; the surface draws nothing while it returns false
        /// (e.g. transparent button variants). Register the properties it reads with
        /// <see cref="DaisyNeumorphic.RegisterRefreshTriggers{TControl}"/>.
        /// </summary>
        public Func<TControl, bool>? IsActive { get; init; }

        /// <summary>
        /// When true, the surface only draws while its border paints a visible background.
        /// Background changes of the border re-apply the effect.
        /// </summary>
        public bool RequiresBackground { get; init; }
    }
}
