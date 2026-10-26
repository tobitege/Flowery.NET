<!-- Supplementary documentation for DaisyThemeDropdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeDropdown is a ComboBox listing available themes from `DaisyThemeManager`. It previews theme colors in a 2×2 dot grid and applies the selected theme. It syncs selection with the current theme when themes change externally. Preview brushes come from `DaisyThemeManager.TryCreatePalette`, including themes registered at runtime.

## How Theming Works

Setting SelectedTheme selects and applies that registered theme.
External theme changes update the selection without reapplying it.

This control uses `DaisyThemeManager` internally, which works with Avalonia's `ThemeDictionaries` architecture:

- Setting `RequestedThemeVariant` triggers Avalonia's built-in resource refresh
- Each of the 35 built-in themes is mapped to either `Light` or `Dark` variant with its unique color palette
- Themes added with `DaisyThemeManager.RegisterTheme` appear in the list and use that factory for preview swatches
- For custom CSS themes loaded at runtime, use `DaisyThemeLoader.ApplyThemeToApplication()`

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
| ItemsSource | Auto-populated from `DaisyThemeManager.AvailableThemes` with preview brushes from `TryCreatePalette`. |
| Sync | Subscribes to `ThemeChanged` and `AvailableThemesChanged` so selection and the theme list stay current. |

Use `DaisyThemeManager.ExcludedThemes` before creating controls, or `HideTheme` and `ShowTheme` at runtime, to control which names appear.
Set `DaisyThemeManager.PreferredThemes = ["Business", "Dark"]` to group visible preferred themes first, sorted by display text, without selecting them.
List updates do not apply a theme. Hiding the active theme clears the selection while retaining its `SelectedTheme` name.
See [Theme visibility and ordering](DaisyThemeManager.md#theme-visibility-and-ordering) for examples.

## Initialization Behavior

### Bound Item Sources

`ItemsSource` can be bound to an `ObservableCollection<ThemePreviewInfo>` with a one-way binding.
The control retains the original collection and binding while displaying a resolved list.
Add, remove, move, reset, and source replacement update the displayed entries.
Global and local theme additions still apply to that list.
An empty external collection stays empty unless additions supply entries; setting the source to null restores the default catalog.
Collection subscriptions are removed when the control detaches and restored when it reattaches.
Make collection changes on the UI thread.

### Mix Standard and Product Themes

`AddTheme` extends this instance's existing list. Standard entries and manager additions remain available:

```csharp
themeDropdown.AddTheme("Business", "MyBusiness", false);
themeDropdown.AddTheme("TheaterCinema", "DarkAndRed", true);
themeDropdown.AddTheme("Corporate", "Office", true);
```

The arguments are the original theme name, the display text, and the preferred flag.
Preferred entries appear first; both groups are sorted alphabetically by display text.
When both groups are present, the popup shows a separator between them. The separator is not an item and cannot select or apply a theme.
A repeated original name updates its text and flag without adding a duplicate.
These settings affect only this dropdown and override global `DaisyThemeManager.AddTheme` settings for the same original name.
Global exclusions remain effective. `RemoveAddedTheme(originalName)` (formerly `RemoveThemeOverride`) restores the global or standard entry, if available.

Adding or renaming entries does not apply a theme. Selecting `DarkAndRed` applies `TheaterCinema`; `SelectedTheme` holds `TheaterCinema`.
**Alias responsibility:** Your application must keep its own original-name/display-name mapping if it needs to reuse, persist, or look up aliases.
Aliases are display text only. Use original names for selection, exclusions, preferences, and theme operations; theme-name event arguments also use original names.
Product themes added locally are registered when selected. Preview swatches use their original palette.
Unknown names and empty labels raise `ArgumentException` without changing the list.

### Initial Selection

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
