<!-- Supplementary documentation for DaisyCountdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyCountdown is a templated numeric timer that can show live clock units (hours/minutes/seconds) or run a countdown/looping counter. It supports 1–3 digit formatting, configurable tick interval, looping from a custom value, and raises `CountdownCompleted` when a non-looping countdown hits zero. Defaults to a monospace font for stable width.

## Modes & Properties

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `ClockUnit` | `CountdownClockUnit` | `None` | When set to `Hours`, `Minutes`, or `Seconds`, pulls the current system time for that unit on each tick. Setting `ClockUnit` starts the timer automatically. |
| `IsCountingDown` | `bool` | `false` | When true, decrements `Value` on each tick until 0; stops or loops depending on `Loop`. |
| `Value` | `int` | `0` | Current displayed number; coerced to 0–999. |
| `Digits` | `int` | `1` | Number of digits to display (1–3). Pads `Value` with zeros (`D1`, `D2`, `D3`). |
| `Interval` | `TimeSpan` | `1s` | Tick interval. Applies to both clock and countdown modes. |
| `Loop` | `bool` | `false` | When true, resets `Value` to `LoopFrom` after reaching 0 instead of stopping. |
| `LoopFrom` | `int` | `59` | Reset value used when `Loop=True`. |
| `Size` | `DaisySize` | `Medium` | Display size: ExtraSmall (16px), Small (20px), Medium (24px), Large (32px), ExtraLarge (40px). |
| `DisplayValue` | `string` | (read-only) | Formatted string used by the template. |

## Events

| Event | Description |
| ----- | ----------- |
| `CountdownCompleted` | Fired when a non-looping countdown reaches 0. |

## Quick Examples

```xml
<!-- Live clock: HH:MM:SS -->
<StackPanel Orientation="Horizontal" Spacing="4">
    <controls:DaisyCountdown ClockUnit="Hours" Digits="2" />
    <TextBlock Text=":" FontSize="24" VerticalAlignment="Center" />
    <controls:DaisyCountdown ClockUnit="Minutes" Digits="2" />
    <TextBlock Text=":" FontSize="24" VerticalAlignment="Center" />
    <controls:DaisyCountdown ClockUnit="Seconds" Digits="2" />
</StackPanel>

<!-- Looping seconds counter -->
<controls:DaisyCountdown Value="59"
                         Digits="2"
                         IsCountingDown="True"
                         Loop="True"
                         LoopFrom="59"
                         FontSize="48" />

<!-- Static numeric display with custom interval -->
<controls:DaisyCountdown Value="120" Digits="3" Interval="00:00:00.5" />

<!-- Countdown with completion handling (code-behind) -->
<controls:DaisyCountdown x:Name="Timer"
                         Value="10"
                         Digits="2"
                         IsCountingDown="True"
                         Loop="False" />

<!-- Size variants -->
<controls:DaisyCountdown Value="42" Size="ExtraSmall" />
<controls:DaisyCountdown Value="42" Size="Small" />
<controls:DaisyCountdown Value="42" Size="Medium" />
<controls:DaisyCountdown Value="42" Size="Large" />
<controls:DaisyCountdown Value="42" Size="ExtraLarge" />
```

## Accessibility Support

DaisyCountdown includes built-in accessibility for screen readers via the `AccessibleText` property. The control uses a live region to announce value changes, and the automation peer provides context about the current value and unit.

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `AccessibleText` | `string` | `"Countdown"` | Context text announced by screen readers (e.g., "Time remaining: 30 seconds"). |

### How It Works

1. **Live Region**: The control sets `AutomationLiveSetting.Polite` so screen readers announce value changes without interrupting other content.
2. **Unit Awareness**: When `ClockUnit` is set, the announcement includes the unit (e.g., "Countdown: 45 seconds").
3. **Custom Context**: Use `AccessibleText` to provide meaningful context.

### Accessibility Examples

```xml
<!-- Default: announces "Countdown: 30" -->
<controls:DaisyCountdown Value="30" />

<!-- With unit: announces "Countdown: 45 seconds" -->
<controls:DaisyCountdown ClockUnit="Seconds" Digits="2" />

<!-- Custom context: announces "Time remaining: 10" -->
<controls:DaisyCountdown Value="10" AccessibleText="Time remaining" IsCountingDown="True" />

<!-- Custom context with unit: announces "Session expires in: 5 minutes" -->
<controls:DaisyCountdown ClockUnit="Minutes" AccessibleText="Session expires in" Digits="2" />
```

## Global Size Awareness

DaisyCountdown implements `IScalableControl` and automatically responds to `FlowerySizeManager` global size changes. The control's font size scales proportionally based on the current global size setting.

```xml
<!-- Countdown that respects global size settings -->
<controls:DaisyCountdown Value="30" Size="Medium" />

<!-- Opt out of global size for fixed sizing -->
<controls:DaisyCountdown Value="30" Size="Large"
                         controls:FlowerySizeManager.IgnoreGlobalSize="True" />
```

## Tips & Best Practices

- **Digits**: Use `Digits="2"` for clock displays to maintain consistent width; `Digits="3"` for longer counts (e.g., days).
- **Interval**: Adjust for faster/slower ticks; smaller intervals increase timer frequency, so keep UI lightweight.
- **Loop Mode**: Set `LoopFrom` to your desired reset point (commonly 59 or 99).
- **Font**: Keep the monospace font for stable width; if you override it, choose another fixed-width family.
- **Cleanup**: The timer automatically stops when the control is detached from the visual tree. You can also manually stop it with `IsCountingDown=False`.
- **Accessibility**: Use `AccessibleText` to describe what the countdown represents (e.g., "Time until launch" or "Seconds remaining").
- **Size Consistency**: Use `DaisySize` values for consistent scaling. The control implements `IScalableControl` and responds to `FlowerySizeManager` global size changes.
