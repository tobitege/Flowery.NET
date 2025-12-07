<!-- Supplementary documentation for DaisyThemeDropdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeDropdown is a ComboBox listing available themes from `DaisyThemeManager`. It previews theme colors in a 2×2 dot grid and applies the selected theme. It syncs selection with the current theme when themes change externally.

## How Theming Works

This control uses `DaisyThemeManager` internally, which works with Avalonia's `ThemeDictionaries` architecture:

- Setting `RequestedThemeVariant` triggers Avalonia's built-in resource refresh
- Each of the 35 built-in themes is mapped to either `Light` or `Dark` variant with its unique color palette
- For custom themes beyond the built-in set, use `DaisyThemeLoader.ApplyThemeToApplication()` instead

## Properties & Behavior

| Property | Description |
|----------|-------------|
| `SelectedTheme` | Name of the currently selected theme. Setting this applies the theme. |
| ItemsSource | Auto-populated from `DaisyThemeManager.AvailableThemes` with preview brushes. |
| Sync | Subscribes to `ThemeChanged` to update selection when themes change elsewhere. |

## Quick Examples

In your `.axaml` file (e.g., `MainWindow.axaml`), add the namespace and control:

```xml
<!-- Add at top of file -->
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<!-- Default theme dropdown - shows all 35 themes with color previews -->
<controls:DaisyThemeDropdown Width="220" />

<!-- Binding selected theme to ViewModel -->
<controls:DaisyThemeDropdown SelectedTheme="{Binding CurrentTheme, Mode=TwoWay}" />
```

**Prerequisite**: Your `App.axaml` must include `<daisy:DaisyUITheme />` in `Application.Styles`. If you're not using another base theme (like Semi or Material), add `<FluentTheme />` as the minimum required for core Avalonia controls to render properly.

## Tips & Best Practices

- Use alongside `DaisyThemeController` for quick toggle + full list selection.
- Ensure theme palette resources (`DaisyBase100Brush`, etc.) are present for accurate previews.
- Set explicit width if you have long theme names; the popup inherits min width from the template (200px).
