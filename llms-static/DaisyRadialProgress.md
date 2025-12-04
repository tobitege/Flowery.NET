<!-- Supplementary documentation for DaisyRadialProgress -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyRadialProgress is a circular progress indicator with **8 color variants**, **3 size presets**, and configurable stroke thickness. It inherits `RangeBase` for `Minimum`, `Maximum`, and `Value`, converting the range to a 0–360° sweep. Shows a percentage label by default.

## Variant Options

Uses `DaisyProgressVariant`: Default, Primary, Secondary, Accent, Info, Success, Warning, Error (foreground changes).

## Size & Thickness

| Size | Diameter | Thickness | Label Font |
|------|----------|-----------|------------|
| Small | 32 | 3 | 8 |
| Medium (default) | 48 | 4 | 10 |
| Large | 80 | 8 | 14 |

## Quick Examples

```xml
<!-- Basic -->
<controls:DaisyRadialProgress Value="70" />

<!-- Themed and large -->
<controls:DaisyRadialProgress Value="30" Variant="Primary" />
<controls:DaisyRadialProgress Value="100" Variant="Accent" Size="Large" />

<!-- Custom thickness and range -->
<controls:DaisyRadialProgress Minimum="0" Maximum="250" Value="125" Thickness="6" />
```

## Tips & Best Practices

- Keep `Minimum < Maximum`; default is 0–100.
- Adjust `Thickness` to match size; thicker strokes on Large keep the ring balanced.
- If you need custom center content (icon/text), replace the default label via a control template override.
- For indeterminate circular loaders, use `DaisyLoading` variants instead; this control is determinate.
