<!-- Supplementary documentation for DaisyThemeController -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeController is a toggle-based switch for applying Daisy themes via `DaisyThemeManager`. It supports multiple display modes (toggle, checkbox, swap, text/icon variants) and syncs `IsChecked` with the current theme. Switching updates `CheckedTheme`/`UncheckedTheme` and can auto-adopt new themes.

## How Theming Works

This control uses `DaisyThemeManager` internally, which works with Avalonia's `ThemeDictionaries` architecture:

- Setting `RequestedThemeVariant` triggers Avalonia's built-in resource refresh
- Each of the 35 built-in themes is mapped to either `Light` or `Dark` variant with its unique color palette
- For custom themes beyond the built-in set, use `DaisyThemeLoader.ApplyThemeToApplication()` instead

## Properties

| Property | Description |
| -------- | ----------- |
| `Mode` | Visual mode: Toggle, Checkbox, Swap, ToggleWithText, ToggleWithIcons. |
| `UncheckedLabel` / `CheckedLabel` | Labels for light/dark (or custom) modes. |
| `UncheckedTheme` / `CheckedTheme` | Theme names to apply on off/on states (defaults: Light/Dark). |
| `IsCurrentThemeDark` | Read-only flag returning `DaisyThemeManager.IsCurrentThemeDark`. |

## Behavior

- Toggling applies the target theme via `DaisyThemeManager.ApplyTheme(...)`.
- Subscribes to `DaisyThemeManager.ThemeChanged` to sync `IsChecked` when theme changes externally.
- When a new theme is applied that isn't the unchecked theme, `CheckedTheme`/`CheckedLabel` update to that theme name.

## Quick Examples

In your `.axaml` file (e.g., `MainWindow.axaml`), add the namespace and control:

```xml
<!-- Add at top of file -->
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<!-- Simple toggle - just drop it in, defaults to Light/Dark -->
<controls:DaisyThemeController Mode="Toggle" />

<!-- Animated sun/moon swap (as used in Gallery) -->
<controls:DaisyThemeController Mode="Swap" />

<!-- Custom theme pairing -->
<controls:DaisyThemeController Mode="ToggleWithText"
    UncheckedTheme="Light" CheckedTheme="Synthwave"
    UncheckedLabel="Light" CheckedLabel="Synthwave" />
```

**Prerequisite**: Your `App.axaml` must include `<daisy:DaisyUITheme />` in `Application.Styles`. If you're not using another base theme (like Semi or Material), add `<FluentTheme />` as the minimum required for core Avalonia controls to render properly.

## Tips & Best Practices

- Keep `UncheckedTheme` aligned with your base theme so `IsChecked=False` reflects the default look.
- If you dynamically load themes, let the controller auto-update `CheckedTheme` to the latest applied theme.
- Choose Mode to match UI density: Swap/ToggleWithIcons for compact, ToggleWithText for clarity.
- Bind `IsChecked` if you need to track theme state elsewhere; the controller will still apply themes.
