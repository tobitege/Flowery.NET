using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A modal dialog control styled after DaisyUI's Modal component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyModal : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyModal);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<DaisyModal, bool>(nameof(IsOpen));

        public bool IsOpen
        {
            get => GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>
        /// Gets or sets the top-left corner radius (maps to --modal-tl).
        /// </summary>
        public static readonly StyledProperty<double> TopLeftRadiusProperty =
            AvaloniaProperty.Register<DaisyModal, double>(nameof(TopLeftRadius), 16.0);

        public double TopLeftRadius
        {
            get => GetValue(TopLeftRadiusProperty);
            set => SetValue(TopLeftRadiusProperty, value);
        }

        /// <summary>
        /// Gets or sets the top-right corner radius (maps to --modal-tr).
        /// </summary>
        public static readonly StyledProperty<double> TopRightRadiusProperty =
            AvaloniaProperty.Register<DaisyModal, double>(nameof(TopRightRadius), 16.0);

        public double TopRightRadius
        {
            get => GetValue(TopRightRadiusProperty);
            set => SetValue(TopRightRadiusProperty, value);
        }

        /// <summary>
        /// Gets or sets the bottom-left corner radius (maps to --modal-bl).
        /// </summary>
        public static readonly StyledProperty<double> BottomLeftRadiusProperty =
            AvaloniaProperty.Register<DaisyModal, double>(nameof(BottomLeftRadius), 16.0);

        public double BottomLeftRadius
        {
            get => GetValue(BottomLeftRadiusProperty);
            set => SetValue(BottomLeftRadiusProperty, value);
        }

        /// <summary>
        /// Gets or sets the bottom-right corner radius (maps to --modal-br).
        /// </summary>
        public static readonly StyledProperty<double> BottomRightRadiusProperty =
            AvaloniaProperty.Register<DaisyModal, double>(nameof(BottomRightRadius), 16.0);

        public double BottomRightRadius
        {
            get => GetValue(BottomRightRadiusProperty);
            set => SetValue(BottomRightRadiusProperty, value);
        }
    }

    public class ModalCornerRadiusConverter : IMultiValueConverter
    {
        public static readonly ModalCornerRadiusConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 4 &&
                values[0] is double topLeft &&
                values[1] is double topRight &&
                values[2] is double bottomRight &&
                values[3] is double bottomLeft)
            {
                return new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
            }
            return new CornerRadius(16);
        }
    }
}
