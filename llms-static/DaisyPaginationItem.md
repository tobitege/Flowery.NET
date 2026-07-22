# Overview

A button control representing a single page in a `DaisyPagination` control. This is a helper class typically created automatically by the parent pagination control.

`DaisyPaginationItem` extends `Button` and adds pagination-specific properties like `PageNumber` and `IsActive`. While you can use it manually, it's typically auto-generated when you populate a `DaisyPagination` with page numbers.

## Properties

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `IsActive` | `bool` | `false` | Whether this item is the currently selected page |
| `PageNumber` | `int?` | `null` | The page number this item represents (null for non-numeric items like "..." or arrows) |

## Usage

### Automatic (Recommended)

When using `DaisyPagination`, items are created automatically:

```xml
<controls:DaisyPagination CurrentPage="3">
    <sys:Int32>1</sys:Int32>
    <sys:Int32>2</sys:Int32>
    <sys:Int32>3</sys:Int32>
    <sys:Int32>4</sys:Int32>
    <sys:Int32>5</sys:Int32>
</controls:DaisyPagination>
```

### Manual (Advanced)

For custom pagination layouts, you can create items manually:

```xml
<controls:DaisyPagination CurrentPage="2">
    <controls:DaisyPaginationItem Content="«" PageNumber="{x:Null}" />
    <controls:DaisyPaginationItem Content="1" PageNumber="1" />
    <controls:DaisyPaginationItem Content="2" PageNumber="2" IsActive="True" />
    <controls:DaisyPaginationItem Content="3" PageNumber="3" />
    <controls:DaisyPaginationItem Content="..." IsEnabled="False" />
    <controls:DaisyPaginationItem Content="10" PageNumber="10" />
    <controls:DaisyPaginationItem Content="»" PageNumber="{x:Null}" />
</controls:DaisyPagination>
```

## Styling

The active state is styled via the `IsActive` property. The control automatically inherits `Size` and `ButtonStyle` from its parent `DaisyPagination`.

## See Also

- [DaisyPagination](DaisyPagination.html) - The parent container control
