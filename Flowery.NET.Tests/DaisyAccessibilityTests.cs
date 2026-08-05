using System;
using System.Globalization;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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
        #region DaisyLoading Tests

        [AvaloniaFact]
        public void DaisyLoading_Should_Have_Default_AccessibleText()
        {
            var loading = new DaisyLoading();
            var window = new Window { Content = loading };
            window.Show();

            Assert.Null(loading.AccessibleText);
            Assert.Equal("Loading", AutomationProperties.GetName(loading));
        }

        [AvaloniaFact]
        public void DaisyLoading_Should_Update_AutomationName_When_AccessibleText_Changes()
        {
            var loading = new DaisyLoading();
            var window = new Window { Content = loading };
            window.Show();

            loading.AccessibleText = "Loading your profile";

            Assert.Equal("Loading your profile", AutomationProperties.GetName(loading));
        }

        [AvaloniaFact]
        public void DaisyLoading_AutomationPeer_Should_Return_ProgressBar_Type()
        {
            var loading = new DaisyLoading();
            var window = new Window { Content = loading };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(loading);

            Assert.NotNull(peer);
            Assert.Equal(AutomationControlType.ProgressBar, peer.GetAutomationControlType());
        }

        #endregion

        #region DaisyStatusIndicator Tests

        [AvaloniaFact]
        public void DaisyStatusIndicator_Should_Have_Color_Based_Default_Text()
        {
            var indicator = new DaisyStatusIndicator { Color = DaisyColor.Success };
            var window = new Window { Content = indicator };
            window.Show();

            Assert.Equal("Online", AutomationProperties.GetName(indicator));
        }

        [AvaloniaFact]
        public void DaisyStatusIndicator_Should_Map_Colors_To_Semantic_Text()
        {
            var window = new Window();
            window.Show();

            var testCases = new[]
            {
                (DaisyColor.Success, "Online"),
                (DaisyColor.Error, "Error"),
                (DaisyColor.Warning, "Warning"),
                (DaisyColor.Info, "Information"),
                (DaisyColor.Primary, "Active"),
                (DaisyColor.Neutral, "Status"),
            };

            foreach (var (color, expectedText) in testCases)
            {
                var indicator = new DaisyStatusIndicator { Color = color };
                window.Content = indicator;

                Assert.Equal(expectedText, AutomationProperties.GetName(indicator));
            }
        }

        [AvaloniaFact]
        public void DaisyStatusIndicator_Custom_AccessibleText_Should_Override_Color_Default()
        {
            var indicator = new DaisyStatusIndicator
            {
                Color = DaisyColor.Success,
                AccessibleText = "User is available"
            };
            var window = new Window { Content = indicator };
            window.Show();

            Assert.Equal("User is available", AutomationProperties.GetName(indicator));
        }

        [AvaloniaFact]
        public void DaisyStatusIndicator_AutomationPeer_Should_Return_StatusBar_Type()
        {
            var indicator = new DaisyStatusIndicator();
            var window = new Window { Content = indicator };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(indicator);

            Assert.NotNull(peer);
            Assert.Equal(AutomationControlType.StatusBar, peer.GetAutomationControlType());
        }

        #endregion

        #region DaisyProgress Tests

        [AvaloniaFact]
        public void DaisyProgress_Should_Have_Default_AccessibleText()
        {
            var progress = new DaisyProgress();
            var window = new Window { Content = progress };
            window.Show();

            Assert.Null(progress.AccessibleText);
            Assert.Equal("Progress", AutomationProperties.GetName(progress));
        }

        [AvaloniaFact]
        public void DaisyProgress_AutomationPeer_Should_Include_Percentage()
        {
            var progress = new DaisyProgress { Value = 50, Minimum = 0, Maximum = 100 };
            var window = new Window { Content = progress };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(progress);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("50%", name);
        }

        [AvaloniaFact]
        public void DaisyProgress_AutomationPeer_Should_Include_Custom_Text_With_Percentage()
        {
            var progress = new DaisyProgress
            {
                Value = 75,
                Minimum = 0,
                Maximum = 100,
                AccessibleText = "Uploading file"
            };
            var window = new Window { Content = progress };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(progress);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("Uploading file", name);
            Assert.Contains("75%", name);
        }

        #endregion

        #region DaisyRadialProgress Tests

        [AvaloniaFact]
        public void DaisyRadialProgress_Should_Have_Default_AccessibleText()
        {
            var progress = new DaisyRadialProgress();
            var window = new Window { Content = progress };
            window.Show();

            Assert.Null(progress.AccessibleText);
            Assert.Equal("Progress", AutomationProperties.GetName(progress));
        }

        [AvaloniaFact]
        public void DaisyRadialProgress_AutomationPeer_Should_Include_Percentage()
        {
            var progress = new DaisyRadialProgress { Value = 70 };
            var window = new Window { Content = progress };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(progress);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("70%", name);
        }

        #endregion

        #region DaisyCountdown Tests

        [AvaloniaFact]
        public void DaisyCountdown_Should_Have_Default_AccessibleText()
        {
            var countdown = new DaisyCountdown();
            var window = new Window { Content = countdown };
            window.Show();

            Assert.Null(countdown.AccessibleText);
            Assert.Equal("Countdown", AutomationProperties.GetName(countdown));
        }

        [AvaloniaFact]
        public void DaisyCountdown_AutomationPeer_Should_Include_Value()
        {
            var countdown = new DaisyCountdown { Value = 30 };
            var window = new Window { Content = countdown };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(countdown);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("30", name);
        }

        [AvaloniaFact]
        public void DaisyCountdown_AutomationPeer_Should_Include_Unit_When_ClockUnit_Set()
        {
            var countdown = new DaisyCountdown { ClockUnit = CountdownClockUnit.Seconds };
            var window = new Window { Content = countdown };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(countdown);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("seconds", name);
        }

        [AvaloniaFact]
        public void DaisyCountdown_Should_Have_Polite_LiveSetting()
        {
            var countdown = new DaisyCountdown();
            var window = new Window { Content = countdown };
            window.Show();

            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(countdown));
        }

        #endregion

        #region DaisySkeleton Tests

        [AvaloniaFact]
        public void DaisySkeleton_Should_Have_Default_AccessibleText()
        {
            var skeleton = new DaisySkeleton();
            var window = new Window { Content = skeleton };
            window.Show();

            Assert.Null(skeleton.AccessibleText);
            Assert.Equal("Loading placeholder", AutomationProperties.GetName(skeleton));
        }

        [AvaloniaFact]
        public void DaisySkeleton_Should_Update_AutomationName_When_AccessibleText_Changes()
        {
            var skeleton = new DaisySkeleton();
            var window = new Window { Content = skeleton };
            window.Show();

            skeleton.AccessibleText = "Loading user avatar";

            Assert.Equal("Loading user avatar", AutomationProperties.GetName(skeleton));
        }

        #endregion

        #region DaisyRating Tests

        [AvaloniaFact]
        public void DaisyRating_Should_Have_Default_AccessibleText()
        {
            var rating = new DaisyRating();
            var window = new Window { Content = rating };
            window.Show();

            Assert.Null(rating.AccessibleText);
            Assert.Equal("Rating", AutomationProperties.GetName(rating));
        }

        [AvaloniaFact]
        public void DaisyRating_AutomationPeer_Should_Include_Star_Count()
        {
            var rating = new DaisyRating { Value = 3, Maximum = 5 };
            var window = new Window { Content = rating };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(rating);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("3", name);
            Assert.Contains("5", name);
            Assert.Contains("stars", name);
        }

        [AvaloniaFact]
        public void DaisyRating_AutomationPeer_Should_Return_Slider_Type()
        {
            var rating = new DaisyRating();
            var window = new Window { Content = rating };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(rating);

            Assert.NotNull(peer);
            Assert.Equal(AutomationControlType.Slider, peer.GetAutomationControlType());
        }

        [AvaloniaFact]
        public void DaisyRating_AutomationPeer_Should_Include_Custom_Text()
        {
            var rating = new DaisyRating
            {
                Value = 4,
                Maximum = 5,
                AccessibleText = "Product rating"
            };
            var window = new Window { Content = rating };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(rating);
            var name = peer?.GetName();

            Assert.NotNull(name);
            Assert.Contains("Product rating", name);
            Assert.Contains("4", name);
        }

        #endregion

        #region DaisyAccessibility Helper Tests

        [AvaloniaFact]
        public void DaisyAccessibility_GetEffectiveAccessibleText_Should_Return_Custom_When_Set()
        {
            var loading = new DaisyLoading { AccessibleText = "Custom text" };

            var result = DaisyAccessibility.GetEffectiveAccessibleText(loading, "Default");

            Assert.Equal("Custom text", result);
        }

        [AvaloniaFact]
        public void DaisyAccessibility_GetEffectiveAccessibleText_Should_Return_Default_When_Null()
        {
            var loading = new DaisyLoading();

            var result = DaisyAccessibility.GetEffectiveAccessibleText(loading, "Default");

            Assert.Equal("Default", result);
        }

        [AvaloniaFact]
        public void DaisyAccessibility_ApplyAutomationProperties_Should_Forward_Form_Metadata()
        {
            var label = new TextBlock { Text = "Account password" };
            var source = new Border();
            var target = new TextBox();
            AutomationProperties.SetName(source, "Password");
            AutomationProperties.SetHelpText(source, "At least twelve characters");
            AutomationProperties.SetLabeledBy(source, label);
            AutomationProperties.SetAutomationId(source, "account-password");
            AutomationProperties.SetIsRequiredForForm(source, true);

            DaisyAccessibility.ApplyAutomationProperties(
                source,
                target,
                automationIdSuffix: "input");

            Assert.Equal("Password", AutomationProperties.GetName(target));
            Assert.Equal("At least twelve characters", AutomationProperties.GetHelpText(target));
            Assert.Same(label, AutomationProperties.GetLabeledBy(target));
            Assert.Equal("account-password-input", AutomationProperties.GetAutomationId(target));
            Assert.True(AutomationProperties.GetIsRequiredForForm(target));
        }

        [AvaloniaFact]
        public void DaisyNumberFlow_AutomationPeer_Should_Expose_Formatted_Value_And_Child_Actions()
        {
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
            var window = new Window { Content = numberFlow };
            window.Show();
            numberFlow.UpdateLayout();

            try
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(numberFlow);
                var increment = GetTemplatePart<DaisyButton>(numberFlow, "PART_IncrementButton");
                var decrement = GetTemplatePart<DaisyButton>(numberFlow, "PART_DecrementButton");

                Assert.NotNull(peer);
                Assert.Equal(AutomationControlType.Spinner, peer.GetAutomationControlType());
                Assert.Equal("Quantity: $12.5 kg", peer.GetName());
                Assert.Equal("Increase Quantity", AutomationProperties.GetName(increment));
                Assert.Equal("quantity-increment", AutomationProperties.GetAutomationId(increment));
                Assert.Equal("Decrease Quantity", AutomationProperties.GetName(decrement));
                Assert.Equal("quantity-decrement", AutomationProperties.GetAutomationId(decrement));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void DaisyPasswordBox_Should_Expose_Inner_Input_And_Reveal_Action()
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
            var panel = new StackPanel();
            panel.Children.Add(label);
            panel.Children.Add(passwordBox);
            var window = new Window { Content = panel };
            window.Show();
            passwordBox.UpdateLayout();

            try
            {
                var input = GetTemplatePart<TextBox>(passwordBox, "PART_TextBox");
                var reveal = GetTemplatePart<Button>(passwordBox, "PART_RevealButton");

                Assert.Equal("Password", AutomationProperties.GetName(input));
                Assert.Equal("At least twelve characters", AutomationProperties.GetHelpText(input));
                Assert.Same(label, AutomationProperties.GetLabeledBy(input));
                Assert.Equal("account-password-input", AutomationProperties.GetAutomationId(input));
                Assert.True(AutomationProperties.GetIsRequiredForForm(input));
                Assert.Equal("Show password", AutomationProperties.GetName(reveal));
                Assert.Equal("account-password-reveal", AutomationProperties.GetAutomationId(reveal));

                reveal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal("Hide password", AutomationProperties.GetName(reveal));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void DaisyOtpInput_Should_Expose_Positioned_Named_Slots()
        {
            var input = new DaisyOtpInput { Length = 4 };
            AutomationProperties.SetName(input, "Security code");
            AutomationProperties.SetHelpText(input, "Enter the code from your authenticator");
            AutomationProperties.SetAutomationId(input, "security-code");
            var slots = input.Children.OfType<TextBox>().ToArray();

            Assert.Equal(4, slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.Equal(
                    $"Security code, digit {i + 1} of 4",
                    AutomationProperties.GetName(slots[i]));
                Assert.Equal(
                    $"security-code-slot-{i + 1}",
                    AutomationProperties.GetAutomationId(slots[i]));
                Assert.Equal(i + 1, AutomationProperties.GetPositionInSet(slots[i]));
                Assert.Equal(4, AutomationProperties.GetSizeOfSet(slots[i]));
                Assert.Equal(
                    "Enter the code from your authenticator",
                    AutomationProperties.GetHelpText(slots[i]));
            }
        }

        [AvaloniaFact]
        public void DaisyNumericUpDown_Should_Expose_Editor_And_Icon_Actions()
        {
            var numberInput = new DaisyNumericUpDown
            {
                PlaceholderText = "Amount",
                ShowClearButton = true
            };
            AutomationProperties.SetAutomationId(numberInput, "amount");
            var window = new Window { Content = numberInput };
            window.Show();
            numberInput.UpdateLayout();

            try
            {
                var input = GetTemplatePart<TextBox>(numberInput, "PART_DaisyTextBox");
                var increment = GetTemplatePart<Button>(numberInput, "PART_IncreaseButton");
                var decrement = GetTemplatePart<Button>(numberInput, "PART_DecreaseButton");
                var clear = GetTemplatePart<Button>(numberInput, "PART_ClearButton");

                Assert.Equal("Amount", AutomationProperties.GetName(input));
                Assert.Equal("amount-input", AutomationProperties.GetAutomationId(input));
                Assert.Equal("Increase Amount", AutomationProperties.GetName(increment));
                Assert.Equal("amount-increment", AutomationProperties.GetAutomationId(increment));
                Assert.Equal("Decrease Amount", AutomationProperties.GetName(decrement));
                Assert.Equal("amount-decrement", AutomationProperties.GetAutomationId(decrement));
                Assert.Equal("Clear Amount", AutomationProperties.GetName(clear));
                Assert.Equal("amount-clear", AutomationProperties.GetAutomationId(clear));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Navigation_Controls_Should_Name_Icon_Only_Buttons()
        {
            var carousel = new DaisyCarousel();
            AutomationProperties.SetAutomationId(carousel, "gallery");
            var window = new Window { Content = carousel };
            window.Show();
            carousel.UpdateLayout();

            try
            {
                var previousSlide = GetTemplatePart<Button>(carousel, "PART_PreviousButton");
                var nextSlide = GetTemplatePart<Button>(carousel, "PART_NextButton");
                Assert.Equal("Previous slide", AutomationProperties.GetName(previousSlide));
                Assert.Equal("gallery-previous", AutomationProperties.GetAutomationId(previousSlide));
                Assert.Equal("Next slide", AutomationProperties.GetName(nextSlide));
                Assert.Equal("gallery-next", AutomationProperties.GetAutomationId(nextSlide));

                var stack = new DaisyStack { ShowNavigation = true };
                stack.Items.Add(new Border());
                AutomationProperties.SetAutomationId(stack, "stack");
                window.Content = stack;
                stack.UpdateLayout();

                var previousItem = GetTemplatePart<Button>(stack, "PART_PreviousButton");
                var nextItem = GetTemplatePart<Button>(stack, "PART_NextButton");
                Assert.Equal("Previous item", AutomationProperties.GetName(previousItem));
                Assert.Equal("stack-previous", AutomationProperties.GetAutomationId(previousItem));
                Assert.Equal("Next item", AutomationProperties.GetName(nextItem));
                Assert.Equal("stack-next", AutomationProperties.GetAutomationId(nextItem));
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region Debug Inspection Tests

        /// <summary>
        /// Debug test to inspect what screen readers would see for each control.
        /// Run with debugger attached and check the Output window (Debug.WriteLine).
        /// This test always passes - it's for manual inspection only.
        /// </summary>
        [AvaloniaFact]
        public void Debug_Inspect_All_Accessible_Controls()
        {
            var window = new Window();
            window.Show();

            // DaisyLoading
            var loading = new DaisyLoading { AccessibleText = "Loading your data" };
            window.Content = loading;
            PrintPeerInfo("DaisyLoading", loading);

            // DaisyStatusIndicator - various colors
            foreach (var color in new[] { DaisyColor.Success, DaisyColor.Error, DaisyColor.Warning })
            {
                var indicator = new DaisyStatusIndicator { Color = color };
                window.Content = indicator;
                PrintPeerInfo($"DaisyStatusIndicator ({color})", indicator);
            }

            // DaisyStatusIndicator with custom text
            var customIndicator = new DaisyStatusIndicator { Color = DaisyColor.Success, AccessibleText = "User is online" };
            window.Content = customIndicator;
            PrintPeerInfo("DaisyStatusIndicator (custom)", customIndicator);

            // DaisyProgress
            var progress = new DaisyProgress { Value = 65, AccessibleText = "Uploading file" };
            window.Content = progress;
            PrintPeerInfo("DaisyProgress", progress);

            // DaisyRadialProgress
            var radial = new DaisyRadialProgress { Value = 80, AccessibleText = "Battery level" };
            window.Content = radial;
            PrintPeerInfo("DaisyRadialProgress", radial);

            // DaisyCountdown - with unit
            var countdown = new DaisyCountdown { Value = 45, ClockUnit = CountdownClockUnit.Seconds, AccessibleText = "Time remaining" };
            window.Content = countdown;
            PrintPeerInfo("DaisyCountdown (seconds)", countdown);

            // DaisyCountdown - without unit
            var countdownNoUnit = new DaisyCountdown { Value = 10, AccessibleText = "Countdown" };
            window.Content = countdownNoUnit;
            PrintPeerInfo("DaisyCountdown (no unit)", countdownNoUnit);

            // DaisySkeleton
            var skeleton = new DaisySkeleton { AccessibleText = "Loading user profile" };
            window.Content = skeleton;
            PrintPeerInfo("DaisySkeleton", skeleton);

            // DaisyRating - various values
            var rating = new DaisyRating { Value = 3.5, Maximum = 5, Precision = RatingPrecision.Half, AccessibleText = "Movie rating" };
            window.Content = rating;
            PrintPeerInfo("DaisyRating (3.5/5)", rating);

            var ratingFull = new DaisyRating { Value = 4, Maximum = 5, Precision = RatingPrecision.Full, AccessibleText = "Product rating" };
            window.Content = ratingFull;
            PrintPeerInfo("DaisyRating (4/5)", ratingFull);

            Assert.True(true); // Always passes - this is for inspection only
        }

        private static T GetTemplatePart<T>(Control owner, string name) where T : Control
        {
            return owner.GetVisualDescendants()
                .OfType<T>()
                .Single(control => string.Equals(control.Name, name, StringComparison.Ordinal));
        }

        private static void PrintPeerInfo(string label, Control control)
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            var separator = new string('-', 50);

            System.Diagnostics.Debug.WriteLine(separator);
            System.Diagnostics.Debug.WriteLine($"Control: {label}");
            System.Diagnostics.Debug.WriteLine($"  Name (announced): {peer?.GetName() ?? "(null)"}");
            System.Diagnostics.Debug.WriteLine($"  ControlType:      {peer?.GetAutomationControlType()}");
            System.Diagnostics.Debug.WriteLine($"  ClassName:        {peer?.GetClassName() ?? "(null)"}");
            System.Diagnostics.Debug.WriteLine($"  AutomationId:     {peer?.GetAutomationId() ?? "(null)"}");
            System.Diagnostics.Debug.WriteLine($"  IsContentElement: {peer?.IsContentElement()}");
            System.Diagnostics.Debug.WriteLine($"  IsControlElement: {peer?.IsControlElement()}");

            // Also check AutomationProperties directly
            System.Diagnostics.Debug.WriteLine($"  [Direct] AutomationProperties.Name: {AutomationProperties.GetName(control)}");

            if (control is DaisyCountdown cd)
            {
                System.Diagnostics.Debug.WriteLine($"  [Direct] LiveSetting: {AutomationProperties.GetLiveSetting(cd)}");
            }
        }

        #endregion
    }
}
