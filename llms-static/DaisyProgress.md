<!-- Supplementary documentation for DaisyProgress -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyProgress is a styled progress bar with **8 color variants**, **4 size presets**, and indeterminate support. It inherits `ProgressBar` behavior for `Value`, `Minimum`, `Maximum`, and `IsIndeterminate`, using theme colors for the indicator fill.

**Determinate mode** features smooth width transitions (250ms) on value updates for a more fluid feel.

## Variant Options

| Variant | Description |
| ------- | ----------- |
| Default | Neutral bar; foreground uses base content color. |
| Primary / Secondary / Accent | Brand/secondary accents. |
| Info / Success / Warning / Error | Semantic fills for status-driven progress. |

## Size Options

| Size | Height | CornerRadius |
| ---- | ------ | ------------ |
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

## Accessibility Support

DaisyProgress includes built-in accessibility for screen readers via the `AccessibleText` property. The automation peer automatically announces the current percentage along with the accessible text.

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `AccessibleText` | `string` | `"Progress"` | Context text announced by screen readers (e.g., "Uploading file, 45%"). |

### Accessibility Examples

```xml
<!-- Default: announces "Progress, 40%" -->
<controls:DaisyProgress Value="40" />

<!-- Contextual: announces "Uploading file, 60%" -->
<controls:DaisyProgress Value="60" AccessibleText="Uploading file" />

<!-- Contextual: announces "Download progress, 80%" -->
<controls:DaisyProgress Value="80" AccessibleText="Download progress" Variant="Primary" />
```

## Tips & Best Practices

- Choose semantic variants to match the task (e.g., `Success` for completion, `Warning` for slow steps).
- For very small spaces, use `Size="Small"` or `ExtraSmall` to keep bars unobtrusive.
- If using indeterminate mode, ensure it communicates ongoing work without a definite end time; determinate values are better for known-length tasks.
- Wrap in a container with explicit width; the indicator scales to parent width via the `ProgressBar` layout.
- Use `AccessibleText` to provide context about what is loading (e.g., "Saving document" instead of generic "Progress").
