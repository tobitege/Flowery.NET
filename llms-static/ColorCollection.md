<!-- Supplementary documentation for ColorCollection -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

ColorCollection is a collection class for managing colors with support for predefined palettes. It implements `IList<Color>` and provides factory methods for creating standard color palettes like Named Colors, Office 2010, MS Paint, Web Safe, and custom grayscale gradients.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count` | `int` | Number of colors in the collection |
| `IsReadOnly` | `bool` | Always `false` |
| `this[int]` | `Color` | Indexer for accessing colors |

## Events

| Event | Description |
|-------|-------------|
| `CollectionChanged` | Fired when the collection is modified |

## Constructors

```csharp
// Empty collection
var colors = new ColorCollection();

// From existing colors
var colors = new ColorCollection(new[] 
{ 
    Colors.Red, 
    Colors.Green, 
    Colors.Blue 
});

// From another collection
var colors = new ColorCollection(existingColorList);
```

## Factory Methods (Built-in Palettes)

| Method | Colors | Description |
|--------|--------|-------------|
| `CreateNamedColors()` | 140+ | All CSS/HTML named colors |
| `CreateOffice2010()` | 70 | Microsoft Office 2010 theme colors |
| `CreatePaint()` | 48 | MS Paint default palette |
| `CreateWebSafe()` | 216 | Web-safe 216-color palette |
| `CreateGrayscale(n)` | n | Custom grayscale gradient with n steps |

```csharp
// Create built-in palettes
var named = ColorCollection.CreateNamedColors();
var office = ColorCollection.CreateOffice2010();
var paint = ColorCollection.CreatePaint();
var webSafe = ColorCollection.CreateWebSafe();

// Create grayscale palette
var grays16 = ColorCollection.CreateGrayscale(16);   // 16 shades
var grays256 = ColorCollection.CreateGrayscale(256); // Full grayscale
```

## Collection Methods

```csharp
var colors = new ColorCollection();

// Add colors
colors.Add(Colors.Red);
colors.AddRange(new[] { Colors.Green, Colors.Blue });

// Remove colors
colors.Remove(Colors.Red);
colors.RemoveAt(0);
colors.Clear();

// Query
bool hasRed = colors.Contains(Colors.Red);
int index = colors.IndexOf(Colors.Blue);

// Find with tolerance
int index = colors.Find(someColor);           // Exact match
int index = colors.Find(someColor, 10);       // Within tolerance

// Insert
colors.Insert(0, Colors.Yellow);

// Copy
var array = new Color[colors.Count];
colors.CopyTo(array, 0);
```

## Find Method

The `Find` method locates a color in the collection with optional tolerance:

```csharp
// Exact match
int index = colors.Find(Colors.Red);  // Returns -1 if not found

// With tolerance (useful for similar colors)
int index = colors.Find(someColor, tolerance: 10);
// Tolerance compares R, G, B, A components within ±tolerance
```

## Quick Examples

```csharp
// Use with DaisyColorGrid
var grid = new DaisyColorGrid
{
    Palette = ColorCollection.CreateOffice2010()
};

// Custom palette for a specific app
var brandColors = new ColorCollection(new[]
{
    Color.FromRgb(0x1E, 0x90, 0xFF),  // Brand blue
    Color.FromRgb(0xFF, 0x69, 0xB4),  // Brand pink
    Color.FromRgb(0x32, 0xCD, 0x32),  // Brand green
});

// Combine palettes
var combined = new ColorCollection();
combined.AddRange(ColorCollection.CreatePaint());
combined.AddRange(brandColors);

// React to changes
colors.CollectionChanged += (s, e) =>
{
    RefreshColorGrid();
};

// XAML binding (static palettes)
// <colorpicker:DaisyColorGrid Palette="{x:Static colorpicker:ColorCollection.Office2010}" />
```

## Palette Previews

### Named Colors (140+)
Standard CSS/HTML color names: AliceBlue, AntiqueWhite, Aqua, Aquamarine, Azure, Beige, Bisque, Black, BlanchedAlmond, Blue, BlueViolet, Brown, BurlyWood, CadetBlue, Chartreuse, Chocolate, Coral, CornflowerBlue, Cornsilk, Crimson, Cyan, DarkBlue, DarkCyan, DarkGoldenrod, DarkGray, DarkGreen, DarkKhaki, DarkMagenta, DarkOliveGreen, DarkOrange, DarkOrchid, DarkRed, DarkSalmon, DarkSeaGreen, DarkSlateBlue, DarkSlateGray, DarkTurquoise, DarkViolet, DeepPink, DeepSkyBlue, DimGray, DodgerBlue, Firebrick, FloralWhite, ForestGreen, Fuchsia, Gainsboro, GhostWhite, Gold, Goldenrod, Gray, Green, GreenYellow, Honeydew, HotPink, IndianRed, Indigo, Ivory, Khaki, Lavender, LavenderBlush, LawnGreen, LemonChiffon, LightBlue, LightCoral, LightCyan, LightGoldenrodYellow, LightGray, LightGreen, LightPink, LightSalmon, LightSeaGreen, LightSkyBlue, LightSlateGray, LightSteelBlue, LightYellow, Lime, LimeGreen, Linen, Magenta, Maroon, MediumAquamarine, MediumBlue, MediumOrchid, MediumPurple, MediumSeaGreen, MediumSlateBlue, MediumSpringGreen, MediumTurquoise, MediumVioletRed, MidnightBlue, MintCream, MistyRose, Moccasin, NavajoWhite, Navy, OldLace, Olive, OliveDrab, Orange, OrangeRed, Orchid, PaleGoldenrod, PaleGreen, PaleTurquoise, PaleVioletRed, PapayaWhip, PeachPuff, Peru, Pink, Plum, PowderBlue, Purple, Red, RosyBrown, RoyalBlue, SaddleBrown, Salmon, SandyBrown, SeaGreen, SeaShell, Sienna, Silver, SkyBlue, SlateBlue, SlateGray, Snow, SpringGreen, SteelBlue, Tan, Teal, Thistle, Tomato, Turquoise, Violet, Wheat, White, WhiteSmoke, Yellow, YellowGreen

### Office 2010 (70)
Professional theme colors organized in rows: base colors, tints, and shades matching Microsoft Office 2010 color themes.

### Paint (48)
Classic MS Paint palette with primary colors, secondary colors, and common variations.

### Web Safe (216)
The 216 colors that display consistently across all browsers and systems (6×6×6 RGB cube).

## Tips & Best Practices

- Use **Named Colors** for general-purpose color selection.
- Use **Office 2010** for professional/business applications.
- Use **Paint** for simple, familiar color selection.
- Use **Web Safe** when colors must be web-compatible.
- Use `CreateGrayscale()` for monochrome/grayscale applications.
- The `Find` method with tolerance is useful when colors might have slight variations.
- Subscribe to `CollectionChanged` to refresh UI when custom colors are modified.
- Reuse `ColorCollection` instances across dialog sessions to persist custom colors.

---

