<!-- Supplementary documentation for DaisyGlassModal -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassModal is a [DaisyModal](DaisyModal.md) whose dialog is a glass surface: it dims the page and blurs it behind the dialog. `IsOpen`, `Padding`, `DialogMaxWidth` and the four corner radii work as on DaisyModal. The glass properties are those of [DaisyGlass](DaisyGlass.md): `EnableBackdropBlur`, `BlurMode`, `GlassBlur`, `GlassSaturation`, `GlassTint`/`GlassTintOpacity`, the liquid-glass optics, `GlassFill`/`GlassGradient`/`GlassFillOpacity` and `GlassPattern`/`GlassPatternColor`/`GlassPatternOpacity`. The glass is painted first, so its backdrop holds only what lies behind the control.

## Key Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `EnableBackdropBlur` / `BlurMode` | `True` / `SkiaSharp` | Blurs the dimmed page behind the dialog. |
| `GlassBlur` | 50 | Blur strength. |
| `GlassColorOpacity` | 0.3 | Opacity of `Background` over the glass. |

## Quick Examples

```xml
<controls:DaisyGlassModal x:Name="Dialog" DialogMaxWidth="360">
    <StackPanel Spacing="12">
        <TextBlock Text="Glass dialog" FontSize="22" FontWeight="Bold" Foreground="White" />
        <controls:DaisyGlassButton Content="Close" HorizontalAlignment="Right" Click="CloseDialog_Click" />
    </StackPanel>
</controls:DaisyGlassModal>
```

## Tips & Best Practices

- The modal covers its container; place it in the area that should be dimmed and blurred, for example the window root or a single panel.
