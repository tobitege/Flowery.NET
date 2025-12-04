<!-- Supplementary documentation for DaisySkeleton -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisySkeleton provides loading placeholders with a pulsing animation. It can render as a block (default) or animate text color when `IsTextMode=True`. Use it to indicate content is loading for avatars, text lines, cards, or any element you want to reserve space for.

## Modes

| Mode | Description |
|------|-------------|
| Default | Animated opacity on a solid background with corner radius. |
| TextMode (`IsTextMode=True`) | Hides the block background and animates the content’s opacity (skeleton-text style). |

## Quick Examples

```xml
<!-- Basic shapes -->
<controls:DaisySkeleton Height="32" Width="32" CornerRadius="16" />
<controls:DaisySkeleton Height="20" />
<controls:DaisySkeleton Height="20" Width="150" />

<!-- Avatar + text skeleton -->
<StackPanel Orientation="Horizontal" Spacing="12">
    <controls:DaisySkeleton Width="48" Height="48" CornerRadius="24" />
    <StackPanel Spacing="8" VerticalAlignment="Center">
        <controls:DaisySkeleton Height="16" Width="80" />
        <controls:DaisySkeleton Height="16" Width="120" />
    </StackPanel>
</StackPanel>

<!-- Text mode -->
<controls:DaisySkeleton IsTextMode="True" Content="AI is thinking..." />
```

## Tips & Best Practices

- Match skeleton sizes to the real content to prevent layout shifts when data loads.
- Use `CornerRadius` to mirror the final shape (e.g., round for avatars, small radius for text lines).
- Text mode works best when you supply representative placeholder text with similar length to the final content.
- Keep skeleton colors neutral; rely on the built-in pulse rather than bright accents.
