<!-- Supplementary documentation for DaisyCountdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyCountdown is a templated numeric timer that can show live clock units (hours/minutes/seconds) or run a countdown/looping counter. It supports 1–3 digit formatting, configurable tick interval, looping from a custom value, and raises `CountdownCompleted` when a non-looping countdown hits zero. Defaults to a monospace font for stable width.

## Modes & Properties

| Property | Description |
|----------|-------------|
| `ClockUnit` (`None`, `Hours`, `Minutes`, `Seconds`) | When set, pulls the current system time for that unit on each tick. Setting `ClockUnit` starts the timer automatically. |
| `IsCountingDown` (bool) | When true, decrements `Value` on each tick until 0; stops or loops depending on `Loop`. |
| `Value` (int, 0–999) | Current displayed number; coerced to 0–999. |
| `Digits` (1–3) | Pads `Value` (`D1`, `D2`, `D3`). |
| `Interval` (TimeSpan) | Tick interval (default 1s). Applies to both clock and countdown modes. |
| `Loop` (bool) / `LoopFrom` (int) | When `Loop=True`, resets `Value` to `LoopFrom` after reaching 0. |
| `DisplayValue` (string, read-only) | Formatted string used by the template. |
| `CountdownCompleted` (event) | Fired when a non-looping countdown reaches 0. |

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
```

## Tips & Best Practices

- Use `Digits="2"` for clock displays to maintain consistent width; `Digits="3"` for longer counts (e.g., days).
- Adjust `Interval` for faster/slower ticks; smaller intervals increase timer frequency, so keep UI lightweight.
- In loop mode, set `LoopFrom` to your desired reset point (commonly 59 or 99).
- For accessibility/legibility, keep the monospace font; if you override it, choose another fixed-width family.
- Remember to stop timers (`IsCountingDown=False`) if you hide/dispose the control; the template also stops the timer when detached from the visual tree.
