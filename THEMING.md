# Theming Guide

This guide covers installation, theme configuration, and advanced theming features in Flowery.NET.

## Installation

1. Add the NuGet package:

```bash
dotnet add package Flowery.NET
```

2. Configure your **`App.axaml`** with DaisyUITheme:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:daisy="clr-namespace:Flowery;assembly=Flowery.NET"
             x:Class="YourApp.App"
             RequestedThemeVariant="Dark">

    <Application.Styles>
        <FluentTheme />           <!-- Or another base theme (Semi, Material, etc.) -->
        <daisy:DaisyUITheme />    <!-- Flowery.NET styles and themes -->
    </Application.Styles>
</Application>
```

> **Note:** The `Flowery` namespace is only for `DaisyUITheme`. For controls, use `Flowery.Controls`.

3. Your **`App.axaml.cs`** remains standard:

```csharp
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
```

## Available Themes

Flowery.NET includes all 35 official DaisyUI themes:

| Light Themes | Dark Themes |
| ------------ | ----------- |
| Light, Acid, Autumn, Bumblebee, Caramellatte, Cmyk, Corporate, Cupcake, Cyberpunk, Emerald, Fantasy, Garden, Lemonade, Lofi, Nord, Pastel, Retro, Silk, Valentine, Winter, Wireframe | Dark, Abyss, Aqua, Black, Business, Coffee, Dim, Dracula, Forest, Halloween, Luxury, Night, Sunset, Synthwave |

## Product Palettes

Flowery.NET includes **96 industry-standard product palettes** (e.g., "SaaS", "Fintech", "Health", "Social") designed for specific use cases. These are generic, high-quality palettes that work seamlessly with DaisyUI components.

### Usage

Use the **DaisyProductThemeDropdown** to let users select a product palette:

```xml
<controls:DaisyProductThemeDropdown />
```

Or apply programmatically:

```csharp
using Flowery.Theming;
using Flowery.Controls;

// 1. Get the SaaS palette
if (ProductPaletteFactory.FindByName("SaaS") is ProductPalette saas)
{
    // 2. Convert to ThemeInfo and Register
    var info = new DaisyThemeInfo(saas.Name, FloweryColorHelpers.IsDark(saas.Background));
    DaisyThemeManager.RegisterTheme(info, () => ProductPaletteFactory.CreateResourceDictionary(saas));

    // 3. Apply
    DaisyThemeManager.ApplyTheme("SaaS");
}
```

## Dynamic Theme Registration

The `DaisyThemeManager` now supports registering custom themes at runtime, enabling plugins and dynamic palette generation.

```csharp
// Register a custom theme
var myTheme = new DaisyThemeInfo("MyCustomTheme", isDark: true);

DaisyThemeManager.RegisterTheme(myTheme, () => 
{
    // Return a ResourceDictionary (loaded from XAML, built in code, etc.)
    return new ResourceDictionary(); 
});

// Apply it
DaisyThemeManager.ApplyTheme("MyCustomTheme");
```


---

## Quick Start

### Switch Themes in Code

```csharp
using Flowery.Controls;

// One-liner to switch themes
DaisyThemeManager.ApplyTheme("Synthwave");
```

### Switch Themes in XAML

```xml
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<!-- Drop-in theme switcher - no code-behind needed -->
<controls:DaisyThemeDropdown Width="220" />

<!-- Or a simple toggle -->
<controls:DaisyThemeController Mode="Swap" />
```

---

## Theme Controls

### DaisyThemeDropdown

Dropdown listing all 35 themes with color previews. Automatically syncs with `DaisyThemeManager.CurrentThemeName`.

```xml
<controls:DaisyThemeDropdown Width="220" />
<controls:DaisyThemeDropdown SelectedTheme="{Binding CurrentTheme, Mode=TwoWay}" />
```

### DaisyThemeController

Flexible toggle between two themes with multiple presentation modes:

```xml
<!-- Simple toggle switch -->
<controls:DaisyThemeController Mode="Toggle" />

<!-- Checkbox style -->
<controls:DaisyThemeController Mode="Checkbox" />

<!-- Animated sun/moon swap -->
<controls:DaisyThemeController Mode="Swap" />

<!-- Toggle with text labels -->
<controls:DaisyThemeController Mode="ToggleWithText"
    UncheckedTheme="Light" CheckedTheme="Synthwave"
    UncheckedLabel="Light" CheckedLabel="Synthwave" />

<!-- Toggle with sun/moon icons -->
<controls:DaisyThemeController Mode="ToggleWithIcons" />
```

### DaisyThemeSwap

Toggle button with animated sun/moon icons for light/dark switching.

```xml
<controls:DaisyThemeSwap />
```

### DaisyThemeRadio

Radio button for selecting a specific theme. Use `GroupName` across multiple radios for a theme picker.

```xml
<controls:DaisyThemeRadio ThemeName="Light" GroupName="themes" />
<controls:DaisyThemeRadio ThemeName="Dark" GroupName="themes" />
<controls:DaisyThemeRadio ThemeName="Synthwave" GroupName="themes" />
```

---

## DaisyThemeManager API

The centralized static class for theme management.

```csharp
using Flowery.Controls;

// Apply a theme
DaisyThemeManager.ApplyTheme("Synthwave");

// Get current theme
var current = DaisyThemeManager.CurrentThemeName;

// List all themes
foreach (var theme in DaisyThemeManager.AvailableThemes)
    Console.WriteLine($"{theme.Name} (Dark: {theme.IsDark})");

// Listen for changes
DaisyThemeManager.ThemeChanged += (sender, themeName) =>
    Console.WriteLine($"Theme changed to: {themeName}");

// Check if dark
bool isDark = DaisyThemeManager.IsDarkTheme("Dracula");
```

### Key Members

| Member | Description |
| ------ | ----------- |
| `ApplyTheme(string)` | Loads and applies a theme by name |
| `CurrentThemeName` | Currently applied theme name |
| `AvailableThemes` | Read-only list of all theme info |
| `ThemeChanged` | Event fired after theme changes |
| `SuppressThemeApplication` | When true, `ApplyTheme` only updates internal state (for initialization) |
| `CustomThemeApplicator` | Optional delegate to override default theme application |
| `IsDarkTheme(string)` | Returns whether a theme is dark |

---

## Advanced: Persisting Theme Preferences

When your app persists theme preferences, theme controls construct before your saved theme is restored. Use one of these approaches:

### Option 1: Apply Theme Before UI Construction (Recommended)

Theme controls automatically sync to `CurrentThemeName` during construction:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    // Apply saved theme FIRST - controls will sync to it
    var savedTheme = LoadThemeFromSettings() ?? "Dark";
    DaisyThemeManager.ApplyTheme(savedTheme);

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        desktop.MainWindow = new MainWindow();

    base.OnFrameworkInitializationCompleted();
}
```

### Option 2: Suppress During Initialization

For complex scenarios, suppress theme application during UI construction:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var savedTheme = LoadThemeFromSettings() ?? "Dark";

    // Suppress actual application during UI construction
    DaisyThemeManager.SuppressThemeApplication = true;
    DaisyThemeManager.ApplyTheme(savedTheme); // Only updates internal state

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        desktop.MainWindow = new MainWindow();

    // Now actually apply
    DaisyThemeManager.SuppressThemeApplication = false;
    DaisyThemeManager.ApplyTheme(savedTheme);

    base.OnFrameworkInitializationCompleted();
}
```

---

## Advanced: Custom Theme Applicator

> 📖 **[Full Migration Guide](https://tobitege.github.io/Flowery.NET/#MigrationExample)** - Step-by-step guide for apps with custom theming architectures.

Override the default theme application behavior for in-place updates, persistence, or custom logic:

```csharp
// In App.axaml.cs OnFrameworkInitializationCompleted:
DaisyThemeManager.CustomThemeApplicator = themeName =>
{
    var themeInfo = DaisyThemeManager.GetThemeInfo(themeName);
    if (themeInfo == null) return false;

    // Your custom logic here
    MyApp.ApplyThemeInPlace(themeInfo);
    AppSettings.Current.Theme = themeName;
    AppSettings.Save();

    return true;
};
```

All built-in theme controls (`DaisyThemeDropdown`, `DaisyThemeController`, `DaisyThemeRadio`, `DaisyThemeSwap`) automatically use your custom applicator.

---

## How Theming Works

Flowery.NET uses Avalonia's `ThemeDictionaries` architecture:

1. **Colors.axaml** contains `ResourceDictionary.ThemeDictionaries` with `Light` and `Dark` variants
2. Setting `Application.RequestedThemeVariant` triggers Avalonia's built-in resource refresh
3. Each of the 35 built-in themes is mapped to either `Light` or `Dark` variant with its unique color palette

### When to Use Which API

| Scenario | API |
| -------- | --- |
| Switch between built-in themes | `DaisyThemeManager.ApplyTheme()` |
| Custom theme application strategy | Set `DaisyThemeManager.CustomThemeApplicator` |
| Toggle Light/Dark modes only | `RequestedThemeVariant = ThemeVariant.Light/Dark` |
| Load custom themes from CSS | `DaisyThemeLoader.ApplyThemeToApplication()` |

---

## Runtime Theme Loading from CSS

Parse DaisyUI CSS files and apply themes at runtime:

```csharp
using Flowery.Theming;

// Parse and apply a CSS theme file
var theme = DaisyUiCssParser.ParseFile("path/to/mytheme.css");
DaisyThemeLoader.ApplyThemeToApplication(theme);

// Or parse from string
var cssContent = File.ReadAllText("mytheme.css");
var customTheme = DaisyUiCssParser.Parse(cssContent, "mytheme");

// Access parsed data
Console.WriteLine($"Theme: {theme.Name}, IsDark: {theme.IsDark}");
foreach (var color in theme.Colors)
    Console.WriteLine($"  {color.Key}: {color.Value}");
```

### Generate AXAML from Parsed Theme

```csharp
var theme = DaisyUiCssParser.ParseFile("dracula.css");

// Generate single theme AXAML
var axaml = DaisyUiAxamlGenerator.Generate(theme);
File.WriteAllText("Themes/Palettes/Dracula.axaml", axaml);

// Generate combined Light/Dark AXAML
var lightTheme = DaisyUiCssParser.ParseFile("corporate.css");
var darkTheme = DaisyUiCssParser.ParseFile("business.css");
var combinedAxaml = DaisyUiAxamlGenerator.GenerateCombined(lightTheme, darkTheme);
```

### DaisyThemeLoader API

```csharp
var loader = new DaisyThemeLoader();

// Load from directory or file
loader.LoadFromDirectory("themes/");
loader.LoadFromFile("custom/mytheme.css");

// Get and apply
var synthwave = loader.GetTheme("synthwave");
if (synthwave != null)
    DaisyThemeLoader.ApplyThemeToApplication(synthwave);

// Export to AXAML
var axaml = DaisyThemeLoader.ExportToAxaml(synthwave);
```

### Color Conversion

```csharp
using Flowery.Theming;

// Convert OKLCH (DaisyUI CSS format) to Hex
var hex = ColorConverter.OklchToHex("65.69% 0.196 275.75");
// Result: "#5B21B6"

// Parse hex to RGB
var (r, g, b) = ColorConverter.HexToRgb("#5B21B6");
```
