<!-- Supplementary documentation for DaisyGlassDesigner -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyGlassDesigner is a visual editor for a glass look, which applies to every control on glass ([DaisyGlassSurface](DaisyGlassSurface.md)). A live preview sits on a colorful stage; the settings are grouped in tabs; the result comes out as XAML, C# or JSON with the attached glass properties, or applies to a control. Embed it in a page like any control, or open it over the app with `ShowOverlayAsync`, which works on desktop, mobile and in the browser because it uses the overlay layer of the window or view.

The preview is a `DaisyButton` with `DaisyGlassButtonTheme`. Its text, variant, style, size and shape are preview options: they show how the look sits on a button, and they are not part of the design. A `Square` or `Circle` preview grows until the whole text fits inside the shape, and a `Block` preview takes the width of the stage.

## Tabs

| Tab | Settings |
| --- | -------- |
| Presets | Ten starting looks: Frost, Midnight, Aurora, Sunset, Holo, Neon, Ocean, Candy, Liquid Gold, Plasma. A preset also sets the preview button it was made for (variant, style and shape); the text and size stay. |
| Button | The preview options `Variant`, `ButtonStyle`, `Size` and `Shape`, and `ColorOpacity`, `TextShadowOpacity` and `TextContrast` of the design. The text of the preview has its own field above the tabs. |
| Glass | `BlurMode`, `EnableBackdropBlur`, `Blur`, `Saturation`, `Tint` and `TintOpacity`, the reflection, the simulated opacity, the liquid optics (`Depth`, `Curvature`, `Bend`, `Dispersion`) and the lens (`Refraction`, `Bezel`, `Zoom`, `RimSaturation`), which only the `SkiaSharp` mode draws. |
| Pattern | The `DaisyCardPattern` set as tiles, the pattern color and opacity. |
| Fill | None, a `Gradient` preset, or a linear, radial or conic gradient with two to five colors (the model and JSON take any number), its angle and `FillOpacity`. Ten palettes (Sunrise, Lagoon, Forest, Berry, Spring, Candy, Ember, Twilight, Neon, Pearl) set the colors of a gradient, which stay editable; on no fill or a preset they start a linear gradient. |
| Animation | An animated `Background`: drawn with gradients (`DaisyGlassGradientBackground`) or shaders (`DaisyGlassShaderBackground`), its `Effect`, `Speed`, `FrameRate` and opacity. `LiquidColor` adds its color: the accent color of the system, or one picked with the swatch (`BackgroundColor` of the design). |

Fill and animated background are layers, not alternatives: the glass draws the blurred backdrop, the tint, the fill at `FillOpacity`, the animated background at `BackgroundOpacity` and the pattern, in this order. The animated background therefore covers the fill at its opacity and hides it at 1. The Fill and Animation tabs sit next to each other and say so.

Open, Save and Reset sit next to the tabs, so they are at hand on every tab. Open and Save use the file dialog of the platform (`TopLevel.StorageProvider`) and read and write the design as a JSON file; a file that is no design shows why below the tabs and leaves the design as it is. The stage offers four backdrops and a disc that drifts behind the preview, so the blur is visible. The disc is off by default and stops when the designer leaves the visual tree. The `Code` button opens the code drawer with the XAML, C# and JSON of the design, a copy button, and an import field for JSON.

## Key Members

| Member | Description |
| ------ | ----------- |
| `Design` | The design being edited, as a `DaisyGlassDesign`. Getting returns a copy; setting loads a copy. |
| `DesignChanged` | Raised when the design changes through the editor, a preset, an import or `Design`. |
| `PreviewButton` | The button of the preview. |
| `PreviewText` / `PreviewVariant` / `PreviewButtonStyle` / `PreviewSize` / `PreviewShape` | The preview options. |
| `Xaml` / `CSharp` / `Json` | The current design as code. |
| `LoadFrom(control)` / `ApplyTo(control)` | Reads the glass look of a control, or puts a control on glass with the current look. A `DaisyButton` also sets the preview options. |
| `ImportJson(json)` | Loads a design written by `Json`; throws `JsonException` for anything else. |
| `SaveAsync(stream)` / `LoadAsync(stream)` | Write the design as JSON (UTF-8) to a stream, replacing what a seekable stream held, and load it again; `LoadAsync` throws `JsonException` for anything that is no design and leaves the design as it is. |
| `Reset()` | Returns to the default look; the preview options stay. |
| `ShowOverlayAsync(target, initial)` (static) | Opens a designer over the app; returns the design on Apply, `null` on Cancel, Escape or when the window closes. The keyboard stays inside the overlay, and the focus returns where it was. |
| `EditAsync(control)` (static) | Opens a designer with the glass look of `control` and applies the result to it; returns whether the user applied. On a control on glass only what the user changed is written, so the styles and the glass theme of the control keep applying to the rest; a control not on glass yet gets the whole design. A `DaisyButton` sets the preview options, which do not change the button. |

## DaisyGlassDesign

`DaisyGlassDesign` holds a glass look as plain values, so apps can store, share and apply designs without the editor. Its defaults are those of `DaisyGlassButtonTheme`. The names of its values are those of the attached properties of `DaisyGlassSurface`.

| Member | Description |
| ------ | ----------- |
| `ApplyTo(control)` | Switches `DaisyGlassSurface.IsEnabled` on and sets the attached glass properties; a fill or background of kind `Keep` stays as it is. |
| `FromControl(control)` (static) | Reads a control: a setting it does not set comes from its glass. A fill or animated background the design cannot describe (a solid or image brush, a gradient with another geometry, opacity, transform or spread, a custom background) reads as `Keep`, and applying the design leaves it as it is. |
| `ToXaml(prefix, element)` / `ToCSharp(target)` | The design as attached properties on an element, or as `DaisyGlassSurface.Set…` calls. Without `element` or `target` the code is a glass `DaisyButton` and holds only the values that differ from its theme, and the tint, which the theme leaves to the color theme; for another element or control it holds every value, since no theme gives that glass the look of the design. |
| `ToJson()` / `FromJson(json)` (static) | JSON with camel-case names, enum names and `#AARRGGBB` colors; missing values keep their defaults. `FromJson` throws a `JsonException` for an unknown enum value, an empty fill stop or a number that is not finite or out of range. |
| `CreateFill()` / `CreateBackground()` | The fill brush and the animated background of the design. |
| `ShaderBackgroundAvailable` (static) | Whether shader backgrounds can be created, which needs Flowery.NET.Skia. Without it a shader background falls back to the gradient implementation. |

## Quick Examples

```xml
<!-- Embedded -->
<controls:DaisyGlassDesigner x:Name="Designer" />
```

```csharp
// Edit the glass of an existing control over the app
await DaisyGlassDesigner.EditAsync(myGlassButton);

// Ask for a design and keep it as JSON
if (await DaisyGlassDesigner.ShowOverlayAsync(this) is { } design)
    File.WriteAllText("glass.json", design.ToJson());

// Apply a stored design to any control on glass
DaisyGlassDesign.FromJson(json).ApplyTo(myCard);
```

## Using a Design in Your App

The `?` button next to Open and Save sums this up in a glass dialog and links here. There are two ways to take a design from the designer into an app.

**Code.** Open the Code drawer and copy the XAML or C# into your page. It sets the attached glass properties of `DaisyGlassSurface` on the control, so the look is part of your markup and needs no file at run time:

```xml
<controls:DaisyButton Theme="{StaticResource DaisyGlassButtonTheme}" Content="Continue"
                      controls:DaisyGlassSurface.Gradient="Aurora"
                      controls:DaisyGlassSurface.Blur="40" />
```

**File.** Save the design as JSON and load it at run time. This keeps looks out of the markup, lets several controls share one look, and lets you change a look by replacing the file. In Visual Studio:

1. Add the saved file to the project, for example as `Assets/Glass/MyLook.json`.
2. Set its **Build Action** to `AvaloniaResource` in the Properties window. Projects made from the Avalonia templates already include `Assets/**` as `AvaloniaResource`:

   ```xml
   <ItemGroup>
     <AvaloniaResource Include="Assets\**" />
   </ItemGroup>
   ```

3. Load the file and apply it to any control, which puts the control on glass:

   ```csharp
   using Avalonia.Platform;
   using Flowery.Controls;

   using var stream = AssetLoader.Open(new Uri("avares://MyApp/Assets/Glass/MyLook.json"));
   using var reader = new StreamReader(stream);
   var design = DaisyGlassDesign.FromJson(reader.ReadToEnd());
   design.ApplyTo(saveButton);
   design.ApplyTo(cancelButton);
   ```

   `MyApp` is the assembly name of the project. `FromJson` throws a `JsonException` for a file that is no design.

A file next to the app instead of inside it, such as a look the user picks, loads the same way with `File.ReadAllText`. To edit a stored design again, open the file in the designer, or load it in code with `designer.Design = DaisyGlassDesign.FromJson(json)` or `await designer.LoadAsync(stream)`.

## Tips & Best Practices

- Shader backgrounds need `FlowerySkia.Register()`; the Animation tab disables the Shader option without it, and the live blur falls back to `BitmapCapture` (the Glass tab says so).
- The exported XAML of a shader background uses the `skia` prefix; the snippet starts with a comment that names the namespace.
- A preview with an animated background redraws at its `FrameRate`; the presets show still buttons.
- The labels follow `FloweryLocalization`; control and enum names stay in English.
