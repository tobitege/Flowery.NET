using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Flowery.Skia;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

public class Given_SkiaGlassCache
{
    [Fact]
    public void When_Tint_Radius_Lighting_Or_Lens_Change_The_Skia_Operation_Is_Not_Equal()
    {
        var assembly = typeof(FlowerySkia).Assembly;
        var type = assembly.GetType("Flowery.Skia.SkiaGlassDrawOperation", throwOnError: true)!;
        var redrawType = assembly.GetType("Flowery.Skia.SkiaGlassRedraw", throwOnError: true)!;
        var lensType = assembly.GetType("Flowery.Skia.SkiaGlassLens", throwOnError: true)!;
        var redraw = Activator.CreateInstance(redrawType, new Border())!;
        var lens = Activator.CreateInstance(lensType, 0d, .5d, 1d, 1d)!;
        var bentLens = Activator.CreateInstance(lensType, .6d, .5d, 1d, 1d)!;
        object[] values = [redraw, new Rect(0, 0, 100, 100), 10d, Colors.Red, .5d, 5d, 1d, 45d, .2d, .6d, .6d, .3d, .2d, lens];
        using var original = (ICustomDrawOperation)Activator.CreateInstance(type, values)!;
        foreach (var (index, value) in new (int, object)[] { (3, Colors.Blue), (5, 12d), (7, 90d), (13, bentLens) })
        {
            var changed = (object[])values.Clone();
            changed[index] = value;
            using var operation = (ICustomDrawOperation)Activator.CreateInstance(type, changed)!;
            Assert.False(original.Equals(operation), $"Skia operation ignored visual parameter {index}.");
        }
    }
}
