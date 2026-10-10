using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyRadioVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Success,
        Warning,
        Info,
        Error
    }

    /// <summary>
    /// A RadioButton control styled after DaisyUI's Radio component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyRadio : RadioButton, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyRadio);


        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FloweryScaleManager.SetScaleFactor(this, FloweryScaleManager.SanitizeScaleFactor(scaleFactor));
        }

        public static readonly StyledProperty<DaisyRadioVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyRadio, DaisyRadioVariant>(nameof(Variant), DaisyRadioVariant.Default);

        public DaisyRadioVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyRadio, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }
    }
}
