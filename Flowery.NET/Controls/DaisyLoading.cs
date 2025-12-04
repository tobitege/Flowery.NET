using System;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Flowery.Controls
{
    /// <summary>
    /// Loading animation variant styles.
    /// </summary>
    public enum DaisyLoadingVariant
    {
        /// <summary>Spinner animation (default) - rotating arc</summary>
        Spinner,
        /// <summary>Dots animation - three bouncing dots</summary>
        Dots,
        /// <summary>Ring animation - rotating ring</summary>
        Ring,
        /// <summary>Ball animation - bouncing ball</summary>
        Ball,
        /// <summary>Bars animation - three animated bars</summary>
        Bars,
        /// <summary>Infinity animation - infinity symbol path</summary>
        Infinity,
        /// <summary>Orbit animation - dots orbiting around a square (terminal-style)</summary>
        Orbit,
        /// <summary>Snake animation - centipede-like segments moving back and forth</summary>
        Snake,
        /// <summary>Pulse animation - breathing/pulsing effect</summary>
        Pulse,
        /// <summary>Wave animation - multiple elements creating a wave</summary>
        Wave,
        /// <summary>Bounce animation - bouncing squares</summary>
        Bounce
    }

    /// <summary>
    /// Loading color variants matching DaisyUI theme colors.
    /// </summary>
    public enum DaisyLoadingColor
    {
        /// <summary>Default color (base content)</summary>
        Default,
        /// <summary>Primary theme color</summary>
        Primary,
        /// <summary>Secondary theme color</summary>
        Secondary,
        /// <summary>Accent theme color</summary>
        Accent,
        /// <summary>Neutral theme color</summary>
        Neutral,
        /// <summary>Info theme color</summary>
        Info,
        /// <summary>Success theme color</summary>
        Success,
        /// <summary>Warning theme color</summary>
        Warning,
        /// <summary>Error theme color</summary>
        Error
    }

    /// <summary>
    /// A Loading control styled after DaisyUI's Loading component.
    /// Shows an animation to indicate that something is loading.
    /// </summary>
    public class DaisyLoading : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyLoading);

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyLoadingVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisyLoadingVariant>(nameof(Variant), DaisyLoadingVariant.Spinner);

        /// <summary>
        /// Gets or sets the loading animation variant (Spinner, Dots, Ring, Ball, Bars, Infinity).
        /// </summary>
        public DaisyLoadingVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the loading indicator (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Color"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyLoadingColor> ColorProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisyLoadingColor>(nameof(Color), DaisyLoadingColor.Default);

        /// <summary>
        /// Gets or sets the color variant (Default, Primary, Secondary, Accent, etc.).
        /// </summary>
        public DaisyLoadingColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }
    }
}
