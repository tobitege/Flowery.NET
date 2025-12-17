# FloweryScaleManager

Opt-in automatic font scaling for Daisy controls based on window size. Set `EnableScaling="True"` on any container to make all child Daisy controls automatically scale their fonts as the window resizes.

## Quick Start

```xml
xmlns:services="clr-namespace:Flowery.Services;assembly=Flowery.NET"

<!-- Just add this to any container -->
<UserControl services:FloweryScaleManager.EnableScaling="True">
    
    <!-- All child Daisy controls will auto-scale! -->
    <controls:DaisyInput Label="Street" Watermark="Enter address..." />
    <controls:DaisyButton Content="Save" />
    <controls:DaisySelect PlaceholderText="Select country" />
    <controls:DaisyBadge Content="NEW" />
    
</UserControl>
```

## How It Works

1. **Opt-In**: By default, controls behave normally with no scaling
2. **Inherited**: When `EnableScaling="True"` is set, it propagates to all child controls
3. **Window Tracking**: The manager attaches to the parent Window and monitors resize events
4. **Scale Factor**: Calculates a scale factor (0.5 to 1.0) based on window dimensions vs reference size
5. **Auto-Apply**: All Daisy controls that implement `IScalableControl` automatically adjust their fonts

## Attached Properties

### EnableScaling

Enables automatic font scaling for all Daisy controls within this container.

```xml
<StackPanel services:FloweryScaleManager.EnableScaling="True">
    <!-- Child Daisy controls will scale -->
</StackPanel>
```

**Type**: `bool`  
**Default**: `false`  
**Inherits**: Yes (child controls inherit this property)

### ScaleFactor

The current scale factor (read-only). Automatically calculated based on window size.

**Type**: `double`  
**Range**: 0.5 to 1.0  
**Inherits**: Yes

## Configuration

Configure global scaling parameters at app startup:

```csharp
using Flowery.Services;

// Set reference dimensions (default: 1920x1080 HD)
FloweryScaleManager.ReferenceWidth = 1920;
FloweryScaleManager.ReferenceHeight = 1080;

// Set scale factor limits
FloweryScaleManager.MinScaleFactor = 0.5;  // Minimum 50%
FloweryScaleManager.MaxScaleFactor = 1.0;  // Maximum 100%
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ReferenceWidth` | double | 1920 | Reference width for 100% scale |
| `ReferenceHeight` | double | 1080 | Reference height for 100% scale |
| `MinScaleFactor` | double | 0.5 | Minimum scale factor (50%) |
| `MaxScaleFactor` | double | 1.0 | Maximum scale factor (100%) |

## Supported Controls

The following controls implement `IScalableControl` and auto-scale when enabled:

| Control | What Scales |
|---------|-------------|
| `DaisyInput` | Label font size, input text font size |
| `DaisyButton` | Content font size |
| `DaisySelect` | Font size |
| `DaisyBadge` | Content font size |
| `DaisyTextArea` | (inherits from DaisyInput) |

## Combining with ScaleExtension

**FloweryScaleManager** handles Daisy control fonts automatically. For other elements, use `ScaleExtension`:

```xml
<UserControl services:FloweryScaleManager.EnableScaling="True">
    
    <!-- Daisy controls auto-scale their fonts -->
    <controls:DaisyInput Label="Name" />
    
    <!-- Manual scaling for regular TextBlocks, spacing, etc. -->
    <TextBlock Text="Welcome" FontSize="{services:Scale FontTitle}" />
    <StackPanel Spacing="{services:Scale SpacingMedium}">
        <controls:DaisyCard Width="{services:Scale CardWidth}" Padding="{services:Scale SpacingSmall}">
            <controls:DaisyButton Content="Submit" />
        </controls:DaisyCard>
    </StackPanel>
    
</UserControl>
```

## Creating Custom Scalable Controls

Implement `IScalableControl` to make your custom control support auto-scaling:

```csharp
using Flowery.Services;

public class MyCustomControl : Control, IScalableControl
{
    // Base font size before scaling
    private const double BaseFontSize = 14.0;
    private const double MinFontSize = 11.0;
    
    public void ApplyScaleFactor(double scaleFactor)
    {
        // Use FloweryScaleManager helper to apply scaling with minimum
        FontSize = FloweryScaleManager.ApplyScale(BaseFontSize, MinFontSize, scaleFactor);
    }
}
```

### Helper Methods

```csharp
// Simple scaling
double scaled = FloweryScaleManager.ApplyScale(baseValue, scaleFactor);

// Scaling with minimum value
double scaled = FloweryScaleManager.ApplyScale(baseValue, minValue, scaleFactor);

// Get current scale factor for a window size
double factor = FloweryScaleManager.CalculateScaleFactor(width, height);
```

## Events

### ScaleFactorChanged

Raised when the scale factor changes for any window:

```csharp
FloweryScaleManager.ScaleFactorChanged += (sender, args) =>
{
    Console.WriteLine($"Scale factor changed to: {args.ScaleFactor}");
    // args.Window provides the Window reference
};
```

## Scale Calculation

The scale factor is calculated as:

```text
scaleFactor = min(windowWidth / referenceWidth, windowHeight / referenceHeight)
scaleFactor = clamp(scaleFactor, minScaleFactor, maxScaleFactor)
```

**Examples** (with default 1920x1080 reference):

| Window Size | Scale Factor |
|-------------|--------------|
| 1920×1080 | 1.0 (100%) |
| 1440×900 | 0.75 (75%) |
| 1280×720 | 0.667 (67%) |
| 960×540 | 0.5 (50% - minimum) |

## Best Practices

### 1. Enable at Container Level

Set `EnableScaling` on a parent container, not individual controls:

```xml
<!-- ✅ Good: One declaration for all children -->
<UserControl services:FloweryScaleManager.EnableScaling="True">
    <controls:DaisyInput ... />
    <controls:DaisyButton ... />
</UserControl>

<!-- ❌ Avoid: Setting on each control -->
<controls:DaisyInput services:FloweryScaleManager.EnableScaling="True" ... />
<controls:DaisyButton services:FloweryScaleManager.EnableScaling="True" ... />
```

### 2. Combine with FloweryResponsive

For complex layouts, combine with discrete breakpoints:

```xml
<UserControl services:FloweryScaleManager.EnableScaling="True"
             services:FloweryResponsive.IsEnabled="True"
             services:FloweryResponsive.BaseMaxWidth="400">
    <!-- Fonts scale continuously, layout responds to breakpoints -->
</UserControl>
```

### 3. Use ScaleExtension for Non-Text Properties

FloweryScaleManager only scales fonts. Use ScaleExtension for padding, widths, margins:

```xml
<controls:DaisyCard 
    Width="{services:Scale CardWidth}" 
    Padding="{services:Scale SpacingSmall}">
    <!-- Font scaling is automatic for labels/content -->
    <controls:DaisyInput Label="Name" />
</controls:DaisyCard>
```

## Comparison: FloweryScaleManager vs ScaleExtension

| Feature | FloweryScaleManager | ScaleExtension |
|---------|---------------------|----------------|
| **Scope** | Auto-scales Daisy control fonts | Scales any property |
| **Usage** | Attached property on container | Markup extension per property |
| **Effort** | One line for all children | One binding per property |
| **Customization** | Automatic based on control type | Full control over values |
| **Best For** | Consistent font scaling | Spacing, widths, custom values |
