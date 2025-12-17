using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A radial (circular) progress indicator styled after DaisyUI's Radial Progress component.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyRadialProgress : RangeBase, IScalableControl
    {
        private const string DefaultAccessibleText = "Progress";
        private const double BaseTextFontSize = 14.0;

        protected override Type StyleKeyOverride => typeof(DaisyRadialProgress);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        static DaisyRadialProgress()
        {
            DaisyAccessibility.SetupAccessibility<DaisyRadialProgress>(DefaultAccessibleText);
        }

        public DaisyRadialProgress()
        {
            Minimum = 0;
            Maximum = 100;
            Value = 0;
        }

        public static readonly StyledProperty<DaisyProgressVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyRadialProgress, DaisyProgressVariant>(nameof(Variant), DaisyProgressVariant.Default);

        public DaisyProgressVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyRadialProgress, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<double> ThicknessProperty =
            AvaloniaProperty.Register<DaisyRadialProgress, double>(nameof(Thickness), 4);

        public double Thickness
        {
            get => GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
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
            return new DaisyRadialProgressAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyRadialProgress that exposes it as a ProgressBar to assistive technologies.
    /// </summary>
    internal class DaisyRadialProgressAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Progress";

        public DaisyRadialProgressAutomationPeer(DaisyRadialProgress owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ProgressBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyRadialProgress";
        }

        protected override string? GetNameCore()
        {
            var progress = (DaisyRadialProgress)Owner;
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
