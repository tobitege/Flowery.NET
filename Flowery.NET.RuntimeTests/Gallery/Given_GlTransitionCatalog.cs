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

    [Fact]
    public async Task When_A_Transition_Has_One_Image_The_Host_Shows_That_Assets_Are_Missing()
    {
        var host = new OneImageHost { Width = 160, Height = 100 };
        try
        {
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(host);
            await RuntimeTestHelpers.WaitUntilAsync(() => host.LastRenderError is not null, "the renderer failed");
            Assert.Equal("Not supported (assets missing)", host.LastRenderError!.Message);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await FlushAsync(host.Dispatcher);
        }
    }

    [Fact]
    public async Task When_Only_The_Transition_Changes_The_Images_Stay_Loaded()
    {
        // The host passes its cached images again with every change; equal assets keep the state and its textures.
        var host = new CarouselGlTransitionHost { Width = 160, Height = 100, TransitionName = "fade", IntervalSeconds = 1, TransitionSeconds = 1 };
        try
        {
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(host);
            await host.ReadFrameAsync(0.5);
            var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
            var state = Assert.IsType<FloweryCarouselGLTransitions.State>(canvas.State);
            var textures = state.textures;

            host.TransitionName = "luma";
            await host.ReadFrameAsync(0.5);
            host.TransitionSeconds = 2;
            await host.ReadFrameAsync(0.5);

            Assert.Same(state, canvas.State);
            Assert.Same(textures, state.textures);
            Assert.Equal("luma", state.transitionName);
            Assert.Null(host.LastRenderError);
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await FlushAsync(host.Dispatcher);
        }
    }

    [Fact]
    public async Task When_The_Images_Change_Or_The_Host_Goes_The_Old_Images_Are_Released()
    {
        var host = new SwitchingImagesHost { Width = 160, Height = 100 };
        try
        {
            RuntimeTestHelpers.AttachToHost(host);
            await RuntimeTestHelpers.EnsureLoadedAsync(host);
            await host.ReadFrameAsync(0.5);
            var canvas = Assert.IsType<CarouselGlCanvas>(host.Content);
            var first = Assert.IsType<FloweryCarouselGLTransitions.State>(canvas.State).textures.ToList();

            // Regression: the engine cached every image it loaded by its data URI until the host went, so each other image
            // set stayed in memory next to the shown one.
            host.ShowSecondImages();
            await host.ReadFrameAsync(0.5);
            var second = Assert.IsType<FloweryCarouselGLTransitions.State>(canvas.State).textures.ToList();
            Assert.All(first, static image => Assert.Equal(IntPtr.Zero, image.Handle));
            Assert.All(second, static image => Assert.NotEqual(IntPtr.Zero, image.Handle));

            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await RuntimeTestHelpers.WaitForIdleAsync();
            Assert.All(second, static image => Assert.Equal(IntPtr.Zero, image.Handle));
        }
        finally
        {
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await FlushAsync(host.Dispatcher);
        }
    }

    [Fact]
    public void When_A_Uniform_Default_Is_Parsed_It_Is_Converted_Once_For_The_Frames()
    {
        var parsed = FloweryCarouselGLTransitions.ParseUniforms("""
            uniform float a; // = 0.5
            uniform int count; // = 3
            uniform int reverse; // = true
            uniform vec2 center; // = vec2(0.25, 0.75)
            uniform ivec2 size; // = ivec2(10, 20)
            """);
        var byName = parsed.Uniforms.ToDictionary(static uniform => uniform.Name);

        Assert.Equal(0.5f, byName["a"].FrameValue);
        Assert.Equal(3, byName["count"].FrameValue);
        Assert.Equal(1, byName["reverse"].FrameValue);
        Assert.Equal(new[] { 0.25f, 0.75f }, Assert.IsType<float[]>(byName["center"].FrameValue));
        Assert.Equal(new[] { 10, 20 }, Assert.IsType<int[]>(byName["size"].FrameValue));
        // The same value every frame.
        Assert.Same(byName["center"].FrameValue, byName["center"].FrameValue);
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
            new(CarouselGlMode.Transition, "fade", null, new Dictionary<string, string>
            {
                ["from"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg",
                ["to"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_green.jpg"
            }, 0, 1);
    }

    // A transition host that switches to two other images.
    private sealed class SwitchingImagesHost : CarouselGlNativeHost
    {
        private string _from = "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg";
        private string _to = "avares://Flowery.NET.Gallery/Assets/wallpaper_green.jpg";

        public void ShowSecondImages()
        {
            _from = "avares://Flowery.NET.Gallery/Assets/wallpaper_red.jpg";
            _to = "avares://Flowery.NET.Gallery/Assets/wallpaper_yellow.jpg";
            RefreshRenderer();
        }

        protected override CarouselGlEngine CreateEngine() => new FloweryCarouselGLTransitions();

        protected override IReadOnlyDictionary<string, string> ShaderSources => CarouselGlTransitionHost.AvailableShaders;

        protected override CarouselGlConfiguration? CreateConfiguration() =>
            new(CarouselGlMode.Transition, "fade", null, new Dictionary<string, string> { ["from"] = _from, ["to"] = _to }, 1, 1);
    }

    // A transition host whose two images are the same, so the transition has one image.
    private sealed class OneImageHost : CarouselGlNativeHost
    {
        protected override CarouselGlEngine CreateEngine() => new FloweryCarouselGLTransitions();

        protected override IReadOnlyDictionary<string, string> ShaderSources => CarouselGlTransitionHost.AvailableShaders;

        protected override CarouselGlConfiguration? CreateConfiguration() =>
            new(CarouselGlMode.Transition, "fade", null, new Dictionary<string, string>
            {
                ["from"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg",
                ["to"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg"
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
