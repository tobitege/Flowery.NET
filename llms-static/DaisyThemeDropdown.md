<!-- Supplementary documentation for DaisyThemeDropdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeDropdown is a ComboBox listing available themes from `DaisyThemeManager`. It previews theme colors in a 2×2 dot grid and applies the selected theme. It syncs selection with the current theme when themes change externally.

## Properties & Behavior

| Property | Description |
|----------|-------------|
| `SelectedTheme` | Name of the currently selected theme. Setting this applies the theme. |
| ItemsSource | Auto-populated from `DaisyThemeManager.AvailableThemes` with preview brushes. |
| Sync | Subscribes to `ThemeChanged` to update selection when themes change elsewhere. |

## Quick Examples

```xml
<!-- Default theme dropdown -->
<controls:DaisyThemeDropdown Width="220" />

<!-- Binding selected theme -->
<controls:DaisyThemeDropdown SelectedTheme="{Binding CurrentTheme, Mode=TwoWay}" />
```

## Tips & Best Practices

- Use alongside `DaisyThemeController` for quick toggle + full list selection.
- Ensure theme palette resources (`DaisyBase100Brush`, etc.) are present for accurate previews.
- Set explicit width if you have long theme names; the popup inherits min width from the template (200px).
