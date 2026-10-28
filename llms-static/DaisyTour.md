<!-- Supplementary documentation for DaisyTour -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

## Overview

`DaisyTour` explains existing controls with a dimmed backdrop, live target highlights, annotations, and a navigation card.
It uses Avalonia controls and vector shapes.
It does not change application data or move the target into the overlay.

## Add a tour

Add one host as the last child of the page's root `Grid`.
Keep the host outside scrollable content.
The host presents its overlay in the overlay layer (`OverlayLayer`) of its window or view.

```xml
<Grid xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET">
    <StackPanel>
        <controls:DaisyButton x:Name="CreateButton" Content="Create" />
        <controls:DaisyButton Content="Show tour" Click="ShowTour_Click" />
    </StackPanel>
    <controls:DaisyTour x:Name="Tour">
        <controls:DaisyTour.Steps>
            <controls:DaisyTourStep Id="create" Target="{Binding #CreateButton}"
                Title="Create an item" Description="Use this button to create an item." />
        </controls:DaisyTour.Steps>
    </controls:DaisyTour>
</Grid>
```

```csharp
private async void ShowTour_Click(object? sender, RoutedEventArgs e)
{
    await Tour.StartAsync();
}
```

`Steps` is an `ObservableCollection<DaisyTourStep>`.
Each step needs a unique, nonempty `Id` and a target in the host's window or view (`TopLevel`).
An empty collection does not open a tour.

## Prepare a target

Use `PrepareAsync` to select a tab, expand a section, or create a target.
The host calls it before each visit, including Back.
The callback receives the session cancellation token.
After preparation, the host brings the target into view and measures its current layout.

```csharp
Tour.Steps.Add(new DaisyTourStep
{
    Id = "details",
    Title = "Details",
    Description = "Edit the selected item's details here.",
    PrepareAsync = (step, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        Tabs.SelectedItem = DetailsTab;
        step.Target = DetailsEditor;
        return Task.CompletedTask;
    }
});
```

For asynchronous application work, await that work with the supplied token.
Do not use fixed delays to wait for the layout.
Keep step definitions in the application; the library does not discover targets from names or screen coordinates.

## State and outcomes

| Member | Meaning |
| --- | --- |
| `StartAsync(CancellationToken)` | Start a session at its first step. |
| `NextAsync()` | Prepare the next step, or complete the final step. |
| `BackAsync()` | Prepare the previous step again. |
| `Skip()` | Close with `Skipped`. Escape has the same effect. |
| `Cancel()` | Close with `Cancelled`. |
| `IsOpen`, `CurrentIndex`, `CurrentStep` | Read the current session state. |
| `Closed` | Receive one outcome for the session. |
| `StepTargetUnavailable` | Learn that a step cannot show everything it describes. |

The `Closed` event supplies `DaisyTourClosedEventArgs`.
Its `Reason` is `Completed`, `Skipped`, `Cancelled`, `TargetUnavailable`, or `Failed`.
`TargetUnavailable` means the tour could not open because it belongs to no window.
`Failed` means the tour itself could not continue; `Error` tells why.
`AnnotationId` is always null, because target problems no longer close the tour.

A target that cannot be shown never closes a running tour.
The step stays open in a warning state:

- The card waits at its stationary position; with `CardPosition` `Target`, it waits in the center.
- The card frame and the current progress step take the warning color, and a note above the description says what is wrong. Screen readers hear the note when it appears, and the card's name starts with it.
- Connectors that need the missing element are hidden.

| `DaisyTourTargetProblem` | Cause | Ends when |
| --- | --- | --- |
| `Target` | The step target is missing, collapsed, transparent, detached, in another window, or stays outside the visible area after the tour scrolled it into view once. | The target can be shown again; the step returns to normal. |
| `Annotation` | An end of an annotation is collapsed, transparent, detached, or in another window. | The end can be measured again. |
| `Preparation` | `PrepareAsync` threw. The note says that the step could not be prepared completely; the target is still highlighted when it can be shown. | The step is left. |

`StepTargetUnavailable` supplies `DaisyTourStepTargetUnavailableEventArgs` with `StepId`, `Problem`, `AnnotationId`, `Target`, and `Error`.
It is raised after the step is laid out and settled, so a handler may call `Skip()`, `NextAsync()` or `StartAsync()`; reports of a visit that a handler ended are dropped.
Each cause is reported once per visit of the step: an element that returns and leaves again is not reported twice, every unavailable end of every annotation is reported, and an end that is the step target is reported as the target.
A handler that throws is written to the debug output and does not affect the tour.
While the editor previews the tour, the editor shows the error instead, and the event is not raised.

```csharp
Tour.StepTargetUnavailable += (_, problem) =>
    Log.Warning(problem.Error, "Tour step {Step} cannot show {Problem} {Annotation}", problem.StepId, problem.Problem, problem.AnnotationId);
```

```csharp
Tour.Closed += (_, result) =>
{
    if (result.Reason is DaisyTourCloseReason.Completed or DaisyTourCloseReason.Skipped)
        SaveTourSeen();
};
```

The application owns any first-run flag and replay command.
Keep replay available after completion or Skip.
Do not mark cancellation or a failure as successful completion.

## Annotations

Each step has an `Annotations` collection.
Give each annotation an `Id` that is unique within its step: `StepTargetUnavailable` names it, and style profiles use it.
An annotation uses the step target unless its `Target` supplies another control.
Set `Source` to connect two distinct controls.
Both controls must belong to the same window or view and be visible in the layout.
When both cannot fit in a scrolled area, the step target is scrolled into view first.
A connector then still reaches the other control's real position, even outside the window.
A collapsed, transparent, detached, or foreign-window endpoint hides its connector and puts the step in its warning state.

```csharp
var step = new DaisyTourStep
{
    Id = "relationship",
    Target = InputControl,
    Title = "Input and output",
    Description = "The output shows the result of this input."
};
step.Annotations.Add(new DaisyTourAnnotation
{
    Id = "input-output",
    Preset = DaisyTourAnnotationPreset.TwoEndedArrow,
    Source = InputControl,
    Target = OutputControl,
    SourceAnchor = DaisyTourAnchor.Right,
    TargetAnchor = DaisyTourAnchor.Left
});
Tour.Steps.Add(step);
```

| Preset | Appearance |
| --- | --- |
| `ThinArrow`, `ThickArrow`, `BlockArrow` | Thin stroke, thick stroke, or filled arrow. |
| `CurvedArrow`, `TwoEndedArrow` | Curved path or arrowheads at both ends. |
| `DottedLine`, `DashedLine`, `DashDotLine` | Connectors with separate stroke patterns. |
| `Cloud`, `SpeechBubble` | Shapes with optional text and tails. |

`SourceAnchor` and `TargetAnchor` accept `Auto`, `Top`, `Bottom`, `Left`, `Right`, and `Center`.
Automatic anchors use the current bounds of both controls.
Resize, reflow, and scrolling update the geometry during the active step.
When another scroll moves the step target out of view, the tour scrolls its targets into view again; a target that stays outside the visible area puts the step in its warning state until it returns.

## Style inheritance

`DaisyTour.AnnotationDefaults` supplies tour defaults.
`DaisyTourStep.AnnotationDefaults` overrides those defaults for one step.
Each `DaisyTourAnnotation` can override individual properties again.
Unset brushes, `Default` enum values, and `NaN` measurements inherit their values.

| Properties | Use |
| --- | --- |
| `Kind`, `Path` | Choose Arrow, Line, Cloud, SpeechBubble, or Custom; choose Straight, Curved, Elbow, SCurve, Bezier, or Custom paths. |
| `Stroke`, `Fill`, `Foreground`, `Opacity` | Set stroke, shape fill, text color, and opacity. |
| `StrokeThickness`, `StrokeStyle`, `DashArray`, `DashOffset` | Set stroke width and pattern. Dash values are multiples of stroke thickness. |
| `StartHead`, `EndHead`, `HeadLength`, `HeadWidth`, `ArrowStyle`, `BodyWidth` | Set each arrowhead and the arrow body independently. |
| `Width`, `Height`, `MaxWidth`, `MaxHeight`, `FontSize`, `CornerRadius` | Set the shape and text dimensions in DIP. |
| `Curvature`, anchor offsets, `OffsetX`, `OffsetY` | Adjust the path and its position relative to current target bounds. |
| `ControlPoint1X`, `ControlPoint1Y`, `ControlPoint2X`, `ControlPoint2Y` | Place the control points of a `Bezier` path. |
| `Text`, `Content`, `Tail`, `TailWidth`, `TailLength` | Supply optional annotation content and a separate Speech or Thought tail. |
| `Geometry`, `Template` | Supply custom vector geometry or a custom presentation template. |

```csharp
step.Annotations.Add(new DaisyTourAnnotation
{
    Kind = DaisyTourAnnotationKind.Line,
    Source = InputControl,
    Target = OutputControl,
    Path = DaisyTourAnnotationPath.Elbow,
    StrokeStyle = DaisyTourStrokeStyle.Custom,
    DashArray = new DaisyTourDashCollection { 6, 2, 1, 2 },
    StrokeThickness = 4,
    StartHead = DaisyTourArrowHead.None,
    EndHead = DaisyTourArrowHead.Triangle
});
```

## Connector paths

Connectors start and end at the outer edge of each highlight frame.
Automatic anchors dock at the facing side centers along the main direction.
When the controls overlap on that axis, the other axis is used.
`Elbow` paths leave and enter perpendicular to those sides.
When a course would run back through its own control, that anchor faces the course's first direction instead.
Arrowheads and block bodies are filled with `Fill`, or with the stroke color when `Fill` is not set.
`Curved` bends by `Curvature`, a fraction of the connector length that defaults to 0.25.
`Elbow` uses right angles; `CornerRadius` rounds both corners.
`SCurve` leaves each anchor perpendicular to its edge.
Its `Curvature`, 0.5 by default, scales each control's reach by the distance along that edge's direction.
`Bezier` uses two control points in connector coordinates.
(0,0) is the start, (1,0) is the end, and positive Y bends to the same side as a positive `Curvature`.
A control point without both coordinates uses the `SCurve` control point.
`Custom` fits the annotation's `Geometry` between both anchors by rotation and uniform scaling.
The first and last point of its figure meet the anchors, and the stroke keeps its thickness.
Path markup such as `Geometry="M0,0 C20,-30 30,-30 50,0"` works the same way.
A geometry without a path figure, such as an `EllipseGeometry`, meets the anchors at the middle of its left and right bounds edges.
`ArrowStyle="Block"` is not applied to `Custom` paths.

```xml
<controls:DaisyTourAnnotation Source="{Binding #ThemeSelector}" Target="{Binding #SaveButton}"
                              Path="Bezier" ControlPoint1X="0.2" ControlPoint1Y="-0.6"
                              ControlPoint2X="0.8" ControlPoint2Y="0.6" />
```

## Route limits

A connector can keep an area between its two ends clear.
`RouteLimitSide` is `Above`, `Below`, `LeftOf`, or `RightOf` and names the side of a limit line that the course stays on.
`RouteLimit` places that line `RouteLimitMargin` (12 DIP by default) beyond the matching edge of a control, and the line follows that control.
Without `RouteLimit`, `RouteLimitCoordinate` sets a fixed window coordinate in DIP: y for `Above` and `Below`, x for `LeftOf` and `RightOf`.
When an end lies beyond the line, the course leaves that end perpendicular to the line, crosses on the allowed side, and returns to the other end.
`Straight` and `Elbow` paths use right-angle corners rounded by `CornerRadius`; curve paths use smooth corners.
When both ends are on the allowed side, the path keeps its shape and curves do not bend across the line.
The ends never belong to the kept-clear area, and a limit control that cannot be measured is ignored.

```xml
<controls:DaisyTourAnnotation Source="{Binding #SaveButton}" Target="{Binding #PublishButton}"
                              Path="Elbow" CornerRadius="12"
                              RouteLimit="{Binding #StatusBadge}" RouteLimitSide="Above" />
```

## Surface annotations

Clouds and speech bubbles accept text with or without a tail.
They also accept a tail without text.
Without `Width` and `Height`, a shape grows to fit its text up to `MaxWidth` and `MaxHeight`.
Text that exceeds the available height ends with an ellipsis.
`Tail="None"` removes the tail independently of the content.
Without a `Source`, a cloud or speech bubble goes beside its target in one of eight directions, inside the view and clear of the card and other highlights.
It keeps its direction while that still fits and narrows before it is clamped into view.
Custom geometry and templates retain the host's target anchoring and lifecycle.
A `Custom` shape is centered on its `Source`, or on the step target without a `Source`.
Without `Width` and `Height`, its box is sized so that an inscribed ellipse encloses the highlighted control.

## Appearance

The host uses the existing Daisy theme, motion policy, and five `DaisySize` tiers.
`Size` defaults to `Medium`; like the other Daisy controls, the tour follows the global size of `FlowerySizeManager` unless its `Size` is set in XAML, code, a style, or a binding.
An active profile can override the size. Profile `inherit` uses the global size.
`HighlightPadding` defaults to 6 DIP, and `HighlightCornerRadius` defaults to 8 DIP.
The highlight frame uses the theme's Primary color.
The step card's border uses the theme's text color at 45% opacity, so it stands apart from the highlight and from Base300 borders.
`CardBorderBrush` replaces it for one tour; a `DaisyTourCardBorderBrush` resource replaces it for the whole application.
High contrast (`DaisyAccessibility.IsHighContrast`) uses the contrast border color instead.

## Card placement

`DaisyTour.CardPosition` sets where the step card appears; `DaisyTourStep.CardPosition` overrides it for one step, and `Default` inherits it.

- `Target`, the default, places the card beside the highlighted controls. `DaisyTourStep.Placement` (`Auto`, `Top`, `Bottom`, `Left`, `Right`) sets the preferred side; the next side that fits follows.
- `TopLeft`, `Top`, `TopRight`, `Left`, `Center`, `Right`, `BottomLeft`, `Bottom`, and `BottomRight` keep the card at that window position, 12 DIP from the edges. These positions do not mirror in right-to-left languages.
- A fixed position that would cover the highlight slides along its own edge: `Left` and `Right` vertically, `Top` and `Bottom` horizontally, corners and `Center` along either axis. Without room on that edge, the card goes beside the target.
- A card that would cover a connector course between two controls, or the area its route limit keeps clear, moves away. It returns to its position from the previous step when that is clear; otherwise it follows the rules above and may also move away from the target. When no spot is clear, it stays.
- The card and annotation surfaces use the part of the window that its screen's working area shows, so a window that extends below the taskbar or off the screen keeps them visible.

## Localization and accessibility

`NextText`, `BackText`, `SkipText`, and `FinishText` provide optional application overrides.
Without overrides, the host uses the 12 built-in translations.
`Tour_StepCount` supplies the parameterized step announcement.

Bind application titles, descriptions, and annotation text through the existing localization service.
In XAML, a localization markup extension derived from `LocalizeExtensionBase` works on steps and annotations like on any control:

```xml
<controls:DaisyTourStep Id="create" Target="{Binding #CreateButton}"
                        Title="{loc:Localize Tour_CreateTitle}"
                        Description="{loc:Localize Tour_CreateDescription}" />
```

In code, bind the text to the indexer of the localization source:

```csharp
var step = Tour.Steps[0];
step.Bind(DaisyTourStep.TitleProperty, new Binding("[Tour_CreateTitle]")
{
    Source = Localization,
    Mode = BindingMode.OneWay
});
step.Bind(DaisyTourStep.DescriptionProperty, new Binding("[Tour_CreateDescription]")
{
    Source = Localization,
    Mode = BindingMode.OneWay
});
```

The overlay blocks input to the application, including input through visible target highlights.
Tab and Shift+Tab stay inside the navigation card.
Escape skips the tour.
Closure restores previous focus when the element can still accept focus.
The navigation card remains responsible for the main accessible explanation.

## Tour designer and style profiles

`DaisyTourEditor` edits the appearance of an existing tour with the same renderer as normal playback.
The application still owns the steps, targets, preparation callbacks, content, bindings, and custom objects.
The editor keeps its draft separate from these objects.
Apply activates the draft through `DaisyTour.StyleProfile`.
Discard closes the editor and preserves the previously active profile.

Give the tour, each step, and each annotation a stable, nonempty ID.
Keep these IDs unchanged between application versions that share profiles.
Create the editor without a visual parent, then open it for the loaded tour:

```csharp
private readonly DaisyTourEditor _editor = new();

private async Task EditTourAsync(CancellationToken token)
{
    Tour.Id = "onboarding";
    await _editor.OpenAsync(Tour, token);
}
```

The editor mounts its inspector and handles inside the tour overlay.
An external panel behind this overlay cannot receive normal application input.
`ToolbarContent` accepts application controls for file commands or status messages inside the inspector.
The application owns file dialogs and storage.
`IsOpen` indicates whether the editor still accepts operations.

| Editor member | Purpose |
| --- | --- |
| `OpenAsync(tour, token)` | Open a draft for an existing tour. |
| `DraftProfile` | Read the current draft. |
| `ImportJson(json)`, `ImportProfile(profile)` | Validate the complete profile, then replace the draft as one undo action. |
| `ExportJson()`, `ExportProfile()` | Obtain the draft for storage. |
| `Apply()`, `Discard()` | Keep the draft as the active profile, or close without applying it. |
| `Undo()`, `Redo()` | Move through draft changes. |
| `ProfileChanged` | Observe draft changes. |
| `Closed`, `LastError` | Observe editor closure and inspect a lifecycle error. A preview step that cannot show its target stays open and shows its error in the editor. |

Export and Apply are separate commands.
Export saves the current draft without changing the active profile.
Import validates the schema and references before changing the draft.
Invalid input leaves the draft unchanged.
Editor closure does not report normal tour completion or skipping.

### JSON contract

`DaisyTourStyleProfileSerializer` uses source-generated JSON metadata.
`Serialize` and `Deserialize` validate the portable data.
`Validate(profile, tour)` also validates the IDs against the application tour.
Profiles use schema version 1, case-sensitive property names, and enum names rather than numbers.
Colors use `#AARRGGBB` strings.

```json
{
  "schemaVersion": 1,
  "tourId": "onboarding",
  "annotationDefaults": {
    "values": { "strokeThickness": 3, "endHead": "Triangle" }
  },
  "steps": [
    {
      "id": "relationship",
      "annotations": [
        {
          "id": "input-output",
          "style": {
            "values": { "path": "Curved", "curvature": 0.35, "stroke": "#FF3B82F6" },
            "inherit": ["headWidth"]
          }
        }
      ]
    }
  ]
}
```

Each override contains `values` and an optional `inherit` array:

- An omitted property uses the application value at that scope.
- A property in `values` overrides the application value.
- A property in `inherit` skips the application value at that scope and uses the parent scope.

The same property cannot appear in both `values` and `inherit`.
Omit unset properties instead of writing `null` or `NaN`.
Unknown versions, members, IDs, duplicate IDs, invalid values, and invalid enum names cause validation errors.
Step IDs must be unique within the tour, and annotation IDs must be unique within their step.

The root `style` supports size, card position, card border color, highlight padding, and highlight corner radius.
A step `style` supports card position, preferred placement, and optional `cardX`/`cardY` values from 0 to 1.
These coordinates place the card within the available layout area: 0 is the left/top edge, and 1 is the right/bottom edge.
Each omitted coordinate keeps the default placement on that axis.
They are saved with the step and used during playback.
Tour and step `annotationDefaults` support common annotation appearance.
An annotation `style` also supports its anchors, offsets, and route limit values.

The profile stores scalar values, solid colors, dash arrays, and enum choices.
It does not serialize controls, callbacks, text bindings, templates, arbitrary brushes, or custom geometry.
Those values remain in the application definition.
Connector control points use relative connector coordinates, and size values use DIP.

To use a saved profile without the editor, load it before playback:

```csharp
var profile = DaisyTourStyleProfileSerializer.Deserialize(json);
DaisyTourStyleProfileSerializer.Validate(profile, Tour);
Tour.StyleProfile = profile;
await Tour.StartAsync();
```

### Gallery designer example

Open **Guided Tour**, then select **Design tour**.
The dedicated `gallery-tour` example has stable IDs for straight, curved, Bezier, and block arrows, a speech bubble, and prepared targets.
Select a step and an annotation to edit its appearance.
Use the inspector or the handles on the preview.
Move the inspector to the other side or collapse it when it covers a target.
The inspector starts at up to 300 DIP wide. Drag its inner edge to resize it, or focus the edge and use the arrow keys.
The width stays between 180 and 600 DIP, limited by the space needed for the preview card.
Moving or collapsing the inspector preserves the chosen width. This layout preference is not part of the exported tour profile.
The preview card starts at the bottom center of the full preview area.
Opening, closing, or resizing the inspector preserves the card's position unless the two overlap.
When they overlap, the card temporarily moves out of the inspector's way and returns when its intended position is clear again.
This automatic movement does not change saved coordinates, create an undo entry, or enable the card's save button.
Drag the move grip in its header to reposition it, or focus the grip and use the arrow keys.
Dragging and keyboard movement only change the temporary preview position. They do not change the profile, undo history, or exported JSON.
The adjacent save button is available when the position differs from the step's current position.
Select **Save card position for this step** to store `cardX`/`cardY` in the current step as one undo action.
Only this saved position is exported and used during playback. Temporary movement is cleared when you change the step or close the editor.
Remove both overrides through **Use application setting** to restore automatic positioning.
Without saved coordinates or temporary movement, the editor uses bottom placement; normal playback uses the configured card-position settings.
Use the arrow keys on a focused handle for precise changes.
The curvature handle stays just below the visible arc midpoint as the window changes size.
Dragging moves the arc with the handle; grabbing its edge does not change the curvature.
Each annotation-handle drag is one undo action; Escape cancels a drag or closes the editor without applying the draft.
The Undo and Redo arrow buttons have localized tooltips and accessible names.
Use **Import JSON** and **Export JSON** in the inspector to exchange profiles.
Select **Replay applied design** to view the active profile with normal tour navigation.

The Gallery opens and saves profiles with the file dialog of the platform (`TopLevel.StorageProvider`).
It reads the complete import before it changes the draft.
File dialog cancellation preserves the draft.
For a file with a file system path, an export writes and flushes an adjacent temporary file, closes it, then replaces the destination; a file without one, such as a browser download, is written through its own stream.
The success message appears only after replacement completes.
If an export fails, the error includes the temporary path when one exists.
The incomplete file remains available for diagnosis.

Full tour construction, target discovery, tapered arrows, and gradient editing are outside this editor version.

## Gallery demos

Open **Guided Tour** in the Gallery.

| Button | Shows |
| --- | --- |
| Start / replay | Typed XAML steps, nested scrolling, and a target created in another tab. |
| Arrows, Lines | Arrow presets, and stroke patterns on `Straight`, `Curved`, and `Elbow` lines between two controls. |
| Clouds and bubbles, Custom shape | Surface annotations and a custom vector shape around the target. |
| Regions | A connector from the sidebar's theme selection to a control on the page. |
| Paths | `Elbow`, `SCurve`, `Bezier`, and `Custom` paths from the sidebar to the page. |
| Route limit | Limits set by a control and by a fixed coordinate. |

**Step card position** applies `DaisyTour.CardPosition` to every demo.

Use the Gallery to check these behaviors before release:

- Complete, skip, and replay each sequence.
- Use Back after the prepared tab step.
- Resize the window through wrapping boundaries during a connector step.
- Switch through all five sizes, themes, and languages during an open tour.
- Move the window between monitors with different DPI settings.
- Check keyboard navigation, a screen reader, high contrast, and reduced motion.

Runtime tests establish state and geometry behavior.
They do not replace visual checks of alignment, clipping, focus indicators, or screen reader output.
