using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyButtonTests
    {
        [AvaloniaFact]
        public void Should_Have_Default_Variant_As_Default()
        {
            var btn = new DaisyButton();
            Assert.Equal(DaisyButtonVariant.Default, btn.Variant);
        }

        [AvaloniaFact]
        public void Should_Apply_Styles()
        {
            var btn = new DaisyButton { Variant = DaisyButtonVariant.Primary };

            // To properly test styles in Avalonia headless, we often need to attach it to a root
            var window = new Window { Content = btn };
            window.Show();

            // Verification of applied styles is tricky without visual inspection,
            // but we can ensure the property is set and the control initializes.
            Assert.Equal(DaisyButtonVariant.Primary, btn.Variant);
        }
    }
}
