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
(English default language. Localized to 11 languages!)

This library provides native Avalonia controls that mimic the utility-first, semantic class naming of DaisyUI, making it easy to build beautiful, themed UIs in Avalonia. A NuGet package is also available.

> [!NOTE]
> **🚧 Active Development  -  Expect Breaking Changes 🚧**
>
> This will be under heavy development with a lot of changes across many files, while I'll keep
> refining and adding missing features to existing controls or even add custom new ones, like the
> weather widgets. Pin to a specific commit if you need stability!

## Features

- **Native Controls**: C# classes inheriting from Avalonia primitives (e.g., `DaisyButton : Button`).
- **35 DaisyUI Themes**: All official DaisyUI themes included (Light, Dark, Cupcake, Dracula, Nord, Synthwave, and more).
- **Runtime Theme Switching**: Use `DaisyThemeDropdown` to switch themes at runtime.
- **Localization Support**: Built-in i18n with **11 languages** (🇺🇸 🇩🇪 🇫🇷 🇪🇸 🇮🇹 🇨🇳 🇰🇷 🇯🇵 🇸🇦 🇹🇷 🇺🇦), localizable theme names, and runtime language switching. [📖 Guide](LOCALIZATION.md)
- **Variants**: Supports `Primary`, `Secondary`, `Accent`, `Ghost`, etc.
- **Framework Support**: Library targets `netstandard2.0` for maximum compatibility.
- **Gallery App**: Multi-platform demo application showcasing all controls and features (Desktop, Browser/WASM, Android, iOS).

## Documentation

📖 **[View the full documentation](https://tobitege.github.io/Flowery.NET/)** - Browse all controls with properties, enum values, and XAML usage examples.

## Quick Start

1. Install the NuGet package:

```bash
dotnet add package Flowery.NET
```

1. Add to your `App.axaml`:

```xml
<Application.Styles>
    <FluentTheme />
    <daisy:DaisyUITheme />
</Application.Styles>
```

1. Use controls in your views:

```xml
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<controls:DaisyButton Content="Primary Button" Variant="Primary" />
<controls:DaisyInput Watermark="Type here" Variant="Bordered" />
<controls:DaisyThemeDropdown Width="220" />
```

📖 **[Full Installation & Theming Guide](THEMING.md)** - Detailed setup, theme switching, persistence, and custom theme loading.

---

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

- **Loading** (`DaisyLoading`): Animated loading indicators with **27 variants** (extended from DaisyUI's original 6) across 4 categories:
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

### Theme Controls

- **Theme Swap** (`DaisyThemeSwap`): Toggle button with animated sun/moon icons.
- **Theme Dropdown** (`DaisyThemeDropdown`): Dropdown to select from all 35 themes.
- **Theme Radio** (`DaisyThemeRadio`): Radio button for theme selection.
- **Theme Controller** (`DaisyThemeController`): Flexible toggle with multiple modes (Toggle, Checkbox, Swap, ToggleWithText, ToggleWithIcons).
- **Theme Manager** (`DaisyThemeManager`): Static class for programmatic theme control.

---

## ✨ Flowery.NET Exclusives

> **Beyond DaisyUI**  -  The following features and controls are **not part of the original DaisyUI CSS specification**. They are unique to Flowery.NET, built natively for Avalonia.

### Accessibility

Controls that convey state visually include built-in screen reader support via `AccessibleText`:

```xml
<daisy:DaisyLoading Variant="Spinner" AccessibleText="Loading your profile" />
<daisy:DaisyStatusIndicator Color="Success" />
<daisy:DaisyProgress Value="45" AccessibleText="Upload progress" />
```

Supported: `DaisyLoading`, `DaisyProgress`, `DaisyRadialProgress`, `DaisyStatusIndicator`, `DaisyCountdown`, `DaisySkeleton`, `DaisyRating`.

### Utility Controls

- **Component Sidebar** (`DaisyComponentSidebar`): Pre-built documentation/admin sidebar with categories and search.
- **Modifier Keys** (`DaisyModifierKeys`): Visualizes keyboard modifiers (Shift, Ctrl, Alt) and locks.

### Color Picker Suite

A complete suite rebuilt natively for Avalonia with DaisyUI styling, inspired by Cyotek's `ColorPicker`:

- **Color Wheel** (`DaisyColorWheel`): Circular HSL color wheel.
- **Color Grid** (`DaisyColorGrid`): Grid-based palette selector.
- **Color Slider** (`DaisyColorSlider`): Channel-specific sliders (R/G/B/A/H/S/L).
- **Color Editor** (`DaisyColorEditor`): Comprehensive RGB/HSL editor.
- **Screen Color Picker** (`DaisyScreenColorPicker`): Eyedropper tool (Windows only).
- **Color Picker Dialog** (`DaisyColorPickerDialog`): Full-featured modal dialog.

### Date Timeline

A horizontal scrollable date picker inspired by FadyFayezYounan's `easy_date_timeline`:

- **Date Timeline** (`DaisyDateTimeline`): Scrollable date picker with selectable date items. Supports multiple header types (MonthYear, Switcher, None), sizes, disable strategies, and marked dates with tooltips.
- **Date Timeline Item** (`DaisyDateTimelineItem`): Individual date cell with day name, date number, and month. Supports selection, disable states, and marker highlights.

### DaisyTabs Color Features

- **Per-tab palette colors**: `DaisyTabs` supports a **theme-independent color palette** (12 fixed colors) via `TabPaletteColor`, designed for end users (Purple, Indigo, Pink, SkyBlue, Blue, Lime, Green, Yellow, Orange, Red, Gray, Default).
- **Inline color picker**: Optional tab context menu shows a **two-row dot grid** for quick color changes and reset (hollow dot), without leaving the tab strip.
- **Semantic colors (optional)**: For apps that want colors to follow the active theme, `TabColor` exposes the semantic Daisy colors (`Primary`, `Success`, etc.) as an alternative API.

### Mask Input

- **Mask Input** (`DaisyMaskInput`): Masked input for structured values (time, expiry date, card number, CVC) with `Mode` presets and localized auto-watermarks.

### Numeric Input

- **Numeric Up/Down** (`DaisyNumericUpDown`): Advanced numeric input with **6 number bases** (Decimal, Hex, Binary, Octal, ColorHex, IPv4). Features real-time filtering, thousand separators, prefix/suffix display.

### Visual Effects

Cross-platform visual effects library (WASM-compatible). See [Effects.md](Flowery.NET/docs/index.html#Effects) for full documentation.

- **RevealBehavior**: Fade-in + slide animation on element attach.
- **ScrambleHoverBehavior**: Random character scramble on hover, resolves left-to-right.
- **WaveTextBehavior**: Infinite sine wave animation on text.
- **CursorFollowBehavior**: Spring-physics cursor follower element.

```xml
xmlns:fx="clr-namespace:Flowery.Effects;assembly=Flowery.NET"

<Border fx:RevealBehavior.IsEnabled="True" fx:RevealBehavior.Direction="Bottom"/>
<TextBlock fx:ScrambleHoverBehavior.IsEnabled="True" Text="Hover Me!"/>
```

### Weather Widgets

Weather display widgets with animated condition icons:

- **Weather Icon** (`DaisyWeatherIcon`): Animated weather condition icon.
- **Weather Card** (`DaisyWeatherCard`): Composite widget with current, forecast, and metrics.
- **Weather Current** (`DaisyWeatherCurrent`): Current temperature and conditions.
- **Weather Forecast** (`DaisyWeatherForecast`): Daily forecast strip.
- **Weather Metrics** (`DaisyWeatherMetrics`): UV, wind, humidity display.

---

## Control Template Guidelines

For contributors creating or modifying control templates:

### Background Border Architecture

Background borders should be **siblings** of content, not parents:

```xml
<!-- ✅ Correct: Border and content are siblings -->
<Panel>
    <Border x:Name="PART_Background" Background="{TemplateBinding Background}" />
    <ContentPresenter Content="{TemplateBinding Content}" />
</Panel>

<!-- ❌ Avoid: Content nested inside border -->
<Border Background="{TemplateBinding Background}">
    <ContentPresenter Content="{TemplateBinding Content}" />
</Border>
```

### Opacity Guidelines

- **Never** set `Opacity` on containers holding text
- Use separate background layers for disabled/hover states
- For disabled states, reduce opacity on `PART_Background` only

---

## Gallery App Architecture

The Gallery demo application uses a **multi-platform architecture** to showcase Flowery.NET controls across different platforms:

| Project | Description |
|---------|-------------|
| `Flowery.NET.Gallery` | Shared library containing all UI, views, and examples |
| `Flowery.NET.Gallery.Desktop` | Desktop host for Windows, Linux, and macOS |
| `Flowery.NET.Gallery.Browser` | WebAssembly host for running in browsers |
| `Flowery.NET.Gallery.Android` | Android mobile app |
| `Flowery.NET.Gallery.iOS` | iOS mobile app (requires macOS to build) |

The shared library contains the `MainView` UserControl with all the demo content, while each platform host provides the entry point and platform-specific configuration. This architecture ensures the same UI runs consistently across all supported platforms.

**Running the Gallery:**

```bash
# Desktop (Windows/Linux/macOS)
dotnet run --project Flowery.NET.Gallery.Desktop

# Browser (WebAssembly)
dotnet run --project Flowery.NET.Gallery.Browser

# Android (requires Android SDK)
dotnet build Flowery.NET.Gallery.Android -f net9.0-android
```

---

## Technical Requirements

**To use the library:**

- .NET Standard 2.0 compatible framework (.NET Core 2.0+, .NET Framework 4.6.1+, .NET 5/6/7/8+)
- Avalonia UI 11.0+

**To build from source:**

- .NET 8.0 SDK or later
- Visual Studio 2022, JetBrains Rider, or VS Code
- Windows, macOS, or Linux

## Windows SmartScreen Warning

When running the Gallery app for the first time, you may see a SmartScreen warning (app is not code-signed).

**To run:** Click "More info" → "Run anyway"

Or: Right-click `.exe` → Properties → Check "Unblock" → OK

## License

MIT

## Support

If you find this library useful, consider supporting its development:

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-support-yellow?style=flat-square&logo=buy-me-a-coffee)](https://buymeacoffee.com/tobitege23) [![Ko-Fi](https://img.shields.io/badge/Ko--Fi-support-ff5e5b?style=flat-square&logo=ko-fi)](https://ko-fi.com/tobitege)

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

## Credits & References

- [**Avalonia UI**](https://avaloniaui.net/) - The cross-platform UI framework.
- [**DaisyUI**](https://daisyui.com/) - The original Tailwind CSS component library.
- [**Avalonia.Fonts.Inter**](https://www.nuget.org/packages/Avalonia.Fonts.Inter) - The font used in the gallery.
- [**Cyotek ColorPicker**](https://github.com/cyotek/Cyotek.Windows.Forms.ColorPicker) - Inspiration for color picker controls.
- [**Easy Date Timeline**](https://github.com/FadyFayezYounan/easy_date_timeline) - Inspiration for date timeline controls.
- [**smoothui**](https://github.com/educlopez/smoothui) - Inspiration for visual effects (React/Tailwind/Framer Motion).
- [**@frandelfo**](https://github.com/frandelfo) - Multi-platform Gallery architecture (Desktop, Browser, Android, iOS).

> **Disclaimer:** This project is not affiliated with, endorsed by, or sponsored by any of the above.
