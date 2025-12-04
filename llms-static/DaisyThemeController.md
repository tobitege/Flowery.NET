<!-- Supplementary documentation for DaisyThemeController -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeController is a toggle-based switch for applying Daisy themes via `DaisyThemeManager`. It supports multiple display modes (toggle, checkbox, swap, text/icon variants) and syncs `IsChecked` with the current theme. Switching updates `CheckedTheme`/`UncheckedTheme` and can auto-adopt new themes.

## Properties

| Property | Description |
|----------|-------------|
| `Mode` | Visual mode: Toggle, Checkbox, Swap, ToggleWithText, ToggleWithIcons. |
| `UncheckedLabel` / `CheckedLabel` | Labels for light/dark (or custom) modes. |
| `UncheckedTheme` / `CheckedTheme` | Theme names to apply on off/on states (defaults: Light/Dark). |

## Behavior

- Toggling applies the target theme via `DaisyThemeManager.ApplyTheme(...)`.
- Subscribes to `DaisyThemeManager.ThemeChanged` to sync `IsChecked` when theme changes externally.
- When a new theme is applied that isn’t the unchecked theme, `CheckedTheme`/`CheckedLabel` update to that theme name.

## Quick Examples

```xml
<!-- Basic light/dark toggle -->
<controls:DaisyThemeController Mode="Toggle" UncheckedTheme="Light" CheckedTheme="Dark"
                              UncheckedLabel="Light" CheckedLabel="Dark" />

<!-- Swap style with icons -->
<controls:DaisyThemeController Mode="Swap">
    <controls:DaisyThemeController.UncheckedLabel>☀</controls:DaisyThemeController.UncheckedLabel>
    <controls:DaisyThemeController.CheckedLabel>🌙</controls:DaisyThemeController.CheckedLabel>
</controls:DaisyThemeController>
```

## Tips & Best Practices

- Keep `UncheckedTheme` aligned with your base theme so `IsChecked=False` reflects the default look.
- If you dynamically load themes, let the controller auto-update `CheckedTheme` to the latest applied theme.
- Choose Mode to match UI density: Swap/ToggleWithIcons for compact, ToggleWithText for clarity.
- Bind `IsChecked` if you need to track theme state elsewhere; the controller will still apply themes.
