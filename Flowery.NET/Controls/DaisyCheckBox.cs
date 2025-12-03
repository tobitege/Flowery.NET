using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

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
    /// </summary>
    public class DaisyCheckBox : CheckBox
    {
        protected override Type StyleKeyOverride => typeof(DaisyCheckBox);

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
