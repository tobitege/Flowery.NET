using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Custom;
using Xunit;

namespace Flowery.NET.Tests
{
    [Collection("LocalizationTests")]
    public class DaisyInputTests
    {
        [AvaloniaFact]
        public void Inputs_Start_With_Their_Defaults_And_Apply_Their_Templates()
        {
            var select = new DaisySelect();
            var maskInput = new DaisyMaskInput { Mask = "00:00:00", PlaceholderText = "00:00:00" };
            Assert.Equal(DaisySelectVariant.Bordered, select.Variant);
            Assert.True(new DaisyTextArea().AcceptsReturn);
            Assert.Equal(DaisyInputVariant.Bordered, maskInput.Variant);
            Assert.Equal(DaisySize.Medium, maskInput.Size);

            using var window = new TestWindow(select);
            Assert.NotEmpty(select.GetVisualChildren());
            window.Show(maskInput);
            Assert.NotEmpty(maskInput.GetVisualChildren());
        }

        [AvaloniaFact]
        public void Mask_Modes_Place_Typed_Digits_Into_Their_Mask_And_Drop_The_Rest()
        {
            var dateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;
            var cases = new (DaisyMaskInputMode Mode, int YearDigits, string Typed, string Text)[]
            {
                (DaisyMaskInputMode.Timer, 2, "123456", "12:34:56"),
                (DaisyMaskInputMode.Timer, 2, "12ab3456", "12:34:56"),
                (DaisyMaskInputMode.CreditCardNumber, 2, "1234567890123456", "1234 5678 9012 3456"),
                (DaisyMaskInputMode.CreditCardNumber, 2, "12345678901234567890", "1234 5678 9012 3456"),
                // The mask's slash is the date separator of the current culture.
                (DaisyMaskInputMode.ExpiryDate, 2, "1226", $"12{dateSeparator}26"),
                (DaisyMaskInputMode.ExpiryDate, 4, "122026", $"12{dateSeparator}2026"),
                (DaisyMaskInputMode.Cvc, 2, "12345", "123")
            };
            using var window = new TestWindow(new Border());
            foreach (var (mode, yearDigits, typed, text) in cases)
            {
                var input = new DaisyMaskInput { Mode = mode, ExpiryYearDigits = yearDigits };
                window.Show(input);
                input.Focus();
                foreach (var character in typed)
                    window.Window.KeyTextInput(character.ToString());

                Assert.True(text == input.Text, $"Typing '{typed}' into {mode} gave '{input.Text}', not '{text}'.");
            }
        }
    }
}
