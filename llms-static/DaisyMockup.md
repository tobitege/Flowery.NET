<!-- Supplementary documentation for DaisyMockup -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyMockup frames content in simulated shells: **Code**, **Window**, or **Browser**. It provides the chrome (dots, toolbar/address bar) and padding so you can showcase code, UI previews, or pages in a themed wrapper.

## Variants

| Variant | Description |
|---------|-------------|
| Code (default) | Dark background, no border, generous padding for code snippets. |
| Window | Neutral window frame with header dots and inner content area. |
| Browser | Adds toolbar with traffic-light dots and an address bar showing `Url`. |

## Properties

| Property | Description |
|----------|-------------|
| `Variant` | Selects shell style. |
| `Url` | Displayed in the Browser variant’s address bar. |
| `Padding` | Inner content padding (variant defaults apply). |

## Quick Examples

```xml
<!-- Code mockup -->
<controls:DaisyMockup Variant="Code">
    <TextBlock Text="console.log('Hello world');" Foreground="White" />
</controls:DaisyMockup>

<!-- Window mockup -->
<controls:DaisyMockup Variant="Window">
    <StackPanel Spacing="8">
        <TextBlock Text="Window Content" FontWeight="SemiBold" />
        <controls:DaisyButton Content="Action" Variant="Primary" />
    </StackPanel>
</controls:DaisyMockup>

<!-- Browser mockup with URL -->
<controls:DaisyMockup Variant="Browser" Url="https://daisyui.com" Width="400">
    <Grid Background="{DynamicResource DaisyBase100Brush}">
        <TextBlock Text="Hello!" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Grid>
</controls:DaisyMockup>
```

## Tips & Best Practices

- For long code, pair the Code variant with a monospace font and consider wrapping in a `ScrollViewer`.
- Set explicit `Width` for the Browser variant to keep the address bar readable.
- Adjust `Padding` if you need tighter or looser content spacing; variants set sensible defaults.
- Use meaningful `Url` strings to convey context in the Browser mockup, even if static.
