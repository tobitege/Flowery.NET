<!-- Supplementary documentation for DaisyCalendarDatePicker -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyCalendarDatePicker is a `CalendarDatePicker` styled like `DaisyInput`. It keeps the calendar popup and text entry, replaces the Fluent glyph with the Daisy calendar icon, and themes the popup calendar with Daisy colors. The popup follows `Size`: its cells use the `DaisySize*Height` tokens and its text the `DaisySize*FontSize` tokens. Today is highlighted with a primary ring. The same control exists in Flowery.WinUI and Flowery.Uno with the same properties.

## Properties

| Property | Description |
| -------- | ----------- |
| `Date` (`DateTimeOffset?`) | Selected date. Mirrors the inherited `SelectedDate` (`DateTime?`); setting either updates the other. |
| `PlaceholderText` (string) | Text shown while no date is selected (inherited). |
| `Variant` (`DaisyInputVariant`, default Bordered) | Surface style, the same set as `DaisyInput`. |
| `Size` (`DaisySize`, default Medium) | Height, padding and font size from the `DaisySize*` and `DaisyInput*Padding` tokens. Follows the global size when not set. |
| `BorderRingBrush` (IBrush) | Border brush while focused; overrides the variant's focus color. |
| `DateChanged` (event) | Raised when the selected date changes, with `OldDate` and `NewDate`. |

## Variant Options

| Variant | Description |
| --- | --- |
| **Bordered** | Standard input outline (default). |
| **Ghost** | Transparent background with no border; a subtle fill while focused. |
| **Filled** | Filled background with a bottom border. |
| **Primary** / **Secondary** / **Accent** | Colored border from the palette. |
| **Info** / **Success** / **Warning** / **Error** | Status-colored border. |

## Quick Examples

```xml
<!-- Basic usage -->
<controls:DaisyCalendarDatePicker PlaceholderText="Select date" />

<!-- Variant and size -->
<controls:DaisyCalendarDatePicker Variant="Primary" Size="Small" PlaceholderText="Due date" />

<!-- Custom focus ring -->
<controls:DaisyCalendarDatePicker BorderRingBrush="{DynamicResource DaisyAccentBrush}" />
```

```csharp
picker.DateChanged += (_, e) => Console.WriteLine($"{e.OldDate} -> {e.NewDate}");
```

## Tips & Best Practices

- Use `Date` in code shared with Flowery.WinUI or Flowery.Uno; `SelectedDate` works as well on Avalonia.
- Use `Variant="Filled"` for low-contrast, form-heavy layouts.
- Pair with form validation to highlight missing or invalid dates.
