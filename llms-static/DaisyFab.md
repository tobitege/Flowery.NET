<!-- Supplementary documentation for DaisyFab -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyFab is a Floating Action Button container with built-in trigger and child action buttons. It supports **Vertical** stacking, **Horizontal** expansion, or **Flower** fan-out layouts. It auto-closes when an action is clicked and allows full customization of the trigger via content or specific icon data.

## Layout & Interaction

| Property | Description |
| -------- | ----------- |
| `Layout` (`Vertical`, `Horizontal`, `Flower`) | `Vertical` stacks upward (Right aligned); `Horizontal` expands rightward (Left aligned); `Flower` fans out in a quarter-circle (Left aligned). |
| `IsOpen` | Toggles the visibility/animation of action buttons. Trigger click flips this. |
| `AutoClose` (default `True`) | Clicking an action button closes the menu. |

## Trigger Appearance

| Property | Description |
| -------- | ----------- |
| `TriggerVariant` | `DaisyButtonVariant` for the trigger (default Primary). |
| `TriggerContent` | Trigger content (default "+"). Ignored if `TriggerIconData` is set. |
| `TriggerIconData` | Vector path (StreamGeometry) for the trigger icon. |
| `TriggerIconSize` | Explicit size for the trigger icon. |
| `Size` | Propagated to trigger and action buttons (ExtraSmall–ExtraLarge from `DaisySize`). |

## Quick Examples

```xml
<!-- Horizontal FAB (Aligned Left, opens Right) -->
<controls:DaisyFab Layout="Horizontal" TriggerContent="+" TriggerVariant="Accent" Size="Medium">
    <controls:DaisyButton Content="1" />
    <controls:DaisyButton Content="2" />
    <controls:DaisyButton Content="3" />
</controls:DaisyFab>

<!-- Flower layout with Vector Icon -->
<controls:DaisyFab Layout="Flower" TriggerVariant="Secondary" 
                   TriggerIconData="M13,0L6,14H11V24L18,10H13V0Z" TriggerIconSize="16">
    <controls:DaisyButton Content="A" />
    <controls:DaisyButton Content="B" />
    <controls:DaisyButton Content="C" />
</controls:DaisyFab>
```

## Tips & Best Practices

- Keep action count small (2–5) so spacing in Vertical/Flower layouts remains readable.
- Use `AutoClose=False` if actions open modal flows and you want the menu to stay open.
- Prefer circular action buttons with concise icons/letters; labels belong in tooltips or surrounding UI.
- Position the FAB with layout (default bottom-right with margin 16) to avoid overlap with content.
