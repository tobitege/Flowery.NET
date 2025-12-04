<!-- Supplementary documentation for DaisyAvatarGroup -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyAvatarGroup arranges multiple `DaisyAvatar` items in an overlapping row. It uses a custom panel that offsets each avatar by a configurable overlap while keeping earlier items on top (higher z-order). Ideal for showing participants, teams, or contributors in a compact space.

## Layout Options

| Property | Description |
|----------|-------------|
| `Overlap` (double, default 24) | How much each avatar overlaps the previous one. Higher values create tighter stacks; lower values show more of each avatar. |
| `MaxVisible` (int) | Exposed for future/consumer logic. The built-in template currently renders all items; if you need a cap, trim your ItemsSource or add a placeholder item like “+5”. |

## Quick Examples

```xml
<!-- Simple stacked avatars -->
<controls:DaisyAvatarGroup Overlap="16">
    <controls:DaisyAvatar Size="Small" Background="#FFCDD2">
        <TextBlock Text="AL" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </controls:DaisyAvatar>
    <controls:DaisyAvatar Size="Small" Background="#C8E6C9" HasRing="True" RingColor="Success">
        <TextBlock Text="BK" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </controls:DaisyAvatar>
    <controls:DaisyAvatar Size="Small" Background="#BBDEFB">
        <TextBlock Text="CM" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </controls:DaisyAvatar>
</controls:DaisyAvatarGroup>

<!-- Tighter overlap for larger avatars -->
<controls:DaisyAvatarGroup Overlap="32">
    <controls:DaisyAvatar Size="Large" Status="Online">
        <Image Source="avares://Flowery.NET.Gallery/Assets/avalonia-logo.ico" Stretch="UniformToFill" />
    </controls:DaisyAvatar>
    <controls:DaisyAvatar Size="Large" Status="Offline" Shape="Rounded">
        <PathIcon Data="{DynamicResource DaisyIconDog}" Width="56" Height="56" />
    </controls:DaisyAvatar>
</controls:DaisyAvatarGroup>

<!-- Showing overflow with a placeholder -->
<controls:DaisyAvatarGroup Overlap="14">
    <controls:DaisyAvatar Size="Small" Background="#FFE082">
        <TextBlock Text="A" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </controls:DaisyAvatar>
    <controls:DaisyAvatar Size="Small" Background="#B3E5FC">
        <TextBlock Text="B" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </controls:DaisyAvatar>
    <controls:DaisyAvatar Size="Small" IsPlaceholder="True">
        <TextBlock Text="+5" FontWeight="SemiBold"
                   HorizontalAlignment="Center" VerticalAlignment="Center" />
    </controls:DaisyAvatar>
</controls:DaisyAvatarGroup>
```

## Tips & Best Practices

- Keep all child avatars the same size for a clean stack; mix sizes only when intentionally highlighting one person.
- Set `Overlap` to roughly one-third to one-half of the avatar width (e.g., 12–20 for 32px avatars) so names/initials remain legible.
- Order matters: earlier items sit on top of later ones; place priority users first.
- For overflow counts, add an `IsPlaceholder` avatar with text like “+3” rather than relying on `MaxVisible` (not enforced by the default template).
