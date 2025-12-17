<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6.0] - 2025-12-17

### New

- **ScaleExtension**: New markup extension for responsive scaling with minimal XAML (`Flowery.Services`)
  - Semantic presets: `FontTitle`, `FontBody`, `SpacingMedium`, `CardWidth`, etc. (24 built-in presets)
  - Automatic window size detection - no more verbose `$parent[Window]` bindings
  - Usage: `<TextBlock FontSize="{services:Scale FontTitle}"/>` instead of long converter bindings
  - Supports custom values: `{services:Scale 24,12}` for base 24, min 12
- **FloweryScaleConfig**: Configuration for scale presets
  - Override preset values at app startup: `FloweryScaleConfig.SetPresetValues(FloweryScalePreset.CardWidth, 450)`
  - Adjust reference dimensions (default: 1920×1080 HD)
  - Adjust minimum scale factor (default: 0.5)
- **FloweryScaleConverter**: Proportional scaling converter for advanced scenarios
  - Real-time continuous scaling as window resizes (unlike discrete breakpoints)
  - Configurable `ReferenceWidth`, `ReferenceHeight`, `MinScaleFactor` properties
- **DaisyButton**: Added `IconLeft`, `IconRight`, and `IconSpacing` properties
  - Place icons alongside button text without manual StackPanel wrappers
  - Icons auto-hide when null, auto-show when set
  - `IconSpacing` controls gap between icons and content (default: 6px)
  - Usage: `<controls:DaisyButton Content="Add"><controls:DaisyButton.IconLeft><PathIcon .../></controls:DaisyButton.IconLeft></controls:DaisyButton>`
- **LocalizeExtensionBase**: Abstract base class for localization markup extensions
  - Apps inherit and plug in their localization source via `GetLocalizedString()` and `SubscribeToCultureChanged()`
  - Eliminates boilerplate code duplication across projects
  - Flowery.NET and Gallery now use this base class
- **FloweryLocalization.CustomResolver**: New extensibility point for app-specific localization
  - Apps can set `FloweryLocalization.CustomResolver = MyAppLocalization.GetString` to provide translations for library controls
  - Enables `FloweryComponentSidebar` to display localized sidebar item names from the consuming app
  - Library's internal keys (`Size_*`, `Theme_*`, `Accessibility_*`) remain unaffected
- **FlowerySizeManager**: New FlowerySizeManager service for app-wide size control (ExtraSmall to ExtraLarge)
- **DaisySizeDropdown**: Standalone control with localized size names (12 languages)
  - New `SizeOptions` property for full customization
  - `SizePreviewInfo.IsVisible` to hide unwanted sizes
  - `SizePreviewInfo.DisplayNameOverride` for custom labels (overrides localized text)
  - Example: Show only "Compact", "Normal", "Large" instead of all 5 sizes
- IgnoreGlobalSize attached property for size demonstration exemptions
- GlobalSizing.md documentation with usage examples
- Gallery applies global size on startup and category navigation
- FloweryScaleConverter.md: Comprehensive documentation covering both ScaleExtension and FloweryScaleConverter
- LocalizeExtensionBase.md: Documentation for creating app-specific LocalizeExtension classes
- **FloweryScaleManager**: Opt-in automatic font scaling for Daisy controls (`Flowery.Services`)
  - Set `services:FloweryScaleManager.EnableScaling="True"` on any container to enable
  - All child Daisy controls (DaisyInput, DaisyButton, DaisySelect, DaisyBadge, etc.) auto-scale their fonts
  - Non-breaking: controls behave normally unless EnableScaling is explicitly enabled
  - `IScalableControl` interface for custom control scaling support
  - `OverrideScaleFactor`: Optional manual zoom override (bypasses window-size calculation)
- **Hebrew Localization**: Added Hebrew (he) as the 12th supported language across both library and Gallery

### Gallery App

- **Scaling Examples**: New showcase page demonstrating `ScaleExtension` with real-world customer details form
  - Address, Contact, Payment, Customer Group, Activity, and Notes cards
  - All elements scale proportionally as window resizes
  - Auto Scaling panel includes Max Scale, optional manual Zoom slider (5% steps), and desktop-only Resolution presets
  - Available Scale Presets reference section with live examples
- **Sidebar Localization**: All sidebar category and item names now display localized text instead of keys
  - Uses `CustomResolver` to provide Gallery-specific translations to `FloweryComponentSidebar`

### Changed

- **LocalizeExtension** (Flowery.NET): Now inherits from `LocalizeExtensionBase`, reducing code duplication
- **LOCALIZATION.md**: Updated to document `LocalizeExtensionBase` pattern and `CustomResolver` for app localization
- Carousel hides left navigation button at 1st, right button on last slide
- **Design Tokens**: Rebalanced Global Size scaling to be text-focused
  - Heights now scale modestly (ExtraLarge: 80→48px, Large: 64→48px, Medium: 48→40px)
  - Button/Input/Tab/Menu padding reduced proportionally to font size growth
  - Result: larger sizes now feel like "bigger text" rather than "bloated boxes with excessive whitespace"

### Fixed

- **DaisyBadge**: Auto Scaling now also scales Height and Padding to prevent clipped text at high zoom
- **DaisyStatusIndicator**: Added Auto Scaling support (scales dot size within EnableScaling regions)
- **Gallery (Desktop)**: Switching away (alt-tab) and back no longer snaps the Scaling demo page scroll to the top

## [1.5.1] - 2025-12-15

### New

- **FloweryResponsive**: New responsive layout helper moved from Gallery to library (`Flowery.Services`)
  - Attached properties: `IsEnabled`, `BaseMaxWidth`, `ResponsiveMaxWidth`, `CurrentBreakpoint`
  - `FloweryBreakpoints` static class with predefined breakpoints (ExtraSmall 430px, Small 640px, Medium 768px, Large 1024px, ExtraLarge 1280px, TwoXL 1536px)
  - Helper methods: `GetBreakpointName()`, `IsAtLeast()`, `IsBelow()`
  - Documentation added to llms-static/ and categorized as Helper on the docs site
- **FloweryComponentSidebar**: Added `SidebarThemeSelectorItem` marker class for theme dropdown rendering
- **Run scripts** added specifically for Android and Desktop for faster iterations

### Changed

- **BREAKING**: Renamed `DaisyComponentSidebar` to `FloweryComponentSidebar` (no original DaisyUI equivalent)
  - Update XAML: `<controls:DaisyComponentSidebar>` → `<controls:FloweryComponentSidebar>`
  - Theme file renamed from `DaisyComponentSidebar.axaml` to `FloweryComponentSidebar.axaml`
- **BREAKING**: `FloweryComponentSidebar` no longer initializes with default categories
  - Callers must now set `Categories` and `AvailableLanguages` properties explicitly
  - Refactored to be a generic, reusable control without hardcoded Gallery-specific data
  - Added `OnCategoriesChanged` handler to support external category assignment
- Gallery: All 12 example views now use `FloweryResponsive` helper for responsive layout behavior
  - Carousel, ChatBubble, Collapse, Diff, Tables now adapt to screen size
  - Tables wrapped in `ScrollViewer` for horizontal scrolling on narrow screens
- FloweryComponentSidebar: Enhanced documentation with language/translation support section

### Gallery App

- Created `GallerySidebarData.cs` with all showcase categories, items, and languages
- Created `GalleryThemeSelectorItem` and `GalleryLanguageSelectorItem` classes
- Updated `MainView.axaml.cs` to initialize sidebar with Gallery-specific data
- Moved theme selector from header to sidebar (above language selector)
- Android version works much better now due to improved responsive layout with content-aware sidebar and title-bar collapsing

### UI/UX Improvements

- Added theme dropdown template to sidebar theme file
- Adjusted margins to prevent dropdown cutoff in sidebar
- Unified font sizes to 12px across sidebar components
- Improved hamburger button styling (`Size="Small"`, `Variant="Ghost"`, 18×18 icon)
- Added smarter responsive breakpoint logic based on content width

### Fixed

- **FloweryComponentSidebar**: Fixed hamburger button not showing on narrow windows
  - Added `OnLoaded` handler to apply responsive layout on initial load
  - Refactored collapse logic to be content-aware, not pixel-based:
    - Collapse if content area would be < 400px
    - Collapse if sidebar takes > 35% of screen width
  - Works on all platforms: desktop resize, mobile orientation, foldables

## [1.5.0] - 2025-12-14

### New

- **Effects Collection** ([Effects](https://tobitege.github.io/Flowery.NET/#Effects)): WASM-compatible UI animation behaviors inspired by [smoothui](https://github.com/educlopez/smoothui)
  - **RevealBehavior**: 5 reveal modes (FadeReveal, SlideIn, FadeOnly, Scale, ScaleSlide) with configurable direction and distance
  - **ScrambleHoverBehavior**: Character scramble effect on hover with progressive reveal
  - **WaveTextBehavior**: Continuous sine wave animation for text
  - **CursorFollowBehavior**: Spring-physics cursor follower with 3 shapes (Circle, Square, Ring) and adjustable opacity
  - Programmatic API for demos: `TriggerScramble()`, `ResetScramble()`, `SetTargetPosition()`, `ShowFollower()`, `HideFollower()`
  - Gallery: Interactive showcase with auto-looping infinity path demo for GIF recording

![Effects Showcase](https://tobitege.github.io/Flowery.NET/images/effects_showcase.webp)

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

- Gallery Sidebar: Multi-template item rendering now uses `ItemsControl.DataTemplates` (fixes AVLN3000 in `FloweryComponentSidebar.axaml`)

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

[1.5.0]: https://github.com/tobitege/Flowery.NET/compare/v1.4.1...HEAD
[1.4.1]: https://github.com/tobitege/Flowery.NET/compare/v1.4.0...v1.4.1
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
