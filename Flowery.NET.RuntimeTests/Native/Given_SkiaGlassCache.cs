using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Skia;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

public class Given_SkiaGlassCache
{
    private static readonly SkiaGlassLens Lens = new(Refraction: 0, Bezel: .5, Zoom: 1, RimSaturation: 1);

    [Fact]
    public void When_Tint_Radius_Lighting_Or_Lens_Change_The_Skia_Operation_Is_Not_Equal()
    {
        var redraw = new SkiaGlassRedraw(new Border());
        using var original = Operation(redraw);
        // The same values give an equal operation, so an unequal one below comes from the changed value.
        using (var same = Operation(redraw))
            Assert.True(original.Equals(same), "Equal values must give an equal operation.");

        foreach (var (name, changed) in new (string, SkiaGlassDrawOperation)[]
        {
            ("tint", Operation(redraw, tint: Colors.Blue)),
            ("corner radius", Operation(redraw, cornerRadius: 12)),
            ("sheen angle", Operation(redraw, sheenAngle: 90)),
            ("lens", Operation(redraw, lens: Lens with { Refraction = .6 }))
        })
        {
            using (changed)
                Assert.False(original.Equals(changed), $"The Skia operation ignored a change of the {name}.");
        }
    }

    private static SkiaGlassDrawOperation Operation(SkiaGlassRedraw redraw, Color? tint = null, double cornerRadius = 5,
        double sheenAngle = 45, SkiaGlassLens? lens = null) =>
        new(redraw, new Rect(0, 0, 100, 100), blurRadius: 10, tintColor: tint ?? Colors.Red, tintOpacity: .5,
            cornerRadius: cornerRadius, saturation: 1, sheenAngle: sheenAngle, reflectOpacity: .2, depth: .6, curvature: .6,
            bend: .3, dispersion: .2, lens: lens ?? Lens);
}
