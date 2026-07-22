using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests
{
    [Collection("LocalizationTests")]
    public class ButtonIconTests : IDisposable
    {
        private readonly CultureInfo _originalCulture;

        public ButtonIconTests()
        {
            _originalCulture = FloweryLocalization.CurrentCulture;
        }

        public void Dispose()
        {
            FloweryLocalization.SetCulture(_originalCulture);
        }

        [AvaloniaFact]
        public void DaisyButton_ResolvesSymbolAndSizeAwareSpacing()
        {
            var button = new DaisyButton
            {
                Content = "Save",
                IconSymbol = DaisyIconSymbol.Save,
                Size = Flowery.Controls.DaisySize.Large
            };

            Assert.NotNull(button.EffectiveIconData);
            Assert.True(button.HasUnifiedIcon);
            Assert.Equal(18.0, button.EffectiveIconSize);
            Assert.Equal(8.0, button.EffectiveIconSpacing);
        }

        [AvaloniaFact]
        public void DaisyIconText_PrefersCustomGeometryOverSymbol()
        {
            var customGeometry = StreamGeometry.Parse("M0,0 L10,10");
            var iconText = new DaisyIconText
            {
                IconSymbol = DaisyIconSymbol.Add,
                IconData = customGeometry
            };

            Assert.Same(customGeometry, iconText.EffectiveIconData);
            Assert.True(iconText.HasIcon);
        }

        [AvaloniaFact]
        public void AllIconSymbols_ResolveToGeometry()
        {
            foreach (DaisyIconSymbol symbol in Enum.GetValues(typeof(DaisyIconSymbol)))
            {
                var iconText = new DaisyIconText { IconSymbol = symbol };
                Assert.NotNull(iconText.EffectiveIconData);
            }
        }

        [AvaloniaFact]
        public void DaisyLoginButton_UsesLocalizedLabelAndBrandIcon()
        {
            FloweryLocalization.SetCulture("de");
            var button = new DaisyLoginButton
            {
                Brand = DaisyLoginBrand.Google
            };

            Assert.Equal("Mit Google anmelden", button.Content);
            Assert.NotNull(button.IconLeft);
        }

        [AvaloniaFact]
        public void AllLoginBrands_CreateAnIcon()
        {
            foreach (DaisyLoginBrand brand in Enum.GetValues(typeof(DaisyLoginBrand)))
            {
                var button = new DaisyLoginButton { Brand = brand };
                Assert.NotNull(button.IconLeft);
            }
        }

        [AvaloniaFact]
        public void ButtonIconTemplates_LoadForAllPlacements()
        {
            var panel = new StackPanel();
            foreach (IconPlacement placement in Enum.GetValues(typeof(IconPlacement)))
            {
                panel.Children.Add(new DaisyButton
                {
                    Content = placement.ToString(),
                    IconSymbol = DaisyIconSymbol.Add,
                    IconPlacement = placement
                });
                panel.Children.Add(new DaisyIconText
                {
                    Text = placement.ToString(),
                    IconSymbol = DaisyIconSymbol.Add,
                    IconPlacement = placement
                });
            }

            var window = new Window { Content = panel };
            window.Show();

            Assert.Equal(8, panel.Children.Count);
            window.Close();
        }
    }
}
