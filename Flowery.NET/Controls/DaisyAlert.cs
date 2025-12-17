using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyAlertVariant
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// An Alert control styled after DaisyUI's Alert component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyAlert : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAlert);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyAlertVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyAlert, DaisyAlertVariant>(nameof(Variant), DaisyAlertVariant.Info);

        public DaisyAlertVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<object?> IconProperty =
            AvaloniaProperty.Register<DaisyAlert, object?>(nameof(Icon));

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}
