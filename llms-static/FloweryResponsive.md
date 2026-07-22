<!-- Supplementary documentation for FloweryResponsive -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

FloweryResponsive provides responsive layout functionality via attached properties. It calculates responsive widths based on available container space and exposes standard breakpoint constants for consistent responsive behavior across your app.

## FloweryBreakpoints

Standard responsive breakpoints aligned with common CSS frameworks:

| Constant | Value | Description |
| -------- | ----- | ----------- |
| `ExtraSmall` | 480px | Extra small screens (phones in portrait) |
| `Small` | 640px | Small screens (phones landscape, small tablets) |
| `Medium` | 768px | Medium screens (tablets) |
| `Large` | 1024px | Large screens (small laptops) |
| `ExtraLarge` | 1280px | Extra large screens (desktops) |
| `TwoXL` | 1536px | 2XL screens (large desktops) |

### Helper Methods

```csharp
// Get breakpoint name for a width
string bp = FloweryBreakpoints.GetBreakpointName(800); // "md"

// Check if at/above a breakpoint
bool isDesktop = FloweryBreakpoints.IsAtLeast(width, FloweryBreakpoints.Large);

// Check if below a breakpoint
bool isMobile = FloweryBreakpoints.IsBelow(width, FloweryBreakpoints.Medium);
```

## FloweryResponsive Attached Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `IsEnabled` | bool | Enable responsive behavior on the container |
| `BaseMaxWidth` | double | Maximum width when space is available (default: 430) |
| `ResponsiveMaxWidth` | double | Calculated responsive width (read-only, bind to this) |
| `CurrentBreakpoint` | string | Current breakpoint name (xs, sm, md, lg, xl, 2xl) |

## XAML Usage

```xml
<!-- Add namespace -->
xmlns:services="clr-namespace:Flowery.Services;assembly=Flowery.NET"

<!-- Enable on a container (e.g., ScrollViewer) -->
<ScrollViewer x:Name="MainScrollViewer"
              services:FloweryResponsive.IsEnabled="True"
              services:FloweryResponsive.BaseMaxWidth="430">
    
    <!-- Child elements can bind to the calculated width -->
    <StackPanel MaxWidth="{Binding (services:FloweryResponsive.ResponsiveMaxWidth), 
                                   ElementName=MainScrollViewer}">
        <!-- Content adapts to available space -->
    </StackPanel>
</ScrollViewer>
```

## How It Works

1. Attach `IsEnabled="True"` to a container control
2. The helper listens to the container's `Bounds` property changes
3. It calculates `ResponsiveMaxWidth = Min(BaseMaxWidth, AvailableWidth - Padding)`
4. Child controls bind to `ResponsiveMaxWidth` for responsive sizing
5. `CurrentBreakpoint` updates with the breakpoint name for conditional logic

## Tips & Best Practices

- Use `BaseMaxWidth` to set the ideal maximum width for your content
- Bind controls like `DaisyCarousel`, `DaisyCard`, or `DaisyTable` to `ResponsiveMaxWidth`
- Use `CurrentBreakpoint` in converters for conditional visibility or styling
- The minimum width is clamped to 200px to maintain usability
- Default padding of 48px accounts for margins and scrollbars
