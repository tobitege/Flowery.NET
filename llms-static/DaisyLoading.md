<!-- Supplementary documentation for DaisyLoading -->
<!-- This content is prepended to auto-generated docs by generate_docs.py -->

# Overview

DaisyLoading provides animated loading indicators with **11 different animation styles**, **5 size options**, and **9 color variants**. The control includes both standard DaisyUI animations and creative terminal-inspired variants. All animations scale properly across all sizes using Viewbox-based rendering.

## Animation Variants

### DaisyUI Standard Variants

| Variant | Description |
|---------|-------------|
| **Spinner** | Classic rotating arc animation (default). Smooth 270° arc that rotates continuously. |
| **Dots** | Three dots bouncing vertically with staggered timing, creating a wave-like effect. |
| **Ring** | Rotating 90° arc with a subtle background track showing the full circle. |
| **Ball** | Single ball bouncing with squash/stretch deformation for a playful effect. |
| **Bars** | Three vertical bars with staggered height animation (audio equalizer style). |
| **Infinity** | Infinity symbol (∞) with animated dash offset creating a flowing path effect. |

### Terminal-Inspired Variants

| Variant | Description |
|---------|-------------|
| **Orbit** | Dots orbiting around a square border (npm/yarn terminal-style). Three dots with trailing opacity follow the square's perimeter: top → right → bottom → left. |
| **Snake** | Five segments moving back and forth horizontally with staggered delays, creating a "centipede" or "caterpillar" crawling effect. |
| **Pulse** | Sonar/heartbeat style - a center dot gently pulses while two rings expand outward and fade, creating a radar ping effect. |
| **Wave** | Five dots moving in a smooth sine wave pattern with staggered phases, reminiscent of audio equalizers or water ripples. |
| **Bounce** | Four squares in a 2×2 grid that gently highlight in clockwise sequence. Uses soft opacity transitions (0.25 → 0.7) to avoid harsh flashing. |

## Size Options

All variants scale proportionally across sizes. Canvas-based animations use Viewbox wrapping for smooth scaling.

| Size | Dimensions | Use Case |
|------|------------|----------|
| ExtraSmall | 16×16px | Inline with text, compact buttons |
| Small | 20×20px | Small UI elements, table cells |
| Medium | 24×24px | Default, general purpose (recommended) |
| Large | 36×36px | Prominent loading states, cards |
| ExtraLarge | 48×48px | Full-page loading overlays, hero sections |

## Color Variants

Use the `Color` property to apply theme colors. All variants support coloring.

| Color | Description |
|-------|-------------|
| `Default` | Base content color (inherits from theme) |
| `Primary` | Primary brand color |
| `Secondary` | Secondary brand color |
| `Accent` | Accent/highlight color |
| `Neutral` | Neutral/muted color |
| `Info` | Information/help color (typically blue) |
| `Success` | Success/confirmation color (typically green) |
| `Warning` | Warning/caution color (typically yellow/orange) |
| `Error` | Error/danger color (typically red) |

## Quick Examples

```xml
<!-- Basic spinner (default) -->
<controls:DaisyLoading Variant="Spinner" />

<!-- Different sizes -->
<controls:DaisyLoading Variant="Spinner" Size="ExtraSmall" />
<controls:DaisyLoading Variant="Spinner" Size="Large" />
<controls:DaisyLoading Variant="Spinner" Size="ExtraLarge" />

<!-- With colors -->
<controls:DaisyLoading Variant="Spinner" Color="Primary" />
<controls:DaisyLoading Variant="Ring" Color="Success" />
<controls:DaisyLoading Variant="Dots" Color="Warning" />

<!-- Terminal-style variants -->
<controls:DaisyLoading Variant="Orbit" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="Snake" Color="Success" />
<controls:DaisyLoading Variant="Pulse" Color="Info" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Wave" Color="Warning" />
<controls:DaisyLoading Variant="Bounce" Color="Error" Size="Large" />

<!-- Combining all options -->
<controls:DaisyLoading Variant="Orbit" Size="ExtraLarge" Color="Accent" />
```

## Animation Timing Reference

| Variant | Duration | Notes |
|---------|----------|-------|
| Spinner | 0.75s | Single rotation cycle |
| Dots | 0.6s | Bounce cycle with 0.1s stagger |
| Ring | 0.75s | Same as Spinner |
| Ball | 0.6s | Bounce with squash/stretch |
| Bars | 0.8s | Height pulse with 0.15s stagger |
| Infinity | 1.5s | Full dash offset cycle |
| Orbit | 1.2s | Full perimeter orbit with 0.15s trailing |
| Snake | 1.6s | Back-and-forth with 0.08s segment delay |
| Pulse | 1.5s | Ring expansion with 0.5s stagger |
| Wave | 1.0s | Sine wave with 0.1s phase delay |
| Bounce | 1.6s | Gentle clockwise sequence (0.4s per square) |
