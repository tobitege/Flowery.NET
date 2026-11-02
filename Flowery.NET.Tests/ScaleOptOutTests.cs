using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// A control that does not scale (EnableScaling is false) keeps the fonts of its size when an ancestor carries a scale
/// factor. DaisyInput and DaisyCard applied the inherited factor on every size change; the Kiosk's scaling slide,
/// which sets the size of its demo controls itself, showed inputs at twice their size.
/// </summary>
public class ScaleOptOutTests
{
    [AvaloniaFact]
    public void An_Input_That_Does_Not_Scale_Ignores_The_Factor_Of_A_Scaled_Ancestor()
    {
        var scaled = new DaisyInput();
        var plain = new DaisyInput();
        using var window = new TestWindow(CreatePanels(scaled, plain));

        foreach (var size in new[] { DaisySize.Large, DaisySize.ExtraSmall, DaisySize.ExtraLarge })
        {
            scaled.Size = size;
            plain.Size = size;
            Assert.Equal(plain.ScaledTextFontSize, scaled.ScaledTextFontSize);
            Assert.Equal(plain.ScaledLabelFontSize, scaled.ScaledLabelFontSize);
            Assert.Equal(plain.FontSize, scaled.FontSize);
        }
    }

    [AvaloniaFact]
    public void A_Card_That_Does_Not_Scale_Ignores_The_Factor_Of_A_Scaled_Ancestor()
    {
        var scaled = new DaisyCard();
        var plain = new DaisyCard();
        using var window = new TestWindow(CreatePanels(scaled, plain));

        foreach (var size in new[] { DaisySize.Large, DaisySize.ExtraSmall, DaisySize.ExtraLarge })
        {
            scaled.Size = size;
            plain.Size = size;
            Assert.Equal(plain.TitleFontSize, scaled.TitleFontSize);
            Assert.Equal(plain.BodyFontSize, scaled.BodyFontSize);
        }
    }

    // The factor goes straight onto the ancestor, as FloweryScaleManager sets it on a scaled region; the controls
    // inherit it but do not scale themselves.
    private static StackPanel CreatePanels(Control scaled, Control plain)
    {
        var scaledAncestor = new StackPanel { Children = { scaled } };
        scaledAncestor.SetValue(FloweryScaleManager.ScaleFactorProperty, 2.0);
        Assert.False(FloweryScaleManager.GetEnableScaling(scaled));
        return new StackPanel { Children = { scaledAncestor, new StackPanel { Children = { plain } } } };
    }
}
