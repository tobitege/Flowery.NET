<!-- Supplementary documentation for DaisyRating -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyRating is a star rating control built on `RangeBase`. It supports **precision modes** (full/half/0.1), **3 size presets**, and respects `Minimum/Maximum/Value` (default 0–5). Filled stars are clipped based on `Value` to show partial ratings; clicking updates the value unless `IsReadOnly` is set.

## Precision Modes

| Mode | Snapping |
|------|----------|
| Full (default) | Whole stars (1, 2, 3…). |
| Half | 0.5 increments. |
| Precise | 0.1 increments. |

## Size Options

| Size | Star Size | Use Case |
|------|-----------|----------|
| ExtraSmall | 12px | Dense tables/toolbars. |
| Small | 16px | Compact cards/forms. |
| Medium (default) | 24px | General usage. |
| Large | 32px | Hero ratings or touch-friendly UIs. |
| ExtraLarge | Falls back to Medium styling (no explicit theme overrides). |

## Quick Examples

```xml
<!-- Basic -->
<controls:DaisyRating Value="2.5" />

<!-- Large, whole-star only -->
<controls:DaisyRating Value="4" Size="Large" Precision="Full" />

<!-- Read-only display -->
<controls:DaisyRating Value="4.5" IsReadOnly="True" />
```

## Tips & Best Practices

- Set `Maximum` to the number of stars (default 5); `Value` is clipped within `Minimum/Maximum`.
- Use `Precision="Half"` for UX parity with common review widgets; `Precise` for finer sliders.
- When read-only, disable pointer updates via `IsReadOnly=True` but still show the clipped fill.
- For accessibility, pair with a numeric label (e.g., “4.5 out of 5”) near the control.
