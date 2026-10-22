using Avalonia;
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
        var type = typeof(DaisyGlass).Assembly.GetType("Flowery.Controls.SkiaGlassDrawOperation", throwOnError: true)!;
        object[] values = [new Rect(0, 0, 100, 100), 10d, Colors.Red, .5d, 5d, 1d, 45d, .2d, .6d, .6d, .3d, .2d];
        using var original = (ICustomDrawOperation)Activator.CreateInstance(type, values)!;
        foreach (var (index, value) in new (int, object)[] { (2, Colors.Blue), (4, 12d), (6, 90d) })
        {
            var changed = (object[])values.Clone();
            changed[index] = value;
            using var operation = (ICustomDrawOperation)Activator.CreateInstance(type, changed)!;
            Assert.False(original.Equals(operation), $"Skia operation ignored visual parameter {index}.");
        }
    }
}
