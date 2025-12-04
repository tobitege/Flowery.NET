<!-- Supplementary documentation for DaisyProgress -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyProgress is a styled progress bar with **8 color variants**, **4 size presets**, and indeterminate support. It inherits `ProgressBar` behavior for `Value`, `Minimum`, `Maximum`, and `IsIndeterminate`, using theme colors for the indicator fill.

## Variant Options

| Variant | Description |
|---------|-------------|
| Default | Neutral bar; foreground uses base content color. |
| Primary / Secondary / Accent | Brand/secondary accents. |
| Info / Success / Warning / Error | Semantic fills for status-driven progress. |

## Size Options

| Size | Height | CornerRadius |
|------|--------|--------------|
| ExtraSmall | 2 | 1 |
| Small | 4 | 2 |
| Medium (default) | 8 | 4 |
| Large | 16 | 8 |

## Quick Examples

```xml
<!-- Determinate -->
<controls:DaisyProgress Value="40" />
<controls:DaisyProgress Value="60" Variant="Primary" />
<controls:DaisyProgress Value="80" Variant="Accent" Size="Large" />

<!-- Indeterminate -->
<controls:DaisyProgress IsIndeterminate="True" Variant="Secondary" />
```

## Tips & Best Practices

- Choose semantic variants to match the task (e.g., `Success` for completion, `Warning` for slow steps).
- For very small spaces, use `Size="Small"` or `ExtraSmall` to keep bars unobtrusive.
- If using indeterminate mode, ensure it communicates ongoing work without a definite end time; determinate values are better for known-length tasks.
- Wrap in a container with explicit width; the indicator scales to parent width via the `ProgressBar` layout.
