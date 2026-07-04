# Working With Animations

You are working in the Flowery.NET repository, an Avalonia control library styled after daisyUI. Treat animation work as visual design plus Avalonia layout engineering. The goal is not only to make XAML compile; the animation must remain recognizable at small sizes, scale through the existing `DaisyLoading` size system, and behave consistently across themes.

## DaisyLoading Variant Architecture

For DaisyLoading variants, every change must respect the existing architecture:

1. Update the enum in `Flowery.NET/Controls/DaisyLoading.cs` when adding a new variant. Add an XML doc comment directly above the enum member:

```csharp
/// <summary>Xyz animation - short description</summary>
```

2. Implement the template and animations under `Flowery.NET/Themes/DaisyLoading/`. Register new theme files through `StyleInclude` in `Flowery.NET/Themes/DaisyLoading.axaml`.

3. Add the gallery row in `Flowery.NET.Gallery/Examples/LoadingExamples.cs`. Use the matching `Create*Rows()` method and give the row a concise label with a short hint, for example `ClockSpin (waiting)`.

4. Add the designer preview element in the `Design.PreviewWith` section of `Flowery.NET/Themes/DaisyLoading.axaml`.

After adding controls or variants, run:

```powershell
python Utils/generate_docs.py
```

Do not run `dotnet build` unless explicitly asked. Do not use CUA or UI automation unless explicitly asked.

Read the current template before changing a visual bug. Identify the geometric cause first. Then redesign in one focused pass.

## Standard Template Shape

Loading variants should use a stable 96x96 design space inside a `Viewbox`:

```xml
<Style Selector="controls|DaisyLoading[Variant=Xyz]">
    <Setter Property="Template">
        <ControlTemplate>
            <Viewbox Width="{TemplateBinding Width}" Height="{TemplateBinding Height}" Stretch="Uniform">
                <Canvas Width="96" Height="96" ClipToBounds="True">
                    <!-- shapes with Classes="part-name" -->
                </Canvas>
            </Viewbox>
        </ControlTemplate>
    </Setter>
</Style>
```

Use `ClipToBounds="True"` when shapes move beyond the intended visual bounds. Keep animation selectors outside the `ControlTemplate` and target classed elements:

```xml
<Style Selector="controls|DaisyLoading[Variant=Xyz] /template/ Ellipse.part-name">
    <Style.Animations><Animation Duration="0:0:1.5" IterationCount="Infinite"><KeyFrame KeyTime="0:0:0"><Setter Property="Opacity" Value="0"/></KeyFrame><KeyFrame KeyTime="0:0:0.75"><Setter Property="Opacity" Value="1"/></KeyFrame><KeyFrame KeyTime="0:0:1.5"><Setter Property="Opacity" Value="0"/></KeyFrame></Animation></Style.Animations>
</Style>
```

Preserve the existing single-line `<Style.Animations>` formatting in the loading theme files unless the surrounding file already uses a multi-line block.

## Colors And Theme Behavior

Use theme-aware brushes:

- Use `{TemplateBinding Foreground}` for the main motif.
- Use `{DynamicResource DaisyBase100Brush}` for cutouts, separators, masked backgrounds, and surfaces that should appear as the current theme background.
- Avoid hard-coded colors unless the variant is intentionally retro or diagnostic and the existing file already uses that style.

Prefer filled or partly filled silhouettes over thin outline-only drawings. Small loading indicators must still read at `Small` and `Medium` sizes.

## Avalonia Transform Rules

Avalonia is not WPF. Do not copy WPF transform-origin assumptions.

### Critical Rule: RenderTransformOrigin

In Avalonia, `RenderTransformOrigin` defaults to `RelativePoint.Center`. That means a centered rotation usually needs no `RenderTransformOrigin` at all.

Do not write this when you mean "center":

```xml
RenderTransformOrigin="0.5,0.5"
```

That is WPF-style thinking and can make the pivot behave as if it is near the top-left of the visual. This exact mistake broke `ClockSpin`: the clock hands rotated around a point close to the upper-left animation frame instead of the clock center.

For centered rotation in a 96x96 loading template, prefer this:

```xml
<Canvas Classes="minute-hand" Width="96" Height="96">
    <Path Stroke="{TemplateBinding Foreground}" StrokeThickness="4" StrokeLineCap="Round" Data="M 48 48 L 48 26"/>
</Canvas>
```

Then animate the wrapper:

```xml
<Style Selector="controls|DaisyLoading[Variant=ClockSpin] /template/ Canvas.minute-hand">
    <Style.Animations><Animation Duration="0:0:1.2" IterationCount="Infinite"><KeyFrame KeyTime="0:0:0"><Setter Property="RotateTransform.Angle" Value="0"/></KeyFrame><KeyFrame KeyTime="0:0:1.2"><Setter Property="RotateTransform.Angle" Value="360"/></KeyFrame></Animation></Style.Animations>
</Style>
```

If a transform must pivot somewhere other than center, make that explicit only after checking Avalonia's `RelativePoint` syntax. Percent-style values such as `50%,50%` are relative; plain decimal values such as `0.5,0.5` are not a safe substitute for WPF's relative center convention.

### Never Rotate Path Directly

A `Path`'s layout bounds come from its geometry. Rotating a `Path` directly often pivots around the path's own tight bounds rather than the intended icon or canvas center.

Wrap rotatable path content in a classed `Canvas` and animate the `Canvas`:

```xml
<Canvas Classes="rotating-part" Width="96" Height="96">
    <Path Stroke="{TemplateBinding Foreground}" StrokeThickness="4" StrokeLineCap="Round" Data="M 48 48 L 48 20"/>
</Canvas>
```

This is especially important for clock hands, hourglasses, needles, magnifying glasses, arrows, and any asymmetric path.

### Move Groups As Groups

If the whole motif should wobble, shake, pulse, drift, or rotate, wrap all relevant shapes in a single classed `Canvas` and animate that wrapper. Do not animate each child separately unless the children are intentionally independent.

```xml
<Canvas Classes="pig" Width="96" Height="96">
    <!-- all pig shapes here -->
</Canvas>
```

This keeps the silhouette coherent and avoids parts drifting apart.

### Keep Transform Types Consistent

Keyframe animations on transform properties work without declaring a `RenderTransform`, but each animated transform type must stay consistent on the same element. Do not mix unrelated transform assumptions across keyframes.

Good:

```xml
<Setter Property="TranslateTransform.X" Value="0"/>
<Setter Property="TranslateTransform.Y" Value="8"/>
```

Good:

```xml
<Setter Property="RotateTransform.Angle" Value="360"/>
```

Avoid redesigning one element so that different selectors fight over different transform behavior.

## Geometry Checklist

Before writing animation keyframes, define the static drawing:

- The canvas is 96x96.
- The intended visual center is usually `(48,48)`.
- Strokes should be thick enough to survive downscaling.
- Filled silhouettes are more readable than hairline outlines.
- Cutouts and separators should use `DaisyBase100Brush`.
- Details must overlap the silhouette when they belong to it. A snout, stamp, badge, handle, or connector should not look like it floats beside the object.
- Round objects should be round-ish ellipses, not flat slivers. For example, coins should be closer to `14x13` than `18x7`.
- Use `Border` with `CornerRadius` for rounded rectangles. Avalonia `Rectangle` does not have `CornerRadius`.
- `Border` has no `Foreground`; use `Background` and `BorderBrush`.
- Avalonia `Path` does not support `StrokeLineJoin`; do not use it.

## Animation Timing Rules

Make loops seamless:

- First and last keyframes should carry identical values when the animation returns to its start.
- Hold phases should use two keyframes with the same value.
- Stagger repeated parts with `Delay` or offset keyframes.
- Keep cycles short enough for loading feedback, usually between `0:0:0.8` and `0:0:2.4`.
- Use different durations only when the concept needs it, for example fast minute hand and slower hour hand in `ClockSpin`.

Example hold phase:

```xml
<KeyFrame KeyTime="0:0:0.35"><Setter Property="Opacity" Value="1"/></KeyFrame>
<KeyFrame KeyTime="0:0:0.85"><Setter Property="Opacity" Value="1"/></KeyFrame>
```

For stack-growth animations, synchronize the static layer fade-in with the falling layer's landing keyframe. The item should land first, then the next layer appears bottom-up.

## Visual Debugging Workflow

When a variant looks wrong:

1. Read the current template and the animation selectors.
2. Identify what is being animated: the whole motif, a wrapper, a path, or a child shape.
3. Check whether the animated element's layout bounds match the intended pivot or movement area.
4. For rotation bugs, inspect `RenderTransformOrigin` first.
5. For detached details, inspect absolute coordinates and overlap.
6. For unreadable small variants, increase fill area or stroke thickness instead of adding detail.
7. Redesign the static geometry before tweaking keyframes.

Do not keep making small random timing or coordinate changes when the geometry is wrong. Fix the pivot, wrapper, bounds, or silhouette.

## ClockSpin Reference Lesson

`ClockSpin` is the reference case for centered rotation:

- The clock face is centered at `(48,48)`.
- The hands are drawn from `(48,48)` outward.
- Each hand is inside a 96x96 wrapper `Canvas`.
- The wrapper is animated with `RotateTransform.Angle`.
- The wrapper does not set `RenderTransformOrigin`, so Avalonia's center default applies.
- The `Path` itself is never rotated directly.

This makes the hands rotate like a real clock instead of orbiting around the top-left of the animation frame.

## Human Review Checklist

Before accepting a loading animation:

- Does it read correctly at small, medium, and large sizes?
- Does the motif remain recognizable without the label?
- Does it stay inside the intended 96x96 view, or is clipping intentional?
- Does the loop return cleanly to its first frame?
- Are moving parts attached to the object they belong to?
- Are theme colors used instead of fixed colors?
- Are rotations applied to wrapper canvases rather than raw paths?
- Is `RenderTransformOrigin="0.5,0.5"` absent unless there is a very specific, verified reason?
- Did documentation get regenerated when a new variant was added?

