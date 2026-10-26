using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Helpers;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyMnemonicTextTests
    {
        [AvaloniaFact]
        public void Text_And_Button_Labels_Underline_The_Mnemonic_And_Keep_The_Source_Text()
        {
            var text = new DaisyMnemonicText { Text = "&Texte" };
            using var window = new TestWindow(text);
            Assert.Equal('T', text.MnemonicChar);
            Assert.Equal("Texte", FloweryMnemonicHelpers.GetDisplayText(text.Text));
            AssertUnderlined(text, "T", "exte");

            var button = new DaisyButton { Content = "&Save" };
            window.Show(button);
            Assert.Equal("&Save", button.Content as string);
            Assert.Equal('S', FloweryMnemonicHelpers.GetMnemonicChar(button.Content as string));
            AssertUnderlined(button.GetVisualDescendants().OfType<DaisyMnemonicText>().Single(), "S", "ave");
        }

        private static void AssertUnderlined(DaisyMnemonicText text, string mnemonic, string rest)
        {
            var runs = Assert.IsAssignableFrom<InlineCollection>(text.Inlines).OfType<Run>().ToList();
            Assert.Equal(mnemonic + rest, string.Concat(runs.Select(run => run.Text)));
            var underlined = Assert.Single(runs, run => run.TextDecorations == TextDecorations.Underline);
            Assert.Equal(mnemonic, underlined.Text);
        }
    }
}
