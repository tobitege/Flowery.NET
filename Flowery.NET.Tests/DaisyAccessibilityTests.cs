﻿using System;
using System.Globalization;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.HeadlessTestingFramework;
using Avalonia.HeadlessTestingFramework.Appium;
using Flowery.Controls;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests
{
    // Run sequentially to minimize interference
    [Collection("LocalizationTests")]
    public class DaisyAccessibilityTests : IDisposable
    {
        private readonly CultureInfo _originalCulture;

        public DaisyAccessibilityTests()
        {
            _originalCulture = FloweryLocalization.CurrentCulture;
            FloweryLocalization.SetCulture("en-US");
        }

        public void Dispose()
        {
            FloweryLocalization.SetCulture(_originalCulture);
        }

        [AvaloniaFact]
        public void Status_Controls_Name_Themselves_Until_AccessibleText_Replaces_The_Name()
        {
            var cases = new (Control Control, string Name)[]
            {
                (new DaisyLoading(), "Loading"),
                (new DaisySkeleton(), "Loading placeholder"),
                (new DaisyProgress(), "Progress"),
                (new DaisyRadialProgress(), "Progress"),
                (new DaisyCountdown(), "Countdown"),
                (new DaisyRating(), "Rating"),
                (new DaisyStatusIndicator { Color = DaisyColor.Success }, "Online")
            };
            using var window = new TestWindow(new Border());
            foreach (var (control, name) in cases)
            {
                window.Show(control);
                var type = control.GetType().Name;
                Assert.Null(DaisyAccessibility.GetAccessibleText(control));
                Assert.True(name == AutomationProperties.GetName(control), $"{type} is named '{AutomationProperties.GetName(control)}'.");

                DaisyAccessibility.SetAccessibleText(control, "Custom name");
                Assert.True("Custom name" == AutomationProperties.GetName(control), $"{type} kept '{AutomationProperties.GetName(control)}'.");
            }

            // The colour of a status indicator decides what it announces, whether it is set first or changed later.
            var indicator = new DaisyStatusIndicator();
            window.Show(indicator);
            foreach (var (color, text) in new[]
            {
                (DaisyColor.Success, "Online"),
                (DaisyColor.Error, "Error"),
                (DaisyColor.Warning, "Warning"),
                (DaisyColor.Info, "Information"),
                (DaisyColor.Primary, "Active"),
                (DaisyColor.Neutral, "Status")
            })
            {
                indicator.Color = color;
                Assert.Equal(text, AutomationProperties.GetName(indicator));
                var fresh = new DaisyStatusIndicator { Color = color };
                window.Show(fresh);
                Assert.Equal(text, AutomationProperties.GetName(fresh));
                window.Show(indicator);
            }
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(new DaisyCountdown()));
        }

        [AvaloniaFact]
        public void Automation_Peers_Report_The_Role_And_The_Current_Value()
        {
            using var window = new TestWindow(new Border());

            Assert.Equal(AutomationControlType.ProgressBar, Peer(window, new DaisyLoading()).GetAutomationControlType());
            Assert.Equal(AutomationControlType.StatusBar, Peer(window, new DaisyStatusIndicator()).GetAutomationControlType());

            var progress = new DaisyProgress { Value = 50, Minimum = 0, Maximum = 100 };
            var peer = Peer(window, progress);
            Assert.Contains("50%", peer.GetName());
            progress.Value = 75;
            DaisyAccessibility.SetAccessibleText(progress, "Uploading file");
            Assert.Contains("Uploading file", peer.GetName());
            Assert.Contains("75%", peer.GetName());

            Assert.Contains("70%", Peer(window, new DaisyRadialProgress { Value = 70 }).GetName());
            Assert.Contains("30", Peer(window, new DaisyCountdown { Value = 30 }).GetName());
            Assert.Contains("seconds", Peer(window, new DaisyCountdown { ClockUnit = CountdownClockUnit.Seconds }).GetName());

            var rating = new DaisyRating { Value = 3, Maximum = 5 };
            peer = Peer(window, rating);
            Assert.Equal(AutomationControlType.Slider, peer.GetAutomationControlType());
            Assert.Contains("3", peer.GetName());
            Assert.Contains("5", peer.GetName());
            Assert.Contains("stars", peer.GetName());
            rating.Value = 4;
            DaisyAccessibility.SetAccessibleText(rating, "Product rating");
            Assert.Contains("Product rating", peer.GetName());
            Assert.Contains("4", peer.GetName());
        }

        [AvaloniaFact]
        public void Accessibility_Helpers_Fall_Back_To_The_Default_And_Forward_Form_Metadata()
        {
            var loading = new DaisyLoading();
            Assert.Equal("Default", DaisyAccessibility.GetEffectiveAccessibleText(loading, "Default"));
            loading.AccessibleText = "Custom text";
            Assert.Equal("Custom text", DaisyAccessibility.GetEffectiveAccessibleText(loading, "Default"));

            var label = new TextBlock { Text = "Account password" };
            var source = new Border();
            var target = new TextBox();
            AutomationProperties.SetName(source, "Password");
            AutomationProperties.SetHelpText(source, "At least twelve characters");
            AutomationProperties.SetLabeledBy(source, label);
            AutomationProperties.SetAutomationId(source, "account-password");
            AutomationProperties.SetIsRequiredForForm(source, true);

            DaisyAccessibility.ApplyAutomationProperties(source, target, automationIdSuffix: "input");

            Assert.Equal("Password", AutomationProperties.GetName(target));
            Assert.Equal("At least twelve characters", AutomationProperties.GetHelpText(target));
            Assert.Same(label, AutomationProperties.GetLabeledBy(target));
            Assert.Equal("account-password-input", AutomationProperties.GetAutomationId(target));
            Assert.True(AutomationProperties.GetIsRequiredForForm(target));
        }

        [AvaloniaFact]
        public void Template_Parts_Are_Found_By_Automation_Id_And_Named_After_Their_Owner()
        {
            // UI automation finds the parts by the owner's automation id, not by their template names.
            var numberFlow = new DaisyNumberFlow
            {
                Value = 12.5m,
                FormatString = "0.0",
                Culture = CultureInfo.InvariantCulture,
                Prefix = "$",
                Suffix = " kg",
                ShowControls = true
            };
            AutomationProperties.SetName(numberFlow, "Quantity");
            AutomationProperties.SetAutomationId(numberFlow, "quantity");
            using var window = new TestWindow(numberFlow);
            var driver = new AvaloniaDriver(window.Window);

            var peer = ControlAutomationPeer.CreatePeerForElement(numberFlow);
            Assert.Equal(AutomationControlType.Spinner, peer.GetAutomationControlType());
            Assert.Equal("Quantity: $12.5 kg", peer.GetName());
            AssertNamed(driver, "quantity-increment", "Increase Quantity");
            AssertNamed(driver, "quantity-decrement", "Decrease Quantity");

            var numberInput = new DaisyNumericUpDown { PlaceholderText = "Amount", ShowClearButton = true };
            AutomationProperties.SetAutomationId(numberInput, "amount");
            window.Show(numberInput);
            AssertNamed(driver, "amount-input", "Amount");
            AssertNamed(driver, "amount-increment", "Increase Amount");
            AssertNamed(driver, "amount-decrement", "Decrease Amount");
            AssertNamed(driver, "amount-clear", "Clear Amount");

            var carousel = new DaisyCarousel();
            AutomationProperties.SetAutomationId(carousel, "gallery");
            window.Show(carousel);
            AssertNamed(driver, "gallery-previous", "Previous slide");
            AssertNamed(driver, "gallery-next", "Next slide");

            var stack = new DaisyStack { ShowNavigation = true };
            stack.Items.Add(new Border());
            AutomationProperties.SetAutomationId(stack, "stack");
            window.Show(stack);
            AssertNamed(driver, "stack-previous", "Previous item");
            AssertNamed(driver, "stack-next", "Next item");
        }

        [AvaloniaFact]
        public void Password_Box_Forwards_Its_Form_Metadata_And_Renames_The_Reveal_Button_When_Clicked()
        {
            var label = new TextBlock { Text = "Account password" };
            var passwordBox = new DaisyPasswordBox
            {
                Label = "Password",
                HelperText = "At least twelve characters",
                IsRequired = true
            };
            AutomationProperties.SetAutomationId(passwordBox, "account-password");
            AutomationProperties.SetLabeledBy(passwordBox, label);
            using var window = new TestWindow(new StackPanel { Children = { label, passwordBox } });
            var driver = new AvaloniaDriver(window.Window);

            var input = driver.FindElement(By.AutomationId("account-password-input")).Control;
            Assert.Equal("Password", AutomationProperties.GetName(input));
            Assert.Equal("At least twelve characters", AutomationProperties.GetHelpText(input));
            Assert.Same(label, AutomationProperties.GetLabeledBy(input));
            Assert.True(AutomationProperties.GetIsRequiredForForm(input));

            AssertNamed(driver, "account-password-reveal", "Show password");
            driver.FindElement(By.AutomationId("account-password-reveal")).Control.PointerClick();
            AssertNamed(driver, "account-password-reveal", "Hide password");
        }

        [AvaloniaFact]
        public void Otp_Input_Slots_Know_Their_Position()
        {
            var input = new DaisyOtpInput { Length = 4 };
            AutomationProperties.SetName(input, "Security code");
            AutomationProperties.SetHelpText(input, "Enter the code from your authenticator");
            AutomationProperties.SetAutomationId(input, "security-code");
            var slots = input.Children.OfType<TextBox>().ToArray();

            Assert.Equal(4, slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                Assert.Equal($"Security code, digit {i + 1} of 4", AutomationProperties.GetName(slots[i]));
                Assert.Equal($"security-code-slot-{i + 1}", AutomationProperties.GetAutomationId(slots[i]));
                Assert.Equal(i + 1, AutomationProperties.GetPositionInSet(slots[i]));
                Assert.Equal(4, AutomationProperties.GetSizeOfSet(slots[i]));
                Assert.Equal("Enter the code from your authenticator", AutomationProperties.GetHelpText(slots[i]));
            }
        }

        private static AutomationPeer Peer(TestWindow window, Control control)
        {
            window.Show(control);
            return ControlAutomationPeer.CreatePeerForElement(control);
        }

        private static void AssertNamed(AvaloniaDriver driver, string automationId, string name) =>
            Assert.Equal(name, driver.FindElement(By.AutomationId(automationId)).GetAttribute("accessibilityname"));
    }
}
