# FlowerySizeManager

Static service for global size management across all Daisy controls. Provides discrete size tiers (ExtraSmall to ExtraLarge). Every loaded control that declares a `StyledProperty<DaisySize>` named `SizeProperty` follows the current tier unless a `Size` value was set explicitly.

## Quick Start

```csharp
using Flowery.Controls;

// Apply a global size - every control with a Size property follows,
// including controls created later and controls in other windows
FlowerySizeManager.ApplySize(DaisySize.Large);

// Or use a built-in dropdown
```

```xml
<controls:DaisySizeDropdown />
```

## Size Tiers

| Size | Typical Use Case | Height | Font Size |
| ---- | ---------------- | ------ | --------- |
| `ExtraSmall` | High-density UIs, data tables | 24px | 10px |
| `Small` | Dense desktop apps | 28px | 12px |
| `Medium` | **Default** - matches the class defaults | 32px | 14px |
| `Large` | Larger screens, presentations | 36px | 18px |
| `ExtraLarge` | Maximum readability, kiosk | 40px | 20px |

## API Reference

### Properties

```csharp
// Get the current global size
DaisySize currentSize = FlowerySizeManager.CurrentSize;

// Enable/disable supplying the global size to controls (default: true)
FlowerySizeManager.EnableGlobalAutoSize = true;

// Auto-apply global size to new controls (default: true)
FlowerySizeManager.UseGlobalSizeByDefault = true;

// Optional extra root for RefreshAllSizes(); desktop windows are found automatically
FlowerySizeManager.MainWindow = myWindow;
```

### Methods

```csharp
// Apply by enum - every loaded control with a Size property follows
FlowerySizeManager.ApplySize(DaisySize.Large);

// Apply by name (returns true if successful)
bool success = FlowerySizeManager.ApplySize("Large");

// Re-apply the current size to all open windows (only needed for controls
// that were created before DaisyUITheme was loaded)
FlowerySizeManager.RefreshAllSizes();

// Reset to default (Medium)
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

## How Controls Follow the Global Size

`DaisyUITheme` registers `FlowerySizeManager` when it is loaded. From then on the manager:

1. Tracks every control whose type declares a `StyledProperty<DaisySize>` named `SizeProperty` when the control raises `Loaded`, in any window or popup
2. Supplies the current tier with `SetCurrentValue` on load, on every `ApplySize()` call and whenever the control's `Size` value changes (for example when a style is added or removed)
3. Skips controls that have a base value for `Size` (XAML, code, style or binding) - those are never overwritten
4. Respects `IgnoreGlobalSize` and clears a value it supplied earlier when a control opts out
5. Stops tracking the control on `Unloaded`

`RefreshAllSizes()` additionally walks all open top-level windows (desktop lifetime windows, the single-view main view and `MainWindow`). It is only needed for controls that were created before `DaisyUITheme` was loaded.

### Respecting Explicit Values

Controls with **explicitly-set `Size` properties** are never overwritten:

```xml
<!-- This control WILL respond to global size changes -->
<controls:DaisyButton Content="Responds" />

<!-- This control will ALWAYS be Large (explicit value respected) -->
<controls:DaisyButton Size="Large" Content="Always Large" />
```

A `Size` setter in a style also wins. Removing the style hands the control back to the global size. The check uses Avalonia's `GetBaseValue()`, so a value supplied by the manager is never mistaken for an explicit value.

### Setup in App.axaml.cs

No setup is required beyond adding `DaisyUITheme` to `Application.Styles`. Apply a size whenever you like:

```csharp
public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        FlowerySizeManager.ApplySize(DaisySize.Large);
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

Every Daisy control that declares a `StyledProperty<DaisySize>` named `SizeProperty` responds to global size changes automatically:

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

If your control declares a public static `StyledProperty<DaisySize>` named `SizeProperty`, it will **automatically** respond to global size changes once it is loaded. No manual subscription needed!

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
| Small | `DaisySizeSmallHeight` (28) | `DaisySizeSmallFontSize` (12) |
| Medium | `DaisySizeMediumHeight` (32) | `DaisySizeMediumFontSize` (14) |
| Large | `DaisySizeLargeHeight` (36) | `DaisySizeLargeFontSize` (18) |
| ExtraLarge | `DaisySizeExtraLargeHeight` (40) | `DaisySizeExtraLargeFontSize` (20) |

## Comparison: FlowerySizeManager vs FloweryScaleManager

| Feature | FlowerySizeManager | FloweryScaleManager |
| ------- | ------------------ | ------------------- |
| **Purpose** | User preference / accessibility | Responsive window sizing |
| **Scope** | Entire app (global) | Only `EnableScaling="True"` containers |
| **Scaling** | Discrete tiers (XS, S, M, L, XL) | Continuous (0.5× to 1.0×) |
| **Trigger** | User selection | Automatic (window resize) |
| **Propagation** | Automatic, per loaded control | Manual container opt-in |
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

1. **Load DaisyUITheme first** - The theme registers the size manager; no `MainWindow` setup is needed
2. **Call RefreshAllSizes only for early controls** - Controls created before the theme was loaded are picked up by the window walk
3. **Start with Medium** - The default matches the class defaults; use `Small` for dense desktop apps
4. **Provide a size picker** - Use `DaisySizeDropdown` for user control
5. **Test all sizes** - Ensure layouts work at ExtraSmall and ExtraLarge
6. **Use design tokens** - Not hardcoded values
7. **Use ResponsiveFont for TextBlocks** - Not DynamicResource
8. **Don't subscribe to SizeChanged for sizing** - Controls with a `Size` property are handled automatically
9. **Use explicit Size for demos** - Gallery size examples should set `Size="Large"` etc. directly
10. **Use IgnoreGlobalSize for size demo containers** - Prevents demo controls from responding to global changes
