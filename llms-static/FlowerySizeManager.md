# FlowerySizeManager

Static service for global size management across all Daisy controls. Provides discrete size tiers (ExtraSmall to ExtraLarge) that all controls respond to simultaneously via automatic visual tree propagation.

## Quick Start

```csharp
using Flowery.Controls;

// Set the main window for visual tree propagation (in App.axaml.cs)
FlowerySizeManager.MainWindow = MainWindow;

// Apply a global size - automatically propagates to all controls
FlowerySizeManager.ApplySize(DaisySize.Medium);

// Or use a built-in dropdown
```

```xml
<controls:DaisySizeDropdown />
```

## Size Tiers

| Size | Typical Use Case | Height | Font Size |
| ---- | ---------------- | ------ | --------- |
| `ExtraSmall` | High-density UIs, data tables | 24px | 10px |
| `Small` | **Default** - Desktop apps | 32px | 12px |
| `Medium` | Touch-friendly, accessibility | 48px | 14px |
| `Large` | Larger screens, presentations | 64px | 18px |
| `ExtraLarge` | Maximum readability, kiosk | 80px | 20px |

## API Reference

### Properties

```csharp
// Get the current global size
DaisySize currentSize = FlowerySizeManager.CurrentSize;

// Enable/disable automatic visual tree propagation (default: true)
FlowerySizeManager.EnableGlobalAutoSize = true;

// Auto-apply global size to new controls (default: true)
FlowerySizeManager.UseGlobalSizeByDefault = true;

// Set the main window for visual tree propagation
FlowerySizeManager.MainWindow = myWindow;
```

### Methods

```csharp
// Apply by enum - automatically propagates to all controls in visual tree
FlowerySizeManager.ApplySize(DaisySize.Large);

// Apply by name (returns true if successful)
bool success = FlowerySizeManager.ApplySize("Large");

// Force refresh all sizes (call after window fully loads)
FlowerySizeManager.RefreshAllSizes();

// Reset to default (Small)
FlowerySizeManager.Reset();

// Get sidebar width for a given size
double width = FlowerySizeManager.GetSidebarWidth(DaisySize.Medium); // 220
```

### Events

```csharp
FlowerySizeManager.SizeChanged += (sender, size) =>
{
    Console.WriteLine($"Size changed to: {size}");
};
```

## Visual Tree Propagation

When `EnableGlobalAutoSize` is `true` (default), calling `ApplySize()` automatically:

1. Walks the entire visual tree starting from `MainWindow.Content`
2. Finds all controls with a `Size` property of type `DaisySize`
3. Sets the size **only if not explicitly set** in XAML or code
4. Respects `IgnoreGlobalSize` to skip entire branches

### Respecting Explicit Values

Controls with **explicitly-set `Size` properties** are never overwritten:

```xml
<!-- This control WILL respond to global size changes -->
<controls:DaisyButton Content="Responds" />

<!-- This control will ALWAYS be Large (explicit value respected) -->
<controls:DaisyButton Size="Large" Content="Always Large" />
```

This uses Avalonia's `IsSet()` to detect locally-set values vs. defaults.

### Setup in App.axaml.cs

```csharp
public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            
            // Enable visual tree propagation
            FlowerySizeManager.MainWindow = mainWindow;
            
            // Refresh after window loads to catch all controls
            mainWindow.Opened += (_, _) => FlowerySizeManager.RefreshAllSizes();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

## Attached Properties

### IgnoreGlobalSize

Prevents a control (and its descendants) from responding to global size changes:

```xml
<!-- Single control -->
<controls:DaisyButton controls:FlowerySizeManager.IgnoreGlobalSize="True" 
                      Size="Large" Content="Always Large" />

<!-- Container (protects all children) -->
<StackPanel controls:FlowerySizeManager.IgnoreGlobalSize="True">
    <controls:DaisyButton Size="ExtraSmall" Content="XS" />
    <controls:DaisyButton Size="Small" Content="S" />
    <controls:DaisyButton Size="Medium" Content="M" />
    <controls:DaisyButton Size="Large" Content="L" />
    <controls:DaisyButton Size="ExtraLarge" Content="XL" />
</StackPanel>
```

#### Visual Tree Inheritance

The property uses "first explicit setting wins" semantics via visual tree traversal:

1. `ShouldIgnoreGlobalSize(control)` walks up from the control to the root
2. Stops at the first ancestor with an explicitly set value (`True` or `False`)
3. Returns that value, or `false` if no explicit setting is found

This allows **opting back in** within an opted-out container:

```xml
<StackPanel controls:FlowerySizeManager.IgnoreGlobalSize="True">
    <controls:DaisyButton Size="Small" Content="Stays Small" />
    
    <!-- This child opts BACK IN to global sizing -->
    <Border controls:FlowerySizeManager.IgnoreGlobalSize="False">
        <controls:DaisyButton Content="Responds to global size!" />
    </Border>
</StackPanel>
```

> **API Methods:**
> - `GetIgnoreGlobalSize(control)` - Direct property access on control only
> - `ShouldIgnoreGlobalSize(control)` - Walks visual tree, returns first explicit setting

### ResponsiveFont

Makes TextBlock font sizes respond to global size changes:

```xml
<TextBlock Text="Body text" 
           controls:FlowerySizeManager.ResponsiveFont="Primary" />

<TextBlock Text="Hint text" Opacity="0.7"
           controls:FlowerySizeManager.ResponsiveFont="Secondary" />

<TextBlock Text="Section Title" FontWeight="Bold"
           controls:FlowerySizeManager.ResponsiveFont="SectionHeader" />

<TextBlock Text="Page Title" FontWeight="Bold"
           controls:FlowerySizeManager.ResponsiveFont="Header" />
```

#### Font Tiers

| Tier | Description | XS/S/M/L/XL Sizes |
| ---- | ----------- | ------------------- |
| `None` | No responsive sizing (default) | - |
| `Primary` | Body text, descriptions | 10/12/14/18/20 |
| `Secondary` | Hints, captions, labels | 9/10/12/14/16 |
| `Tertiary` | Very small text, counters | 8/9/11/12/14 |
| `SectionHeader` | Section titles, group headers | 12/14/16/18/20 |
| `Header` | Page titles, main headings | 14/16/20/24/28 |

#### How It Works

1. When `ResponsiveFont` is set, the TextBlock is registered internally
2. Font size is immediately applied based on current global size
3. Updates automatically when global size changes
4. Registration is cleaned up when control is unloaded
5. Respects `ShouldIgnoreGlobalSize()` - opted-out TextBlocks are skipped

> **Why not DynamicResource?** Avalonia's nested resource dictionary scoping prevents dynamically updated resources from propagating reliably. The attached property approach (event subscription) is the recommended pattern for text that should scale with global size.

## Supported Controls

All Daisy controls with a `Size` property respond to global size changes automatically via visual tree propagation:

- `DaisyButton`, `DaisyInput`, `DaisyTextArea`
- `DaisySelect`, `DaisyCheckBox`, `DaisyRadio`, `DaisyToggle`
- `DaisyBadge`, `DaisyProgress`, `DaisyRadialProgress`
- `DaisyTabs`, `DaisyMenu`, `DaisyKbd`
- `DaisyAvatar`, `DaisyLoading`, `DaisyFileInput`
- `DaisyNumericUpDown`, `DaisyDateTimeline`, `DaisyPasswordBox`
- `DaisyClock`, `DaisySlideToConfirm`
- And more...

## Making Custom Controls Size-Aware

### Automatic (Recommended)

If your control has a `Size` property of type `DaisySize`, it will **automatically** respond to global size changes via visual tree propagation. No manual subscription needed!

```csharp
public class MyDaisyControl : TemplatedControl
{
    public static readonly StyledProperty<DaisySize> SizeProperty =
        AvaloniaProperty.Register<MyDaisyControl, DaisySize>(nameof(Size), DaisySize.Small);

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

### Manual Event Subscription (Advanced)

For controls that need custom sizing logic beyond the `Size` property:

```csharp
public class MyControl : UserControl
{
    public MyControl()
    {
        InitializeComponent();
        FlowerySizeManager.SizeChanged += OnSizeChanged;
        ApplySize(FlowerySizeManager.CurrentSize);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        FlowerySizeManager.SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, DaisySize size)
    {
        // Check if this control or ancestors opted out
        if (FlowerySizeManager.ShouldIgnoreGlobalSize(this))
            return;
        ApplySize(size);
    }

    private void ApplySize(DaisySize size)
    {
        MyTextBlock.FontSize = FlowerySizeManager.GetFontSizeForTier(
            ResponsiveFontTier.Primary, size);
    }
}
```

## Built-in UI Control

### DaisySizeDropdown

Ready-to-use dropdown for size selection:

```xml
<controls:DaisySizeDropdown />
```

Features:

- Shows current size with abbreviation (XS, S, M, L, XL)
- Localized size names (11 languages)
- Automatically updates all controls when selection changes

**Advanced:** Customize visible sizes and display names via `SizeOptions` property. See [DaisySizeDropdown](DaisySizeDropdown.md).

## Localization

Size names are localized. Add translations to your language files:

```json
{
  "Size_ExtraSmall": "Extra Small",
  "Size_Small": "Small",
  "Size_Medium": "Medium",
  "Size_Large": "Large",
  "Size_ExtraLarge": "Extra Large"
}
```

Supported: English, German, French, Spanish, Italian, Japanese, Korean, Arabic, Turkish, Ukrainian, Chinese (Simplified).

## Design Tokens Integration

Each size tier maps to specific [Design Tokens](DesignTokens.md):

| Size | Height Token | Font Size Token |
| ---- | ------------ | --------------- |
| ExtraSmall | `DaisySizeExtraSmallHeight` (24) | `DaisySizeExtraSmallFontSize` (10) |
| Small | `DaisySizeSmallHeight` (32) | `DaisySizeSmallFontSize` (12) |
| Medium | `DaisySizeMediumHeight` (48) | `DaisySizeMediumFontSize` (14) |
| Large | `DaisySizeLargeHeight` (64) | `DaisySizeLargeFontSize` (18) |
| ExtraLarge | `DaisySizeExtraLargeHeight` (80) | `DaisySizeExtraLargeFontSize` (20) |

## Comparison: FlowerySizeManager vs FloweryScaleManager

| Feature | FlowerySizeManager | FloweryScaleManager |
| ------- | ------------------ | ------------------- |
| **Purpose** | User preference / accessibility | Responsive window sizing |
| **Scope** | Entire app (global) | Only `EnableScaling="True"` containers |
| **Scaling** | Discrete tiers (XS, S, M, L, XL) | Continuous (0.5× to 1.0×) |
| **Trigger** | User selection | Automatic (window resize) |
| **Propagation** | Automatic visual tree walk | Manual container opt-in |
| **Best For** | Desktop apps, accessibility | Data forms, dashboards |

> **Most apps should use FlowerySizeManager only.** FloweryScaleManager is an advanced feature for specific responsive scenarios.

## Platform DPI Notes

> **Windows vs Skia Desktop**: At the same `DaisySize` setting, Windows (WinUI) and Skia Desktop may render text at different physical sizes.

| Platform | DPI Behavior |
| -------- | ------------ |
| **Windows (WinUI)** | Automatically respects system DPI scaling (e.g., 125%, 150%). Text appears larger on high-DPI displays. |
| **Skia Desktop** | May render at 1:1 pixel ratio regardless of system DPI settings. Text appears smaller on high-DPI displays. |

**Example**: On a 125% scaled display, a control set to `DaisySize.Small` (12px font) will appear:

- **Windows**: ~15px physical (12 × 1.25)
- **Skia Desktop**: ~12px physical (no scaling applied)

This is a platform rendering characteristic, not a bug. If visual consistency is critical across platforms, consider:

1. Accepting the difference as platform-native behavior
2. Investigating Skia DPI awareness settings in your Uno Platform configuration
3. Using `FloweryScaleManager` to apply manual DPI compensation on Desktop builds

## Best Practices

1. **Set MainWindow early** - Configure `FlowerySizeManager.MainWindow` in App initialization
2. **Call RefreshAllSizes after load** - Ensures all controls get sized after visual tree is built
3. **Start with Small** - Default size works well for most desktop apps
4. **Provide a size picker** - Use `DaisySizeDropdown` for user control
5. **Test all sizes** - Ensure layouts work at ExtraSmall and ExtraLarge
6. **Use design tokens** - Not hardcoded values
7. **Use ResponsiveFont for TextBlocks** - Not DynamicResource
8. **Don't subscribe to SizeChanged for sizing** - Visual tree propagation handles it automatically
9. **Use explicit Size for demos** - Gallery size examples should set `Size="Large"` etc. directly
10. **Use IgnoreGlobalSize for size demo containers** - Prevents demo controls from responding to global changes
