<!-- Supplementary documentation for DaisyGlassBadge -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassBadge is a [DaisyBadge](DaisyBadge.md) on a glass surface that blurs whatever lies behind it, for labels on images, cards or glass bars. Variants, outline and sizes work as on DaisyBadge. The glass properties are those of [DaisyGlass](DaisyGlass.md): `EnableBackdropBlur`, `BlurMode`, `GlassBlur`, `GlassSaturation`, `GlassTint`/`GlassTintOpacity`, the liquid-glass optics, `GlassFill`/`GlassGradient`/`GlassFillOpacity` and `GlassPattern`/`GlassPatternColor`/`GlassPatternOpacity`. The glass is painted first, so its backdrop holds only what lies behind the control.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the live backdrop. |
| `GlassBlur` | 20 | Blur strength. |
| `GlassColorOpacity` | 0.45 | Opacity of the variant color over the glass; the default variant is clear glass. |
| `GlassTextShadowOpacity` | 0.5 | Shadow under the content. |
| `GlassTextContrast` | `True` | Keeps the content at WCAG contrast over the backdrop, as on [DaisyGlassButton](DaisyGlassButton.md); a `Foreground` set on the badge is kept. Needs `EnableBackdropBlur=True` and is off with high contrast. |
| `EffectiveForeground` (read-only) | | `Foreground`, adjusted for contrast while `GlassTextContrast` is on. |

## Quick Examples

```xml
<controls:DaisyGlassBadge Content="Docs" Foreground="White" />
<controls:DaisyGlassBadge Content="Beta" Variant="Primary" />
<controls:DaisyGlassBadge Content="New" GlassGradient="Sunset" Foreground="White" />
```
