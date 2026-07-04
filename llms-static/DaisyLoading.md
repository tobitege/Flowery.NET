<!-- Supplementary documentation for DaisyLoading -->
<!-- This content is prepended to auto-generated docs by generate_docs.py -->

# Overview

DaisyLoading provides animated loading indicators with **73 different animation styles**, **5 size options**, and **9 color variants**. The control includes standard DaisyUI animations, creative terminal-inspired variants, Matrix/retro variants, unique special effect variants, professional business-themed animations, and nostalgic Windows 95-style animations. All animations scale properly across all sizes using Viewbox-based rendering.

![Loading Animations](images/loading_animations.gif)

**Key Feature:** DaisyLoading includes built-in accessibility support for screen readers via the `AccessibleText` property and proper automation peers.

## Animation Variants

### DaisyUI Standard Variants

| Variant | Description |
| ------- | ----------- |
| **Spinner** | Classic rotating arc animation (default). Smooth 270° arc that rotates continuously. |
| **Dots** | Three dots bouncing vertically with staggered timing, creating a wave-like effect. |
| **Ring** | Rotating 90° arc with a subtle background track showing the full circle. |
| **Ball** | Single ball bouncing with squash/stretch deformation for a playful effect. |
| **Bars** | Three vertical bars with staggered height animation (audio equalizer style). |
| **Infinity** | Infinity symbol (∞) with animated dash offset creating a flowing path effect. |

### Terminal-Inspired Variants

| Variant | Description |
| ------- | ----------- |
| **Orbit** | Dots orbiting around a square border (npm/yarn terminal-style). Three dots with trailing opacity follow the square's perimeter: top → right → bottom → left. |
| **Snake** | Five segments moving back and forth horizontally with staggered delays, creating a "centipede" or "caterpillar" crawling effect. |
| **Pulse** | Sonar/heartbeat style - a center dot gently pulses while two rings expand outward and fade, creating a radar ping effect. |
| **Wave** | Five dots moving in a smooth sine wave pattern with staggered phases, reminiscent of audio equalizers or water ripples. |
| **Bounce** | Four squares in a 2×2 grid that gently highlight in clockwise sequence. Uses soft opacity transitions (0.25 → 0.7) to avoid harsh flashing. |

### Matrix/Colon-Dot Variants

| Variant | Description |
| ------- | ----------- |
| **Matrix** | Colon-dotted pattern (`::: :::`) with a smooth wave of brightness traveling left to right. Each "colon" is two vertically stacked dots, grouped in sets of 3 with gaps between groups. |
| **MatrixInward** | Same colon pattern but the wave starts from the center (inner dots) and moves outward to the edges. Creates a "burst from center" effect. |
| **MatrixOutward** | Same colon pattern but the wave starts from the edges (outer dots) and converges toward the center. Creates a "closing in" effect. |
| **MatrixVertical** | Same colon pattern but the wave moves vertically - all top dots light up together, then all bottom dots. Creates a vertical "blink" effect. |

### Special Effect Variants

| Variant | Description |
| ------- | ----------- |
| **MatrixRain** | Digital rain inspired by "The Matrix" movie. Four columns of falling dots at different speeds, each with a bright leading dot and dimmer trailing dot. |
| **Hourglass** | Classic hourglass timer with flowing sand animation. Top sand depletes, stream flows through the middle, bottom sand accumulates. 2-second cycle. |
| **SignalSweep** | Oscilloscope-style bar sweeping left to right with a fading gradient trail. Includes subtle horizontal grid lines. |
| **BitFlip** | 4×2 grid of dots flipping on/off in pseudo-random binary patterns, like data streaming through a register. |
| **PacketBurst** | Center dot pulses while four particles shoot outward to all edges (left, right, up, down), mimicking network packet transmission. |
| **CometTrail** | Bright dot orbiting in a circle with three trailing dots that fade behind it, creating a comet-like tail effect. |
| **Heartbeat** | EKG/heart monitor-style pulse line scrolling horizontally with characteristic heartbeat spikes. |
| **TunnelZoom** | Three concentric rings expanding outward from center and fading, creating a warp tunnel/zoom effect. |
| **GlitchReveal** | Six vertical columns flashing in random-seeming patterns, mimicking terminal glitch/interference effects. |
| **RippleMatrix** | 3×3 dot grid (9 dots) with brightness rippling outward from the center dot to adjacent dots then corner dots. |
| **CursorBlink** | Classic CLI terminal prompt (`>`) with a blinking block cursor. Simple and nostalgic. |
| **CountdownSpinner** | 12 dots arranged in a clock face pattern, lighting up sequentially like clock hands ticking. |

### Business Variants

Professional animations suitable for enterprise and productivity applications.

| Variant | Description |
| ------- | ----------- |
| **DocumentFlipOn** | Three stacked document pages with the front page sliding in from the upper-left. Represents opening/loading a document. |
| **DocumentFlipOff** | Three stacked document pages with the front page sliding out to the upper-right and fading. Represents closing/saving a document. |
| **MailSend** | Paper/letter sliding down into an envelope. Perfect for email sending or message submission states. |
| **CloudUpload** | Cloud icon with an animated arrow rising upward. Ideal for upload operations. |
| **CloudDownload** | Cloud icon with an animated arrow falling downward. Ideal for download operations. |
| **DocumentStamp** | Document with an "OK" approval stamp that scales down with a bounce effect. Great for approval/confirmation states. |
| **DocumentReject** | Document with an "✕" rejection stamp that scales down with a bounce effect. Perfect for rejection/error states. |
| **ChartPulse** | Five bar chart columns pulsing at different heights with a sweep line. Analytics/reporting themed. |
| **CalendarTick** | Calendar with header and rings, featuring a checkmark that pops in. Scheduling/completion themed. |
| **ApprovalFlow** | Three workflow nodes (circles) with inner dots that pulse in sequence. Workflow/process states. |
| **BriefcaseSpin** | Briefcase with handle that wobbles side-to-side with a subtle bounce. Business/work themed. |
| **BatteryCharging** | Segmented battery icon that fills from empty to full, useful for device/power states. |
| **BatteryEmptying** | Segmented battery icon that drains from full to empty, useful for discharge or remaining-power states. |
| **TrafficLightUp** | Vertical traffic light that cycles from green to yellow to red. |
| **TrafficLightRight** | Rotated traffic light that cycles toward the right. |
| **TrafficLightDown** | Vertical traffic light that cycles from red to yellow to green. |
| **TrafficLightLeft** | Rotated traffic light that cycles toward the left. |
| **PrinterOutput** | Printer body with a page feeding out line by line. Good for report generation, print queues, and document export. |
| **PaperShredder** | Document entering a shredder while strips fall below. Useful for secure deletion, redaction, or disposal workflows. |
| **SignaturePen** | Pen travels across a document while signature strokes appear in sequence. Represents contract signing and approval completion. |
| **DocumentScan** | Scan beam moves down a document while text lines brighten. Ideal for OCR, imports, and document ingestion. |
| **FolderSync** | Two folders connected by sync arrows with a document moving between them. Suitable for folder sync and replication tasks. |
| **MailReceive** | Envelope opens while a document rises out. Counterpart to MailSend for receiving or inbox operations. |
| **PhoneRing** | Handset shakes with wave arcs pulsing outward. Good for calls, contact attempts, or connection setup. |
| **CoinStack** | Coin drops onto a stack while stack levels brighten. Suitable for payment, billing, and accounting workflows. |
| **InvoicePaid** | Invoice document with a PAID stamp that lands with a scale bounce. Purpose-built for billing and payment confirmation. |
| **PiggyBank** | Coin falls into a piggy bank and the body wobbles. Useful for saving, budgeting, or lightweight finance states. |
| **PieChartFill** | Pie chart segments brighten in sequence. Suitable for dashboard loading and report aggregation. |
| **TrendLine** | Analytical trend line draws upward over a grid and ends with an arrow. Good for forecasts, charts, and data analysis. |
| **ClockSpin** | Analog clock with independently rotating hands. Represents waiting, scheduling, or time-sensitive processing. |
| **CoffeeCup** | Coffee cup with rising steam trails. Friendly "please wait" animation for longer business operations. |

### Windows 95 Retro Variants

Nostalgic animations inspired by classic Windows 95 file operations.

| Variant | Description |
| ------- | ----------- |
| **Win95FileCopy** | Two folder icons with paper documents flying in an arc from left folder to right folder. Classic file copy animation. |
| **Win95Search** | Row of folder icons with a flashlight and beam sweeping left-to-right. File search animation. |
| **Win95Delete** | Large recycle bin with papers flying down into the lid and fading out. File deletion animation. |
| **Win95EmptyRecycle** | Large recycle bin with papers flying upward out of the lid and fading. Emptying recycle bin animation. |
| **Win95Defrag** | Block-grid defragmentation animation with square cells lighting in stepped groups. Inspired by classic disk optimization UIs. |
| **Win95Download** | Paper travels from a globe icon to a folder with a chunky download arrow. Classic web download motif. |
| **Win95Install** | Floppy disk slides into a drive while an activity LED blinks. Nostalgic installer/media loading state. |
| **Win95ScanDisk** | Retro ScanDisk panel with boxes filling from left to right. Useful for checking, validating, or scanning operations. |
| **Win95Hourglass** | Pixel-style hourglass cursor rotating in four discrete steps. Deliberately chunky for authentic retro timing. |
| **Win95DialUp** | Two computer icons exchange a moving signal dot while screens blink. Dial-up/network connection inspired. |
| **Win95Solitaire** | Cards cascade away from a starting stack, echoing the classic Solitaire win animation. |
| **Win95PrintQueue** | Printer outputs a page in visible step increments. Retro counterpart to PrinterOutput. |
| **Win95FindComputer** | Magnifying glass searches over a computer icon. Network computer search inspired. |
| **Win95Startup** | Four startup pane squares light up in sequence inside a simple window frame. |
| **Win95StartupColor** | Win95Startup with the classic Windows flag colors (red, green, blue, yellow) for the four panes. |

## Accessibility Support

DaisyLoading is designed to be accessible to users of assistive technologies like screen readers. The visual animation is decorative; the accessibility layer provides meaningful information.

### How It Works

1. **AutomationPeer**: The control exposes itself as a `ProgressBar` to assistive technologies via a custom `DaisyLoadingAutomationPeer`.

2. **Default Accessible Name**: By default, screen readers announce **"Loading"** when encountering this control.

3. **Customizable Text**: Use the `AccessibleText` property to provide context-specific messages.

### The AccessibleText Property

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `AccessibleText` | `string` | `"Loading"` | The text announced by screen readers when the control receives focus or is encountered. |

When you change `AccessibleText`, the control automatically updates its `AutomationProperties.Name` so screen readers pick up the new value.

### Accessibility Examples

```xml
<!-- Default: screen reader announces "Loading" -->
<controls:DaisyLoading Variant="Spinner" />

<!-- Contextual message: announces "Loading your profile data" -->
<controls:DaisyLoading Variant="Dots" AccessibleText="Loading your profile data" />

<!-- Action-specific: announces "Saving your changes" -->
<controls:DaisyLoading Variant="Ring" AccessibleText="Saving your changes" />

<!-- With full context: announces "Uploading file, please wait" -->
<controls:DaisyLoading Variant="Bars" AccessibleText="Uploading file, please wait" />

<!-- Processing state: announces "Processing payment" -->
<controls:DaisyLoading Variant="Pulse" AccessibleText="Processing payment" Color="Primary" Size="Large" />
```

### Best Practices for Accessible Loading States

1. **Be Specific**: Instead of generic "Loading", describe *what* is loading:
   - ✅ `"Loading search results"`
   - ✅ `"Fetching user data"`
   - ❌ `"Loading"` (too generic when context matters)

2. **Include Progress if Known**: If you know the progress, consider using `DaisyProgress` instead, or append progress info:
   - `"Uploading file (45% complete)"`

3. **Keep It Concise**: Screen readers read the text aloud, so keep messages short and clear:
   - ✅ `"Saving changes"`
   - ❌ `"Please wait while we save your changes to the database"`

4. **Match Visual Context**: If there's visible text near the loader (e.g., "Loading your dashboard..."), use the same text for `AccessibleText`.

5. **Update Dynamically**: If the loading state changes (e.g., from "Connecting" to "Downloading"), update `AccessibleText` accordingly in your ViewModel.

### Technical Implementation Details

The accessibility is implemented via:

```csharp
// Static constructor sets default accessible name
static DaisyLoading()
{
    AutomationProperties.NameProperty.OverrideDefaultValue<DaisyLoading>("Loading");
}

// Custom automation peer exposes control as ProgressBar
protected override AutomationPeer OnCreateAutomationPeer()
{
    return new DaisyLoadingAutomationPeer(this);
}

// DaisyLoadingAutomationPeer returns:
// - AutomationControlType.ProgressBar (so AT recognizes it as a progress indicator)
// - The AccessibleText value as the control's Name
// - IsContentElement = true, IsControlElement = true (so it's discoverable)
```

This ensures that:

- Screen readers identify the control as a **progress indicator** (not just a generic element)
- The accessible name is always available and customizable
- The control participates in the accessibility tree properly

## Theme File Organization

The DaisyLoading theme styles are organized into multiple files for maintainability:

| File | Contents |
| ---- | -------- |
| `Themes/DaisyLoading.axaml` | Main aggregator with design preview; includes all sub-files |
| `Themes/DaisyLoading/DaisyLoading.Base.axaml` | `ControlTheme` with default Spinner template, shared spinning animation, size styles, and color styles |
| `Themes/DaisyLoading/DaisyLoading.Classic.axaml` | Dots, Ring, Ball, Bars, Infinity variants |
| `Themes/DaisyLoading/DaisyLoading.Terminal.axaml` | Orbit, Snake, Pulse, Wave, Bounce variants |
| `Themes/DaisyLoading/DaisyLoading.Matrix.axaml` | Matrix, MatrixInward, MatrixOutward, MatrixVertical variants |
| `Themes/DaisyLoading/DaisyLoading.Dots.axaml` | MatrixRain, BitFlip, PacketBurst, CometTrail, RippleMatrix, CountdownSpinner variants (dot-based animations) |
| `Themes/DaisyLoading/DaisyLoading.Special.axaml` | Hourglass, SignalSweep, Heartbeat, TunnelZoom, GlitchReveal, CursorBlink variants (non-dot special effects) |
| `Themes/DaisyLoading/DaisyLoading.Business.axaml` | DocumentFlipOn, DocumentFlipOff, MailSend, CloudUpload, CloudDownload, DocumentStamp, DocumentReject, ChartPulse, CalendarTick, ApprovalFlow, BriefcaseSpin, BatteryCharging, BatteryEmptying, and TrafficLight variants (professional/enterprise animations) |
| `Themes/DaisyLoading/DaisyLoading.Business2.axaml` | PrinterOutput, PaperShredder, SignaturePen, DocumentScan, FolderSync, MailReceive, PhoneRing, CoinStack, InvoicePaid, PiggyBank, PieChartFill, TrendLine, ClockSpin, CoffeeCup variants (additional professional workflow animations) |
| `Themes/DaisyLoading/DaisyLoading.Win95.axaml` | Win95FileCopy, Win95Search, Win95Delete, Win95EmptyRecycle variants (retro Windows 95 file operation animations) |
| `Themes/DaisyLoading/DaisyLoading.Win95B.axaml` | Win95Defrag, Win95Download, Win95Install, Win95ScanDisk, Win95Hourglass, Win95DialUp, Win95Solitaire, Win95PrintQueue, Win95FindComputer, Win95Startup, Win95StartupColor variants (additional Windows 95-inspired animations) |

### Adding New Variants

To add a new loading variant:

1. Choose the appropriate category file (or create a new one if it doesn't fit existing categories)
2. Add a `Style Selector="controls|DaisyLoading[Variant=YourVariant]"` with a `ControlTemplate`
3. Add animation styles targeting template elements using the pattern `controls|DaisyLoading[Variant=YourVariant] /template/ ElementType.ClassName`
4. Add the new enum value to `DaisyLoadingVariant` in `Controls/DaisyLoading.cs`
5. Update the design preview in `DaisyLoading.axaml` to include the new variant

## Size Options

All variants scale proportionally across sizes. Canvas-based animations use Viewbox wrapping for smooth scaling.

| Size | Dimensions | Use Case |
| ---- | ---------- | -------- |
| ExtraSmall | 16×16px | Inline with text, compact buttons |
| Small | 20×20px | Small UI elements, table cells |
| Medium | 24×24px | Default, general purpose (recommended) |
| Large | 36×36px | Prominent loading states, cards |
| ExtraLarge | 48×48px | Full-page loading overlays, hero sections |

## Color Variants

Use the `Color` property to apply theme colors. All variants support coloring.

| Color | Description |
| ----- | ----------- |
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

<!-- Matrix/Colon-dot variants -->
<controls:DaisyLoading Variant="Matrix" Color="Accent" />
<controls:DaisyLoading Variant="MatrixInward" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="MatrixOutward" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="MatrixVertical" Color="Success" />

<!-- Special effect variants -->
<controls:DaisyLoading Variant="MatrixRain" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="Hourglass" Color="Warning" Size="ExtraLarge" />
<controls:DaisyLoading Variant="SignalSweep" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="BitFlip" Color="Primary" />
<controls:DaisyLoading Variant="PacketBurst" Color="Secondary" Size="Large" />
<controls:DaisyLoading Variant="CometTrail" Color="Accent" />
<controls:DaisyLoading Variant="Heartbeat" Color="Error" Size="Large" />
<controls:DaisyLoading Variant="TunnelZoom" Color="Info" Size="ExtraLarge" />
<controls:DaisyLoading Variant="GlitchReveal" Color="Success" />
<controls:DaisyLoading Variant="RippleMatrix" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="CursorBlink" Color="Success" />
<controls:DaisyLoading Variant="CountdownSpinner" Color="Warning" Size="Large" />

<!-- Business variants -->
<controls:DaisyLoading Variant="DocumentFlipOn" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="DocumentFlipOff" Color="Secondary" Size="Large" />
<controls:DaisyLoading Variant="MailSend" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="CloudUpload" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="CloudDownload" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="DocumentStamp" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="DocumentReject" Color="Error" Size="Large" />
<controls:DaisyLoading Variant="ChartPulse" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="CalendarTick" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="ApprovalFlow" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="BriefcaseSpin" Color="Neutral" Size="Large" />
<controls:DaisyLoading Variant="PrinterOutput" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="PaperShredder" Color="Error" Size="Large" />
<controls:DaisyLoading Variant="SignaturePen" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="DocumentScan" Color="Accent" Size="Large" />
<controls:DaisyLoading Variant="FolderSync" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="MailReceive" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="PhoneRing" Color="Warning" Size="Large" />
<controls:DaisyLoading Variant="CoinStack" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="InvoicePaid" Color="Success" Size="Large" />
<controls:DaisyLoading Variant="PiggyBank" Color="Accent" Size="Large" />
<controls:DaisyLoading Variant="PieChartFill" Color="Info" Size="Large" />
<controls:DaisyLoading Variant="TrendLine" Color="Primary" Size="Large" />
<controls:DaisyLoading Variant="ClockSpin" Color="Warning" Size="Large" />
<controls:DaisyLoading Variant="CoffeeCup" Color="Neutral" Size="Large" />

<!-- Windows 95 retro variants -->
<controls:DaisyLoading Variant="Win95FileCopy" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Search" Color="Info" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Delete" Color="Error" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95EmptyRecycle" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Defrag" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Download" Color="Info" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Install" Color="Neutral" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95ScanDisk" Color="Success" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Hourglass" Color="Warning" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95DialUp" Color="Info" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Solitaire" Color="Accent" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95PrintQueue" Color="Neutral" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95FindComputer" Color="Info" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95Startup" Color="Primary" Size="ExtraLarge" />
<controls:DaisyLoading Variant="Win95StartupColor" Size="ExtraLarge" />

<!-- With accessibility -->
<controls:DaisyLoading Variant="Spinner" AccessibleText="Loading dashboard" />
```

## Animation Authoring Notes

- `RenderTransformOrigin` in AXAML uses Avalonia point syntax, not WPF numeric shorthand. Use `RenderTransformOrigin="50%,50%"` for an explicit center pivot, or omit the property to keep Avalonia's default center origin.
- Do not use `RenderTransformOrigin="0.5,0.5"` in AXAML for centered loading rotations. That can pivot near the top-left corner; `ClockSpin` keeps its hand canvases centered by relying on Avalonia's default center origin.
- In C# helper code, `new RelativePoint(0.5, 0.5, RelativeUnit.Relative)` is still the correct centered relative origin.

## Animation Timing Reference

| Variant | Duration | Notes |
| ------- | -------- | ----- |
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
| Matrix | 1.8s | Smooth wave left-to-right with overlap |
| MatrixInward | 1.2s | Center-to-edges wave |
| MatrixOutward | 1.2s | Edges-to-center wave |
| MatrixVertical | 1.0s | Top-to-bottom blink |
| MatrixRain | 0.7-1.1s | Variable speeds per column |
| Hourglass | 2.0s | Full sand flow cycle |
| SignalSweep | 1.2s | Left-to-right sweep with trail |
| BitFlip | 1.6s | Pseudo-random binary patterns |
| PacketBurst | 1.2s | Center pulse with 4-direction burst |
| CometTrail | 1.5s | Full circular orbit with 0.1s trail delays |
| Heartbeat | 1.5s | Horizontal scroll of EKG pattern |
| TunnelZoom | 1.5s | Ring expansion with 0.5s stagger |
| GlitchReveal | 2.0s | Random column flash patterns |
| RippleMatrix | 1.2s | Center-outward ripple wave |
| CursorBlink | 1.0s | 50% on, 50% off blink cycle |
| CountdownSpinner | 1.2s | Sequential 12-position lighting |
| DocumentFlipOn | 1.5s | Page slides in from upper-left |
| DocumentFlipOff | 1.5s | Page slides out to upper-right with fade |
| MailSend | 2.0s | Paper slides down into envelope |
| CloudUpload | 1.4s | Arrow rises with opacity pulse |
| CloudDownload | 1.4s | Arrow falls with opacity pulse |
| DocumentStamp | 2.0s | Stamp scales down with bounce |
| DocumentReject | 2.0s | X stamp scales down with bounce |
| ChartPulse | 1.2s | Bars pulse with 0.15s stagger, 2.4s sweep |
| CalendarTick | 2.0s | Checkmark pops in with bounce |
| ApprovalFlow | 2.4s | Sequential node highlighting (0.8s per node) |
| BriefcaseSpin | 1.2s | Wobble rotation with bounce |
| BatteryCharging | Varies | Battery segments fill from empty to full |
| BatteryEmptying | Varies | Battery segments drain from full to empty |
| TrafficLightUp | Varies | Directional traffic-light cycle upward |
| TrafficLightRight | Varies | Directional traffic-light cycle to the right |
| TrafficLightDown | Varies | Directional traffic-light cycle downward |
| TrafficLightLeft | Varies | Directional traffic-light cycle to the left |
| PrinterOutput | 1.6s | Page feeds out from printer with fade-out reset |
| PaperShredder | 1.7s | Page enters shredder while strips fall below |
| SignaturePen | 1.8s | Pen motion with staged signature stroke reveal |
| DocumentScan | 1.6s | Scan beam moves down document and brightens lines |
| FolderSync | 1.6s | Document transfers between folders while sync arrows pulse |
| MailReceive | 1.6s | Document rises from opened envelope |
| PhoneRing | 0.8-1.2s | Handset shakes quickly while ring waves pulse |
| CoinStack | 1.4s | Coin drops and stack levels brighten |
| InvoicePaid | 1.5s | PAID stamp scales down and fades |
| PiggyBank | 1.5s | Coin drops into piggy bank with wobble |
| PieChartFill | 1.6s | Pie segments brighten sequentially |
| TrendLine | 1.5s | Trend segments appear from left to right |
| ClockSpin | 1.2s / 3.6s | Minute and hour hands rotate at different speeds |
| CoffeeCup | 1.5s | Steam trails rise with staggered delays |
| Win95FileCopy | 1.6s | Papers fly with arc motion, 0.4s stagger |
| Win95Search | 2.0s | Flashlight sweeps left-to-right |
| Win95Delete | 1.6s | Papers fly down into bin, 0.2s stagger |
| Win95EmptyRecycle | 1.6s | Papers fly up from bin, 0.2s stagger |
| Win95Defrag | 1.4s | Block groups light in stepped sequence |
| Win95Download | 1.5s | Paper moves from globe to folder |
| Win95Install | 1.6s | Floppy slides into drive while LED blinks |
| Win95ScanDisk | 1.2s | Scan boxes light left-to-right |
| Win95Hourglass | 1.6s | Hourglass rotates around its center |
| Win95DialUp | 1.4s | Signal dot moves between computers |
| Win95Solitaire | 1.6s | Cards cascade with staggered delays |
| Win95PrintQueue | 1.4s | Printer page advances in visible steps |
| Win95FindComputer | 1.8s | Magnifying glass scans over computer icon |
| Win95Startup | 1.2s | Four startup panes light in sequence |
| Win95StartupColor | 1.2s | Four Windows-colored panes light in sequence |

## Property Summary

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `Variant` | `DaisyLoadingVariant` | `Spinner` | Animation style (72 options) |
| `Size` | `DaisySize` | `Medium` | Control dimensions (5 options). Uses shared enum. |
| `Color` | `DaisyColor` | `Default` | Theme color (9 options). Uses shared enum. |
| `AccessibleText` | `string` | `"Loading"` | Screen reader announcement |

## Variant Selection Guide

| Use Case | Recommended Variants |
| -------- | ------------------- |
| General purpose | `Spinner`, `Ring`, `Dots` |
| Form submission | `Spinner`, `Pulse`, `Hourglass`, `DocumentStamp` |
| Data fetching | `Dots`, `Wave`, `Matrix`, `RippleMatrix` |
| File upload/download | `Bars`, `MatrixRain`, `Hourglass`, `SignalSweep`, `CloudUpload`, `CloudDownload`, `Win95Download` |
| File operations | `Win95FileCopy`, `Win95Search`, `Win95Delete`, `Win95EmptyRecycle`, `Win95Defrag`, `Win95ScanDisk` |
| Connection/sync | `Orbit`, `Pulse`, `PacketBurst`, `FolderSync`, `Win95DialUp` |
| Terminal/developer UI | `Snake`, `Matrix`, `MatrixRain`, `CursorBlink`, `BitFlip`, `GlitchReveal` |
| Gaming/entertainment | `Bounce`, `MatrixRain`, `CometTrail`, `TunnelZoom` |
| Retro/nostalgic | `Hourglass`, `Matrix`, `Infinity`, `CursorBlink`, `Win95FileCopy`, `Win95Search`, `Win95Delete`, `Win95EmptyRecycle`, `Win95Defrag`, `Win95Install`, `Win95Hourglass`, `Win95DialUp`, `Win95Solitaire`, `Win95Startup`, `Win95StartupColor` |
| Health/medical UI | `Heartbeat`, `Pulse` |
| Sci-fi/futuristic | `TunnelZoom`, `SignalSweep`, `PacketBurst`, `GlitchReveal` |
| Time-based operations | `Hourglass`, `CountdownSpinner`, `CalendarTick`, `ClockSpin`, `CoffeeCup` |
| Business/enterprise | `DocumentFlipOn`, `DocumentFlipOff`, `DocumentStamp`, `DocumentReject`, `BriefcaseSpin`, `ApprovalFlow`, `PrinterOutput`, `InvoicePaid` |
| Email/messaging | `MailSend`, `MailReceive`, `PhoneRing`, `Dots`, `Spinner` |
| Analytics/reporting | `ChartPulse`, `PieChartFill`, `TrendLine`, `Bars`, `Wave` |
| Approval workflows | `DocumentStamp`, `DocumentReject`, `ApprovalFlow`, `CalendarTick`, `SignaturePen` |
| Cloud operations | `CloudUpload`, `CloudDownload`, `PacketBurst`, `FolderSync` |
| Document processing | `DocumentFlipOn`, `DocumentFlipOff`, `DocumentStamp`, `DocumentReject`, `DocumentScan`, `PaperShredder`, `SignaturePen`, `PrinterOutput` |
| Finance/billing | `CoinStack`, `InvoicePaid`, `PiggyBank` |
| Scheduling/calendar | `CalendarTick`, `CountdownSpinner`, `Hourglass`, `ClockSpin` |
