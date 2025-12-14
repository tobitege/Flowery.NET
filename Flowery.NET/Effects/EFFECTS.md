# Flowery.Effects

A collection of pluggable visual effects for Avalonia controls, designed for cross-platform compatibility including Browser/WASM.

## Installation

Add the namespace to your AXAML:

```xml
xmlns:fx="clr-namespace:Flowery.Effects;assembly=Flowery.NET"
```

---

## Effects

### RevealBehavior

Fade-in + slide animation when element enters the visual tree.

```xml
<Border fx:RevealBehavior.IsEnabled="True"
        fx:RevealBehavior.Duration="0:0:0.5"
        fx:RevealBehavior.Direction="Bottom"
        fx:RevealBehavior.Distance="30">
    <TextBlock Text="I fade in from below!"/>
</Border>
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | bool | false | Enable the effect |
| `Duration` | TimeSpan | 500ms | Animation duration |
| `Direction` | RevealDirection | Bottom | Origin direction (Top/Bottom/Left/Right) |
| `Distance` | double | 30 | Slide distance in pixels |
| `Easing` | Easing | CubicEaseOut | Easing function |

---

### ScrambleHoverBehavior

Randomly scrambles text characters on hover, then resolves left-to-right.

```xml
<TextBlock Text="Hover Me!"
           fx:ScrambleHoverBehavior.IsEnabled="True"
           fx:ScrambleHoverBehavior.ScrambleChars="!@#$%^&*()"
           fx:ScrambleHoverBehavior.Duration="0:0:0.5"
           fx:ScrambleHoverBehavior.FrameRate="30"/>
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | bool | false | Enable the effect |
| `ScrambleChars` | string | `!@#$%^&*()[]{}...` | Characters used for scrambling |
| `Duration` | TimeSpan | 500ms | Time to fully resolve text |
| `FrameRate` | int | 30 | Updates per second |

---

### WaveTextBehavior

Infinite sine wave animation on the Y axis.

```xml
<TextBlock Text="Wave!"
           fx:WaveTextBehavior.IsEnabled="True"
           fx:WaveTextBehavior.Amplitude="5"
           fx:WaveTextBehavior.Duration="0:0:1"
           fx:WaveTextBehavior.StaggerDelay="0:0:0.05"/>
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | bool | false | Enable the effect |
| `Amplitude` | double | 5 | Maximum vertical movement (pixels) |
| `Duration` | TimeSpan | 1000ms | Wave cycle duration |
| `StaggerDelay` | TimeSpan | 50ms | Delay between characters (future) |

---

### CursorFollowBehavior

Creates a follower element that tracks mouse position with spring physics.

```xml
<Panel fx:CursorFollowBehavior.IsEnabled="True"
       fx:CursorFollowBehavior.FollowerSize="20"
       fx:CursorFollowBehavior.FollowerBrush="{DynamicResource DaisyPrimaryBrush}"
       fx:CursorFollowBehavior.Stiffness="0.15"
       fx:CursorFollowBehavior.Damping="0.85"/>
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | bool | false | Enable the effect |
| `FollowerSize` | double | 20 | Size of follower circle |
| `FollowerBrush` | IBrush | DodgerBlue (50%) | Fill brush for follower |
| `Stiffness` | double | 0.15 | Spring stiffness (0-1) |
| `Damping` | double | 0.85 | Velocity damping (0-1) |

> **Note**: Must be applied to a `Panel` (e.g., `Grid`, `Canvas`, `StackPanel`).

---

## AnimationHelper

Core utility for WASM-compatible animations. Use this for custom effects:

```csharp
using Flowery.Effects;

// Animate a single value
await AnimationHelper.AnimateAsync(
    value => element.Opacity = value,
    from: 0.0,
    to: 1.0,
    duration: TimeSpan.FromMilliseconds(500),
    easing: new CubicEaseOut());

// Animate with progress callback (t = 0 to 1, eased)
await AnimationHelper.AnimateAsync(
    t =>
    {
        element.Opacity = t;
        transform.X = AnimationHelper.Lerp(startX, endX, t);
    },
    duration: TimeSpan.FromMilliseconds(500),
    easing: new CubicEaseInOut());
```

---

## Cross-Platform Compatibility

All effects use manual `Task.Delay` + `Dispatcher.UIThread` interpolation instead of Avalonia's declarative `Animation` keyframes. This pattern ensures consistent behavior across:

- ✅ Windows / macOS / Linux (Desktop)
- ✅ Browser / WebAssembly
- ✅ iOS / Android

### The Pattern

```csharp
// Standard Avalonia Animation - may have WASM issues
var animation = new Animation { Duration = duration, ... };
await animation.RunAsync(target);

// WASM-compatible pattern (used by Flowery.Effects)
for (int i = 0; i <= steps; i++)
{
    var t = easing.Ease((double)i / steps);
    await Dispatcher.UIThread.InvokeAsync(() => ApplyValue(t));
    if (i < steps) await Task.Delay(stepDuration, ct);
}
```

---

## Future Enhancements

- **ScrollableCardStack**: 3D stacking effect with scale/blur/opacity (deferred, requires Composition API)
- **Per-character WaveText**: Split text into individual TextBlocks for true character-level wave
- **TypewriterBehavior**: Progressive text reveal with blinking cursor

---

## Credits

This library is inspired by [smoothui](https://github.com/educlopez/smoothui) by Eduardo López, a React/Tailwind/Framer Motion component library.

**smoothui** is licensed under the [MIT License](https://github.com/educlopez/smoothui/blob/main/LICENSE).
