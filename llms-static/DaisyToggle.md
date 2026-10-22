<!-- Supplementary documentation for DaisyToggle -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyToggle is a styled `ToggleSwitch` with **8 color variants** and **5 size presets**. `TogglePadding` controls the knob inset. The knob animates when the state changes. Use it for binary on/off controls.

## Variant Options

| Variant | Description |
| ------- | ----------- |
| Default | Neutral track and knob; darkens when checked. |
| Primary / Secondary / Accent | Colored track when checked. |
| Success / Warning / Info / Error | Semantic track colors when checked. |

## Size Options

| Size | Track (W×H) | Knob | Label font |
| ---- | ----------- | ---- | ---------- |
| ExtraSmall | 16×8 | 4px | 10px |
| Small | 20×10 | 6px | 12px |
| Medium | 24×12 | 8px | 14px |
| Large | 28×14 | 10px | 18px |
| ExtraLarge | 32×16 | 12px | 20px |

Without an explicit `Size`, attached toggles follow `FlowerySizeManager.CurrentSize`, including runtime changes.
Application size styles take precedence over the global default. Label text, track, and knob scale together when `FloweryScaleManager` is used.
Scaling does not replace `FontSize` styles or bindings.
The global default is Small. Local `Size` values and branches with `IgnoreGlobalSize=true` remain independent.
Track, knob, and label font use size-tier tokens. Knob positions follow the current track bounds instead of fixed offsets.
The full label row keeps its normal size-tier height for interaction.

## Additional Styling

| Property | Description |
| -------- | ----------- |
| `TogglePadding` | Internal knob inset (default 1). |

## Quick Examples

```xml
<controls:DaisyToggle Content="Toggle" />
<controls:DaisyToggle Content="Primary" Variant="Primary" IsChecked="True" />
<controls:DaisyToggle Content="Error" Variant="Error" Size="Small" IsChecked="True" />
<controls:DaisyToggle Content="Large Switch" Variant="Accent" Size="Large" />
```

## Tips & Best Practices

- Match `Size` to surrounding controls; use Large for mobile or card headers, Small/XS for dense lists.
- Use semantic variants to convey meaning (Success for enabled/ready, Error for risky toggles).
- Keep `TogglePadding` small; larger padding may reduce visible track fill on small sizes.
- Bind `IsChecked` for state management; it inherits ToggleSwitch behavior.

## Neumorphic Effect

With [DaisyNeumorphic](DaisyNeumorphic.md) active, the track (`SwitchArea`) is always inset and the knob always raised, whatever the global mode. The knob is an `Ellipse`, which has no `BoxShadow`; its shadow is drawn on `PART_KnobShadow`, a round border that follows the knob's size and position.
