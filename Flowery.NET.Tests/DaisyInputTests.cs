using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Controls.Custom;
using System.Linq;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisySelectTests
    {
        [AvaloniaFact]
        public void Should_Have_Default_Variant_As_Bordered()
        {
            var select = new DaisySelect();
            Assert.Equal(DaisySelectVariant.Bordered, select.Variant);
        }

        [AvaloniaFact]
        public void Should_Initialize()
        {
            var select = new DaisySelect();
            var window = new Window { Content = select };
            window.Show();

            Assert.NotNull(select);
        }
    }

    public class DaisyTextAreaTests
    {
        [AvaloniaFact]
        public void Should_Have_AcceptsReturn_True()
        {
            var textArea = new DaisyTextArea();
            Assert.True(textArea.AcceptsReturn);
        }
    }

    public class DaisyMaskInputTests
    {
        private static void TypeText(Window window, string text)
        {
            foreach (var c in text)
                window.KeyTextInput(c.ToString());
        }

        [AvaloniaFact]
        public void Should_Have_Default_Variant_As_Bordered()
        {
            var input = new DaisyMaskInput();
            Assert.Equal(DaisyInputVariant.Bordered, input.Variant);
        }

        [AvaloniaFact]
        public void Should_Have_Default_Size_As_Medium()
        {
            var input = new DaisyMaskInput();
            Assert.Equal(DaisySize.Medium, input.Size);
        }

        [AvaloniaFact]
        public void Should_Initialize()
        {
            var input = new DaisyMaskInput
            {
                Mask = "00:00:00",
                PlaceholderText = "00:00:00"
            };

            var window = new Window { Content = input };
            window.Show();

            Assert.NotNull(input);
        }

        [AvaloniaFact]
        public void Should_Apply_Time_Mask_When_Typing()
        {
            var input = new DaisyMaskInput { Mode = DaisyMaskInputMode.Timer };
            var window = new Window { Content = input };
            window.Show();

            input.Focus();
            TypeText(window, "123456");

            var digits = new string((input.Text ?? string.Empty).Where(char.IsDigit).ToArray());
            Assert.Equal("123456", digits);
        }

        [AvaloniaFact]
        public void Should_Ignore_Invalid_Characters_For_Digit_Masks()
        {
            var input = new DaisyMaskInput { Mode = DaisyMaskInputMode.Timer };
            var window = new Window { Content = input };
            window.Show();

            input.Focus();
            TypeText(window, "12ab34");

            var text = input.Text ?? string.Empty;
            Assert.DoesNotContain(text, c => char.IsLetter(c));

            var digits = new string(text.Where(char.IsDigit).ToArray());
            Assert.Equal("1234", digits);
        }

        [AvaloniaFact]
        public void Should_Apply_Card_Number_Mask_When_Typing()
        {
            var input = new DaisyMaskInput { Mode = DaisyMaskInputMode.CreditCardNumber };
            var window = new Window { Content = input };
            window.Show();

            input.Focus();
            TypeText(window, "1234567890123456");

            var digits = new string((input.Text ?? string.Empty).Where(char.IsDigit).ToArray());
            Assert.Equal("1234567890123456", digits);
        }

        [AvaloniaFact]
        public void Should_Not_Accept_More_Digits_Than_Mask_Allows()
        {
            var input = new DaisyMaskInput { Mode = DaisyMaskInputMode.CreditCardNumber };
            var window = new Window { Content = input };
            window.Show();

            input.Focus();
            TypeText(window, "12345678901234567890");

            var digits = new string((input.Text ?? string.Empty).Where(char.IsDigit).ToArray());
            Assert.Equal("1234567890123456", digits);
        }
    }
}
