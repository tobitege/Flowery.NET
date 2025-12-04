<!-- Supplementary documentation for DaisyThemeManager -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeManager is the central theme loader/applicator for DaisyUI palettes. It tracks available themes, applies palette ResourceDictionaries, updates Avalonia `RequestedThemeVariant`, and notifies listeners via `ThemeChanged`. Helpers expose current/alternate theme names and light/dark metadata.

## Key Members

| Member | Description |
|--------|-------------|
| `AvailableThemes` | Read-only list of `DaisyThemeInfo` (Name, IsDark) for all bundled themes. |
| `ApplyTheme(string name)` | Loads `Themes/Palettes/Daisy{name}.axaml`, swaps the palette, updates `RequestedThemeVariant`, and raises `ThemeChanged`. |
| `CurrentThemeName` | Name of the currently applied theme. |
| `BaseThemeName` | Default/unchecked theme name (default “Light”). |
| `AlternateThemeName` | Current theme if not the base; otherwise “Dark”. |
| `ThemeChanged` | Event fired with the new theme name after successful application. |
| `IsDarkTheme(string name)` | Returns whether the theme is marked as dark. |

## Usage Notes

- Palettes live under `Themes/Palettes/Daisy{name}.axaml`; ensure the name matches `AvailableThemes`.
- Applying the same theme twice short-circuits.
- On apply, the previous palette is removed from `Application.Current.Resources` before adding the new one.
- `RequestedThemeVariant` is set to `ThemeVariant.Dark` or `ThemeVariant.Light` based on `IsDark`.

## Quick Example (code-behind)

```csharp
// Apply Synthwave
DaisyThemeManager.ApplyTheme("Synthwave");

// Toggle between base/alternate themes
var target = DaisyThemeManager.CurrentThemeName == DaisyThemeManager.BaseThemeName
    ? DaisyThemeManager.AlternateThemeName
    : DaisyThemeManager.BaseThemeName;
DaisyThemeManager.ApplyTheme(target);
```
