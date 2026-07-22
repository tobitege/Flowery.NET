using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Helpers;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyMnemonicTextTests
    {
        [AvaloniaFact]
        public void Should_Expose_Mnemonic_And_Strip_Marker_From_Display()
        {
            var text = new DaisyMnemonicText { Text = "&Texte" };
            var window = new Window { Content = text };
            window.Show();

            Assert.Equal('T', text.MnemonicChar);
            Assert.Equal("Texte", FloweryMnemonicHelpers.GetDisplayText(text.Text));
            Assert.NotNull(text.Inlines);
            Assert.True(text.Inlines.Count > 0);
        }

        [AvaloniaFact]
        public void Should_Keep_Source_Text_With_Markers()
        {
            var button = new DaisyButton { Content = "&Save" };
            var window = new Window { Content = button };
            window.Show();

            Assert.Equal("&Save", button.Content as string);
            Assert.Equal('S', FloweryMnemonicHelpers.GetMnemonicChar(button.Content as string));
        }
    }
}
