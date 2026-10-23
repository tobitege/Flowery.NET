<!-- Supplementary documentation for DaisyKbd -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyKbd renders keyboard keycaps for inline hints (e.g., shortcuts). It provides **5 size presets**, monospace text, a subtle 3D border, and centered content. Use it to display keys like `Ctrl`, `Shift`, or `Cmd` in docs and tooltips.

## Size Options

The table lists unscaled default token values in logical units.

| Size | Height | Padding | Font Size |
| ---- | ---------- | ------- | --------- |
| ExtraSmall | 16 | 2,0 | 10 |
| Small | 20 | 3,0 | 12 |
| Medium (default) | 24 | 4,0 | 14 |
| Large | 32 | 6,0 | 18 |
| ExtraLarge | 40 | 8,0 | 20 |

> [!NOTE]
> DaisyKbd uses **fixed heights** for each size to match DaisyUI's keycap design.

## Quick Examples

```xml
<StackPanel Orientation="Horizontal" Spacing="4">
    <TextBlock Text="Press" VerticalAlignment="Center" />
    <controls:DaisyKbd Content="Ctrl" />
    <TextBlock Text="+" VerticalAlignment="Center" />
    <controls:DaisyKbd Content="S" />
    <TextBlock Text="to save." VerticalAlignment="Center" />
</StackPanel>

<controls:DaisyKbd Size="Small" Content="Esc" />
<controls:DaisyKbd Size="Large" Content="Shift" />
```

## Tips & Best Practices

- Keep content short (1–5 characters) for a key-like appearance.
- Use consistent sizes within a sentence to avoid visual jitter.
- Combine with instructions in tooltips or docs to clarify keyboard interactions.

## Neumorphic Effect

With [DaisyNeumorphic](DaisyNeumorphic.md) active, keys look raised (or pressed in). The key cap border (`PART_Surface`) has an uneven `BorderThickness`, which draws no `BoxShadow`, so the template is a `Panel` with an unclipped `PART_NeumorphicShadow` layer behind it and a hit-test invisible `PART_NeumorphicInset` overlay on top.
