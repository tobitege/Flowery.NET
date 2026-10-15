using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyNumericUpDownTests
    {
        #region Complex Scenarios

        [AvaloniaFact]
        public void Should_Work_With_Currency_Configuration()
        {
            var control = new DaisyNumericUpDown
            {
                Prefix = "€",
                FormatString = "N2",
                Minimum = 0,
                Maximum = 10000,
                Increment = 0.01m,
                Value = 99.99m
            };
            using var window = new WindowScope(control);

            Assert.Equal("€", control.Prefix);
            Assert.Equal(99.99m, control.Value);
        }

        [AvaloniaFact]
        public void Should_Work_With_Percentage_Configuration()
        {
            var control = new DaisyNumericUpDown
            {
                Suffix = "%",
                Minimum = 0,
                Maximum = 100,
                Increment = 1,
                Value = 75
            };
            using var window = new WindowScope(control);

            Assert.Equal("%", control.Suffix);
            Assert.Equal(75m, control.Value);
        }

        [AvaloniaFact]
        public void Should_Work_With_Hex_Color_Configuration()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                ShowBasePrefix = true,
                Minimum = 0,
                Maximum = 0xFFFFFF,
                Value = 0xFF5733
            };
            using var window = new WindowScope(control);

            Assert.Equal(DaisyNumberBase.Hexadecimal, control.NumberBase);
            Assert.Equal(0xFF5733, control.Value);
        }

        [AvaloniaFact]
        public void Should_Work_With_Binary_Flags_Configuration()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Binary,
                ShowBasePrefix = true,
                Minimum = 0,
                Maximum = 255,
                Value = 0b10101010
            };
            using var window = new WindowScope(control);

            Assert.Equal(DaisyNumberBase.Binary, control.NumberBase);
            Assert.Equal(170m, control.Value); // 0b10101010 = 170
        }

        #endregion

        #region Value and Bounds Behavior

        [AvaloniaFact]
        public void Programmatic_Value_Set_Is_Allowed_Outside_Bounds()
        {
            // Note: Avalonia's NumericUpDown allows programmatic sets outside bounds
            // Clamping only happens during increment/decrement via buttons
            var control = new DaisyNumericUpDown
            {
                Minimum = 0,
                Maximum = 100
            };
            using var window = new WindowScope(control);

            control.Value = 150; // Above maximum - allowed programmatically

            Assert.Equal(150m, control.Value);
        }

        #endregion

        #region NumberBase Conversions

        [AvaloniaFact]
        public void Hexadecimal_Value_255_Should_Be_Stored_Correctly()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                Value = 0xFF // 255 in decimal
            };
            using var window = new WindowScope(control);

            Assert.Equal(255m, control.Value);
        }

        [AvaloniaFact]
        public void Binary_Value_Should_Be_Stored_As_Decimal()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Binary,
                Value = 0b1111 // 15 in decimal
            };
            using var window = new WindowScope(control);

            Assert.Equal(15m, control.Value);
        }

        [AvaloniaFact]
        public void Changing_NumberBase_Should_Not_Change_Value()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Decimal,
                Value = 255
            };
            using var window = new WindowScope(control);

            // Change to hex
            control.NumberBase = DaisyNumberBase.Hexadecimal;

            // Value should remain 255 (just displayed differently)
            Assert.Equal(255m, control.Value);

            // Change to binary
            control.NumberBase = DaisyNumberBase.Binary;

            // Value should still remain 255
            Assert.Equal(255m, control.Value);
        }

        #endregion

        #region Null Value Handling

        [AvaloniaFact]
        public void Null_Value_Should_Be_Allowed()
        {
            var control = new DaisyNumericUpDown();
            using var window = new WindowScope(control);

            control.Value = null;

            Assert.Null(control.Value);
        }

        #endregion

        #region Prefix/Suffix Independence

        [AvaloniaFact]
        public void Changing_Prefix_Should_Not_Change_Value()
        {
            var control = new DaisyNumericUpDown
            {
                Prefix = "$",
                Value = 99.99m
            };
            using var window = new WindowScope(control);

            control.Prefix = "€";

            Assert.Equal(99.99m, control.Value);
            Assert.Equal("€", control.Prefix);
        }

        #endregion

        #region TextBox Display Tests (Actual UI Behavior)

        private sealed class WindowScope : IDisposable
        {
            private readonly Window _window;
            public WindowScope(Control content)
            {
                _window = new Window { Content = content };
                _window.Show();
            }
            public void Dispose() => _window.Close();
        }

        private TextBox? GetTextBox(DaisyNumericUpDown control)
        {
            // Use VisualTree to find the TextBox in the control's template
            // Named PART_DaisyTextBox to avoid base NumericUpDown interference with PART_TextBox
            return control.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Name == "PART_DaisyTextBox");
        }

        [AvaloniaFact]
        public void Hexadecimal_TextBox_Should_Display_Hex_Format_Not_Decimal()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                Value = 255
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Critical: The text should be "0xFF" or "FF", NOT "255"
            Assert.DoesNotContain("255", textBox.Text ?? "");
            Assert.True(
                textBox.Text?.Contains("FF", StringComparison.OrdinalIgnoreCase) ?? false,
                $"Expected hex format containing 'FF', but got: {textBox.Text}");
        }

        [AvaloniaFact]
        public void Binary_TextBox_Should_Display_Binary_Format_Not_Decimal()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Binary,
                Value = 10
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Critical: The text should be "0b1010" or "1010", NOT "10"
            Assert.True(
                textBox.Text?.Contains("1010") ?? false,
                $"Expected binary format containing '1010', but got: {textBox.Text}");
        }

        [AvaloniaFact]
        public void ThousandSeparators_TextBox_Should_Display_Formatted()
        {
            var control = new DaisyNumericUpDown
            {
                ShowThousandSeparators = true,
                Value = 1234567
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Should contain separator (comma or dot depending on locale)
            // At minimum, shouldn't be plain "1234567"
            var text = textBox.Text ?? "";
            Assert.True(
                text.Contains(",") || text.Contains(".") || text.Contains(" "),
                $"Expected thousand separators, but got plain: {text}");
        }

        [AvaloniaFact]
        public void Hexadecimal_With_ShowBasePrefix_Should_Include_0x()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                ShowBasePrefix = true,
                Value = 255
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.StartsWith("0x", textBox.Text ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [AvaloniaFact]
        public void Hexadecimal_Without_ShowBasePrefix_Should_Not_Include_0x()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                ShowBasePrefix = false,
                Value = 255
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.DoesNotContain("0x", textBox.Text ?? "");
            Assert.Equal("FF", textBox.Text);
        }

        [AvaloniaFact]
        public void Binary_With_ShowBasePrefix_Should_Include_0b()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Binary,
                ShowBasePrefix = true,
                Value = 10
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.StartsWith("0b", textBox.Text ?? "", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Value Stability Tests (Preventing Double Conversion Bugs)

        [AvaloniaFact]
        public void Hex_Value_Should_Remain_Stable_After_Multiple_Value_Changes()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                Value = 255
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Simulate multiple increments/decrements that return to original value
            // This exercises the display update code paths
            control.Value = 256;
            control.Value = 255;
            control.Value = 254;
            control.Value = 255;

            // Value should be exactly 255, and display should be hex
            Assert.Equal(255m, control.Value);
            Assert.True(textBox.Text?.Contains("FF", StringComparison.OrdinalIgnoreCase) ?? false,
                $"After multiple changes, expected hex 'FF', got: {textBox.Text}");
        }

        [AvaloniaFact]
        public void Incrementing_Hex_Value_Should_Stay_In_Hex()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Hexadecimal,
                Value = 255,
                Increment = 1
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Initial check
            var initialText = textBox.Text;
            Assert.True(initialText?.Contains("FF", StringComparison.OrdinalIgnoreCase) ?? false,
                $"Initial text should be hex, got: {initialText}");

            // Increment the value (simulating button click)
            control.Value = 256;

            // After increment, should show 0x100, NOT "256"
            var afterText = textBox.Text;
            Assert.DoesNotContain("256", afterText ?? "");
            Assert.True(afterText?.Contains("100", StringComparison.OrdinalIgnoreCase) ?? false,
                $"After increment, expected hex '100', got: {afterText}");
        }

        [AvaloniaFact]
        public void Decrementing_Binary_Value_Should_Stay_In_Binary()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Binary,
                Value = 10,  // binary: 1010
                Increment = 1
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Decrement to 9 (binary: 1001)
            control.Value = 9;

            var text = textBox.Text ?? "";
            Assert.DoesNotContain("9", text.Replace("1001", "")); // "9" shouldn't appear except as part of 1001
            Assert.Contains("1001", text);
        }

        [AvaloniaFact]
        public void Changing_NumberBase_Should_Update_Display_Correctly()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Decimal,
                Value = 255
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Start in decimal - should show "255"
            Assert.Equal("255", textBox.Text);

            // Switch to hex
            control.NumberBase = DaisyNumberBase.Hexadecimal;

            // Should now show hex format
            Assert.True(textBox.Text?.Contains("FF", StringComparison.OrdinalIgnoreCase) ?? false,
                $"After switching to hex, expected 'FF', got: {textBox.Text}");

            // Switch to binary
            control.NumberBase = DaisyNumberBase.Binary;

            // Should show "11111111" (255 in binary)
            Assert.Contains("11111111", textBox.Text ?? "");
        }

        [AvaloniaFact]
        public void Value_Should_Not_Change_When_Only_NumberBase_Changes()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Decimal,
                Value = 170
            };
            using var window = new WindowScope(control);

            // Switch bases multiple times
            control.NumberBase = DaisyNumberBase.Hexadecimal; // 0xAA
            Assert.Equal(170m, control.Value);

            control.NumberBase = DaisyNumberBase.Binary; // 10101010
            Assert.Equal(170m, control.Value);

            control.NumberBase = DaisyNumberBase.Decimal;
            Assert.Equal(170m, control.Value);
        }

        #endregion

        #region Octal Display Tests

        [AvaloniaFact]
        public void Octal_TextBox_Should_Display_Octal_Format()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Octal,
                Value = 493 // 0o755 in octal (Unix permissions)
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Should display "0o755" or "755", NOT "493"
            Assert.DoesNotContain("493", textBox.Text ?? "");
            Assert.Contains("755", textBox.Text ?? "");
        }

        [AvaloniaFact]
        public void Octal_With_ShowBasePrefix_Should_Include_0o()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Octal,
                ShowBasePrefix = true,
                Value = 493
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.StartsWith("0o", textBox.Text ?? "");
        }

        [AvaloniaFact]
        public void Octal_Unix_Permissions_Should_Display_Correctly()
        {
            // Common Unix permission values
            var testCases = new[]
            {
                (value: 493m, octal: "755"), // rwxr-xr-x
                (value: 420m, octal: "644"), // rw-r--r--
                (value: 511m, octal: "777"), // rwxrwxrwx
                (value: 448m, octal: "700"), // rwx------
            };

            foreach (var (decimalValue, expectedOctal) in testCases)
            {
                var control = new DaisyNumericUpDown
                {
                    NumberBase = DaisyNumberBase.Octal,
                    ShowBasePrefix = false,
                    Value = decimalValue
                };
                using var window = new WindowScope(control);

                var textBox = GetTextBox(control);
                Assert.NotNull(textBox);
                Assert.Equal(expectedOctal, textBox.Text);
            }
        }

        #endregion

        #region ColorHex Display Tests

        [AvaloniaFact]
        public void ColorHex_TextBox_Should_Display_Color_Format()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.ColorHex,
                Value = 16734003 // #FF5733
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Should display color hex, NOT decimal
            Assert.DoesNotContain("16734003", textBox.Text ?? "");
            Assert.Contains("FF5733", textBox.Text?.ToUpperInvariant() ?? "");
        }

        [AvaloniaFact]
        public void ColorHex_Should_Include_Hash_Prefix()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.ColorHex,
                ShowBasePrefix = true,
                Value = 16734003
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.StartsWith("#", textBox.Text ?? "");
        }

        [AvaloniaFact]
        public void ColorHex_Should_Pad_To_6_Digits()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.ColorHex,
                ShowBasePrefix = false,
                Value = 255 // Should be "0000FF", not "FF"
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal(6, textBox.Text?.Length);
            Assert.True(textBox.Text?.EndsWith("FF", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        [AvaloniaFact]
        public void ColorHex_Common_Colors_Should_Display_Correctly()
        {
            var testCases = new[]
            {
                (value: 16711680m, hex: "FF0000"), // Red
                (value: 65280m, hex: "00FF00"),    // Green
                (value: 255m, hex: "0000FF"),      // Blue
                (value: 16777215m, hex: "FFFFFF"), // White
                (value: 0m, hex: "000000"),        // Black
            };

            foreach (var (decimalValue, expectedHex) in testCases)
            {
                var control = new DaisyNumericUpDown
                {
                    NumberBase = DaisyNumberBase.ColorHex,
                    ShowBasePrefix = false,
                    HexCase = DaisyHexCase.Upper,
                    Value = decimalValue
                };
                using var window = new WindowScope(control);

                var textBox = GetTextBox(control);
                Assert.NotNull(textBox);
                Assert.Equal(expectedHex, textBox.Text);
            }
        }

        [AvaloniaFact]
        public void ColorHex_Should_Respect_HexCase()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.ColorHex,
                ShowBasePrefix = false,
                HexCase = DaisyHexCase.Lower,
                Value = 16734003 // ff5733 lowercase
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("ff5733", textBox.Text);
        }

        #endregion

        #region Helper Method Tests

        [AvaloniaFact]
        public void ToHexString_Should_Return_Correct_Format()
        {
            var control = new DaisyNumericUpDown { Value = 255 };

            Assert.Equal("0xFF", control.ToHexString());
            Assert.Equal("FF", control.ToHexString(false));
        }

        [AvaloniaFact]
        public void ToHexString_Should_Respect_HexCase()
        {
            var control = new DaisyNumericUpDown
            {
                Value = 171, // 0xAB
                HexCase = DaisyHexCase.Lower
            };

            Assert.Equal("0xab", control.ToHexString());
            Assert.Equal("ab", control.ToHexString(false));
        }

        [AvaloniaFact]
        public void ToBinaryString_Should_Return_Correct_Format()
        {
            var control = new DaisyNumericUpDown { Value = 10 };

            Assert.Equal("0b1010", control.ToBinaryString());
            Assert.Equal("1010", control.ToBinaryString(false));
        }

        [AvaloniaFact]
        public void ToOctalString_Should_Return_Correct_Format()
        {
            var control = new DaisyNumericUpDown { Value = 493 }; // 755 octal

            Assert.Equal("0o755", control.ToOctalString());
            Assert.Equal("755", control.ToOctalString(false));
        }

        [AvaloniaFact]
        public void ToColorHexString_Should_Return_Padded_Format()
        {
            var control = new DaisyNumericUpDown { Value = 255 }; // Blue

            Assert.Equal("#0000FF", control.ToColorHexString());
            Assert.Equal("0000FF", control.ToColorHexString(false));
        }

        [AvaloniaFact]
        public void ToColorHexString_Should_Respect_HexCase()
        {
            var control = new DaisyNumericUpDown
            {
                Value = 16734003,
                HexCase = DaisyHexCase.Lower
            };

            Assert.Equal("#ff5733", control.ToColorHexString());
            Assert.Equal("ff5733", control.ToColorHexString(false));
        }

        [AvaloniaFact]
        public void ToFormattedString_Should_Use_Current_NumberBase()
        {
            var control = new DaisyNumericUpDown { Value = 255 };

            control.NumberBase = DaisyNumberBase.Decimal;
            Assert.Equal("255", control.ToFormattedString());

            control.NumberBase = DaisyNumberBase.Hexadecimal;
            Assert.Contains("FF", control.ToFormattedString()?.ToUpperInvariant() ?? "");

            control.NumberBase = DaisyNumberBase.Binary;
            Assert.Contains("11111111", control.ToFormattedString() ?? "");

            control.NumberBase = DaisyNumberBase.Octal;
            Assert.Contains("377", control.ToFormattedString() ?? "");

            control.NumberBase = DaisyNumberBase.ColorHex;
            Assert.Contains("0000FF", control.ToFormattedString()?.ToUpperInvariant() ?? "");
        }

        [AvaloniaFact]
        public void Helper_Methods_Should_Return_Null_For_Null_Value()
        {
            var control = new DaisyNumericUpDown { Value = null };

            Assert.Null(control.ToHexString());
            Assert.Null(control.ToBinaryString());
            Assert.Null(control.ToOctalString());
            Assert.Null(control.ToColorHexString());
            Assert.Null(control.ToFormattedString());
        }

        #endregion

        #region Value Stability with New Bases

        [AvaloniaFact]
        public void Switching_To_Octal_Should_Not_Change_Value()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Decimal,
                Value = 493
            };
            using var window = new WindowScope(control);

            control.NumberBase = DaisyNumberBase.Octal;
            Assert.Equal(493m, control.Value);

            var textBox = GetTextBox(control);
            Assert.Contains("755", textBox?.Text ?? "");
        }

        [AvaloniaFact]
        public void Switching_To_ColorHex_Should_Not_Change_Value()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Decimal,
                Value = 16711680 // Red
            };
            using var window = new WindowScope(control);

            control.NumberBase = DaisyNumberBase.ColorHex;
            Assert.Equal(16711680m, control.Value);

            var textBox = GetTextBox(control);
            Assert.Contains("FF0000", textBox?.Text?.ToUpperInvariant() ?? "");
        }

        [AvaloniaFact]
        public void Cycling_Through_All_Bases_Should_Preserve_Value()
        {
            var control = new DaisyNumericUpDown
            {
                Value = 255
            };
            using var window = new WindowScope(control);

            // Cycle through all bases
            control.NumberBase = DaisyNumberBase.Decimal;
            Assert.Equal(255m, control.Value);

            control.NumberBase = DaisyNumberBase.Hexadecimal;
            Assert.Equal(255m, control.Value);

            control.NumberBase = DaisyNumberBase.Binary;
            Assert.Equal(255m, control.Value);

            control.NumberBase = DaisyNumberBase.Octal;
            Assert.Equal(255m, control.Value);

            control.NumberBase = DaisyNumberBase.ColorHex;
            Assert.Equal(255m, control.Value);

            control.NumberBase = DaisyNumberBase.Decimal;
            Assert.Equal(255m, control.Value);
        }

        #endregion

        #region IP Address Tests

        [AvaloniaFact]
        public void IPAddress_Should_Display_Dotted_Notation()
        {
            // 192.168.1.1 = (192 << 24) | (168 << 16) | (1 << 8) | 1 = 3232235777
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = 3232235777m
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("192.168.1.1", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Zero_Should_Display_As_0_0_0_0()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = 0m
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("0.0.0.0", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Max_Should_Display_As_255_255_255_255()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = 4294967295m // uint.MaxValue
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("255.255.255.255", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Common_Values_Should_Display_Correctly()
        {
            var testCases = new[]
            {
                (value: 2130706433m, ip: "127.0.0.1"),     // localhost
                (value: 167772161m, ip: "10.0.0.1"),       // private network
                (value: 3232235521m, ip: "192.168.0.1"),   // common router
                (value: 4294967040m, ip: "255.255.255.0"), // subnet mask
            };

            foreach (var (ipValue, expectedIP) in testCases)
            {
                var control = new DaisyNumericUpDown
                {
                    NumberBase = DaisyNumberBase.IPAddress,
                    Value = ipValue
                };
                using var window = new WindowScope(control);

                var textBox = GetTextBox(control);
                Assert.NotNull(textBox);
                Assert.Equal(expectedIP, textBox.Text);
            }
        }

        [AvaloniaFact]
        public void IPAddress_Value_Should_Not_Change_When_Only_Display_Mode_Changes()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.Decimal,
                Value = 3232235777m // 192.168.1.1
            };
            using var window = new WindowScope(control);

            // Switch to IP mode
            control.NumberBase = DaisyNumberBase.IPAddress;
            Assert.Equal(3232235777m, control.Value);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("192.168.1.1", textBox.Text);

            // Switch back to decimal
            control.NumberBase = DaisyNumberBase.Decimal;
            Assert.Equal(3232235777m, control.Value);
        }

        #endregion

        #region IP Address Per-Octet Increment Tests

        private RepeatButton? GetIncreaseButton(DaisyNumericUpDown control)
        {
            return control.GetVisualDescendants()
                .OfType<RepeatButton>()
                .FirstOrDefault(b => b.Name == "PART_IncreaseButton");
        }

        private RepeatButton? GetDecreaseButton(DaisyNumericUpDown control)
        {
            return control.GetVisualDescendants()
                .OfType<RepeatButton>()
                .FirstOrDefault(b => b.Name == "PART_DecreaseButton");
        }

        private void ClickButton(RepeatButton? button)
        {
            if (button == null) return;
            // Simulate button click by raising the Click event
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(RepeatButton.ClickEvent));
        }

        [AvaloniaFact]
        public void IPAddress_Increment_First_Octet_Cursor_At_Start()
        {
            // Start: 10.20.30.40
            // Cursor at position 0 (in first octet)
            // Increment should change to 11.20.30.40
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (20u << 16) | (30u << 8) | 40u // 10.20.30.40
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("10.20.30.40", textBox.Text);

            // Position cursor in first octet
            textBox.CaretIndex = 1; // Inside "10"

            // Click increase button
            var increaseButton = GetIncreaseButton(control);
            ClickButton(increaseButton);

            // First octet should increment: 10 -> 11
            Assert.Equal("11.20.30.40", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Increment_Second_Octet()
        {
            // Start: 10.20.30.40
            // Cursor in second octet
            // Increment should change to 10.21.30.40
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (20u << 16) | (30u << 8) | 40u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Position cursor in second octet (after "10.")
            textBox.CaretIndex = 4; // Inside "20"

            var increaseButton = GetIncreaseButton(control);
            ClickButton(increaseButton);

            Assert.Equal("10.21.30.40", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Increment_Third_Octet()
        {
            // Start: 10.20.30.40
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (20u << 16) | (30u << 8) | 40u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Position cursor in third octet (after "10.20.")
            textBox.CaretIndex = 7; // Inside "30"

            var increaseButton = GetIncreaseButton(control);
            ClickButton(increaseButton);

            Assert.Equal("10.20.31.40", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Increment_Fourth_Octet()
        {
            // Start: 10.20.30.40
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (20u << 16) | (30u << 8) | 40u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Position cursor in fourth octet (at end)
            textBox.CaretIndex = 11; // Inside "40"

            var increaseButton = GetIncreaseButton(control);
            ClickButton(increaseButton);

            Assert.Equal("10.20.30.41", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Decrement_Second_Octet()
        {
            // Start: 10.20.30.40
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (20u << 16) | (30u << 8) | 40u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);

            // Position cursor in second octet
            textBox.CaretIndex = 4;

            var decreaseButton = GetDecreaseButton(control);
            ClickButton(decreaseButton);

            Assert.Equal("10.19.30.40", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Increment_At_Max_255_Should_Wrap_To_Zero()
        {
            // Start: 10.255.30.40 - second octet at max
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (255u << 16) | (30u << 8) | 40u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("10.255.30.40", textBox.Text);

            // Position cursor in second octet (the 255)
            textBox.CaretIndex = 5;

            var increaseButton = GetIncreaseButton(control);
            ClickButton(increaseButton);

            // Should wrap to 0
            Assert.Equal("10.0.30.40", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Decrement_At_Zero_Should_Wrap_To_255()
        {
            // Start: 10.0.30.40 - second octet at min
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (0u << 16) | (30u << 8) | 40u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("10.0.30.40", textBox.Text);

            // Position cursor in second octet (the 0)
            textBox.CaretIndex = 3;

            var decreaseButton = GetDecreaseButton(control);
            ClickButton(decreaseButton);

            // Should wrap to 255
            Assert.Equal("10.255.30.40", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Multiple_Increments_Same_Octet()
        {
            // Start: 192.168.1.1
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = 3232235777m
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            Assert.NotNull(textBox);
            Assert.Equal("192.168.1.1", textBox.Text);

            // Position cursor in fourth octet
            textBox.CaretIndex = 12;

            var increaseButton = GetIncreaseButton(control);

            // Click 5 times
            for (int i = 0; i < 5; i++)
            {
                ClickButton(increaseButton);
            }

            // 1 + 5 = 6
            Assert.Equal("192.168.1.6", textBox.Text);
        }

        [AvaloniaFact]
        public void IPAddress_Increment_Different_Octets_Sequentially()
        {
            // Start: 10.10.10.10
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = (10u << 24) | (10u << 16) | (10u << 8) | 10u
            };
            using var window = new WindowScope(control);

            var textBox = GetTextBox(control);
            var increaseButton = GetIncreaseButton(control);
            Assert.NotNull(textBox);
            Assert.Equal("10.10.10.10", textBox.Text);

            // Increment first octet
            textBox.CaretIndex = 1;
            ClickButton(increaseButton);
            Assert.Equal("11.10.10.10", textBox.Text);

            // Increment third octet
            textBox.CaretIndex = 7;
            ClickButton(increaseButton);
            Assert.Equal("11.10.11.10", textBox.Text);

            // Increment fourth octet
            textBox.CaretIndex = 11;
            ClickButton(increaseButton);
            Assert.Equal("11.10.11.11", textBox.Text);
        }

        #endregion
    }
}
