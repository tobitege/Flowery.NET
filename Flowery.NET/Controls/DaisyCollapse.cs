using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A Collapse/Expander control styled after DaisyUI's Collapse component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyCollapse : Expander, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyCollapse);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        // Inherits Expander logic.
        // DaisyUI Collapse variants:
        // - collapse-arrow: Arrow on right.
        // - collapse-plus: Plus/Minus sign.
        // - Default: No icon usually (just click).

        // We can add a Variant enum if we want to switch icons easily.

        public static readonly StyledProperty<DaisyCollapseVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyCollapse, DaisyCollapseVariant>(nameof(Variant), DaisyCollapseVariant.Arrow);

        public DaisyCollapseVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }
    }

    public enum DaisyCollapseVariant
    {
        Arrow,
        Plus
    }
}
