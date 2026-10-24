<!-- Supplementary documentation for DaisyGlassNavbar -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassNavbar is a [DaisyNavbar](DaisyNavbar.md) on a glass surface. Placed over scrolling content, it blurs the content that passes under it on every frame. The `NavbarStart`, `NavbarCenter` and `NavbarEnd` sections, `IsFullWidth` and `Content` work as on DaisyNavbar. The glass properties are those of [DaisyGlass](DaisyGlass.md): `EnableBackdropBlur`, `BlurMode`, `GlassBlur`, `GlassSaturation`, `GlassTint`/`GlassTintOpacity`, the liquid-glass optics, `GlassFill`/`GlassGradient`/`GlassFillOpacity` and `GlassPattern`/`GlassPatternColor`/`GlassPatternOpacity`. The glass is painted first, so its backdrop holds only what lies behind the control.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the live backdrop. |
| `GlassBlur` | 40 | Blur strength. |
| `GlassColorOpacity` | 0.25 | Opacity of `Background` over the glass. |
| `HasShadow` | `False` | A drop shadow would show through the translucent glass; a light border (`#33FFFFFF`) separates the bar instead. |

## Quick Examples

```xml
<Grid>
    <ScrollViewer><!-- content that scrolls under the bar --></ScrollViewer>
    <controls:DaisyGlassNavbar VerticalAlignment="Top" Margin="12" Foreground="White">
        <controls:DaisyGlassNavbar.NavbarStart>
            <TextBlock Text="Flowery" FontWeight="Bold" />
        </controls:DaisyGlassNavbar.NavbarStart>
        <controls:DaisyGlassNavbar.NavbarEnd>
            <controls:DaisyGlassButton Content="Sign in" GlassGradient="Aurora" Foreground="White" />
        </controls:DaisyGlassNavbar.NavbarEnd>
    </controls:DaisyGlassNavbar>
</Grid>
```

## Tips & Best Practices

- Put the bar above the scrolling content in the same `Grid` and give the content top padding, so its start is visible below the bar.
