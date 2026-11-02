using Avalonia.Animation.Easings;

namespace Flowery.NET.Kiosk;

/// <summary>
/// An elastic ease out with a set number of oscillations and springiness. <see cref="ElasticEaseOut"/> has neither;
/// the data slide pops its cards in with one oscillation and a springiness of 6.
/// </summary>
internal sealed class KioskElasticEase : Easing
{
    public int Oscillations { get; init; } = 3;
    public double Springiness { get; init; } = 3;

    public override double Ease(double progress) => 1 - EaseIn(1 - progress);

    private double EaseIn(double time)
    {
        var oscillations = Math.Max(0, Oscillations);
        var springiness = Math.Max(0, Springiness);
        var expo = springiness == 0 ? time : (Math.Exp(springiness * time) - 1) / (Math.Exp(springiness) - 1);
        return expo * Math.Sin((Math.PI * 2 * oscillations + Math.PI / 2) * time);
    }
}
