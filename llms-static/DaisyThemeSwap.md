<!-- Supplementary documentation for DaisyThemeSwap -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyThemeSwap is a themed variant of DaisySwap for toggling light/dark (or any two) themes. It defaults to a rotate transition and keeps `IsChecked` in sync with the current theme’s light/dark status. Clicking applies `LightTheme` or `DarkTheme` via `DaisyThemeManager`.

## Properties

| Property | Description |
|----------|-------------|
| `LightTheme` | Theme applied when toggling to light (default “Light”). |
| `DarkTheme` | Theme applied when toggling to dark (default “Dark”). |
| `OnContent` / `OffContent` / `IndeterminateContent` | Inherited from DaisySwap for custom visuals. |
| `TransitionEffect` | Inherited; defaults to `Rotate`. |

## Behavior

- Click toggles between `LightTheme` and `DarkTheme`.
- `IsChecked` is set to true when the current theme is dark (via `IsDarkTheme`).
- Listens to `ThemeChanged` to stay synced with external theme changes.

## Quick Examples

```xml
<!-- Simple light/dark swap -->
<controls:DaisyThemeSwap>
    <controls:DaisySwap.OnContent>
        <PathIcon Data="{StaticResource DaisyIconMoon}" />
    </controls:DaisySwap.OnContent>
    <controls:DaisySwap.OffContent>
        <PathIcon Data="{StaticResource DaisyIconSun}" />
    </controls:DaisySwap.OffContent>
</controls:DaisyThemeSwap>

<!-- Custom theme pair -->
<controls:DaisyThemeSwap LightTheme="Corporate" DarkTheme="Synthwave" />
```

## Tips & Best Practices

- Set `LightTheme` to your base theme so toggling off returns to the default look.
- Provide clear icon/text content for on/off states to make the toggle intent obvious.
- If you allow many themes, pair this with `DaisyThemeDropdown` for full selection plus quick toggle.
