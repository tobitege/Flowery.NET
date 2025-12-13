<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.x] - 2025-12-xx - unreleased

### Fixed

- **DaisySelect**: Fixed dropdown popup positioning in Browser/WebAssembly when `SelectedIndex` is set during initialization
- **DaisyTextArea**: Fixed Filled variant bottom border styling to match other variants
- **Gallery**: Improved sidebar navigation performance by caching category views and speeding up section scrolling (notably in Browser/WebAssembly)
- **run_browser.ps1**: fixed to run with debug configuration by default

## [1.4.1] - 2025-12-13

### Added

- **Multi-platform Gallery**: New cross-platform architecture for the demo app (thanks @frandelfo !)
  - `Flowery.NET.Gallery` - Shared library with all UI and examples
  - `Flowery.NET.Gallery.Desktop` - Desktop host (Windows, Linux, macOS)
  - `Flowery.NET.Gallery.Browser` - WebAssembly host for browsers
  - `Flowery.NET.Gallery.Android` - Android mobile app
  - `Flowery.NET.Gallery.iOS` - iOS mobile app
- Build scripts: `scripts/build_all.ps1` with per-project build and timing summary
  - Run a full build with: `pwsh .\scripts\build_all.ps1`
- Build scripts: `scripts/run_browser.ps1` for WASM development
- Documentation: Gallery App Architecture section in README
- Gallery: Enhanced Carousel example with descriptive text and emoji icons per slide
- **Cross-platform state storage**: Sidebar state (last viewed item, collapsed categories) now persists in Browser/WASM
  - `IStateStorage` abstraction with platform-specific implementations
  - `FileStateStorage` for Desktop (uses `%LocalAppData%`)
  - `BrowserStateStorage` for WASM (uses browser `localStorage` via JS interop)
  - `StateStorageProvider` for runtime configuration

### Fixed

- **DaisyStack/DaisyCarousel**: Fixed navigation animations not working in Browser/WebAssembly
  - Root cause: Avalonia's `Animation.RunAsync()` with `TranslateTransform` properties doesn't work reliably across platforms
  - Solution: Replaced with manual property interpolation using easing functions for consistent behavior on Desktop, Browser, Android, and iOS
- Browser: Add `WasmBuildNative=true` (required for SkiaSharp in WASM)
- Browser: Add MutationObserver splash screen hide and error handling to `main.js`
- Android: Add `AcceptAndroidSDKLicenses=true` (avoids interactive license prompts)
- Desktop/Android: Add `WithInterFont()` for consistent font rendering across all platforms
- Shared library: Remove `Avalonia.Desktop` package (now in Desktop project only)
- Shared library: Delete `Program.cs` (entry point now in platform-specific hosts)

## [1.4.0] - 2025-12-12

### Added

- DaisyTabs: Per-tab color support:
  - Theme-independent end-user palette via `TabPaletteColor` (fixed colors)
  - Theme-aware semantic colors via `TabColor` (`DaisyColor`)
- DaisyTabs: Optional tab header context menu (EnableTabContextMenu) with:
  - Close Tab / Close Other Tabs / Close Tabs to the Right
  - Inline two-row color-dot picker (palette) with reset (hollow dot)
- DaisyTabs: Localized context menu strings for all supported languages
- Gallery: real-life new DaisyTabs example looking like a VS Code editor with colored tabs
- Gallery: Language selector dropdown in the sidebar (Home) for all supported languages, with persisted selection across sessions
- Localization: Python tooling to validate/sync RESX keys (`Utils/check_resx_keys.py`, `Utils/sync_resx_keys.py`)

### Changed

- Localization: `FloweryLocalization` now releases resources on culture switch and theme display names fall back to invariant resources / theme name when missing
- CI/Release: Localization RESX key validation is now enforced in GitHub workflows (missing keys fail the pipeline)

### Fixed

- Gallery Sidebar: Multi-template item rendering now uses `ItemsControl.DataTemplates` (fixes AVLN3000 in `DaisyComponentSidebar.axaml`)

## [1.3.1] - 2025-12-11

### Added

- Localization: Added Italian translations (`it`) - thanks @frandelfo!

## [1.3.0] - 2025-12-11

### Added

- **DaisyDateTimeline**: New horizontal scrollable date picker inspired by Flutter's easy_date_timeline
  - 5 size variants, 3 header types (None, MonthYear, Switcher), 6 disable strategies
  - Marked dates with secondary color highlighting and tooltips
  - Full keyboard navigation (arrow keys, Home/End, Page Up/Down, Enter to confirm)
  - Mouse wheel scrolling and click-drag panning
  - AutoWidth mode to fit exact number of visible days
  - Vertical and Horizontal layout modes
- **DaisyDateTimelineItem**: Individual date cell control with selection, today highlight, and marker states
- Gallery: New "Date Display" section with DateTimeline and Timeline examples
- DaisyIcons: Added DaisyIconDateDisplay (calendar icon)

![DaisyDateTimeline Examples](https://tobitege.github.io/Flowery.NET/images/DaisyDateTimeline.png)

### Changed

- Gallery: Moved Timeline examples from "Data Display" to new "Date Display" section
- Docs: External links now open in new browser tab (target="_blank")
- Docs: DaisyDateTimeline* controls marked as custom (✦ badge in sidebar)

## [1.2.0] - 2025-12-10

### Added

- DaisyTokens.axaml: Centralized design tokens for consistent sizing across controls (heights, fonts, padding, corner radii)
- DesignTokens.md: Documentation for design token system in llms-static/
- DaisyTabs: TabWidthMode property with Auto/Equal/Fixed modes to prevent layout shift
- DecimalExtensions: Static conversion methods for Hex/Binary/Octal/ColorHex/IP (for ViewModel use without control instance)
- Semantic content color resources (DaisyPrimaryContentBrush, DaisySecondaryContentBrush, etc.)

### Changed

- DaisyButton, DaisyInput, DaisySelect, DaisyNumericUpDown, DaisyTabs, DaisyBadge, DaisyFileInput: Refactored to use DynamicResource tokens
- Single-line controls now use fixed Height instead of MinHeight for accurate Size variant rendering

### Fixed

- DaisyButton/DaisyInput/DaisySelect/DaisyNumericUpDown/DaisyKbd/DaisyPagination: Size variants now display correct heights (MinHeight in nested Avalonia style selectors wasn't overriding base values)
- DaisyTabs: Fixed layout shift in Boxed and Lifted variants caused by FontWeight changes on selection
- DaisyInput: Watermark now respects VerticalContentAlignment property
- DaisyToolTip: Use semantic content brushes for variant foreground colors
- DaisyBadge/DaisyButton: Fixed foreground color handling with semantic content resources

## [1.1.0] - 2025-12-09

### Added

- DaisyThemeManager: `SuppressThemeApplication` property to prevent theme controls from overriding persisted themes during app initialization

### Fixed

- DaisyThemeDropdown: Constructor no longer overrides app's persisted theme preference. Dropdowns now sync to `CurrentThemeName` instead of applying "Dark" during construction.

## [1.0.9] - 2025-12-09

### Added

- DaisyThemeManager: `CustomThemeApplicator` hook allows apps to override default theme application behavior (e.g., in-place ThemeDictionary updates, persistence)
- DaisyThemeManager: `SetCurrentTheme()` helper method for custom applicators to update internal state
- Flowery.Capture.NET: New screen capture library for automated documentation screenshots (no NuGet yet)

### Changed

- Gallery: Auto-screen-grabbing of sections for docs
- Documentation: Enhanced generation with 90+ images
- Sidebar: Sorting improvements and reduced gaps

### Fixed

- ColorPickerDialog image display

## [1.0.8] - 2025-12-08

### Added

- DaisyNumericUpDown: Full-featured numeric input control with:
  - Multiple number bases: Decimal, Hexadecimal (0xFF), Binary (0b1010), Octal (0o755), ColorHex (#FF5733), IPAddress (192.168.1.1)
  - Prefix/Suffix support for currency and units
  - Thousand separators with locale-aware formatting
  - Optional clear button (appears on focus, resets to 0)
  - Per-octet increment/decrement for IP addresses with wrapping behavior
  - Real-time input filtering and paste validation
  - Helper methods: ToHexString(), ToBinaryString(), ToOctalString(), ToColorHexString(), ToIPAddressString()

![NumericUpDown Examples](https://tobitege.github.io/Flowery.NET/NumericUpDown_Examples.png)

## [1.0.7] - 2025-12-07

### Changed

- DaisyInput: Refactored template to separate background Border from content (matching DaisyButton pattern). This prevents opacity issues where styling the border would unintentionally affect text visibility (dimmed text in unfocused state).

## [1.0.6] - 2025-12-06

### Added

- ColorPicker controls (HslColor, color wheel, spectrum)
- DaisyStack: navigation and counter display with actual item stacking
- DaisyStatusIndicator: expanded to 27 animation variants
- Shared DaisyColor/DaisySize enums for consistency
- Accessibility features for select controls

### Changed

- Documentation site: enhanced navigation and responsive sidebar
- Stack demo with new assets

### Fixed

- Docs fixes for theme switcher and llms.txt generation

## [1.0.5] - 2025-12-04

### Fixed

- Release workflow ordering (parallel NuGet + Gallery, then Release)
- Duplicate file upload in release workflow

## [1.0.3] - 2025-12-04

### Added

- Documentation generator and GitHub Pages site
- DaisyLoading: 16+ new variants (MatrixRain, Hourglass, and more)
- DaisyLoading: basic accessibility support
- DaisyDock control with click handler examples
- DaisyStatus indicator control
- DaisyAvatarGroup control
- DaisyList control with examples
- Rating with half-star support

### Changed

- DaisyLoading refactored into separate files for maintainability
- DaisySteps with more options and examples
- DaisyPagination revised with more examples
- DaisyNavbar and DaisyMenu with more examples
- DaisyBreadcrumb revised with more examples
- DaisyCheckbox enhanced with more examples
- DaisySkeleton updated with examples
- DaisyTable revised with more examples
- DaisyStat improved
- Sidebar remembers open sections
- Sidebar now lists Custom Controls

### Fixed

- Glass rendering improvements
- Sidebar navigation and scroll targeting
- MainWindow DemoModal references
- GitHub workflow permissions for creating releases

## [1.0.2] - 2025-12-02

### Added

- DaisyGlass: real backdrop blur with multiple modes
- Gallery workflow automation
- Updated screenshot

## [1.0.1] - 2025-12-02

### Added

- Initial public release
- 35 DaisyUI themes (21 light, 14 dark)
- Core controls: Button, Input, Checkbox, Radio, Toggle, Select, TextArea, Range, Rating
- Layout controls: Card, Divider, Drawer, Hero, Join, Stack
- Navigation: Breadcrumbs, Menu, Navbar, Pagination, Steps, Tabs
- Feedback: Alert, Loading, Progress, RadialProgress, Skeleton, Toast
- Data Display: Accordion, Avatar, Badge, Carousel, ChatBubble, Collapse, Countdown, Diff, Kbd, Stat, Table, Timeline
- Theme controls: ThemeSwap, ThemeDropdown, ThemeRadio, ThemeController, ThemeManager
- Custom controls: ComponentSidebar, ModifierKeys
- Gallery demo application

[1.4.1]: https://github.com/tobitege/Flowery.NET/compare/v1.4.0...HEAD
[1.4.0]: https://github.com/tobitege/Flowery.NET/compare/v1.3.1...v1.4.0
[1.3.1]: https://github.com/tobitege/Flowery.NET/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/tobitege/Flowery.NET/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/tobitege/Flowery.NET/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/tobitege/Flowery.NET/compare/v1.0.9...v1.1.0
[1.0.9]: https://github.com/tobitege/Flowery.NET/compare/v1.0.8...v1.0.9
[1.0.8]: https://github.com/tobitege/Flowery.NET/compare/v1.0.7...v1.0.8
[1.0.7]: https://github.com/tobitege/Flowery.NET/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/tobitege/Flowery.NET/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/tobitege/Flowery.NET/compare/v1.0.3...v1.0.5
[1.0.3]: https://github.com/tobitege/Flowery.NET/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/tobitege/Flowery.NET/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/tobitege/Flowery.NET/releases/tag/v1.0.1
