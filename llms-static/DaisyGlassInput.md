<!-- Supplementary documentation for DaisyGlassInput -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassInput is a [DaisyInput](DaisyInput.md) whose field is a glass surface, for forms over images or colorful backgrounds. Labels, the floating label, icons, hint and helper text, validation and variants work as on DaisyInput: it uses the DaisyInput templates and puts the glass behind their `PART_Background` field when a template is applied. The glass properties are those of [DaisyGlass](DaisyGlass.md): `EnableBackdropBlur`, `BlurMode`, `GlassBlur`, `GlassSaturation`, `GlassTint`/`GlassTintOpacity`, the liquid-glass optics, `GlassFill`/`GlassGradient`/`GlassFillOpacity` and `GlassPattern`/`GlassPatternColor`/`GlassPatternOpacity`. The glass is painted first, so its backdrop holds only what lies behind the control.

## How It Differs from DaisyInput

- The field background is transparent and the border is a light `#66FFFFFF`; the `Filled` variant and the focused `Ghost` variant brighten the glass instead of covering it.
- The text, placeholder (at 65 % opacity), icons, caret and floating label follow `Foreground`, so a single `Foreground` keeps the field readable on any backdrop. The floating label sits on a translucent dark chip.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the live backdrop. |
| `GlassBlur` | 30 | Blur strength. |
| `GlassTint` / `GlassTintOpacity` | White / 0.15 | Tint over the blurred backdrop. |

## Quick Examples

```xml
<controls:DaisyGlassInput PlaceholderText="Email" Foreground="White" />
<controls:DaisyGlassInput PlaceholderText="Password" PasswordChar="●" Foreground="White" />
<controls:DaisyGlassInput Label="Workspace" LabelPosition="Floating" Foreground="White" GlassTint="#0F172A" GlassTintOpacity="0.3" />
```
