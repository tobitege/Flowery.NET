using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;
using Flowery.Controls;

namespace Flowery.Helpers;

internal class DaisyRangeAutomationPeer(RangeBase owner) : RangeBaseAutomationPeer(owner), IRangeValueProvider
{
    public override bool IsReadOnly => Owner is DaisyRating { IsReadOnly: true };
    double IRangeValueProvider.SmallChange => Owner is DaisyRating rating
        ? rating.Precision switch { RatingPrecision.Half => .5, RatingPrecision.Precise => .1, _ => 1 }
        : SmallChange;

    void IRangeValueProvider.SetValue(double value)
    {
        if (!Owner.IsEffectivelyEnabled) throw new InvalidOperationException("The control is disabled.");
        if (IsReadOnly) throw new InvalidOperationException("The value is read-only.");
        if (!double.IsFinite(value) || value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
        Owner.SetCurrentValue(RangeBase.ValueProperty, value);
    }

    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
}
