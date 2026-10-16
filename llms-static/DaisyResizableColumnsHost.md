<!-- Supplementary documentation for DaisyResizableColumnsHost -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyResizableColumnsHost is an `ItemsControl` for multi-column layouts with a shared resize grip. When the mouse is near the gap between two columns, a grip appears; dragging it changes `ColumnWidth`, which all columns share, within `MinColumnWidth` and `MaxColumnWidth`. The same control exists in Flowery.WinUI and Flowery.Uno.

## Key Features

- **Shared resizing**: dragging the grip after column *n* moves it by the combined growth of the *n + 1* columns before it, so every column keeps the same width.
- **Hover discovery**: the grip appears within 20 px of a gap (mouse only) and hides when the pointer leaves.
- **Clamped width**: `ColumnWidth` always stays between `MinColumnWidth` and `MaxColumnWidth`.
- **Keyboard**: with focus, Left/Right (Up/Down when vertical) change the width by 8 px, Page Up/Down by 32 px, Home/End jump to the minimum/maximum.
- **Orientation**: columns stack horizontally (default) or vertically.

## Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **ColumnWidth** | `double` | 250 | Shared width of all columns. |
| **MinColumnWidth** | `double` | 100 | Smallest column width. |
| **MaxColumnWidth** | `double` | 1000 | Largest column width. |
| **ColumnSpacing** | `double` | 0 | Gap between columns; the grip sits in its middle. |
| **GripVisibility** | `DaisyGripVisibility` | Auto | `Auto` and `Visible` show the grip on hover; `Hidden` turns it off (the keyboard still works). |
| **IsGripEnabled** | `bool` | true | Whether the columns can be resized at all. |
| **Orientation** | `Orientation` | Horizontal | Stacking direction of the columns. |
| **AccessibleText** | `string?` | null | Name announced by screen readers; defaults to "Resizable columns". |

Events: `ColumnWidthChanged` (new width), `ResizeDragStarted`, `ResizeDragCompleted`.

## Quick Examples

```xml
<!-- Columns as direct children -->
<controls:DaisyResizableColumnsHost x:Name="Columns" ColumnWidth="200" MinColumnWidth="120" ColumnSpacing="16">
    <Border Width="{Binding #Columns.ColumnWidth}" Background="{DynamicResource DaisyBase200Brush}" />
    <Border Width="{Binding #Columns.ColumnWidth}" Background="{DynamicResource DaisyBase200Brush}" />
</controls:DaisyResizableColumnsHost>

<!-- Columns from ItemsSource -->
<controls:DaisyResizableColumnsHost ItemsSource="{Binding Columns}" ColumnWidth="{Binding SharedWidth, Mode=TwoWay}">
    <controls:DaisyResizableColumnsHost.ItemTemplate>
        <DataTemplate>
            <Border Width="{Binding $parent[controls:DaisyResizableColumnsHost].ColumnWidth}">
                <!-- Column content -->
            </Border>
        </DataTemplate>
    </controls:DaisyResizableColumnsHost.ItemTemplate>
</controls:DaisyResizableColumnsHost>
```

## Tips & Best Practices

- **Bind the column width**: the host does not size the columns itself; bind the width of each item (or the root of the `ItemTemplate`) to `ColumnWidth`.
- **Items panel**: the default panel is a `StackPanel` bound to `Orientation` and `ColumnSpacing`. A custom `ItemsPanel` must handle both itself.
