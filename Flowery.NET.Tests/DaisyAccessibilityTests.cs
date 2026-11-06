using System.Globalization;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests
{
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
        public void Automation_Peers_Report_The_Role_And_The_Current_Value()
        {
            using var window = new TestWindow(new Border());

            Assert.Equal(AutomationControlType.ProgressBar, Peer(window, new DaisyLoading()).GetAutomationControlType());
            Assert.Equal(AutomationControlType.StatusBar, Peer(window, new DaisyStatusIndicator()).GetAutomationControlType());

            var progress = new DaisyProgress { Value = 50, Minimum = 0, Maximum = 100 };
            var peer = Peer(window, progress);
            Assert.Equal("Progress, 50%", peer.GetName());
            progress.Value = 75;
            DaisyAccessibility.SetAccessibleText(progress, "Uploading file");
            Assert.Equal("Uploading file, 75%", peer.GetName());

            Assert.Equal("Progress, 70%", Peer(window, new DaisyRadialProgress { Value = 70 }).GetName());
            Assert.Equal("Countdown: 30", Peer(window, new DaisyCountdown { Value = 30 }).GetName());
            // A clock unit shows the seconds of the current time.
            Assert.Matches("^Countdown: [0-9]{1,2} seconds$", Peer(window, new DaisyCountdown { ClockUnit = CountdownClockUnit.Seconds }).GetName());

            var rating = new DaisyRating { Value = 3, Maximum = 5 };
            peer = Peer(window, rating);
            Assert.Equal(AutomationControlType.Slider, peer.GetAutomationControlType());
            Assert.Equal("Rating: 3 of 5 stars", peer.GetName());
            rating.Value = 4;
            DaisyAccessibility.SetAccessibleText(rating, "Product rating");
            Assert.Equal("Product rating: 4 of 5 stars", peer.GetName());
        }

        [AvaloniaFact]
        public void A_Rating_Announces_Its_Stars_In_The_Current_Language()
        {
            using var culture = new CultureScope();
            FloweryLocalization.SetCulture("de-DE");
            using var window = new TestWindow(new Border());
            Assert.Equal("Bewertung: 3 von 5 Sternen", Peer(window, new DaisyRating { Value = 3, Maximum = 5 }).GetName());
        }

        private static AutomationPeer Peer(TestWindow window, Control control)
        {
            window.Show(control);
            return ControlAutomationPeer.CreatePeerForElement(control);
        }
    }
}
