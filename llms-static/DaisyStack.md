<!-- Supplementary documentation for DaisyStack -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyStack stacks its children with layered offsets, creating a deck-of-cards effect. It inherits from `Grid` so children overlap; theme styles apply Z-index, translation, scale, and opacity per child order. Use it to showcase layered previews or overlapping cards.

## Behavior

- Children overlap in a single cell (Grid behavior).
- Theming applies:
  - 1st child: top-most, full size.
  - 2nd child: slight offset/scale, lower opacity.
  - 3rd child: further offset/scale, lower opacity.
- Additional children follow default Grid behavior; extend styles if you need more layers.

## Quick Examples

```xml
<controls:DaisyStack>
    <Border Background="Red" Width="100" Height="100" />
    <Border Background="Green" Width="100" Height="100" />
    <Border Background="Blue" Width="100" Height="100" />
</controls:DaisyStack>
```

## Tips & Best Practices

- Keep child sizes equal for a neat stacked appearance; varied sizes will emphasize offsets.
- Adjust styles for more layers by extending nth-child selectors if stacking >3 items.
- Combine with shadows on children to enhance depth.
- Wrap in a container with padding to prevent clipped offsets.
