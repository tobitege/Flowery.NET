using System;
using Avalonia;
using Avalonia.Automation.Peers;
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
        Bounce,
        /// <summary>Matrix animation - colon-dotted pattern with wave moving left to right</summary>
        Matrix,
        /// <summary>MatrixInward animation - both groups fade from inner to outer dots</summary>
        MatrixInward,
        /// <summary>MatrixOutward animation - both groups fade from outer to inner dots</summary>
        MatrixOutward,
        /// <summary>MatrixVertical animation - wave moves top to bottom across all dots</summary>
        MatrixVertical,
        /// <summary>MatrixRain animation - digital rain of dots falling down</summary>
        MatrixRain,
        /// <summary>Hourglass animation - classic hourglass with flowing sand</summary>
        Hourglass,
        /// <summary>SignalSweep animation - oscilloscope bar scanning left to right</summary>
        SignalSweep,
        /// <summary>BitFlip animation - dots flip like binary on/off states</summary>
        BitFlip,
        /// <summary>PacketBurst animation - dot shoots to edges and returns to center</summary>
        PacketBurst,
        /// <summary>CometTrail animation - bright dot with fading tail in a loop</summary>
        CometTrail,
        /// <summary>Heartbeat animation - EKG-style pulse line</summary>
        Heartbeat,
        /// <summary>TunnelZoom animation - concentric rings expanding outward</summary>
        TunnelZoom,
        /// <summary>GlitchReveal animation - random columns flash like terminal glitch</summary>
        GlitchReveal,
        /// <summary>RippleMatrix animation - brightness ripples outward from center</summary>
        RippleMatrix,
        /// <summary>CursorBlink animation - classic CLI cursor that moves and blinks</summary>
        CursorBlink,
        /// <summary>CountdownSpinner animation - 12 dots toggle like clock ticking</summary>
        CountdownSpinner
    }

    /// <summary>
    /// A Loading control styled after DaisyUI's Loading component.
    /// Shows an animation to indicate that something is loading.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// </summary>
    public class DaisyLoading : TemplatedControl
    {
        private const string DefaultAccessibleText = "Loading";

        protected override Type StyleKeyOverride => typeof(DaisyLoading);

        static DaisyLoading()
        {
            DaisyAccessibility.SetupAccessibility<DaisyLoading>(DefaultAccessibleText);
        }

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
        public static readonly StyledProperty<DaisyColor> ColorProperty =
            AvaloniaProperty.Register<DaisyLoading, DaisyColor>(nameof(Color), DaisyColor.Default);

        /// <summary>
        /// Gets or sets the color variant (Default, Primary, Secondary, Accent, etc.).
        /// </summary>
        public DaisyColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// Default is "Loading". Set to a more specific message like "Loading data" or "Please wait".
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyLoadingAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyLoading that exposes it as a ProgressBar to assistive technologies.
    /// </summary>
    internal class DaisyLoadingAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Loading";

        public DaisyLoadingAutomationPeer(DaisyLoading owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ProgressBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyLoading";
        }

        protected override string? GetNameCore()
        {
            var loading = (DaisyLoading)Owner;
            return DaisyAccessibility.GetEffectiveAccessibleText(loading, DefaultAccessibleText);
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
