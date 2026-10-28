using Avalonia.Headless.XUnit;
using Flowery.Effects;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// The SkSL port of the carousel GL effects: the pure functions of the engine, its programs, the registry, and frames
/// that it draws on detached canvases.
/// </summary>
public class CarouselGlTests
{
    private static readonly string Red = SolidImage(SKColors.Red);
    private static readonly string Blue = SolidImage(SKColors.Blue);

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0.25, 1, 0, 0.25)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1.5, 1, 0, 0.5)]
    [InlineData(2, 1, 0, 0)]
    [InlineData(0.5, 1, 1, 0)]
    [InlineData(1.5, 1, 1, 0.5)]
    [InlineData(2.5, 1, 1, 1)]
    [InlineData(3.75, 1, 1, 0.25)]
    [InlineData(0.0005, 0, 0, 0.5)]
    public void Transition_Progress_Holds_Rises_Holds_And_Falls(double elapsed, double transition, double interval, double expected)
    {
        Assert.Equal(expected, FloweryCarouselGL.ComputeTransitionProgress(elapsed, transition, interval), 9);
    }

    [Fact]
    public void Effect_Params_Follow_The_Effect_Type_And_Stay_In_Range()
    {
        var panZoom = FloweryCarouselGL.GetEffectParams("pan-zoom", 0);
        Assert.Equal(new FloweryCarouselGL.EffectParams(0, 0.06, 1.12, 0, 1), panZoom);
        Assert.Equal(panZoom, FloweryCarouselGL.GetEffectParams("unknown", 0));
        Assert.Equal(panZoom, FloweryCarouselGL.GetEffectParams(null, 0));

        var zoomIn = FloweryCarouselGL.GetEffectParams("zoom-in", Math.PI / 0.6);
        Assert.Equal(1.18, zoomIn.Scale, 9);
        var zoomOut = FloweryCarouselGL.GetEffectParams("zoom-out", 0);
        Assert.Equal(1.18, zoomOut.Scale, 9);

        var throwStart = FloweryCarouselGL.GetEffectParams("throw", 0);
        Assert.Equal((-0.15, 0.12, -0.12, 1.1), (throwStart.OffsetX, throwStart.OffsetY, throwStart.Rotation, throwStart.Scale));
        var throwMiddle = FloweryCarouselGL.GetEffectParams("throw", 2);
        Assert.Equal(0.15, throwMiddle.OffsetX, 9);

        var pulse = FloweryCarouselGL.GetEffectParams("pulse", 3 * Math.PI / 2 / 1.2);
        Assert.Equal(0.8, pulse.Alpha, 9);
        Assert.Equal(0.92, pulse.Scale, 9);
        foreach (var type in FloweryCarouselGL.EffectVariants.Keys)
        {
            for (var t = 0.0; t < 30; t += 0.37)
            {
                var value = FloweryCarouselGL.GetEffectParams(type, t);
                Assert.InRange(value.Alpha, 0, 1);
                Assert.True(value.Scale >= 0.2, type);
            }
        }
    }

    [Fact]
    public void The_Resolve_Functions_Keep_The_Js_Rules()
    {
        Assert.Equal(2.2, FloweryCarouselGL.NormalizeTransitionSeconds(0, 2.2));
        Assert.Equal(2.2, FloweryCarouselGL.NormalizeTransitionSeconds(double.NaN, 2.2));
        Assert.Equal(0.8, FloweryCarouselGL.NormalizeTransitionSeconds(0.8, 2.2));

        Assert.Equal(20, FloweryCarouselGL.ResolvePixelateSize(0));
        Assert.Equal(20, FloweryCarouselGL.ResolvePixelateSize(double.PositiveInfinity));
        Assert.Equal(6.5, FloweryCarouselGL.ResolvePixelateSize(6.5));

        Assert.Equal(0, FloweryCarouselGL.ResolveSliceCount(1.9));
        Assert.Equal(0, FloweryCarouselGL.ResolveSliceCount(double.NaN));
        Assert.Equal(2, FloweryCarouselGL.ResolveSliceCount(2.7));
        Assert.Equal(12, FloweryCarouselGL.ResolveSliceCount(12));

        Assert.True(FloweryCarouselGL.ResolveStaggerFlag(true));
        Assert.True(FloweryCarouselGL.ResolveStaggerFlag("true"));
        Assert.True(FloweryCarouselGL.ResolveStaggerFlag(1));
        Assert.False(FloweryCarouselGL.ResolveStaggerFlag(false));
        Assert.False(FloweryCarouselGL.ResolveStaggerFlag("yes"));
        Assert.False(FloweryCarouselGL.ResolveStaggerFlag(0));
        Assert.False(FloweryCarouselGL.ResolveStaggerFlag(null));

        Assert.Equal(0, FloweryCarouselGL.ResolveStaggerMs(-5));
        Assert.Equal(50, FloweryCarouselGL.ResolveStaggerMs(50));

        Assert.Equal(0.5, FloweryCarouselGL.ResolveDissolveDensity(double.NaN));
        Assert.Equal(1, FloweryCarouselGL.ResolveDissolveDensity(3));
        Assert.Equal(0, FloweryCarouselGL.ResolveDissolveDensity(-1));

        Assert.Equal(180, FloweryCarouselGL.ResolveFlipAngle(double.NaN));
        Assert.Equal(30, FloweryCarouselGL.ResolveFlipAngle(10));
        Assert.Equal(180, FloweryCarouselGL.ResolveFlipAngle(270));
        Assert.Equal(90, FloweryCarouselGL.ResolveFlipAngle(90));

        Assert.Equal("data:x", FloweryCarouselGL.ResolveAssetPath("data:x"));
        Assert.Equal("//host/a.png", FloweryCarouselGL.ResolveAssetPath("//host/a.png"));
        Assert.Equal("https://host/a.png", FloweryCarouselGL.ResolveAssetPath("https://host/a.png"));
        var error = Assert.Throws<InvalidOperationException>(() => FloweryCarouselGL.ResolveAssetPath("Assets/a.png"));
        Assert.Equal("Image asset must be resolved by the native host: Assets/a.png", error.Message);
    }

    [Fact]
    public void The_Settings_Fall_Back_To_Their_First_Variant()
    {
        Assert.Equal(new FloweryCarouselGL.MaskSettings(7, 8), FloweryCarouselGL.GetMaskSettings("wormhole"));
        Assert.Equal(FloweryCarouselGL.GetMaskSettings("checkerboard"), FloweryCarouselGL.GetMaskSettings("missing"));
        Assert.Equal(new FloweryCarouselGL.FlipSettings(0, -1, 0, 1.8, 0, 180), FloweryCarouselGL.GetFlipSettings("cube-left"));
        Assert.Equal(FloweryCarouselGL.GetFlipSettings("flip-plane"), FloweryCarouselGL.GetFlipSettings(null));
        Assert.Equal(FloweryCarouselGL.GetTransitionSettings("fade"), FloweryCarouselGL.GetTransitionSettings(""));
        Assert.Equal(FloweryCarouselGL.GetEffectSettings("pan-zoom"), FloweryCarouselGL.GetEffectSettings("missing"));
        for (var i = 0; i < 50; i++)
            Assert.Contains(FloweryCarouselGL.GetTransitionSettings("random"), FloweryCarouselGL.TransitionVariants.Values);
    }

    [Fact]
    public void The_Images_Of_A_Mode_Are_Its_Keys_Only()
    {
        var assets = new Dictionary<string, string> { ["maskA"] = "a", ["maskB"] = "b", ["effect"] = "e" };
        Assert.Equal(new Dictionary<string, string?> { ["maskA"] = "a", ["maskB"] = "b" }, FloweryCarouselGL.ResolveImages("mask", assets));
        Assert.Equal(new Dictionary<string, string?> { ["flipA"] = null, ["flipB"] = null }, FloweryCarouselGL.ResolveImages("flip", assets));
        Assert.Equal(new Dictionary<string, string?> { ["effect"] = "e" }, FloweryCarouselGL.ResolveImages("effect", assets));
        Assert.Equal(3, FloweryCarouselGL.ResolveImages("other", assets).Count);
        Assert.Null(FloweryCarouselGL.GetPairKeys("effect"));
        Assert.Equal(("transitionA", "transitionB"), FloweryCarouselGL.GetPairKeys("transition"));
    }

    [AvaloniaFact]
    public void A_New_Configuration_Keeps_The_Image_That_Is_Showing()
    {
        var assets = new Dictionary<string, string> { ["maskA"] = "a", ["maskB"] = "b" };
        var state = new FloweryCarouselGL.State(new CarouselGlCanvas());

        Assert.Same(assets, FloweryCarouselGL.ResolveAssetsForCurrentImage("mask", assets, null));
        Assert.Same(assets, FloweryCarouselGL.ResolveAssetsForCurrentImage("mask", assets, state));

        state.currentImage = "b";
        var swapped = FloweryCarouselGL.ResolveAssetsForCurrentImage("mask", assets, state)!;
        Assert.Equal(("b", "a"), (swapped["maskA"], swapped["maskB"]));
        Assert.Equal("a", assets["maskA"]);

        state.currentImage = "a";
        Assert.Same(assets, FloweryCarouselGL.ResolveAssetsForCurrentImage("mask", assets, state));

        state.currentImage = null;
        state.currentSwap = true;
        swapped = FloweryCarouselGL.ResolveAssetsForCurrentImage("mask", assets, state)!;
        Assert.Equal("b", swapped["maskA"]);

        // An effect has no pair, and a pair of one image has nothing to swap.
        Assert.Same(assets, FloweryCarouselGL.ResolveAssetsForCurrentImage("effect", assets, state));
        var same = new Dictionary<string, string> { ["maskA"] = "a", ["maskB"] = "a" };
        Assert.Same(same, FloweryCarouselGL.ResolveAssetsForCurrentImage("mask", same, state));
    }

    [Fact]
    public void Every_Effect_Is_Registered_With_A_Mode_And_Variant_The_Engine_Knows()
    {
        foreach (var kind in Enum.GetValues<CarouselGlEffectKind>().Where(kind => kind != CarouselGlEffectKind.None))
        {
            Assert.True(CarouselGlEffectRegistry.TryGet(kind, out var definition), kind.ToString());
            Assert.NotNull(definition);
            Assert.Equal(kind, definition.Kind);
            var variant = Assert.IsType<string>(definition.Variant);
            var known = definition.Mode switch
            {
                "mask" => FloweryCarouselGL.MaskVariants.ContainsKey(variant),
                "flip" => FloweryCarouselGL.FlipVariants.ContainsKey(variant),
                "transition" => variant == "random" || FloweryCarouselGL.TransitionVariants.ContainsKey(variant),
                "effect" => FloweryCarouselGL.EffectVariants.ContainsKey(variant),
                "text" => variant == "text-fill",
                _ => false
            };
            Assert.True(known, kind + " has the unknown " + definition.Mode + " variant " + variant + ".");
        }
        Assert.False(CarouselGlEffectRegistry.TryGet(CarouselGlEffectKind.None, out _));
    }

    [Fact]
    public void Registering_A_Definition_Replaces_The_Previous_One()
    {
        Assert.True(CarouselGlEffectRegistry.TryGet(CarouselGlEffectKind.Spiral, out var original));
        try
        {
            var assets = new Dictionary<string, string> { ["maskA"] = Red, ["maskB"] = Blue };
            CarouselGlEffectRegistry.Register(new CarouselGlEffectDefinition(CarouselGlEffectKind.Spiral, "mask", assets, "spiral"));
            Assert.True(CarouselGlEffectRegistry.TryGet(CarouselGlEffectKind.Spiral, out var replaced));
            Assert.Same(assets, replaced!.Assets);
        }
        finally
        {
            CarouselGlEffectRegistry.Register(original!);
        }
    }

    [Fact]
    public void Every_SkSL_Program_Compiles()
    {
        foreach (var program in new[]
        {
            FloweryCarouselGL.MaskProgram, FloweryCarouselGL.FlipProgram, FloweryCarouselGL.TransitionProgram,
            FloweryCarouselGL.EffectProgram, FloweryCarouselGL.TextFillProgram
        })
        {
            Assert.NotNull(program.Value);
        }
        Assert.Equal(["u_tex1", "u_tex2"], FloweryCarouselGL.MaskProgram.Value.Children);
        Assert.Equal(["u_image", "u_mask"], FloweryCarouselGL.TextFillProgram.Value.Children);
    }

    [AvaloniaFact]
    public void A_Canvas_Sizes_Its_Buffer_From_Its_Size_When_Detached()
    {
        var canvas = new CarouselGlCanvas { Width = 64.7, Height = 40 };
        Assert.Equal((1, 1), (canvas.PixelWidth, canvas.PixelHeight));
        Assert.True(canvas.ResizeCanvasToDisplaySize());
        Assert.Equal((64, 40), (canvas.PixelWidth, canvas.PixelHeight));
        Assert.Equal(new Avalonia.Size(64, 40), canvas.Resolution);
        Assert.False(canvas.ResizeCanvasToDisplaySize());

        var empty = new CarouselGlCanvas();
        Assert.False(empty.ResizeCanvasToDisplaySize());
        Assert.Equal((1, 1), (empty.PixelWidth, empty.PixelHeight));
        Assert.Equal("Skia", empty.Renderer);
        Assert.Equal(new byte[4], empty.ReadFrame().Pixel);
    }

    [AvaloniaFact]
    public void Pixelate_Blocks_Follow_The_Pixel_Size()
    {
        var canvas = new CarouselGlCanvas { Width = 200, Height = 100 };
        canvas.ResizeCanvasToDisplaySize();
        Assert.Equal((5d, 20d), FloweryCarouselGL.GetPixelateBlocks(canvas, 20));
        Assert.Equal((100d, 101d), FloweryCarouselGL.GetPixelateBlocks(canvas, 0.5));
    }

    [Fact]
    public void A_Frame_Shader_Is_Disposed_When_Its_Last_Draw_Releases_It()
    {
        var shader = SKShader.CreateColor(SKColors.Red);
        var holder = new CarouselGlShaderHolder(shader, new Avalonia.Size(1, 1));
        holder.Retain();
        holder.Release();
        Assert.NotEqual(IntPtr.Zero, shader.Handle);
        holder.Release();
        Assert.Equal(IntPtr.Zero, shader.Handle);
    }

    [AvaloniaFact]
    public void A_Fade_Shows_The_First_Image_At_Progress_0_And_The_Second_At_1()
    {
        using var effect = Attach("transition", "fade", new Dictionary<string, string> { ["transitionA"] = Red, ["transitionB"] = Blue });
        AssertPixel([255, 0, 0, 255], effect.Sample(0).Pixel);
        AssertPixel([128, 0, 128, 255], effect.Sample(0.5).Pixel);
        AssertPixel([0, 0, 255, 255], effect.Sample(1).Pixel);
        Assert.Equal((64, 40), (effect.Sample(1).Width, effect.Sample(1).Height));
    }

    [AvaloniaFact]
    public void A_Fade_Through_Black_Is_Black_Halfway()
    {
        using var effect = Attach("transition", "fade-black", new Dictionary<string, string> { ["transitionA"] = Red, ["transitionB"] = Blue });
        AssertPixel([0, 0, 0, 255], effect.Sample(0.5).Pixel);
    }

    [AvaloniaFact]
    public void A_Checkerboard_Mask_Shows_The_First_Image_At_Progress_0_And_The_Second_At_1()
    {
        using var effect = Attach("mask", "checkerboard", new Dictionary<string, string> { ["maskA"] = Red, ["maskB"] = Blue });
        AssertPixel([255, 0, 0, 255], effect.Sample(0).Pixel);
        AssertPixel([0, 0, 255, 255], effect.Sample(1).Pixel);
    }

    [AvaloniaFact]
    public void A_Flip_At_Angle_0_Shows_The_Front_Image_In_Its_Projected_Quad()
    {
        using var effect = Attach("flip", "flip-plane", new Dictionary<string, string> { ["flipA"] = Red, ["flipB"] = Blue });
        AssertPixel([255, 0, 0, 255], effect.Sample(0).Pixel);
        // The quad lies at depth 2.2, so it covers the middle 1 / 2.2 of the canvas; the rest stays clear.
        Assert.Equal(new byte[4], effect.Canvas.ReadPixel(0, 0));
        Assert.Equal(new byte[4], effect.Canvas.ReadPixel(10, 20));
        AssertPixel([255, 0, 0, 255], effect.Canvas.ReadPixel(20, 20));
    }

    [AvaloniaFact]
    public void A_Flip_At_180_Degrees_Shows_The_Back_Image_Shaded()
    {
        using var effect = Attach("flip", "flip-plane", new Dictionary<string, string> { ["flipA"] = Red, ["flipB"] = Blue });
        // Progress 1 turns the plane by the flip angle of 180 degrees: the back faces the viewer, at full shade.
        AssertPixel([0, 0, 255, 255], effect.Sample(1).Pixel);
    }

    [AvaloniaFact]
    public void An_Effect_With_Alpha_Reads_Back_Unpremultiplied()
    {
        using var effect = Attach("effect", "pulse", new Dictionary<string, string> { ["effect"] = Red });
        AssertPixel([255, 0, 0, 255], effect.Sample(Math.PI / 2 / 1.2).Pixel);
        // The pulse is most transparent at 0.8; GL keeps the color and Skia reads it back the same.
        AssertPixel([255, 0, 0, 204], effect.Sample(3 * Math.PI / 2 / 1.2).Pixel);
    }

    [Fact]
    public void Text_Fill_Shows_The_Image_Only_Where_The_Mask_Is_Opaque()
    {
        // The headless platform draws no text into a RenderTargetBitmap, so the letters come from a mask with an
        // opaque left half here; the runtime tests check the drawn letters. GL samples the mask at the pixel itself.
        using var image = SKImage.FromEncodedData(Convert.FromBase64String(Red[(Red.IndexOf(',') + 1)..]));
        using var maskBitmap = new SKBitmap(new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Premul));
        maskBitmap.Erase(SKColors.Transparent);
        using (var maskCanvas = new SKCanvas(maskBitmap))
            maskCanvas.DrawRect(SKRect.Create(10, 5), new SKPaint { Color = SKColors.White });
        using var mask = SKImage.FromBitmap(maskBitmap);
        var program = FloweryCarouselGL.TextFillProgram.Value;
        using var uniforms = new SKRuntimeEffectUniforms(program)
        {
            ["u_resolution"] = new[] { 20f, 10f },
            ["u_offset"] = new[] { 0.05f, 0f },
            ["u_scale"] = 1.2f
        };
        using var imageShader = Texture(image);
        using var maskShader = Texture(mask);
        using var children = new SKRuntimeEffectChildren(program) { ["u_image"] = imageShader, ["u_mask"] = maskShader };
        using var shader = program.ToShader(uniforms, children);
        using var frame = new SKBitmap(new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var frameCanvas = new SKCanvas(frame))
        {
            frameCanvas.Clear(SKColors.Transparent);
            frameCanvas.DrawRect(SKRect.Create(20, 10), new SKPaint { Shader = shader });
        }

        // The mask covers the top left quarter: the letters keep their place and their way up.
        for (var x = 0; x < 20; x++)
        {
            var pixel = frame.GetPixel(x, 2);
            if (x < 10)
                Assert.Equal(new SKColor(255, 0, 0, 255), pixel);
            else
                Assert.Equal(0, pixel.Alpha);
            Assert.Equal(0, frame.GetPixel(x, 7).Alpha);
        }
    }

    [AvaloniaFact]
    public void Every_Mode_Keeps_The_Top_Of_Its_Image_At_The_Top()
    {
        // GL uploads images flipped and starts its texture coordinates at the bottom; together they keep images upright.
        var image = TwoColorImage(SKColors.Red, SKColors.Blue);
        foreach (var (mode, variant, keyA, keyB, top, bottom) in new[]
        {
            ("transition", "fade", "transitionA", "transitionB", 2, 37),
            ("mask", "checkerboard", "maskA", "maskB", 2, 37),
            ("effect", "pan-zoom", "effect", "effect", 2, 37),
            ("flip", "flip-plane", "flipA", "flipB", 12, 27)
        })
        {
            using var effect = Attach(mode, variant, new Dictionary<string, string> { [keyA] = image, [keyB] = image });
            effect.Sample(0);
            AssertPixel([255, 0, 0, 255], effect.Canvas.ReadPixel(32, top));
            AssertPixel([0, 0, 255, 255], effect.Canvas.ReadPixel(32, bottom));
        }
    }

    [AvaloniaFact]
    public void A_Missing_Image_Fails_The_Canvas_With_The_Js_Message()
    {
        var engine = new FloweryCarouselGL();
        var canvas = new CarouselGlCanvas { Width = 64, Height = 40 };
        Exception? failure = null;
        canvas.Failed += (_, error) => failure = error;
        engine.AttachOverlay(canvas, new CarouselGlConfiguration("effects", "mask", "checkerboard", null,
            new Dictionary<string, string> { ["maskA"] = Red }, 0, 1));
        try
        {
            Assert.Equal("Not supported (assets missing)", canvas.Error);
            Assert.Equal("Not supported (assets missing)", failure?.Message);
            var waiting = canvas.WaitFrameAsync();
            Assert.True(waiting.IsFaulted);
        }
        finally
        {
            engine.DetachAllOverlays();
        }
    }

    [AvaloniaFact]
    public void An_Unresolved_Image_Path_Fails_The_Canvas()
    {
        var engine = new FloweryCarouselGL();
        var canvas = new CarouselGlCanvas { Width = 64, Height = 40 };
        engine.AttachOverlay(canvas, new CarouselGlConfiguration("effects", "effect", "drift", null,
            new Dictionary<string, string> { ["effect"] = "Assets/missing.jpg" }, 0, 1));
        try
        {
            Assert.Equal("Image asset must be resolved by the native host: Assets/missing.jpg", canvas.Error);
            Assert.Null(canvas.State);
        }
        finally
        {
            engine.DetachAllOverlays();
        }
    }

    [AvaloniaFact]
    public void An_Image_That_Does_Not_Decode_Falls_Back_With_The_Js_Message()
    {
        var engine = new FloweryCarouselGL();
        var canvas = new CarouselGlCanvas { Width = 64, Height = 40 };
        engine.AttachOverlay(canvas, new CarouselGlConfiguration("effects", "effect", "drift", null,
            new Dictionary<string, string> { ["effect"] = "data:image/png;base64,AAAA" }, 0, 1));
        try
        {
            Assert.Equal("Not supported (asset load failed)", canvas.Error);
        }
        finally
        {
            engine.DetachAllOverlays();
        }
    }

    [AvaloniaFact]
    public async Task The_Session_Configures_Samples_And_Pauses_Like_The_Bridge()
    {
        using var session = new CarouselGlSession(new FloweryCarouselGL(), null);
        session.View.Width = 64;
        session.View.Height = 40;
        await session.InitializeAsync();
        Assert.True(session.IsInitialized);
        var configuration = new CarouselGlConfiguration("effects", "transition", "fade", null,
            new Dictionary<string, string> { ["transitionA"] = Red, ["transitionB"] = Blue }, 0, 1);

        var configured = await session.ConfigureAsync(configuration);
        Assert.True(configured.Frames > 0);
        Assert.Equal((64, 40), (configured.Width, configured.Height));
        AssertPixel([255, 0, 0, 255], (await session.SampleAsync(0)).Pixel);
        AssertPixel([0, 0, 255, 255], (await session.SampleAsync(1)).Pixel);
        // A running loop answers a sample without a time with its next frame.
        var next = await session.SampleAsync(null);
        Assert.True(next.Frames > configured.Frames);

        // Paused, a sample draws a frame of its own, and the canvas keeps showing its last frame.
        Assert.True(await session.SetActiveAsync(false));
        Assert.True(session.View.IsVisible);
        var framesBefore = session.View.Frames;
        await Task.Delay(100);
        Assert.Equal(framesBefore, session.View.Frames);
        var paused = await session.SampleAsync(null);
        Assert.Equal(framesBefore + 1, paused.Frames);
        Assert.True(await session.SetActiveAsync(true));
        Assert.True(session.View.IsVisible);

        session.Dispose();
        Assert.False(session.IsInitialized);
        Assert.Null(session.View.State);
        Assert.Equal(new byte[4], session.View.ReadFrame().Pixel);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.SampleAsync(0));
    }

    [AvaloniaFact]
    public async Task The_Engine_Compiles_Every_Program_Of_Every_Variant()
    {
        using var session = new CarouselGlSession(new FloweryCarouselGL(), null);
        var compiled = await session.CompileAsync();
        Assert.Equal(49, compiled.Count);
        Assert.Equal(compiled.Count, compiled.Distinct().Count());
        Assert.Equal("mask:checkerboard", compiled[0]);
        Assert.Equal("text:text-fill", compiled[^1]);
    }

    private static ActiveEffect Attach(string mode, string variant, IReadOnlyDictionary<string, string> assets, double width = 64, double height = 40)
    {
        var engine = new FloweryCarouselGL();
        var canvas = new CarouselGlCanvas { Width = width, Height = height };
        var configuration = new CarouselGlConfiguration("effects", mode, variant, null, assets, 0, 1);
        engine.AttachOverlay(canvas, configuration);
        engine.SetOverlayTiming(configuration.IntervalSeconds);
        engine.SetOverlayTransitionDuration(configuration.TransitionSeconds);
        engine.Resize(canvas);
        // The data URIs decode at once, so the draw loop runs when the overlay is attached.
        Assert.Null(canvas.Error);
        Assert.NotNull(canvas.State?.Render);
        return new ActiveEffect(engine, canvas);
    }

    private static void AssertPixel(byte[] expected, byte[] actual, int tolerance = 1)
    {
        for (var channel = 0; channel < 4; channel++)
            Assert.True(Math.Abs(expected[channel] - actual[channel]) <= tolerance,
                $"Expected [{string.Join(", ", expected)}] but read [{string.Join(", ", actual)}].");
    }

    // The child shader of a texture, as CarouselGlEngine.CreateTextureShader makes it.
    private static SKShader Texture(SKImage image) =>
        image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Linear),
            SKMatrix.CreateScale(1f / image.Width, 1f / image.Height));

    private static string TwoColorImage(SKColor top, SKColor bottom)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(bottom);
        using (var canvas = new SKCanvas(bitmap))
            canvas.DrawRect(SKRect.Create(8, 4), new SKPaint { Color = top });
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    private static string SolidImage(SKColor color)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    private sealed class ActiveEffect(FloweryCarouselGL engine, CarouselGlCanvas canvas) : IDisposable
    {
        public CarouselGlCanvas Canvas { get; } = canvas;

        // The bridge's sample with a time: draw at that time since the start of the effect, then read the frame.
        public CarouselGlFrame Sample(double elapsedSeconds)
        {
            var state = Assert.IsAssignableFrom<ICarouselGlState>(Canvas.State);
            state.Render!(state.NativeStart!.Value + elapsedSeconds * 1000);
            return Canvas.ReadFrame();
        }

        public void Dispose() => engine.DetachAllOverlays();
    }
}
