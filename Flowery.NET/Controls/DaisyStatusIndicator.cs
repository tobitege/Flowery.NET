using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;

namespace Flowery.Controls
{
    /// <summary>
    /// Animation variant styles for the status indicator.
    /// </summary>
    public enum DaisyStatusIndicatorVariant
    {
        /// <summary>Static dot with no animation (default)</summary>
        Default,
        /// <summary>Ping animation - expanding ring that fades out</summary>
        Ping,
        /// <summary>Bounce animation - dot bounces up and down</summary>
        Bounce,
        /// <summary>Pulse animation - breathing/pulsing opacity effect</summary>
        Pulse,
        /// <summary>Blink animation - simple on/off blinking</summary>
        Blink,
        /// <summary>Ripple animation - multiple expanding rings</summary>
        Ripple,
        /// <summary>Heartbeat animation - double-pulse like a heartbeat</summary>
        Heartbeat,
        /// <summary>Spin animation - rotating dot indicator</summary>
        Spin,
        /// <summary>Wave animation - wave-like scale effect</summary>
        Wave,
        /// <summary>Glow animation - glowing halo effect</summary>
        Glow,
        /// <summary>Morph animation - shape morphing effect</summary>
        Morph,
        /// <summary>Orbit animation - small dot orbiting around</summary>
        Orbit,
        /// <summary>Radar animation - radar sweep effect</summary>
        Radar,
        /// <summary>Sonar animation - sonar ping effect</summary>
        Sonar,
        /// <summary>Beacon animation - lighthouse beacon sweep</summary>
        Beacon,
        /// <summary>Shake animation - horizontal shake effect</summary>
        Shake,
        /// <summary>Wobble animation - wobbling rotation effect</summary>
        Wobble,
        /// <summary>Pop animation - pop in/out scale effect</summary>
        Pop,
        /// <summary>Flicker animation - random flickering effect</summary>
        Flicker,
        /// <summary>Breathe animation - slow breathing scale</summary>
        Breathe,
        /// <summary>Ring animation - expanding ring outline</summary>
        Ring,
        /// <summary>Flash animation - quick flash effect</summary>
        Flash,
        /// <summary>Swing animation - pendulum swing effect</summary>
        Swing,
        /// <summary>Jiggle animation - jiggling effect</summary>
        Jiggle,
        /// <summary>Throb animation - throbbing intensity effect</summary>
        Throb,
        /// <summary>Twinkle animation - star-like twinkling</summary>
        Twinkle,
        /// <summary>Splash animation - splash ripple effect</summary>
        Splash
    }

    /// <summary>
    /// A status indicator control that displays a small colored dot to represent status.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// </summary>
    public class DaisyStatusIndicator : TemplatedControl
    {
        private const string DefaultAccessibleText = "Status";

        protected override Type StyleKeyOverride => typeof(DaisyStatusIndicator);

        static DaisyStatusIndicator()
        {
            DaisyAccessibility.SetupAccessibility<DaisyStatusIndicator>(DefaultAccessibleText);

            ColorProperty.Changed.AddClassHandler<DaisyStatusIndicator>((control, _) =>
            {
                control.UpdateAccessibleNameFromColor();
            });
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyStatusIndicatorVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisyStatusIndicatorVariant>(nameof(Variant), DaisyStatusIndicatorVariant.Default);

        /// <summary>
        /// Gets or sets the animation variant (Default, Ping, Bounce, Pulse, Blink, etc.).
        /// </summary>
        public DaisyStatusIndicatorVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Color"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyColor> ColorProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisyColor>(nameof(Color), DaisyColor.Neutral);

        /// <summary>
        /// Gets or sets the color variant (Default, Primary, Secondary, Accent, etc.).
        /// </summary>
        public DaisyColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the status indicator (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// When null (default), the text is automatically derived from the Color property.
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        private void UpdateAccessibleNameFromColor()
        {
            if (DaisyAccessibility.GetAccessibleText(this) == null)
            {
                Avalonia.Automation.AutomationProperties.SetName(this, GetDefaultAccessibleText());
            }
        }

        internal string GetDefaultAccessibleText()
        {
            return Color switch
            {
                DaisyColor.Success => "Online",
                DaisyColor.Error => "Error",
                DaisyColor.Warning => "Warning",
                DaisyColor.Info => "Information",
                DaisyColor.Primary => "Active",
                DaisyColor.Secondary => "Secondary",
                DaisyColor.Accent => "Highlighted",
                DaisyColor.Neutral => DefaultAccessibleText,
                DaisyColor.Default => DefaultAccessibleText,
                _ => DefaultAccessibleText
            };
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyStatusIndicatorAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyStatusIndicator that exposes it as a status element to assistive technologies.
    /// </summary>
    internal class DaisyStatusIndicatorAutomationPeer : ControlAutomationPeer
    {
        public DaisyStatusIndicatorAutomationPeer(DaisyStatusIndicator owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.StatusBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyStatusIndicator";
        }

        protected override string? GetNameCore()
        {
            var indicator = (DaisyStatusIndicator)Owner;
            return DaisyAccessibility.GetAccessibleText(indicator) ?? indicator.GetDefaultAccessibleText();
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
