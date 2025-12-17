<!-- Supplementary documentation for DaisyTabs -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyTabs is a styled `TabControl` with four header variants (None, Bordered, Lifted, Boxed) and size presets. It uses a WrapPanel for tab headers and supports standard tab behaviors (`SelectedIndex`, `Items`, `TabItem` content).

## Variant Options

| Variant | Description |
|---------|-------------|
| None | Text-only tabs; selected text is bold; no underline/box. |
| Bordered (default) | Underline on hover/selected. |
| Lifted | Folder-tab style with top/sides border; selected tab has background. |
| Boxed | Rounded pills with visible borders; selected uses primary border. |

## Size Options

| Size | Padding | Font Size |
|------|---------|-----------|
| ExtraSmall | 8,4 | 10 |
| Small | 12,6 | 12 |
| Medium (default) | 16,8 | 14 |
| Large | 20,12 | 18 |

## Tab Width Options

Control tab sizing behavior to prevent layout shifts or create uniform navigation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| TabWidthMode | Enum | Auto | `Auto`, `Equal`, or `Fixed` |
| TabWidth | double | NaN | Fixed width when mode is `Fixed` |
| TabMinWidth | double | 0 | Minimum width for each tab |
| TabMaxWidth | double | ∞ | Maximum width for each tab |

| Mode | Behavior |
|------|----------|
| Auto | Each tab sizes to fit its content (default) |
| Equal | All tabs use the width of the widest tab |
| Fixed | All tabs use the specified `TabWidth` value |

## Quick Examples

```xml
<!-- Bordered (default) -->
<controls:DaisyTabs>
    <TabItem Header="Tab 1"><TextBlock Text="Content 1" Margin="8" /></TabItem>
    <TabItem Header="Tab 2"><TextBlock Text="Content 2" Margin="8" /></TabItem>
</controls:DaisyTabs>

<!-- Boxed -->
<controls:DaisyTabs Variant="Boxed">
    <TabItem Header="Tab 1"><TextBlock Text="Content 1" Margin="8" /></TabItem>
    <TabItem Header="Tab 2"><TextBlock Text="Content 2" Margin="8" /></TabItem>
</controls:DaisyTabs>

<!-- Lifted, small -->
<controls:DaisyTabs Variant="Lifted" Size="Small">
    <TabItem Header="Tab 1"><TextBlock Text="Content 1" Margin="8" /></TabItem>
    <TabItem Header="Tab 2"><TextBlock Text="Content 2" Margin="8" /></TabItem>
</controls:DaisyTabs>

<!-- Equal width tabs (prevents layout shift) -->
<controls:DaisyTabs Variant="Boxed" TabWidthMode="Equal">
    <TabItem Header="Short"><TextBlock Text="Content 1" Margin="8" /></TabItem>
    <TabItem Header="Much Longer Tab"><TextBlock Text="Content 2" Margin="8" /></TabItem>
    <TabItem Header="Med"><TextBlock Text="Content 3" Margin="8" /></TabItem>
</controls:DaisyTabs>

<!-- Fixed width tabs -->
<controls:DaisyTabs Variant="Boxed" TabWidthMode="Fixed" TabWidth="120">
    <TabItem Header="Tab 1"><TextBlock Text="Content 1" Margin="8" /></TabItem>
    <TabItem Header="Tab 2"><TextBlock Text="Content 2" Margin="8" /></TabItem>
</controls:DaisyTabs>

<!-- Auto with max width constraint -->
<controls:DaisyTabs TabMaxWidth="150">
    <TabItem Header="This is a very long tab header"><TextBlock Text="Content" Margin="8" /></TabItem>
    <TabItem Header="Short"><TextBlock Text="Content" Margin="8" /></TabItem>
</controls:DaisyTabs>
```

## Tab Colors

DaisyTabs supports two different tab-color systems:

- **Palette colors (recommended)**: end-user friendly, **theme-independent**, fixed colors. Use `DaisyTabs.TabPaletteColor`.
- **Semantic colors**: theme-aware (Primary/Success/…), matches DaisyUI semantics. Use `DaisyTabs.TabColor` if you want colors to change with themes.

### Palette Colors (Theme-Independent, Recommended)

Assign a palette color to individual tabs using the `DaisyTabs.TabPaletteColor` attached property. Palette colors are fixed and do not change with themes.

| Palette Value | Description |
|--------------|-------------|
| Default | No custom color |
| Purple | Purple |
| Indigo | Indigo |
| Pink | Pink |
| SkyBlue | Light blue / sky blue |
| Blue | Blue |
| Lime | Lime (highlighter green) |
| Green | Green |
| Yellow | Yellow |
| Orange | Orange |
| Red | Red |
| Gray | Gray |

#### Resetting Palette Color to Default

To reset a tab's palette color, set `TabPaletteColor` to `Default`:

```csharp
// Reset programmatically
DaisyTabs.SetTabPaletteColor(tabItem, DaisyTabPaletteColor.Default);
```

```xml
<!-- Reset in XAML -->
<TabItem Header="My Tab" controls:DaisyTabs.TabPaletteColor="Default" />
```

When using the context menu, clicking the hollow dot ("Default") resets the tab color.

#### Palette Color Examples

```xml
<!-- Direct TabItem usage with palette colors -->
<controls:DaisyTabs Variant="Boxed">
    <TabItem Header="Home" controls:DaisyTabs.TabPaletteColor="Blue">
        <TextBlock Text="Home content" Margin="8" />
    </TabItem>
    <TabItem Header="Settings" controls:DaisyTabs.TabPaletteColor="Purple">
        <TextBlock Text="Settings content" Margin="8" />
    </TabItem>
    <TabItem Header="Errors" controls:DaisyTabs.TabPaletteColor="Red">
        <TextBlock Text="Error log" Margin="8" />
    </TabItem>
</controls:DaisyTabs>

<!-- ItemsSource with palette colors via ItemContainerTheme -->
<controls:DaisyTabs ItemsSource="{Binding Tabs}" Variant="Bordered">
    <controls:DaisyTabs.ItemContainerTheme>
        <ControlTheme TargetType="TabItem" BasedOn="{StaticResource {x:Type TabItem}}">
            <Setter Property="(controls:DaisyTabs.TabPaletteColor)" 
                    Value="{Binding Color}" />
        </ControlTheme>
    </controls:DaisyTabs.ItemContainerTheme>
</controls:DaisyTabs>
```

### Semantic Colors (Theme-Aware)

If you want tab colors to follow the active theme, use `DaisyTabs.TabColor` with `DaisyColor` values:

| DaisyColor | Description |
|-----------|-------------|
| Default | No semantic color |
| Primary | Theme primary |
| Secondary | Theme secondary |
| Accent | Theme accent |
| Neutral | Theme neutral |
| Info | Theme info |
| Success | Theme success |
| Warning | Theme warning |
| Error | Theme error |

## Context Menu

Enable a right-click context menu on tabs with close actions and color selection. The menu is **off by default** and only raises events/callbacks - your app handles the actual tab manipulation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| EnableTabContextMenu | bool | false | Shows context menu on right-click |

### Context Menu Items

When enabled, right-clicking a tab shows:

- **Close Tab** – Requests closing the clicked tab
- **Close Other Tabs** – Requests closing all tabs except the clicked one
- **Close Tabs to the Right** – Requests closing tabs after the clicked one
- **Tab Color** – Inline color dots (palette). Click a dot to apply it; click the hollow dot to reset.

### Events & Callbacks

DaisyTabs raises events and supports optional callbacks for each action. Use events for standard .NET patterns or callbacks for simpler inline wiring.

| Event | Callback Property | EventArgs | Description |
|-------|-------------------|-----------|-------------|
| CloseTabRequested | CloseTabCallback | DaisyTabEventArgs | User requested closing a tab |
| CloseOtherTabsRequested | CloseOtherTabsCallback | DaisyTabEventArgs | User requested closing other tabs |
| CloseTabsToRightRequested | CloseTabsToRightCallback | DaisyTabEventArgs | User requested closing tabs to the right |
| TabPaletteColorChangeRequested | TabPaletteColorChangeCallback | DaisyTabPaletteColorChangedEventArgs | User selected a new palette color |
| TabColorChangeRequested | TabColorChangeCallback | DaisyTabColorChangedEventArgs | User selected a new semantic color (theme-aware) |

**DaisyTabEventArgs** properties:

- `TabItem` – The TabItem control
- `TabIndex` – Index in the Items collection
- `DataItem` – The bound data item (if using ItemsSource)

**DaisyTabPaletteColorChangedEventArgs** adds:

- `NewColor` – The selected `DaisyTabPaletteColor` value

**DaisyTabColorChangedEventArgs** adds (semantic):

- `NewColor` – The selected DaisyColor value

### Context Menu Example

```xml
<controls:DaisyTabs x:Name="DocumentTabs"
                    Variant="Boxed"
                    EnableTabContextMenu="True"
                    CloseTabRequested="OnCloseTab"
                    CloseOtherTabsRequested="OnCloseOtherTabs"
                    TabPaletteColorChangeRequested="OnTabPaletteColorChange">
    <TabItem Header="Document 1"><TextBlock Text="Content 1" /></TabItem>
    <TabItem Header="Document 2"><TextBlock Text="Content 2" /></TabItem>
    <TabItem Header="Document 3"><TextBlock Text="Content 3" /></TabItem>
</controls:DaisyTabs>

<!-- Toast for feedback -->
<controls:DaisyToast x:Name="TabToast" 
                     HorizontalAlignment="Center" 
                     VerticalAlignment="Bottom" />

<!-- Modal for confirmations -->
<controls:DaisyModal x:Name="ConfirmModal" IsOpen="False">
    <StackPanel Spacing="16">
        <TextBlock x:Name="ConfirmText" FontWeight="SemiBold" />
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <controls:DaisyButton Content="Cancel" Click="OnCancelConfirm" />
            <controls:DaisyButton Content="Close Tabs" Variant="Error" Click="OnConfirmClose" />
        </StackPanel>
    </StackPanel>
</controls:DaisyModal>
```

```csharp
// Code-behind example
private void OnCloseTab(object? sender, DaisyTabEventArgs e)
{
    // Remove the tab from Items
    DocumentTabs.Items.Remove(e.TabItem);
    
    // Show feedback via DaisyToast
    TabToast.Items.Add(new DaisyAlert 
    { 
        Content = $"Closed '{e.TabItem.Header}'",
        Variant = DaisyAlertVariant.Info 
    });
}

private DaisyTabEventArgs? _pendingCloseArgs;

private void OnCloseOtherTabs(object? sender, DaisyTabEventArgs e)
{
    // Show confirmation via DaisyModal
    _pendingCloseArgs = e;
    var otherCount = DocumentTabs.Items.Count - 1;
    ConfirmText.Text = $"Close {otherCount} other tab(s)?";
    ConfirmModal.IsOpen = true;
}

private void OnConfirmClose(object? sender, RoutedEventArgs e)
{
    if (_pendingCloseArgs != null)
    {
        var keepTab = _pendingCloseArgs.TabItem;
        var toRemove = DocumentTabs.Items.OfType<TabItem>()
            .Where(t => t != keepTab).ToList();
        
        foreach (var tab in toRemove)
            DocumentTabs.Items.Remove(tab);
        
        DocumentTabs.SelectedItem = keepTab;
    }
    ConfirmModal.IsOpen = false;
    _pendingCloseArgs = null;
}

private void OnCancelConfirm(object? sender, RoutedEventArgs e)
{
    ConfirmModal.IsOpen = false;
    _pendingCloseArgs = null;
}

private void OnTabPaletteColorChange(object? sender, DaisyTabPaletteColorChangedEventArgs e)
{
    // Color is already applied to the attached property.
    // Optionally persist it:
    // _settings.SaveTabColor(GetTabKey(e.TabItem), e.NewColor);
}
```

## Persistence Hooks

DaisyTabs does **not** persist tab state itself. Instead, use the callbacks to save/load tab colors or other state via your app's settings or DI services.

### Persistence Recipe

```csharp
// In your App or ViewModel startup:
public void InitializeTabs(DaisyTabs tabs, ITabStateService stateService)
{
    // Load saved colors when tabs are created
    foreach (var tab in tabs.Items.OfType<TabItem>())
    {
        var key = GetTabKey(tab);
        var savedColor = stateService.GetTabColor(key);
        if (savedColor.HasValue)
            DaisyTabs.SetTabPaletteColor(tab, savedColor.Value);
    }
    
    // Save colors when changed
    tabs.TabPaletteColorChangeRequested += (_, e) =>
    {
        var key = GetTabKey(e.TabItem);
        stateService.SaveTabColor(key, e.NewColor);
    };
}

private string GetTabKey(TabItem tab)
{
    // Use tab header, data context ID, or other unique identifier
    return tab.Header?.ToString() ?? tab.GetHashCode().ToString();
}
```

## Tips & Best Practices

- Use **Lifted** for app-like tabs over panels; **Boxed** for pill-style segmented navigation.
- Use **TabWidthMode="Equal"** to prevent layout shifts when tab selection causes font weight changes.
- Keep headers short; WrapPanel allows wrapping but concise labels prevent clutter.
- Adjust `Size` to match surrounding controls; Small/XS for toolbars, Large for hero sections.
- For tab content padding, set `Padding` on DaisyTabs or inside each TabItem as needed.
- **Context menu** is opt-in via `EnableTabContextMenu="True"`. Wire events to handle actions in your app.
- Use **DaisyModal** for destructive confirmations (close multiple tabs) and **DaisyToast** for feedback.
- Tab colors can be set declaratively via XAML or programmatically via `DaisyTabs.SetTabPaletteColor()` (palette) or `DaisyTabs.SetTabColor()` (semantic/theme-aware).

## Architecture Notes

DaisyTabs is designed as a **styling + callback** control, not a document manager. It:

- **Does**: Style tabs, expose tab colors, provide a context menu, raise events
- **Does not**: Manage tab lifecycle, close tabs automatically, persist state, handle drag-reorder

For richer "document tabs" behavior (close buttons on each tab, dirty indicators, drag-to-reorder, overflow handling), consider creating a dedicated `DaisyDocumentTabs` control that wraps TabControl and owns document-specific behaviors.
