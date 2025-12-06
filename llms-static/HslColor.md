<!-- Supplementary documentation for HslColor -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

HslColor is a struct representing colors in the HSL (Hue, Saturation, Lightness) color space. It provides seamless conversion between HSL and RGB color spaces, making it essential for color picker controls that need intuitive color manipulation.

## Why HSL?

HSL is more intuitive than RGB for color selection:

| Color Space | Best For |
|-------------|----------|
| **RGB** | Precise color values, web colors, digital displays |
| **HSL** | Intuitive color adjustment—change brightness without affecting hue |

## Properties

| Property | Type | Range | Description |
|----------|------|-------|-------------|
| `H` | `double` | 0-359 | Hue angle in degrees (0=red, 120=green, 240=blue) |
| `S` | `double` | 0-1 | Saturation (0=gray, 1=vivid) |
| `L` | `double` | 0-1 | Lightness (0=black, 0.5=pure color, 1=white) |
| `A` | `int` | 0-255 | Alpha/transparency |
| `IsEmpty` | `bool` | - | Whether this is an empty color |

## Static Members

| Member | Description |
|--------|-------------|
| `HslColor.Empty` | Represents an empty/undefined HSL color |

## Constructors

```csharp
// From HSL values (alpha defaults to 255)
var hsl = new HslColor(180, 0.8, 0.5);  // Cyan-ish

// From HSL values with alpha
var hsl = new HslColor(255, 180, 0.8, 0.5);  // Fully opaque cyan

// From Avalonia Color
var hsl = new HslColor(Colors.Red);
```

## Conversion Methods

```csharp
// HSL to RGB
HslColor hsl = new HslColor(0, 1, 0.5);  // Pure red in HSL
Color rgb = hsl.ToRgbColor();             // Convert to RGB

// With custom alpha
Color rgb = hsl.ToRgbColor(128);          // 50% transparent

// Static conversion
Color rgb = HslColor.HslToRgb(180, 0.8, 0.5);
Color rgb = HslColor.HslToRgb(255, 180, 0.8, 0.5);  // With alpha

// RGB to HSL
HslColor.RgbToHsl(255, 0, 0, out double h, out double s, out double l);
```

## Implicit Conversions

HslColor supports implicit conversion to/from Avalonia `Color`:

```csharp
// HSL to Color (implicit)
HslColor hsl = new HslColor(120, 1, 0.5);
Color color = hsl;  // Implicit conversion

// Color to HSL (implicit)
Color color = Colors.Blue;
HslColor hsl = color;  // Implicit conversion
```

## Hue Reference

| Hue (degrees) | Color |
|---------------|-------|
| 0 | Red |
| 30 | Orange |
| 60 | Yellow |
| 120 | Green |
| 180 | Cyan |
| 240 | Blue |
| 270 | Purple |
| 300 | Magenta |
| 330 | Pink |

## Saturation & Lightness

| Saturation | Effect |
|------------|--------|
| 0.0 | Grayscale (no color) |
| 0.5 | Muted/pastel |
| 1.0 | Full vivid color |

| Lightness | Effect |
|-----------|--------|
| 0.0 | Black |
| 0.25 | Dark shade |
| 0.5 | Pure color |
| 0.75 | Light tint |
| 1.0 | White |

## Quick Examples

```csharp
// Create a pure blue
var blue = new HslColor(240, 1, 0.5);

// Create a pastel pink
var pastelPink = new HslColor(350, 0.5, 0.8);

// Create a dark green
var darkGreen = new HslColor(120, 1, 0.25);

// Adjust lightness without changing hue
var hsl = new HslColor(Colors.Red);
hsl.L = 0.75;  // Make it lighter
Color lighterRed = hsl.ToRgbColor();

// Adjust saturation
hsl.S = 0.3;  // Make it more gray/muted
Color mutedRed = hsl.ToRgbColor();

// Compare colors
var a = new HslColor(180, 0.5, 0.5);
var b = new HslColor(180, 0.5, 0.5);
bool equal = (a == b);  // true
```

## Tips & Best Practices

- Use HSL when you need to adjust brightness or saturation without changing the base color.
- Hue wraps around: 360° = 0° (both are red).
- For grayscale colors, saturation is 0 and hue is irrelevant.
- Lightness of 0.5 gives the purest representation of the hue.
- Use implicit conversions for clean code when working with Avalonia controls.
- The `IsEmpty` property is useful for nullable color scenarios.

---

