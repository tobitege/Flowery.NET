<!-- Supplementary documentation for DaisyHero -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyHero is a full-width banner container for impactful headlines. It provides padding, a large minimum height, and centers its content over a background color or image you provide. Use it for marketing hero sections, page intros, or prominent callouts.

## Layout & Styling

| Property | Description |
|----------|-------------|
| `Background` | Set to a solid color, gradient, or image brush for the hero backdrop. |
| `Padding` | Space around the content (default 40). |
| `MinHeight` | Default 300 to keep the hero tall enough for headline + CTA. |
| `CornerRadius` | Inherited from ContentControl; set for rounded hero edges when embedded in cards. |

## Quick Examples

```xml
<!-- Simple hero with CTA -->
<controls:DaisyHero Background="{DynamicResource DaisyPrimaryBrush}" Padding="48" Height="180">
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Spacing="8">
        <TextBlock Text="Hello there" FontSize="30" FontWeight="Bold" Foreground="White" />
        <TextBlock Text="Provident cupiditate voluptatem et in." Foreground="White" />
        <controls:DaisyButton Content="Get Started" Variant="Secondary" Margin="0,10,0,0" />
    </StackPanel>
</controls:DaisyHero>

<!-- Hero with image background -->
<controls:DaisyHero Padding="56">
    <controls:DaisyHero.Background>
        <ImageBrush Source="avares://Flowery.NET.Gallery/Assets/hero.jpg"
                    Stretch="UniformToFill" />
    </controls:DaisyHero.Background>
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Left" Spacing="10">
        <TextBlock Text="Build faster UI" FontSize="32" FontWeight="Bold" Foreground="White" />
        <controls:DaisyButton Content="Browse Components" Variant="Primary" />
    </StackPanel>
</controls:DaisyHero>
```

## Tips & Best Practices

- Use high-contrast foreground content over bright backgrounds; add an overlay (e.g., semi-transparent rectangle) if needed.
- Set explicit `Height` when embedding multiple heroes in a layout to maintain consistent proportions.
- Combine with `CornerRadius` for card-style heroes inside grids or dashboards.
- Keep content concise: headline, subtext, and one primary CTA work best.
