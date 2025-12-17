<!-- Supplementary documentation for FloweryScaleConverter and ScaleExtension -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

Flowery.NET provides two approaches for responsive scaling based on window size:

1. **`ScaleExtension`** (Recommended) - Markup extension with semantic presets
2. **`FloweryScaleConverter`** - Traditional converter for advanced scenarios

Both provide **continuous, real-time scaling** as the window resizes, unlike `FloweryResponsive` which uses discrete breakpoints.

## Quick Comparison

| Feature | ScaleExtension | FloweryScaleConverter |
|---------|---------------|----------------------|
| **XAML Syntax** | `{services:Scale FontTitle}` | Full binding with converter |
| **Window Detection** | Automatic | Manual (`ElementName=...`) |
| **Presets** | Built-in semantic names | Not available |
| **Customization** | Via `FloweryScaleConfig` | Converter properties |
| **Best For** | Most use cases | Complex multi-window scenarios |

---

## ScaleExtension (Recommended)

The cleanest way to use responsive scaling. Automatically finds the parent window and scales values.

## Basic Usage

```xml
<!-- Add namespace -->
xmlns:services="clr-namespace:Flowery.Services;assembly=Flowery.NET"

<!-- Using presets (cleanest) -->
<TextBlock FontSize="{services:Scale FontTitle}"/>
<TextBlock FontSize="{services:Scale FontBody}"/>
<Border Padding="{services:Scale SpacingMedium}"/>
<Grid Width="{services:Scale CardWidth}"/>

<!-- Using custom values (base,min) -->
<TextBlock FontSize="{services:Scale 24,12}"/>
<Border Width="{services:Scale 300}"/>

<!-- Using explicit properties -->
<TextBlock FontSize="{services:Scale Preset=Custom, BaseValue=24, MinValue=12}"/>
```

## Available Presets

### Font Sizes

| Preset | Base | Min | Use Case |
|--------|------|-----|----------|
| `FontDisplay` | 32 | 20 | Hero/display text |
| `FontTitle` | 28 | 18 | Page titles |
| `FontHeading` | 20 | 14 | Section headings |
| `FontSubheading` | 16 | 11 | Subheadings, emphasized text |
| `FontBody` | 14 | 10 | Body text |
| `FontCaption` | 12 | 9 | Captions, secondary text |
| `FontSmall` | 11 | 8 | Fine print, labels |
| `FontThemeLabel` | 13 | 11 | Theme selector labels |

### Spacing

| Preset | Base | Use Case |
|--------|------|----------|
| `SpacingXL` | 40 | Large gaps, section separators |
| `SpacingLarge` | 28 | Major element spacing |
| `SpacingMedium` | 20 | Standard padding/margins |
| `SpacingSmall` | 12 | Compact spacing |
| `SpacingXS` | 8 | Minimal spacing |

### Dimensions

| Preset | Base | Min | Use Case |
|--------|------|-----|----------|
| `CardWidth` | 400 | - | Card/container width |
| `CardHeight` | 120 | 100 | Card/container min height |
| `IconContainerLarge` | 140 | - | Large icon backgrounds |
| `IconContainerMedium` | 40 | - | Standard icon backgrounds |
| `IconContainerSmall` | 24 | - | Compact icon backgrounds |
| `IconLarge` | 32 | - | Large icons |
| `IconMedium` | 24 | - | Standard icons |
| `IconSmall` | 16 | - | Small icons |
| `ControlHeight` | 40 | - | Button/input heights |
| `Thumbnail` | 80 | - | Thumbnail images |
| `Avatar` | 48 | - | User avatars |
| `PanelLarge` | 250 | 200 | Large panels/dialogs |
| `PanelMedium` | 140 | - | Medium panels |

## Customizing Presets

Apps can override preset values at startup:

```csharp
// In App.axaml.cs or Program.cs
FloweryScaleConfig.SetPresetValues(FloweryScalePreset.FontTitle, 32, 22);
FloweryScaleConfig.SetPresetValues(FloweryScalePreset.CardWidth, 450);

// Adjust reference dimensions (default: 1920x1080)
FloweryScaleConfig.ReferenceWidth = 2560;  // For 1440p baseline
FloweryScaleConfig.ReferenceHeight = 1440;

// Adjust minimum scale factor (default: 0.5)
FloweryScaleConfig.MinScaleFactor = 0.6;  // Won't go below 60%
```

## Before & After Comparison

### Before (Verbose)

```xml
<TextBlock FontSize="{Binding $parent[Window].((vm:MainWindowViewModel)DataContext).WindowSize, 
    Converter={x:Static vm:MainWindowViewModel.ScaleConverter}, 
    ConverterParameter='16,11'}"/>

<Border Width="{Binding $parent[Window].((vm:MainWindowViewModel)DataContext).WindowSize, 
    Converter={x:Static vm:MainWindowViewModel.ScaleConverter}, 
    ConverterParameter=400}"/>

<Border Padding="{Binding $parent[Window].((vm:MainWindowViewModel)DataContext).WindowSize, 
    Converter={x:Static vm:MainWindowViewModel.ScaleConverter}, 
    ConverterParameter=20}"/>
```

### After (Clean)

```xml
<TextBlock FontSize="{services:Scale FontSubheading}"/>
<Border Width="{services:Scale CardWidth}"/>
<Border Padding="{services:Scale SpacingMedium}"/>
```

---

## FloweryScaleConverter (Advanced)

For scenarios where you need explicit control over the binding source or are working with multiple windows.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ReferenceWidth` | double | 1920 | Width for 100% scaling (Full HD) |
| `ReferenceHeight` | double | 1080 | Height for 100% scaling (Full HD) |
| `MinScaleFactor` | double | 0.5 | Minimum scale (50% of base value) |
| `DefaultMinFontSize` | double | 9.0 | Default minimum for font sizes |

## Usage

```xml
<Window.Resources>
    <services:FloweryScaleConverter x:Key="ScaleConverter"/>
</Window.Resources>

<!-- Requires explicit binding to window bounds -->
<TextBlock FontSize="{Binding Bounds, ElementName=RootWindow, 
    Converter={StaticResource ScaleConverter}, 
    ConverterParameter='24,12'}"/>
```

---

## How Scaling Works

1. **Calculate scale ratios** for width and height:
   - `widthScale = windowWidth / ReferenceWidth`
   - `heightScale = windowHeight / ReferenceHeight`

2. **Use most constraining axis** (maintains aspect ratio):
   - `scale = Min(widthScale, heightScale)`

3. **Clamp to bounds**:
   - `scale = Clamp(scale, MinScaleFactor, 1.0)`

4. **Apply to base value**:
   - `result = baseValue × scale`
   - If minimum specified: `result = Max(result, minValue)`

## Example Scaling Behavior

With default settings (1920×1080 reference, 0.5 min scale):

| Window Size | Scale Factor | FontTitle (28,18) | CardWidth (400) |
|-------------|--------------|-------------------|-----------------|
| 1920×1080 | 1.0 | 28pt | 400px |
| 1440×810 | 0.75 | 21pt | 300px |
| 1280×720 | 0.67 | 18.8pt → 18pt (min) | 267px |
| 960×540 | 0.5 | 18pt (min) | 200px |

---

## Tips & Best Practices

- **Use presets for consistency** - Semantic names ensure consistent sizing across your app
- **Customize at startup** - Set preset overrides in `App.axaml.cs` before any UI loads
- **Min values prevent unreadable text** - Always use min values for fonts
- **ScaleExtension is preferred** - Only use FloweryScaleConverter for edge cases
- **Combine with FloweryResponsive** - Use `FloweryResponsive` for layout breakpoints, `ScaleExtension` for sizing
