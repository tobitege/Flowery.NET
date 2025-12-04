<!-- Supplementary documentation for DaisyTable -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyTable provides a styled table layout with header, body, and footer sections plus row/cell primitives. It supports **zebra striping**, **pinned headers/columns** (via the template), **row active/hover states**, and **size presets** that adjust padding and font size. Use `DaisyTableHead`, `DaisyTableBody`, and `DaisyTableFoot` to structure content; rows/cells handle layout and styling.

## Structure

- `DaisyTable` (root ItemsControl)
  - `DaisyTableHead` → `DaisyTableRow` → `DaisyTableHeaderCell`
  - `DaisyTableBody` → `DaisyTableRow` → `DaisyTableCell`
  - `DaisyTableFoot` → `DaisyTableRow` → `DaisyTableCell`

## Table Properties

| Property | Description |
|----------|-------------|
| `Size` | ExtraSmall, Small, Medium (default), Large, ExtraLarge; adjusts padding/font via converters. |
| `Zebra` (bool) | Applies alternate row backgrounds in `DaisyTableBody`. |
| `PinRows` / `PinCols` (bool) | Exposed for sticky headers/columns; template groundwork present. |

## Row & Cell Properties

| Element | Property | Description |
|---------|----------|-------------|
| `DaisyTableRow` | `IsActive` | Highlights the row as selected/active. |
| `DaisyTableRow` | `HighlightOnHover` | Enables hover highlighting. |
| `DaisyTableHeaderCell` | `ColumnWidth` | GridLength for the column (Star/Absolute/Auto). |
| `DaisyTableCell` | — | Standard content cell. |

## Quick Examples

```xml
<controls:DaisyTable Size="Small" Zebra="True">
    <controls:DaisyTableHead>
        <controls:DaisyTableRow>
            <controls:DaisyTableHeaderCell Content="Name" ColumnWidth="2*" />
            <controls:DaisyTableHeaderCell Content="Role" ColumnWidth="*" />
            <controls:DaisyTableHeaderCell Content="Actions" ColumnWidth="Auto" />
        </controls:DaisyTableRow>
    </controls:DaisyTableHead>

    <controls:DaisyTableBody>
        <controls:DaisyTableRow HighlightOnHover="True">
            <controls:DaisyTableCell Content="Alice" />
            <controls:DaisyTableCell Content="Engineer" />
            <controls:DaisyTableCell>
                <controls:DaisyButton Variant="Ghost" Size="Small" Content="Edit" />
            </controls:DaisyTableCell>
        </controls:DaisyTableRow>
        <controls:DaisyTableRow HighlightOnHover="True" IsActive="True">
            <controls:DaisyTableCell Content="Bob" />
            <controls:DaisyTableCell Content="Designer" />
            <controls:DaisyTableCell>
                <controls:DaisyButton Variant="Ghost" Size="Small" Content="Edit" />
            </controls:DaisyTableCell>
        </controls:DaisyTableRow>
    </controls:DaisyTableBody>
</controls:DaisyTable>
```

## Tips & Best Practices

- Use `ColumnWidth` on header cells to control column sizing across body rows (star widths distribute remaining space).
- Enable `Zebra` for dense data tables to improve scanability.
- Mark important rows with `IsActive=True`; combine with `HighlightOnHover` for interactivity.
- Keep padding/font consistent by using the `Size` presets instead of manual per-cell tweaks.
- If you need sticky headers/columns, leverage `PinRows/PinCols` alongside template adjustments for your layout.
