using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.HeadlessTestingFramework;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyNumericUpDownTests
    {
        private static readonly DaisyNumberBase[] AllBases = Enum.GetValues<DaisyNumberBase>();

        [AvaloniaFact]
        public void The_Value_Only_Changes_When_It_Is_Set()
        {
            var control = new DaisyNumericUpDown
            {
                Prefix = "$",
                FormatString = "N2",
                Minimum = 0,
                Maximum = 100,
                Increment = 0.01m,
                Value = 99.99m
            };
            using var window = new TestWindow(control);

            control.Prefix = "€";
            control.Suffix = "%";
            control.ShowThousandSeparators = true;
            Assert.Equal(99.99m, control.Value);

            // Programmatic values may leave the bounds; only the spin buttons clamp.
            control.Value = 150;
            Assert.Equal(150m, control.Value);

            foreach (var value in new[] { 255m, 3232235777m })
            {
                control.Value = value;
                foreach (var numberBase in AllBases.Append(DaisyNumberBase.Decimal))
                {
                    control.NumberBase = numberBase;
                    Assert.True(value == control.Value, $"{numberBase} changed {value} to {control.Value}.");
                }
            }

            control.Value = null;
            Assert.Null(control.Value);
        }

        [AvaloniaFact]
        public void The_Text_Shows_The_Value_In_The_Number_Base()
        {
            var control = new DaisyNumericUpDown { ShowThousandSeparators = true, Value = 1234567 };
            using var window = new TestWindow(control);
            var textBox = control.GetByName<TextBox>("PART_DaisyTextBox");

            // The separator depends on the culture.
            Assert.Matches("[,. ]", textBox.Text);
            control.ShowThousandSeparators = false;

            // One control walks through every case, so each row also checks that a change updates the text.
            var cases = new (DaisyNumberBase Base, bool Prefix, DaisyHexCase Case, decimal Value, string Text)[]
            {
                (DaisyNumberBase.Decimal, true, DaisyHexCase.Upper, 255, "255"),
                (DaisyNumberBase.Hexadecimal, true, DaisyHexCase.Upper, 255, "0xFF"),
                (DaisyNumberBase.Hexadecimal, false, DaisyHexCase.Upper, 255, "FF"),
                (DaisyNumberBase.Hexadecimal, true, DaisyHexCase.Upper, 256, "0x100"),
                (DaisyNumberBase.Binary, true, DaisyHexCase.Upper, 10, "0b1010"),
                (DaisyNumberBase.Binary, false, DaisyHexCase.Upper, 9, "1001"),
                (DaisyNumberBase.Binary, false, DaisyHexCase.Upper, 255, "11111111"),
                (DaisyNumberBase.Octal, true, DaisyHexCase.Upper, 493, "0o755"),
                (DaisyNumberBase.Octal, false, DaisyHexCase.Upper, 420, "644"),
                (DaisyNumberBase.Octal, false, DaisyHexCase.Upper, 511, "777"),
                (DaisyNumberBase.Octal, false, DaisyHexCase.Upper, 448, "700"),
                (DaisyNumberBase.ColorHex, true, DaisyHexCase.Upper, 16734003, "#FF5733"),
                (DaisyNumberBase.ColorHex, false, DaisyHexCase.Lower, 16734003, "ff5733"),
                (DaisyNumberBase.ColorHex, false, DaisyHexCase.Upper, 16711680, "FF0000"),
                (DaisyNumberBase.ColorHex, false, DaisyHexCase.Upper, 65280, "00FF00"),
                (DaisyNumberBase.ColorHex, false, DaisyHexCase.Upper, 255, "0000FF"),
                (DaisyNumberBase.ColorHex, false, DaisyHexCase.Upper, 16777215, "FFFFFF"),
                (DaisyNumberBase.ColorHex, false, DaisyHexCase.Upper, 0, "000000"),
                (DaisyNumberBase.IPAddress, true, DaisyHexCase.Upper, 0, "0.0.0.0"),
                (DaisyNumberBase.IPAddress, true, DaisyHexCase.Upper, 3232235777, "192.168.1.1"),
                (DaisyNumberBase.IPAddress, true, DaisyHexCase.Upper, 2130706433, "127.0.0.1"),
                (DaisyNumberBase.IPAddress, true, DaisyHexCase.Upper, 167772161, "10.0.0.1"),
                (DaisyNumberBase.IPAddress, true, DaisyHexCase.Upper, 4294967040, "255.255.255.0"),
                (DaisyNumberBase.IPAddress, true, DaisyHexCase.Upper, 4294967295, "255.255.255.255")
            };
            foreach (var (numberBase, prefix, hexCase, value, text) in cases)
            {
                control.NumberBase = numberBase;
                control.ShowBasePrefix = prefix;
                control.HexCase = hexCase;
                control.Value = value;
                Assert.True(text == textBox.Text,
                    $"{value} in {numberBase} (prefix {prefix}, {hexCase}) showed '{textBox.Text}', not '{text}'.");
            }
        }

        [AvaloniaFact]
        public void The_Formatting_Helpers_Return_Every_Base()
        {
            var control = new DaisyNumericUpDown { Value = 255 };
            Assert.Equal("0xFF", control.ToHexString());
            Assert.Equal("FF", control.ToHexString(false));
            Assert.Equal("#0000FF", control.ToColorHexString());
            Assert.Equal("0000FF", control.ToColorHexString(false));

            var expected = new Dictionary<DaisyNumberBase, string>
            {
                [DaisyNumberBase.Decimal] = "255",
                [DaisyNumberBase.Hexadecimal] = "FF",
                [DaisyNumberBase.Binary] = "11111111",
                [DaisyNumberBase.Octal] = "377",
                [DaisyNumberBase.ColorHex] = "0000FF"
            };
            foreach (var (numberBase, digits) in expected)
            {
                control.NumberBase = numberBase;
                Assert.Contains(digits, control.ToFormattedString()?.ToUpperInvariant());
            }

            control.Value = 10;
            Assert.Equal("0b1010", control.ToBinaryString());
            Assert.Equal("1010", control.ToBinaryString(false));
            control.Value = 493;
            Assert.Equal("0o755", control.ToOctalString());
            Assert.Equal("755", control.ToOctalString(false));

            control.HexCase = DaisyHexCase.Lower;
            control.Value = 171;
            Assert.Equal("0xab", control.ToHexString());
            Assert.Equal("ab", control.ToHexString(false));
            control.Value = 16734003;
            Assert.Equal("#ff5733", control.ToColorHexString());
            Assert.Equal("ff5733", control.ToColorHexString(false));

            control.Value = null;
            Assert.Null(control.ToHexString());
            Assert.Null(control.ToBinaryString());
            Assert.Null(control.ToOctalString());
            Assert.Null(control.ToColorHexString());
            Assert.Null(control.ToFormattedString());
        }

        [AvaloniaFact]
        public void The_Spin_Buttons_Step_The_Octet_At_The_Caret_And_Wrap()
        {
            var control = new DaisyNumericUpDown
            {
                NumberBase = DaisyNumberBase.IPAddress,
                Value = Address(10, 20, 30, 40)
            };
            using var window = new TestWindow(control);
            var textBox = control.GetByName<TextBox>("PART_DaisyTextBox");
            Assert.Equal("10.20.30.40", textBox.Text);

            Step(control, textBox, caret: 1, up: true, "11.20.30.40");
            Step(control, textBox, caret: 4, up: true, "11.21.30.40");
            Step(control, textBox, caret: 7, up: true, "11.21.31.40");
            Step(control, textBox, caret: 11, up: true, "11.21.31.41");
            Step(control, textBox, caret: 4, up: false, "11.20.31.41");

            control.Value = Address(10, 255, 30, 40);
            Step(control, textBox, caret: 5, up: true, "10.0.30.40");
            Step(control, textBox, caret: 3, up: false, "10.255.30.40");

            control.Value = Address(192, 168, 1, 1);
            for (var click = 0; click < 4; click++)
                Step(control, textBox, caret: 12, up: true, $"192.168.1.{click + 2}");
        }

        private static decimal Address(uint a, uint b, uint c, uint d) => (a << 24) | (b << 16) | (c << 8) | d;

        private static void Step(DaisyNumericUpDown control, TextBox textBox, int caret, bool up, string expected)
        {
            textBox.CaretIndex = caret;
            control.GetByName<RepeatButton>(up ? "PART_IncreaseButton" : "PART_DecreaseButton").PointerClick();
            Assert.Equal(expected, textBox.Text);
        }
    }
}
