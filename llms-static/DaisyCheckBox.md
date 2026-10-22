<!-- Supplementary documentation for DaisyCheckBox -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyCheckBox is a styled checkbox with **9 color variants** and **5 size presets**. It uses a filled box with a checkmark when checked and inherits Avalonia's standard `IsChecked`/`IsThreeState` behavior. Use variants for semantic meaning and sizes to match surrounding controls.

## Variant Options

| Variant | Description |
| ------- | ----------- |
| Default | Neutral base styling. |
| Primary / Secondary / Accent | Brand-aligned colors for emphasis. |
| Neutral | Muted neutral fill. |
| Info / Success / Warning / Error | Semantic colors for statuses. |

## Size Options

| Size | Box | Check | Label font |
| ---- | --- | ----- | ---------- |
| ExtraSmall | 8px | 5px | 10px |
| Small | 10px | 6px | 12px |
| Medium | 12px | 8px | 14px |
| Large | 14px | 10px | 18px |
| ExtraLarge | 16px | 12px | 20px |

Without an explicit `Size`, attached checkboxes follow `FlowerySizeManager.CurrentSize`, including runtime changes.
Size values supplied by application styles also take precedence over the global default.
With `FloweryScaleManager`, label text and the indicator scale together from the current size-tier resources. Scaling does not replace `FontSize` styles or bindings.
The global default is Small. Local `Size` values and branches with `IgnoreGlobalSize=true` remain independent.
Labels use the manager's primary font sizes. Indicator size, checkmark size, and label font have separate checkbox tokens.
The control retains the normal size-tier row height for clicking the label; only the visual indicator is compact.

## Quick Examples

```xml
<!-- Basic variants -->
<controls:DaisyCheckBox Content="Default" />
<controls:DaisyCheckBox Content="Primary" Variant="Primary" IsChecked="True" />
<controls:DaisyCheckBox Content="Warning" Variant="Warning" IsChecked="True" />
<controls:DaisyCheckBox Content="Error" Variant="Error" IsChecked="True" />

<!-- Sizes -->
<StackPanel Spacing="6">
    <controls:DaisyCheckBox Content="Extra Small" Size="ExtraSmall" IsChecked="True" />
    <controls:DaisyCheckBox Content="Small" Size="Small" IsChecked="True" />
    <controls:DaisyCheckBox Content="Medium" Size="Medium" IsChecked="True" />
    <controls:DaisyCheckBox Content="Large" Size="Large" IsChecked="True" />
    <controls:DaisyCheckBox Content="Extra Large" Size="ExtraLarge" IsChecked="True" />
</StackPanel>

<!-- Disabled states -->
<controls:DaisyCheckBox Content="Disabled unchecked" IsEnabled="False" />
<controls:DaisyCheckBox Content="Disabled checked" IsEnabled="False" IsChecked="True" />
<controls:DaisyCheckBox Content="Disabled Primary" Variant="Primary" IsEnabled="False" IsChecked="True" />
```

## Tips & Best Practices

- Pair sizes with neighboring controls (Small for dense forms; Large for card layouts or touch devices).
- Use semantic variants to reinforce status (Success/Warning/Error) without extra icons.
- Keep content concise; longer labels may need additional left padding if you override `Padding`.
- Respect accessibility: ensure checked/unchecked colors have sufficient contrast against backgrounds.

## Neumorphic Effect

With [DaisyNeumorphic](DaisyNeumorphic.md) active, the box (`PART_Border`) is always inset, like an input well, whatever the global mode.
