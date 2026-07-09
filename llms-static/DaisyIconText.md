# Overview

`DaisyIconText` combines an icon and optional text with size-aware spacing and coloring. Use `IconSymbol` for built-in symbols or `IconData` for custom geometry. When both are set, `IconData` takes precedence.

## Examples

```xml
<controls:DaisyIconText IconSymbol="Save" Text="Save" Variant="Primary" />
<controls:DaisyIconText IconSymbol="Share" Text="Share"
                        IconPlacement="Right" Variant="Secondary" />
<controls:DaisyIconText IconSymbol="Home" Text="Home"
                        IconPlacement="Top" Variant="Accent" />
<controls:DaisyIconText IconData="M12 4v16m8-8H4" Size="Large" />
```

`IconPlacement` accepts `Left`, `Right`, `Top`, and `Bottom`. `IconSize`, `FontSizeOverride`, and `Spacing` can override the values derived from `Size`.
