<!-- Supplementary documentation for FloweryCarouselGL -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

## Overview

Flowery.NET.Skia draws the GL carousel effects with SkSL runtime shaders (`SKRuntimeEffect`) through the Skia renderer of Avalonia.
Avalonia draws with Skia on every target, so the effects run on Windows, Linux, macOS, in the browser (WebAssembly), on Android and on iOS, with the GPU backend Skia uses there.
The package keeps the 51 registered effects of Flowery.WinUI, and the Gallery keeps its catalog of 100 transition shaders, ported to SkSL.
There is no WebView, no browser engine and no JavaScript: the shader hosts are Avalonia controls.

## Two Shader Hosts

| Host | Assembly and namespace | Purpose |
| --- | --- | --- |
| CarouselGlEffectHost | Flowery.NET.Skia, Flowery.Effects | Registered masks, flips, transitions, text fill, and continuous effects |
| CarouselGlTransitionHost | Flowery.NET.Gallery, Flowery.NET.Gallery.Effects | Gallery integration for the gl-transitions shader catalog |

Both derive from CarouselGlNativeHost.
Each host owns a canvas (`CarouselGlCanvas`) that fills the host and shows the frames of its engine.
The engine compiles the SkSL programs once per process and draws each frame as a shader, which the canvas paints with an `ICustomDrawOperation`.
The hosts need no registration: `FlowerySkia.Register()` concerns the glass blur only.

## Use a Registered Effect

```xml
xmlns:fx="clr-namespace:Flowery.Effects;assembly=Flowery.NET.Skia"

<fx:CarouselGlEffectHost Effect="Checkerboard"
                         Height="280"
                         TransitionSeconds="0.8"
                         TransitionSliceCount="8" />
```

The registry's built-in definitions have no images by default.
Register application assets for the selected effect before the host loads:

```csharp
CarouselGlEffectRegistry.Register(
    new CarouselGlEffectDefinition(
        CarouselGlEffectKind.Checkerboard,
        "mask",
        new Dictionary<string, string>
        {
            ["maskA"] = "avares://MyApp/Assets/slide1.jpg",
            ["maskB"] = "avares://MyApp/Assets/slide2.jpg"
        },
        "checkerboard"));
```

The Gallery registers its own presets and assets through `CarouselGlGalleryPresets.EnsureRegistered()`.

## Preserved Effect Families

- Mask patterns, including blinds, slices, checkerboard, spiral, matrix rain, and wormhole.
- Pixel transitions, including dissolve and pixelate.
- Flip and cube transitions.
- Fade, slide, push, zoom, cover, reveal, and wipe transitions, and Random, which picks one of them.
- Pan, zoom, drift, pulse, breath, and throw effects.
- Text-fill effects and the external shader collection used by the Gallery.

## Effect Host Properties

| Property | Default | Purpose |
| --- | --- | --- |
| Effect | None | Selects the registered preset |
| OverlayEffect | None | Adds a continuous effect over a compatible transition |
| IntervalSeconds | 0 | Controls the interval supplied to the renderer |
| TransitionSeconds | 2.4 | Transition duration |
| TransitionSliceCount | 8 | Number of tiles or slices |
| TransitionStaggerSlices | true | Enables staggered slice animation |
| TransitionSliceStaggerMs | 50 | Delay between slices |
| TransitionPixelateSize | 20 | Pixel block size |
| TransitionDissolveDensity | 0.5 | Dissolve density |
| TransitionFlipAngle | 90 | Flip angle in degrees |

Property changes refresh the renderer configuration.
Effect names use CarouselGlEffectKind.
CarouselGlEffectRegistry.TryGet reads a definition, and Register replaces or adds that definition.
`Effect` hides `Visual.Effect`, the render effect of Avalonia, on this control.

## Gallery Transition Host

CarouselGlTransitionHost exposes TransitionName, IntervalSeconds, and TransitionSeconds.
Its AvailableShaders property provides the complete embedded catalog: 100 SkSL ports of the gl-transitions shaders, embedded in the Gallery assembly.
The shader loader requires exactly 100 entries and reports an error if that catalog is incomplete.

The Gallery owns this integration.
Applications that need it can use the same host pattern with their own shader resources and image configuration.

## Custom Engines

A host derives from CarouselGlNativeHost and overrides these members:

| Member | Purpose |
| --- | --- |
| `CreateEngine()` | Creates the engine of a renderer session; the host calls it once per session |
| `CreateConfiguration()` | Returns the effect to draw as a `CarouselGlConfiguration`, or null for none |
| `ShaderSources` (optional) | Shader sources by name, which the engine reads as `NativeShaders` |

`CreateEngine()` takes the place of the engine script that the WinUI host ran in WebView2.
An engine derives from `CarouselGlEngine` and overrides `AttachOverlay`, `SetOverlayTiming`, `SetOverlayTransitionDuration`, `SetOverlayActive`, `Resize`, `CompileAsync` and `DetachAllOverlays`, which only the session of its host calls.
It draws on the `CarouselGlCanvas` it gets in `AttachOverlay`: `RequestAnimationFrame` runs its draw loop, `Present` shows a frame shader, `Frame` counts a drawn frame, and `Fail` stops the canvas with an error that the host shows.
The canvas's `State` (`ICarouselGlState`) lets the host draw a frame at a given time for `ReadFrameAsync`.
`CreateProgram`, `DecodeImageAsync`, `CreateTextureShader` and `DrawArrays` compile programs, load images and draw a frame with its textures.

A frame shader draws over (0, 0) to the canvas's `Resolution` in local units; pass that size as a `u_resolution` uniform.
Texture shaders take normalized coordinates from the top left, as `CreateTextureShader` makes them.
A compile error throws an `InvalidOperationException` with the Skia error text, which the host keeps visible.

## Assets

Include images as Avalonia resources (`AvaloniaResource`) and name them with `avares://` URIs; Android and the browser have no application files.
The host also accepts data URIs, HTTP(S) URLs, `ms-appx:///` paths, relative application paths, and rooted file paths.
Relative paths resolve below `AppContext.BaseDirectory`; for `ms-appx:///` paths, the host removes that prefix and decodes URI characters before it resolves the path.
The host reads `avares://` resources with `AssetLoader` and local files with `File.ReadAllBytesAsync`, and passes their complete contents to the engine as data URIs.
The engine decodes the images with Skia; it does not decode SVG.

Preset asset keys remain unchanged:

| Mode | Keys |
| --- | --- |
| Mask | maskA, maskB |
| Flip | flipA, flipB |
| Transition | transitionA, transitionB |
| Continuous effect | effect |
| Text fill | text |

## Lifetime and Diagnostics

The host starts its renderer session when it is loaded.
It pauses the draw loop when it leaves the effective viewport, while it or a parent is hidden (`IsVisible`), and while animations are reduced (`DaisyAccessibility.IsReducedMotion`), and resumes it afterwards.
A paused host keeps showing its last frame; the WinUI host hid its canvas and showed nothing.
A paused host also draws a changed setting, such as `IntervalSeconds`, at once.
The frames follow the `Opacity` of the host and its parents, as in the slide fade of a `DaisyCarousel`.
Unload closes the session, cancels pending requests, removes event handlers, and releases the canvas and its images.

CarouselGlNativeHost exposes these diagnostics:

| Member | Purpose |
| --- | --- |
| IsRendererInitialized | Indicates that the renderer session completed initialization |
| LastRenderError | Retains the most recent initialization or render error |
| RenderFailed | Reports renderer failures, once per failure |
| ReadFrameAsync(elapsedSeconds) | Reads renderer identity, frame count, pixel sample, and dimensions from a loaded host |
| CompileShadersAsync() | Compiles the host's shader catalog and returns its shader names |

`ReadFrameAsync` returns a `CarouselGlFrame`:

| Field | Value |
| --- | --- |
| Renderer | The Skia backend of the last frame drawn on screen: "Skia OpenGL", "Skia Direct3D", "Skia Vulkan", "Skia Metal", or "Skia CPU" without a GPU context; "Skia" before the first frame |
| Frames | The number of frames drawn |
| Pixel | The center pixel as non-premultiplied RGBA, read back from a raster render of the same frame shader |
| Width, Height | The size of the drawing buffer in device pixels |

With `elapsedSeconds`, the host draws the frame at that time of its animation; without it, a running host waits for the next frame of its loop, and a paused host draws the frame at once.
Renderer failures remain visible: the host shows "GL renderer: " and the error message, and diagnostic requests fail.
Frame and shader diagnostics require a loaded host.

## References

- [DaisyCarousel](DaisyCarousel.md)
- [Effects](Effects.md)
- [Animations](working_with_animations.md)
- [DaisyAccessibility](DaisyAccessibility.md)
