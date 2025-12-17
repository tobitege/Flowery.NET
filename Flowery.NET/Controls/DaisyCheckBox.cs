using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyCheckBoxVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Neutral,
        Success,
        Warning,
        Info,
        Error
    }

    /// <summary>
    /// A CheckBox control styled after DaisyUI's Checkbox component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyCheckBox : CheckBox, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyCheckBox);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyCheckBoxVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyCheckBox, DaisyCheckBoxVariant>(nameof(Variant), DaisyCheckBoxVariant.Default);

        public DaisyCheckBoxVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyCheckBox, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }
    }
}
