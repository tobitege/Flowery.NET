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

1. Themes are loaded from `ProductPaletteFactory.GetAll()` for preview/display.
2. On selection, the control raises `ProductThemeSelected`.
3. If `ApplyOnSelection == true`, the control registers and applies the selected palette through `DaisyThemeManager`.
4. Registration prefers precompiled palette data via `ProductPalettes.Get(themeName)` for performance.
5. The control listens to `DaisyThemeManager.ThemeChanged` and syncs selection with the current active theme.

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
