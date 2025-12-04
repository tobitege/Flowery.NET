<!-- Supplementary documentation for DaisyTextArea -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyTextArea is a multiline variant of DaisyInput. It inherits all input variants/sizes, sets `AcceptsReturn`/`AcceptsTab=True`, and enables `TextWrapping`. Use it for longer text entries while keeping consistent styling with other form fields.

## Key Behavior

- Multiline by default (`AcceptsReturn=True`, `TextWrapping=Wrap`).
- Inherits DaisyInput properties: `Variant`, `Size`, padding, selection brushes, etc.
- Supports `PlaceholderText` via DaisyInput base styling.

## Quick Examples

```xml
<!-- Basic textarea -->
<controls:DaisyTextArea Watermark="Enter description..." MinHeight="120" />

<!-- Primary variant, large -->
<controls:DaisyTextArea Variant="Primary" Size="Large" Watermark="Feedback" MinHeight="160" />
```

## Tips & Best Practices

- Set `MinHeight` or `Height` to provide enough room for content; allow wrapping instead of horizontal scrolling.
- Use the same variant/size scheme as adjacent inputs for visual consistency.
- For code/comment fields, pair with monospace `FontFamily` if needed.
- Remember to handle validation like any TextBox; semantic variants (Error/Success) can signal state.
