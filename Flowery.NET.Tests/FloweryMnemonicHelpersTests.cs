using Flowery.Helpers;
using Xunit;

namespace Flowery.NET.Tests
{
    public class FloweryMnemonicHelpersTests
    {
        [Theory]
        [InlineData(null, "", -1, null)]
        [InlineData("", "", -1, null)]
        [InlineData("Plain", "Plain", -1, null)]
        [InlineData("&Texte", "Texte", 0, 'T')]
        [InlineData("Te&xt / Kalkulation", "Text / Kalkulation", 2, 'x')]
        [InlineData("Save && Exit", "Save & Exit", -1, null)]
        [InlineData("&&Start", "&Start", -1, null)]
        [InlineData("A&B&C", "ABC", 1, 'B')]
        [InlineData("Trailing&", "Trailing", -1, null)]
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
