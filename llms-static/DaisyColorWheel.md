<!-- Supplementary documentation for DaisyColorWheel -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyColorWheel is a circular color wheel control for selecting hue and saturation values in the HSL color space. It renders a full-spectrum color wheel with a draggable selection marker, allowing intuitive color picking through mouse interaction. The wheel displays colors at a configurable lightness level.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Color` | `Color` | `Red` | The selected RGB color |
| `HslColor` | `HslColor` | `H:0, S:1, L:0.5` | The HSL representation of the color |
| `Lightness` | `double` | `0.5` | Lightness value (0-1) for the wheel display |
| `SelectionSize` | `double` | `10` | Size of the selection marker circle |
| `SelectionOutlineColor` | `Color` | `White` | Color of the selection marker outline |
| `ShowCenterLines` | `bool` | `false` | Whether to show center crosshairs |
| `ColorStep` | `int` | `4` | Rendering quality (lower = smoother, higher = faster) |
| `OnColorChanged` | `Action<Color>?` | `null` | Optional callback invoked when color changes |

## How It Works

The color wheel maps:

- **Angle** (0-360°) → **Hue** (color around the wheel)
- **Distance from center** (0 to radius) → **Saturation** (center = gray, edge = vivid)
- **Lightness** is controlled separately via the `Lightness` property

Clicking or dragging on the wheel updates both the `Color` and `HslColor` properties.

## Events and Callbacks

### Event-Based

```csharp
colorWheel.ColorChanged += (sender, e) => 
{
    // e.Color contains the new color
    UpdatePreview(e.Color);
};
```

### Callback-Based

```xml
<colorpicker:DaisyColorWheel 
    OnColorChanged="{Binding HandleWheelColorChanged}" />
```

```csharp
// Direct assignment
var wheel = new DaisyColorWheel
{
    OnColorChanged = color => SelectedColor = color
};
```

## Quick Examples

```xml
<!-- Basic color wheel -->
<colorpicker:DaisyColorWheel 
    Width="200" 
    Height="200" 
    Color="{Binding SelectedColor, Mode=TwoWay}" />

<!-- With lightness control -->
<StackPanel Spacing="8">
    <colorpicker:DaisyColorWheel 
        x:Name="wheel"
        Width="200" 
        Height="200"
        Lightness="{Binding #lightnessSlider.Value}" />
    
    <Slider 
        x:Name="lightnessSlider" 
        Minimum="0" 
        Maximum="1" 
        Value="0.5" />
</StackPanel>

<!-- High quality rendering -->
<colorpicker:DaisyColorWheel 
    ColorStep="1"
    Width="300" 
    Height="300" />

<!-- With center crosshairs -->
<colorpicker:DaisyColorWheel 
    ShowCenterLines="True"
    SelectionSize="12"
    SelectionOutlineColor="Black" />

<!-- With callback -->
<colorpicker:DaisyColorWheel 
    OnColorChanged="{Binding OnWheelColorSelected}" />
```

## HslColor Struct

The `HslColor` struct represents colors in HSL space:

| Property | Type | Range | Description |
|----------|------|-------|-------------|
| `H` | `double` | 0-359 | Hue angle in degrees |
| `S` | `double` | 0-1 | Saturation (0 = gray, 1 = vivid) |
| `L` | `double` | 0-1 | Lightness (0 = black, 0.5 = pure, 1 = white) |
| `A` | `int` | 0-255 | Alpha/transparency |

```csharp
// Create from HSL values
var hsl = new HslColor(180, 0.8, 0.5); // Cyan-ish

// Create from RGB Color
var hsl = new HslColor(Colors.Red);

// Convert to RGB
Color rgb = hsl.ToRgbColor();

// Implicit conversions
Color color = hsl;           // HSL to Color
HslColor hsl2 = Colors.Blue; // Color to HSL
```

## Tips & Best Practices

- Pair with a **lightness slider** (`DaisyColorSlider` with `Channel="Lightness"`) for complete HSL control.
- Use `ColorStep="1"` for smooth gradients on larger wheels; use higher values for better performance.
- The wheel automatically maintains the current `Lightness` when selecting colors.
- Use `ShowCenterLines="True"` to help users find the exact center (gray/neutral colors).
- For dark themes, consider setting `SelectionOutlineColor` to a lighter color for visibility.

## Acknowledgments

This control is part of the Flowery.NET color picker suite, inspired by the [Cyotek ColorPicker](https://github.com/cyotek/Cyotek.Windows.Forms.ColorPicker) library (MIT License).
