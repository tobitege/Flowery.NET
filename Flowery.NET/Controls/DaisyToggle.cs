using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyToggleVariant
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
    /// A ToggleSwitch control styled after DaisyUI's Toggle component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyToggle : ToggleSwitch, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyToggle);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyToggleVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyToggle, DaisyToggleVariant>(nameof(Variant), DaisyToggleVariant.Default);

        public DaisyToggleVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyToggle, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the internal padding of the toggle knob area (maps to --toggle-p).
        /// </summary>
        public static readonly StyledProperty<double> TogglePaddingProperty =
            AvaloniaProperty.Register<DaisyToggle, double>(nameof(TogglePadding), 2.0);

        public double TogglePadding
        {
            get => GetValue(TogglePaddingProperty);
            set => SetValue(TogglePaddingProperty, value);
        }
    }
}
