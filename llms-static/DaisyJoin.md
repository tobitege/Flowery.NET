<!-- Supplementary documentation for DaisyJoin -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyJoin groups adjacent controls into a seamless, connected set by trimming internal corners and overlapping borders. It works horizontally by default and supports vertical orientation. Ideal for segmented buttons, grouped inputs, or stacked radio/button groups.

## Behavior & Styling

| Feature | Description |
|---------|-------------|
| Orientation | Default Horizontal; set `Orientation="Vertical"` to stack items. |
| CornerRadius | First/last children keep outer rounding; middle items have square corners for a continuous strip. |
| Margins | Negative margins collapse borders between items, avoiding double borders. |
| Supported children | Any control with `CornerRadius`/`Margin` (e.g., Button, TextBox, ComboBox, DaisyThemeRadio in button mode, Border). |

## Quick Examples

```xml
<!-- Segmented buttons -->
<controls:DaisyJoin>
    <controls:DaisyButton Content="Left" />
    <controls:DaisyButton Content="Middle" />
    <controls:DaisyButton Content="Right" />
</controls:DaisyJoin>

<!-- Input with action button -->
<controls:DaisyJoin>
    <controls:DaisyInput Watermark="Search" />
    <controls:DaisyButton Content="Go" Variant="Primary" />
</controls:DaisyJoin>

<!-- Vertical join -->
<controls:DaisyJoin Orientation="Vertical">
    <controls:DaisyButton Content="Top" />
    <controls:DaisyButton Content="Middle" />
    <controls:DaisyButton Content="Bottom" />
</controls:DaisyJoin>
```

## Tips & Best Practices

- Ensure children use consistent heights/paddings for a clean seam.
- Place semantic variants on endpoints to emphasize actions (e.g., Primary on the rightmost button).
- For form combos (input + button), keep the input first so borders align naturally.
- When stacking vertically, use equal widths to avoid staggered edges.
