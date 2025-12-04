<!-- Supplementary documentation for DaisyIndicator -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyIndicator overlays a badge on top of any content (often an icon or card). You provide the main `Content` and a `Badge` slot; the badge is positioned via alignment properties and offset with a half-size translate to sit on the corner. Use it for notification dots, counts, or status chips.

## Properties

| Property | Description |
|----------|-------------|
| `Badge` | Badge content (e.g., `DaisyBadge`, ellipse, dot). Hidden when null. |
| `BadgeHorizontalAlignment` | `Left`, `Center`, `Right` (default Right). |
| `BadgeVerticalAlignment` | `Top`, `Center`, `Bottom` (default Top). |

## Quick Examples

```xml
<!-- Icon with count -->
<controls:DaisyIndicator>
    <controls:DaisyIndicator.Badge>
        <controls:DaisyBadge Variant="Primary" Content="99+" />
    </controls:DaisyIndicator.Badge>
    <Button Content="Inbox" />
</controls:DaisyIndicator>

<!-- Top-left dot on an avatar -->
<controls:DaisyIndicator BadgeHorizontalAlignment="Left"
                         BadgeVerticalAlignment="Top">
    <controls:DaisyIndicator.Badge>
        <Border Width="10" Height="10" CornerRadius="5" Background="Red" />
    </controls:DaisyIndicator.Badge>
    <controls:DaisyAvatar Size="Large" Status="Online" />
</controls:DaisyIndicator>
```

## Tips & Best Practices

- Keep badges small relative to the host; `DaisyBadge` with `Size="ExtraSmall"` works well for counts.
- Adjust alignments to match the host shape (e.g., top-right for icons, bottom-right for cards).
- Badge positioning uses a half-width/half-height translation; ensure the host has enough padding to avoid clipping.
- For simple dots, a small `Border` or `Ellipse` is lighter than a full badge control.
