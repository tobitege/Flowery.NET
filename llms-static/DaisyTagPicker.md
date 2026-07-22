<!-- Supplementary documentation for DaisyTagPicker -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyTagPicker is an organized tag selection component inspired by "Animated Tags". It separates selected tags into a distinct, bordered area with "remove" icons, while keeping available tags in a separate list with "add" icons.

## Features

* **Organized Layout**: Distinct areas for selected and available tags.
* **Interactive Icons**: Automatic "add" (Plus) and "remove" (Close) icons on tag chips.
* **Customizable Title**: Set the header for the selected tags area using the `Title` property.
* **Flexible Sizing**: Inherits DaisyUI size presets (ExtraSmall, Small, Medium, Large, ExtraLarge) for all chips.

## Properties

| Property | Description |
| -------- | ----------- |
| `Tags` (`IList<string>?`) | Pool of all available tags. |
| `SelectedTags` (`IList<string>?`) | Currently selected tags. When null, managed internally. |
| `Title` (`string`) | Header text for the selected tags box (default: "Selected Tags"). |
| `Size` (`DaisySize`) | Size preset for the tag chips (default `Small`). |

## Events

| Event | Description |
| ----- | ----------- |
| `SelectionChanged` | Raised whenever the selection changes. |

## Quick Examples

```xml
<!-- Basic usage with internal selection -->
<controls:DaisyTagPicker Title="Selected Skills">
    <controls:DaisyTagPicker.Tags>
        <x:Array Type="{x:Type sys:String}">
            <sys:String>Avalonia</sys:String>
            <sys:String>C#</sys:String>
            <sys:String>DaisyUI</sys:String>
            <sys:String>XAML</sys:String>
        </x:Array>
    </controls:DaisyTagPicker.Tags>
</controls:DaisyTagPicker>

<!-- MVVM binding -->
<controls:DaisyTagPicker Tags="{Binding AllTags}"
                       SelectedTags="{Binding UserTags, Mode=TwoWay}"
                       Title="Chosen Options"
                       Size="Medium" />
```

## Tips & Best Practices

- Selection is string-based; ensure tag strings are unique.
- The control automatically filters the pool of `Tags` to only show unselected ones in the "Available" area.
- If you bind `SelectedTags`, the list is replaced with a new instance on every toggle.
