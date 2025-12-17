using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Visual variant for DaisyInput controls.
    /// </summary>
    public enum DaisyInputVariant
    {
        Bordered,
        Ghost,
        Filled,
        Primary,
        Secondary,
        Accent,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Label positioning mode for DaisyInput controls.
    /// </summary>
    public enum DaisyLabelPosition
    {
        /// <summary>No label displayed.</summary>
        None,
        /// <summary>Standard label above input.</summary>
        Top,
        /// <summary>Label floats to top on focus.</summary>
        Floating,
        /// <summary>Label inside border, top-aligned.</summary>
        Inset
    }

    /// <summary>
    /// A TextBox control styled after DaisyUI's Input component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyInput : TextBox, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyInput);

        // Base font sizes for scaling (before scale factor is applied)
        private const double BaseLabelFontSize = 12.0;
        private const double BaseTextFontSize = 14.0;

        #region Scaling Properties

        /// <summary>
        /// Defines the <see cref="ScaledLabelFontSize"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ScaledLabelFontSizeProperty =
            AvaloniaProperty.Register<DaisyInput, double>(nameof(ScaledLabelFontSize), BaseLabelFontSize);

        /// <summary>
        /// Gets the scaled font size for the label. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledLabelFontSize
        {
            get => GetValue(ScaledLabelFontSizeProperty);
            private set => SetValue(ScaledLabelFontSizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ScaledTextFontSize"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ScaledTextFontSizeProperty =
            AvaloniaProperty.Register<DaisyInput, double>(nameof(ScaledTextFontSize), BaseTextFontSize);

        /// <summary>
        /// Gets the scaled font size for the input text. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledTextFontSize
        {
            get => GetValue(ScaledTextFontSizeProperty);
            private set => SetValue(ScaledTextFontSizeProperty, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            ScaledLabelFontSize = FloweryScaleManager.ApplyScale(BaseLabelFontSize, 10.0, scaleFactor);
            ScaledTextFontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
            FontSize = ScaledTextFontSize;
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyInputVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyInput, DaisyInputVariant>(nameof(Variant), DaisyInputVariant.Bordered);

        /// <summary>
        /// Gets or sets the visual variant (e.g., Bordered, Ghost, Filled, Primary).
        /// </summary>
        public DaisyInputVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }
        #endregion

        #region Size Property
        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyInput, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the input.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }
        #endregion

        #region Label Properties
        /// <summary>
        /// Defines the <see cref="Label"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> LabelProperty =
            AvaloniaProperty.Register<DaisyInput, string?>(nameof(Label), null);

        /// <summary>
        /// Gets or sets the label text displayed above/around the input.
        /// </summary>
        public string? Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="LabelPosition"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyLabelPosition> LabelPositionProperty =
            AvaloniaProperty.Register<DaisyInput, DaisyLabelPosition>(nameof(LabelPosition), DaisyLabelPosition.Top);

        /// <summary>
        /// Gets or sets the label positioning mode.
        /// </summary>
        public DaisyLabelPosition LabelPosition
        {
            get => GetValue(LabelPositionProperty);
            set => SetValue(LabelPositionProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsRequired"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsRequiredProperty =
            AvaloniaProperty.Register<DaisyInput, bool>(nameof(IsRequired), false);

        /// <summary>
        /// Gets or sets whether the input is required (shows asterisk indicator).
        /// </summary>
        public bool IsRequired
        {
            get => GetValue(IsRequiredProperty);
            set => SetValue(IsRequiredProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsOptional"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsOptionalProperty =
            AvaloniaProperty.Register<DaisyInput, bool>(nameof(IsOptional), false);

        /// <summary>
        /// Gets or sets whether to show "Optional" text next to the label.
        /// </summary>
        public bool IsOptional
        {
            get => GetValue(IsOptionalProperty);
            set => SetValue(IsOptionalProperty, value);
        }
        #endregion

        #region Helper Text Properties
        /// <summary>
        /// Defines the <see cref="HintText"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> HintTextProperty =
            AvaloniaProperty.Register<DaisyInput, string?>(nameof(HintText), null);

        /// <summary>
        /// Gets or sets hint text displayed above the input (below label).
        /// </summary>
        public string? HintText
        {
            get => GetValue(HintTextProperty);
            set => SetValue(HintTextProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="HelperText"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> HelperTextProperty =
            AvaloniaProperty.Register<DaisyInput, string?>(nameof(HelperText), null);

        /// <summary>
        /// Gets or sets helper text displayed below the input (right-aligned).
        /// </summary>
        public string? HelperText
        {
            get => GetValue(HelperTextProperty);
            set => SetValue(HelperTextProperty, value);
        }
        #endregion

        #region Icon Properties
        /// <summary>
        /// Defines the <see cref="StartIcon"/> property.
        /// </summary>
        public static readonly StyledProperty<StreamGeometry?> StartIconProperty =
            AvaloniaProperty.Register<DaisyInput, StreamGeometry?>(nameof(StartIcon), null);

        /// <summary>
        /// Gets or sets the icon displayed at the start (left) of the input.
        /// </summary>
        public StreamGeometry? StartIcon
        {
            get => GetValue(StartIconProperty);
            set => SetValue(StartIconProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="EndIcon"/> property.
        /// </summary>
        public static readonly StyledProperty<StreamGeometry?> EndIconProperty =
            AvaloniaProperty.Register<DaisyInput, StreamGeometry?>(nameof(EndIcon), null);

        /// <summary>
        /// Gets or sets the icon displayed at the end (right) of the input.
        /// </summary>
        public StreamGeometry? EndIcon
        {
            get => GetValue(EndIconProperty);
            set => SetValue(EndIconProperty, value);
        }
        #endregion

        #region Border Ring Property
        /// <summary>
        /// Defines the <see cref="BorderRingBrush"/> property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> BorderRingBrushProperty =
            AvaloniaProperty.Register<DaisyInput, IBrush?>(nameof(BorderRingBrush), null);

        /// <summary>
        /// Gets or sets a custom brush for the focus ring around the input.
        /// When set, this brush is used instead of the default focus color.
        /// </summary>
        public IBrush? BorderRingBrush
        {
            get => GetValue(BorderRingBrushProperty);
            set => SetValue(BorderRingBrushProperty, value);
        }
        #endregion
    }
}
