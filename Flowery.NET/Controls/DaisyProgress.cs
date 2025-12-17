using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyProgressVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// A Progress bar control styled after DaisyUI's Progress component.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyProgress : ProgressBar, IScalableControl
    {
        private const string DefaultAccessibleText = "Progress";
        private const double BaseTextFontSize = 12.0;

        protected override Type StyleKeyOverride => typeof(DaisyProgress);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        static DaisyProgress()
        {
            DaisyAccessibility.SetupAccessibility<DaisyProgress>(DefaultAccessibleText);
        }

        public static readonly StyledProperty<DaisyProgressVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyProgress, DaisyProgressVariant>(nameof(Variant), DaisyProgressVariant.Default);

        public DaisyProgressVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyProgress, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// Default is "Progress". The current percentage is automatically appended.
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyProgressAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyProgress that exposes it as a ProgressBar to assistive technologies.
    /// </summary>
    internal class DaisyProgressAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Progress";

        public DaisyProgressAutomationPeer(DaisyProgress owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ProgressBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyProgress";
        }

        protected override string? GetNameCore()
        {
            var progress = (DaisyProgress)Owner;
            var localizedDefault = FloweryLocalization.GetStringInternal("Accessibility_Progress");
            var text = DaisyAccessibility.GetEffectiveAccessibleText(progress, localizedDefault);
            var range = progress.Maximum - progress.Minimum;
            if (range > 0)
            {
                var percent = (int)((progress.Value - progress.Minimum) / range * 100);
                return $"{text}, {percent}%";
            }
            return text;
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
