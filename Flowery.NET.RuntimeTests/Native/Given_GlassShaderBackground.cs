using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Skia;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

/// <summary>
/// The SkSL shader backgrounds of the glass, drawn by the Skia renderer of a real window.
/// </summary>
public class Given_GlassShaderBackground
{
    [Fact]
    public async Task When_The_Background_Or_A_Parent_Is_Half_Transparent_The_Shader_Is_Drawn_Half_Transparent()
    {
        // Regression: Avalonia multiplies opacity into each paint instead of drawing a layer, and the shader paint
        // ignored it, so a shader background drew at full strength instead of at GlassBackgroundOpacity (0.6).
        var layer = new DaisyGlassShaderBackground { Effect = DaisyGlassAnimation.Plasma, Speed = 0 }.CreateLayer();
        layer.Width = 160;
        layer.Height = 80;
        var parent = new Border { Child = layer };
        var backdrop = new Border
        {
            Background = Brushes.Black,
            Child = parent,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        try
        {
            await RuntimeTestHelpers.AttachAsync(backdrop);
            // Without speed the plasma stands still, so every render shows the same frame.
            using var opaque = RuntimeTestHelpers.Render(backdrop, "glass-shader-opaque");
            foreach (var (layerOpacity, parentOpacity, name) in new[] { (0.5, 1.0, "layer"), (1.0, 0.5, "parent"), (0.6, 0.5, "both") })
            {
                layer.Opacity = layerOpacity;
                parent.Opacity = parentOpacity;
                await RuntimeTestHelpers.WaitForRenderAsync();
                using var faded = RuntimeTestHelpers.Render(backdrop, "glass-shader-" + name);
                var opacity = layerOpacity * parentOpacity;
                foreach (var (x, y) in new[] { (20, 20), (80, 40), (140, 60) })
                {
                    var reference = opaque.GetPixel(x, y);
                    var pixel = faded.GetPixel(x, y);
                    // Over black, a shader at an opacity keeps that share of each color.
                    var expected = new Color(255, (byte)Math.Round(reference.Red * opacity),
                        (byte)Math.Round(reference.Green * opacity), (byte)Math.Round(reference.Blue * opacity));
                    Assert.True(RuntimeTestHelpers.IsClose(pixel, expected, 3),
                        $"With the {name} at {opacity}, ({x}, {y}) shows {pixel}; at full opacity it is {reference}, expected {expected}.");
                }
            }
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
        }
    }
}
