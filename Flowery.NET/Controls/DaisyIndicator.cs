using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// An indicator control styled after DaisyUI's Indicator component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyIndicator : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyIndicator);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<object?> BadgeProperty =
            AvaloniaProperty.Register<DaisyIndicator, object?>(nameof(Badge));

        public object? Badge
        {
            get => GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }

        public static readonly StyledProperty<HorizontalAlignment> BadgeHorizontalAlignmentProperty =
            AvaloniaProperty.Register<DaisyIndicator, HorizontalAlignment>(nameof(BadgeHorizontalAlignment), HorizontalAlignment.Right);

        public HorizontalAlignment BadgeHorizontalAlignment
        {
            get => GetValue(BadgeHorizontalAlignmentProperty);
            set => SetValue(BadgeHorizontalAlignmentProperty, value);
        }

        public static readonly StyledProperty<VerticalAlignment> BadgeVerticalAlignmentProperty =
            AvaloniaProperty.Register<DaisyIndicator, VerticalAlignment>(nameof(BadgeVerticalAlignment), VerticalAlignment.Top);

        public VerticalAlignment BadgeVerticalAlignment
        {
            get => GetValue(BadgeVerticalAlignmentProperty);
            set => SetValue(BadgeVerticalAlignmentProperty, value);
        }
    }
}
