<!-- Supplementary documentation for DaisySlideToConfirm -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisySlideToConfirm asks the user to drag a handle to the end of a track before an action runs, for actions that should not happen by accident, such as power off, delete or pay. While the handle moves, the track blends from `DaisyBase300Brush` to `SlideColor`, and the label turns from the base text color to black or white, whichever reads better on the variant color, and from 70 % to full opacity. At the end the label shows `ConfirmingText` and the control raises `SlideCompleted`. A handle let go before the end jumps back to the start.

## Behaviour

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Text` | `string` | `"SLIDE TO CONFIRM"` | Label on the track. |
| `ConfirmingText` | `string` | `"CONFIRMING..."` | Label after the slide is completed, until the reset. |
| `AutoReset` | `bool` | `false` | Resets the control `ResetDelay` after the completion. Without it, the control stays completed until `Reset()` is called. |
| `ResetDelay` | `TimeSpan` | `0:0:0.9` | Delay of the automatic reset. |

The default texts are English and not translated; set `Text` and `ConfirmingText` for other languages.

- `SlideCompleted` (`EventHandler`) is raised once per completion. A completed control ignores drags, keys and UI Automation until it is reset.
- `Reset()` moves the handle back to the start, shows `Text` again and cancels a pending automatic reset. Removing the control from the visual tree cancels it too.

### Keyboard and UI Automation

The control is focusable. Space and Enter complete it (`OnKeyDown`): the handle moves to the end, the label shows `ConfirmingText` and `SlideCompleted` is raised, as after a drag. UI Automation exposes the control as a button whose Invoke pattern does the same. A disabled control ignores the keys, and Invoke throws an `InvalidOperationException`.

## Look

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Variant` | `DaisySlideToConfirmVariant` | `Primary` | Color of the track border, the icon, the handle tint and the default `SlideColor`: `Default`, `Primary`, `Secondary`, `Accent`, `Info`, `Success`, `Warning` or `Error`. `Default` looks like `Primary`. |
| `DepthStyle` | `DaisyDepthStyle` | `Flat` | `Flat` draws no shadow, `ThreeDimensional` an inset shadow in the track and a small shadow under the handle, `Raised` deeper shadows of both. |
| `Size` | `DaisySize` | `Medium` | Track height, handle, icon and font size (see Computed Sizes). Without an explicit value it follows the global size of [FlowerySizeManager](FlowerySizeManager.md). |
| `SlideColor` | `Color` | variant color | Color the track blends to while the handle moves. While it is unset (`#00000000`), the control sets it to the variant color when its template is applied. |
| `IconData` | `StreamGeometry?` | `DaisyIconPowerOff` | Icon on the handle; the theme sets the default. |
| `IconForeground` | `IBrush?` | `null` | Not drawn by the current template: the icon always has the variant color. |
| `HandleBackground` | `IBrush?` | `null` | While it is null, the handle is the variant color at 20 % opacity. The current template does not draw a brush set here; while one is set, the handle shows `DaisyBase100Brush`. |
| `TrackWidth` | `double` | `NaN` | Not used by the current template. The control stretches like any control and is at least 200 wide (`MinWidth`); set `Width` for a fixed width. |
| `TrackHeight` | `double` | `NaN` | Replaces the track height of `Size`. The handle, icon and font keep the sizes of `Size`. |

The track has a `DaisySizeLargeCornerRadius` corner radius, a border of `DaisyBorderThicknessMedium` and a padding of 4. The label is bold and centered with a left margin of 48, so it clears the handle. In the neumorphic mode the track is always inset and the handle raised ([DaisyNeumorphic](DaisyNeumorphic.md)).

`DaisySlideToConfirmVariant` and `DaisyDepthStyle` are the types in `Flowery.Controls`, declared in `DaisySlideToConfirm.cs`. `Flowery.Enums` declares two types of the same names that no control uses. XAML picks the right ones from the property types; a C# file that imports both namespaces writes `Flowery.Controls.DaisySlideToConfirmVariant`.

## Computed Sizes

Read-only properties that follow `Size` (and `TrackHeight`) and that the template binds to:

| Size | `EffectiveTrackHeight` | `EffectiveHandleSize` | `EffectiveIconSize` | `EffectiveFontSize` |
| --- | --- | --- | --- | --- |
| ExtraSmall | 28 | 20 | 12 | 8 |
| Small | 36 | 28 | 16 | 9 |
| Medium (default) | 48 | 40 | 20 | 11 |
| Large | 56 | 48 | 24 | 13 |
| ExtraLarge | 64 | 56 | 28 | 15 |

`EffectiveTrackHeight` is `TrackHeight` when that is set.

DaisySlideToConfirm implements `IScalableControl`. Inside a container with `FloweryScaleManager.EnableScaling="True"`, [FloweryScaleManager](FloweryScaleManager.md) sets the read-only `ScaledFontSize` to 11 times the scale factor, at least 8. The current template sizes the label with `EffectiveFontSize`, so `ScaledFontSize` is there for custom templates.

## Quick Examples

```xml
<!-- Variants with their own texts and icons -->
<controls:DaisySlideToConfirm Variant="Primary"
    Text="SLIDE TO POWER OFF" ConfirmingText="POWERING OFF..."
    IconData="{StaticResource DaisyIconPowerOff}" AutoReset="True" />
<controls:DaisySlideToConfirm Variant="Success"
    Text="SLIDE TO CONFIRM" ConfirmingText="CONFIRMED!"
    IconData="{StaticResource DaisyIconCheck}" AutoReset="True" />
<controls:DaisySlideToConfirm Variant="Error"
    Text="SLIDE TO DELETE" ConfirmingText="DELETING..."
    IconData="{StaticResource DaisyIconDelete}" AutoReset="True" />
<controls:DaisySlideToConfirm Variant="Info"
    Text="SLIDE TO UNLOCK" ConfirmingText="UNLOCKED!"
    IconData="{StaticResource DaisyIconSettings}" AutoReset="True" />

<!-- Sizes -->
<controls:DaisySlideToConfirm Size="ExtraSmall" Variant="Primary" Text="EXTRA SMALL" AutoReset="True" />
<controls:DaisySlideToConfirm Size="Small" Variant="Primary" Text="SMALL" AutoReset="True" />
<controls:DaisySlideToConfirm Size="Medium" Variant="Primary" Text="MEDIUM (DEFAULT)" AutoReset="True" />
<controls:DaisySlideToConfirm Size="Large" Variant="Primary" Text="LARGE" AutoReset="True" />

<!-- Depth styles -->
<controls:DaisySlideToConfirm DepthStyle="Flat" Variant="Secondary" Text="FLAT STYLE" AutoReset="True" />
<controls:DaisySlideToConfirm DepthStyle="ThreeDimensional" Variant="Secondary" Text="3D EFFECT" AutoReset="True" />
<controls:DaisySlideToConfirm DepthStyle="Raised" Variant="Secondary" Text="RAISED STYLE" AutoReset="True" />
```

### Reset After the Action

Without `AutoReset`, the control stays completed while the action runs, and the app resets it afterwards:

```xml
<controls:DaisySlideToConfirm x:Name="DeleteSlider" Variant="Error"
    Text="SLIDE TO DELETE" ConfirmingText="DELETING..."
    IconData="{StaticResource DaisyIconDelete}"
    SlideCompleted="OnDeleteConfirmed" />
```

```csharp
private async void OnDeleteConfirmed(object? sender, EventArgs e)
{
    await DeleteSelectedItemsAsync(); // the app's own action
    DeleteSlider.Reset();
}
```

## Tips & Best Practices

- Use `Error` for destructive actions, `Success` for approvals and `Info` for unlocks; the variant colors follow the theme.
- `SlideColor` keeps the color it took first. When the `Variant` changes at run time, set `SlideColor` as well, or the track keeps blending to the color of the first variant.
- Keep `Text` short: the label is centered in the track to the right of the handle and does not wrap.
- Use `AutoReset` for actions that finish at once; for longer ones, leave it off and call `Reset()` when the action is done, so `ConfirmingText` stays visible while it runs.
- For a plain action without the extra step, use [DaisyButton](DaisyButton.md); to ask in a dialog, use [DaisyModal](DaisyModal.md).
