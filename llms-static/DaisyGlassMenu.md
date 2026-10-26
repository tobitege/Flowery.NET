<!-- Supplementary documentation for DaisyGlassMenu -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassMenu is a [DaisyMenu](DaisyMenu.md) on a glass surface, for context menus, dropdowns and side menus. Items get a rounded highlight on hover and when selected, drawn in the text color at low opacity, so it shows on light and dark glass. Two item classes help with context menus:

| Class | Effect |
| ----- | ------ |
| `menu-destructive` | The item takes the Error color, for actions such as Delete. |
| `menu-separator` | The item is a thin divider that takes neither the pointer nor the focus. |

The glass properties are those of [DaisyGlass](DaisyGlass.md).

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the live backdrop. |
| `GlassBlur` | 40 | Blur strength. |
| `GlassTint` / `GlassTintOpacity` | `DaisyGlassTintColor` / 0.35 | Tint of the surface. |
| `CornerRadius` / `Padding` | 22 / 6 | Rounded surface around the items. |
| `Size` / `Orientation` | `Medium` / `Vertical` | As on DaisyMenu. |

## Popups

A glass sees only what is drawn in its own window. A `Popup` normally opens its own window, so a glass menu in it would show no backdrop. Open the menu in the overlay layer of the window instead:

```xml
<Popup x:Name="MenuPopup" PlacementTarget="{Binding #MoreButton}" Placement="Bottom" VerticalOffset="8"
       ShouldUseOverlayLayer="True" IsLightDismissEnabled="True">
    <controls:DaisyGlassMenu Width="230" Foreground="White" SelectionChanged="Menu_SelectionChanged">
        <ListBoxItem>
            <StackPanel Orientation="Horizontal" Spacing="12">
                <PathIcon Width="16" Height="16" Data="{StaticResource DaisyIconEdit}" />
                <TextBlock Text="Rename" VerticalAlignment="Center" />
            </StackPanel>
        </ListBoxItem>
        <ListBoxItem Classes="menu-separator" />
        <ListBoxItem Classes="menu-destructive" Content="Delete" />
    </controls:DaisyGlassMenu>
</Popup>
```

## Tips & Best Practices

- For a context menu, act on `SelectionChanged`, then clear `SelectedItem` and close the popup, so the menu keeps no selection.
