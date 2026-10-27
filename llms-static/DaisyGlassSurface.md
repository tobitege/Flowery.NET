<!-- Supplementary documentation for DaisyGlassSurface -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

`DaisyGlassSurface` puts a standard control on glass that blurs whatever lies behind it, without a glass subclass per control. It is a set of attached properties: `IsEnabled` switches the glass on, and the glass settings pass to a [DaisyGlass](DaisyGlass.md) in the template of the control.

- The **glass themes** bring their own `DaisyGlass` named `PART_Glass` and switch the glass on. Set them with `Theme`.
- A template without its own glass gets one: the behavior inserts a `DaisyGlass` right behind the surface part (`PartName`, else `PART_Background`, `Background`, `PART_Surface`, `PART_SelectedContainer` or `PART_SolidBackground`). The glass follows the corner radius, margin, alignment, size, minimum and maximum size, visibility and grid cell of the surface, and the surface turns transparent. Turning the glass off removes it and restores the surface; a new template gets one new glass. The surface has to sit in a `Panel` of the template and to stretch or have a size; a surface that is the root of its template, such as that of a plain `DaisyBadge`, gets no glass, so use the glass theme there.
- An inserted glass blurs its backdrop with `BlurMode=SkiaSharp` (`BitmapCapture` without Flowery.NET.Skia) and otherwise keeps the values of the [DaisyGlass](DaisyGlass.md) theme, such as a tint opacity of 0.5. The settings below change any of them.

## Input Controls

`IsEnabled` alone puts every input control on glass; none needs a glass theme.

| Control | Surface part |
| ------- | ------------ |
| `DaisyInput`, `DaisyTextArea`, `DaisyPasswordBox`, `DaisyMaskInput`, `DaisyNumericUpDown` | `PART_Background` |
| `DaisySelect`, `DaisyCalendarDatePicker` | `Background` |
| `DaisyFileInput` | `PART_Surface` |
| `DaisyTagPicker` | `PART_SelectedContainer` |
| `DaisyOtpInput` | each digit box: the input passes `IsEnabled`, `FollowForeground`, `ClearSurface` and the glass settings on to them |

## Glass Mode

The glass mode puts all input controls of the app on glass at once, with every theme, like the global neumorphic mode:

```csharp
DaisyGlassSurface.GlobalEnabled = true;
DaisyGlassSurface.GlobalBlurMode = GlassBlurMode.SkiaSharp;   // the default
```

- It is an app style, so `DaisyGlassSurface.IsEnabled="False"` on a control, or a style of its window, wins over it. A `BlurMode` set on a control wins over `GlobalBlurMode`. `GlobalBlurMode` applies to every glass the behavior inserts behind a surface part, also that of a card with `IsEnabled`; the glass of the glass themes keeps its own blur mode.
- The mode can be switched on before the app exists, for example in `Program.Main`; it takes effect once `DaisyUITheme` loads or a theme is applied.
- While it is on, the glass tint (`DaisyGlassTintColor`) is `Base100` of the active theme, so the glass takes the hue of the theme and the theme text on it keeps its contrast. A theme that sets its own tint, such as Glassy, keeps it. A theme switch computes the tint again.
- `GlobalChanged` fires after every change. `FloweryComponentSidebar` offers the mode as a switch (`SidebarGlassModeItem`).
- Glass only reads as glass over a varied backdrop. Over a flat theme surface it is a slightly tinted field with shine and rim. So while the mode is on, a theme without a backdrop of its own gets soft glows in its primary and secondary color as `DaisyBackdropBrush`, which [DaisyBackdrop](DaisyBackdrop.md) shows behind the app.

### Cost

Measured on a page with 84 input controls over a detailed backdrop that scrolls with them (Release build, 144 Hz display, 24 logical cores; CPU in percent of one core):

| `GlobalBlurMode` | Idle | Scrolling | Typing |
| ---------------- | ---: | --------- | -----: |
| Glass mode off | 0.4 % | 20 %, 144 frames/s | 3.9 % |
| `SkiaSharp` | 5.7 % | 72 %, 75 frames/s | 2.7 % |
| `BitmapCapture` | 0.2 % | 82 %, 143 frames/s | 2.0 % |
| `Simulated` | 0.8 % | 40 %, 144 frames/s | 1.8 % |

- `SkiaSharp` is the default because it is the only mode that blurs what moves behind the fields, such as scrolling content. Idle and typing stay cheap; scrolling many fields at once costs most and can halve a high frame rate.
- `BitmapCapture` captures its backdrop again only when the size of the glass changes, so while scrolling it shows an outdated backdrop, and it costs more than `SkiaSharp`.
- `Simulated` draws no blur: a translucent tinted field with shine and rim. Use it for pages with very many fields or for weak hardware.

## Glass Themes

| Theme | Control | Look |
| ----- | ------- | ---- |
| `DaisyGlassButtonTheme` | `DaisyButton` | The variant color tints the glass at `ColorOpacity` (0.35); the default variant is clear glass that brightens on hover. Text contrast is on. |
| `DaisyGlassBadgeTheme` | `DaisyBadge` | Like the button, with `ColorOpacity` 0.45. |
| `DaisyGlassInputTheme` | `DaisyInput` | The `DaisyInput` templates with the glass behind the field; text, placeholder, icons, caret and floating label follow `Foreground`. |
| `DaisyGlassNavbarTheme` | `DaisyNavbar` | A glass bar with a light border and no drop shadow. |
| `DaisyGlassModalTheme` | `DaisyModal` | A glass dialog that blurs the dimmed page behind it. |
| `DaisyGlassButtonGroupTheme` | `DaisyButtonGroup` | A segmented control on a glass track: a pill slides to the selected segment (see [DaisyButtonGroup](DaisyButtonGroup.md)). |
| `DaisyGlassRangeTheme` | `DaisyRange` | A thin track and a frosted glass pill thumb that turns into a clear lens while dragged (`:dragging`). |
| `DaisyGlassToggleTheme` | `DaisyToggle` | A glass track that fills with the variant color when switched on; the knob springs across. |
| `DaisyGlassMenuTheme` | `DaisyMenu` | A menu on glass with a soft hover and selection pill. |

The themes use `BlurMode=SkiaSharp`, which needs Flowery.NET.Skia (`FlowerySkia.Register()`); without it the glass uses `BitmapCapture`. The tint comes from the theme resource `DaisyGlassTintColor`: white, or navy in the Glassy theme.

`DaisyButtonGroup` gets its look from styles rather than a `ControlTheme`, so its glass template and segment styles follow `DaisyGlassSurface.IsEnabled`. `DaisyGlassButtonGroupTheme` turns it on together with the live blur, the tint, `AutoSelect` and `Shape="Pill"`; use the theme, since `IsEnabled` alone brings the template without those settings.

## Attached Properties

| Property | Default | Description |
| -------- | ------- | ----------- |
| `IsEnabled` | `null` | `True` puts the control on glass; `null` and `False` leave it as it is. |
| `PartName` | `null` | The surface part that gets the glass behind it, or, with a `PART_Glass` in the template, the tint layer above it. |
| `ClearSurface` | `True` | Whether the surface turns transparent over an inserted glass. A theme that gives the surface translucent colors of its own turns it off. |
| `FollowForeground` | `False` | The text, placeholder, floating label and icons of a field follow `Foreground`, for colorful backdrops. |
| `TextContrast` | `True` | The text stays readable over the measured backdrop: it stays light and moves toward white until it has 3:1, helped by the shadow under it, and turns dark with WCAG contrast only where even white stays below 2.5:1, such as over a white backdrop. Only glass with a tint layer over it measures its backdrop, as in the glass button and badge themes; other glass shows app content and measures nothing. |
| `EffectiveForeground` | — | Read-only: the brush the glass themes draw text and icons with, `Foreground` adjusted for contrast. |
| `ColorOpacity` | 0.35 | Opacity of the variant color over the glass (button, badge, navbar, modal). |
| `TextShadowOpacity` | 0.5 | Opacity of the shadow under text and icons. |

The glass settings map one to one to [DaisyGlass](DaisyGlass.md). They are nullable: a setting that is not set keeps the value of the glass theme.

| Setting | DaisyGlass | Setting | DaisyGlass |
| ------- | ---------- | ------- | ---------- |
| `EnableBackdropBlur` | `EnableBackdropBlur` | `Dispersion` | `GlassDispersion` |
| `BlurMode` | `BlurMode` | `Fill` | `GlassFill` |
| `Blur` | `GlassBlur` | `Gradient` | `GlassGradient` |
| `Saturation` | `GlassSaturation` | `FillOpacity` | `GlassFillOpacity` |
| `Opacity` | `GlassOpacity` | `Pattern` | `GlassPattern` |
| `Tint` | `GlassTint` | `PatternColor` | `GlassPatternColor` |
| `TintOpacity` | `GlassTintOpacity` | `PatternOpacity` | `GlassPatternOpacity` |
| `ReflectDegree` | `GlassReflectDegree` | `Background` | `GlassBackground` |
| `ReflectOpacity` | `GlassReflectOpacity` | `BackgroundOpacity` | `GlassBackgroundOpacity` |
| `Depth` / `Curvature` / `Bend` | `GlassDepth` / `GlassCurvature` / `GlassBend` | | |

`DaisyGlassSurface.GetGlass(control)` returns the glass in use, or `null` while the control is not on glass.

## Quick Examples

```xml
<!-- Glass themes -->
<controls:DaisyButton Theme="{StaticResource DaisyGlassButtonTheme}" Content="Continue" Variant="Primary" />
<controls:DaisyBadge Theme="{StaticResource DaisyGlassBadgeTheme}" Content="New" />
<controls:DaisyRange Theme="{StaticResource DaisyGlassRangeTheme}" Value="35" Maximum="100" />

<!-- Settings on a themed control -->
<controls:DaisyButton Theme="{StaticResource DaisyGlassButtonTheme}" Content="Aurora"
                      controls:DaisyGlassSurface.Gradient="Aurora"
                      controls:DaisyGlassSurface.Blur="40" />

<!-- Any control with a surface part, without a theme -->
<controls:DaisyCard controls:DaisyGlassSurface.IsEnabled="True"
                    controls:DaisyGlassSurface.TintOpacity="0.3" />
<controls:DaisySelect controls:DaisyGlassSurface.IsEnabled="True" PlaceholderText="Pick one" />
```

```csharp
var input = new DaisyInput { Label = "Email" };
DaisyGlassSurface.SetIsEnabled(input, true);
DaisyGlassSurface.SetFollowForeground(input, true);
```

## Tips & Best Practices

- Glass only reads as glass over a varied backdrop; over a flat surface it is a slightly tinted surface with shine and rim.
- The dropdown of a select and the calendar of a date picker keep their opaque surface; only the field is on glass.
- Shape a look visually with [DaisyGlassDesigner](DaisyGlassDesigner.md), which writes these attached properties as XAML or C#.
