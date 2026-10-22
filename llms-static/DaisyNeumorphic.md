# Overview

`DaisyNeumorphic` adds neumorphic (soft UI) effects to Daisy controls: surfaces look raised from, or pressed into, the background. A top-left light source is assumed, so a light shadow falls on one side of the surface and a dark shadow on the opposite side.

The effect is drawn with Avalonia's `Border.BoxShadow`, which supports several shadows and inner (`inset`) shadows on every platform. No platform-specific code is involved.

## Modes

| Mode | Shadows |
| ---- | ------- |
| `None` | No effect. |
| `Raised` | Light shadow top-left, dark shadow bottom-right, outside the surface. |
| `Inset` | The same pair as inner shadows: the surface looks pressed in. |
| `Flat` | A single, smaller dark shadow. |

## Global Mode

Registered controls follow the global setting unless they set their own value.

```csharp
DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;   // also sets GlobalEnabled = true
DaisyNeumorphic.GlobalIntensity = 0.25;                     // 0.0 - 1.0, default 0.45

DaisyNeumorphic.GlobalEnabled = false;  // keeps the mode
DaisyNeumorphic.GlobalEnabled = true;   // restores the last mode (Raised by default)
```

`GlobalMode` and `GlobalEnabled` stay in sync: any mode other than `None` enables the effect, `None` disables it. `GlobalChanged` fires after every global change.

The Gallery and `FloweryComponentSidebar` offer a selector for the global mode via `SidebarNeumorphicSelectorItem` (bound to `FloweryComponentSidebar.NeumorphicMode`). The Gallery's *Effects > Neumorphic Effect* page shows the registered controls side by side with their own mode and intensity selector.

## Per-Control Settings

| Attached property | Type | Default | Description |
| ----------------- | ---- | ------- | ----------- |
| `IsEnabled` | `bool?` | `null` | Enables or disables the effect for this control. `null` uses `GlobalEnabled`. |
| `Mode` | `DaisyNeumorphicMode?` | `null` | Mode for this control. `null` uses `GlobalMode`. |
| `Intensity` | `double?` | `null` | Shadow strength. `null` uses `GlobalIntensity`. |
| `Offset` | `double` | `4` | Minimum shadow offset. An explicit value replaces the size-derived offset. |
| `BlurRadius` | `double` | `4` | Minimum blur. An explicit value replaces the size-derived blur. |
| `DarkShadowColor` | `Color?` | `null` | Overrides the theme-derived dark shadow color. |
| `LightShadowColor` | `Color?` | `null` | Overrides the light shadow color (white). |
| `ScopeEnabled` | `bool?` | `null` | Enables or disables the effect for a whole subtree. The nearest value wins over `IsEnabled`. |
| `ScopeMode` | `DaisyNeumorphicMode?` | `null` | Mode for a whole subtree; a control's own `Mode` still wins. |
| `ScopeIntensity` | `double?` | `null` | Intensity for a whole subtree; a control's own `Intensity` still wins. |

```xml
<controls:DaisyCard controls:DaisyNeumorphic.Mode="Inset"
                    controls:DaisyNeumorphic.Intensity="0.7">
    <TextBlock Text="Pressed-in card" />
</controls:DaisyCard>

<!-- Keep a whole area flat -->
<StackPanel controls:DaisyNeumorphic.ScopeEnabled="False">
    ...
</StackPanel>

<!-- Draw a whole area inset, whatever the global mode -->
<StackPanel controls:DaisyNeumorphic.ScopeEnabled="True"
            controls:DaisyNeumorphic.ScopeMode="Inset"
            controls:DaisyNeumorphic.ScopeIntensity="0.6">
    ...
</StackPanel>
```

`IsEnabled="True"` alone draws nothing while the global mode is `None`; set `Mode` as well to show the effect independently.

### Precedence

The effect is decided in this order; the first step with a value wins:

1. Not registered, excluded (rows, cells), inside a segment container (ButtonGroup, Join, Pagination), or high contrast: no effect. These rules are structural; nothing overrides them.
2. `ScopeEnabled` on the control or its nearest ancestor that has a value.
3. The control's own `IsEnabled`.
4. `GlobalEnabled` (ignored by controls that are off by default, such as ChatBubble).

A scope therefore beats an explicit `IsEnabled="False"` inside it, but never raises rows, cells or segments. To keep one control flat inside an enabled scope, set `ScopeEnabled="False"` on that control.

The mode is the control's own `Mode`, then the nearest `ScopeMode`, then `GlobalMode`; the intensity follows the same order with `Intensity`, `ScopeIntensity` and `GlobalIntensity`.

## Shadow Size and Colors

The offset and blur grow with the control's `Size` (`DaisyResourceLookup.GetDefaultElevation`): ExtraSmall 10, Small 11, Medium 12, Large 14, ExtraLarge 16. The offset is 35% of the elevation and the blur 1.5 times the offset; `Flat` uses 60% of both. Shadow alpha is the color's alpha multiplied by the intensity.

Colors are resolved per control: its own `DarkShadowColor`/`LightShadowColor`, then `GlobalDarkShadowColor`/`GlobalLightShadowColor`, then the theme. `GetEffectiveDarkShadowColor(control)` and `GetEffectiveLightShadowColor(control)` return the result.

The global colors are `null` by default. Then the dark shadow is derived from the current theme's `DaisyBase300Color` (darkened by 50% for light themes, 70% for dark themes) and the light shadow is white. A global color set by the app stays in place when the theme changes; set it back to `null` to follow the theme again.

High contrast (`DaisyAccessibility.HighContrastEnabled`) turns the effect off.

## Which Controls Draw the Effect

Only controls with a visible surface draw the effect; the surface is the template border that paints the control's background.

- **Surfaces:** Card, PatternedCard, ExpandableCard, Alert, AccordionItem, Collapse, Hero, Mockup, Modal, Navbar, Dock, WeatherCard, WeatherMetrics, Button (not Ghost or Link), Badge, Avatar, Countdown (when it has a background), NumberFlow (with `ShowControls`), Kbd, ModifierKeys keys, Input, TextArea, MaskInput, PasswordBox, NumericUpDown, FileInput, TagPicker, Select, SizeDropdown, ThemeDropdown, ProductThemeDropdown, CalendarDatePicker, Dropdown trigger, Range track, Progress, Skeleton, DateTimeline items, boxed Tabs header, boxed Timeline items, WeatherForecast day tiles (`DaisyWeatherForecastItem`).
- **SlideToConfirm:** the track is always inset and the handle always raised.
- **Toggle, CheckBox, Radio:** the track and boxes are always inset (input wells); the toggle knob is always raised.
- **ChatBubble:** off by default; set `IsEnabled="True"` to draw it.
- **No effect:** controls without a surface (spinners, text, icons, custom-rendered color pickers, layout containers), and `DaisyFab` (its action buttons draw the effect themselves).

Card, PatternedCard, Navbar, WeatherCard and DaisyStats remove their own drop shadow `Effect` while the neumorphic effect is drawn, so the two shadows do not stack.

### Composite Controls

Composite controls draw one surface for the whole control:

- **DaisyTable** is one raised surface, like a card. `DaisyTableRow`, `DaisyTableCell` and `DaisyTableHeaderCell` are excluded, so no box appears around a row or cell and a `*` column does not stretch a shadow across the table.
- **DaisyList** is one surface; `DaisyListRow` and `DaisyListColumn` are excluded.
- **DaisyStats** is one surface.
- **DaisyButtonGroup** draws one surface around its segments.
- **DaisyButtonGroup, DaisyJoin and DaisyPagination** keep their children flat.

`FloweryComponentSidebar` and the FlowKanban board set `ScopeEnabled="False"`.

## For Control Authors

Flowery.NET has no common base class, so a control takes part by registering its surface once, for example in its static constructor:

```csharp
static MyPanel()
{
    DaisyNeumorphic.RegisterSurface<MyPanel>(c => c.SurfaceBorder);
}
```

For anything beyond a plain surface, pass a `DaisyNeumorphicSurface<T>`:

```csharp
DaisyNeumorphic.RegisterSurface(new DaisyNeumorphicSurface<MyButton>
{
    Resolve = c => c.SurfaceBorder,
    IsActive = c => c.Variant != MyVariant.Ghost   // transparent variants draw nothing
});
DaisyNeumorphic.RegisterRefreshTriggers<MyButton>(MyButton.VariantProperty);   // re-apply when IsActive may change
```

| Option | Description |
| ------ | ----------- |
| `Resolve` | Returns the surface `Border`, or `null` while it does not exist yet. A missing surface is retried once after the next layout pass. |
| `OuterShadowLayer` | Unclipped border behind a clipping surface for Raised and Flat shadows (see Clipping). |
| `InsetOverlay` | Border on top of the content for Inset shadows (see Clipping). |
| `FixedMode` | Pins the surface to one mode. |
| `IsActive` | Condition on the control; the surface draws nothing while it returns false. |
| `RequiresBackground` | Draw only while the surface border paints a visible background; background changes of the border re-apply the effect. |

- Call `RegisterSurface` more than once for several surfaces. Surfaces registered for a derived type replace those of its base types; all other registrations are inherited.
- `RegisterRefreshTriggers<T>(properties)` re-applies the effect when these control properties change. The control's `DaisySize` `Size` property is always included.
- `RegisterExclusion<T>()` keeps a type flat even when a base type is registered, regardless of the order of the calls.
- `RegisterSegmentContainer<T>()` keeps registered child controls flat; a scope cannot override this.
- `RegisterShadowPassThrough<T>()` lets the shadows of registered child controls through a container that clips by default (see Clipping).
- `RegisterEffectSuppression<T>()` removes the control's own `Effect` (e.g. a drop shadow) while the effect is drawn.
- `RegisterDisabledByDefault<T>()` makes the control ignore `GlobalEnabled`; it draws only when enabled on itself or by a scope.
- `DaisyNeumorphic.Refresh(control)` re-applies the effect after a control replaced its surface in code.
- `IsEffectivelyEnabled(control)` and `IsRegistered(type)` report the current state.

### Clipping and Border Thickness

Avalonia draws a border with uneven `BorderThickness` (for example the key cap look `1,1,1,2` of `DaisyKbd`) as geometry, and that path draws no `BoxShadow`. Give such surfaces an `OuterShadowLayer` and an `InsetOverlay` with an even (or no) border thickness, as `DaisyKbd` and `DaisyModifierKeys` do. An `Ellipse` has no `BoxShadow` either; `DaisyToggle` draws its knob shadow on a round border that follows the knob.

A border's `ClipToBounds` also cuts off its own outer shadow. Avalonia 12 templated controls clip to their bounds by default. While the effect is drawn, that default clip is lifted on the elements between the shadow border and the control. Clips that a theme, style, template or code set explicitly stay in place, and elements outside the control are not touched (a surface that is not inside the control, such as popup content, keeps all clips around it).

A container whose registered children sit close to its edges can let their shadows through with `RegisterShadowPassThrough<T>()`. While a child draws the effect, the default clips from the child up to the container are lifted as well; each child holds its own lift, so the container clips again only after the last child turns the effect off. `DaisyWeatherForecast` uses this for its day tiles and keeps clipping its content otherwise.

When the surface has to clip its content (rounded tables and lists, progress tracks, avatar masks), add an unclipped layer behind it and pass it as `OuterShadowLayer`. Raised and Flat shadows go to that layer; Inset shadows stay on the surface:

```xml
<Panel>
    <Border x:Name="PART_NeumorphicShadow" CornerRadius="{TemplateBinding CornerRadius}" />
    <Border x:Name="PART_Surface" ClipToBounds="True" ...>
        <ItemsPresenter />
    </Border>
</Panel>
```

When the content paints opaque backgrounds over the surface (table rows), pass an overlay on top as `InsetOverlay` (`IsHitTestVisible="False"`), so Inset shadows stay visible.

Shadows are applied with animation priority and removed again when the effect turns off, so styles and template bindings for `BoxShadow`, `Effect` and `ClipToBounds` come back unchanged.

## Comparison with Flowery.Uno

| Topic | Flowery.Uno | Flowery.NET |
| ----- | ----------- | ----------- |
| Opt-in surface | `DaisyBaseContentControl.GetNeumorphicHostElement()` | `DaisyNeumorphic.RegisterSurface<T>()` |
| Global settings | `DaisyBaseContentControl.GlobalNeumorphic*` | `DaisyNeumorphic.Global*` (shadow colors are `Color?`; `null` follows the theme) |
| Subtree scope | `DaisyBaseContentControl.NeumorphicScopeEnabled` | `DaisyNeumorphic.ScopeEnabled`, plus `ScopeMode` and `ScopeIntensity` |
| Segment children | Containers set `IsEnabled=false` on children; a scope can re-enable them | Structural rule; a scope cannot re-enable segments |
| Rendering | Composition/elevation per platform, gradient overlays for Inset | `Border.BoxShadow` on all platforms |
