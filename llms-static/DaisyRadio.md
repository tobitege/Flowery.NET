<!-- Supplementary documentation for DaisyRadio -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyRadio is a styled `RadioButton` with **8 color variants** and **5 size presets**. It shows a filled inner circle when checked and supports standard radio grouping via `GroupName`. Use variants for semantic meaning or brand alignment in forms and option groups.

## Variant Options

| Variant | Description |
| ------- | ----------- |
| Default | Neutral fill when checked. |
| Primary / Secondary / Accent | Brand-aligned fills. |
| Success / Warning / Info / Error | Semantic fills for status or validation. |

## Size Options

| Size | Outer | Inner | Label font |
| ---- | ----- | ----- | ---------- |
| ExtraSmall | 8px | 4px | 10px |
| Small | 10px | 5px | 12px |
| Medium | 12px | 6px | 14px |
| Large | 14px | 7px | 18px |
| ExtraLarge | 16px | 8px | 20px |

All five sizes have explicit styles. The indicator stays smaller than the label font and grows with each tier.
Without a local `Size`, attached radios follow `FlowerySizeManager.CurrentSize`, including runtime changes.
Application size styles take precedence over the global default. Label text and the indicator scale together when `FloweryScaleManager` is used.
Scaling does not replace `FontSize` styles or bindings.
Explicit sizes and branches with `IgnoreGlobalSize=true` remain independent.
The full label row stays clickable, and `GroupName` retains normal radio selection behavior.

## Quick Examples

```xml
<!-- Basic group -->
<StackPanel Spacing="6">
    <controls:DaisyRadio Content="Option A" GroupName="demo" />
    <controls:DaisyRadio Content="Option B" GroupName="demo" IsChecked="True" />
</StackPanel>

<!-- Semantic variants -->
<controls:DaisyRadio Content="Success" Variant="Success" GroupName="status" />
<controls:DaisyRadio Content="Error" Variant="Error" GroupName="status" />

<!-- Compact radios -->
<controls:DaisyRadio Content="Small" Size="Small" GroupName="sizeDemo" IsChecked="True" />
<controls:DaisyRadio Content="XS" Size="ExtraSmall" GroupName="sizeDemo" />
```

## Tips & Best Practices

- Always set `GroupName` for mutually exclusive choices; Avalonia handles exclusivity within a group.
- Align size with neighboring inputs; use Small/XS for dense forms, Large for mobile-friendly layouts.
- Use semantic variants to indicate consequence (e.g., Error for destructive choices).
- Keep labels short; adjust `Padding` if you customize spacing.

## Neumorphic Effect

With [DaisyNeumorphic](DaisyNeumorphic.md) active, the circle (`PART_Border`) is always inset, like an input well, whatever the global mode.
