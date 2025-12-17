<!-- Supplementary documentation for DaisyInput -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyInput is a styled TextBox with **10 variants** and **4 size presets**. It supports bordered, ghost, filled, and semantic colored borders, plus labels, helper text, icons, and inner content slots. Defaults to a padded, rounded text field that matches DaisyUI forms.

## Variant Options

| Variant | Description |
|---------|-------------|
| Bordered (default) | Subtle 30% opacity border; brightens on focus. |
| Ghost | No border and transparent background; adds light fill on focus. |
| Filled | Filled background with bottom border; ideal for material-style forms. |
| Primary / Secondary / Accent | Colored borders with focus states. |
| Info / Success / Warning / Error | Semantic border colors. |

## Size Options

| Size | Height | Font Size | Use Case |
|------|--------|-----------|----------|
| ExtraSmall | 24 | 10 | Dense tables/toolbars. |
| Small | 32 | 12 | Compact forms. |
| Medium (default) | 48 | 14 | General usage. |
| Large | 64 | 18 | Prominent inputs/hero sections. |

> [!NOTE]
> DaisyInput uses **fixed heights** for each size to match DaisyUI's design. Single-line inputs will not grow vertically. For multi-line text entry, use `DaisyTextArea` instead.

## Properties

DaisyInput extends `TextBox` - all standard TextBox properties (`Text`, `Watermark`, `Padding`, `TextAlignment`, `TextWrapping`, `VerticalContentAlignment`, etc.) are supported.

**DaisyInput-specific properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Variant` | `DaisyInputVariant` | `Bordered` | Visual style variant (see table above). |
| `Size` | `DaisySize` | `Medium` | Size preset (see table above). |
| `Label` | `string?` | `null` | Label text displayed above the input. |
| `LabelPosition` | `DaisyLabelPosition` | `Top` | Label positioning mode (None, Top, Floating, Inset). |
| `IsRequired` | `bool` | `false` | Shows asterisk (*) indicator next to label. |
| `IsOptional` | `bool` | `false` | Shows "Optional field" text next to label. |
| `HintText` | `string?` | `null` | Hint text displayed below label, above input. |
| `HelperText` | `string?` | `null` | Helper text displayed below input (right-aligned). |
| `StartIcon` | `StreamGeometry?` | `null` | Icon displayed at the start (left) of the input. |
| `EndIcon` | `StreamGeometry?` | `null` | Icon displayed at the end (right) of the input. |
| `BorderRingBrush` | `IBrush?` | `null` | Custom brush for the focus ring (overrides default). |
| `InnerLeftContent` | `object` | `null` | Content slot inside left edge (e.g., search icon). |
| `InnerRightContent` | `object` | `null` | Content slot inside right edge (e.g., clear button). |

## Content Alignment

The `VerticalContentAlignment` property controls vertical positioning of both the watermark and text content:

- **Center** (default): Text vertically centered - standard for single-line inputs.
- **Top**: Text aligned to top - used by `DaisyTextArea` for multi-line editing.

## Quick Examples

```xml
<!-- Basic -->
<controls:DaisyInput Watermark="Bordered (Default)" />

<!-- Filled variant -->
<controls:DaisyInput Variant="Filled" Watermark="Filled style" />

<!-- With label and required indicator -->
<controls:DaisyInput Label="Email" IsRequired="True" Watermark="Enter email..." />

<!-- With label and optional indicator -->
<controls:DaisyInput Label="Notes" IsOptional="True" HelperText="Optional field" Watermark="Enter notes..." />

<!-- With helper text -->
<controls:DaisyInput Label="Password" HelperText="Must be at least 8 characters" Watermark="Enter password..." />

<!-- With icons -->
<controls:DaisyInput StartIcon="{StaticResource DaisyIconHome}" Watermark="Address" />
<controls:DaisyInput EndIcon="{StaticResource DaisyIconSearch}" Watermark="Search..." />

<!-- Semantic variants -->
<controls:DaisyInput Variant="Error" HelperText="Invalid input" Watermark="Error state" />
<controls:DaisyInput Variant="Success" HelperText="Looks good!" Watermark="Valid input" />

<!-- Sizes -->
<controls:DaisyInput Size="Small" Watermark="Small Input" />
<controls:DaisyInput Size="Large" Watermark="Large Input" />
```

## Tips & Best Practices

- Use `Label` for form field names and `Watermark` for placeholder text inside the input.
- Use `IsRequired="True"` to show an asterisk (*) indicator for required fields.
- Use `HelperText` for validation messages or additional guidance.
- Use Ghost for inputs on colored surfaces; use Bordered/Primary for standard light backgrounds.
- Use Filled variant for material design style forms.
- Pair semantic variants with validation states (Error/Success) to reinforce feedback.
- Keep `Padding` consistent across form fields; sizes already tune height and font size.
- For search bars, add a left icon; for clear actions, add a right button via `InnerRightContent`.
