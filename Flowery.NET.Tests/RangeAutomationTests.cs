using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests;

public class RangeAutomationTests
{
    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("DaisyGlassRangeTheme")]
    public void TheSliderIsOneTabStopInEveryTheme(string? theme)
    {
        var range = new DaisyRange { Width = 300 };
        if (theme != null) range.Theme = DaisyGlassSurfaceTests.GlassTheme(theme);
        var window = new Window { Content = range, Width = 400, Height = 150 };
        try
        {
            window.Show();
            window.UpdateLayout();
            // Tab stopped at both halves of the track as well as at the slider itself.
            var halves = range.GetVisualDescendants().OfType<RepeatButton>().ToList();
            Assert.Equal(2, halves.Count);
            Assert.All(halves, half => Assert.False(half.Focusable));
            Assert.True(range.Focusable);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TrackActionsHaveLocalizedNamesAndStableIds()
    {
        var culture = FloweryLocalization.CurrentCulture.Name;
        var range = new DaisyRange { Width = 300 };
        AutomationProperties.SetAutomationId(range, "exposure");
        AutomationProperties.SetName(range, "Exposure");
        var window = new Window { Content = range, Width = 400, Height = 150 };
        try
        {
            FloweryLocalization.SetCulture("en");
            window.Show();
            window.UpdateLayout();
            var increase = range.GetVisualDescendants().OfType<RepeatButton>()
                .Single(button => button.Name == "PART_IncreaseButton");
            var decrease = range.GetVisualDescendants().OfType<RepeatButton>()
                .Single(button => button.Name == "PART_DecreaseButton");
            Assert.Equal("Increase Exposure", ControlAutomationPeer.CreatePeerForElement(increase)!.GetName());
            Assert.Equal("Decrease Exposure", ControlAutomationPeer.CreatePeerForElement(decrease)!.GetName());
            Assert.Equal("exposure.Increase", AutomationProperties.GetAutomationId(increase));
            Assert.Equal("exposure.Decrease", AutomationProperties.GetAutomationId(decrease));
            FloweryLocalization.SetCulture("de");
            AutomationProperties.SetName(range, "Belichtung");
            Assert.Contains("Belichtung", ControlAutomationPeer.CreatePeerForElement(increase)!.GetName());
            Assert.DoesNotContain("Exposure", ControlAutomationPeer.CreatePeerForElement(decrease)!.GetName());
            Assert.Equal("exposure.Increase", AutomationProperties.GetAutomationId(increase));
        }
        finally { window.Close(); FloweryLocalization.SetCulture(culture); }
    }
}
