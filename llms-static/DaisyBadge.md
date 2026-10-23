<!-- Supplementary documentation for DaisyBadge -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyBadge is a compact label for counts, statuses, or tags. It supports **10 color variants**, **5 size presets**, and an **outline** mode. Use it alone or inside buttons and lists.

## Variant Options

| Variant | Description |
| ------- | ----------- |
| Default | Neutral gray fill with contrasting text; fits most backgrounds. |
| Neutral | Muted neutral tone for secondary tags. |
| Primary | High-emphasis brand color. |
| Secondary | Alternate brand color. |
| Accent | Highlight color for special items. |
| Ghost | Subtle neutral fill; minimal visual weight. |
| Info | Informational blue tone. |
| Success | Positive/confirming green tone. |
| Warning | Cautionary amber tone. |
| Error | Critical/destructive red tone. |

## Size Options

The table lists unscaled default token values in logical units.

| Size | Height | Horizontal Padding | Font Size |
| ---- | ------ | ------------------ | --------- |
| ExtraSmall | 16 | 4 | 10 |
| Small | 20 | 6 | 12 |
| Medium (default) | 24 | 8 | 14 |
| Large | 28 | 12 | 18 |
| ExtraLarge | 32 | 14 | 20 |

## Outline Mode

| Property | Effect |
| -------- | ------ |
| `IsOutline=True` | Makes the badge transparent with a colored border and matching text. Border/text color follows `Variant` (Primary/Secondary/Accent) or uses base content color for Default. |

## Quick Examples

```xml
<!-- Basic badge -->
<controls:DaisyBadge Content="New" />

<!-- Color variants -->
<controls:DaisyBadge Content="Primary" Variant="Primary" />
<controls:DaisyBadge Content="Info" Variant="Info" />
<controls:DaisyBadge Content="Error" Variant="Error" />

<!-- Outline style -->
<controls:DaisyBadge Content="Outline" Variant="Primary" IsOutline="True" />
<controls:DaisyBadge Content="Ghost" Variant="Ghost" IsOutline="True" />

<!-- Sizes -->
<StackPanel Orientation="Horizontal" Spacing="6">
    <controls:DaisyBadge Content="XS" Size="ExtraSmall" />
    <controls:DaisyBadge Content="Small" Size="Small" />
    <controls:DaisyBadge Content="Medium" Size="Medium" />
    <controls:DaisyBadge Content="Large" Size="Large" />
    <controls:DaisyBadge Content="XL" Size="ExtraLarge" />
</StackPanel>

<!-- Inside a button -->
<controls:DaisyButton Variant="Ghost">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <TextBlock Text="Messages" VerticalAlignment="Center" />
        <controls:DaisyBadge Content="5" Variant="Error" Size="Small" />
    </StackPanel>
</controls:DaisyButton>

<!-- Placeholder/count style -->
<controls:DaisyBadge Content="+5" Variant="Neutral" IsOutline="True" />
```

## Tips & Best Practices

- Choose outline mode when placing badges on colored surfaces to avoid heavy fills.
- Keep text short (1–10 characters) to maintain the pill shape and legibility.
- Pair `Info/Success/Warning/Error` with matching semantic contexts (errors, confirmations, cautions).
- When embedding in other controls, pick a smaller size (Small/ExtraSmall) so the badge supports rather than competes with the parent.
