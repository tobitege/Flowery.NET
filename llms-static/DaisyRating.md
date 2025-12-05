<!-- Supplementary documentation for DaisyRating -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyRating is a star rating control built on `RangeBase`. It supports **precision modes** (full/half/0.1), **3 size presets**, and respects `Minimum/Maximum/Value` (default 0–5).
Filled stars are clipped based on `Value` to show partial ratings; clicking updates the value unless `IsReadOnly` is set.

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

## Accessibility Support

DaisyRating includes built-in accessibility for screen readers via the `AccessibleText` property. The automation peer automatically announces the current star count (e.g., "Rating: 3 of 5 stars").

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AccessibleText` | `string` | `"Rating"` | Context text announced by screen readers (e.g., "Product rating: 4 of 5 stars"). |

### How It Works

The automation peer formats the announcement based on the current value and precision:

- **Full precision**: "Rating: 3 of 5 stars"
- **Half precision**: "Rating: 3.5 of 5 stars"
- **Precise mode**: "Rating: 3.7 of 5 stars"

### Accessibility Examples

```xml
<!-- Default: announces "Rating: 3 of 5 stars" -->
<controls:DaisyRating Value="3" />

<!-- Contextual: announces "Product rating: 4 of 5 stars" -->
<controls:DaisyRating Value="4" AccessibleText="Product rating" />

<!-- Half precision: announces "Movie rating: 4.5 of 5 stars" -->
<controls:DaisyRating Value="4.5" Precision="Half" AccessibleText="Movie rating" />

<!-- Read-only with context: announces "Average user rating: 3 of 5 stars" -->
<controls:DaisyRating Value="3" IsReadOnly="True" AccessibleText="Average user rating" />
```

## Tips & Best Practices

- Set `Maximum` to the number of stars (default 5); `Value` is clipped within `Minimum/Maximum`.
- Use `Precision="Half"` for UX parity with common review widgets; `Precise` for finer sliders.
- When read-only, disable pointer updates via `IsReadOnly=True` but still show the clipped fill.
- Use `AccessibleText` to provide context (e.g., "Product rating" or "Your review"); the star count is automatically appended.
- The built-in accessibility replaces the need for a separate numeric label in most cases.
