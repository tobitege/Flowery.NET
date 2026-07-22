<!-- Supplementary documentation for DaisyDropdown -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyDropdown is a lightweight dropdown menu control built on a `Popup`. It displays a trigger area with a placeholder/selection, and shows a menu (via `DaisyMenu`) when opened. Use it for compact menu-style pickers and small action lists.

## Properties

| Property | Description |
| -------- | ----------- |
| `ItemsSource` (`IEnumerable?`) | Items displayed in the dropdown menu. |
| `SelectedItem` (object?) | Currently selected item. When set/bound, the trigger shows this item. |
| `PlaceholderText` (string?) | Text shown when `SelectedItem` is null (default `Select`). |
| `IsOpen` (bool) | Controls whether the popup is open. |
| `PlacementMode` (`PlacementMode`) | Popup placement relative to the trigger (default `Bottom`). |
| `CloseOnSelection` (bool) | Closes the dropdown after selecting an item (default `True`). |
| `Size` (`DaisySize`) | Size preset (affects height and font size). |

## Events

| Event | Description |
| ----- | ----------- |
| `SelectedItemChanged` | Raised when the selected item changes. Handler signature: `void OnSelectedItemChanged(object? sender, DaisyDropdownSelectionChangedEventArgs e)` (use `e.SelectedItem`). |

## Quick Examples

```xml
<!-- Simple string list -->
<controls:DaisyDropdown PlaceholderText="Pick one"
                       ItemsSource="{Binding Options}"
                       SelectedItem="{Binding SelectedOption, Mode=TwoWay}" />

<!-- Keep open after selection -->
<controls:DaisyDropdown CloseOnSelection="False"
                       ItemsSource="{Binding Actions}" />

<!-- Compact size -->
<controls:DaisyDropdown Size="Small" ItemsSource="{Binding Options}" />
```

## Tips & Best Practices

- Use `DaisySelect` for a ComboBox-style selection control; use DaisyDropdown when you want a menu-like popup.
- For action menus, handle `SelectedItemChanged` and then reset `SelectedItem` back to `null` to return to the placeholder.
- For highly customized menu content, prefer `DaisyPopover` and place your own layout inside `PopoverContent`.
- If you bind `SelectedItem`, ensure your items have a meaningful `ToString()` or are UI elements you want displayed as-is.
