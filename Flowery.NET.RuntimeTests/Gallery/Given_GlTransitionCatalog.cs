using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Effects;
using Flowery.NET.Gallery.Effects;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>
/// The SkSL port of the gl-transitions catalog of the Gallery: the uniform parser, the wrapper, the 100 shaders and the
/// engine. The headless tests do not reference the Gallery, so these run here; most of them need no window.
/// </summary>
public class Given_GlTransitionCatalog
{
    private const int W = 64;
    private const int H = 40;

    [Theory]
    [InlineData("uniform float a; // = 4", "4")]
    [InlineData("uniform float uLineWidth;// = 0.1", "0.1")]
    [InlineData("uniform float horizontalHexagons; //= 20;", "20")]
    [InlineData("uniform vec3 color /* = vec3(0.9, 0.4, 0.2) */;", "vec3(0.9, 0.4, 0.2)")]
    [InlineData("uniform float colorPhase; // = 0.4 ; // if 0.0, there is no black phase", "0.4")]
    [InlineData("uniform float strength; // the strength", null)]
    [InlineData("uniform float x; // =", null)]
    [InlineData("uniform float x;", null)]
    public void When_A_Uniform_Line_Is_Read_Its_Default_Is_The_Expression_After_The_Equals_Sign(string line, string? expected)
    {
        Assert.Equal(expected, FloweryCarouselGLTransitions.ExtractDefaultExpression(line));
    }

    [Fact]
    public void When_Numbers_And_Constructor_Arguments_Are_Read_They_Follow_The_Js()
    {
        Assert.Equal(new[] { 0, 0, 0, 0.6 }, FloweryCarouselGLTransitions.ExtractNumbers("0.,0.,0.,.6"));
        Assert.Equal(new[] { -0.01, 3 }, FloweryCarouselGLTransitions.ExtractNumbers("-1e-2 and 3"));
        // The type name holds a digit too, which is why the vector types read their constructor arguments only.
        Assert.Equal(new[] { 4.0, 0, 0, 0, 0.6 }, FloweryCarouselGLTransitions.ExtractNumbers("vec4(0.,0.,0.,.6)"));
        Assert.Equal(new[] { 2.0, 10, 10 }, FloweryCarouselGLTransitions.ExtractNumbers("ivec2(10, 10)"));
        Assert.Equal("10, 10", FloweryCarouselGLTransitions.ExtractConstructorArgs(" ivec2(10, 10) "));
        Assert.Equal("0.5", FloweryCarouselGLTransitions.ExtractConstructorArgs("0.5"));
    }

    [Fact]
    public void When_A_Default_Is_Parsed_It_Follows_The_Uniform_Type()
    {
        Assert.Equal(4.0, FloweryCarouselGLTransitions.ParseDefaultValue("float", "4"));
        Assert.Equal(0.7, FloweryCarouselGLTransitions.ParseDefaultValue("float", ".7"));
        Assert.Null(FloweryCarouselGLTransitions.ParseDefaultValue("float", "none"));
        Assert.Equal(2.0, FloweryCarouselGLTransitions.ParseDefaultValue("int", "2.9"));
        Assert.Equal(-1.0, FloweryCarouselGLTransitions.ParseDefaultValue("int", "-1"));
        // SkSL has no bool uniforms: a former bool uniform is an int whose true or false default reads as 1 or 0.
        Assert.Equal(1.0, FloweryCarouselGLTransitions.ParseDefaultValue("int", "true"));
        Assert.Equal(0.0, FloweryCarouselGLTransitions.ParseDefaultValue("int", "false"));
        Assert.True(FloweryCarouselGLTransitions.ParseDefaultValue("bool", "TRUE") is true);
        Assert.True(FloweryCarouselGLTransitions.ParseDefaultValue("bool", "0") is false);
        Assert.Equal(new[] { 0.5, 0.5 }, FloweryCarouselGLTransitions.ParseDefaultValue("vec2", "vec2(0.5)"));
        Assert.Equal(new[] { 0, 0.2, 0.4, 0.4 }, FloweryCarouselGLTransitions.ParseDefaultValue("vec4", "vec4(0.0, 0.2, 0.4)"));
        Assert.Equal(new[] { 4.0, 4.0 }, FloweryCarouselGLTransitions.ParseDefaultValue("ivec2", "ivec2(4)"));
        Assert.Null(FloweryCarouselGLTransitions.ParseDefaultValue("mat2", "mat2(1.0)"));
        Assert.Null(FloweryCarouselGLTransitions.ParseDefaultValue("float", "  "));
    }

    [Fact]
    public void When_A_Source_Is_Parsed_It_Keeps_Defaults_And_Samplers_And_Skips_Progress_And_Ratio()
    {
        const string source = """
            uniform float a, b; // = 4
            uniform vec3 color /* = vec3(0.9, 0.4, 0.2) */;
            uniform int reverse; // = false;
            uniform float progress; // = 0.5
            uniform float ratio; // = 2
            uniform shader luma;
            uniform float weights[4]; // = 1
            uniform float noDefault;
              uniform ivec2 size; // = ivec2(10, 10)
            float notUniform; // = 3
            """;
        var parsed = FloweryCarouselGLTransitions.ParseUniforms(source.Replace("\n", "\r\n"));

        Assert.Equal(
            ["a:float=4", "b:float=4", "color:vec3=0.9,0.4,0.2", "reverse:int=0", "weights:float=1", "size:ivec2=10,10"],
            parsed.Uniforms.Select(u => u.Name + ":" + u.Type + "=" + Format(u.Value)));
        Assert.Equal(["luma"], parsed.Samplers);
    }

    [Fact]
    public void When_The_Catalog_Loads_It_Has_The_100_Shaders_Of_The_Table()
    {
        Assert.Equal(100, FloweryCarouselGLTransitions.GlTransitions.Length);
        Assert.Equal(FloweryCarouselGLTransitions.GlTransitions.Select(entry => entry.Name).Order(StringComparer.Ordinal),
            CarouselGlTransitionHost.AvailableShaders.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void When_Every_Shader_Is_Wrapped_It_Compiles_And_Gets_A_Value_For_Every_Uniform()
    {
        var failures = new List<string>();
        foreach (var (name, source) in CarouselGlTransitionHost.AvailableShaders)
        {
            using var effect = SKRuntimeEffect.CreateShader(FloweryCarouselGLTransitions.BuildFragmentShader(source), out var error);
            if (effect is null)
            {
                failures.Add(name + ": " + error);
                continue;
            }
            var parsed = FloweryCarouselGLTransitions.ParseUniforms(source);
            // A new SKRuntimeEffectUniforms is uninitialized (GL starts uniforms at 0), so each one needs a value.
            var set = parsed.Uniforms.Select(u => u.Name).Concat(["progress", "ratio", "u_resolution"]).ToHashSet();
            var unset = effect.Uniforms.Where(uniform => !set.Contains(uniform)).ToList();
            if (unset.Count > 0)
                failures.Add(name + ": no value for " + string.Join(", ", unset));
            if (!effect.Children.SequenceEqual(["from", "to", .. parsed.Samplers]))
                failures.Add(name + ": children " + string.Join(", ", effect.Children));
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void When_Every_Transition_Is_Drawn_At_Progress_0_And_1_It_Shows_The_First_And_The_Second_Image()
    {
        using var from = GradientImage(true);
        using var to = GradientImage(false);
        var fromReference = DrawImage(from);
        var toReference = DrawImage(to);
        var failures = new List<string>();
        foreach (var (name, source) in CarouselGlTransitionHost.AvailableShaders)
        {
            var start = RenderTransition(source, 0, from, to);
            var end = RenderTransition(source, 1, from, to);
            if (MaxDifference(start, fromReference) > 2)
                failures.Add(name + " at 0 differs from the first image by " + MaxDifference(start, fromReference));
            if (MaxDifference(end, toReference) > 2)
                failures.Add(name + " at 1 differs from the second image by " + MaxDifference(end, toReference));
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void When_BookFlip_Is_Halfway_It_Shows_The_First_Image_Left_And_The_Second_Right()
    {
        // Regression: at progress 0.5 the skews divide by zero, and mix() kept the NaN of the dropped side, so the
        // frame came out white on Skia's CPU backend.
        using var from = GradientImage(true);
        using var to = GradientImage(false);
        var fromReference = DrawImage(from);
        var toReference = DrawImage(to);
        var frame = RenderTransition(CarouselGlTransitionHost.AvailableShaders["BookFlip"], 0.5f, from, to);
        for (var y = 0; y < H; y += 3)
        {
            for (var x = 0; x < W; x += 3)
            {
                var reference = x < W / 2 ? fromReference : toReference;
                var index = (y * W + x) * 4;
                for (var channel = 0; channel < 4; channel++)
                    Assert.True(Math.Abs(frame[index + channel] - reference[index + channel]) <= 2,
                        $"Pixel ({x}, {y}) is [{string.Join(", ", frame[index..(index + 4)])}].");
            }
        }
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0.25, 1, 0, 0.25)]
    [InlineData(1, 1, 0, 0)]
    [InlineData(1.5, 1, 0, 0.5)]
    [InlineData(0.5, 1, 1, 0)]
    [InlineData(1.5, 1, 1, 0.5)]
    [InlineData(2.5, 1, 1, 1)]
    [InlineData(3, 1, 1, 0)]
    [InlineData(0.0005, 0, 0, 0.5)]
    public void When_Time_Passes_The_Progress_Holds_Rises_And_Holds(double elapsed, double transition, double interval, double expected)
    {
        Assert.Equal(expected, FloweryCarouselGLTransitions.ComputeTransitionProgress(elapsed, transition, interval), 9);
    }

    [Fact]
    public void When_Durations_Are_Resolved_They_Keep_The_Js_Rules()
    {
        Assert.Equal(3, FloweryCarouselGLTransitions.GetTransitionCycleSeconds(1, 1));
        Assert.Equal(0.001, FloweryCarouselGLTransitions.GetTransitionCycleSeconds(0, -2));
        Assert.Equal(1.25, FloweryCarouselGLTransitions.GetTransitionDurationScale(null));
        Assert.Equal(1.25, FloweryCarouselGLTransitions.GetTransitionDurationScale("fade"));
        Assert.Equal(2.5, FloweryCarouselGLTransitions.GetTransitionDurationScale(" Circle "));
        Assert.Equal(1.25, FloweryCarouselGLTransitions.GetTransitionDurationScale("CircleCrop"));
        Assert.Equal(0.75, FloweryCarouselGLTransitions.NormalizeTransitionSeconds(0, 0.6, "fade"), 9);
        Assert.Equal(0.75, FloweryCarouselGLTransitions.NormalizeTransitionSeconds(double.NaN, 0.6, null), 9);
        Assert.Equal(5, FloweryCarouselGLTransitions.NormalizeTransitionSeconds(2, 0.6, "circle"), 9);
    }

    [Fact]
    public void When_Names_Images_And_Paths_Are_Resolved_They_Keep_The_Js_Rules()
    {
        Assert.Equal("angular", FloweryCarouselGLTransitions.ResolveTransitionEntry(null)?.Name);
        Assert.Equal("angular", FloweryCarouselGLTransitions.ResolveTransitionEntry("  ")?.Name);
        Assert.Equal("angular", FloweryCarouselGLTransitions.ResolveTransitionEntry("unknown")?.Name);
        Assert.Equal("fade", FloweryCarouselGLTransitions.ResolveTransitionEntry(" FADE ")?.Name);
        Assert.Contains(FloweryCarouselGLTransitions.ResolveTransitionEntry("Random"), FloweryCarouselGLTransitions.GlTransitions);

        Assert.Equal(["a", "b"], FloweryCarouselGLTransitions.ResolveImages(new Dictionary<string, string> { ["from"] = "a", ["to"] = "b" }));
        Assert.Equal(["a", "b"], FloweryCarouselGLTransitions.ResolveImages(new Dictionary<string, string> { ["from"] = "", ["transitionA"] = "a", ["transitionB"] = "b" }));
        // One image is not a transition: the JS falls back to its default images, which the host never resolves.
        string[] defaults = ["Assets/wallpaper_blue.jpg", "Assets/wallpaper_green.jpg", "Assets/wallpaper_red.jpg", "Assets/wallpaper_yellow.jpg"];
        Assert.Equal(defaults, FloweryCarouselGLTransitions.ResolveImages(new Dictionary<string, string> { ["from"] = "a", ["to"] = "a" }));
        Assert.Equal(defaults, FloweryCarouselGLTransitions.ResolveImages(null));

        Assert.Equal("data:x", FloweryCarouselGLTransitions.ResolveAssetPath("data:x"));
        Assert.Equal("//host/a.png", FloweryCarouselGLTransitions.ResolveAssetPath("//host/a.png"));
        Assert.Equal("https://host/a.png", FloweryCarouselGLTransitions.ResolveAssetPath("https://host/a.png"));
        var error = Assert.Throws<InvalidOperationException>(() => FloweryCarouselGLTransitions.ResolveAssetPath("Assets/a.jpg"));
        Assert.Equal("Image asset must be resolved by the native host: Assets/a.jpg", error.Message);
    }

    [Fact]
    public async Task When_A_Transition_Has_An_Extra_Sampler_It_Samples_The_Solid_Gray_Texture()
    {
        // luma shows the first image while the progress stays below the red of its sampler, 128 / 255. The host holds
        // each image for one second, and one second of transition lasts 1.25 seconds.
        var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 1, TransitionSeconds = 1 };
        try
        {
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(host);
            var first = await host.ReadFrameAsync(0.5);
            var second = await host.ReadFrameAsync(1 + 1.25 + 0.5);
            Assert.False(first.Pixel.SequenceEqual(second.Pixel), "The images must differ at the center.");

            host.TransitionName = "luma";
            AssertPixel(first.Pixel, (await host.ReadFrameAsync(1 + 0.45 * 1.25)).Pixel);
            AssertPixel(second.Pixel, (await host.ReadFrameAsync(1 + 0.55 * 1.25)).Pixel);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await FlushAsync(host.Dispatcher);
        }
    }

    [Fact]
    public async Task When_A_Shader_Is_Missing_The_Host_Shows_The_Js_Message()
    {
        var host = new MissingShaderHost { Width = 160, Height = 100 };
        var failures = new List<Exception>();
        host.RenderFailed += (_, failure) => failures.Add(failure);
        try
        {
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(host);
            await RuntimeTestHelpers.WaitUntilAsync(() => host.LastRenderError is not null, "the renderer failed");
            await RuntimeTestHelpers.WaitForIdleAsync();

            // The load error comes first; the fallback message that follows it reaches a closed renderer. Regression: a
            // request that failed at once answered before the error the canvas had posted, so the host showed the
            // fallback message.
            Assert.Equal("Embedded shader missing: fade", Assert.Single(failures).Message);
            var text = Assert.IsType<TextBlock>(host.Content);
            Assert.Equal("GL renderer: Embedded shader missing: fade", text.Text);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await FlushAsync(host.Dispatcher);
        }
    }

    // A transition host without shader sources, as a page whose shader is missing.
    private sealed class MissingShaderHost : CarouselGlNativeHost
    {
        protected override CarouselGlEngine CreateEngine() => new FloweryCarouselGLTransitions();

        protected override CarouselGlConfiguration? CreateConfiguration() =>
            new("transitions", "transition", "fade", null, new Dictionary<string, string>
            {
                ["from"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg",
                ["to"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_green.jpg"
            }, 0, 1);
    }

    // One frame of a transition in its wrapper, drawn as the engine draws it, as non-premultiplied RGBA.
    private static byte[] RenderTransition(string source, float progress, SKImage from, SKImage to)
    {
        var parsed = FloweryCarouselGLTransitions.ParseUniforms(source);
        using var effect = SKRuntimeEffect.CreateShader(FloweryCarouselGLTransitions.BuildFragmentShader(source), out var error);
        Assert.True(effect is not null, error);
        using var uniforms = new SKRuntimeEffectUniforms(effect) { ["u_resolution"] = new[] { (float)W, H } };
        if (uniforms.Contains("progress")) uniforms["progress"] = progress;
        if (uniforms.Contains("ratio")) uniforms["ratio"] = W / (float)H;
        FloweryCarouselGLTransitions.ApplyUniformDefaults(uniforms, parsed.Uniforms);
        using var gray = SKImage.FromPixelCopy(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul), new byte[] { 128, 128, 128, 255 }, 4)!;
        List<(string Name, SKImage Image)> textures = [("from", from), ("to", to), .. FloweryCarouselGLTransitions.SetupExtraSamplers(effect, parsed.Samplers, gray)];
        var shaders = textures.Select(texture => Texture(texture.Image)).ToList();
        try
        {
            using var children = new SKRuntimeEffectChildren(effect);
            for (var i = 0; i < textures.Count; i++)
                children[textures[i].Name] = shaders[i];
            using var shader = effect.ToShader(uniforms, children);
            using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Src };
            return Draw(canvas => canvas.DrawRect(SKRect.Create(W, H), paint));
        }
        finally
        {
            foreach (var shader in shaders)
                shader.Dispose();
        }
    }

    // The independent reference of an image at progress 0 or 1: the image drawn over the frame with linear sampling.
    private static byte[] DrawImage(SKImage image)
    {
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        return Draw(canvas => canvas.DrawImage(image, SKRect.Create(W, H), new SKSamplingOptions(SKFilterMode.Linear), paint));
    }

    private static byte[] Draw(Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul))!;
        draw(surface.Canvas);
        using var bitmap = new SKBitmap(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Assert.True(surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.Bytes;
    }

    private static int MaxDifference(byte[] a, byte[] b) => a.Zip(b, (x, y) => Math.Abs(x - y)).Max();

    // A gradient with a 4x4 texel checker: the two images differ in every pixel, and neither is symmetric.
    private static SKImage GradientImage(bool first)
    {
        const int width = 32, height = 20;
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5) / width;
                var v = (y + 0.5) / height;
                var checker = (x / 4 + y / 4) % 2 == 0 ? 1.0 : 0.8;
                var (r, g, b) = first ? (0.6 + 0.4 * u, v, 0.1) : (0.1, u, 0.6 + 0.4 * v);
                bitmap.SetPixel(x, y, new SKColor((byte)(r * checker * 255), (byte)(g * checker * 255), (byte)(b * checker * 255)));
            }
        }
        return SKImage.FromBitmap(bitmap);
    }

    // The child shader of a texture, as CarouselGlEngine.CreateTextureShader makes it.
    private static SKShader Texture(SKImage image) =>
        image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Linear),
            SKMatrix.CreateScale(1f / image.Width, 1f / image.Height));

    private static string Format(object value) => value switch
    {
        double[] values => string.Join(",", values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };

    // mix(to, from, 1) can round one level apart from from itself.
    private static void AssertPixel(byte[] expected, byte[] actual, int tolerance = 1)
    {
        for (var channel = 0; channel < 4; channel++)
            Assert.True(Math.Abs(expected[channel] - actual[channel]) <= tolerance,
                $"Expected [{string.Join(", ", expected)}] but read [{string.Join(", ", actual)}].");
    }

    private static Task FlushAsync(Dispatcher dispatcher) =>
        dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background).GetTask();
}
