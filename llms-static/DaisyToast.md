<!-- Supplementary documentation for DaisyToast -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyToast is a simple toast container (ItemsControl) that stacks toast items such as `DaisyAlert`. You control its screen position via alignment and offset properties. Place it in an overlay/top layer to float above other content; set `ZIndex` accordingly.

## Properties

| Property | Description |
|----------|-------------|
| `ToastHorizontalOffset` | Horizontal margin from the aligned edge (default 16). |
| `ToastVerticalOffset` | Vertical margin from the aligned edge (default 16). |
| Alignment | Use `HorizontalAlignment`/`VerticalAlignment` on the control to place it (e.g., `BottomRight`). |

## Quick Examples

```xml
<!-- Bottom-right toast stack -->
<Grid>
    <!-- app content -->
    <controls:DaisyToast HorizontalAlignment="Right"
                         VerticalAlignment="Bottom"
                         ToastHorizontalOffset="16"
                         ToastVerticalOffset="16">
        <controls:DaisyAlert Variant="Info" Content="New message arrived." />
        <controls:DaisyAlert Variant="Success" Content="Message sent successfully." />
    </controls:DaisyToast>
</Grid>
```

## Tips & Best Practices

- Place DaisyToast in an overlay layer or at the end of your layout with a high `ZIndex` so it sits above content.
- Use `ItemsPanel` (default StackPanel) spacing to control gap between toasts.
- Pair with timers/dismiss logic in your viewmodel to add/remove items.
- Set alignment (`Bottom/Top` and `Left/Right`) plus offsets for desired corner placement.
