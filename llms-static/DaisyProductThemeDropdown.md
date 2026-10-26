<!-- Supplementary documentation for DaisyProductThemeDropdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

`DaisyProductThemeDropdown` is a specialized theme dropdown for **96 industry-specific palettes**.  
It complements `DaisyThemeDropdown` (built-in DaisyUI themes) by providing product-oriented palettes like SaaS, healthcare, fintech, legal, education, and more.

These palettes are sourced from the [UI UX Pro Max Skill](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) project and integrated into Flowery.NET.

## When to Use

| Scenario | Recommended Control |
| --- | --- |
| Standard DaisyUI themes (Light, Dark, Dracula, etc.) | `DaisyThemeDropdown` |
| Industry/product palettes (SaaS, Healthcare, Cybersecurity, etc.) | `DaisyProductThemeDropdown` |

## Properties

| Property | Type | Description |
| --- | --- | --- |
| `SelectedTheme` | `string` | Name of the selected product theme. |
| `ApplyOnSelection` | `bool` | `true` by default. Applies selected theme immediately. |
| `ProductThemeSelected` | `event EventHandler<string>` | Raised when a product theme is selected; event arg is the selected theme name. |
| `ItemsSource` | `IEnumerable<ProductThemePreviewInfo>` | Populated automatically when attached to visual tree. |

## Runtime Behavior

An `ItemsSource` binding to an `ObservableCollection<ProductThemePreviewInfo>` remains active.
The original collection is retained separately from the displayed, resolved list.
Collection changes and source replacement refresh the view; detached controls stop observing collections and refresh on reattachment.
Use a one-way binding and make collection changes on the UI thread.

1. Themes are loaded from `ProductPaletteFactory.GetAll()` for preview/display.
2. On selection, the control raises `ProductThemeSelected`.
3. If `ApplyOnSelection == true`, the control registers and applies the selected palette through `DaisyThemeManager`.
4. Registration prefers precompiled palette data via `ProductPalettes.Get(themeName)` for performance.
5. The control listens to `DaisyThemeManager.ThemeChanged` and syncs selection with the current active theme.
6. `ExcludedThemes`, `HideTheme`, `ShowTheme`, and `PreferredThemes` in `DaisyThemeManager` also control this list, including product names not yet registered.

Visibility and ordering changes refresh attached dropdowns without applying a palette or raising `ProductThemeSelected`.
With `ApplyOnSelection=false`, list updates preserve the local selection when it remains visible.
Hiding the selected theme clears its selection without changing the active palette.
Reattaching a dropdown applies the latest settings. Without `AddTheme` overrides, other product entries retain their catalog order.
See [Theme visibility and ordering](DaisyThemeManager.md#theme-visibility-and-ordering) for examples.

## Add Standard Themes and Local Labels

Manager-level additions also appear in the product dropdown. Local calls override the same original name only in this instance:

```csharp
DaisyThemeManager.AddTheme("TheaterCinema", "DarkAndRed", true);
productThemeDropdown.AddTheme("Business", "MyBusiness", true);
productThemeDropdown.AddTheme("TheaterCinema", "Cinema", false);
```

The product catalog remains available. Preferred entries appear first, with each group sorted by display text.
When both groups are present, the popup shows a separator between them. It cannot be selected and does not change keyboard navigation.
Repeated names update an entry without duplication. Exclusions remain effective.
`RemoveAddedTheme(originalName)` (formerly `RemoveThemeOverride`) restores the global or catalog entry, if available.
`SelectedTheme` and `ProductThemeSelected` use the original name, not the label.
**Alias responsibility:** Your application must keep its own original-name/display-name mapping if it needs to reuse, persist, or look up aliases.
Aliases are display text only. For example, display `DarkAndRed`, but use `TheaterCinema` for selection, exclusions, preferences, and theme operations.
`ProductThemePreviewInfo.Palette` is null for standard-theme entries; product entries retain their product palette.

## Quick Examples

```xml
<!-- Add namespace -->
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<!-- Basic usage -->
<controls:DaisyProductThemeDropdown Width="220" />

<!-- Selection without immediate apply -->
<controls:DaisyProductThemeDropdown
    x:Name="ThemeSelector"
    Width="220"
    ApplyOnSelection="False" />
```

### Code-Behind Usage

```csharp
using Flowery.Controls;
using Flowery.Theming;

// Query available palettes
var allPalettes = ProductPaletteFactory.GetAll();
var fintech = ProductPaletteFactory.FindByName("BankingFinance");

// Listen for selection
ThemeSelector.ProductThemeSelected += (_, themeName) =>
{
    // themeName is the selected palette name
    Console.WriteLine($"Selected product theme: {themeName}");
};
```

## Tips

- Use this dropdown when your app needs domain-specific visual identity.
- Keep `ApplyOnSelection=true` for instant preview UX.
- Use `ApplyOnSelection=false` when you need explicit confirm/apply flows.
- Pair with `DaisyThemeController` if you want quick light/dark toggles plus full product-theme selection.

## Related Controls

- [DaisyThemeDropdown](DaisyThemeDropdown.md)
- [DaisyThemeManager](DaisyThemeManager.md)
- [DaisyThemeController](DaisyThemeController.md)
