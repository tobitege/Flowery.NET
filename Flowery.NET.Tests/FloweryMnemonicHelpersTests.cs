using Flowery.Helpers;
using Xunit;

namespace Flowery.NET.Tests
{
    public class FloweryMnemonicHelpersTests
    {
        [Theory]
        [InlineData("&Texte", "Texte", 0, 'T')]
        [InlineData("Save && Exit", "Save & Exit", -1, null)]
        [InlineData("&", "", -1, null)]
        public void Parse_WinFormsMnemonicRules(
            string? input,
            string expectedDisplay,
            int expectedIndex,
            char? expectedMnemonic)
        {
            var info = FloweryMnemonicHelpers.Parse(input);

            Assert.Equal(expectedDisplay, info.DisplayText);
            Assert.Equal(expectedIndex, info.MnemonicIndex);
            Assert.Equal(expectedMnemonic, info.MnemonicChar);
            Assert.Equal(expectedDisplay, FloweryMnemonicHelpers.GetDisplayText(input));
            Assert.Equal(expectedMnemonic, FloweryMnemonicHelpers.GetMnemonicChar(input));
        }
    }
}
