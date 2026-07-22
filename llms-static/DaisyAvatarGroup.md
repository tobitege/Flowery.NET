<!-- Supplementary documentation for DaisyAvatarGroup -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyAvatarGroup arranges multiple `DaisyAvatar` items in an overlapping row. It uses a custom panel that offsets each avatar by a configurable overlap while keeping earlier items on top (higher z-order). Ideal for showing participants, teams, or contributors in a compact space.

## Layout Options

| Property | Description |
| -------- | ----------- |
| `Size` (`DaisySize`, default `Medium`) | Controls the group sizing behavior used by the theme (currently: overlap presets) and the built-in overflow avatar (`+N`). Note: this does **not** automatically set the `Size` of child `DaisyAvatar` items. |
| `Overlap` (double, default 24) | How much each avatar overlaps the previous one. Higher values create tighter stacks; lower values show more of each avatar. |
| `MaxVisible` (int) | Maximum number of avatars to show before collapsing. If the group has more items than this limit, the last visible slot becomes a placeholder showing the remaining count (e.g., "+5"). Default is 0 (show all). |

## Quick Examples

```xml
<!-- Simple stacked avatars -->
<controls:DaisyAvatarGroup>
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

<!-- MaxVisible: Automatically collapses overflow -->
<!-- Shows first 3 avatars, and a "+7" placeholder for the rest -->
<controls:DaisyAvatarGroup Overlap="16" MaxVisible="4">
    <!-- Imagine 10 items here -->
    <controls:DaisyAvatar ... /> 
    <controls:DaisyAvatar ... />
    ...
</controls:DaisyAvatarGroup>

<!-- Tighter overlap for larger avatars -->
<controls:DaisyAvatarGroup Size="Large">
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
- Use `MaxVisible` to automatically handle overflow counts, or add a manual `IsPlaceholder="True"` avatar if you need custom logic.
