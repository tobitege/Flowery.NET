<!-- Supplementary documentation for DaisyThemeManager -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeManager is the central theme loader/applicator for the **35 built-in DaisyUI themes**. It tracks available themes, applies palette ResourceDictionaries, updates Avalonia `RequestedThemeVariant`, and notifies listeners via `ThemeChanged`. Helpers expose current/alternate theme names and light/dark metadata.

## When to Use

| Scenario | Recommended API |
|----------|-----------------|
| Switch between built-in themes (Light, Dark, Dracula, etc.) | `DaisyThemeManager.ApplyTheme()` ✓ |
| Custom theme application strategy (in-place updates, persistence) | Set `DaisyThemeManager.CustomThemeApplicator` |
| Load custom themes from CSS at runtime | `DaisyThemeLoader.ApplyThemeToApplication()` |

**Key difference:**

- `DaisyThemeManager.ApplyTheme()` adds palette resources to `MergedDictionaries` and sets the appropriate `RequestedThemeVariant`. Best for switching between the 35 built-in themes.
- `DaisyThemeLoader.ApplyThemeToApplication()` updates resources in-place within `ThemeDictionaries`. Use this for custom themes loaded from CSS files at runtime.

### Quick Comparison (in code-behind or ViewModel)

```csharp
using Flowery.Controls;
using Flowery.Theming;

// Built-in themes: use DaisyThemeManager
DaisyThemeManager.ApplyTheme("Synthwave");

// Custom CSS themes: use DaisyThemeLoader
var theme = DaisyUiCssParser.ParseFile("mytheme.css");
DaisyThemeLoader.ApplyThemeToApplication(theme);
```

**Prerequisite**: Your `App.axaml` must include `<daisy:DaisyUITheme />` in `Application.Styles`. If you're not using another base theme (like Semi or Material), add `<FluentTheme />` as the minimum required for core Avalonia controls to render properly.

## Key Members

| Member | Description |
|--------|-------------|
| `AvailableThemes` | Read-only list of `DaisyThemeInfo` (Name, IsDark) for all bundled themes. |
| `ApplyTheme(string name)` | Loads `Themes/Palettes/Daisy{name}.axaml`, swaps the palette, updates `RequestedThemeVariant`, and raises `ThemeChanged`. Uses `CustomThemeApplicator` if set. |
| `CustomThemeApplicator` | Optional `Func<string, bool>` delegate. When set, called instead of the default MergedDictionaries approach. |
| `SetCurrentTheme(string name)` | Updates internal state and fires `ThemeChanged`. Used by custom applicators after applying a theme. |
| `CurrentThemeName` | Name of the currently applied theme. |
| `BaseThemeName` | Default/unchecked theme name (default "Light"). |
| `AlternateThemeName` | Current theme if not the base; otherwise "Dark". |
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

## Custom Theme Applicator

> Available since v1.0.9

For apps that need custom theme application logic (e.g., in-place ThemeDictionary updates, persisting settings), set the `CustomThemeApplicator` delegate at startup:

```csharp
// In App.axaml.cs OnFrameworkInitializationCompleted:
DaisyThemeManager.CustomThemeApplicator = themeName =>
{
    var themeInfo = DaisyThemeManager.GetThemeInfo(themeName);
    if (themeInfo == null) return false;
    
    // Custom in-place update logic
    var paletteUri = new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{themeInfo.Name}.axaml");
    var palette = (ResourceDictionary)AvaloniaXamlLoader.Load(paletteUri);
    
    var app = Application.Current;
    var targetVariant = themeInfo.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    
    if (app.Resources.ThemeDictionaries.TryGetValue(targetVariant, out var themeDict)
        && themeDict is IResourceDictionary dict)
    {
        foreach (var kvp in palette)
            dict[kvp.Key] = kvp.Value;
    }
    
    app.RequestedThemeVariant = targetVariant;
    
    // Persist to settings
    AppSettings.Current.DaisyUiTheme = themeName;
    AppSettings.Save();
    
    return true;
};
```

All built-in theme controls (`DaisyThemeDropdown`, `DaisyThemeController`, `DaisyThemeRadio`, `DaisyThemeSwap`) automatically use the custom applicator when set.
