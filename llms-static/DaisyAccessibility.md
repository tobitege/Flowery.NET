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

## Best Practices

1. **Always set AccessibleText for icon-only controls** - Screen readers can't interpret icons
2. **Be descriptive but concise** - "Delete" is better than "Click to delete this item from the list"
3. **Include state information** - "Mute (currently unmuted)" is more helpful than just "Mute"
4. **Test with a screen reader** - Windows Narrator (Win+Ctrl+Enter) or NVDA
