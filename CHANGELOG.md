<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.8]: https://github.com/tobitege/Flowery.NET/compare/v1.0.7...v1.0.8
[1.0.7]: https://github.com/tobitege/Flowery.NET/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/tobitege/Flowery.NET/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/tobitege/Flowery.NET/compare/v1.0.3...v1.0.5
[1.0.3]: https://github.com/tobitege/Flowery.NET/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/tobitege/Flowery.NET/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/tobitege/Flowery.NET/releases/tag/v1.0.1
