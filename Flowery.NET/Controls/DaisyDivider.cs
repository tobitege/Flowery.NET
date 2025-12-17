using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls
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

    /// <summary>
    /// A divider control styled after DaisyUI's Divider component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDivider : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDivider);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<bool> HorizontalProperty =
            AvaloniaProperty.Register<DaisyDivider, bool>(nameof(Horizontal), false);

        public static readonly StyledProperty<DaisyDividerColor> ColorProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerColor>(nameof(Color), DaisyDividerColor.Default);

        public static readonly StyledProperty<DaisyDividerPlacement> PlacementProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerPlacement>(nameof(Placement), DaisyDividerPlacement.Default);

        /// <summary>
        /// Gets or sets the margin of the divider (maps to --divider-m).
        /// </summary>
        public static readonly StyledProperty<Thickness> DividerMarginProperty =
            AvaloniaProperty.Register<DaisyDivider, Thickness>(nameof(DividerMargin), new Thickness(0, 4));

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

        public Thickness DividerMargin
        {
            get => GetValue(DividerMarginProperty);
            set => SetValue(DividerMarginProperty, value);
        }
    }
}
