using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using System.Windows.Input;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A file input control styled after DaisyUI's File Input component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyFileInput : Button, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyFileInput);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<string> FileNameProperty =
            AvaloniaProperty.Register<DaisyFileInput, string>(nameof(FileName), "No file chosen");

        public string FileName
        {
            get => GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly StyledProperty<DaisyButtonVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyFileInput, DaisyButtonVariant>(nameof(Variant), DaisyButtonVariant.Default);

        public DaisyButtonVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

         public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyFileInput, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }
    }
}
