<!-- Supplementary documentation for DaisyBackdrop -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

`DaisyBackdrop` is the lowest layer of a window: it draws the backdrop of the theme and shows its content over it. The backdrop switches with the theme, so an app needs no handling of its own. Glass only reads as glass over a varied backdrop, so the backdrop gives the glass of the glass themes and the glass mode something to show.

The backdrop is two theme resources:

| Resource | Type | Description |
| -------- | ---- | ----------- |
| `DaisyBackdropBrush` | `IBrush` | The static backdrop: a color, a gradient, a drawing or an `ImageBrush`. A theme can ship its image as `avares://`. |
| `DaisyBackdropEffect` | `DaisyGlassBackground` | Optional: an animated effect over the brush, such as Plasma, Aurora or Liquid Gold, as gradients or as shaders from Flowery.NET.Skia. A brush cannot animate itself, so the effect is a resource of its own. |

When a theme defines no backdrop, the brush is `Base100` of the theme. In the glass mode (`DaisyGlassSurface.GlobalEnabled`) it is soft glows in the primary and secondary color of the theme on `Base100`. Glassy defines a violet and blue wave backdrop.

## Properties

| Property | Description |
| -------- | ----------- |
| `Background` | The brush; the theme sets it to `DaisyBackdropBrush`. |
| `BackdropEffect` | The animated effect over the brush; the theme sets it to `DaisyBackdropEffect`. With reduced motion it shows a still frame; with high contrast it is hidden, and the brush is the plain background of the contrast colors. |
| `Content` | The content over the backdrop. |

## Usage

As the root of a window, with the content inside:

```xml
<Window xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET">
    <controls:DaisyBackdrop>
        <local:MainView />
    </controls:DaisyBackdrop>
</Window>
```

As the lowest layer of a grid, under the rest:

```xml
<Grid>
    <controls:DaisyBackdrop />
    <local:MainView />
</Grid>
```

An app that wants only the brush, without the effect, uses the resource directly:

```xml
<Window Background="{DynamicResource DaisyBackdropBrush}" />
```

## A Theme With a Backdrop

A palette sets the brush, and optionally the effect, like any other theme resource:

```xml
<ImageBrush x:Key="DaisyBackdropBrush" Source="avares://MyApp/Assets/backdrop.jpg" Stretch="UniformToFill" />
<controls:DaisyGlassGradientBackground x:Key="DaisyBackdropEffect" Effect="Aurora" Speed="0.4" FrameRate="20" />
```

## A Backdrop Chosen at Run Time

`DaisyThemeManager.BackdropOverride` replaces the backdrop of every theme and stays across theme switches. It is a short text that an app saves next to the theme name; the library stores nothing itself:

```csharp
DaisyThemeManager.BackdropOverride = "effect:Aurora";              // an effect, slow and at 20 frames per second
DaisyThemeManager.BackdropOverride = @"image:C:\Pictures\bg.jpg";  // an image file, a file: or an avares:// URI
DaisyThemeManager.BackdropOverride = "#0B1B5E";                    // a color
DaisyThemeManager.BackdropOverride = null;                         // the backdrop of the theme again

settings.Backdrop = DaisyThemeManager.BackdropOverride;            // "effect:Aurora", "#0B1B5E", ...
```

- Text in none of these forms throws a `FormatException`. The getter returns the text in its normal form.
- The library loads and decodes an image itself, at most 3840 pixels wide, and scales it to fill the backdrop (`UniformToFill`). An image that cannot be loaded leaves the backdrop of the theme. Set an image after the application has started.
- `BackdropOverrideChanged` fires after every change. `FloweryComponentSidebar` offers a backdrop selector (`SidebarBackdropItem`): the backdrop of the theme, the effects, a color or an image file.
