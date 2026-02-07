<!-- Supplementary documentation for DaisyDivider -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyDivider separates content with a line that can run horizontally (default) or vertically (`Horizontal=True`). It supports **10 visual styles**, **9 color options**, **4 ornament shapes**, **size variants**, adjustable thickness, **start/end placement** to hide one side of the line, optional inline content, and adjustable margins.

## Visual Styles

| Style | Description |
| ----- | ----------- |
| `Solid` (default) | Simple single solid line. |
| `Dashed` | Dashed line pattern (- - - -). |
| `Dotted` | Dotted line pattern (• • • •). |
| `Inset` | 3D embossed dual-line effect (groove/ridge). |
| `Double` | Two parallel lines with gap. |
| `Gradient` | Fades from edges toward center (transparent at edges, solid in center). |
| `Ornament` | Decorative geometric shape in center (Diamond, Circle, Star, Square). |
| `Glow` | Animated pulsing opacity effect. |
| `Wave` | Wavy/curved line pattern. |
| `Tapered` | Thick center tapering to thin points at ends. |

## Ornament Shapes

When using `DividerStyle="Ornament"`, the `Ornament` property controls the center shape:

| Ornament | Description |
| -------- | ----------- |
| `Diamond` (default) | Diamond shape ◆ |
| `Circle` | Circle shape ● |
| `Star` | 4-point star shape ✦ |
| `Square` | Square shape ■ |

## Orientation & Placement

| Option | Description |
| ------ | ----------- |
| `Horizontal=False` (default) | Renders a horizontal rule (line left/right). |
| `Horizontal=True` | Renders a vertical rule (line above/below). |
| `Placement=Start` | Hides the line before the content (useful for headings). |
| `Placement=End` | Hides the line after the content. |

## Color Options

| Color | Description |
| ----- | ----------- |
| Default | Subtle base-content line (10% opacity). |
| Neutral / Primary / Secondary / Accent / Success / Warning / Info / Error | Solid colored line matching the Daisy palette. |

## Size Variants

| Size | Description |
| ---- | ----------- |
| `ExtraSmall` | Compact spacing and 10pt font. |
| `Small` (default) | Standard spacing and 12pt font. |
| `Medium` | Moderate spacing and 14pt font. |
| `Large` | Generous spacing and 16pt font. |
| `ExtraLarge` | Maximum spacing and 18pt font. |

## Layout

| Property | Description |
| -------- | ----------- |
| `DividerMargin` | Spacing around the line/content (default `0,4`). |
| `LineThickness` | Thickness of the divider line in pixels (default `2`). |
| `TextBackground` | Background brush for text container (matches parent when set). |
| `Content` | Inline text/element placed between (or beside in vertical mode) the line segments. Hidden automatically when empty. |

## Quick Examples

```xml
<!-- Basic divider with text -->
<controls:DaisyDivider>OR</controls:DaisyDivider>

<!-- Different styles -->
<controls:DaisyDivider DividerStyle="Dashed" Color="Primary" />
<controls:DaisyDivider DividerStyle="Dotted" Color="Secondary" />
<controls:DaisyDivider DividerStyle="Inset" LineThickness="2" />
<controls:DaisyDivider DividerStyle="Double" Color="Accent" LineThickness="2" />
<controls:DaisyDivider DividerStyle="Gradient" Color="Primary" LineThickness="3" />
<controls:DaisyDivider DividerStyle="Glow" Color="Success" />

<!-- Ornament styles -->
<controls:DaisyDivider DividerStyle="Ornament" Ornament="Diamond" Color="Primary" />
<controls:DaisyDivider DividerStyle="Ornament" Ornament="Circle" Color="Secondary" />
<controls:DaisyDivider DividerStyle="Ornament" Ornament="Star" Color="Accent" />
<controls:DaisyDivider DividerStyle="Ornament" Ornament="Square" Color="Warning" />

<!-- Wave and Tapered styles -->
<controls:DaisyDivider DividerStyle="Wave" Color="Info" LineThickness="2" />
<controls:DaisyDivider DividerStyle="Tapered" Color="Error" LineThickness="4" />

<!-- Size variants with ornaments -->
<controls:DaisyDivider Size="Small" DividerStyle="Ornament" Ornament="Diamond" Color="Primary" />
<controls:DaisyDivider Size="Large" DividerStyle="Ornament" Ornament="Star" Color="Accent" />
<controls:DaisyDivider Size="ExtraLarge" DividerStyle="Ornament" Ornament="Circle" Color="Success" />

<!-- Vertical divider between items -->
<StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Spacing="16">
    <TextBlock Text="Left" VerticalAlignment="Center" />
    <controls:DaisyDivider Horizontal="True" Height="60">OR</controls:DaisyDivider>
    <TextBlock Text="Right" VerticalAlignment="Center" />
</StackPanel>

<!-- Colored dividers -->
<controls:DaisyDivider Color="Primary">Primary</controls:DaisyDivider>
<controls:DaisyDivider Color="Success">Success</controls:DaisyDivider>
<controls:DaisyDivider Color="Error">Error</controls:DaisyDivider>

<!-- Placements -->
<controls:DaisyDivider Placement="Start">Start</controls:DaisyDivider>
<controls:DaisyDivider>Default</controls:DaisyDivider>
<controls:DaisyDivider Placement="End">End</controls:DaisyDivider>

<!-- Size variants -->
<controls:DaisyDivider Size="ExtraSmall">XS</controls:DaisyDivider>
<controls:DaisyDivider Size="Medium">Medium</controls:DaisyDivider>
<controls:DaisyDivider Size="ExtraLarge">XL</controls:DaisyDivider>

<!-- Custom thickness -->
<controls:DaisyDivider LineThickness="1" />
<controls:DaisyDivider LineThickness="4" Color="Primary" />
<controls:DaisyDivider LineThickness="8" Color="Accent" />

<!-- Custom margin -->
<controls:DaisyDivider DividerMargin="0,16" Color="Accent">More breathing room</controls:DaisyDivider>
```

## Tips & Best Practices

- Use `Horizontal=True` for separating items in toolbars or inline menus.
- Apply `Placement=Start` to align section titles with a trailing rule without a leading line.
- Increase `DividerMargin` when separating large blocks; reduce it for dense lists.
- If the divider appears too faint on dark backgrounds, pick a color variant instead of the default.
- Use `Glow` style sparingly - it's an animated pulsing effect best for highlighting important sections.
- Use `Ornament` styles to add elegant visual breaks in content-heavy layouts.
- Use the `Size` property with `Ornament` styles to scale the decorative shape appropriately.
- Match `TextBackground` to the parent container's background when using dividers inside cards.
