using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

public class Given_SkiaGlassCache
{
    [Fact]
    public void When_Tint_Radius_Or_Lighting_Change_The_Skia_Operation_Is_Not_Equal()
    {
        var assembly = typeof(DaisyGlass).Assembly;
        var type = assembly.GetType("Flowery.Controls.SkiaGlassDrawOperation", throwOnError: true)!;
        var redrawType = assembly.GetType("Flowery.Controls.SkiaGlassRedraw", throwOnError: true)!;
        var redraw = Activator.CreateInstance(redrawType, new Border())!;
        object[] values = [redraw, new Rect(0, 0, 100, 100), 10d, Colors.Red, .5d, 5d, 1d, 45d, .2d, .6d, .6d, .3d, .2d];
        using var original = (ICustomDrawOperation)Activator.CreateInstance(type, values)!;
        foreach (var (index, value) in new (int, object)[] { (3, Colors.Blue), (5, 12d), (7, 90d) })
        {
            var changed = (object[])values.Clone();
            changed[index] = value;
            using var operation = (ICustomDrawOperation)Activator.CreateInstance(type, changed)!;
            Assert.False(original.Equals(operation), $"Skia operation ignored visual parameter {index}.");
        }
    }
}
