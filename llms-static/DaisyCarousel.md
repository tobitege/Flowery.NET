# Overview

DaisyCarousel is a ContentControl that displays one slide at a time.
Assign a Panel containing the slides to Content.
The control transfers the panel children into its slide cache.

## Navigation and animation

| Property | Behavior |
| --- | --- |
| SelectedIndex | Selects a cached slide; indexes are clamped to the available range. |
| ItemCount | Read-only number of cached slides. |
| ShowNavigation | Shows previous/next buttons when navigation is available. |
| WrapAround | Allows navigation between the first and last slide. |
| Orientation | Sets the navigation arrow orientation. |
| Mode | Manual, Slideshow, Random, or Kiosk navigation. |
| SlideInterval | Automatic navigation interval in seconds; the controller uses a minimum of 0.5 seconds. |
| SlideTransition | Animation between slides. None switches immediately. |
| TransitionDuration | Transition duration in seconds. |
| SlideEffect | Decorative motion applied to the current slide after its transition. |

Fade, SlideLeft/Right/Up/Down, PushLeft/Right/Up/Down, and ZoomIn/Out use the shared transition helper.
Other transition values use its fade fallback.
Reduced motion stops automatic navigation and decorative effects.
Manual selection remains available.

## Example

```xml
<controls:DaisyCarousel Width="480" Height="200"
                       SelectedIndex="{Binding SlideIndex}"
                       Mode="Slideshow" SlideInterval="5"
                       WrapAround="True"
                       SlideTransition="Fade" TransitionDuration="0.4">
    <StackPanel>
        <controls:DaisyCard>
            <TextBlock Text="First slide" />
        </controls:DaisyCard>
        <controls:DaisyCard>
            <TextBlock Text="Second slide" />
        </controls:DaisyCard>
    </StackPanel>
</controls:DaisyCarousel>
```

Use SelectedIndex for programmatic navigation.
SlideChanged reports the old and new indexes.
Replacing Content rebuilds the slide cache; reapplying the template preserves it.
