<!-- Supplementary documentation for DaisyInput -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyInput is a styled TextBox with **9 variants** and **4 size presets**. It supports bordered, ghost, and semantic colored borders, plus inner content slots for icons or buttons. Defaults to a padded, rounded text field that matches DaisyUI forms.

## Variant Options

| Variant | Description |
|---------|-------------|
| Bordered (default) | Subtle 30% opacity border; brightens on focus. |
| Ghost | No border and transparent background; adds light fill on focus. |
| Primary / Secondary / Accent | Colored borders with focus states. |
| Info / Success / Warning / Error | Semantic border colors. |

## Size Options

| Size | Min Height | Font Size | Use Case |
|------|------------|-----------|----------|
| ExtraSmall | 24 | 10 | Dense tables/toolbars. |
| Small | 32 | 12 | Compact forms. |
| Medium (default) | 48 | 14 | General usage. |
| Large | 64 | 18 | Prominent inputs/hero sections. |

## Slots

- `InnerLeftContent` / `InnerRightContent`: Place icons or buttons inside the control (e.g., clear/search actions). Configure via attached content presenters in templates.
- `Watermark`: Standard placeholder text.

## Quick Examples

```xml
<!-- Basic -->
<controls:DaisyInput Watermark="Bordered (Default)" />

<!-- Ghost -->
<controls:DaisyInput Variant="Ghost" Watermark="Ghost" />

<!-- Semantic -->
<controls:DaisyInput Variant="Primary" Watermark="Primary" />
<controls:DaisyInput Variant="Error" Watermark="Error state" />

<!-- Sizes -->
<controls:DaisyInput Size="Small" Watermark="Small Input" />
<controls:DaisyInput Size="Large" Watermark="Large Input" />

<!-- With icons -->
<controls:DaisyInput Watermark="Search..." Size="Small">
    <controls:DaisyInput.InnerLeftContent>
        <PathIcon Data="{StaticResource DaisyIconSearch}" Width="14" Height="14" Opacity="0.7" />
    </controls:DaisyInput.InnerLeftContent>
</controls:DaisyInput>
```

## Tips & Best Practices

- Use Ghost for inputs on colored surfaces; use Bordered/Primary for standard light backgrounds.
- Pair semantic variants with validation states (Error/Success) to reinforce feedback.
- Keep `Padding` consistent across form fields; sizes already tune height and font size.
- For search bars, add a left icon; for clear actions, add a right button via `InnerRightContent`.
