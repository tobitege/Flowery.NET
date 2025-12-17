using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Specifies the number base for display and input.
    /// </summary>
    public enum DaisyNumberBase
    {
        /// <summary>Decimal (base 10)</summary>
        Decimal,
        /// <summary>Hexadecimal (base 16, prefix 0x)</summary>
        Hexadecimal,
        /// <summary>Binary (base 2, prefix 0b)</summary>
        Binary,
        /// <summary>Octal (base 8, prefix 0o)</summary>
        Octal,
        /// <summary>Color hex (base 16, prefix #, e.g., #FF5733)</summary>
        ColorHex,
        /// <summary>IPv4 address format (e.g., 192.168.1.1)</summary>
        IPAddress
    }

    /// <summary>
    /// Specifies the letter case for hexadecimal digits (A-F).
    /// </summary>
    public enum DaisyHexCase
    {
        /// <summary>Uppercase (0xFF)</summary>
        Upper,
        /// <summary>Lowercase (0xff)</summary>
        Lower
    }

    /// <summary>
    /// A NumericUpDown control styled after DaisyUI's Input component with spin buttons.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyNumericUpDown : NumericUpDown, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyNumericUpDown);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private RepeatButton? _increaseButton;
        private RepeatButton? _decreaseButton;
        private Button? _clearButton;
        private TextBox? _textBox;
        private bool _isEditing;
        private bool _isUpdatingValue;
        private DispatcherTimer? _errorFlashTimer;

        /// <summary>
        /// Defines the <see cref="ShowInputError"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowInputErrorProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, bool>(nameof(ShowInputError), true);

        /// <summary>
        /// Gets or sets whether to show visual error feedback on invalid input.
        /// </summary>
        public bool ShowInputError
        {
            get => GetValue(ShowInputErrorProperty);
            set => SetValue(ShowInputErrorProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="InputErrorDuration"/> property.
        /// </summary>
        public static readonly StyledProperty<int> InputErrorDurationProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, int>(nameof(InputErrorDuration), 150);

        /// <summary>
        /// Gets or sets the duration in milliseconds for the error flash. Default is 150ms.
        /// </summary>
        public int InputErrorDuration
        {
            get => GetValue(InputErrorDurationProperty);
            set => SetValue(InputErrorDurationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyInputVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, DaisyInputVariant>(nameof(Variant), DaisyInputVariant.Bordered);

        /// <summary>
        /// Gets or sets the visual variant (e.g., Bordered, Ghost, Primary).
        /// </summary>
        public DaisyInputVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the input.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowButtons"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowButtonsProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, bool>(nameof(ShowButtons), true);

        /// <summary>
        /// Gets or sets whether the increment/decrement buttons are visible.
        /// </summary>
        public bool ShowButtons
        {
            get => GetValue(ShowButtonsProperty);
            set => SetValue(ShowButtonsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowClearButton"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowClearButtonProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, bool>(nameof(ShowClearButton), false);

        /// <summary>
        /// Gets or sets whether a clear button is shown on the left when focused.
        /// The clear button resets the value to 0.
        /// </summary>
        public bool ShowClearButton
        {
            get => GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="NumberBase"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyNumberBase> NumberBaseProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, DaisyNumberBase>(nameof(NumberBase), DaisyNumberBase.Decimal);

        /// <summary>
        /// Gets or sets the number base (Decimal, Hexadecimal, Binary).
        /// </summary>
        public DaisyNumberBase NumberBase
        {
            get => GetValue(NumberBaseProperty);
            set => SetValue(NumberBaseProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowThousandSeparators"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowThousandSeparatorsProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, bool>(nameof(ShowThousandSeparators), false);

        /// <summary>
        /// Gets or sets whether to display thousand separators when not editing.
        /// </summary>
        public bool ShowThousandSeparators
        {
            get => GetValue(ShowThousandSeparatorsProperty);
            set => SetValue(ShowThousandSeparatorsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowBasePrefix"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowBasePrefixProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, bool>(nameof(ShowBasePrefix), true);

        /// <summary>
        /// Gets or sets whether to show the base prefix (0x for hex, 0b for binary).
        /// </summary>
        public bool ShowBasePrefix
        {
            get => GetValue(ShowBasePrefixProperty);
            set => SetValue(ShowBasePrefixProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="HexCase"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyHexCase> HexCaseProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, DaisyHexCase>(nameof(HexCase), DaisyHexCase.Upper);

        /// <summary>
        /// Gets or sets the letter case for hexadecimal digits (A-F).
        /// </summary>
        public DaisyHexCase HexCase
        {
            get => GetValue(HexCaseProperty);
            set => SetValue(HexCaseProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Prefix"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> PrefixProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, string?>(nameof(Prefix));

        /// <summary>
        /// Gets or sets a fixed prefix displayed on the left (e.g., "$", "€").
        /// </summary>
        public string? Prefix
        {
            get => GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MaxDecimalPlaces"/> property.
        /// </summary>
        public static readonly StyledProperty<int> MaxDecimalPlacesProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, int>(nameof(MaxDecimalPlaces), -1);

        /// <summary>
        /// Gets or sets the maximum number of decimal places allowed. -1 means unlimited.
        /// </summary>
        public int MaxDecimalPlaces
        {
            get => GetValue(MaxDecimalPlacesProperty);
            set => SetValue(MaxDecimalPlacesProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MaxIntegerDigits"/> property.
        /// </summary>
        public static readonly StyledProperty<int> MaxIntegerDigitsProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, int>(nameof(MaxIntegerDigits), -1);

        /// <summary>
        /// Gets or sets the maximum number of integer digits allowed. -1 means unlimited.
        /// </summary>
        public int MaxIntegerDigits
        {
            get => GetValue(MaxIntegerDigitsProperty);
            set => SetValue(MaxIntegerDigitsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Suffix"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> SuffixProperty =
            AvaloniaProperty.Register<DaisyNumericUpDown, string?>(nameof(Suffix));

        /// <summary>
        /// Gets or sets a fixed suffix displayed on the right (e.g., "%", "kg").
        /// </summary>
        public string? Suffix
        {
            get => GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        #region Value Conversion Helpers

        /// <summary>
        /// Gets the current value as a hexadecimal string (e.g., "FF" or "ff").
        /// </summary>
        /// <param name="includePrefix">Include "0x" prefix (default: true)</param>
        /// <returns>Hex string, or null if Value is null</returns>
        public string? ToHexString(bool includePrefix = true)
        {
            if (Value == null) return null;
            var format = HexCase == DaisyHexCase.Upper ? "X" : "x";
            var hex = ((long)Value.Value).ToString(format);
            return includePrefix ? "0x" + hex : hex;
        }

        /// <summary>
        /// Gets the current value as a binary string (e.g., "1010").
        /// </summary>
        /// <param name="includePrefix">Include "0b" prefix (default: true)</param>
        /// <returns>Binary string, or null if Value is null</returns>
        public string? ToBinaryString(bool includePrefix = true)
        {
            if (Value == null) return null;
            var binary = Convert.ToString((long)Value.Value, 2);
            return includePrefix ? "0b" + binary : binary;
        }

        /// <summary>
        /// Gets the current value as an octal string (e.g., "377").
        /// </summary>
        /// <param name="includePrefix">Include "0o" prefix (default: true)</param>
        /// <returns>Octal string, or null if Value is null</returns>
        public string? ToOctalString(bool includePrefix = true)
        {
            if (Value == null) return null;
            var octal = Convert.ToString((long)Value.Value, 8);
            return includePrefix ? "0o" + octal : octal;
        }

        /// <summary>
        /// Gets the current value as a color hex string (e.g., "#FF5733").
        /// </summary>
        /// <param name="includePrefix">Include "#" prefix (default: true)</param>
        /// <returns>Color hex string padded to 6 digits, or null if Value is null</returns>
        public string? ToColorHexString(bool includePrefix = true)
        {
            if (Value == null) return null;
            var format = HexCase == DaisyHexCase.Upper ? "X" : "x";
            var hex = ((long)Value.Value).ToString(format).PadLeft(6, '0');
            return includePrefix ? "#" + hex : hex;
        }

        /// <summary>
        /// Gets the current value as an IPv4 address string (e.g., "192.168.1.1").
        /// </summary>
        /// <returns>IPv4 string, or null if Value is null</returns>
        public string? ToIPAddressString()
        {
            if (Value == null) return null;
            var ipValue = (uint)Math.Max(0, Math.Min(uint.MaxValue, Value.Value));
            return $"{(ipValue >> 24) & 0xFF}.{(ipValue >> 16) & 0xFF}.{(ipValue >> 8) & 0xFF}.{ipValue & 0xFF}";
        }

        /// <summary>
        /// Gets the current value formatted according to the current NumberBase setting.
        /// </summary>
        /// <param name="includePrefix">Include base prefix if applicable (default: true)</param>
        /// <returns>Formatted string, or null if Value is null</returns>
        public string? ToFormattedString(bool includePrefix = true)
        {
            if (Value == null) return null;
            return NumberBase switch
            {
                DaisyNumberBase.Hexadecimal => ToHexString(includePrefix && ShowBasePrefix),
                DaisyNumberBase.ColorHex => ToColorHexString(includePrefix && ShowBasePrefix),
                DaisyNumberBase.Binary => ToBinaryString(includePrefix && ShowBasePrefix),
                DaisyNumberBase.Octal => ToOctalString(includePrefix && ShowBasePrefix),
                DaisyNumberBase.IPAddress => ToIPAddressString(),
                _ => Value.Value.ToString(FormatString ?? "0", CultureInfo.CurrentCulture)
            };
        }

        #endregion

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Unsubscribe from old controls
            if (_increaseButton != null)
                _increaseButton.Click -= OnIncreaseClick;
            if (_decreaseButton != null)
                _decreaseButton.Click -= OnDecreaseClick;
            if (_clearButton != null)
                _clearButton.Click -= OnClearClick;
            if (_textBox != null)
            {
                _textBox.RemoveHandler(TextInputEvent, OnTextInput);
                _textBox.GotFocus -= OnTextBoxGotFocus;
                _textBox.LostFocus -= OnTextBoxLostFocus;
                _textBox.PastingFromClipboard -= OnPastingFromClipboard;
            }

            // Find template parts
            _increaseButton = e.NameScope.Find<RepeatButton>("PART_IncreaseButton");
            _decreaseButton = e.NameScope.Find<RepeatButton>("PART_DecreaseButton");
            _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
            _textBox = e.NameScope.Find<TextBox>("PART_DaisyTextBox");

            // Subscribe to events
            if (_increaseButton != null)
                _increaseButton.Click += OnIncreaseClick;
            if (_decreaseButton != null)
                _decreaseButton.Click += OnDecreaseClick;
            if (_clearButton != null)
                _clearButton.Click += OnClearClick;
            if (_textBox != null)
            {
                // Use tunneling to intercept TextInput BEFORE the TextBox processes it
                _textBox.AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
                _textBox.GotFocus += OnTextBoxGotFocus;
                _textBox.LostFocus += OnTextBoxLostFocus;
                _textBox.PastingFromClipboard += OnPastingFromClipboard;
            }

            UpdateDisplayText();
        }

        private void OnTextBoxGotFocus(object? sender, GotFocusEventArgs e)
        {
            _isEditing = true;
            // Don't call UpdateDisplayText here - it would strip formatting set by button clicks
            // The formatted text stays visible; input filtering handles valid characters
        }

        private void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            _isEditing = false;
            // Parse the edited value
            TryParseAndSetValue();
            UpdateDisplayText();
        }

        private void TryParseAndSetValue()
        {
            if (_textBox == null || _isUpdatingValue) return;

            var text = _textBox.Text ?? "";

            // Remove prefixes for parsing
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            else if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            else if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            else if (text.StartsWith("#"))
                text = text.Substring(1);

            try
            {
                decimal newValue;
                switch (NumberBase)
                {
                    case DaisyNumberBase.Hexadecimal:
                    case DaisyNumberBase.ColorHex:
                        if (long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexVal))
                            newValue = hexVal;
                        else
                            return;
                        break;
                    case DaisyNumberBase.Binary:
                        newValue = Convert.ToInt64(text, 2);
                        break;
                    case DaisyNumberBase.Octal:
                        newValue = Convert.ToInt64(text, 8);
                        break;
                    case DaisyNumberBase.IPAddress:
                        // Parse IPv4 format (a.b.c.d) to 32-bit value
                        var parts = text.Split('.');
                        if (parts.Length != 4)
                            return;
                        uint ipValue = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            if (!byte.TryParse(parts[i], out var octet))
                                return;
                            ipValue = (ipValue << 8) | octet;
                        }
                        newValue = ipValue;
                        break;
                    default:
                        // Remove thousand separators for parsing
                        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
                        text = text.Replace(separator, "");
                        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var decVal))
                            newValue = decVal;
                        else
                            return;
                        break;
                }

                if (newValue >= Minimum && newValue <= Maximum)
                {
                    _isUpdatingValue = true;
                    try
                    {
                        Value = newValue;
                    }
                    finally
                    {
                        _isUpdatingValue = false;
                    }
                }
            }
            catch
            {
                // Invalid input, ignore
            }
        }

        private void UpdateDisplayText(bool forceFormatted = false)
        {
            if (_textBox == null || Value == null) return;

            var value = Value.Value;
            string displayText;
            var showFormatting = forceFormatted || !_isEditing;

            switch (NumberBase)
            {
                case DaisyNumberBase.Hexadecimal:
                    var hexFormat = HexCase == DaisyHexCase.Upper ? "X" : "x";
                    displayText = ((long)value).ToString(hexFormat);
                    if (ShowBasePrefix && showFormatting)
                        displayText = "0x" + displayText;
                    break;
                case DaisyNumberBase.ColorHex:
                    var colorFormat = HexCase == DaisyHexCase.Upper ? "X" : "x";
                    // Pad to 6 digits for proper color format (RGB)
                    displayText = ((long)value).ToString(colorFormat);
                    if (displayText.Length < 6)
                        displayText = displayText.PadLeft(6, '0');
                    if (ShowBasePrefix && showFormatting)
                        displayText = "#" + displayText;
                    break;
                case DaisyNumberBase.Binary:
                    displayText = Convert.ToString((long)value, 2);
                    if (ShowBasePrefix && showFormatting)
                        displayText = "0b" + displayText;
                    break;
                case DaisyNumberBase.Octal:
                    displayText = Convert.ToString((long)value, 8);
                    if (ShowBasePrefix && showFormatting)
                        displayText = "0o" + displayText;
                    break;
                case DaisyNumberBase.IPAddress:
                    // Convert 32-bit value to IPv4 format (a.b.c.d)
                    var ipValue = (uint)Math.Max(0, Math.Min(uint.MaxValue, value));
                    displayText = $"{(ipValue >> 24) & 0xFF}.{(ipValue >> 16) & 0xFF}.{(ipValue >> 8) & 0xFF}.{ipValue & 0xFF}";
                    break;
                default:
                    if (ShowThousandSeparators && showFormatting)
                        displayText = value.ToString("N0", CultureInfo.CurrentCulture);
                    else
                        displayText = value.ToString(FormatString ?? "0", CultureInfo.CurrentCulture);
                    break;
            }

            _textBox.Text = displayText;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Skip updates if we're in the middle of our own value update
            if (_isUpdatingValue)
                return;

            if (change.Property == ValueProperty ||
                change.Property == NumberBaseProperty ||
                change.Property == ShowThousandSeparatorsProperty ||
                change.Property == ShowBasePrefixProperty ||
                change.Property == HexCaseProperty ||
                change.Property == FormatStringProperty)
            {
                if (!_isEditing)
                    UpdateDisplayText();
            }
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            var currentText = _textBox?.Text ?? "";
            var caretIndex = _textBox?.CaretIndex ?? 0;
            var selectionLength = _textBox?.SelectedText?.Length ?? 0;

            // Build what the text would look like after input
            var beforeCaret = currentText.Substring(0, Math.Max(0, caretIndex - selectionLength + selectionLength));

            // Check each character being input (e.Text is non-null after IsNullOrEmpty check)
            foreach (var c in e.Text!)
            {
                if (!IsValidNumericChar(c, currentText, caretIndex))
                {
                    e.Handled = true;
                    FlashInputError();
                    return;
                }
            }

            // Check max length
            var newLength = currentText.Length - selectionLength + e.Text.Length;
            var maxLen = GetMaxLength();
            if (maxLen > 0 && newLength > maxLen)
            {
                e.Handled = true;
                FlashInputError();
                return;
            }

            // Auto-case hex input to match HexCase setting (applies to Hex and ColorHex)
            if ((NumberBase == DaisyNumberBase.Hexadecimal || NumberBase == DaisyNumberBase.ColorHex) && _textBox != null)
            {
                var casedText = HexCase == DaisyHexCase.Upper
                    ? e.Text!.ToUpperInvariant()
                    : e.Text!.ToLowerInvariant();

                if (casedText != e.Text)
                {
                    // Replace the input with properly cased version
                    e.Handled = true;
                    var newText = currentText.Substring(0, caretIndex) + casedText +
                                  currentText.Substring(caretIndex + selectionLength);
                    _textBox.Text = newText;
                    _textBox.CaretIndex = caretIndex + casedText.Length;
                }
            }
        }

        private async void OnPastingFromClipboard(object? sender, RoutedEventArgs e)
        {
            if (_textBox == null) return;

            // Get clipboard text
            var clipboard = TopLevel.GetTopLevel(_textBox)?.Clipboard;
            if (clipboard == null) return;

            var pastedText = await clipboard.TryGetTextAsync();
            if (string.IsNullOrEmpty(pastedText)) return;

            // Filter or validate the pasted text
            var filteredText = FilterPastedText(pastedText!);

            if (string.IsNullOrEmpty(filteredText))
            {
                // Reject paste entirely if nothing valid
                e.Handled = true;
                FlashInputError();
                return;
            }

            if (filteredText != pastedText)
            {
                // Replace paste with filtered version - flash to indicate some chars were removed
                e.Handled = true;
                FlashInputError();

                var currentText = _textBox.Text ?? "";
                var caretIndex = _textBox.CaretIndex;
                var selectionLength = _textBox.SelectedText?.Length ?? 0;

                var newText = currentText.Substring(0, caretIndex) + filteredText +
                              currentText.Substring(caretIndex + selectionLength);

                // Check max length constraints
                if (!IsWithinMaxLength(newText))
                {
                    // Truncate if necessary
                    var maxLen = GetMaxLength();
                    if (maxLen > 0 && newText.Length > maxLen)
                        newText = newText.Substring(0, maxLen);
                }

                _textBox.Text = newText;
                _textBox.CaretIndex = Math.Min(caretIndex + filteredText.Length, newText.Length);
            }
        }

        private string FilterPastedText(string text)
        {
            var result = new System.Text.StringBuilder();
            var simulatedText = _textBox?.Text ?? "";
            var caretPos = 0;

            foreach (var c in text)
            {
                // Apply hex case transformation
                var charToAdd = c;
                if ((NumberBase == DaisyNumberBase.Hexadecimal || NumberBase == DaisyNumberBase.ColorHex) &&
                    ((c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                {
                    charToAdd = HexCase == DaisyHexCase.Upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                }

                // Check if character is valid in context
                if (IsValidForPaste(charToAdd, simulatedText + result.ToString(), caretPos))
                {
                    result.Append(charToAdd);
                    caretPos++;
                }
            }

            return result.ToString();
        }

        private bool IsValidForPaste(char c, string currentText, int caretIndex)
        {
            // Simplified validation for paste - allow valid characters
            switch (NumberBase)
            {
                case DaisyNumberBase.Hexadecimal:
                    return char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f') || c == 'x' || c == 'X';
                case DaisyNumberBase.ColorHex:
                    return char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f') || c == '#';
                case DaisyNumberBase.Binary:
                    return c == '0' || c == '1' || c == 'b' || c == 'B';
                case DaisyNumberBase.Octal:
                    return c >= '0' && c <= '7' || c == 'o' || c == 'O';
                case DaisyNumberBase.IPAddress:
                    return char.IsDigit(c) || c == '.';
                default:
                    var decSep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                    var negSign = CultureInfo.CurrentCulture.NumberFormat.NegativeSign;
                    return char.IsDigit(c) || decSep.Contains(c.ToString()) || negSign.Contains(c.ToString()) || c == 'e' || c == 'E' || c == '+' || c == '-';
            }
        }

        private bool IsWithinMaxLength(string text)
        {
            var maxLen = GetMaxLength();
            return maxLen <= 0 || text.Length <= maxLen;
        }

        private int GetMaxLength()
        {
            return NumberBase switch
            {
                DaisyNumberBase.ColorHex => 7, // #RRGGBB
                DaisyNumberBase.IPAddress => 15, // 255.255.255.255
                DaisyNumberBase.Hexadecimal => 18, // 0x + 16 hex digits (long.MaxValue = 0x7FFFFFFFFFFFFFFF)
                DaisyNumberBase.Binary => 66, // 0b + 64 binary digits
                DaisyNumberBase.Octal => 24, // 0o + 22 octal digits (long.MaxValue = 0o777777777777777777777)
                DaisyNumberBase.Decimal => GetDecimalMaxLength(),
                _ => 20 // Safe fallback for any numeric
            };
        }

        private int GetDecimalMaxLength()
        {
            // Calculate max length based on constraints or use practical defaults
            var intDigits = MaxIntegerDigits >= 0 ? MaxIntegerDigits : 15; // Quadrillions is plenty
            var decDigits = MaxDecimalPlaces >= 0 ? MaxDecimalPlaces : 6;  // 6 decimal places is typical max
            // +1 for decimal separator, +1 for negative sign, +4 for exponent (e+XX)
            return intDigits + decDigits + 6; // Max ~27 chars with defaults
        }

        private bool IsValidNumericChar(char c, string currentText, int caretIndex)
        {
            switch (NumberBase)
            {
                case DaisyNumberBase.Hexadecimal:
                    return IsValidHexChar(c, currentText, caretIndex);
                case DaisyNumberBase.ColorHex:
                    return IsValidColorHexChar(c, currentText, caretIndex);
                case DaisyNumberBase.Binary:
                    return IsValidBinaryChar(c, currentText, caretIndex);
                case DaisyNumberBase.Octal:
                    return IsValidOctalChar(c, currentText, caretIndex);
                case DaisyNumberBase.IPAddress:
                    return IsValidIPAddressChar(c, currentText, caretIndex);
                default:
                    return IsValidDecimalChar(c, currentText, caretIndex);
            }
        }

        private bool IsValidHexChar(char c, string currentText, int caretIndex)
        {
            // Allow hex digits 0-9, A-F, a-f
            if (char.IsDigit(c))
                return true;
            if ((c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))
                return true;
            // Allow 'x' or 'X' for prefix "0x" at position 1
            if ((c == 'x' || c == 'X') && caretIndex == 1 && currentText.StartsWith("0"))
                return !currentText.Contains("x") && !currentText.Contains("X");
            return false;
        }

        private bool IsValidColorHexChar(char c, string currentText, int caretIndex)
        {
            // Max length: 7 with '#' prefix (#RRGGBB) or 6 without
            var maxLength = currentText.StartsWith("#") ? 7 : 6;
            if (currentText.Length >= maxLength)
                return false;

            // Allow hex digits 0-9, A-F, a-f
            if (char.IsDigit(c))
                return true;
            if ((c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))
                return true;
            // Allow '#' prefix at position 0
            if (c == '#' && caretIndex == 0)
                return !currentText.Contains("#");
            return false;
        }

        private bool IsValidOctalChar(char c, string currentText, int caretIndex)
        {
            // Allow octal digits 0-7
            if (c >= '0' && c <= '7')
                return true;
            // Allow 'o' or 'O' for prefix "0o" at position 1
            if ((c == 'o' || c == 'O') && caretIndex == 1 && currentText.StartsWith("0"))
                return !currentText.Contains("o") && !currentText.Contains("O");
            return false;
        }

        private bool IsValidIPAddressChar(char c, string currentText, int caretIndex)
        {
            // Max length: 15 (255.255.255.255)
            if (currentText.Length >= 15)
                return false;

            // Allow digits 0-9
            if (char.IsDigit(c))
            {
                // Validate that adding this digit won't create an invalid octet (>255)
                var parts = currentText.Split('.');
                var currentPartIndex = 0;
                var pos = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    pos += parts[i].Length;
                    if (caretIndex <= pos)
                    {
                        currentPartIndex = i;
                        break;
                    }
                    pos++; // for the dot
                    currentPartIndex = i + 1;
                }

                // Get current octet being edited
                var currentOctet = currentPartIndex < parts.Length ? parts[currentPartIndex] : "";
                var newOctet = currentOctet + c;
                if (int.TryParse(newOctet, out var octetValue))
                    return octetValue <= 255;
                return false;
            }

            // Allow '.' as separator (max 3 dots)
            if (c == '.')
            {
                var dotCount = 0;
                foreach (var ch in currentText)
                    if (ch == '.') dotCount++;
                return dotCount < 3;
            }

            return false;
        }

        private bool IsValidBinaryChar(char c, string currentText, int caretIndex)
        {
            // Allow only 0 and 1
            if (c == '0' || c == '1')
                return true;
            // Allow 'b' or 'B' for prefix "0b" at position 1
            if ((c == 'b' || c == 'B') && caretIndex == 1 && currentText.StartsWith("0"))
                return !currentText.Contains("b") && !currentText.Contains("B");
            return false;
        }

        private bool IsValidDecimalChar(char c, string currentText, int caretIndex)
        {
            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var negativeSign = CultureInfo.CurrentCulture.NumberFormat.NegativeSign;
            var positiveSign = CultureInfo.CurrentCulture.NumberFormat.PositiveSign;

            // Check digit limits
            if (char.IsDigit(c))
            {
                // Find decimal separator position
                var decSepIndex = currentText.IndexOf(decimalSeparator, StringComparison.Ordinal);
                var exponentIndex = currentText.IndexOfAny(new[] { 'e', 'E' });

                // Determine if we're adding to integer or decimal part
                var isInDecimalPart = decSepIndex >= 0 && caretIndex > decSepIndex &&
                                      (exponentIndex < 0 || caretIndex < exponentIndex);
                var isInIntegerPart = decSepIndex < 0 || caretIndex <= decSepIndex;
                var isInExponentPart = exponentIndex >= 0 && caretIndex > exponentIndex;

                if (isInDecimalPart && MaxDecimalPlaces >= 0)
                {
                    // Count current decimal places (between decimal separator and exponent/end)
                    var decimalEndIndex = exponentIndex >= 0 ? exponentIndex : currentText.Length;
                    var currentDecimalPlaces = decimalEndIndex - decSepIndex - decimalSeparator.Length;
                    if (currentDecimalPlaces >= MaxDecimalPlaces)
                        return false;
                }

                if (isInIntegerPart && MaxIntegerDigits >= 0 && !isInExponentPart)
                {
                    // Count current integer digits (before decimal separator, excluding negative sign)
                    var integerEndIndex = decSepIndex >= 0 ? decSepIndex :
                                         (exponentIndex >= 0 ? exponentIndex : currentText.Length);
                    var startIndex = currentText.StartsWith(negativeSign) ? negativeSign.Length : 0;
                    var currentIntegerDigits = 0;
                    for (int i = startIndex; i < integerEndIndex; i++)
                        if (char.IsDigit(currentText[i])) currentIntegerDigits++;
                    if (currentIntegerDigits >= MaxIntegerDigits)
                        return false;
                }

                return true;
            }

            // Allow decimal separator if not already present (and not in exponent part)
            if (decimalSeparator.Contains(c.ToString()))
            {
                var exponentIndex = currentText.IndexOfAny(new[] { 'e', 'E' });
                // Don't allow decimal in exponent part
                if (exponentIndex >= 0 && caretIndex > exponentIndex)
                    return false;
                return !currentText.Contains(decimalSeparator);
            }

            // Allow 'e' or 'E' for exponential notation if there's at least one digit before
            if (c == 'e' || c == 'E')
            {
                // Must have at least one digit before exponent
                var hasDigitBefore = false;
                for (int i = 0; i < caretIndex && i < currentText.Length; i++)
                {
                    if (char.IsDigit(currentText[i]))
                    {
                        hasDigitBefore = true;
                        break;
                    }
                }
                // No existing exponent
                return hasDigitBefore && !currentText.Contains("e") && !currentText.Contains("E");
            }

            // Allow negative sign at start OR after exponent
            if (negativeSign.Contains(c.ToString()) || c == '-')
            {
                // At the start (if Minimum allows negative)
                if (caretIndex == 0 && !currentText.StartsWith(negativeSign) && Minimum < 0)
                    return true;

                // After exponent (e.g., 1e-5)
                if (caretIndex > 0 && caretIndex <= currentText.Length)
                {
                    var charBefore = currentText[caretIndex - 1];
                    if ((charBefore == 'e' || charBefore == 'E') && !HasExponentSign(currentText))
                        return true;
                }
                return false;
            }

            // Allow positive sign after exponent (e.g., 1e+5)
            if (positiveSign.Contains(c.ToString()) || c == '+')
            {
                if (caretIndex > 0 && caretIndex <= currentText.Length)
                {
                    var charBefore = currentText[caretIndex - 1];
                    if ((charBefore == 'e' || charBefore == 'E') && !HasExponentSign(currentText))
                        return true;
                }
                return false;
            }

            return false;
        }

        private static bool HasExponentSign(string text)
        {
            var exponentIndex = text.IndexOfAny(new[] { 'e', 'E' });
            if (exponentIndex < 0 || exponentIndex >= text.Length - 1)
                return false;
            var charAfterExp = text[exponentIndex + 1];
            return charAfterExp == '+' || charAfterExp == '-';
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsReadOnly)
            {
                base.OnKeyDown(e);
                return;
            }

            switch (e.Key)
            {
                case Key.Up:
                    Increase();
                    e.Handled = true;
                    return;
                case Key.Down:
                    Decrease();
                    e.Handled = true;
                    return;
            }

            base.OnKeyDown(e);
        }

        private void Increase()
        {
            // IP addresses: increment the octet at cursor position
            if (NumberBase == DaisyNumberBase.IPAddress)
            {
                IncrementIPOctet(1);
                return;
            }

            var newValue = (Value ?? 0) + Increment;
            if (newValue <= Maximum)
            {
                _isUpdatingValue = true;
                try
                {
                    Value = newValue;
                }
                finally
                {
                    _isUpdatingValue = false;
                }
                // Force display update with full formatting (button clicks aren't text editing)
                UpdateDisplayText(forceFormatted: true);
            }
        }

        private void Decrease()
        {
            // IP addresses: decrement the octet at cursor position
            if (NumberBase == DaisyNumberBase.IPAddress)
            {
                IncrementIPOctet(-1);
                return;
            }

            var newValue = (Value ?? 0) - Increment;
            if (newValue >= Minimum)
            {
                _isUpdatingValue = true;
                try
                {
                    Value = newValue;
                }
                finally
                {
                    _isUpdatingValue = false;
                }
                // Force display update with full formatting (button clicks aren't text editing)
                UpdateDisplayText(forceFormatted: true);
            }
        }

        private void OnIncreaseClick(object? sender, RoutedEventArgs e) => Increase();

        private void OnDecreaseClick(object? sender, RoutedEventArgs e) => Decrease();

        private void OnClearClick(object? sender, RoutedEventArgs e)
        {
            _isUpdatingValue = true;
            try
            {
                Value = Math.Max(0, Minimum);
            }
            finally
            {
                _isUpdatingValue = false;
            }
            UpdateDisplayText(forceFormatted: true);
            _textBox?.Focus();
        }

        /// <summary>
        /// Increments or decrements the IP octet at the current cursor position.
        /// </summary>
        private void IncrementIPOctet(int delta)
        {
            if (_textBox == null || Value == null) return;

            var text = _textBox.Text ?? "";
            var caretIndex = _textBox.CaretIndex;
            var parts = text.Split('.');

            if (parts.Length != 4) return;

            // Determine which octet the cursor is in
            var octetIndex = 0;
            var pos = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                pos += parts[i].Length;
                if (caretIndex <= pos)
                {
                    octetIndex = i;
                    break;
                }
                pos++; // for the dot
                octetIndex = i + 1;
            }
            octetIndex = Math.Min(octetIndex, 3);

            // Parse and modify the octet
            if (!int.TryParse(parts[octetIndex], out var octetValue))
                return;

            var newOctetValue = octetValue + delta;

            // Clamp to 0-255, flash error if at limit
            if (newOctetValue < 0)
                newOctetValue = 255;
            else if (newOctetValue > 255)
                newOctetValue = 0;

            // Update the octet
            parts[octetIndex] = newOctetValue.ToString();

            // Rebuild the IP and calculate new value
            var newText = string.Join(".", parts);
            uint ipValue = 0;
            for (int i = 0; i < 4; i++)
            {
                if (byte.TryParse(parts[i], out var b))
                    ipValue = (ipValue << 8) | b;
            }

            _isUpdatingValue = true;
            try
            {
                Value = ipValue;
            }
            finally
            {
                _isUpdatingValue = false;
            }

            // Update display and restore cursor position
            _textBox.Text = newText;
            _textBox.CaretIndex = Math.Min(caretIndex, newText.Length);
        }

        /// <summary>
        /// Triggers a visual error flash to indicate invalid input.
        /// </summary>
        private void FlashInputError()
        {
            if (!ShowInputError) return;

            // Set the error pseudo-class
            PseudoClasses.Add(":input-error");

            // Stop any existing timer
            _errorFlashTimer?.Stop();

            // Create timer to remove the error state
            _errorFlashTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(InputErrorDuration)
            };
            _errorFlashTimer.Tick += (s, e) =>
            {
                PseudoClasses.Remove(":input-error");
                _errorFlashTimer?.Stop();
            };
            _errorFlashTimer.Start();
        }
    }
}
