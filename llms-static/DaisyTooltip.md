<!-- Supplementary documentation for DaisyTooltip -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyTooltip wraps content and shows `Tip` in a Daisy-styled tooltip on hover. It sets up a `ToolTip` for its content, so the tooltip uses the same theme and color classes as a plain `ToolTip` with `Classes="primary"` and so on. The same control exists in Flowery.WinUI and Flowery.Uno with the same properties.

## Properties

| Property | Description |
| -------- | ----------- |
| `Tip` (string) | Tooltip text. No tooltip is shown while it is empty. |
| `Variant` (`DaisyColor`, default Default) | Tooltip color. `Default` and `Neutral` use the neutral colors; the others match the `ToolTip` classes (`primary`, `secondary`, `accent`, `info`, `success`, `warning`, `error`). |
| `Placement` (`PlacementMode`, default Top) | Where the tooltip opens relative to the content. |
| `IsOpen` (bool) | Opens or closes the tooltip in code; also follows hover. |

## Quick Examples

```xml
<controls:DaisyTooltip Tip="Save changes">
    <controls:DaisyButton Content="Save" />
</controls:DaisyTooltip>

<controls:DaisyTooltip Tip="Deleted items cannot be restored" Variant="Error" Placement="Right">
    <controls:DaisyButton Content="Delete" Variant="Error" />
</controls:DaisyTooltip>
```

## Tips & Best Practices

- Keep tips short; use a `DaisyPopover` for longer or interactive content.
- On Avalonia, `ToolTip.Tip` with `Classes` on a plain `ToolTip` gives the same look; use `DaisyTooltip` when the code should also run on Flowery.WinUI or Flowery.Uno.
