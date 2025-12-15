# Global Sizing

Flowery.NET provides a centralized sizing system that allows you to dynamically resize all controls in your application at once. This is useful for accessibility, user preferences, or adapting to different screen sizes.

## Overview

The `FlowerySizeManager` is a static service that:

- Broadcasts size changes to all subscribing controls
- Maintains the current global size setting
- Provides localized size names in 11 languages

## Quick Start

Apply a global size to all controls:

```csharp
using Flowery.Controls;

// Apply a size by enum
FlowerySizeManager.ApplySize(DaisySize.Small);

// Or by name (case-insensitive)
FlowerySizeManager.ApplySize("Large");
```

## Size Options

| Size | Typical Use Case |
|------|------------------|
| `ExtraSmall` | High-density UIs, data tables, compact toolbars |
| `Small` | **Default** - Good balance for desktop apps |
| `Medium` | Touch-friendly, accessibility |
| `Large` | Larger screens, presentations |
| `ExtraLarge` | Maximum readability, kiosk mode |

## API Reference

### Properties

```csharp
// Get the current global size
DaisySize currentSize = FlowerySizeManager.CurrentSize;

// Check/set if new controls should auto-use global size
FlowerySizeManager.UseGlobalSizeByDefault = true;
```

### Methods

```csharp
// Apply a size by enum
FlowerySizeManager.ApplySize(DaisySize.Medium);

// Apply a size by name (returns true if successful)
bool success = FlowerySizeManager.ApplySize("ExtraLarge");

// Reset to default (Small)
FlowerySizeManager.Reset();
```

### Events

```csharp
// Subscribe to size changes
FlowerySizeManager.SizeChanged += (sender, size) =>
{
    Console.WriteLine($"Size changed to: {size}");
    // Update your custom controls here
};
```

## Built-in UI Controls

Flowery.NET provides ready-to-use controls for size selection:

### DaisySizeDropdown

A ComboBox-style dropdown that shows all available sizes with localized names:

```xml
<controls:DaisySizeDropdown />
```

The dropdown:

- Shows the current size with an abbreviation (XS, S, M, L, XL)
- Displays localized size names (e.g., "Klein" in German, "小" in Japanese)
- Automatically updates all controls when selection changes

### Which Controls Respond?

All DaisyUI controls with a `Size` property respond to global size changes:

- `DaisyButton`
- `DaisyInput` / `DaisyTextArea`
- `DaisySelect`
- `DaisyCheckBox` / `DaisyRadio` / `DaisyToggle`
- `DaisyBadge`
- `DaisyProgress` / `DaisyRadialProgress`
- `DaisyTabs`
- `DaisyMenu`
- `DaisyKbd`
- `DaisyAvatar`
- `DaisyLoading`
- `DaisyFileInput`
- `DaisyNumericUpDown`
- `DaisyDateTimeline`
- And more...

## Making Custom Controls Size-Aware

To make your own controls respond to global size changes:

### Option 1: Subscribe in Constructor

```csharp
public class MyCustomControl : UserControl
{
    public MyCustomControl()
    {
        InitializeComponent();
        
        // Subscribe to global size changes
        FlowerySizeManager.SizeChanged += OnSizeChanged;
        
        // Apply initial size
        ApplySize(FlowerySizeManager.CurrentSize);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // Clean up subscription
        FlowerySizeManager.SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, DaisySize size)
    {
        ApplySize(size);
    }

    private void ApplySize(DaisySize size)
    {
        // Scale your control based on size
        var fontSize = size switch
        {
            DaisySize.ExtraSmall => 10,
            DaisySize.Small => 12,
            DaisySize.Medium => 14,
            DaisySize.Large => 16,
            DaisySize.ExtraLarge => 18,
            _ => 14
        };
        
        MyTextBlock.FontSize = fontSize;
    }
}
```

### Option 2: Add a Size Property

For DaisyUI-style controls, add a `Size` styled property:

```csharp
public class MyDaisyControl : TemplatedControl
{
    public static readonly StyledProperty<DaisySize> SizeProperty =
        AvaloniaProperty.Register<MyDaisyControl, DaisySize>(
            nameof(Size), DaisySize.Small);

    public DaisySize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
```

Then use design tokens in your theme:

```xml
<Style Selector="local|MyDaisyControl[Size=Small]">
    <Setter Property="FontSize" Value="{DynamicResource DaisySizeSmallFontSize}" />
    <Setter Property="Height" Value="{DynamicResource DaisySizeSmallHeight}" />
</Style>

<Style Selector="local|MyDaisyControl[Size=Large]">
    <Setter Property="FontSize" Value="{DynamicResource DaisySizeLargeFontSize}" />
    <Setter Property="Height" Value="{DynamicResource DaisySizeLargeHeight}" />
</Style>
```

## Bulk-Applying Size via Reflection

For host applications like the Gallery, you can automatically apply sizes to all DaisyUI controls in the visual tree:

```csharp
private static void ApplyGlobalSizeToControls(Control root, DaisySize size)
{
    foreach (var control in root.GetVisualDescendants().OfType<Control>())
    {
        var sizeProperty = control.GetType().GetProperty("Size");
        if (sizeProperty != null && 
            sizeProperty.PropertyType == typeof(DaisySize) && 
            sizeProperty.CanWrite)
        {
            sizeProperty.SetValue(control, size);
        }
    }
}
```

## Opting Out of Global Sizing

Sometimes you have controls that should **not** respond to global size changes—for example, demonstration controls showing all five size variants. Use the `IgnoreGlobalSize` attached property:

### On Individual Controls

```xml
<controls:DaisyButton controls:FlowerySizeManager.IgnoreGlobalSize="True" 
                      Size="Large" Content="I stay Large!" />
```

### On Parent Containers (Recommended)

Mark a parent container to protect all descendant controls:

```xml
<StackPanel controls:FlowerySizeManager.IgnoreGlobalSize="True">
    <!-- All size example controls inside are protected -->
    <controls:DaisyAvatar Size="ExtraSmall" />
    <controls:DaisyAvatar Size="Small" />
    <controls:DaisyAvatar Size="Medium" />
    <controls:DaisyAvatar Size="Large" />
    <controls:DaisyAvatar Size="ExtraLarge" />
</StackPanel>
```

The attached property is inherited through the visual tree, so you only need to set it on the outermost container.

## Localization

Size names are fully localized. Add translations to your language files:

```json
{
  "Size_ExtraSmall": "Extra Small",
  "Size_Small": "Small",
  "Size_Medium": "Medium",
  "Size_Large": "Large",
  "Size_ExtraLarge": "Extra Large"
}
```

Supported languages: English, German, French, Spanish, Italian, Japanese, Korean, Arabic, Turkish, Ukrainian, Chinese (Simplified).

## Design Tokens Integration

The sizing system works with [Design Tokens](DesignTokens.md). Each size tier maps to specific tokens:

| Size | Height Token | Font Size Token |
|------|--------------|-----------------|
| ExtraSmall | `DaisySizeExtraSmallHeight` (24) | `DaisySizeExtraSmallFontSize` (10) |
| Small | `DaisySizeSmallHeight` (32) | `DaisySizeSmallFontSize` (12) |
| Medium | `DaisySizeMediumHeight` (48) | `DaisySizeMediumFontSize` (14) |
| Large | `DaisySizeLargeHeight` (64) | `DaisySizeLargeFontSize` (18) |
| ExtraLarge | `DaisySizeExtraLargeHeight` (80) | `DaisySizeExtraLargeFontSize` (20) |

## Best Practices

1. **Start with Small** - The default size of `Small` works well for most desktop applications.

2. **Provide a size picker** - Give users control over the size preference, especially for accessibility.

3. **Test all sizes** - Ensure your layouts don't break at `ExtraSmall` (compact) or `ExtraLarge` (spacious).

4. **Use tokens, not hardcoded values** - This ensures your custom controls scale properly.

5. **Unsubscribe from events** - Always clean up `SizeChanged` subscriptions in `OnUnloaded` to prevent memory leaks.

## Example: Settings Panel

A complete example of a settings panel with size selection:

```xml
<StackPanel Spacing="16">
    <TextBlock Text="Display Size" FontWeight="Bold" />
    
    <controls:DaisySizeDropdown Width="180" />
    
    <TextBlock Text="This text will resize when you change the size above."
               FontSize="{DynamicResource DaisySizeMediumFontSize}" />
    
    <controls:DaisyButton Content="Sample Button" />
    <controls:DaisyInput Watermark="Sample Input" />
</StackPanel>
```

All controls in the panel will automatically resize when the dropdown selection changes.
