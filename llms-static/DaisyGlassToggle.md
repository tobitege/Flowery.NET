<!-- Supplementary documentation for DaisyGlassToggle -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassToggle is a [DaisyToggle](DaisyToggle.md) on a glass track. Switched on, the track fills with the variant color; the Default variant uses the Success color, like a system switch. The white pill knob slides with a spring and stretches while it is pressed. `Content`, `Variant` and `Size` work as on DaisyToggle. The glass properties are those of [DaisyGlass](DaisyGlass.md) and apply to the track.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `Variant` | `Default` | Color of the checked track: Success for Default, else the variant color. |
| `Size` | `Medium` | Track and knob size, `ExtraSmall` (36 × 20) to `ExtraLarge` (68 × 38). |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the live backdrop behind the track. |
| `GlassBlur` | 20 | Blur strength. |
| `GlassTint` / `GlassTintOpacity` | `DaisyGlassTintColor` / 0.18 | Tint of the unchecked track. |
| `BorderBrush` | `#33FFFFFF` | Outline of the track. |

## Quick Examples

```xml
<controls:DaisyGlassToggle Content="Wi-Fi" IsChecked="True" Foreground="White" />
<controls:DaisyGlassToggle Variant="Primary" Size="Large" />
```
