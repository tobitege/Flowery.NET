<!-- Supplementary documentation for DaisyGlass -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlass is a frosted-glass container with multiple blur strategies: simulated gradients, bitmap capture, or SkiaSharp GPU blur. It exposes tint/opacity controls, saturation, reflection parameters, liquid-glass rim optics, and a toggle for real backdrop capture. Use it to create translucent panels over imagery or colorful backgrounds.

## Blur Modes

| Mode | Description |
| ---- | ----------- |
| `Simulated` | No real blur; uses layered gradients/textures for a lightweight glass look. |
| `BitmapCapture` (default) | Captures underlying content once and blurs it; good balance of quality/perf. |
| `SkiaSharp` | GPU blur via Skia; more real-time but experimental. |

## Key Properties

| Property | Description |
| -------- | ----------- |
| `EnableBackdropBlur` (bool) | Enables real capture/blur; simulated layers are always rendered. |
| `BlurMode` | Chooses blur pipeline (Simulated/BitmapCapture/SkiaSharp). |
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
