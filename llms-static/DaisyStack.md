<!-- Supplementary documentation for DaisyStack -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyStack displays children in a stacked arrangement with optional navigation. It inherits from `ItemsControl` so children overlap in a Grid panel. In static mode (default), theme styles apply Z-index, translation, scale, and opacity per child order creating a deck-of-cards effect. When navigation is enabled, only one item is visible at a time with animated transitions.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowNavigation` | `bool` | `false` | Shows prev/next arrows; only one item visible at a time |
| `NavigationPlacement` | `DaisyStackNavigation` | `Horizontal` | Arrow direction: `Horizontal` (left/right) or `Vertical` (up/down) |
| `NavigationColor` | `DaisyColor` | `Default` | Color for navigation buttons (Primary, Secondary, Accent, etc.) |
| `ShowCounter` | `bool` | `false` | Shows "1 / 5" counter label |
| `CounterPlacement` | `DaisyPlacement` | `Bottom` | Counter position: `Top`, `Bottom`, `Start`, `End` |
| `SelectedIndex` | `int` | `0` | Currently visible item index (0-based) |
| `TransitionDuration` | `TimeSpan` | `300ms` | Animation duration for navigation |
| `StackOpacity` | `double` | `0.6` | Opacity of background items in static mode |

## Enums

### DaisyStackNavigation

- `Horizontal` - Left/Right arrows
- `Vertical` - Up/Down arrows

### DaisyPlacement

- `Top` - Counter above the stack
- `Bottom` - Counter below the stack (default)
- `Start` - Counter to the left
- `End` - Counter to the right

## Behavior

### Static Mode (ShowNavigation=false)

- All children are visible with layered offsets
- 1st child: top-most, full opacity
- 2nd child: slight offset/scale, reduced opacity
- 3rd+ children: further offset/scale, lower opacity
- Supports unlimited children with progressive offsets

### Navigation Mode (ShowNavigation=true)

- Only one item visible at a time
- Animated slide transitions between items
- Wraps around (last → first, first → last)
- Counter shows current position

## Quick Examples

### Basic Static Stack

```xml
<controls:DaisyStack Width="100" Height="100">
    <Border Background="#F87171" CornerRadius="8" />
    <Border Background="#34D399" CornerRadius="8" />
    <Border Background="#60A5FA" CornerRadius="8" />
</controls:DaisyStack>
```

### With Horizontal Navigation

```xml
<controls:DaisyStack Width="220" Height="140"
                     ShowNavigation="True"
                     ShowCounter="True">
    <Border Background="#F87171" CornerRadius="8">
        <TextBlock Text="Slide 1" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White"/>
    </Border>
    <Border Background="#34D399" CornerRadius="8">
        <TextBlock Text="Slide 2" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White"/>
    </Border>
    <Border Background="#60A5FA" CornerRadius="8">
        <TextBlock Text="Slide 3" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White"/>
    </Border>
</controls:DaisyStack>
```

### With Vertical Navigation

```xml
<controls:DaisyStack Width="220" Height="160"
                     ShowNavigation="True"
                     ShowCounter="True"
                     NavigationPlacement="Vertical"
                     CounterPlacement="End">
    <Border Background="#A78BFA" CornerRadius="8">
        <TextBlock Text="Card A" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White"/>
    </Border>
    <Border Background="#F472B6" CornerRadius="8">
        <TextBlock Text="Card B" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White"/>
    </Border>
</controls:DaisyStack>
```

### Colored Navigation Buttons

```xml
<controls:DaisyStack Width="180" Height="100"
                     ShowNavigation="True"
                     NavigationColor="Primary">
    <Border Background="{DynamicResource DaisyBase300Brush}" CornerRadius="8"/>
    <Border Background="{DynamicResource DaisyBase200Brush}" CornerRadius="8"/>
</controls:DaisyStack>
```

### Programmatic Navigation

```csharp
// Navigate to next/previous
myStack.Next();
myStack.Previous();

// Jump to specific index
myStack.SelectedIndex = 2;
```

## Tips & Best Practices

- Keep child sizes equal for a neat stacked appearance
- Use `ShowNavigation="True"` for interactive card browsing
- Combine with shadows on children to enhance depth
- Wrap in a container with padding to prevent clipped offsets in static mode
- Use `NavigationColor` to match your app's theme
- For image galleries, consider `DaisyCarousel` instead (has more carousel-specific features)
- The `SelectedIndex` property can be bound for MVVM scenarios
