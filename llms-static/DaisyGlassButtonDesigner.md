<!-- Supplementary documentation for DaisyGlassButtonDesigner -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassButtonDesigner is a visual editor for [DaisyGlassButton](DaisyGlassButton.md). A live preview sits on a colorful stage; the settings are grouped in tabs; the result comes out as XAML, C# or JSON, or applies to a button. Embed it in a page like any control, or open it over the app with `ShowOverlayAsync`, which works on desktop, mobile and in the browser because it uses the overlay layer of the window or view.

## Tabs

| Tab | Settings |
| --- | -------- |
| Presets | Ten starting looks: Frost, Midnight, Aurora, Sunset, Holo, Neon, Ocean, Candy, Liquid Gold, Plasma. A preset keeps the button text. |
| Button | Text, `Variant`, `ButtonStyle`, `Size`, `Shape`, `GlassColorOpacity`, `GlassTextShadowOpacity`, `GlassTextContrast`. |
| Glass | `BlurMode`, `EnableBackdropBlur`, `GlassBlur`, `GlassSaturation`, `GlassTint` and `GlassTintOpacity`, the reflection, the simulated opacity and the liquid optics (`GlassDepth`, `GlassCurvature`, `GlassBend`, `GlassDispersion`). |
| Fill | None, a `GlassGradient` preset, or a linear, radial or conic gradient with two to five colors (the model and JSON take any number), its angle and `GlassFillOpacity`. |
| Pattern | The `DaisyCardPattern` set as tiles, the pattern color and opacity. |
| Motion | An animated `GlassBackground`: drawn with gradients (`DaisyGlassGradientBackground`) or shaders (`DaisyGlassShaderBackground`), its `Effect`, `Speed`, `FrameRate` and opacity. |

The stage offers four backdrops and a disc that drifts behind the button, so the blur is visible. The disc is off by default and stops when the designer leaves the visual tree. The `Code` button opens the code drawer with the XAML, C# and JSON of the design, a copy button, and an import field for JSON.

## Key Members

| Member | Description |
| ------ | ----------- |
| `Design` | The design being edited, as a `DaisyGlassButtonDesign`. Getting returns a copy; setting loads a copy. |
| `DesignChanged` | Raised when the design changes through the editor, a preset, an import or `Design`. |
| `PreviewButton` | The button of the preview. |
| `Xaml` / `CSharp` / `Json` | The current design as code. |
| `LoadFrom(button)` / `ApplyTo(button)` | Reads the look of a button, or gives a button the current look; `ApplyTo` keeps the button's content. |
| `ImportJson(json)` | Loads a design written by `Json`; throws `JsonException` for anything else. |
| `Reset()` | Returns to the default look and keeps the text. |
| `ShowOverlayAsync(target, initial)` (static) | Opens a designer over the app; returns the design on Apply, `null` on Cancel, Escape or when the window closes. The keyboard stays inside the overlay, and the focus returns where it was. |
| `EditAsync(button)` (static) | Opens a designer with the look of `button` and applies the result to it; returns whether the user applied. A text content can be edited too; other content stays, and the designer then offers no text. |

## DaisyGlassButtonDesign

`DaisyGlassButtonDesign` holds the look as plain values, so apps can store, share and apply designs without the editor. Its defaults are those of a `DaisyGlassButton` in the Flowery theme.

| Member | Description |
| ------ | ----------- |
| `ApplyTo(button, includeContent)` | Sets every glass and button property of the design on `button`. |
| `FromButton(button)` (static) | Reads a button. A fill or animated background the design cannot describe (a solid or image brush, a radial gradient with another geometry, a custom background) reads as `Keep`, and applying the design leaves it as it is. |
| `ToXaml(prefix)` / `ToCSharp()` | The values that differ from the defaults, as a `DaisyGlassButton` element or a C# initializer. |
| `ToJson()` / `FromJson(json)` (static) | JSON with camel-case names, enum names and `#AARRGGBB` colors; missing values keep their defaults. `FromJson` throws a `JsonException` for an unknown enum value, an empty fill stop or a number that is not finite or out of range. |
| `CreateFill()` / `CreateBackground()` | The fill brush and the animated background of the design. |
| `ShaderBackgroundAvailable` (static) | Whether shader backgrounds can be created, which needs Flowery.NET.Skia. Without it a shader background falls back to the gradient implementation. |

## Quick Examples

```xml
<!-- Embedded -->
<controls:DaisyGlassButtonDesigner x:Name="Designer" />
```

```csharp
// Edit an existing button over the app
await DaisyGlassButtonDesigner.EditAsync(myGlassButton);

// Ask for a design and keep it as JSON
if (await DaisyGlassButtonDesigner.ShowOverlayAsync(this) is { } design)
    File.WriteAllText("button.json", design.ToJson());

// Apply a stored design
DaisyGlassButtonDesign.FromJson(json).ApplyTo(myGlassButton);
```

## Tips & Best Practices

- Shader backgrounds need `FlowerySkia.Register()`; the Motion tab disables the Shader option without it, and the live blur falls back to `BitmapCapture` (the Glass tab says so).
- The exported XAML of a shader background uses the `skia` prefix; the snippet starts with a comment that names the namespace.
- A preview with an animated background redraws at its `FrameRate`; the presets show still buttons.
- The labels follow `FloweryLocalization`; control and enum names stay in English.
