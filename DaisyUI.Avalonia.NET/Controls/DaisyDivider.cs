using System;
using Avalonia;
using Avalonia.Controls;

namespace DaisyUI.Avalonia.Controls
{
    public enum DaisyDividerColor
    {
        Default,
        Neutral,
        Primary,
        Secondary,
        Accent,
        Success,
        Warning,
        Info,
        Error
    }

    public enum DaisyDividerPlacement
    {
        Default,
        Start,
        End
    }

    public class DaisyDivider : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDivider);

        public static readonly StyledProperty<bool> HorizontalProperty =
            AvaloniaProperty.Register<DaisyDivider, bool>(nameof(Horizontal), false);

        public static readonly StyledProperty<DaisyDividerColor> ColorProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerColor>(nameof(Color), DaisyDividerColor.Default);

        public static readonly StyledProperty<DaisyDividerPlacement> PlacementProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerPlacement>(nameof(Placement), DaisyDividerPlacement.Default);

        public bool Horizontal
        {
            get => GetValue(HorizontalProperty);
            set => SetValue(HorizontalProperty, value);
        }

        public DaisyDividerColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public DaisyDividerPlacement Placement
        {
            get => GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }
    }
}
