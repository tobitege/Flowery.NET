<!-- Supplementary documentation for DaisyBreadcrumbs -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyBreadcrumbs shows a horizontal trail of navigation steps. It auto-builds `DaisyBreadcrumbItem` containers, applies separators between items, and marks only the trailing item as non-clickable. Supports custom separators, icons, command execution on intermediate items, and horizontal scrolling when content overflows.

## Breadcrumb Properties

| Property | Description |
|----------|-------------|
| `Separator` (string, default "/") | Text used between items. Accepts any string (e.g., `›`, `»`, `>`). |
| `SeparatorOpacity` (double, default 0.5) | Controls the visual weight of the separator glyph. |
| `Items` | Supply `DaisyBreadcrumbItem` children directly or bind an `ItemsSource`; containers are created automatically. |

## Item Properties

| Property | Description |
|----------|-------------|
| `Content` | The label text; supports templated content. |
| `Icon` (Geometry) | Optional leading icon; hidden when not set. |
| `Command` / `CommandParameter` | Invoked on click for non-last items when `IsClickable=True`. |
| `IsClickable` (bool) | Toggle interactivity; still respects `IsLast` (last item never executes commands). |
| `IsFirst` / `IsLast` / `Index` | Set by the parent for styling and interaction; not usually set manually. |
| `Separator` / `SeparatorOpacity` | Inherited from the parent; override per item if needed. |

## Quick Examples

```xml
<!-- Basic trail -->
<controls:DaisyBreadcrumbs>
    <controls:DaisyBreadcrumbItem Content="Home" />
    <controls:DaisyBreadcrumbItem Content="Documents" />
    <controls:DaisyBreadcrumbItem Content="Add Document" />
</controls:DaisyBreadcrumbs>

<!-- Custom separators and opacity -->
<controls:DaisyBreadcrumbs Separator="›" SeparatorOpacity="0.7">
    <controls:DaisyBreadcrumbItem Content="Root" />
    <controls:DaisyBreadcrumbItem Content="Folder" />
    <controls:DaisyBreadcrumbItem Content="File" />
</controls:DaisyBreadcrumbs>

<!-- With icons -->
<controls:DaisyBreadcrumbs>
    <controls:DaisyBreadcrumbItem Content="Home" Icon="{StaticResource DaisyIconHome}" />
    <controls:DaisyBreadcrumbItem Content="Library" Icon="{StaticResource DaisyIconFolder}" />
    <controls:DaisyBreadcrumbItem Content="Report.pdf" Icon="{StaticResource DaisyIconDocument}" />
</controls:DaisyBreadcrumbs>

<!-- Scrollable long path -->
<controls:DaisyBreadcrumbs MaxWidth="220">
    <controls:DaisyBreadcrumbItem Content="Long text 1" />
    <controls:DaisyBreadcrumbItem Content="Long text 2" />
    <controls:DaisyBreadcrumbItem Content="Long text 3" />
    <controls:DaisyBreadcrumbItem Content="Long text 4" />
    <controls:DaisyBreadcrumbItem Content="Long text 5" />
</controls:DaisyBreadcrumbs>

<!-- Command-driven navigation -->
<controls:DaisyBreadcrumbs Separator=">">
    <controls:DaisyBreadcrumbItem Content="Home"
                                  Command="{Binding NavigateCommand}"
                                  CommandParameter="Home" />
    <controls:DaisyBreadcrumbItem Content="Projects"
                                  Command="{Binding NavigateCommand}"
                                  CommandParameter="Projects" />
    <controls:DaisyBreadcrumbItem Content="Current Project" />
</controls:DaisyBreadcrumbs>
```

## Tips & Best Practices

- Keep labels short to avoid truncation; set `MaxWidth` on the breadcrumb control to enable horizontal scrolling for long paths.
- Use a distinctive separator (e.g., `›` or `»`) to improve scanability on dark backgrounds.
- Bind `Command` on intermediate items for navigation; the last item is intentionally non-interactive to indicate the current page.
- Provide icons for top-level nodes (e.g., Home, Folder) to aid recognition, especially on narrow layouts.
- If you dynamically generate items, let `DaisyBreadcrumbs` create containers so `IsFirst/IsLast` and separators are applied automatically.
