using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls.Custom
{
    /// <summary>
    /// Preset mask modes for <see cref="DaisyMaskInput"/>.
    /// </summary>
    public enum DaisyMaskInputMode
    {
        /// <summary>
        /// Uses the user-supplied <see cref="MaskedTextBox.Mask"/> value (default).
        /// </summary>
        Custom,
        /// <summary>
        /// Simple alphanumeric code mask (e.g. AB12 CDE).
        /// </summary>
        AlphaNumericCode,
        /// <summary>
        /// Timer/time style mask (e.g. 00:00:00).
        /// </summary>
        Timer,
        /// <summary>
        /// Expiry date mask (e.g. MM/YY).
        /// </summary>
        ExpiryDate,
        /// <summary>
        /// Credit card number mask (e.g. 0000 0000 0000 0000).
        /// </summary>
        CreditCardNumber,
        /// <summary>
        /// CVC/CVV mask (e.g. 000).
        /// </summary>
        Cvc
    }

    /// <summary>
    /// A MaskedTextBox control styled after DaisyUI's Input component, with support for input masks.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyMaskInput : MaskedTextBox, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyMaskInput);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private bool _isAutoWatermark;
        private bool _isApplyingMode;

        private const string AlphaNumericCodeMask = "AA00 AAA";
        private const string TimerMask = "00:00:00";
        private const string ExpiryDateShortYearMask = "00/00";
        private const string ExpiryDateLongYearMask = "00/0000";
        private const string CreditCardNumberMask = "0000 0000 0000 0000";
        private const string CvcMask = "000";

        static DaisyMaskInput()
        {
            ModeProperty.Changed.AddClassHandler<DaisyMaskInput>((s, _) => s.ApplyMode(forceWatermarkUpdate: s._isAutoWatermark));
            ExpiryYearDigitsProperty.Changed.AddClassHandler<DaisyMaskInput>((s, _) => s.ApplyMode(forceWatermarkUpdate: s._isAutoWatermark));
        }

        #region Mode Property
        /// <summary>
        /// Defines the <see cref="Mode"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyMaskInputMode> ModeProperty =
            AvaloniaProperty.Register<DaisyMaskInput, DaisyMaskInputMode>(nameof(Mode), DaisyMaskInputMode.Custom);

        /// <summary>
        /// Gets or sets a preset mask mode (Timer, CreditCardNumber, etc.). When set to a value other than Custom,
        /// this updates <see cref="MaskedTextBox.Mask"/> (and may set <see cref="TextBox.Watermark"/> if empty).
        /// </summary>
        public DaisyMaskInputMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }
        #endregion

        #region ExpiryYearDigits Property
        /// <summary>
        /// Defines the <see cref="ExpiryYearDigits"/> property.
        /// </summary>
        public static readonly StyledProperty<int> ExpiryYearDigitsProperty =
            AvaloniaProperty.Register<DaisyMaskInput, int>(nameof(ExpiryYearDigits), 2);

        /// <summary>
        /// Gets or sets the number of year digits for <see cref="DaisyMaskInputMode.ExpiryDate"/>.
        /// Valid values are 2 (YY/JJ) or 4 (YYYY/JJJJ). Default is 2.
        /// </summary>
        public int ExpiryYearDigits
        {
            get => GetValue(ExpiryYearDigitsProperty);
            set => SetValue(ExpiryYearDigitsProperty, value);
        }
        #endregion

        #region Variant Property
        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyInputVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyMaskInput, DaisyInputVariant>(nameof(Variant), DaisyInputVariant.Bordered);

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
            AvaloniaProperty.Register<DaisyMaskInput, DaisySize>(nameof(Size), DaisySize.Medium);

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
            AvaloniaProperty.Register<DaisyMaskInput, string?>(nameof(Label), null);

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
            AvaloniaProperty.Register<DaisyMaskInput, DaisyLabelPosition>(nameof(LabelPosition), DaisyLabelPosition.Top);

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
            AvaloniaProperty.Register<DaisyMaskInput, bool>(nameof(IsRequired), false);

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
            AvaloniaProperty.Register<DaisyMaskInput, bool>(nameof(IsOptional), false);

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
            AvaloniaProperty.Register<DaisyMaskInput, string?>(nameof(HintText), null);

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
            AvaloniaProperty.Register<DaisyMaskInput, string?>(nameof(HelperText), null);

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
            AvaloniaProperty.Register<DaisyMaskInput, StreamGeometry?>(nameof(StartIcon), null);

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
            AvaloniaProperty.Register<DaisyMaskInput, StreamGeometry?>(nameof(EndIcon), null);

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
            AvaloniaProperty.Register<DaisyMaskInput, IBrush?>(nameof(BorderRingBrush), null);

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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            FloweryLocalization.CultureChanged -= OnCultureChanged;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextBox.WatermarkProperty && !_isApplyingMode)
                _isAutoWatermark = false;
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnCultureChanged(sender, culture));
                return;
            }

            if (Mode == DaisyMaskInputMode.ExpiryDate && _isAutoWatermark)
                ApplyMode(forceWatermarkUpdate: true);
        }

        private void ApplyMode(bool forceWatermarkUpdate = false)
        {
            if (Mode == DaisyMaskInputMode.Custom)
                return;

            Mask = Mode switch
            {
                DaisyMaskInputMode.AlphaNumericCode => AlphaNumericCodeMask,
                DaisyMaskInputMode.Timer => TimerMask,
                DaisyMaskInputMode.ExpiryDate => ExpiryYearDigits >= 4 ? ExpiryDateLongYearMask : ExpiryDateShortYearMask,
                DaisyMaskInputMode.CreditCardNumber => CreditCardNumberMask,
                DaisyMaskInputMode.Cvc => CvcMask,
                _ => Mask
            };

            if (forceWatermarkUpdate ? !_isAutoWatermark : !string.IsNullOrEmpty(Watermark))
                return;

            _isApplyingMode = true;
            try
            {
                Watermark = Mode switch
                {
                    DaisyMaskInputMode.AlphaNumericCode => FloweryLocalization.GetStringInternal("MaskInput_Watermark_AlphaNumericCode", "AB12 CDE"),
                    DaisyMaskInputMode.Timer => FloweryLocalization.GetStringInternal("MaskInput_Watermark_Timer", "00:00:00"),
                    DaisyMaskInputMode.ExpiryDate => GetExpiryWatermark(ExpiryYearDigits),
                    DaisyMaskInputMode.CreditCardNumber => FloweryLocalization.GetStringInternal("MaskInput_Watermark_CreditCardNumber", "Card number"),
                    DaisyMaskInputMode.Cvc => FloweryLocalization.GetStringInternal("MaskInput_Watermark_Cvc", "CVC"),
                    _ => Watermark
                };
                _isAutoWatermark = true;
            }
            finally
            {
                _isApplyingMode = false;
            }
        }

        private static string GetExpiryWatermark(int yearDigits)
        {
            if (yearDigits >= 4)
                return FloweryLocalization.GetStringInternal("MaskInput_Watermark_ExpiryLong", "MM/YYYY");

            return FloweryLocalization.GetStringInternal("MaskInput_Watermark_ExpiryShort", "MM/YY");
        }
    }
}
