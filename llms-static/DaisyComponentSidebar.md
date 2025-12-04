<!-- Supplementary documentation for DaisyComponentSidebar -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyComponentSidebar is a navigational sidebar composed of collapsible categories and clickable items. It provides built-in search filtering, a routed `ItemSelected` event, optional badges per item, and persistence of expanded state/last selection to local app data. The default Categories collection is prepopulated for the Flowery gallery, but you can supply your own.

## Key Properties & Events

| Member | Description |
|--------|-------------|
| `Categories` (`ObservableCollection<SidebarCategory>`) | Data source for sections and items. Each category has `Name`, `IconKey` (maps to a geometry resource), `IsExpanded`, and `Items`. |
| `SelectedItem` (`SidebarItem?`) | Currently selected item; set programmatically to highlight and persist selection. |
| `SidebarWidth` (double, default 220) | Fixed width of the sidebar host. |
| `SearchText` (string) | Text used to filter categories/items. Setting it triggers live filtering; clearing restores the full list. |
| `ItemSelected` (event) | Raised with `SidebarItemSelectedEventArgs` containing the selected `SidebarItem` and its `SidebarCategory`. |

## Data Models

- `SidebarCategory`: `Name`, `IconKey`, `IsExpanded`, `Items (ObservableCollection<SidebarItem>)`
- `SidebarItem`: `Id`, `Name`, `TabHeader`, optional `Badge`

## Quick Examples

```xml
<!-- Basic sidebar with custom categories -->
<controls:DaisyComponentSidebar SidebarWidth="240">
    <controls:DaisyComponentSidebar.Categories>
        <controls:SidebarCategory Name="General" IconKey="DaisyIconHome">
            <controls:SidebarItem Name="Overview" Id="overview" TabHeader="General" />
            <controls:SidebarItem Name="Changelog" Id="changelog" TabHeader="General" Badge="New" />
        </controls:SidebarCategory>
        <controls:SidebarCategory Name="Components" IconKey="DaisyIconComponents">
            <controls:SidebarItem Name="Button" Id="button" TabHeader="Components" />
            <controls:SidebarItem Name="Card" Id="card" TabHeader="Components" />
        </controls:SidebarCategory>
    </controls:DaisyComponentSidebar.Categories>
</controls:DaisyComponentSidebar>

<!-- Bind search box elsewhere and react to selection -->
<StackPanel Orientation="Horizontal" Spacing="12">
    <TextBox Width="180"
             Watermark="Search components..."
             Text="{Binding ElementName=Sidebar, Path=SearchText, Mode=TwoWay}" />
    <controls:DaisyComponentSidebar x:Name="Sidebar"
                                    ItemSelected="OnSidebarItemSelected"
                                    Width="260" />
</StackPanel>
```

## Tips & Best Practices

- Provide matching `IconKey` resources (StreamGeometry) in your theme for custom categories.
- Use `Badge` to denote counts or “New” labels; it renders via `DaisyBadge` in the item template.
- The control persists expanded state and last selected item under the user’s local app data; when distributing, ensure the process has write access there.
- For dynamic data, replace the default `Categories` with your own collection; filtering operates on the original backing list `_allCategories`, so update that source directly when modifying data at runtime.
- Subscribe to `ItemSelected` to navigate views/tabs; the event bubbles, so you can handle it at parent containers.
