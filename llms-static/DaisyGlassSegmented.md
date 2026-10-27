<!-- Supplementary documentation for DaisyGlassSegmented -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassSegmented is a segmented control on a glass track. The items share the width of the track, and a pill slides to the selected item with a spring, squashing slightly while it moves. It is a `ListBox`: items are `ListBoxItem`s or any content (`ItemsSource` works), the arrow keys move the selection, and one item is always selected. The glass properties are those of [DaisyGlass](DaisyGlass.md) and apply to the track; the glass is painted first, so its backdrop holds only what lies behind the control.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the live backdrop. |
| `GlassBlur` | 24 | Blur strength. |
| `GlassTint` / `GlassTintOpacity` | `DaisyGlassTintColor` / 0.12 | Tint of the track; the theme resource is white, or navy in the Glassy theme. |
| `Size` | `Medium` | Padding and font size of the items, `ExtraSmall` to `ExtraLarge`. |
| `IndicatorBrush` / `IndicatorBorderBrush` | `#38FFFFFF` / `#26FFFFFF` | Fill and outline of the pill. |
| `CornerRadius` | 9999 | Fully rounded track and pill. |

## Quick Examples

```xml
<controls:DaisyGlassSegmented Foreground="White">
    <ListBoxItem Content="Day" />
    <ListBoxItem Content="Week" />
    <ListBoxItem Content="Month" />
</controls:DaisyGlassSegmented>

<!-- Light glass: a white tint and a dark pill -->
<controls:DaisyGlassSegmented ItemsSource="{Binding Ranges}" SelectedItem="{Binding Range}"
                              GlassTint="White" GlassTintOpacity="0.45"
                              IndicatorBrush="#260B1533" Foreground="#0B1533" />
```

## Tips & Best Practices

- Choose `Foreground` for the backdrop: unselected items show it at 65 %, the selected item at full strength.
- With reduced motion the pill moves without the spring.
