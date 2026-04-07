using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// An OTP/verification code input composed of multiple single-character slots.
    /// </summary>
    public class DaisyOtpInput : StackPanel, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyOtpInput);

        private const double BaseTextFontSize = 14.0;

        private readonly List<TextBox> _slots = new();
        private bool _isUpdating;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            // StackPanel doesn't expose FontSize directly; set it for child text inheritance.
            SetValue(TextBlock.FontSizeProperty, FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor));
        }

        /// <summary>
        /// Defines the <see cref="Length"/> property.
        /// </summary>
        public static readonly StyledProperty<int> LengthProperty =
            AvaloniaProperty.Register<DaisyOtpInput, int>(nameof(Length), 6);

        /// <summary>
        /// Gets or sets the number of slots.
        /// </summary>
        public int Length
        {
            get => GetValue(LengthProperty);
            set => SetValue(LengthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Value"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> ValueProperty =
            AvaloniaProperty.Register<DaisyOtpInput, string?>(nameof(Value), null);

        /// <summary>
        /// Gets or sets the current OTP value.
        /// </summary>
        public string? Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="AcceptsOnlyDigits"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AcceptsOnlyDigitsProperty =
            AvaloniaProperty.Register<DaisyOtpInput, bool>(nameof(AcceptsOnlyDigits), true);

        /// <summary>
        /// Gets or sets whether only digits are allowed.
        /// </summary>
        public bool AcceptsOnlyDigits
        {
            get => GetValue(AcceptsOnlyDigitsProperty);
            set => SetValue(AcceptsOnlyDigitsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="AutoAdvance"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoAdvanceProperty =
            AvaloniaProperty.Register<DaisyOtpInput, bool>(nameof(AutoAdvance), true);

        /// <summary>
        /// Gets or sets whether focus automatically advances to the next slot.
        /// </summary>
        public bool AutoAdvance
        {
            get => GetValue(AutoAdvanceProperty);
            set => SetValue(AutoAdvanceProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="AutoSelectOnFocus"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoSelectOnFocusProperty =
            AvaloniaProperty.Register<DaisyOtpInput, bool>(nameof(AutoSelectOnFocus), true);

        /// <summary>
        /// Gets or sets whether the slot content is selected when focused.
        /// </summary>
        public bool AutoSelectOnFocus
        {
            get => GetValue(AutoSelectOnFocusProperty);
            set => SetValue(AutoSelectOnFocusProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SeparatorInterval"/> property.
        /// </summary>
        public static readonly StyledProperty<int> SeparatorIntervalProperty =
            AvaloniaProperty.Register<DaisyOtpInput, int>(nameof(SeparatorInterval), 0);

        /// <summary>
        /// Gets or sets the slot interval at which a separator is inserted (0 = no separator).
        /// </summary>
        public int SeparatorInterval
        {
            get => GetValue(SeparatorIntervalProperty);
            set => SetValue(SeparatorIntervalProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SeparatorText"/> property.
        /// </summary>
        public static readonly StyledProperty<string> SeparatorTextProperty =
            AvaloniaProperty.Register<DaisyOtpInput, string>(nameof(SeparatorText), "–");

        /// <summary>
        /// Gets or sets the separator text.
        /// </summary>
        public string SeparatorText
        {
            get => GetValue(SeparatorTextProperty);
            set => SetValue(SeparatorTextProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyOtpInput, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the OTP slots.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Raised when the input becomes complete (Value.Length == Length).
        /// </summary>
        public event EventHandler<string>? Completed;

        static DaisyOtpInput()
        {
            LengthProperty.Changed.AddClassHandler<DaisyOtpInput>((s, _) => s.RebuildSlots());
            SeparatorIntervalProperty.Changed.AddClassHandler<DaisyOtpInput>((s, _) => s.RebuildSlots());
            SeparatorTextProperty.Changed.AddClassHandler<DaisyOtpInput>((s, _) => s.RebuildSlots());
            AcceptsOnlyDigitsProperty.Changed.AddClassHandler<DaisyOtpInput>((s, _) => s.ApplyValueToSlots());
            ValueProperty.Changed.AddClassHandler<DaisyOtpInput>((s, _) => s.ApplyValueToSlots());
        }

        public DaisyOtpInput()
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Spacing = 6;

            RebuildSlots();
        }

        private void RebuildSlots()
        {
            _isUpdating = true;
            try
            {
                Children.Clear();

                foreach (var slot in _slots)
                {
                    slot.TextChanged -= OnSlotTextChanged;
                    slot.KeyDown -= OnSlotKeyDown;
                    slot.GotFocus -= OnSlotGotFocus;
                }

                _slots.Clear();

                var length = Length;
                var interval = SeparatorInterval;

                for (int i = 0; i < length; i++)
                {
                    if (interval > 0 && i > 0 && (i % interval) == 0)
                    {
                        var separator = new TextBlock
                        {
                            Text = SeparatorText,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        };
                        separator.Classes.Add("otp-separator");
                        Children.Add(separator);
                    }

                    var box = CreateSlot(i);
                    _slots.Add(box);
                    Children.Add(box);
                }
            }
            finally
            {
                _isUpdating = false;
            }

            ApplyValueToSlots();
        }

        private TextBox CreateSlot(int index)
        {
            var box = new TextBox
            {
                Tag = index,
                TextAlignment = TextAlignment.Center,
                CaretIndex = 0,
                MaxLength = Length
            };

            box.Classes.Add("otp-slot");

            box.TextChanged += OnSlotTextChanged;
            box.KeyDown += OnSlotKeyDown;
            box.GotFocus += OnSlotGotFocus;

            return box;
        }

        private void OnSlotGotFocus(object? sender, FocusChangedEventArgs e)
        {
            if (!AutoSelectOnFocus) return;
            if (sender is TextBox box)
            {
                box.SelectAll();
            }
        }

        private void ApplyValueToSlots()
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                var value = Normalize(Value);

                if (value.Length > Length)
                {
                    value = value.Substring(0, Length);
                }

                for (int i = 0; i < _slots.Count; i++)
                {
                    _slots[i].Text = i < value.Length ? value[i].ToString() : string.Empty;
                    _slots[i].CaretIndex = _slots[i].Text?.Length ?? 0;
                }

                if (!string.Equals(Value, value, StringComparison.Ordinal))
                {
                    SetCurrentValue(ValueProperty, value);
                }
            }
            finally
            {
                _isUpdating = false;
            }

            RaiseCompletedIfNeeded();
        }

        private void OnSlotKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox box) return;
            if (box.Tag is not int index) return;

            switch (e.Key)
            {
                case Key.Left:
                    FocusSlot(index - 1);
                    e.Handled = true;
                    break;
                case Key.Right:
                    FocusSlot(index + 1);
                    e.Handled = true;
                    break;
                case Key.Back:
                {
                    var text = box.Text ?? string.Empty;
                    if (text.Length == 0)
                    {
                        if (index > 0)
                        {
                            _isUpdating = true;
                            try
                            {
                                _slots[index - 1].Text = string.Empty;
                                _slots[index - 1].CaretIndex = 0;
                            }
                            finally
                            {
                                _isUpdating = false;
                            }

                            UpdateValueFromSlots();
                            FocusSlot(index - 1);
                        }
                        e.Handled = true;
                    }
                    break;
                }
            }
        }

        private void OnSlotTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (sender is not TextBox box) return;
            if (box.Tag is not int index) return;

            var text = Normalize(box.Text);
            if (text.Length == 0)
            {
                _isUpdating = true;
                try
                {
                    box.Text = string.Empty;
                    box.CaretIndex = 0;
                }
                finally
                {
                    _isUpdating = false;
                }

                UpdateValueFromSlots();
                return;
            }

            if (text.Length == 1)
            {
                _isUpdating = true;
                try
                {
                    box.Text = text;
                    box.CaretIndex = 1;
                }
                finally
                {
                    _isUpdating = false;
                }

                UpdateValueFromSlots();

                if (AutoAdvance)
                {
                    FocusSlot(index + 1);
                }

                return;
            }

            DistributeText(index, text);
        }

        private void DistributeText(int startIndex, string text)
        {
            _isUpdating = true;
            try
            {
                var slotIndex = startIndex;
                foreach (var ch in text)
                {
                    if (slotIndex >= _slots.Count) break;
                    _slots[slotIndex].Text = ch.ToString();
                    _slots[slotIndex].CaretIndex = 1;
                    slotIndex++;
                }

                // Clear any extra characters that might remain in the source box
                _slots[startIndex].Text = _slots[startIndex].Text?.Length > 0
                    ? _slots[startIndex].Text![0].ToString()
                    : string.Empty;
            }
            finally
            {
                _isUpdating = false;
            }

            UpdateValueFromSlots();

            if (AutoAdvance)
            {
                var next = Math.Min(startIndex + text.Length, _slots.Count);
                FocusSlot(next);
            }
        }

        private void FocusSlot(int index)
        {
            if (index < 0) return;
            if (index >= _slots.Count) return;

            var box = _slots[index];
            box.Focus();
            if (AutoSelectOnFocus)
            {
                box.SelectAll();
            }
        }

        private void UpdateValueFromSlots()
        {
            var chars = new List<char>();
            foreach (var slot in _slots)
            {
                var t = Normalize(slot.Text);
                if (t.Length == 0)
                    break;
                chars.Add(t[0]);
            }

            var newValue = new string(chars.ToArray());
            if (!string.Equals(Value, newValue, StringComparison.Ordinal))
            {
                SetCurrentValue(ValueProperty, newValue);
            }

            RaiseCompletedIfNeeded();
        }

        private void RaiseCompletedIfNeeded()
        {
            var value = Normalize(Value);
            if (value.Length == Length)
            {
                Completed?.Invoke(this, value);
            }
        }

        private string Normalize(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var text = input!;

            if (!AcceptsOnlyDigits)
            {
                return text;
            }

            var result = new char[text.Length];
            var count = 0;
            foreach (var ch in text)
            {
                if (ch >= '0' && ch <= '9')
                {
                    result[count] = ch;
                    count++;
                }
            }

            return new string(result, 0, count);
        }
    }
}
