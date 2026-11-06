using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.HeadlessTestingFramework;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyNumericUpDownTests
    {
        [AvaloniaFact]
        public void The_Text_Shows_The_Value_In_The_Number_Base()
        {
            var control = new DaisyNumericUpDown { ShowThousandSeparators = true, Value = 1234567 };
            using var window = new TestWindow(control);
            var textBox = control.GetByName<TextBox>("PART_DaisyTextBox");

            // The separator is that of the culture.
            using (new CultureScope())
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                control.Value = 7654321;
                Assert.Equal("7.654.321", textBox.Text);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                control.Value = 1234567;
                Assert.Equal("1,234,567", textBox.Text);
            }
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
    }
}
