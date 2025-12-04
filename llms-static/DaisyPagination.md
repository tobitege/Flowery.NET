<!-- Supplementary documentation for DaisyPagination -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyPagination renders joined page buttons with support for numeric and non-numeric items (prev/next/ellipsis). It tracks the current page, toggles `IsActive` on page items, and raises `PageChanged` when a numeric item is clicked. Orientation and size presets let you use it horizontally or vertically.

## Properties & Events

| Property | Description |
|----------|-------------|
| `CurrentPage` (int, min 1) | Selected page; coerced to ≥1. |
| `Size` | ExtraSmall, Small, Medium, Large, ExtraLarge (affects button size/padding). |
| `Orientation` | Horizontal (default) or Vertical. |
| `ButtonStyle` | DaisyButtonStyle for page buttons (Default, Outline, etc.). |
| `PageChanged` event | Fired with the new page number when a numeric item is clicked. |

## Item Containers

- Use `DaisyPaginationItem` for pages; set `PageNumber` and `Content` (auto-created if you pass ints/strings via `Items`).
- `IsActive` marks the current page and switches to active styling.
- Non-numeric content (e.g., `"Previous"`, `"Next"`, `"..."`) can be added; only numeric items change pages.

## Quick Examples

```xml
<!-- Simple numeric pagination -->
<controls:DaisyPagination CurrentPage="2">
    <controls:DaisyPaginationItem Content="1" PageNumber="1" />
    <controls:DaisyPaginationItem Content="2" PageNumber="2" />
    <controls:DaisyPaginationItem Content="3" PageNumber="3" />
    <controls:DaisyPaginationItem Content="4" PageNumber="4" />
</controls:DaisyPagination>

<!-- With prev/next and outline style -->
<controls:DaisyPagination ButtonStyle="Outline" Size="Small" CurrentPage="5">
    <controls:DaisyPaginationItem Content="Previous" />
    <controls:DaisyPaginationItem Content="4" PageNumber="4" />
    <controls:DaisyPaginationItem Content="5" PageNumber="5" />
    <controls:DaisyPaginationItem Content="6" PageNumber="6" />
    <controls:DaisyPaginationItem Content="Next" />
</controls:DaisyPagination>

<!-- Vertical pagination -->
<controls:DaisyPagination Orientation="Vertical" Size="Small" CurrentPage="1">
    <controls:DaisyPaginationItem Content="Top" PageNumber="1" />
    <controls:DaisyPaginationItem Content="Middle" PageNumber="2" />
    <controls:DaisyPaginationItem Content="Bottom" PageNumber="3" />
</controls:DaisyPagination>
```

## Tips & Best Practices

- Bind `CurrentPage` to your view model and handle `PageChanged` to update data; the control updates `IsActive` automatically.
- Use `ButtonStyle="Outline"` on colored backgrounds to avoid heavy fills.
- Include `"..."` items as non-numeric separators; they remain disabled by default.
- Keep button sizes consistent across the pagination bar; let `Size` handle padding and corner radii.
