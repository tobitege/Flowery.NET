<!-- Supplementary documentation for LocalizeExtensionBase -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

LocalizeExtensionBase is an abstract base class for creating localization markup extensions in Avalonia applications. It provides the core implementation for XAML localization with runtime culture switching support.

## Why Use This?

Instead of duplicating localization extension code across projects, inherit from `LocalizeExtensionBase` and plug in your application's localization source. The base class handles:

- Target property detection via `IProvideValueTarget`
- Automatic subscription to culture change events
- Runtime UI updates when language changes
- Binding fallback for complex scenarios

## Creating Your LocalizeExtension

```csharp
using Flowery.Localization;
using System;
using System.Globalization;

namespace MyApp.Localization
{
    public class LocalizeExtension : LocalizeExtensionBase
    {
        public LocalizeExtension() { }
        
        public LocalizeExtension(string key) : base(key) { }

        protected override string GetLocalizedString(string key)
            => MyAppLocalization.GetString(key);

        protected override void SubscribeToCultureChanged(EventHandler<CultureInfo> handler)
            => MyAppLocalization.CultureChanged += handler;

        protected override object? GetBindingSource()
            => MyAppLocalization.Instance;
    }
}
```

## XAML Usage

```xml
<!-- Add namespace -->
xmlns:loc="clr-namespace:MyApp.Localization"

<!-- Use the extension -->
<TextBlock Text="{loc:Localize Button_Save}"/>
<Button Content="{loc:Localize Menu_File}"/>
```

## Requirements for Your Localization Source

Your localization class (e.g., `MyAppLocalization`) needs:

1. **GetString method**: Returns localized string for a key
2. **CultureChanged event**: Fires when UI culture changes  
3. **Instance property** (optional): Singleton with indexer for binding fallback

Example localization class structure:

```csharp
public class MyAppLocalization : INotifyPropertyChanged
{
    public static MyAppLocalization Instance { get; } = new();
    
    public static event EventHandler<CultureInfo>? CultureChanged;
    
    public string this[string key] => GetString(key);
    
    public static string GetString(string key)
    {
        // Your localization lookup logic
    }
    
    public static void SetCulture(CultureInfo culture)
    {
        // Update current culture
        CultureChanged?.Invoke(null, culture);
    }
}
```

## How Runtime Updates Work

1. When `{loc:Localize Key}` is processed, the extension:
   - Gets the initial localized value
   - Subscribes to `CultureChanged` event
   - Returns the value to the target property

2. When culture changes:
   - Your localization source fires `CultureChanged`
   - The extension retrieves the new localized string
   - Updates the target property directly via `SetValue`

## Base Class API

| Member | Description |
|--------|-------------|
| `Key` | The resource key to look up |
| `GetLocalizedString(key)` | Abstract - return localized string for key |
| `SubscribeToCultureChanged(handler)` | Abstract - subscribe to culture changes |
| `GetBindingSource()` | Virtual - return indexer source for fallback binding |

## Tips

- Your `LocalizeExtension` class should be in a namespace that's easy to reference in XAML
- Use short XML namespace aliases like `loc:` for cleaner markup
- The extension works with any `AvaloniaProperty` (Text, Content, Header, etc.)

## Related: CustomResolver for Library Controls

If you need to provide translations for Flowery.NET library controls (like `FloweryComponentSidebar`), use the `CustomResolver`:

```csharp
// In your app's localization static constructor:
FloweryLocalization.CustomResolver = MyAppLocalization.GetString;
```

This allows library controls to resolve app-specific keys (like `Sidebar_Home`) through your localization source, while the library's internal keys (`Size_*`, `Theme_*`) continue to work from the library's resources.
