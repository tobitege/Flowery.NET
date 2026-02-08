<!-- Supplementary documentation for DaisyThemeDropdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeDropdown is a ComboBox listing available themes from `DaisyThemeManager`. It previews theme colors in a 2×2 dot grid and applies the selected theme. It syncs selection with the current theme when themes change externally.

## How Theming Works

This control uses `DaisyThemeManager` internally, which works with Avalonia's `ThemeDictionaries` architecture:

- Setting `RequestedThemeVariant` triggers Avalonia's built-in resource refresh
- Each of the 35 built-in themes is mapped to either `Light` or `Dark` variant with its unique color palette
- For custom themes beyond the built-in set, use `DaisyThemeLoader.ApplyThemeToApplication()` instead

## Size Options

DaisyThemeDropdown supports all standard Flowery.NET sizes:

| Size | Height | Font Size | Use Case |
| ---- | ------ | --------- | -------- |
| ExtraSmall | 24 | 10 | Dense toolbars/sidebars. |
| Small | 28 | 12 | Compact settings panels. |
| Medium (default) | 32 | 14 | General usage. |
| Large | 34 | 18 | Prominent settings. |
| ExtraLarge | 36 | 20 | Hero sections/dashboards. |

## Properties & Behavior

| Property | Description |
| -------- | ----------- |
| `Size` | `DaisySize` preset controlling height and font size (see table above). |
| `SelectedTheme` | Name of the currently selected theme. Setting this applies the theme. |
| `IsCurrentThemeDark` | Read-only flag returning `DaisyThemeManager.IsCurrentThemeDark`. |
| ItemsSource | Auto-populated from `DaisyThemeManager.AvailableThemes` with preview brushes. |
| Sync | Subscribes to `ThemeChanged` to update selection when themes change elsewhere. |

## Initialization Behavior

The dropdown automatically syncs to the current theme during construction:

- If `DaisyThemeManager.CurrentThemeName` is already set (e.g., app restored theme from settings), the dropdown syncs to that theme **without re-applying it**.
- If no theme is set yet, the dropdown defaults to "Dark" **without triggering `ApplyTheme`**.

This ensures apps can restore persisted theme preferences before constructing UI controls without worrying about dropdowns overriding the saved theme.

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
