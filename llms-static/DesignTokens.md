# Design Tokens

Flowery.NET uses centralized design tokens to ensure consistent sizing across all controls. These tokens are defined in `Themes/DaisyTokens.axaml` and can be overridden in your application to customize the entire design system.

## Overview

Design tokens provide a single source of truth for:

- **Control Heights** - Standard heights for buttons, inputs, selects
- **Font Sizes** - Typography scale across controls
- **Corner Radius** - Rounded corners for controls
- **Padding** - Internal spacing for different control types
- **Spacing** - Layout spacing values
- **Border Thickness** - Standard border widths

## How to Override Tokens

Add your overrides in `App.axaml` before the DaisyUITheme. These will take precedence:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:daisy="clr-namespace:Flowery.Controls;assembly=Flowery.NET"
             xmlns:sys="clr-namespace:System;assembly=mscorlib">
    
    <Application.Resources>
        <!-- Override to make all "Small" controls more compact -->
        <sys:Double x:Key="DaisySizeSmallHeight">28</sys:Double>
        <sys:Double x:Key="DaisySizeSmallFontSize">11</sys:Double>
        <Thickness x:Key="DaisyButtonSmallPadding">10,0</Thickness>
    </Application.Resources>

    <Application.Styles>
        <daisy:DaisyUITheme />
    </Application.Styles>
</Application>
```

## Token Reference

### Control Heights

Used for buttons, inputs, selects, and other fixed-height controls.

| Token | Default | Used By |
|-------|---------|---------|
| `DaisySizeExtraLargeHeight` | 48 | DaisyButton, DaisyInput, DaisySelect, DaisyNumericUpDown |
| `DaisySizeLargeHeight` | 48 | " |
| `DaisySizeMediumHeight` | 40 | " |
| `DaisySizeSmallHeight` | 32 | " |
| `DaisySizeExtraSmallHeight` | 24 | " |

### Floating Input Heights

Specific heights for `DaisyInput` with `LabelPosition="Floating"`. These add headroom for the label to float above the input border.

| Token | Default |
|-------|---------|
| `DaisyInputFloatingExtraLargeHeight` | 72 |
| `DaisyInputFloatingLargeHeight` | 72 |
| `DaisyInputFloatingMediumHeight` | 64 |
| `DaisyInputFloatingSmallHeight` | 56 |
| `DaisyInputFloatingExtraSmallHeight` | 48 |

### Font Sizes

#### Primary Font Sizes

Typography scale used across controls for main content.

| Token | Default |
|-------|---------|
| `DaisySizeExtraLargeFontSize` | 20 |
| `DaisySizeLargeFontSize` | 18 |
| `DaisySizeMediumFontSize` | 14 |
| `DaisySizeSmallFontSize` | 12 |
| `DaisySizeExtraSmallFontSize` | 10 |

#### Secondary Font Sizes

For hint text, helper text, captions, labels (~0.8x of primary).

| Token | Default |
|-------|---------|
| `DaisySizeExtraLargeSecondaryFontSize` | 16 |
| `DaisySizeLargeSecondaryFontSize` | 14 |
| `DaisySizeMediumSecondaryFontSize` | 12 |
| `DaisySizeSmallSecondaryFontSize` | 10 |
| `DaisySizeExtraSmallSecondaryFontSize` | 9 |

#### Tertiary Font Sizes

For very small captions, counters (~0.7x of primary).

| Token | Default |
|-------|---------|
| `DaisySizeExtraLargeTertiaryFontSize` | 14 |
| `DaisySizeLargeTertiaryFontSize` | 12 |
| `DaisySizeMediumTertiaryFontSize` | 11 |
| `DaisySizeSmallTertiaryFontSize` | 9 |
| `DaisySizeExtraSmallTertiaryFontSize` | 8 |

#### Header Font Sizes

For section titles and headings (~1.4x of primary).

| Token | Default |
|-------|---------|
| `DaisySizeExtraLargeHeaderFontSize` | 28 |
| `DaisySizeLargeHeaderFontSize` | 24 |
| `DaisySizeMediumHeaderFontSize` | 20 |
| `DaisySizeSmallHeaderFontSize` | 16 |
| `DaisySizeExtraSmallHeaderFontSize` | 14 |

#### Responsive Font Attached Property

Use `FlowerySizeManager.ResponsiveFont` attached property to make TextBlocks respond to global size changes. This subscribes to the `SizeChanged` event and updates font sizes programmatically.

| Tier | Description | XS/S/M/L/XL Sizes |
|------|-------------|-------------------|
| `Primary` | Body text | 10/12/14/18/20 |
| `Secondary` | Hints, captions | 9/10/12/14/16 |
| `Tertiary` | Very small text | 8/9/11/12/14 |
| `Header` | Section titles | 14/16/20/24/28 |

**Usage:**

```xml
<TextBlock Text="Description text" controls:FlowerySizeManager.ResponsiveFont="Primary" />
<TextBlock Text="Secondary hint" controls:FlowerySizeManager.ResponsiveFont="Secondary" />
<TextBlock Text="Section Title" controls:FlowerySizeManager.ResponsiveFont="Header" FontWeight="Bold" />
```

> **Note:** `DynamicResource` bindings to font size tokens don't update reliably due to Avalonia's nested resource scoping. Use the `ResponsiveFont` attached property instead for text that should scale with global size.

### Corner Radius

Rounded corners for controls.

| Token | Default |
|-------|---------|
| `DaisySizeExtraLargeCornerRadius` | 12 |
| `DaisySizeLargeCornerRadius` | 10 |
| `DaisySizeMediumCornerRadius` | 8 |
| `DaisySizeSmallCornerRadius` | 6 |
| `DaisySizeExtraSmallCornerRadius` | 4 |

### Button Padding

Horizontal-only padding (vertical is controlled by MinHeight).

| Token | Default |
|-------|---------|
| `DaisyButtonExtraLargePadding` | 32,0 |
| `DaisyButtonLargePadding` | 24,0 |
| `DaisyButtonMediumPadding` | 16,0 |
| `DaisyButtonSmallPadding` | 12,0 |
| `DaisyButtonExtraSmallPadding` | 8,0 |

### Input Padding

Horizontal-only padding for text inputs.

| Token | Default |
|-------|---------|
| `DaisyInputExtraLargePadding` | 20,0 |
| `DaisyInputLargePadding` | 18,0 |
| `DaisyInputMediumPadding` | 16,0 |
| `DaisyInputSmallPadding` | 12,0 |
| `DaisyInputExtraSmallPadding` | 8,0 |

### Tab Padding

Both horizontal and vertical padding (tabs don't use fixed height).

| Token | Default |
|-------|---------|
| `DaisyTabExtraLargePadding` | 18,10 |
| `DaisyTabLargePadding` | 16,8 |
| `DaisyTabMediumPadding` | 14,6 |
| `DaisyTabSmallPadding` | 12,4 |
| `DaisyTabExtraSmallPadding` | 10,2 |

### Badge Tokens

Badges use a separate, more compact scale.

| Token | Default |
|-------|---------|
| `DaisyBadgeLargeHeight` | 24 |
| `DaisyBadgeMediumHeight` | 20 |
| `DaisyBadgeSmallHeight` | 16 |
| `DaisyBadgeExtraSmallHeight` | 12 |
| `DaisyBadgeLargeFontSize` | 14 |
| `DaisyBadgeMediumFontSize` | 12 |
| `DaisyBadgeSmallFontSize` | 10 |
| `DaisyBadgeExtraSmallFontSize` | 8 |
| `DaisyBadgeLargePadding` | 12,0 |
| `DaisyBadgeMediumPadding` | 8,0 |
| `DaisyBadgeSmallPadding` | 6,0 |
| `DaisyBadgeExtraSmallPadding` | 4,0 |

### Card Padding

| Token | Default |
|-------|---------|
| `DaisyCardLargePadding` | 32 |
| `DaisyCardMediumPadding` | 24 |
| `DaisyCardSmallPadding` | 16 |
| `DaisyCardCompactPadding` | 12 |

### Spacing

General spacing values for layouts.

| Token | Default |
|-------|---------|
| `DaisySpacingXL` | 24 |
| `DaisySpacingLarge` | 16 |
| `DaisySpacingMedium` | 12 |
| `DaisySpacingSmall` | 8 |
| `DaisySpacingXS` | 4 |

### Border Thickness

| Token | Default |
|-------|---------|
| `DaisyBorderThicknessNone` | 0 |
| `DaisyBorderThicknessThin` | 1 |
| `DaisyBorderThicknessMedium` | 2 |
| `DaisyBorderThicknessThick` | 3 |

## Controls Using Tokens

The following controls use design tokens for consistent sizing:

- **DaisyButton** - Heights, font sizes, padding, corner radius
- **DaisyInput** - Heights, font sizes, padding, corner radius
- **DaisySelect** - Heights, font sizes, corner radius
- **DaisyNumericUpDown** - Heights, font sizes, corner radius
- **DaisyTabs** - Font sizes, padding, corner radius
- **DaisyBadge** - Badge-specific heights, font sizes, padding
- **DaisyFileInput** - Heights, font sizes, corner radius
- **DaisyCheckBox** - Indicator sizes, checkmark sizes
- **DaisyRadio** - Indicator sizes, inner dot sizes
- **DaisyToggle** - Switch dimensions, knob sizes
- **DaisyProgress** - Bar heights, corner radius
- **DaisyAvatar** - Avatar dimensions
- **DaisyMenu** - Item padding, font sizes
- **DaisyKbd** - Heights, padding, font sizes, corner radius
- **DaisyTextArea** - MinHeight, padding (multiline-specific)
- **DaisyDateTimeline** - Item heights, padding, corner radius, font sizes
- **DaisyNumberFlow** - LargeDisplay tokens (element sizes, buttons, container)

---

### Checkbox/Radio Indicator Sizes

Outer border size for checkboxes and radio buttons.

| Token | Default |
|-------|---------|
| `DaisyCheckboxExtraLargeSize` | 40 |
| `DaisyCheckboxLargeSize` | 32 |
| `DaisyCheckboxMediumSize` | 24 |
| `DaisyCheckboxSmallSize` | 20 |
| `DaisyCheckboxExtraSmallSize` | 16 |

### Checkmark Icon Sizes

| Token | Default |
|-------|---------|
| `DaisyCheckmarkExtraLargeSize` | 24 |
| `DaisyCheckmarkLargeSize` | 20 |
| `DaisyCheckmarkMediumSize` | 16 |
| `DaisyCheckmarkSmallSize` | 12 |
| `DaisyCheckmarkExtraSmallSize` | 10 |

### Radio Inner Dot Sizes

| Token | Default |
|-------|---------|
| `DaisyRadioDotExtraLargeSize` | 24 |
| `DaisyRadioDotLargeSize` | 20 |
| `DaisyRadioDotMediumSize` | 14 |
| `DaisyRadioDotSmallSize` | 12 |
| `DaisyRadioDotExtraSmallSize` | 8 |

---

### Toggle Switch Sizes

| Token | Default |
|-------|---------|
| `DaisyToggleExtraLargeWidth` | 72 |
| `DaisyToggleExtraLargeHeight` | 40 |
| `DaisyToggleLargeWidth` | 60 |
| `DaisyToggleLargeHeight` | 32 |
| `DaisyToggleMediumWidth` | 48 |
| `DaisyToggleMediumHeight` | 24 |
| `DaisyToggleSmallWidth` | 36 |
| `DaisyToggleSmallHeight` | 20 |
| `DaisyToggleExtraSmallWidth` | 28 |
| `DaisyToggleExtraSmallHeight` | 16 |

### Toggle Knob Sizes

| Token | Default |
|-------|---------|
| `DaisyToggleKnobExtraLargeSize` | 32 |
| `DaisyToggleKnobLargeSize` | 26 |
| `DaisyToggleKnobMediumSize` | 20 |
| `DaisyToggleKnobSmallSize` | 16 |
| `DaisyToggleKnobExtraSmallSize` | 12 |

---

### Progress Bar Heights

| Token | Default |
|-------|---------|
| `DaisyProgressLargeHeight` | 16 |
| `DaisyProgressMediumHeight` | 8 |
| `DaisyProgressSmallHeight` | 4 |
| `DaisyProgressExtraSmallHeight` | 2 |

### Progress Corner Radius

| Token | Default |
|-------|---------|
| `DaisyProgressLargeCornerRadius` | 8 |
| `DaisyProgressMediumCornerRadius` | 4 |
| `DaisyProgressSmallCornerRadius` | 2 |
| `DaisyProgressExtraSmallCornerRadius` | 1 |

---

### Avatar Sizes

| Token | Default |
|-------|---------|
| `DaisyAvatarExtraLargeSize` | 128 |
| `DaisyAvatarLargeSize` | 96 |
| `DaisyAvatarMediumSize` | 48 |
| `DaisyAvatarSmallSize` | 32 |
| `DaisyAvatarExtraSmallSize` | 24 |

---

### Menu Item Padding

| Token | Default |
|-------|---------|
| `DaisyMenuExtraLargePadding` | 20,16 |
| `DaisyMenuLargePadding` | 16,12 |
| `DaisyMenuMediumPadding` | 12,8 |
| `DaisyMenuSmallPadding` | 10,6 |
| `DaisyMenuExtraSmallPadding` | 8,4 |

### Menu Font Sizes

| Token | Default |
|-------|---------|
| `DaisyMenuExtraLargeFontSize` | 18 |
| `DaisyMenuLargeFontSize` | 16 |
| `DaisyMenuMediumFontSize` | 14 |
| `DaisyMenuSmallFontSize` | 12 |
| `DaisyMenuExtraSmallFontSize` | 11 |

---

### Kbd (Keyboard) Heights

| Token | Default |
|-------|---------|
| `DaisyKbdExtraLargeHeight` | 40 |
| `DaisyKbdLargeHeight` | 32 |
| `DaisyKbdMediumHeight` | 24 |
| `DaisyKbdSmallHeight` | 20 |
| `DaisyKbdExtraSmallHeight` | 16 |

### Kbd Padding

| Token | Default |
|-------|---------|
| `DaisyKbdExtraLargePadding` | 8,0 |
| `DaisyKbdLargePadding` | 6,0 |
| `DaisyKbdMediumPadding` | 4,0 |
| `DaisyKbdSmallPadding` | 3,0 |
| `DaisyKbdExtraSmallPadding` | 2,0 |

### Kbd Font Sizes

| Token | Default |
|-------|---------|
| `DaisyKbdExtraLargeFontSize` | 16 |
| `DaisyKbdLargeFontSize` | 14 |
| `DaisyKbdMediumFontSize` | 12 |
| `DaisyKbdSmallFontSize` | 11 |
| `DaisyKbdExtraSmallFontSize` | 10 |

### Kbd Corner Radius

| Token | Default |
|-------|---------|
| `DaisyKbdExtraLargeCornerRadius` | 6 |
| `DaisyKbdLargeCornerRadius` | 5 |
| `DaisyKbdMediumCornerRadius` | 4 |
| `DaisyKbdSmallCornerRadius` | 3 |
| `DaisyKbdExtraSmallCornerRadius` | 2 |

### TextArea MinHeight

| Token | Default |
|-------|---------|
| `DaisyTextAreaExtraLargeMinHeight` | 160 |
| `DaisyTextAreaLargeMinHeight` | 120 |
| `DaisyTextAreaMediumMinHeight` | 80 |
| `DaisyTextAreaSmallMinHeight` | 60 |
| `DaisyTextAreaExtraSmallMinHeight` | 48 |

### TextArea Padding

| Token | Default |
|-------|---------|
| `DaisyTextAreaExtraLargePadding` | 24 |
| `DaisyTextAreaLargePadding` | 20 |
| `DaisyTextAreaMediumPadding` | 16 |
| `DaisyTextAreaSmallPadding` | 12 |
| `DaisyTextAreaExtraSmallPadding` | 8 |

### Input/TextArea Vertical Alignment

| Token | Default |
|-------|---------|
| `DaisyInputVerticalContentAlignment` | Center |
| `DaisyTextAreaVerticalContentAlignment` | Top |

---

### DateTimeline Item Heights

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeHeight` | 130 |
| `DaisyDateTimelineLargeHeight` | 96 |
| `DaisyDateTimelineMediumHeight` | 80 |
| `DaisyDateTimelineSmallHeight` | 68 |
| `DaisyDateTimelineExtraSmallHeight` | 56 |

### DateTimeline Item Widths

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeItemWidth` | 96 |
| `DaisyDateTimelineLargeItemWidth` | 80 |
| `DaisyDateTimelineMediumItemWidth` | 64 |
| `DaisyDateTimelineSmallItemWidth` | 56 |
| `DaisyDateTimelineExtraSmallItemWidth` | 48 |

### DateTimeline Item Padding

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargePadding` | 16,24 |
| `DaisyDateTimelineLargePadding` | 12,16 |
| `DaisyDateTimelineMediumPadding` | 8,12 |
| `DaisyDateTimelineSmallPadding` | 6,10 |
| `DaisyDateTimelineExtraSmallPadding` | 4,8 |

### DateTimeline Corner Radius

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeCornerRadius` | 20 |
| `DaisyDateTimelineLargeCornerRadius` | 16 |
| `DaisyDateTimelineMediumCornerRadius` | 12 |
| `DaisyDateTimelineSmallCornerRadius` | 10 |
| `DaisyDateTimelineExtraSmallCornerRadius` | 8 |

### DateTimeline Day Number Font Sizes

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeDayNumberFontSize` | 32 |
| `DaisyDateTimelineLargeDayNumberFontSize` | 26 |
| `DaisyDateTimelineMediumDayNumberFontSize` | 20 |
| `DaisyDateTimelineSmallDayNumberFontSize` | 16 |
| `DaisyDateTimelineExtraSmallDayNumberFontSize` | 14 |

### DateTimeline Day Name Font Sizes

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeDayNameFontSize` | 15 |
| `DaisyDateTimelineLargeDayNameFontSize` | 13 |
| `DaisyDateTimelineMediumDayNameFontSize` | 10 |
| `DaisyDateTimelineSmallDayNameFontSize` | 10 |
| `DaisyDateTimelineExtraSmallDayNameFontSize` | 9 |

### DateTimeline Month Name Font Sizes

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeMonthNameFontSize` | 14 |
| `DaisyDateTimelineLargeMonthNameFontSize` | 12 |
| `DaisyDateTimelineMediumMonthNameFontSize` | 10 |
| `DaisyDateTimelineSmallMonthNameFontSize` | 9 |
| `DaisyDateTimelineExtraSmallMonthNameFontSize` | 8 |

### DateTimeline Header Font Sizes

| Token | Default |
|-------|---------|
| `DaisyDateTimelineExtraLargeHeaderFontSize` | 20 |
| `DaisyDateTimelineLargeHeaderFontSize` | 18 |
| `DaisyDateTimelineMediumHeaderFontSize` | 16 |
| `DaisyDateTimelineSmallHeaderFontSize` | 14 |
| `DaisyDateTimelineExtraSmallHeaderFontSize` | 12 |

---

## Large Display Tokens

Tokens for dashboard-style display controls like `DaisyNumberFlow`, counters, and statistics displays. These are "bigger than normal" controls meant for prominent numeric displays.

### Display Element (Digit Box) Sizing

| Token | Default | Description |
|-------|---------|-------------|
| `LargeDisplayFontSize` | 36 | Font size for display digits |
| `LargeDisplayElementHeight` | 86 | Height of each digit box |
| `LargeDisplayElementWidth` | 58 | Width of each digit box |
| `LargeDisplayElementCornerRadius` | 12 | Corner radius for digit boxes |
| `LargeDisplayElementSpacing` | 4 | Gap between digit boxes |

### Display Control Buttons

| Token | Default | Description |
|-------|---------|-------------|
| `LargeDisplayButtonSize` | 42 | Width/height of +/- buttons |
| `LargeDisplayButtonFontSize` | 20 | Font size for +/- symbols |
| `LargeDisplayButtonCornerRadius` | 8 | Corner radius for buttons |
| `LargeDisplayButtonSpacing` | 2 | Gap between stacked buttons |

### Display Container (Outer Wrapper)

| Token | Default | Description |
|-------|---------|-------------|
| `LargeDisplayContainerPadding` | 16 | Inner padding of container |
| `LargeDisplayContainerCornerRadius` | 16 | Corner radius of container |

### Controls Using LargeDisplay Tokens

- **DaisyNumberFlow** - Animated number display control
