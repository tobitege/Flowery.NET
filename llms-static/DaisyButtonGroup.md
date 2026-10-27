<!-- Supplementary documentation for DaisyButtonGroup -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

`DaisyButtonGroup` arranges multiple segments (typically `Button` / `ToggleButton`) into a single joined control with shared borders and unified rounding, similar to shadcn's “Button Group”.

It also supports non-clickable segments (for counters/labels) by using a `Border` as an item inside the group.

`DaisyButtonGroup` does not provide any icons/content by default; icons (if any) should be supplied via XAML content or set in code-behind.

## Properties & Events

| Member | Description |
|-- |-- |
| `Variant` (`DaisyButtonVariant`) | Applies a unified color variant to all segments (Default, Primary, Secondary, Accent, etc.). |
| `Size` (`DaisySize`) | Applies size tokens to all segments (height, padding, font size). |
| `ButtonStyle` (`DaisyButtonStyle`) | Segment style: `Default`, `Outline`, `Soft`, `Dash` (outer dashed border). |
| `Shape` (`DaisyButtonGroupShape`) | Corner rounding: `Default`, `Square`, `Rounded`, `Pill`. |
| `Orientation` (`Orientation`) | Layout direction (`Horizontal` or `Vertical`). |
| `AutoSelect` (`bool`) | When `True`, clicking a button segment applies the `button-group-active` class to it and removes it from sibling buttons, and the group works as a segmented control (keyboard and screen readers below). |
| `SelectionMode` | `Single` (default) or `Multiple`. |
| `SelectedIndex` (`int`) | The selected segment in single selection, `-1` for none. Binds two-way by default; a click keeps a binding on it. |
| `SelectedIndices` | The selected segments in multiple selection, an `ObservableCollection<int>`. Settable and bindable two-way, so a view model can bind its own collection. Switching to `Single` leaves it as it is, so the multiple selection comes back with the mode. |
| `ShowShadow` (`bool`) | When `True`, renders a subtle shadow around the group. |
| `ItemSelected` event | Raised when a button segment is clicked, also on the selected segment; provides the clicked `Control`. |
| `SelectionChanged` event | Raised whenever the selection changes, by a click, the keyboard or code, with `AddedIndices` and `RemovedIndices`; in single selection at most one each. |
| `SelectedIndicesChanged` event | Raised when the collection of selected indices changes (multiple selection). |

## Quick Examples

```xml
<!-- Download button with non-clickable counter -->
<controls:DaisyButtonGroup ShowShadow="True">
  <Button Content="Download" />
  <Border>
    <TextBlock Text="15k" />
  </Border>
</controls:DaisyButtonGroup>

<!-- Pill-shaped primary icon group -->
<controls:DaisyButtonGroup Variant="Primary" Shape="Pill" ShowShadow="True">
  <Button Padding="0" Width="40">
    <PathIcon Data="{StaticResource DaisyIconChevronLeft}" Width="18" Height="18" />
  </Button>
  <Button Padding="0" Width="40">
    <PathIcon Data="M8 5v14l11-7z" Width="18" Height="18" />
  </Button>
  <Button Padding="0" Width="40">
    <PathIcon Data="{StaticResource DaisyIconChevronRight}" Width="18" Height="18" />
  </Button>
</controls:DaisyButtonGroup>
```

## Tips & Best Practices

- Use `Border` items for label/counter segments so the group can apply borders and padding consistently.
- For icon-only segments, set `Padding="0"` and a fixed `Width` (e.g. `40`) to get square buttons.
- If you enable `AutoSelect`, style `.button-group-active` for your segment buttons if you want a stronger visual “selected” state.

## Neumorphic Effect

With [DaisyNeumorphic](DaisyNeumorphic.md) active, the group draws one surface around its segments (on the unclipped `PART_NeumorphicShadow` layer behind `PART_Background`), and the segment buttons stay flat. Like `ShowShadow`, the surface spans the group's full size, so size the group to its content when the effect is used.

## Segmented Control

With `AutoSelect` the group is one tab stop: Tab enters it at the selected segment. In single selection the arrow keys, Home and End move the selection; in multiple selection they move the focus and Space toggles the focused segment. Screen readers hear the group as a selection, the selected segment in single selection, and each `DaisyButton` segment as on or off in multiple selection, like toggle buttons.

When the template has a `Border` named `PART_Indicator`, the group slides it under the selected segment in single selection, with a spring and a slight squash while it moves, and without motion when reduced motion is on. In multiple selection the indicator hides, and the segments mark themselves through `button-group-active`.

## Glass

`Theme="{StaticResource DaisyGlassButtonGroupTheme}"` turns the group into a segmented control on a glass track: the segments share its width, a pill slides to the selected segment, and in multiple selection each selected segment gets its own pill that fades in and out. The theme selects (`AutoSelect`); set or bind `SelectedIndex` for the first selection, since a value from the theme would come back over a clicked selection. The group gets its look from styles, so the glass look follows `controls:DaisyGlassSurface.IsEnabled`, which the theme sets together with the glass settings; see [DaisyGlassSurface](DaisyGlassSurface.md).

```xml
<controls:DaisyButtonGroup Theme="{StaticResource DaisyGlassButtonGroupTheme}" SelectedIndex="{Binding Range}" Foreground="White">
    <controls:DaisyButton Content="Day" />
    <controls:DaisyButton Content="Week" />
    <controls:DaisyButton Content="Month" />
</controls:DaisyButtonGroup>
```
