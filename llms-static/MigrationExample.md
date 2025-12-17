# Migration Guide: Integrating Flowery.NET into Existing Apps

This guide explains when and how to use `CustomThemeApplicator` to integrate Flowery.NET's theme controls into apps with existing theming architectures. For general theme management, see [DaisyThemeManager](controls/DaisyThemeManager.html).

## When You Need CustomThemeApplicator

The default `DaisyThemeManager.ApplyTheme()` works by adding/removing palette resources from `Application.Resources.MergedDictionaries`. This works great for simple apps, but you may need `CustomThemeApplicator` if:

| Scenario | Why Default Doesn't Work |
|----------|-------------------------|
| **Custom MergedDictionaries** in `Application.Resources` | Resource resolution conflicts; `DynamicResource` bindings don't refresh |
| **In-place ThemeDictionary updates** required | Default adds to MergedDictionaries, not ThemeDictionaries |
| **Theme persistence** needed | Default doesn't save to settings |
| **Additional actions** on theme change | Logging, analytics, UI updates, etc. |
| **Complex resource hierarchies** | Multiple ResourceDictionaries that need coordinated updates |

If your app works fine with the default behavior, you don't need `CustomThemeApplicator`.

---

## Example: App with Custom Resources

### The Starting Point

You have an Avalonia app with custom resources in `Application.Resources`:

```xml
<!-- App.axaml (before Flowery.NET) -->
<Application ...>
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceInclude Source="avares://MyApp/Themes/CustomResources.axaml" />
                <ResourceInclude Source="avares://MyApp/Themes/Icons.axaml" />
            </ResourceDictionary.MergedDictionaries>
            <SolidColorBrush x:Key="MyCustomBrush" Color="#808080"/>
        </ResourceDictionary>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme />
        <StyleInclude Source="avares://MyApp/Themes/AppStyles.axaml" />
    </Application.Styles>
</Application>
```

### Step 1: Add Flowery.NET

You add `DaisyUITheme` and start using Daisy resources.

> **Namespace Note:** Flowery.NET uses two namespaces:

> - `Flowery` - For `DaisyUITheme` only (in `App.axaml`)
> - `Flowery.Controls` - For all controls (in views like `MainWindow.axaml`)

```xml
<!-- App.axaml (with Flowery.NET added) -->
<Application ...
             xmlns:daisy="clr-namespace:Flowery;assembly=Flowery.NET"
             RequestedThemeVariant="Dark">
    <Application.Resources>
        <!-- Your existing resources remain -->
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceInclude Source="avares://MyApp/Themes/CustomResources.axaml" />
                <ResourceInclude Source="avares://MyApp/Themes/Icons.axaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme />
        <daisy:DaisyUITheme />  <!-- Add this -->
        <StyleInclude Source="avares://MyApp/Themes/AppStyles.axaml" />
    </Application.Styles>
</Application>
```

Your views now use Daisy resources:

```xml
<Border Background="{DynamicResource DaisyBase200Brush}"
        BorderBrush="{DynamicResource DaisyBase300Brush}">
    <TextBlock Foreground="{DynamicResource DaisyBaseContentBrush}" 
               Text="Hello Flowery!"/>
</Border>
```

### Step 2: Add Theme Dropdown - The Problem Appears

You add `DaisyThemeDropdown` for runtime theme switching:

```xml
<controls:DaisyThemeDropdown Width="220"/>
```

**What happens:**

- The dropdown appears ✓
- You select "Synthwave" from the list ✓
- **Nothing changes** ✗

The colors don't update. If you restart the app, it's still using the old theme.

### Why It Doesn't Work

`DaisyThemeDropdown` internally calls `DaisyThemeManager.ApplyTheme()`, which does this:

```csharp
// Removes old palette from MergedDictionaries
app.Resources.MergedDictionaries.Remove(_currentPalette);

// Adds new palette to MergedDictionaries  
app.Resources.MergedDictionaries.Add(newPalette);
```

**If your app has custom MergedDictionaries** in `Application.Resources`, the new palette gets added but Avalonia's `DynamicResource` bindings may not refresh properly because:

1. Resources are added to `MergedDictionaries`, not updated in-place within the active `ThemeDictionary`
2. Avalonia's resource resolution doesn't always trigger a full refresh for this type of change
3. Views may still see cached brush values

**Note:** This issue depends on your specific resource setup. Some apps work fine with the default; others need in-place updates.

---

## The Solution: CustomThemeApplicator

### Step 1: Create Your Custom Applicator

Write a method that applies themes the way your app needs:

```csharp
// App.axaml.cs
public static bool ApplyThemeInPlace(string themeName)
{
    var themeInfo = DaisyThemeManager.GetThemeInfo(themeName);
    if (themeInfo == null) return false;

    var app = Application.Current;
    if (app?.Resources == null) return false;

    // Load the palette
    var paletteUri = new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{themeInfo.Name}.axaml");
    var palette = (ResourceDictionary)AvaloniaXamlLoader.Load(paletteUri);

    // Determine target variant
    var targetVariant = themeInfo.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

    // Update resources IN-PLACE within the ThemeDictionary
    if (app.Resources.ThemeDictionaries.TryGetValue(targetVariant, out var themeDict)
        && themeDict is IResourceDictionary dict)
    {
        foreach (var kvp in palette)
            dict[kvp.Key] = kvp.Value;
    }

    // Switch variant to trigger Avalonia's refresh
    app.RequestedThemeVariant = targetVariant;

    // Optional: if your app has a settings object for persistence, save here
    // MySettings.Current.Theme = themeName;
    // MySettings.Save();

    return true;
}
```

### Step 2: Wire It Up at Startup

```csharp
// App.axaml.cs
public override void OnFrameworkInitializationCompleted()
{
    // One line: tell Flowery.NET to use your applicator
    DaisyThemeManager.CustomThemeApplicator = ApplyThemeInPlace;

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
    }

    base.OnFrameworkInitializationCompleted();
}
```

### Result

**Now all Flowery.NET theme controls work with your custom logic:**

- `DaisyThemeDropdown` works ✓
- `DaisyThemeController` works ✓  
- `DaisyThemeRadio` works ✓
- `DaisyThemeSwap` works ✓
- Theme persists across restarts ✓
- All `DynamicResource` bindings refresh properly ✓

---

## Other Use Cases

### Persistence Only

If the default theme application works but you want to save the selected theme to your app's settings:

```csharp
DaisyThemeManager.CustomThemeApplicator = themeName =>
{
    // Apply theme using default-style logic
    var result = ApplyThemeDefault(themeName); // Your copy of default logic
    if (result)
    {
        // If your app has a settings object, persist the theme here (example)
        // AppSettings.Current.Theme = themeName;
        // AppSettings.Save();
    }
    return result;
};
```

### Logging/Analytics

```csharp
DaisyThemeManager.CustomThemeApplicator = themeName =>
{
    // Use your logging framework (Serilog, NLog, Microsoft.Extensions.Logging, etc.)
    Logger.Info($"Theme changing to: {themeName}");
    
    // Optional: track with your analytics service
    Analytics.Track("theme_changed", new { theme = themeName });
    
    // Do the actual theme application
    return ApplyThemeInPlace(themeName);
};
```

### Chained Actions

This is just a code example to demonstrate what *might* be used.

```csharp
DaisyThemeManager.CustomThemeApplicator = themeName =>
{
    var result = ApplyThemeInPlace(themeName);
    if (result)
    {
        // Update other UI elements
        MainViewModel.Instance.RefreshColors();
        
        // Notify plugins
        PluginManager.OnThemeChanged(themeName);
    }
    return result;
};
```

---

## Summary

| Before CustomThemeApplicator | After |
|------------------------------|-------|
| Built-in theme controls don't work with custom resource setups | All controls work seamlessly |
| Had to build custom theme UI | Use `DaisyThemeDropdown` etc. directly |
| Theme persistence required separate handling | Persistence built into your applicator |
| Complex workarounds or library modifications | Single line of setup |

The `CustomThemeApplicator` pattern gives you full control over how themes are applied while still benefiting from Flowery.NET's theme UI controls.
