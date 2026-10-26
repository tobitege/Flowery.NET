<!-- Supplementary documentation for DaisyGlassRange -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassRange is a [DaisyRange](DaisyRange.md) with a thin translucent track, a colored progress and a thumb of frosted glass, a pill like a [DaisyGlassButton](DaisyGlassButton.md). While the slider is pressed, on the thumb or anywhere on the track, the thumb grows with a spring and clears into a glass lens, so the track and the progress show through it; the pseudo-class `:lens` marks that state. `Minimum`, `Maximum`, `Value`, `Variant` and `Size` work as on DaisyRange. The glass properties are those of [DaisyGlass](DaisyGlass.md) and apply to the thumb at rest; the lens has its own optics: little blur, a light tint and a strong liquid rim, in the `GlassTint` of the control.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `ProgressBrush` | `DaisyPrimaryBrush` | Fill of the track up to the middle of the thumb. The variants set it to their color. |
| `ShowProgress` | `True` | Shows the progress. |
| `ThumbBrush` | Transparent | A color over the glass of the thumb at rest. |
| `BorderBrush` | `#33FFFFFF` | Outline of the thumb at rest. |
| `Background` | `DaisyBaseContentBrush` | Track color, drawn at 22 % so it shows on light and dark backdrops. |
| `Size` | `Medium` | Track height and thumb size, `ExtraSmall` to `ExtraLarge`. |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | The thumb and the lens show the live backdrop. |
| `GlassBlur` | 30 | Blur of the thumb at rest. |
| `GlassTint` / `GlassTintOpacity` | `DaisyGlassTintColor` / 0.15 | Tint of the thumb at rest; the lens takes the same tint color. |

## Quick Examples

```xml
<controls:DaisyGlassRange Minimum="0" Maximum="100" Value="35" />
<controls:DaisyGlassRange Value="70" Maximum="100" Variant="Success" Size="Large" />
<controls:DaisyGlassRange Value="50" Maximum="100" ProgressBrush="#0A84FF" />
```

## Tips & Best Practices

- The lens is drawn over the track, outside the thumb; leave some room above and below the control (`MinHeight` is 40 for `Medium`).
