using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

public class Given_RangeAutomation
{
    [Fact]
    public async Task When_Setting_Values_Through_Automation_DaisyRange_Matches_The_Native_Slider()
    {
        var slider = new DaisyRange { Minimum = 10, Maximum = 20, Value = 12 };
        var native = new Slider { Minimum = 10, Maximum = 20, Value = 12 };
        await RuntimeTestHelpers.AttachAsync(new StackPanel { Children = { slider, native } });

        foreach (var enabled in new[] { true, false })
        foreach (var value in new[] { 17d, 9d, 21d, double.NaN })
        {
            slider.IsEnabled = native.IsEnabled = enabled;
            Assert.True(RangeOutcome(slider, value) == RangeOutcome(native, value),
                $"DaisyRange differs from native Slider for {value}, enabled={enabled}.");
        }
    }

    private static (Type? Error, double Value) RangeOutcome(Slider slider, double value)
    {
        slider.Value = 12;
        var range = ControlAutomationPeer.CreatePeerForElement(slider) as IRangeValueProvider;
        Assert.NotNull(range);
        try
        {
            range.SetValue(value);
            return (null, slider.Value);
        }
        catch (Exception error)
        {
            return (error.GetType(), slider.Value);
        }
    }
}
