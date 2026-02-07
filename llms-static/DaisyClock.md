<!-- Supplementary documentation for DaisyClock -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyClock is a comprehensive "batteries included" clock control that displays time with three visual styles (Segmented, Flip, Text), optional labels, and supports Clock, Timer, and Stopwatch modes. It provides localized labels, 12/24-hour formats, RTL support, and events for timer/stopwatch interaction. Designed as a full replacement for manual DaisyCountdown composition.

## Modes

| Mode | Description |
| --- | --- |
| `Clock` | Shows the current system time, updating every second. |
| `Timer` | Countdown timer from a specified duration. Fires `TimerFinished` when complete. |
| `Stopwatch` | Elapsed time counter with Start/Stop/Reset controls. |

## Display Styles

| Style | Description |
| --- | --- |
| `Segmented` | Uses DaisyCountdown controls for each time unit segment (default). |
| `Flip` | Classic bedside flip-clock look using TextBlocks inside DaisyJoin. |
| `Text` | Simple plain TextBlock showing formatted time string (lightest weight). |

## Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Mode` | `ClockMode` | `Clock` | Clock, Timer, or Stopwatch mode. |
| `DisplayStyle` | `ClockStyle` | `Segmented` | Visual style: Segmented, Flip, or Text. |
| `Orientation` | `Orientation` | `Horizontal` | Layout orientation. |
| `ShowSeconds` | `bool` | `true` | Whether to display the seconds component. |
| `LabelFormat` | `ClockLabelFormat` | `None` | None, Short ("4h 53m"), or Long ("4 hours 53 minutes"). |
| `Separator` | `string` | `":"` | Separator between time units (used with `LabelFormat.None`). |
| `Format` | `ClockFormat` | `TwentyFourHour` | 12-hour or 24-hour format. |
| `ShowAmPm` | `bool` | `false` | Whether to show AM/PM suffix (only in 12h mode). |
| `Size` | `DaisySize` | `Medium` | Size of the clock display. |
| `Spacing` | `double` | `6.0` | Spacing between time units. |
| `UseJoin` | `bool` | `false` | Use DaisyJoin for a segmented appearance. |
| `TimerDuration` | `TimeSpan` | `5 minutes` | Duration for Timer mode. |
| `TimerRemaining` | `TimeSpan` | (read-only) | Remaining time in Timer mode. |
| `StopwatchElapsed` | `TimeSpan` | (read-only) | Elapsed time in Stopwatch mode. |
| `IsRunning` | `bool` | (read-only) | Whether the timer/stopwatch is active. |

## Events

| Event | Description |
| --- | --- |
| `TimerFinished` | Raised when the timer reaches zero. |
| `TimerTick` | Raised each second during timer countdown. |
| `StopwatchTick` | Raised each tick during stopwatch operation. |
| `LapRecorded` | Raised when a lap is recorded via `RecordLap()`. |

## Public Methods

| Method | Description |
| --- | --- |
| `Start()` | Starts the timer or stopwatch (depending on mode). |
| `Pause()` | Pauses the timer or stopwatch. |
| `Reset()` | Stops and resets the timer or stopwatch. |
| `RecordLap()` | Records a lap at the current stopwatch time (Stopwatch mode only). |
| `ClearLaps()` | Clears all recorded laps. |

## Properties (Read-Only)

| Property | Type | Description |
| --- | --- | --- |
| `Laps` | `ReadOnlyObservableCollection<LapTime>` | Collection of recorded lap times. |
| `TimerRemaining` | `TimeSpan` | Remaining time in Timer mode. |
| `StopwatchElapsed` | `TimeSpan` | Elapsed time in Stopwatch mode. |
| `IsRunning` | `bool` | Whether the timer/stopwatch is currently running. |

## Quick Examples

```xml
<!-- Basic clock (24h, segmented) -->
<controls:DaisyClock Size="Large" />

<!-- 12-hour clock with AM/PM -->
<controls:DaisyClock Format="TwelveHour" ShowAmPm="True" />

<!-- Clock without seconds -->
<controls:DaisyClock ShowSeconds="False" />

<!-- Flip clock style (bedside clock look) -->
<controls:DaisyClock DisplayStyle="Flip" Size="Large" />

<!-- Plain text clock (lightest weight) -->
<controls:DaisyClock DisplayStyle="Text" Size="Large" />

<!-- Vertical layout with short labels -->
<controls:DaisyClock LabelFormat="Short" Orientation="Vertical" />

<!-- Joined segments (polished look) -->
<controls:DaisyClock UseJoin="True" Size="Large" />

<!-- 5-minute countdown timer -->
<controls:DaisyClock Mode="Timer"
                     TimerDuration="0:05:00"
                     TimerFinished="OnTimerDone" />

<!-- Stopwatch mode -->
<controls:DaisyClock Mode="Stopwatch" />
```

## Timer & Stopwatch Control (Code-Behind)

```csharp
// Unified Start/Pause/Reset API
MyClock.Start();   // Works for both Timer and Stopwatch modes
MyClock.Pause();   // Pause timer or stopwatch
MyClock.Reset();   // Reset to initial state

// Lap recording (Stopwatch mode only)
MyClock.RecordLap();  // Records current elapsed as a lap
MyClock.ClearLaps();  // Clear all recorded laps

// Access recorded laps
foreach (var lap in MyClock.Laps)
{
    Console.WriteLine($"Lap {lap.Number}: {lap.LapDuration:mm\\:ss\\.ff} (Total: {lap.TotalElapsed:hh\\:mm\\:ss})");
}

// Event handling
MyClock.TimerFinished += (s, e) => ShowNotification("Timer complete!");
MyClock.StopwatchTick += (s, elapsed) => UpdateDisplay(elapsed);
MyClock.LapRecorded += (s, lap) => AddLapToList(lap);
```

## Localization

DaisyClock uses `FloweryLocalization.GetStringInternal()` for localized labels. The following keys are defined in the embedded JSON resource files (`Flowery.NET/Localization/*.json`):

| Key | English | Description |
| --- | --- | --- |
| `Clock_Hour` | "hour" | Singular hour label (Long format) |
| `Clock_Hours` | "hours" | Plural hours label (Long format) |
| `Clock_Hours_Short` | "h" | Short hour suffix (Short format) |
| `Clock_Minute` | "minute" | Singular minute label (Long format) |
| `Clock_Minutes` | "minutes" | Plural minutes label (Long format) |
| `Clock_Minutes_Short` | "m" | Short minute suffix (Short format) |
| `Clock_Second` | "second" | Singular second label (Long format) |
| `Clock_Seconds` | "seconds" | Plural seconds label (Long format) |
| `Clock_Seconds_Short` | "s" | Short second suffix (Short format) |
| `Clock_AM` | "AM" | AM indicator (12-hour format) |
| `Clock_PM` | "PM" | PM indicator (12-hour format) |

The control subscribes to `FloweryLocalization.CultureChanged` and automatically updates when the culture changes. Supported languages include: English, German, French, Spanish, Italian, Japanese, Korean, Chinese (Simplified), Arabic, Turkish, Ukrainian, and Hebrew.

RTL layouts (Arabic, Hebrew) are automatically handled by Avalonia's `FlowDirection` based on the current culture.

## Tips & Best Practices

- **Display Style Selection**: Use `Segmented` for separate digit segments, `Flip` for a retro aesthetic, and `Text` for minimal resource usage.
- **Labels with Vertical**: `LabelFormat.Long` works best with `Orientation="Vertical"` to avoid cramped layouts.
- **UseJoin**: Best suited for `LabelFormat.None`; combines well with `Flip` style.
- **Timer Events**: Always handle `TimerFinished` to notify users when the countdown completes.
- **Stopwatch Performance**: The stopwatch uses `DispatcherTimer` with a 100ms interval for smooth updates.
- **Size Consistency**: Use `DaisySize` values for consistent scaling across the application. The control implements `IScalableControl` and responds to `FlowerySizeManager` global size changes.
- **RTL Support**: The clock automatically adjusts flow direction based on the current culture via `FloweryLocalization.IsRtl`.
- **Memory**: Timers are automatically stopped in `OnDetachedFromVisualTree` when the control is removed from the visual tree, and culture change handlers are properly unsubscribed.
