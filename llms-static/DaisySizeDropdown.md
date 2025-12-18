# DaisySizeDropdown

A dropdown control for selecting a global size (`DaisySize`) that affects (almost) all Flowery.NET controls.

## Overview

`DaisySizeDropdown` provides a user-friendly way to let users choose their preferred UI size. When the user selects a size, it's applied globally via `FlowerySizeManager`. All Daisy controls with a `Size` property automatically update.

## Basic Usage

```xml
xmlns:controls="clr-namespace:Flowery.Controls;assembly=Flowery.NET"

<controls:DaisySizeDropdown Width="180" />
```

This displays a dropdown with all five sizes (ExtraSmall, Small, Medium, Large, ExtraLarge) using localized display names.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SelectedSize` | `DaisySize` | `Medium` | The currently selected size |
| `Size` | `DaisySize` | `Medium` | The size of the dropdown control itself |
| `ShowAbbreviations` | `bool` | `false` | Show abbreviations (XS, S, M, L, XL) instead of full names |
| `SizeOptions` | `IList<SizePreviewInfo>` | `null` | Custom size options to display |

---

## Customizing Available Sizes

By default, all five sizes are shown. You can customize which sizes appear and what they're called using the `SizeOptions` property.

### SizePreviewInfo Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Size` | `DaisySize` | - | The enum value (required) |
| `Name` | `string` | - | Internal name for localization lookup (e.g., "Small") |
| `IsVisible` | `bool` | `true` | Set to `false` to hide this size from the dropdown |
| `DisplayNameOverride` | `string?` | `null` | Custom label that overrides localized text |
| `Abbreviation` | `string` | - | Short form for compact display (XS, S, M, L, XL) |

### Example: Show Only 3 Sizes with Custom Names

```xml
<controls:DaisySizeDropdown>
    <controls:DaisySizeDropdown.SizeOptions>
        <x:Array Type="{x:Type controls:SizePreviewInfo}">
            <controls:SizePreviewInfo Size="Small" Name="Small" 
                                      DisplayNameOverride="Compact" Abbreviation="C" />
            <controls:SizePreviewInfo Size="Medium" Name="Medium" 
                                      DisplayNameOverride="Normal" Abbreviation="N" />
            <controls:SizePreviewInfo Size="Large" Name="Large" 
                                      DisplayNameOverride="Large" Abbreviation="L" />
        </x:Array>
    </controls:DaisySizeDropdown.SizeOptions>
</controls:DaisySizeDropdown>
```

### Example: Hide ExtraSmall and ExtraLarge

```xml
<controls:DaisySizeDropdown>
    <controls:DaisySizeDropdown.SizeOptions>
        <x:Array Type="{x:Type controls:SizePreviewInfo}">
            <controls:SizePreviewInfo Size="ExtraSmall" Name="ExtraSmall" IsVisible="False" />
            <controls:SizePreviewInfo Size="Small" Name="Small" />
            <controls:SizePreviewInfo Size="Medium" Name="Medium" />
            <controls:SizePreviewInfo Size="Large" Name="Large" />
            <controls:SizePreviewInfo Size="ExtraLarge" Name="ExtraLarge" IsVisible="False" />
        </x:Array>
    </controls:DaisySizeDropdown.SizeOptions>
</controls:DaisySizeDropdown>
```

---

## Localization

Size names are automatically localized based on the current culture. The display name resolution order is:

1. **DisplayNameOverride** (if set) – your custom label
2. **Localized text** – from `FloweryLocalization` (e.g., "Klein" in German)
3. **Name** (fallback) – the enum name like "ExtraSmall"

### Supported Languages

Size names are localized in 12 languages:

- English, German, French, Spanish, Italian
- Japanese, Korean, Chinese (Simplified)
- Arabic, Turkish, Ukrainian, Hebrew

### Custom Localization Keys

If you need to override the built-in translations, add these keys to your localization:

```json
{
  "Size_ExtraSmall": "Extra Small",
  "Size_Small": "Small", 
  "Size_Medium": "Medium",
  "Size_Large": "Large",
  "Size_ExtraLarge": "Extra Large"
}
```

---

## Styling

The dropdown respects global size changes. When a new size is selected, the dropdown itself also updates its appearance.

### Size Variants

```xml
<!-- Use the dropdown's own Size property for its appearance -->
<controls:DaisySizeDropdown Size="Small" Width="120" />
<controls:DaisySizeDropdown Size="Large" Width="200" />
```

---

## Programmatic Control

If you need to change the size without the UI:

```csharp
using Flowery.Controls;

// Apply a size globally
FlowerySizeManager.ApplySize(DaisySize.Large);

// Or by name
FlowerySizeManager.ApplySize("Large");

// Check current size
DaisySize current = FlowerySizeManager.CurrentSize;
```

The dropdown automatically syncs with `FlowerySizeManager.CurrentSize`.

---

## Integration with FlowerySizeManager

`DaisySizeDropdown` is designed to work seamlessly with the global sizing system:

- **On selection change**: Calls `FlowerySizeManager.ApplySize()`
- **On external size change**: Syncs to match the current global size
- **On culture change**: Refreshes localized display names

For comprehensive documentation on the sizing system, see [SizingScaling](https://tobitege.github.io/Flowery.NET/#SizingScaling).

---

## Best Practices

1. **Place in settings or sidebar** – Give users easy access to change the size preference.

2. **Consider visibility** – For most apps, 3 sizes (Small, Medium, Large) may be enough.

3. **Use meaningful names** – If "Small" doesn't fit your domain, use `DisplayNameOverride` (e.g., "Compact", "Dense").

4. **Test all sizes** – Ensure your layouts work at both ExtraSmall and ExtraLarge.

5. **Persist preference** – Save the user's selection and restore it on app startup.
