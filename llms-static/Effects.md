<!-- Supplementary documentation for ColorCollection -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

A collection of pluggable visual effects for Avalonia controls (namespace: Flowery.Effects), designed for cross-platform compatibility including Browser/WASM.

<img src="images/effects_showcase.webp" alt="Effects Showcase" style="max-width:50%;width:50%;height:auto;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.15);">

## Installation

Add the namespace to your AXAML:

```xml
xmlns:fx="clr-namespace:Flowery.Effects;assembly=Flowery.NET"
```

## Effects

### RevealBehavior

Entrance animation when element enters the visual tree. Supports 5 modes:

**FadeReveal (default):** Fades in while sliding into position.

```xml
<Border fx:RevealBehavior.IsEnabled="True"
        fx:RevealBehavior.Duration="0:0:0.6"
        fx:RevealBehavior.Direction="Bottom"
        fx:RevealBehavior.Distance="50">
    <TextBlock Text="I fade in from below!"/>
</Border>
```

**SlideIn:** Slides in fully visible (no fade). Good for drawers.

```xml
<Border fx:RevealBehavior.Mode="SlideIn" .../>
```

**FadeOnly:** Pure fade-in with no movement. Good for overlays.

```xml
<Border fx:RevealBehavior.Mode="FadeOnly" fx:RevealBehavior.Duration="0:0:0.8"/>
```

**Scale:** Scales up from center (0.8→1) while fading. Good for modals.

```xml
<Border fx:RevealBehavior.Mode="Scale" fx:RevealBehavior.Duration="0:0:0.5"/>
```

**ScaleSlide:** Combined scale + slide + fade. Maximum impact entrance.

```xml
<Border fx:RevealBehavior.Mode="ScaleSlide"
        fx:RevealBehavior.Direction="Bottom"
        fx:RevealBehavior.Distance="60"/>
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | bool | false | Enable the effect |
| `Mode` | RevealMode | FadeReveal | `FadeReveal`, `SlideIn`, `FadeOnly`, `Scale`, `ScaleSlide` |
| `Duration` | TimeSpan | 500ms | Animation duration |
| `Direction` | RevealDirection | Bottom | Origin direction (Top/Bottom/Left/Right) |
| `Distance` | double | 30 | Slide distance in pixels (for translate modes) |
| `Easing` | Easing | QuadraticEaseOut | Easing function |

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
<Panel Background="Transparent"
       fx:CursorFollowBehavior.IsEnabled="True"
       fx:CursorFollowBehavior.FollowerSize="20"
       fx:CursorFollowBehavior.FollowerShape="Circle"
       fx:CursorFollowBehavior.FollowerOpacity="0.75"
       fx:CursorFollowBehavior.FollowerBrush="{DynamicResource DaisyPrimaryBrush}"/>
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsEnabled` | bool | false | Enable the effect |
| `FollowerSize` | double | 20 | Size of follower element |
| `FollowerShape` | FollowerShape | Circle | `Circle`, `Square`, or `Ring` |
| `FollowerOpacity` | double | 1.0 | Opacity (0.0 - 1.0) |
| `FollowerBrush` | IBrush | DodgerBlue | Fill/stroke brush |
| `Stiffness` | double | 0.15 | Spring stiffness (0-1) |
| `Damping` | double | 0.85 | Velocity damping (0-1) |

> **Note**: Must be applied to a `Panel` (e.g., `Grid`, `Canvas`, `StackPanel`).
> [!IMPORTANT]
> **Container hit testing:** If you want tracking to work across a **container's full area** (not just over child elements), the Panel must have `Background="Transparent"` (or any brush). Without a background, Avalonia only detects mouse events over rendered content.

```xml
<!-- ✅ Correct: Panel receives mouse events everywhere -->
<Panel Background="Transparent" fx:CursorFollowBehavior.IsEnabled="True">
    <TextBlock Text="Content"/>
</Panel>

<!-- ❌ Wrong: Mouse events only fire over the TextBlock -->
<Panel fx:CursorFollowBehavior.IsEnabled="True">
    <TextBlock Text="Content"/>
</Panel>
```

---

## Runtime Usage (C#)

All effects can be applied or modified at runtime using static helper methods:

```csharp
using Flowery.Effects;

// Apply RevealBehavior to any Visual
RevealBehavior.SetIsEnabled(myBorder, true);
RevealBehavior.SetDuration(myBorder, TimeSpan.FromMilliseconds(800));
RevealBehavior.SetDirection(myBorder, RevealDirection.Left);

// Replay a reveal animation (toggle off then on)
RevealBehavior.SetIsEnabled(myBorder, false);
RevealBehavior.SetIsEnabled(myBorder, true);

// Apply ScrambleHover to a TextBlock
ScrambleHoverBehavior.SetIsEnabled(myTextBlock, true);
ScrambleHoverBehavior.SetScrambleChars(myTextBlock, "█▓▒░");

// Trigger scramble programmatically (for demos)
ScrambleHoverBehavior.TriggerScramble(myTextBlock);

// Apply WaveText to a TextBlock
WaveTextBehavior.SetIsEnabled(myTextBlock, true);
WaveTextBehavior.SetAmplitude(myTextBlock, 10);

// Apply CursorFollow to a Panel
CursorFollowBehavior.SetIsEnabled(myPanel, true);
CursorFollowBehavior.SetFollowerBrush(myPanel, Brushes.Red);
CursorFollowBehavior.SetFollowerShape(myPanel, FollowerShape.Ring);

// Programmatically control cursor follower (for demos/automation)
CursorFollowBehavior.ShowFollower(myPanel);
CursorFollowBehavior.SetTargetPosition(myPanel, 100, 50); // Set target x,y
CursorFollowBehavior.HideFollower(myPanel);
```

### Automation Example (Infinity Path)

```csharp
// Animate cursor follower in a figure-8 pattern
private async Task AnimateInfinityPath(Panel panel, CancellationToken ct)
{
    double t = 0;
    while (!ct.IsCancellationRequested)
    {
        var w = panel.Bounds.Width;
        var h = panel.Bounds.Height;
        
        // Lemniscate of Bernoulli
        var sinT = Math.Sin(t);
        var cosT = Math.Cos(t);
        var denom = 1 + sinT * sinT;
        
        var x = (cosT / denom) * w / 3 + w / 2;
        var y = (sinT * cosT / denom) * h / 2 + h / 2;
        
        CursorFollowBehavior.SetTargetPosition(panel, x, y);
        
        t += 0.03;
        await Task.Delay(16, ct);
    }
}
```

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
