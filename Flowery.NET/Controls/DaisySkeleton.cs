using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Flowery.Localization;

namespace Flowery.Controls
{
    /// <summary>
    /// A Skeleton control styled after DaisyUI's Skeleton component.
    /// Used to show a loading state placeholder.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// </summary>
    public class DaisySkeleton : ContentControl
    {
        private const string DefaultAccessibleText = "Loading placeholder";

        protected override Type StyleKeyOverride => typeof(DaisySkeleton);

        static DaisySkeleton()
        {
            DaisyAccessibility.SetupAccessibility<DaisySkeleton>(DefaultAccessibleText);
        }

        /// <summary>
        /// Defines the <see cref="IsTextMode"/> property.
        /// When true, animates text color instead of background (skeleton-text).
        /// </summary>
        public static readonly StyledProperty<bool> IsTextModeProperty =
            AvaloniaProperty.Register<DaisySkeleton, bool>(nameof(IsTextMode), false);

        /// <summary>
        /// Gets or sets whether to use text animation mode.
        /// When true, animates the foreground/text color with a gradient effect instead of background opacity.
        /// </summary>
        public bool IsTextMode
        {
            get => GetValue(IsTextModeProperty);
            set => SetValue(IsTextModeProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// Default is "Loading placeholder".
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisySkeletonAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisySkeleton that exposes it as a progress indicator to assistive technologies.
    /// </summary>
    internal class DaisySkeletonAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Loading placeholder";

        public DaisySkeletonAutomationPeer(DaisySkeleton owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ProgressBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisySkeleton";
        }

        protected override string? GetNameCore()
        {
            var skeleton = (DaisySkeleton)Owner;
            var localizedDefault = FloweryLocalization.GetStringInternal("Accessibility_LoadingPlaceholder");
            return DaisyAccessibility.GetEffectiveAccessibleText(skeleton, localizedDefault);
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
