using Xunit;

namespace Flowery.NET.Kiosk.Tests;

public class KioskElasticEaseTests
{
    [Fact]
    public void The_Ease_Starts_At_Zero_Ends_At_One_And_Overshoots_Once_Per_Oscillation()
    {
        var ease = new KioskElasticEase { Oscillations = 1, Springiness = 6 };
        Assert.Equal(0, ease.Ease(0), 6);
        Assert.Equal(1, ease.Ease(1), 6);

        var samples = Enumerable.Range(0, 1001).Select(step => ease.Ease(step / 1000.0)).ToArray();
        var overshoots = 0;
        for (var index = 1; index < samples.Length; index++)
        {
            if (samples[index - 1] <= 1 && samples[index] > 1)
                overshoots++;
        }
        Assert.Equal(1, overshoots);
        // With one oscillation and a springiness of 6 the ease overshoots by 11.7 % at about a third of the way.
        Assert.Equal(1.117, samples.Max(), 3);
        Assert.InRange(Array.IndexOf(samples, samples.Max()) / 1000.0, 0.3, 0.33);
    }

    [Fact]
    public void More_Oscillations_Swing_More_Often()
    {
        var calm = new KioskElasticEase { Oscillations = 1, Springiness = 6 };
        var lively = new KioskElasticEase { Oscillations = 3, Springiness = 6 };
        Assert.True(Crossings(lively) > Crossings(calm));
    }

    private static int Crossings(KioskElasticEase ease)
    {
        var samples = Enumerable.Range(0, 1001).Select(step => ease.Ease(step / 1000.0) - 1).ToArray();
        return Enumerable.Range(1, samples.Length - 2).Count(index => Math.Sign(samples[index - 1]) != Math.Sign(samples[index]) && samples[index] != 0);
    }
}
