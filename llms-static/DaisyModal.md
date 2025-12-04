<!-- Supplementary documentation for DaisyModal -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyModal is an overlay dialog container with backdrop and configurable corner radii. It toggles visibility via `IsOpen` and centers its content with a max width of 500px, default padding, and drop shadow. Use it for confirmations, forms, or focused flows that require blocking UI beneath.

## Properties

| Property | Description |
|----------|-------------|
| `IsOpen` | Shows/hides the modal and backdrop. |
| `TopLeftRadius` / `TopRightRadius` / `BottomLeftRadius` / `BottomRightRadius` | Individual corner radii combined via `ModalCornerRadiusConverter`. |
| `Padding` | Inner spacing for modal content (default 24). |

## Quick Examples

```xml
<!-- Basic modal -->
<controls:DaisyModal IsOpen="True">
    <StackPanel Spacing="12">
        <TextBlock Text="Hello!" FontSize="20" FontWeight="Bold" />
        <TextBlock Text="Press ESC or click below to close." />
        <controls:DaisyButton Content="Close" Variant="Primary" />
    </StackPanel>
</controls:DaisyModal>

<!-- Custom corners and max width -->
<controls:DaisyModal IsOpen="True"
                    TopLeftRadius="24"
                    TopRightRadius="24"
                    BottomLeftRadius="12"
                    BottomRightRadius="12">
    <StackPanel Spacing="10">
        <TextBlock Text="Custom Modal" FontSize="18" FontWeight="SemiBold" />
        <TextBlock Text="Rounded top corners, tighter bottom." />
    </StackPanel>
</controls:DaisyModal>
```

## Tips & Best Practices

- Bind `IsOpen` to view state and close on ESC or backdrop click in your view logic.
- Keep modal content concise; if you need long forms, add a `ScrollViewer` inside to avoid overflow.
- Adjust individual corner radii to match brand shapes or align with other components.
- For more animation, wrap modal content in transitions; the template currently toggles visibility instantly.
