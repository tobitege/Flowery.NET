<!-- Supplementary documentation for DaisyDrawer -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyDrawer wraps Avalonia's `SplitView` to create a sidebar drawer with responsive open/close support. Defaults to an inline drawer on the left with a 300px open width and no compact pane. The template includes a slide-in animation and optional overlay support for overlay mode. Use it to host navigation or settings panels that toggle in/out of view.

## Key Properties

| Property | Description |
| -------- | ----------- |
| `IsDrawerOpen` | Shows/hides the pane. Toggle this to open/close the drawer. |
| `Pane` / `PaneTemplate` | Content for the sidebar. |
| `Content` / `ContentTemplate` | Main area content. |
| `DrawerWidth` (default 300) | Drawer width when open. |
| `DrawerSide` (default Left) | Side where the drawer appears. |
| `OverlayMode` (default false) | When true, uses `DisplayMode="Overlay"` instead of inline. |
| `ResponsiveMode` | Auto-opens/closes based on available width. |
| `ResponsiveThreshold` (default 500) | Drawer opens when width >= threshold. |
| `SwipeThreshold` (default 48) | Drag distance required to trigger swipe open/close. |
| `EdgeSwipeZone` (default 24) | Edge size in pixels that can start a swipe open gesture. |
| `IsPaneOpen`, `OpenPaneLength`, `PanePlacement`, `DisplayMode` | SplitView properties still supported for compatibility. |

## Key Methods

- `Open()`, `Close()`, `Toggle()` for convenience in code-behind.

## Quick Examples

```xml
<!-- Basic inline drawer -->
<controls:DaisyDrawer IsDrawerOpen="True" DrawerWidth="280">
    <controls:DaisyDrawer.Pane>
        <StackPanel Margin="16" Spacing="8">
            <TextBlock Text="Menu" FontWeight="SemiBold" />
            <Button Content="Item 1" />
            <Button Content="Item 2" />
        </StackPanel>
    </controls:DaisyDrawer.Pane>
    <Grid Background="{DynamicResource DaisyBase100Brush}">
        <TextBlock Text="Main content" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Grid>
</controls:DaisyDrawer>

<!-- Toggling from a button -->
<controls:DaisyButton Content="Toggle" Click="OnToggleDrawer" />
<controls:DaisyDrawer x:Name="Drawer" OverlayMode="True" DrawerSide="Left">
    <!-- Pane/Content here -->
</controls:DaisyDrawer>

<!-- Responsive drawer -->
<controls:DaisyDrawer ResponsiveMode="True" ResponsiveThreshold="640" DrawerWidth="300">
    <!-- Pane/Content here -->
</controls:DaisyDrawer>
```

## Tips & Best Practices

- For overlay behavior, set `OverlayMode="True"` and toggle `IsDrawerOpen`; the template includes a slide-in transform and overlay placeholder.
- Keep `DrawerWidth` between 240-320px for comfortable navigation panels.
- Wrap pane contents in a `ScrollViewer` if the menu can exceed the viewport height.
- If you manage selection in the pane, pair with `DaisyDock` or `DaisyMenu` styles for consistent navigation visuals.
