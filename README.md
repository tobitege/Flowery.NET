<!-- markdownlint-disable MD033 -->
<div align="center">

# 🌼 Flowery.NET

**A C# port of the popular [DaisyUI](https://daisyui.com/) component library for [Avalonia UI](https://avaloniaui.net/).**

[![NuGet](https://img.shields.io/nuget/v/Flowery.NET?style=flat-square)](https://www.nuget.org/packages/Flowery.NET)
[![Downloads](https://img.shields.io/nuget/dt/Flowery.NET?style=flat-square)](https://www.nuget.org/packages/Flowery.NET)
[![License](https://img.shields.io/github/license/tobitege/Flowery.NET?style=flat-square)](LICENSE)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.0+-purple?style=flat-square)](https://avaloniaui.net/)
[![Docs](https://img.shields.io/badge/Docs-GitHub%20Pages-blue?style=flat-square)](https://tobitege.github.io/Flowery.NET/)
[![X](https://img.shields.io/badge/X-@tobitege45259-000000?style=flat-square&logo=x)](https://x.com/tobitege45259)

</div>

![Flowery.NET.Gallery Screenshot](Flowery.NET.Gallery.png)

This library provides native Avalonia controls that mimic the utility-first, semantic class naming of DaisyUI, making it easy to build beautiful, themed UIs in Avalonia. A NuGet package is also available.

> [!NOTE]
> **🚧 Active Development — Expect Breaking Changes 🚧**
>
> This will be under heavy development with a lot of changes across many files, while I'll keep
> refining and adding missing features to existing controls or even add custom new ones, like the
> weather widgets. Pin to a specific commit if you need stability!

## Features

- **Native Controls**: C# classes inheriting from Avalonia primitives (e.g., `DaisyButton : Button`).
- **35 DaisyUI Themes**: All official DaisyUI themes included (Light, Dark, Cupcake, Dracula, Nord, Synthwave, and more).
- **Runtime Theme Switching**: Use `DaisyThemeDropdown` to switch themes at runtime.
- **Variants**: Supports `Primary`, `Secondary`, `Accent`, `Ghost`, etc.
- **Framework Support**: Library targets `netstandard2.0` for maximum compatibility.
- **Gallery App**: Includes a full demo application (`Flowery.NET.Gallery`) showcasing all controls and features.

## Documentation

📖 **[View the full documentation](https://tobitege.github.io/Flowery.NET/)** - Browse all controls with properties, enum values, and XAML usage examples.

The documentation is generated from curated markdown (with optional auto-parsed metadata) and kept up-to-date via CI.

## Installation

1. Add the NuGet package or project reference to `Flowery.NET`.

2. Configure your **`App.axaml`** with DaisyUITheme (and a base theme like FluentTheme if you're not using another):

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:daisy="clr-namespace:Flowery;assembly=Flowery.NET"
             x:Class="YourApp.App"
             RequestedThemeVariant="Dark">
    <!-- RequestedThemeVariant can be "Default", "Light", or "Dark" -->

    <Application.Styles>
        <FluentTheme />           <!-- Or another base theme (Semi, Material, etc.) -->
        <daisy:DaisyUITheme />    <!-- Flowery.NET styles and themes -->
    </Application.Styles>
</Application>
```

> **Note:** The `Flowery` namespace is only for `DaisyUITheme`. For actual controls, use `Flowery.Controls` (see Usage Example below).

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

## Usage Example

The best way to explore the controls is to run the included **Gallery App**. It demonstrates every component, theme switching, and various configuration options.

In your view files (e.g., `MainWindow.axaml`), add the controls namespace:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"
        ...>

    <StackPanel Spacing="10">
        <controls:DaisyButton Content="Primary Button" Variant="Primary" />
        <controls:DaisyInput Watermark="Type here" Variant="Bordered" />
        <controls:DaisyRating Value="3.5" />
    </StackPanel>
</Window>
```

## Components

### Actions

- **Button** (`DaisyButton`): Buttons with variants (Primary, Secondary, Accent, Ghost, Link) and sizes (Large, Normal, Small, Tiny). Supports Outline and Active states.
- **Fab** (`DaisyFab`): Floating Action Button (Speed Dial) with support for multiple actions.
- **Modal** (`DaisyModal`): Dialog box with backdrop overlay.
- **Swap**: (Use `ToggleButton`with custom content or`DaisyToggle`).

### Data Display

- **Accordion** (`DaisyAccordion`): Group of collapse items that ensures only one item is expanded at a time.
- **Alert** (`DaisyAlert`): Feedback messages (Info, Success, Warning, Error).
- **Avatar** (`DaisyAvatar`): User profile image container (Circle, Square) with online/offline status indicators.
- **Badge** (`DaisyBadge`): Small status indicators (Primary, Secondary, Outline, etc.).
- **Card** (`DaisyCard`): Content container with padding and shadow.
- **Carousel** (`DaisyCarousel`): Scrollable container for items.
- **Chat Bubble** (`DaisyChatBubble`): Message bubbles with header, footer, and alignment (Start/End).
- **Collapse** (`DaisyCollapse`): Accordion/Expander with animated arrow or plus/minus icon.
- **Countdown** (`DaisyCountdown`): Monospace digit display.
- **Diff** (`DaisyDiff`): Image comparison slider (Before/After).
- **Hover Gallery** (`DaisyHoverGallery`): Image/Content gallery driven by mouse hover position.
- **Kbd** (`DaisyKbd`): Keyboard key visual style.
- **List** (`DaisyList`, `DaisyListRow`, `DaisyListColumn`): Vertical list layout with flexible row structure. Supports grow columns and wrapped content.
- **Stat** (`DaisyStat`): Statistics block with Title, Value, and Description.
- **Table** (`DaisyTable`): Styled items control for tabular data.
- **Timeline** (`DaisyTimeline`): Vertical list of events with connecting lines.

### Data Input

- **Checkbox** (`DaisyCheckBox`): Checkbox with themed colors.
- **File Input** (`DaisyFileInput`): Styled button/label for file selection.
- **Input** (`DaisyInput`): Text input field with variants (Bordered, Ghost, Primary, etc.).
- **Radio** (`DaisyRadio`): Radio button with themed colors.
- **Range** (`DaisyRange`): Slider control.
- **Rating** (`DaisyRating`): Star rating control with interactivity and partial fill support.
- **Select** (`DaisySelect`): ComboBox with themed styles.
- **Textarea** (`DaisyTextArea`): Multiline text input.
- **Toggle** (`DaisyToggle`): Switch toggle control.

### Layout

- **Divider** (`DaisyDivider`): Separation line with optional text.
- **Drawer** (`DaisyDrawer`): Sidebar navigation with overlay (inherits `SplitView`).
- **Hero** (`DaisyHero`): Large banner component.
- **Join** (`DaisyJoin`): container that groups children (buttons/inputs) by merging their borders.
- **Stack** (`DaisyStack`): container that stacks children visually with offsets.

### Navigation

- **Breadcrumbs** (`DaisyBreadcrumbs`): Navigation path with separators.
- **Menu** (`DaisyMenu`): Vertical or horizontal list of links/actions.
- **Navbar** (`DaisyNavbar`): Top navigation bar container.
- **Pagination** (`DaisyPagination`): Group of buttons for page navigation.
- **Steps** (`DaisySteps`): Progress tracker steps.
- **Tabs** (`DaisyTabs`): Tabbed navigation (Bordered, Lifted, Boxed).

### Feedback & Utils

- **Indicator** (`DaisyIndicator`): Utility to place a badge on the corner of another element.
- **Status Indicator** (`DaisyStatusIndicator`): Status dot with **27 animation variants** (Ping, Pulse, Ripple, Heartbeat, Orbit, Sonar, and more). Supports all theme colors and 5 sizes.

  ![Status Indicator Animations](llms-static/status_animations.gif)

- **Loading** (`DaisyLoading`): Animated loading indicators with **27 variants** across 4 categories:
  - *Classic*: Spinner, Dots, Ring, Ball, Bars, Infinity
  - *Terminal-inspired*: Orbit, Snake, Pulse, Wave, Bounce
  - *Matrix/Colon-dot*: Matrix, MatrixInward, MatrixOutward, MatrixVertical
  - *Special effects*: MatrixRain, Hourglass, SignalSweep, BitFlip, PacketBurst, CometTrail, Heartbeat, TunnelZoom, GlitchReveal, RippleMatrix, CursorBlink, CountdownSpinner
- **Mask** (`DaisyMask`): Applies shapes (Squircle, Heart, Hexagon, etc.) to content.
- **Mockup** (`DaisyMockup`): Frames for Code, Window, or Browser.
- **Progress** (`DaisyProgress`): Linear progress bar.
- **Radial Progress** (`DaisyRadialProgress`): Circular progress indicator.
- **Skeleton** (`DaisySkeleton`): Animated placeholder for loading states.
- **Toast** (`DaisyToast`): Container for stacking alerts (fixed positioning).
- **Tooltip**: Themed standard Avalonia `ToolTip`.

### Accessibility

Controls that convey state visually (loading indicators, progress bars, status dots) include built-in screen reader support via the `AccessibleText` property:

```xml
<!-- Screen reader announces "Loading your profile" -->
<daisy:DaisyLoading Variant="Spinner" AccessibleText="Loading your profile" />

<!-- Auto-announces "Online" based on color; or customize -->
<daisy:DaisyStatusIndicator Color="Success" />
<daisy:DaisyStatusIndicator Color="Error" AccessibleText="Server offline" />

<!-- Announces "Upload progress, 45%" -->
<daisy:DaisyProgress Value="45" AccessibleText="Upload progress" />
```

Supported controls: `DaisyLoading`, `DaisyProgress`, `DaisyRadialProgress`, `DaisyStatusIndicator`, `DaisyCountdown`, `DaisySkeleton`, `DaisyRating`.

### Custom Controls (Avalonia-specific)

These controls are not part of the original DaisyUI CSS specification but are provided as high-level conveniences for building .NET apps.

- **Component Sidebar** (`DaisyComponentSidebar`): A pre-built documentation/admin sidebar with categories, search support, and navigation events.
- **Modifier Keys** (`DaisyModifierKeys`): Visualizes the state of keyboard modifiers (Shift, Ctrl, Alt) and locks (Caps, Num, Scroll).

### Theme Controls

- **Theme Swap** (`DaisyThemeSwap`): Toggle button to switch between light and dark themes with animated sun/moon icons.
- **Theme Dropdown** (`DaisyThemeDropdown`): Dropdown to select from all 35 available DaisyUI themes at runtime.
- **Theme Radio** (`DaisyThemeRadio`): Radio button to select a specific theme.
- **Theme Controller** (`DaisyThemeController`): Flexible theme toggle with multiple presentation modes (Toggle, Checkbox, Swap, ToggleWithText, ToggleWithIcons). Automatically syncs with theme changes app-wide.
- **Theme Manager** (`DaisyThemeManager`): Centralized static class for theme management. Provides `ApplyTheme()`, `CurrentThemeName`, `AvailableThemes`, and `ThemeChanged` event for app-wide synchronization.

## Available Themes

**Light Themes:** Light, Acid, Autumn, Bumblebee, Caramellatte, Cmyk, Corporate, Cupcake, Cyberpunk, Emerald, Fantasy, Garden, Lemonade, Lofi, Nord, Pastel, Retro, Silk, Valentine, Winter, Wireframe

**Dark Themes:** Dark, Abyss, Aqua, Black, Business, Coffee, Dim, Dracula, Forest, Halloween, Luxury, Night, Sunset, Synthwave

## Theme Controller

The `DaisyThemeController` provides multiple ways to toggle between two themes, mimicking the DaisyUI theme-controller component:

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

When the app's theme changes (e.g., via `DaisyThemeDropdown`), the controller automatically updates its `CheckedTheme`and`CheckedLabel` to reflect the new theme.

## How Theming Works

Flowery.NET uses Avalonia's `ThemeDictionaries` to provide seamless theme switching. Understanding this architecture helps you choose the right API for your needs.

### Theme Architecture

1. **Colors.axaml** contains `ResourceDictionary.ThemeDictionaries` with `Light` and `Dark` variants
2. Setting `Application.RequestedThemeVariant` triggers Avalonia's built-in resource refresh
3. For the 35 built-in themes, each is mapped to either `Light` or `Dark` variant with its unique color palette

### When to Use Which API

| Scenario | Recommended API |
|----------|-----------------|
| Switch between built-in themes (Light, Dark, Dracula, etc.) | `DaisyThemeManager.ApplyTheme()` |
| Toggle Light/Dark modes | `RequestedThemeVariant = ThemeVariant.Light/Dark` |
| Load custom themes from CSS at runtime | `DaisyThemeLoader.ApplyThemeToApplication()` |
| Parse DaisyUI CSS files | `DaisyUiCssParser.ParseFile()` |

### Key Difference

- **`DaisyThemeManager.ApplyTheme()`** - Adds palette resources to `MergedDictionaries` and sets the appropriate `RequestedThemeVariant`. Best for switching between the 35 built-in themes.
- **`DaisyThemeLoader.ApplyThemeToApplication()`** - Updates resources in-place within ThemeDictionaries. Use this for custom themes loaded from CSS files at runtime.

### Quick Start Examples

**Switch between built-in themes** (in any code-behind or ViewModel):

```csharp
using Flowery.Controls;

// One-liner to switch themes - call from button click, menu, etc.
DaisyThemeManager.ApplyTheme("Synthwave");
```

**Or use XAML controls** (in your `.axaml` files like `MainWindow.axaml`):

```xml
<!-- Add namespace at top of file -->
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<!-- Drop-in theme switcher - no code-behind needed -->
<controls:DaisyThemeDropdown Width="220" />

<!-- Or a simple toggle -->
<controls:DaisyThemeController Mode="Swap" />
```

**Load custom CSS themes at runtime** (in code-behind):

```csharp
using Flowery.Theming;

// Parse a DaisyUI CSS file and apply it immediately
var theme = DaisyUiCssParser.ParseFile("path/to/mytheme.css");
DaisyThemeLoader.ApplyThemeToApplication(theme);
```

This is the same pattern used in the Gallery app's "CSS Theme Converter" demo.

## Theme Manager

`DaisyThemeManager` provides simple theme switching for the 35 built-in DaisyUI themes. It works best when using the pre-bundled themes and handles the complexity of palette loading and variant selection automatically.

```csharp
using Flowery.Controls;

// Apply a theme by name
DaisyThemeManager.ApplyTheme("Synthwave");

// Get current theme
var current = DaisyThemeManager.CurrentThemeName;

// List all available themes
foreach (var theme in DaisyThemeManager.AvailableThemes)
{
    Console.WriteLine($"{theme.Name} (Dark: {theme.IsDark})");
}

// Listen for theme changes
DaisyThemeManager.ThemeChanged += (sender, themeName) =>
{
    Console.WriteLine($"Theme changed to: {themeName}");
};

// Check if a theme is dark
bool isDark = DaisyThemeManager.IsDarkTheme("Dracula");
```

## Optional Features

### Theme Parsing & Generation Utilities

The library includes helper utilities for parsing DaisyUI CSS theme files and generating Avalonia AXAML resources. These are **not wired up by default** but can be used for custom theme workflows.

#### Parse a DaisyUI CSS Theme File

```csharp
using Flowery.Theming;

// Parse a single CSS file
var theme = DaisyUiCssParser.ParseFile("path/to/synthwave.css");

// Or parse from string
var cssContent = File.ReadAllText("mytheme.css");
var customTheme = DaisyUiCssParser.Parse(cssContent, "mytheme");

// Access parsed data
Console.WriteLine($"Theme: {theme.Name}, IsDark: {theme.IsDark}");
foreach (var color in theme.Colors)
{
    Console.WriteLine($"  {color.Key}: {color.Value}");
}
```

#### Generate AXAML from Parsed Theme

```csharp
using Flowery.Theming;

var theme = DaisyUiCssParser.ParseFile("dracula.css");

// Generate single theme AXAML
var axaml = DaisyUiAxamlGenerator.Generate(theme);
File.WriteAllText("Themes/Palettes/Dracula.axaml", axaml);

// Generate combined Light/Dark AXAML with ThemeDictionaries
var lightTheme = DaisyUiCssParser.ParseFile("corporate.css");
var darkTheme = DaisyUiCssParser.ParseFile("business.css");
var combinedAxaml = DaisyUiAxamlGenerator.GenerateCombined(lightTheme, darkTheme);
File.WriteAllText("Themes/Colors.axaml", combinedAxaml);
```

#### Runtime Theme Loading

```csharp
using Flowery.Theming;

// Create a loader instance
var loader = new DaisyThemeLoader();

// Load themes from a directory
loader.LoadFromDirectory("themes/");

// Or load individual files
loader.LoadFromFile("custom/mytheme.css");

// Get a loaded theme by name
var synthwave = loader.GetTheme("synthwave");
if (synthwave != null)
{
    // Apply theme to the application at runtime
    DaisyThemeLoader.ApplyThemeToApplication(synthwave);
}

// Export a theme to AXAML for embedding
var axaml = DaisyThemeLoader.ExportToAxaml(synthwave);
```

#### Color Conversion

```csharp
using Flowery.Theming;

// Convert OKLCH (used by DaisyUI CSS) to Hex
var hex = ColorConverter.OklchToHex("65.69% 0.196 275.75");
// Result: "#5B21B6"

// Parse hex to RGB
var (r, g, b) = ColorConverter.HexToRgb("#5B21B6");
```

## Control Template Guidelines

For contributors creating or modifying control templates, follow these architectural patterns to ensure consistent behavior:

### Background Border Architecture

Background borders should be **siblings** of content, not parents. This prevents opacity issues where styling the border would inadvertently affect text readability.

```xml
<!-- ✅ Correct: Border and content are siblings in a Panel -->
<Panel>
    <Border x:Name="PART_Background"
            Background="{TemplateBinding Background}"
            BorderBrush="{TemplateBinding BorderBrush}" />
    <ContentPresenter Content="{TemplateBinding Content}" />
</Panel>

<!-- ❌ Avoid: Content nested inside the border -->
<Border Background="{TemplateBinding Background}">
    <ContentPresenter Content="{TemplateBinding Content}" />
</Border>
```

### Opacity Guidelines

- **Never** set `Opacity` on containers that hold text content
- Use separate background layers for disabled/hover states that require transparency
- For disabled states, reduce opacity on `PART_Background` only, keeping text at full opacity

This pattern is used by `DaisyButton`, `DaisyInput`, and other controls to maintain text legibility across all states.

## Technical Requirements

To use the library in your project:

- **.NET Standard 2.0** compatible framework (.NET Core 2.0+, .NET Framework 4.6.1+, .NET 5/6/7/8+).
- **Avalonia UI 11.0+**.

To build and run the source code/gallery:

- **.NET 8.0 SDK** or later.
- **Visual Studio 2022**, JetBrains Rider, or VS Code.
- Supports Windows, macOS, and Linux.

## Windows SmartScreen Warning

When running the Gallery app for the first time on Windows, you may see a "Windows protected your PC" SmartScreen warning. This appears because the app is not code-signed (certificates are expensive 💸).

**To run anyway:**

1. Click **"More info"**
2. Click **"Run anyway"**

Alternatively: Right-click the `.exe` → **Properties** → Check **"Unblock"** → **OK**

## License

MIT

## Support

If you find this library useful, consider supporting its development:

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-support-yellow?style=flat-square&logo=buy-me-a-coffee)](https://buymeacoffee.com/tobitege23) [![Ko-Fi](https://img.shields.io/badge/Ko--Fi-support-ff5e5b?style=flat-square&logo=ko-fi)](https://ko-fi.com/tobitege)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

## Credits & References

This project is built upon and inspired by the following amazing projects:

- [**Avalonia UI**](https://avaloniaui.net/) - The cross-platform UI framework.
- [**DaisyUI**](https://daisyui.com/) - The original Tailwind CSS component library that inspired this port.
- [**Avalonia.Fonts.Inter**](https://www.nuget.org/packages/Avalonia.Fonts.Inter) - The font used in the gallery.
- [**Cyotek ColorPicker**](https://github.com/cyotek/Cyotek.Windows.Forms.ColorPicker) - The color picker controls are inspired by Cyotek's excellent Windows Forms ColorPicker library (MIT License).

> **Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by any of the above.
