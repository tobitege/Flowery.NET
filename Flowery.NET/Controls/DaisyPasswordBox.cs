using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A PasswordBox control styled after DaisyUI's Input component.
    /// Supports labels, helper text, icons, floating label mode, and a reveal button.
    /// </summary>
    public class DaisyPasswordBox : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyPasswordBox);

        private TextBox? _innerTextBox;
        private Button? _revealButton;
        private bool _isPasswordVisible;
        private bool _isSyncingText;

        // Base font sizes for scaling
        private const double BaseLabelFontSize = 12.0;
        private const double BaseTextFontSize = 14.0;

        public DaisyPasswordBox()
        {
            UpdateHasTextPseudoClass();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Unsubscribe from old controls
            if (_innerTextBox != null)
            {
                _innerTextBox.TextChanged -= OnInnerTextChanged;
                _innerTextBox.GotFocus -= OnInnerGotFocus;
                _innerTextBox.LostFocus -= OnInnerLostFocus;
            }

            if (_revealButton != null)
            {
                _revealButton.Click -= OnRevealButtonClick;
            }

            // Get template parts
            _innerTextBox = e.NameScope.Find<TextBox>("PART_TextBox");
            _revealButton = e.NameScope.Find<Button>("PART_RevealButton");

            // Subscribe to events
            if (_innerTextBox != null)
            {
                _innerTextBox.TextChanged += OnInnerTextChanged;
                _innerTextBox.GotFocus += OnInnerGotFocus;
                _innerTextBox.LostFocus += OnInnerLostFocus;

                // Use native password masking - set PasswordChar on the TextBox itself
                _innerTextBox.PasswordChar = PasswordChar;
                _innerTextBox.RevealPassword = _isPasswordVisible;

                // Sync initial password (actual text, not masked - TextBox handles display)
                if (!string.IsNullOrEmpty(Password))
                {
                    _innerTextBox.Text = Password;
                }
            }

            if (_revealButton != null)
            {
                _revealButton.Click += OnRevealButtonClick;
            }
        }

        #region Password Property

        public static readonly StyledProperty<string> PasswordProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, string>(nameof(Password), string.Empty,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the password text.
        /// </summary>
        public string Password
        {
            get => GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        #endregion

        #region Variant Property

        public static readonly StyledProperty<DaisyInputVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, DaisyInputVariant>(nameof(Variant), DaisyInputVariant.Bordered);

        /// <summary>
        /// Gets or sets the visual variant.
        /// </summary>
        public DaisyInputVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        #endregion

        #region Size Property

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, DaisySize>(nameof(Size), DaisySize.Medium);

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

        public static readonly StyledProperty<string?> LabelProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, string?>(nameof(Label));

        /// <summary>
        /// Gets or sets the label text.
        /// </summary>
        public string? Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly StyledProperty<DaisyLabelPosition> LabelPositionProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, DaisyLabelPosition>(nameof(LabelPosition), DaisyLabelPosition.Top);

        /// <summary>
        /// Gets or sets the label positioning mode.
        /// </summary>
        public DaisyLabelPosition LabelPosition
        {
            get => GetValue(LabelPositionProperty);
            set => SetValue(LabelPositionProperty, value);
        }

        public static readonly StyledProperty<bool> IsRequiredProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, bool>(nameof(IsRequired));

        /// <summary>
        /// Gets or sets whether the input is required.
        /// </summary>
        public bool IsRequired
        {
            get => GetValue(IsRequiredProperty);
            set => SetValue(IsRequiredProperty, value);
        }

        public static readonly StyledProperty<bool> IsOptionalProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, bool>(nameof(IsOptional));

        /// <summary>
        /// Gets or sets whether to show "Optional" text.
        /// </summary>
        public bool IsOptional
        {
            get => GetValue(IsOptionalProperty);
            set => SetValue(IsOptionalProperty, value);
        }

        #endregion

        #region Helper Text Properties

        public static readonly StyledProperty<string?> HintTextProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, string?>(nameof(HintText));

        /// <summary>
        /// Gets or sets hint text displayed above the input.
        /// </summary>
        public string? HintText
        {
            get => GetValue(HintTextProperty);
            set => SetValue(HintTextProperty, value);
        }

        public static readonly StyledProperty<string?> HelperTextProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, string?>(nameof(HelperText));

        /// <summary>
        /// Gets or sets helper text displayed below the input.
        /// </summary>
        public string? HelperText
        {
            get => GetValue(HelperTextProperty);
            set => SetValue(HelperTextProperty, value);
        }

        public static readonly StyledProperty<string?> WatermarkProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, string?>(nameof(Watermark));

        /// <summary>
        /// Gets or sets the placeholder/watermark text.
        /// </summary>
        public string? Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        #endregion

        #region Icon Properties

        public static readonly StyledProperty<StreamGeometry?> StartIconProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, StreamGeometry?>(nameof(StartIcon));

        /// <summary>
        /// Gets or sets the icon displayed at the start of the input.
        /// </summary>
        public StreamGeometry? StartIcon
        {
            get => GetValue(StartIconProperty);
            set => SetValue(StartIconProperty, value);
        }

        public static readonly StyledProperty<StreamGeometry?> EndIconProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, StreamGeometry?>(nameof(EndIcon));

        /// <summary>
        /// Gets or sets the icon displayed at the end of the input.
        /// </summary>
        public StreamGeometry? EndIcon
        {
            get => GetValue(EndIconProperty);
            set => SetValue(EndIconProperty, value);
        }

        #endregion

        #region PasswordBox Specific Properties

        public static readonly StyledProperty<char> PasswordCharProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, char>(nameof(PasswordChar), '●');

        /// <summary>
        /// Gets or sets the character used to mask the password.
        /// </summary>
        public char PasswordChar
        {
            get => GetValue(PasswordCharProperty);
            set => SetValue(PasswordCharProperty, value);
        }

        public static readonly StyledProperty<bool> IsPasswordRevealButtonEnabledProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, bool>(nameof(IsPasswordRevealButtonEnabled), true);

        /// <summary>
        /// Gets or sets whether the reveal button is shown.
        /// </summary>
        public bool IsPasswordRevealButtonEnabled
        {
            get => GetValue(IsPasswordRevealButtonEnabledProperty);
            set => SetValue(IsPasswordRevealButtonEnabledProperty, value);
        }

        public static readonly StyledProperty<int> MaxLengthProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, int>(nameof(MaxLength), 0);

        /// <summary>
        /// Gets or sets the maximum length of the password.
        /// </summary>
        public int MaxLength
        {
            get => GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public static readonly StyledProperty<IBrush?> BorderRingBrushProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, IBrush?>(nameof(BorderRingBrush));

        /// <summary>
        /// Gets or sets a custom brush for the focus ring.
        /// </summary>
        public IBrush? BorderRingBrush
        {
            get => GetValue(BorderRingBrushProperty);
            set => SetValue(BorderRingBrushProperty, value);
        }

        #endregion

        #region Read-Only Properties

        public static readonly DirectProperty<DaisyPasswordBox, bool> HasTextProperty =
            AvaloniaProperty.RegisterDirect<DaisyPasswordBox, bool>(
                nameof(HasText),
                o => o.HasText);

        private bool _hasText;

        /// <summary>
        /// Gets whether the password field has text.
        /// </summary>
        public bool HasText
        {
            get => _hasText;
            private set => SetAndRaise(HasTextProperty, ref _hasText, value);
        }

        public static readonly DirectProperty<DaisyPasswordBox, bool> IsPasswordVisibleProperty =
            AvaloniaProperty.RegisterDirect<DaisyPasswordBox, bool>(
                nameof(IsPasswordVisible),
                o => o.IsPasswordVisible);

        /// <summary>
        /// Gets whether the password is currently visible.
        /// </summary>
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            private set
            {
                if (SetAndRaise(IsPasswordVisibleProperty, ref _isPasswordVisible, value))
                {
                    // Use native RevealPassword property for proper masking
                    if (_innerTextBox != null)
                    {
                        _innerTextBox.RevealPassword = value;
                    }
                    PseudoClasses.Set(":password-visible", value);
                }
            }
        }

        #endregion

        #region Scaling Properties

        public static readonly StyledProperty<double> ScaledLabelFontSizeProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, double>(nameof(ScaledLabelFontSize), BaseLabelFontSize);

        /// <summary>
        /// Gets the scaled font size for the label.
        /// </summary>
        public double ScaledLabelFontSize
        {
            get => GetValue(ScaledLabelFontSizeProperty);
            private set => SetValue(ScaledLabelFontSizeProperty, value);
        }

        public static readonly StyledProperty<double> ScaledTextFontSizeProperty =
            AvaloniaProperty.Register<DaisyPasswordBox, double>(nameof(ScaledTextFontSize), BaseTextFontSize);

        /// <summary>
        /// Gets the scaled font size for the input text.
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
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the password content changes.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? PasswordChanged;

        #endregion

        #region Property Changed

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PasswordProperty)
            {
                var newText = change.GetNewValue<string>() ?? string.Empty;
                HasText = !string.IsNullOrEmpty(newText);
                UpdateHasTextPseudoClass();

                // Sync text to inner TextBox (actual password - TextBox handles masking via PasswordChar)
                if (_innerTextBox != null && !_isSyncingText && _innerTextBox.Text != newText)
                {
                    _isSyncingText = true;
                    try
                    {
                        var caretIndex = _innerTextBox.CaretIndex;
                        _innerTextBox.Text = newText;
                        _innerTextBox.CaretIndex = Math.Min(caretIndex, newText.Length);
                    }
                    finally
                    {
                        _isSyncingText = false;
                    }
                }

                PasswordChanged?.Invoke(this, new RoutedEventArgs());
            }
            else if (change.Property == PasswordCharProperty)
            {
                // Update the inner TextBox's PasswordChar
                if (_innerTextBox != null)
                {
                    _innerTextBox.PasswordChar = PasswordChar;
                }
            }
            else if (change.Property == SizeProperty && FloweryScaleManager.GetEnableScaling(this))
            {
                ApplyScaleFactor(FloweryScaleManager.GetScaleFactor(this));
            }
        }

        private void UpdateHasTextPseudoClass()
        {
            PseudoClasses.Set(":hastext", !string.IsNullOrEmpty(Password));
        }

        #endregion

        #region Event Handlers

        private void OnInnerTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_innerTextBox == null || _isSyncingText) return;

            // The inner TextBox contains the actual password text
            // (TextBox handles display masking via PasswordChar property)
            var text = _innerTextBox.Text ?? string.Empty;

            if (Password != text)
            {
                _isSyncingText = true;
                try
                {
                    Password = text;
                }
                finally
                {
                    _isSyncingText = false;
                }
            }
        }

        private void OnInnerGotFocus(object? sender, GotFocusEventArgs e)
        {
            PseudoClasses.Set(":focus", true);
        }

        private void OnInnerLostFocus(object? sender, RoutedEventArgs e)
        {
            PseudoClasses.Set(":focus", false);
        }

        private void OnRevealButtonClick(object? sender, RoutedEventArgs e)
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        #endregion
    }
}
