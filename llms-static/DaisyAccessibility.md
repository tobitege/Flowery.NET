# Overview

A static helper class providing accessibility utilities for Daisy controls. It simplifies setting up screen reader support across all controls.

`DaisyAccessibility` provides:

- An attached property for custom screen reader text
- A setup helper for control authors to register accessibility defaults
- Automatic syncing to Avalonia's `AutomationProperties.Name`

## Attached Property

### AccessibleText

Set custom screen reader text on any Daisy control:

```xml
<controls:DaisyButton 
    Content="🗑️"
    controls:DaisyAccessibility.AccessibleText="Delete item" />
```

This ensures screen readers announce "Delete item" instead of the emoji.

## API Reference

### Methods

| Method | Description |
| ------ | ----------- |
| `GetAccessibleText(obj)` | Gets the accessible text for a control |
| `SetAccessibleText(obj, value)` | Sets the accessible text for a control |
| `SetupAccessibility<T>(defaultText)` | Registers accessibility handling for a control type (for control authors) |
| `GetEffectiveAccessibleText(control, defaultText)` | Gets accessible text with fallback to default |

## For Control Authors

When creating a new Daisy control, call `SetupAccessibility` in the static constructor:

```csharp
public class DaisyMyControl : TemplatedControl
{
    static DaisyMyControl()
    {
        DaisyAccessibility.SetupAccessibility<DaisyMyControl>("My Control");
    }
}
```

This:

1. Sets a default `AutomationProperties.Name` for the control type
2. Automatically syncs any `AccessibleText` changes to `AutomationProperties.Name`

## Examples

### Icon-Only Button

```xml
<controls:DaisyButton 
    controls:DaisyAccessibility.AccessibleText="Search">
    <PathIcon Data="{StaticResource SearchIcon}" />
</controls:DaisyButton>
```

### Status Indicator

```xml
<controls:DaisyStatusIndicator 
    Status="Online"
    controls:DaisyAccessibility.AccessibleText="User is currently online" />
```

### Rating Control

```xml
<controls:DaisyRating 
    Value="4"
    controls:DaisyAccessibility.AccessibleText="Rating: 4 out of 5 stars" />
```

## System contrast and reduced motion

DaisyUITheme observes supported Avalonia platform contrast settings while windows are open.
High contrast overlays opaque semantic brushes without changing CurrentThemeName.
Glass surfaces stop backdrop capture and display an opaque surface. Cards use the same DaisyGlass material implementation.
Shared styles suppress decorative effects and border shadows during high contrast.
Restoring normal contrast reapplies the currently selected palette and permits requested material captures.

Windows motion detection reads the client-area animation setting. Other hosts can supply an explicit override.
Set these values on the UI thread:

```csharp
DaisyAccessibility.HighContrastOverride = true;
DaisyAccessibility.ReducedMotionOverride = true;
// Return to supported system preferences:
DaisyAccessibility.HighContrastOverride = null;
DaisyAccessibility.ReducedMotionOverride = null;
```

Preferences exposes observable IsHighContrast and MotionEnabled values; PreferencesChanged runs on the UI thread.
Reduced motion stops decorative timers, native loading/status animations, text effects, and automatic slideshows.
Finite animations set their final state. Clock, countdown, and other functional timekeeping continue.
Only requested, attached effects resume; Stop, Dispose, and disabled behavior settings prevent automatic restart.

For a custom contrast strategy, set `CustomContrastApplicator` to a `Func<bool, bool>`.
Return true when the host handles the requested contrast state, including restoration when the argument is false.
ObservePlatformSettings returns a disposable subscription for hosts that supply their own IPlatformSettings source.
Dispose it when that source is no longer used.

## Control automation

Rating, progress, radial progress, and color sliders expose RangeValue providers.
Color grids and wheels expose readable/writable color values and keyboard navigation.
Read-only ratings and progress indicators reject changes.
Breadcrumb actions, slide confirmation, and sidebar favorite toggles support keyboard and automation actions.
Input Label, HelperText, HintText, and required state reach the focused text box peer.
Explicit automation names and help text take precedence. Password and OTP names follow live culture changes.

## Best Practices

1. **Always set AccessibleText for icon-only controls** - Screen readers can't interpret icons
2. **Be descriptive but concise** - "Delete" is better than "Click to delete this item from the list"
3. **Include state information** - "Mute (currently unmuted)" is more helpful than just "Mute"
4. **Test with a screen reader** - Windows Narrator (Win+Ctrl+Enter) or NVDA
