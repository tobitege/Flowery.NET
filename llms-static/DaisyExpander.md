<!-- Supplementary documentation for DaisyExpander -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyExpander is a section with a clickable header and a chevron that shows or hides its content. Unlike `DaisyCollapse` it has no animation and no variants: it is a plain, light section with its own header and content padding, header colors and chevron size. The tour editor uses it for its groups of settings.

The header is a button, so Tab reaches it and Enter or Space toggles the section. Screen readers hear the header text and whether the section is expanded or collapsed (expand and collapse pattern).

## Properties

| Property | Description |
| -------- | ----------- |
| `Header` | The header content: a text or any control. |
| `Content` / `ExpanderContent` | The content shown below the header while expanded. `ExpanderContent` is the same as `Content`. |
| `IsExpanded` | Whether the content is shown (default `True`). A click on the header changes it with `SetCurrentValue`, so a binding on it stays; only a `TwoWay` binding writes the click back to the view model. |
| `HeaderBackground` | The background of the header row (default transparent). |
| `HeaderForeground` | The color of the header text and the chevron (default: the text color of the theme). |
| `HeaderPadding` | The padding of the header row (default `8,6,8,6`). |
| `ContentPadding` | The padding around the content (default `0`). |
| `ChevronSize` | The width and height of the chevron (default `12`). |
| `ShowChevron` | Whether the chevron is shown (default `True`). |
| `BorderBrush` / `BorderThickness` | The border of the header row, such as a line above each section. |

In high contrast mode the header takes the contrast colors of the theme and a one pixel border, and `HeaderBackground` and `HeaderForeground` give way until it ends.

## Quick Examples

```xml
<!-- A section that starts collapsed -->
<controls:DaisyExpander Header="Advanced settings" IsExpanded="False">
    <StackPanel Spacing="8">
        <controls:DaisyCheckBox Content="Show hidden files" />
        <controls:DaisyCheckBox Content="Compact rows" />
    </StackPanel>
</controls:DaisyExpander>

<!-- Colored header with a line above it, bound to a view model -->
<controls:DaisyExpander Header="Details"
                        IsExpanded="{Binding DetailsOpen, Mode=TwoWay}"
                        HeaderBackground="{DynamicResource DaisyBase200Brush}"
                        HeaderForeground="{DynamicResource DaisyPrimaryBrush}"
                        BorderBrush="{DynamicResource DaisyBase300Brush}"
                        BorderThickness="0,1,0,0"
                        ContentPadding="8">
    <TextBlock Text="More about this item." TextWrapping="Wrap" />
</controls:DaisyExpander>
```

## Tips & Best Practices

- Use `DaisyExpander` for many plain sections in a settings panel or inspector; use `DaisyCollapse` for a single animated disclosure and `DaisyAccordion` when only one section may be open.
- Keep the header short; it shares its row with the chevron.
- Pick a `HeaderForeground` with enough contrast to `HeaderBackground`; high contrast mode replaces both.
