<!-- Supplementary documentation for DaisyGlassButton -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassButton is a DaisyButton on a glass surface that blurs whatever lies behind it. By default it blurs the live backdrop on every frame (`BlurMode="SkiaSharp"`), so content that moves behind the button stays blurred. Variants, styles, sizes, shapes, icons and states work exactly as on [DaisyButton](DaisyButton.md); the glass properties are those of [DaisyGlass](DaisyGlass.md), including gradients and patterns.

The same glass is available on [DaisyGlassBadge](DaisyGlassBadge.md), [DaisyGlassInput](DaisyGlassInput.md), [DaisyGlassNavbar](DaisyGlassNavbar.md) and [DaisyGlassModal](DaisyGlassModal.md).

## How the Surface Is Built

From back to front: the blurred backdrop, the glass tint, the fill (`GlassFill` or a `GlassGradient` preset), the pattern, the variant color, the borders, then the content with a light text shadow. The glass is painted first, so its backdrop holds only what lies behind the button, never the button's own tint or text.

| Variant | Glass look |
| ------- | ---------- |
| Default | Clear glass; hover brightens it. Fills and patterns keep their colors. |
| Primary, Secondary, Accent, Neutral, Info, Success, Warning, Error | The variant color tints the glass at `GlassColorOpacity`; hover uses the DaisyButton hover color. |
| Ghost | Clear glass with the Ghost hover tint. |
| Outline / Dash / Soft | The DaisyButton border and colors over the glass. |

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` | `True` | Blurs the backdrop; `False` shows simulated glass. |
| `BlurMode` | `SkiaSharp` | `SkiaSharp` blurs the live backdrop on every frame; `BitmapCapture` blurs a snapshot that is taken again when the button moves or resizes. |
| `GlassBlur` | 30 | Blur strength. |
| `GlassTint` / `GlassTintOpacity` | White / 0.15 | Tint over the blurred backdrop. |
| `GlassColorOpacity` | 0.35 | Opacity of the variant color and its hover color over the glass. |
| `GlassFill` / `GlassGradient` / `GlassFillOpacity` | none / `None` / 0.6 | Brush or gradient preset over the glass. |
| `GlassPattern` / `GlassPatternColor` / `GlassPatternOpacity` | `None` / White / 0.4 | Pattern over the fill. |
| `GlassBackground` / `GlassBackgroundOpacity` | none / 0.6 | Animated background (Plasma, Aurora, Liquid Gold) as gradients or, with Flowery.NET.Skia, shaders; see [DaisyGlass](DaisyGlass.md#animated-backgrounds). |
| `GlassTextShadowOpacity` | 0.5 | Shadow under text and icons; keeps them readable on any backdrop. |
| `GlassTextContrast` | `True` | Keeps the text and icons at WCAG contrast over what lies behind the glass: the glass measures its backdrop, and a theme foreground moves toward white or black, keeping its hue, until it has 4.5:1 (3:1 for large text) against the glass over that backdrop. A `Foreground` set on the button is kept. Needs `BitmapCapture` or `SkiaSharp`. |
| `EffectiveForeground` (read-only) | | The brush the text and icons are drawn with: `Foreground`, adjusted while `GlassTextContrast` is on. |
| `GlassSaturation`, `GlassReflectDegree`, `GlassReflectOpacity`, `GlassDepth`, `GlassCurvature`, `GlassBend`, `GlassDispersion` | as DaisyGlass | Saturation and the liquid-glass optics of the SkiaSharp mode. |

## Quick Examples

```xml
<!-- Clear glass and a variant tint -->
<controls:DaisyGlassButton Content="Glass" />
<controls:DaisyGlassButton Variant="Primary" Content="Primary" />

<!-- Gradient presets and patterns -->
<controls:DaisyGlassButton Content="Aurora" GlassGradient="Aurora" Foreground="White" />
<controls:DaisyGlassButton Content="Carbon" GlassPattern="CarbonFiber" GlassTint="#111827" GlassTintOpacity="0.45" Foreground="White" />
<controls:DaisyGlassButton Content="Holo scales" GlassGradient="Holo" GlassFillOpacity="0.55"
                           GlassPattern="Scales" GlassPatternOpacity="0.4" Foreground="#1E1B4B" />

<!-- A call to action with a liquid rim -->
<controls:DaisyGlassButton Content="Get started" Size="ExtraLarge" Shape="Wide"
                           GlassGradient="Aurora" GlassPattern="Noise" GlassPatternOpacity="0.4"
                           GlassDepth="0.9" GlassCurvature="0.8" GlassReflectOpacity="0.3" Foreground="White" />
```

## Tips & Best Practices

- Place the button over a colorful or textured background; on a plain surface the blur has nothing to show.
- Each SkiaSharp glass takes a snapshot of the rendered surface when it draws. Many glass buttons over constantly animated content cost GPU time; use `BlurMode="BitmapCapture"` for static backdrops.
- Choose `Foreground` for the backdrop: light text on dark or saturated glass, dark text on light presets such as `Mint`, `Holo` and `Frost`.
- With high contrast on, the glass shows an opaque semantic surface instead of the blur.
