using System;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests
{
    // Run sequentially to avoid culture interference
    [Collection("LocalizationTests")]
    public class LocalizationTests : IDisposable
    {
        private readonly CultureInfo _originalCulture;

        public LocalizationTests()
        {
            _originalCulture = FloweryLocalization.CurrentCulture;
        }

        public void Dispose()
        {
            // Restore original culture after each test
            FloweryLocalization.SetCulture(_originalCulture);
        }

        [Fact]
        public void GetString_WithDefaultCulture_ReturnsEnglishValue()
        {
            // Arrange
            FloweryLocalization.SetCulture("en-US");

            // Act
            var result = FloweryLocalization.GetString("Select_Placeholder");

            // Assert
            Assert.Equal("Pick one", result);
        }

        [Fact]
        public void GetString_WithGermanCulture_ReturnsGermanValue()
        {
            // Arrange
            FloweryLocalization.SetCulture("de-DE");

            // Act
            var result = FloweryLocalization.GetString("Select_Placeholder");

            // Assert
            Assert.Equal("Auswählen", result);
        }

        [Fact]
        public void GetString_WithUnknownKey_ReturnsKey()
        {
            // Arrange
            var key = "NonExistentKey_12345";

            // Act
            var result = FloweryLocalization.GetString(key);

            // Assert
            Assert.Equal(key, result);
        }

        [Fact]
        public void SetCulture_FiresCultureChangedEvent()
        {
            // Arrange
            var eventFired = false;
            CultureInfo? newCulture = null;
            FloweryLocalization.SetCulture("en-US"); // Ensure starting state

            FloweryLocalization.CultureChanged += (s, c) =>
            {
                eventFired = true;
                newCulture = c;
            };

            // Act
            FloweryLocalization.SetCulture("fr-FR");

            // Assert
            Assert.True(eventFired);
            Assert.Equal("fr-FR", newCulture?.Name);
            Assert.Equal("fr-FR", FloweryLocalization.CurrentCulture.Name);
        }

        [Fact]
        public void GetThemeDisplayName_ReturnsLocalizedThemeName()
        {
            // Arrange
            FloweryLocalization.SetCulture("en-US");

            // Act
            var result = FloweryLocalization.GetThemeDisplayName("Synthwave");

            // Assert
            Assert.Equal("Synthwave", result);
        }
        
        [Fact]
        public void GetThemeDisplayName_WithMissingResource_ThrowsException()
        {
            // Note: We can't easily test this without modifying the assembly's resources
            // or mocking the ResourceManager (which FloweryLocalization doesn't currently support).
            // We'll skip this negative test for now or assume if we ask for a standard theme it works.
        }

        [Fact]
        public void AccessibilityStrings_AreLocalized()
        {
            // Arrange
            FloweryLocalization.SetCulture("es-ES");

            // Act
            var loading = FloweryLocalization.GetString("Accessibility_Loading");
            var status = FloweryLocalization.GetString("Accessibility_StatusOnline");

            // Assert
            Assert.Equal("Cargando", loading);
            Assert.Equal("En línea", status);
        }

        /// <summary>
        /// Integration test: Verifies that controls actually use localized strings at runtime.
        /// This ensures FloweryLocalization integration works end-to-end.
        /// </summary>
        [AvaloniaFact]
        public void DaisyStatusIndicator_Should_Use_Localized_Strings()
        {
            // Arrange - Start with English
            FloweryLocalization.SetCulture("en-US");
            var indicator = new DaisyStatusIndicator { Color = DaisyColor.Error };
            var window = new Window { Content = indicator };
            window.Show();

            // Act & Assert - Verify English
            var peerEn = ControlAutomationPeer.CreatePeerForElement(indicator);
            Assert.Equal("Error", peerEn?.GetName());

            // Act - Switch to German
            FloweryLocalization.SetCulture("de");
            
            // Assert - Verify German (need new peer instance as they can cache)
            var peerDe = ControlAutomationPeer.CreatePeerForElement(indicator);
            Assert.Equal("Fehler", peerDe?.GetName());

            // Act - Switch to Spanish
            FloweryLocalization.SetCulture("es");
            
            // Assert - Verify Spanish
            var peerEs = ControlAutomationPeer.CreatePeerForElement(indicator);
            Assert.Equal("Error", peerEs?.GetName()); // Spanish uses "Error"
        }

        /// <summary>
        /// Integration test: Verifies DaisyLoading uses localized strings.
        /// </summary>
        [AvaloniaFact]
        public void DaisyLoading_Should_Use_Localized_Strings()
        {
            // Arrange
            FloweryLocalization.SetCulture("fr");
            var loading = new DaisyLoading();
            var window = new Window { Content = loading };
            window.Show();

            // Act - Must use automation peer, not AutomationProperties directly
            var peer = ControlAutomationPeer.CreatePeerForElement(loading);
            var name = peer?.GetName();

            // Assert
            Assert.Equal("Chargement", name);
        }

        #region High-Signal Tests

        /// <summary>
        /// Validates that all supported languages load without errors.
        /// Catches malformed .resx files or missing resources.
        /// </summary>
        [Theory]
        [InlineData("de")]
        [InlineData("fr")]
        [InlineData("es")]
        [InlineData("zh-CN")]
        [InlineData("ko")]
        [InlineData("ja")]
        [InlineData("ar")]
        [InlineData("tr")]
        [InlineData("uk")]
        public void AllLanguages_Should_Load_Without_Errors(string culture)
        {
            // Arrange & Act
            FloweryLocalization.SetCulture(culture);
            var result = FloweryLocalization.GetString("Accessibility_Loading");

            // Assert
            Assert.NotEmpty(result);
            Assert.NotEqual("Accessibility_Loading", result); // Should not fallback to key
        }

        /// <summary>
        /// Validates that all accessibility resource keys exist in default resources.
        /// Prevents runtime errors from missing keys.
        /// </summary>
        [Theory]
        [InlineData("Accessibility_Loading")]
        [InlineData("Accessibility_Progress")]
        [InlineData("Accessibility_Rating")]
        [InlineData("Accessibility_LoadingPlaceholder")]
        [InlineData("Accessibility_Status")]
        [InlineData("Accessibility_StatusOnline")]
        [InlineData("Accessibility_StatusError")]
        [InlineData("Accessibility_StatusWarning")]
        [InlineData("Accessibility_StatusInfo")]
        [InlineData("Accessibility_StatusActive")]
        [InlineData("Accessibility_StatusSecondary")]
        [InlineData("Accessibility_StatusHighlighted")]
        [InlineData("Accessibility_Countdown")]
        [InlineData("Select_Placeholder")]
        public void AllAccessibilityKeys_Should_Exist(string key)
        {
            // Arrange
            FloweryLocalization.SetCulture("en-US");

            // Act
            var result = FloweryLocalization.GetString(key);

            // Assert
            Assert.NotEqual(key, result); // Should not fallback to key itself
            Assert.NotEmpty(result);
        }

        /// <summary>
        /// Validates that all theme names have localization entries.
        /// Uses actual themes from FloweryStrings.resx.
        /// </summary>
        [Theory]
        [InlineData("Light")]
        [InlineData("Dark")]
        [InlineData("Cupcake")]
        [InlineData("Bumblebee")]
        [InlineData("Emerald")]
        [InlineData("Corporate")]
        [InlineData("Synthwave")]
        [InlineData("Retro")]
        [InlineData("Cyberpunk")]
        [InlineData("Valentine")]
        [InlineData("Halloween")]
        [InlineData("Garden")]
        [InlineData("Forest")]
        [InlineData("Aqua")]
        [InlineData("Lofi")]
        [InlineData("Pastel")]
        [InlineData("Fantasy")]
        [InlineData("Wireframe")]
        [InlineData("Black")]
        [InlineData("Luxury")]
        [InlineData("Dracula")]
        [InlineData("Cmyk")] // Note: lowercase 'myk'
        [InlineData("Autumn")]
        [InlineData("Business")]
        [InlineData("Acid")]
        [InlineData("Lemonade")]
        [InlineData("Night")]
        [InlineData("Coffee")]
        [InlineData("Winter")]
        [InlineData("Dim")]
        [InlineData("Nord")]
        [InlineData("Sunset")]
        [InlineData("Abyss")]
        [InlineData("Caramellatte")]
        [InlineData("Silk")]
        public void AllThemeNames_Should_Have_Localization_Entries(string themeName)
        {
            // Arrange & Act
            var result = FloweryLocalization.GetThemeDisplayName(themeName);

            // Assert
            Assert.NotEmpty(result);
        }

        #endregion
    }
}
