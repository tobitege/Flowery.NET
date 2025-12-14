# Flowery.NET Localization Guide

Flowery.NET provides built-in localization support for theme names and control text, with easy extensibility for additional languages.

## Supported Languages

The library comes with built-in translations for the following languages:

- 🇺🇸 **English** (Default)
- 🇩🇪 **German** (`de`)
- 🇫🇷 **French** (`fr`)
- 🇪🇸 **Spanish** (`es`)
- 🇮🇹 **Italian** (`it`)
- 🇨🇳 **Mandarin Simplified** (`zh-CN`)
- 🇰🇷 **Korean** (`ko`)
- 🇯🇵 **Japanese** (`ja`)
- 🇸🇦 **Arabic** (`ar`)
- 🇹🇷 **Turkish** (`tr`)
- 🇺🇦 **Ukrainian** (`uk`)

## Quick Start

### Switching Languages at Runtime

```csharp
using Flowery.Localization;

// Switch to German
FloweryLocalization.SetCulture("de");

// Switch to Mandarin
FloweryLocalization.SetCulture("zh-CN");
```

### Date Formatting

The `DaisyDateTimeline` control uses the `Locale` property for date formatting:

```xml
<controls:DaisyDateTimeline Locale="de-DE" />
```

Or bind to the current UI culture:

```csharp
timeline.Locale = FloweryLocalization.CurrentCulture;
```

---

## ⚠️ WebAssembly (Browser) Localization

**CRITICAL:** RESX-based satellite assemblies do **NOT** work reliably in Avalonia WebAssembly. The .NET WASM runtime does not automatically load satellite resource DLLs, causing localization to silently fail.

### The Solution: JSON-Based Localization

For browser apps, use embedded JSON files instead of RESX. This approach:

- ✅ Works in Desktop, Browser, and Android
- ✅ No satellite assembly loading issues
- ✅ Full CJK/Arabic character support
- ✅ AOT/trimming compatible with source generators

### Step-by-Step: Browser Localization Setup

#### 1. Create JSON Translation Files

Create JSON files in your `Localization/` folder:

**`Localization/en.json`** (English - fallback):

```json
{
  "Effects_Reveal_Title": "Reveal Effect",
  "Effects_Reveal_Description": "Entrance animations when element enters the visual tree."
}
```

**`Localization/de.json`** (German):

```json
{
  "Effects_Reveal_Title": "Enthüllungseffekt",
  "Effects_Reveal_Description": "Eingangsanimationen wenn ein Element in den visuellen Baum eintritt."
}
```

**`Localization/ja.json`** (Japanese):

```json
{
  "Effects_Reveal_Title": "リビール効果",
  "Effects_Reveal_Description": "要素がビジュアルツリーに入るときの登場アニメーション。"
}
```

#### 2. Embed JSON Files in .csproj

Add all JSON files as embedded resources:

```xml
<ItemGroup>
  <EmbeddedResource Include="Localization\en.json" />
  <EmbeddedResource Include="Localization\de.json" />
  <EmbeddedResource Include="Localization\ja.json" />
  <!-- Add all other languages -->
</ItemGroup>
```

#### 3. Create JSON Localization Loader

Create a localization class that loads from embedded JSON:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public class GalleryLocalization : INotifyPropertyChanged
{
    private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
    private static readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private static readonly Lazy<GalleryLocalization> _instance = new(() => new GalleryLocalization());

    public static GalleryLocalization Instance => _instance.Value;
    public static event EventHandler<CultureInfo>? CultureChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    static GalleryLocalization()
    {
        // Load all translations at startup
        LoadTranslation("en");
        LoadTranslation("de");
        LoadTranslation("ja");
        // Add all other languages...
        
        // Sync with Flowery library culture changes
        Flowery.Localization.FloweryLocalization.CultureChanged += (s, c) => SetCulture(c);
    }

    // Indexer for XAML binding
    public string this[string key] => GetString(key);

    public static void SetCulture(CultureInfo culture)
    {
        if (_currentCulture.Name == culture.Name) return;
        _currentCulture = culture;
        CultureChanged?.Invoke(null, culture);
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item"));
    }

    public static string GetString(string key)
    {
        // Try exact culture (e.g., "de-DE")
        if (_translations.TryGetValue(_currentCulture.Name, out var exact) && exact.TryGetValue(key, out var v1))
            return v1;
        
        // Try language only (e.g., "de")
        var lang = _currentCulture.TwoLetterISOLanguageName;
        if (_translations.TryGetValue(lang, out var langDict) && langDict.TryGetValue(key, out var v2))
            return v2;
        
        // Fallback to English
        if (_translations.TryGetValue("en", out var en) && en.TryGetValue(key, out var v3))
            return v3;
        
        return key; // Return key if not found
    }

    private static void LoadTranslation(string langCode)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"YourApp.Localization.{langCode}.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return;
        
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        
        // Use source generator for AOT compatibility
        var dict = JsonSerializer.Deserialize(json, LocalizationJsonContext.Default.DictionaryStringString);
        if (dict != null) _translations[langCode] = dict;
    }
}

// REQUIRED: JSON Source Generator for AOT/WASM compatibility
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocalizationJsonContext : JsonSerializerContext { }
```

**⚠️ IMPORTANT:** The `JsonSerializerContext` source generator is **required** for WASM. Without it, you'll get `JsonSerializerIsReflectionDisabled` errors.

#### 4. Create XAML Markup Extension

Create a markup extension that updates when culture changes:

```csharp
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

public class LocalizeExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }
    public LocalizeExtension(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key)) return "[Missing Key]";

        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        
        if (target?.TargetObject is AvaloniaObject obj && 
            target.TargetProperty is AvaloniaProperty prop)
        {
            var value = GalleryLocalization.GetString(Key);
            
            // Subscribe to culture changes
            GalleryLocalization.CultureChanged += (s, c) =>
            {
                obj.SetValue(prop, GalleryLocalization.GetString(Key));
            };
            
            return value;
        }

        // Fallback to binding
        return new Binding($"[{Key}]")
        {
            Source = GalleryLocalization.Instance,
            Mode = BindingMode.OneWay
        };
    }
}
```

#### 5. Use in XAML

```xml
<UserControl xmlns:loc="clr-namespace:YourApp.Localization">
    <TextBlock Text="{loc:Localize Effects_Reveal_Title}" />
</UserControl>
```

---

## CJK Font Support (Japanese, Korean, Chinese)

For CJK characters to display correctly in the browser, you must embed fonts and apply styles in your **consuming application**.

> **⚠️ Important:** Flowery.NET library does **NOT** ship with CJK fonts. Each consuming application that needs CJK support must embed its own fonts. This is because:
>
> - Flowery.NET controls inherit fonts from the application context (they don't have hardcoded fonts)
> - Font files are large (~3-5 MB) and not all apps need CJK support
> - Keeping fonts in the app gives you control over which languages to support

### 1. Embed CJK Fonts in Your App

Include Noto Sans CJK fonts in your application's `.csproj`:

```xml
<ItemGroup>
  <AvaloniaResource Include="Assets\Fonts\NotoSansJP-Regular.otf" />
  <AvaloniaResource Include="Assets\Fonts\NotoSansSC-Regular.otf" />
  <AvaloniaResource Include="Assets\Fonts\NotoSansKR-Regular.otf" />
</ItemGroup>
```

### 2. Define Font Family Resource

In `App.axaml`:

```xml
<Application.Resources>
    <FontFamily x:Key="NotoSansFamily">
        fonts:YourApp#Noto Sans,
        fonts:YourApp#Noto Sans CJK SC,
        fonts:YourApp#Noto Sans CJK JP,
        fonts:YourApp#Noto Sans CJK KR,
        fonts:YourApp#Noto Sans Arabic
    </FontFamily>
</Application.Resources>
```

### 3. Apply Font to All Text Controls

**CRITICAL:** You must explicitly apply the font to each text-displaying control type. `Application` and `Control` base classes do NOT have `FontFamily` property.

```xml
<Application.Styles>
    <FluentTheme />
    <daisy:DaisyUITheme />
    
    <!-- Apply CJK font to all text-displaying controls -->
    <Style Selector="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="TextBox">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="Button">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="ComboBox">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="ComboBoxItem">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="Label">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="ContentControl">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <!-- Add your custom controls -->
    <Style Selector="controls|DaisyButton">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
    <Style Selector="controls|DaisySelect">
        <Setter Property="FontFamily" Value="{StaticResource NotoSansFamily}" />
    </Style>
</Application.Styles>
```

### 4. Browser Project Settings (Optional)

The `.Browser.csproj` requires minimal configuration for JSON-based localization. Add these only if you need specific behavior:

```xml
<PropertyGroup>
    <!-- Only needed if you want date/number formatting for all cultures -->
    <!-- Note: This increases build time significantly -->
    <WasmIncludeFullIcuData>true</WasmIncludeFullIcuData>
    
    <!-- Required for proper culture handling (not invariant) -->
    <InvariantGlobalization>false</InvariantGlobalization>
</PropertyGroup>
```

> **Note:** With JSON-based localization, you do NOT need `SatelliteResourceLanguages`, `WasmCopyResourcesToPublishDirectory`, or other RESX-related settings.

---

## Desktop/Legacy Localization (RESX) - Deprecated

> **⚠️ Note:** Flowery.NET v1.5+ uses **JSON-based localization** instead of RESX. This ensures compatibility with WebAssembly (browser) where RESX satellite assemblies don't work. The JSON approach works on all platforms (Desktop, Browser, Android).
>
> RESX files have been removed from the library. If you have a desktop-only app and prefer RESX, you can still create your own `.resx` files, but JSON is recommended for consistency.

---

## Utility Scripts

Two helper scripts exist under `Utils/` for localization maintenance.

### Convert RESX to JSON

Convert existing RESX files to JSON format:

```bash
python "Utils/convert_resx_to_json.py"
```

### Check for Missing Keys (Legacy)

```bash
python "Utils/sync_resx_keys.py" "Flowery.NET/Localization/FloweryStrings.resx" "Flowery.NET/Localization"
```

---

## Available Resource Keys

| Key | English (Default) | Description |
|-----|-------------------|-------------|
| `Select_Placeholder` | Pick one | DaisySelect default placeholder |
| `Theme_*` | Theme names | Localized display names for all 35 themes |
| `Accessibility_Loading` | Loading | DaisyLoading accessible text |
| `Accessibility_Progress` | Progress | DaisyProgress accessible text |
| `Accessibility_Rating` | Rating | DaisyRating accessible text |

---

## Handling Culture Changes

When culture changes, bound strings need to update. The `LocalizeExtension` handles this automatically by subscribing to `CultureChanged` and directly setting property values.

For ViewModel properties:

```csharp
public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        FloweryLocalization.CultureChanged += (s, culture) =>
        {
            OnPropertyChanged(nameof(Greeting));
        };
    }

    public string Greeting => FloweryLocalization.GetString("Greeting_Text");
}
```

---

## Troubleshooting

### Browser shows empty text after switching language

**Cause:** Bindings to indexers don't refresh in WASM.
**Fix:** Use the `LocalizeExtension` that directly sets property values via `IProvideValueTarget`.

### Japanese/Korean/Chinese shows as boxes (▢▢▢)

**Cause:** CJK fonts not loaded or not applied.
**Fix:**

1. Embed Noto Sans CJK fonts as `AvaloniaResource`
2. Apply font to ALL text-displaying controls in `App.axaml`

### `JsonSerializerIsReflectionDisabled` error

**Cause:** WASM has Native AOT which disables reflection.
**Fix:** Add a `JsonSerializerContext` source generator (see Step 3 above).

### Browser takes 2+ minutes to build

**Cause:** Native AOT compilation.
**Fix:** Disable for Debug builds:

```xml
<WasmBuildNative Condition="'$(Configuration)' == 'Release'">true</WasmBuildNative>
```

### Satellite assemblies not found in browser

**Cause:** RESX doesn't work in WASM.
**Fix:** Switch to JSON-based localization (this entire guide).

---

## Contributing Translations

1. Fork the repository
2. Add your JSON/RESX file for your language
3. Submit a pull request

Contributions for any language are welcome!
