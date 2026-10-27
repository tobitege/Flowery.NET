<!-- Supplementary documentation for DaisyThemeManager -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeManager is the central theme loader/applicator for the **35 built-in DaisyUI themes**. It tracks available themes, applies palette ResourceDictionaries, updates Avalonia `RequestedThemeVariant`, and notifies listeners via `ThemeChanged`. Helpers expose current/alternate theme names and light/dark metadata.

## When to Use

| Scenario | Recommended API |
| -------- | --------------- |
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
| ------ | ----------- |
| `AvailableThemes` | Read-only list of visible registered themes, ordered by preference and name or configured display text. |
| `AddTheme(originalName, displayName, preferred)` | Adds or updates global list entries for both dropdown types without applying a theme. |
| `RemoveAddedTheme(originalName)` | Removes a theme that `AddTheme` added without unregistering it. `RemoveThemeOverride`, its former name, is obsolete. |
| `AddedThemes` | The themes added with `AddTheme`, by original name (case-insensitive), as `DaisyAddedTheme(Name, DisplayName, Preferred)`. A snapshot that later changes do not alter, safe to read on any thread. |
| `ExcludedThemes` | Assign a collection of hidden names. The getter returns a read-only snapshot. |
| `HideTheme(name)` / `ShowTheme(name)` | Add or remove an exclusion at runtime. Return true when the exclusion changes. |
| `IsThemeVisible(name)` | Checks whether a name is eligible for display, even before registration. |
| `PreferredThemes` | Collection of default preferred names for entries without an explicit `AddTheme` flag. Assign `[]` to clear these defaults. |
| `RegisterTheme(info, paletteFactory)` | Adds or replaces a theme. Raises `AvailableThemesChanged` for new names; built-in names only when `NotifyForInternalThemesChanged` is true. |
| `UnregisterTheme(string name)` | Removes a theme that `RegisterTheme` added, with its `AddTheme` entry, and raises `AvailableThemesChanged`. Returns false for built-in themes, the current theme and unknown names. |
| `GetPaletteFactory(string name)` | Returns the factory passed to `RegisterTheme`, or null if the theme is unknown. |
| `TryCreatePalette(string name, out palette)` | Creates a palette from the registered factory. Used by theme previews. |
| `AvailableThemesChanged` | Raised for registration notifications and changes to excluded or preferred names. Visibility and ordering changes always notify. |
| `NotifyForInternalThemesChanged` | When true, `RegisterTheme` also raises `AvailableThemesChanged` for built-in themes. Default is false. |
| `ApplyTheme(string name)` | Loads the registered palette factory, swaps the palette, updates `RequestedThemeVariant`, and raises `ThemeChanged`. Uses `CustomThemeApplicator` if set. |
| `SuppressThemeApplication` | Defers palette application during initialization. After clearing it, call ApplyTheme with the desired name, including the same name. |
| `CustomThemeApplicator` | Optional `Func<string, bool>` delegate. When set, called instead of the default MergedDictionaries approach. |
| `SetCurrentTheme(string name)` | Updates internal state and fires `ThemeChanged`. Used by custom applicators after applying a theme. |
| `CurrentThemeName` | Name of the currently applied theme. |
| `IsCurrentThemeDark` | Read-only flag indicating whether the current theme is dark. |
| `BaseThemeName` | Default/unchecked theme name (default "Light"). |
| `AlternateThemeName` | Current theme if not the base; otherwise "Dark". |
| `ThemeChanged` | Event fired with the new theme name after successful application. |
| `IsDarkTheme(string name)` | Returns whether the theme is marked as dark. |
| `BackdropOverride` | A backdrop chosen at run time that replaces the backdrop of every theme and stays across theme switches: a color such as `#0B1B5E`, `image:` with a file path or URI, or `effect:` with an effect name. `null` keeps the backdrop of the theme. See [DaisyBackdrop](DaisyBackdrop.md). |
| `BackdropOverrideChanged` | Raised when `BackdropOverride` changes. |

## Theme Visibility and Ordering

System contrast is an accessibility preference separate from theme visibility and ordering.
The contrast palette temporarily overlays the chosen theme. Clearing the preference restores the current chosen theme.
CustomThemeApplicator continues to own normal theme changes.
For custom contrast resources, set DaisyAccessibility.CustomContrastApplicator and return true on both activation and restoration.
See [accessibility preferences](DaisyAccessibility.md#system-contrast-and-reduced-motion).

### Global Additions and Display Names

Use `AddTheme` to configure both dropdown types globally, without replacing their existing lists:

```csharp
DaisyThemeManager.AddTheme("Business", "MyBusiness", false);
DaisyThemeManager.AddTheme("TheaterCinema", "DarkAndRed", true);
DaisyThemeManager.AddTheme("Corporate", "Office", true);
```

The arguments are the original name, the display text, and the preferred flag.
Standard, registered custom, and product theme names are supported. Unknown names raise `ArgumentException`.
Original names are case-insensitive. Repeated names update the entry without adding a duplicate.
Product additions are registered globally, but no palette is applied by `AddTheme`.

Multiple preferred entries appear first, sorted alphabetically by display text. The other entries follow in display-text order.
`PreferredThemes` supplies defaults for entries without an explicit `AddTheme` flag.
Exclusions always take precedence.

An individual dropdown can override a global entry:

```csharp
themeDropdown.AddTheme("Business", "My local business", true);
productThemeDropdown.AddTheme("TheaterCinema", "Local cinema", false);
```

Local settings win for that original name only. Other entries continue to inherit global changes.
Apply these changes on the UI thread once controls exist. Attached dropdowns refresh automatically without switching themes.
The display text is local UI text; `SelectedTheme` and `ApplyTheme` still use the original name.

**Alias responsibility:** Alternate display names do not become theme identifiers. Your application must keep its own original-name/display-name mapping if it needs to reuse, persist, or look up aliases.
Use original names for `ApplyTheme`, `HideTheme`, `ShowTheme`, `ExcludedThemes`, `PreferredThemes`, and `RemoveAddedTheme`.
Dropdown selection properties and theme-name event arguments also contain original names. For example, display `DarkAndRed`, but pass `TheaterCinema` to theme operations.

```csharp
// Return this entry to the manager's settings.
themeDropdown.RemoveAddedTheme("Business");

// Remove the global display/order override. Registered themes remain registered.
DaisyThemeManager.RemoveAddedTheme("TheaterCinema");
```

### Before Creating Controls

Configure the lists before constructing your controls:

```csharp
using Flowery.Controls;

DaisyThemeManager.ExcludedThemes = new[] { "Cyberpunk", "SaaS" };
DaisyThemeManager.PreferredThemes = ["Business", "MicroSaaS"];
```

Both `DaisyThemeDropdown` and `DaisyProductThemeDropdown` use these settings.
Names are case-insensitive. Product themes and custom themes can be excluded before registration.
Preferred entries appear first, sorted alphabetically by display text, regardless of the order in the assigned collection.
Without `AddTheme` overrides, non-preferred entries keep their normal order.

### Runtime Changes

At runtime, make changes on the UI thread. Open dropdowns update automatically:

```csharp
DaisyThemeManager.HideTheme("Cyberpunk");
DaisyThemeManager.ShowTheme("Cyberpunk");

// Put these themes in the preferred group in each list that contains them.
DaisyThemeManager.PreferredThemes = ["MicroSaaS", "TheaterCinema"];
```

### Restore the Normal Lists

```csharp
// Show all themes again.
DaisyThemeManager.ExcludedThemes = Array.Empty<string>();

// Restore the normal order without changing the active theme.
DaisyThemeManager.PreferredThemes = [];
```

Assigning `ExcludedThemes` replaces the set of excluded names. It does not replace the catalog of available themes.

```csharp
DaisyThemeManager.ExcludedThemes = ["Cyberpunk"];
DaisyThemeManager.ExcludedThemes = ["Business"];
// Only Business is now excluded. Cyberpunk is visible again.
```

Use `HideTheme("Business")` to add an exclusion while keeping existing exclusions.
Use `ShowTheme("Business")` to remove only that exclusion.

Both `ExcludedThemes` and `PreferredThemes` copy the supplied names and return read-only snapshots.
Changing your original collection later has no effect; assign it again to update the settings.
Assigning `PreferredThemes` replaces only the default preferred names; explicit `AddTheme` flags still take precedence.
Duplicates and casing differences are ignored. Reassigning the same names does not raise another notification.

Exclusions take precedence over `PreferredThemes`. Unknown preferred names have no effect until they appear in a list.
These settings neither select a theme nor change the active palette or `BaseThemeName`.
If the active theme is hidden, it remains active and its dropdown selection becomes empty.
`ApplyTheme`, `GetThemeInfo`, and palette factories still accept hidden themes.

## Usage Notes

- Palettes live under `Themes/Palettes/Daisy{name}.axaml`; ensure the name matches `AvailableThemes`.
- Applying the same theme twice short-circuits.
- On apply, the previous palette is removed from `Application.Current.Resources` before adding the new one.
- `RequestedThemeVariant` is set to `ThemeVariant.Dark` or `ThemeVariant.Light` based on `IsDark`.
- While the glass mode is on (`DaisyGlassSurface.GlobalEnabled`), a small dictionary right after the palette sets `DaisyGlassTintColor` to the theme's `Base100`, computed again on every theme switch. A palette that defines `DaisyGlassTintColor` itself, such as Glassy, keeps its tint.

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

## Initialization & Theme Suppression

Theme controls like `DaisyThemeDropdown`, `DaisyThemeController`, and `DaisyThemeRadio` automatically sync to `CurrentThemeName` during construction. However, in complex scenarios with many controls or custom initialization order, you can use `SuppressThemeApplication` for explicit control:

```csharp
// In App.axaml.cs OnFrameworkInitializationCompleted:
var savedTheme = LoadThemeFromSettings() ?? "Dark";

// Option 1: Simple apps - just apply the theme first
// Theme controls will sync to it automatically
DaisyThemeManager.ApplyTheme(savedTheme);

// Option 2: Complex apps - suppress during UI construction
DaisyThemeManager.SuppressThemeApplication = true;
DaisyThemeManager.ApplyTheme(savedTheme); // Only updates internal state

// Create windows and controls...
desktop.MainWindow = new MainWindow();

// After initialization - now actually apply
DaisyThemeManager.SuppressThemeApplication = false;
DaisyThemeManager.ApplyTheme(savedTheme); // Actually applies
```

## Custom Theme Applicator

- _Available since v1.0.9_

> 📖 **[Full Migration Example](../MigrationExample.md)** - Step-by-step guide for integrating Flowery.NET into existing apps with custom resources.

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
