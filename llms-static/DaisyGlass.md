<!-- Supplementary documentation for DaisyGlass -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlass is a frosted-glass container with multiple blur strategies: simulated gradients, bitmap capture, or SkiaSharp GPU blur. It exposes tint/opacity controls, saturation, reflection parameters, liquid-glass rim optics, and a toggle for real backdrop capture. Use it to create translucent panels over imagery or colorful backgrounds.

## Blur Modes

High contrast replaces the material with an opaque semantic surface and stops backdrop capture.
Capture requests belong to the current host and generation; detached or superseded requests cannot publish their images.
See [accessibility preferences](DaisyAccessibility.md#system-contrast-and-reduced-motion).

| Mode | Description |
| ---- | ----------- |
| `Simulated` | No real blur; uses layered gradients/textures for a lightweight glass look. |
| `BitmapCapture` (default) | Captures underlying content once and blurs it; good balance of quality/perf. |
| `SkiaSharp` | Live blur on every frame with liquid-glass optics. Needs the Flowery.NET.Skia package, registered with `FlowerySkia.Register()`; without it the glass uses `BitmapCapture` (`EffectiveBlurMode` reports the mode in use). |

## Key Properties

| Property | Description |
| -------- | ----------- |
| `EnableBackdropBlur` (bool) | Enables real capture/blur; simulated layers are always rendered. |
| `BlurMode` | Chooses blur pipeline (Simulated/BitmapCapture/SkiaSharp). |
| `EffectiveBlurMode` (read-only) | The mode in use: `SkiaSharp` falls back to `BitmapCapture` when Flowery.NET.Skia is not registered, with a warning in the log once. A glass checks for the renderer when it enters the visual tree; `DaisyGlassLiveRenderer.Register(null)` removes it. |
| `GlassBlur` | Blur radius/strength. |
| `GlassOpacity` | Base tint opacity (overlay). |
| `GlassTint` / `GlassTintOpacity` | Tint color and intensity. |
| `GlassBorderOpacity` | Subtle outline opacity. |
| `GlassReflectDegree` / `GlassReflectOpacity` | Control the reflection effect. |
| `GlassTextShadowOpacity` | Shadow for text content. |
| `GlassSaturation` | Saturation of blurred background (0 = grayscale). |
| `GlassDepth` | How far the liquid-glass rim effect reaches inward from the edge. |
| `GlassCurvature` | Convex lens highlight strength for the liquid-glass overlay. |
| `GlassBend` | Inner rim/meniscus intensity. |
| `GlassDispersion` | Chromatic edge split amount. |
| `BlurredBackground` (read-only) | Captured/blurred image when backdrop blur is enabled. |
| `GlassFill` (IBrush) | Brush painted over the blurred backdrop, such as a linear, radial or conic gradient. Takes precedence over `GlassGradient`. |
| `GlassGradient` | Gradient preset used when `GlassFill` is not set: `None` (default), `Aurora`, `Sunset`, `Ocean`, `Mint`, `Holo`, `Frost`. |
| `GlassFillOpacity` | Opacity of the fill or preset (default 0.6). |
| `GlassPattern` | One of the `DaisyPatternedCard` patterns (`DaisyCardPattern`), drawn over the fill. |
| `GlassPatternColor` / `GlassPatternOpacity` | Pattern color (default white) and the opacity of a typical stroke (default 0.4). |
| `EffectiveGlassFill` (read-only) | The brush in use: `GlassFill`, else the `GlassGradient` preset. |
| `GlassBackground` | An animated background over the fill and below the pattern: a `DaisyGlassGradientBackground`, or a `DaisyGlassShaderBackground` from Flowery.NET.Skia. |
| `GlassBackgroundOpacity` | Opacity of the animated background (default 0.6). |
| `MeasureBackdropColor` (bool) | Measures the average color behind the glass into `BackdropColor` (default off; the glass button and badge turn it on for their text contrast). In `SkiaSharp` mode a measurement reads pixels back from the GPU: after a change behind the glass at most about three times per second, otherwise at most once a second while the glass redraws. |
| `BackdropColor` (read-only) | The measured average color behind the glass, before blur and tint, while `MeasureBackdropColor` and `EnableBackdropBlur` are on in `BitmapCapture` or `SkiaSharp` mode and high contrast is off; `null` otherwise. |
| `TopShineBrush` / `TopShineHeight` (static) | The white shine that every glass draws at its top (60 high). The glass template draws it, and the text contrast of the glass controls counts it. |

## Liquid Glass

Liquid Glass is the SkiaSharp-backed DaisyGlass look that adds a lens-like rim on top of the normal frosted backdrop. It is inspired by displacement-map liquid-glass optics: the background is blurred/tinted, the edge gets a directional specular highlight, and the rim can show depth, curvature, a soft inner bend, and a subtle chromatic split. In Flowery.NET this is a lightweight Avalonia/Skia approximation rather than a browser SVG/WebGL port. Set `EnableBackdropBlur="True"` and `BlurMode="SkiaSharp"`, then tune the liquid parameters:

```xml
<controls:DaisyGlass EnableBackdropBlur="True"
                     BlurMode="SkiaSharp"
                     GlassBlur="28"
                     GlassTint="#FFFFFF"
                     GlassTintOpacity="0.14"
                     GlassReflectOpacity="0.28"
                     GlassDepth="0.85"
                     GlassCurvature="0.8"
                     GlassBend="0.65"
                     GlassDispersion="0.28"
                     CornerRadius="22"
                     Padding="20">
    <TextBlock Text="Liquid Glass" FontWeight="Bold" />
</controls:DaisyGlass>
```

## Fill, Gradients and Patterns

The glass is a stack of layers: the backdrop (blurred or simulated), the tint, the fill, the pattern, then the shine, borders and content. The fill and pattern work in every blur mode and are clipped to the corner radius.

```xml
<!-- A preset gradient with a pattern -->
<controls:DaisyGlass EnableBackdropBlur="True" BlurMode="SkiaSharp"
                     GlassGradient="Holo" GlassFillOpacity="0.55"
                     GlassPattern="Scales" GlassPatternOpacity="0.4" />

<!-- Any brush as the fill -->
<controls:DaisyGlass EnableBackdropBlur="True" BlurMode="SkiaSharp" GlassFillOpacity="0.7">
    <controls:DaisyGlass.GlassFill>
        <RadialGradientBrush Center="50%,0%" GradientOrigin="50%,0%" RadiusX="120%" RadiusY="120%">
            <GradientStop Color="#F0ABFC" Offset="0" />
            <GradientStop Color="#6D28D9" Offset="0.6" />
            <GradientStop Color="#1E1B4B" Offset="1" />
        </RadialGradientBrush>
    </controls:DaisyGlass.GlassFill>
</controls:DaisyGlass>
```

`DaisyGlassGradients.CreateBrush(DaisyGlassGradient)` returns a new brush for a preset. The presets are plain ARGB stops with an angle (linear), a sweep (`Holo`, conic) or a corner highlight (`Frost`, radial), so the other Flowery ports can reproduce the same colors with their own brush types. The patterns are the shared `DaisyCardPattern` set; `GlassPatternOpacity` scales each pattern's own shading, so lighter and darker strokes keep their proportion.

## Animated Backgrounds

`GlassBackground` takes an animated background with an `Effect` (`Plasma`, `Aurora`, `LiquidGold`), a `Speed` (1 is normal, 0 stops it) and a `FrameRate` (redraws per second, default 30; 0 redraws on every display frame). There are two implementations of the same effects, so you can choose between look and cost:

| Background | Package | Drawing |
| ---------- | ------- | ------- |
| `DaisyGlassGradientBackground` | Flowery.NET | Moving linear and radial gradients. No SkiaSharp needed, works with every blur mode. |
| `DaisyGlassShaderBackground` | Flowery.NET.Skia | SkSL runtime shaders drawn per pixel: plasma fields, aurora curtains with shimmer, molten gold with highlights. |

```xml
<controls:DaisyGlassButton Content="Plasma">
    <controls:DaisyGlassButton.GlassBackground>
        <controls:DaisyGlassGradientBackground Effect="Plasma" />
    </controls:DaisyGlassButton.GlassBackground>
</controls:DaisyGlassButton>

<!-- xmlns:skia="clr-namespace:Flowery.Skia;assembly=Flowery.NET.Skia" -->
<controls:DaisyGlass GlassBackgroundOpacity="0.8">
    <controls:DaisyGlass.GlassBackground>
        <skia:DaisyGlassShaderBackground Effect="Aurora" Speed="0.5" />
    </controls:DaisyGlass.GlassBackground>
</controls:DaisyGlass>
```

A background object may be shared; every glass draws its own layer from it, starting at a different phase. With reduced motion the layer shows a still frame; with high contrast it stops, since the contrast surface covers it.

**Cost:** an animated glass redraws `FrameRate` times per second, and in `SkiaSharp` mode that includes its backdrop blur. The rate holds on any refresh rate, so a 144 Hz display does not redraw more often. In the Gallery (Debug build, desktop, region clipping on, see below), six animated glass buttons at the default 30 per second use about 18 % of one CPU core. Keep the number of animated surfaces small. A lower `FrameRate` saves the most; a lower `Speed` slows the motion but not the redraws; `Speed="0"` stops them.

## Performance with Animated Content

By default Avalonia merges all areas that change in a frame into one rectangle. With an animation on screen, that rectangle can reach glass controls far from the change, which then redraw their blur in full on every frame. Turn on region clipping, so each changed area stays separate:

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .With(new CompositionOptions { UseRegionDirtyRectClipping = true }) // Avalonia.Rendering.Composition
    .UseFlowerySkia();
```

In the Gallery, with the default `FrameRate` of 30, turning it on cut the CPU use of the Glass Button section with its six animated buttons from about 70 % to about 18 % of one core. The live glass works with either setting.

## Quick Examples

```xml
<!-- Lightweight simulated glass -->
<controls:DaisyGlass>
    <StackPanel Padding="16" Spacing="8">
        <TextBlock Text="Simulated Glass" FontWeight="Bold" />
        <TextBlock Text="No real blur, just gradients." />
    </StackPanel>
</controls:DaisyGlass>

<!-- Backdrop blur with custom tint -->
<controls:DaisyGlass EnableBackdropBlur="True"
                     BlurMode="BitmapCapture"
                     GlassBlur="30"
                     GlassTint="#FFFFFF"
                     GlassTintOpacity="0.35">
    <StackPanel Padding="16" Spacing="8">
        <TextBlock Text="Blurred Panel" FontWeight="Bold" />
        <TextBlock Text="Captures background once and blurs it." />
    </StackPanel>
</controls:DaisyGlass>

<!-- SkiaSharp live blur -->
<controls:DaisyGlass EnableBackdropBlur="True"
                     BlurMode="SkiaSharp"
                     GlassBlur="40"
                     GlassSaturation="0.9"
                     GlassCurvature="0.75"
                     GlassBend="0.5"
                     GlassDispersion="0.25">
    <TextBlock Text="Live GPU blur" Margin="16,12" />
</controls:DaisyGlass>
```

## Tips & Best Practices

- Use `EnableBackdropBlur=False` (default) for best performance; turn it on only where blur is critical.
- Keep `GlassBlur` moderate (20–40) to avoid heavy GPU/CPU load; higher values can be expensive.
- For dynamic backgrounds, call `RefreshBackdrop()` after major layout/content changes when using capture mode.
- Prefer `Simulated` on low-powered devices; use `SkiaSharp` only when GPU acceleration is available and acceptable.
- `GlassDepth`, `GlassCurvature`, `GlassBend`, and `GlassDispersion` affect the SkiaSharp liquid-glass overlay. They are intentionally lightweight approximations of the upstream displacement-map optics rather than a DOM/SVG filter port.
- Ensure the parent has a background; backdrop capture looks better when sampling colorful or textured surfaces.
- A glass without a fixed size keeps its size in `BitmapCapture` mode; the captured backdrop extends past the edges for the blur but is not part of the layout.
