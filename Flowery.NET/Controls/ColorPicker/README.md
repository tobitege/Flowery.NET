# Color Picker Controls

This is a `netstandard2.0` port of the great, MIT licensed `ColorPicker` component set:
[Cyotek.Windows.Forms.ColorPicker](https://github.com/cyotek/Cyotek.Windows.Forms.ColorPicker)

## 1. **HslColor** (`Flowery.NET\Controls\ColorPicker\HslColor.cs`)

A struct for representing colors in HSL (Hue, Saturation, Lightness) color space with conversion methods to/from RGB.

## 2. **ColorCollection** (`Flowery.NET\Controls\ColorPicker\ColorCollection.cs`)

A collection class for managing colors with predefined palettes:

- `NamedColors` - Standard named colors
- `Office2010` - Office 2010 color palette
- `Paint` - Paint.NET style palette
- `WebSafe` - 216 web-safe colors
- Helper methods for creating grayscale and custom palettes

## 3. **DaisyColorWheel** (`Flowery.NET\Controls\ColorPicker\DaisyColorWheel.cs`)

A circular color wheel control for selecting hue and saturation values:

- Renders a full HSL color wheel
- Selection marker with customizable size and outline
- Configurable lightness level
- Optional center crosshairs
- Mouse drag support for color selection

## 4. **DaisyColorGrid** (`Flowery.NET\Controls\ColorPicker\DaisyColorGrid.cs`)

A grid control for displaying and selecting colors from palettes:

- Configurable cell size, spacing, and columns
- Support for main colors and custom colors sections
- Selection highlighting and hover effects
- Checkerboard pattern for transparent colors

## 5. **DaisyColorSlider** (`Flowery.NET\Controls\ColorPicker\DaisyColorSlider.cs`)

A slider control for individual color channel selection:

- Supports RGB channels (Red, Green, Blue, Alpha)
- Supports HSL channels (Hue, Saturation, Lightness)
- Horizontal/vertical orientation
- Gradient rendering with alpha checkerboard

## 6. **DaisyColorEditor** (`Flowery.NET\Controls\ColorPicker\DaisyColorEditor.cs`)

A comprehensive color editor with sliders and numeric inputs:

- RGB and HSL slider groups
- Hex color input
- NumericUpDown controls for precise values
- Configurable visibility for alpha, RGB, and HSL sections

## 7. **DaisyScreenColorPicker** (`Flowery.NET\Controls\ColorPicker\DaisyScreenColorPicker.cs`)

An eyedropper tool for picking colors:

- Preview display with zoom
- Click to start capture mode
- Platform-specific hooks for Windows screen capture

## 8. **DaisyColorPickerDialog** (`Flowery.NET\Controls\ColorPicker\DaisyColorPickerDialog.cs`)

The main dialog combining all components:

- Color wheel with lightness slider
- Color grid with custom colors
- Full color editor with RGB/HSL sliders
- Original vs new color comparison
- OK/Cancel buttons
- Static `ShowDialogAsync` method for easy usage

## AXAML Themes

- `DaisyColorWheel.axaml`
- `DaisyColorGrid.axaml`
- `DaisyColorSlider.axaml`
- `DaisyColorEditor.axaml`
- `DaisyScreenColorPicker.axaml`
- `DaisyColorPicker.axaml` (combines all)

## Gallery Example

A comprehensive example page (`ColorPickerExamples.axaml`) demonstrating all components.

## Usage Example

```csharp
// Simple usage
var result = await DaisyColorPickerDialog.ShowDialogAsync(ownerWindow, Colors.Red);
if (result.HasValue)
{
    // User selected a color
    var selectedColor = result.Value;
}

// With options
var result = await DaisyColorPickerDialog.ShowDialogAsync(
    ownerWindow,
    initialColor: Colors.Blue,
    showAlphaChannel: true,
    customColors: myCustomColorCollection);
```

The implementation mirrors the key features of Cyotek's ColorPickerDialog while being fully cross-platform with Avalonia UI.
