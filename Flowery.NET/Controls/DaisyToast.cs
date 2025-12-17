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
    /// A Toast container control styled after DaisyUI's Toast component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyToast : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyToast);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Gets or sets the horizontal offset of the toast (maps to --toast-x).
        /// </summary>
        public static readonly StyledProperty<double> ToastHorizontalOffsetProperty =
            AvaloniaProperty.Register<DaisyToast, double>(nameof(ToastHorizontalOffset), 16.0);

        public double ToastHorizontalOffset
        {
            get => GetValue(ToastHorizontalOffsetProperty);
            set => SetValue(ToastHorizontalOffsetProperty, value);
        }

        /// <summary>
        /// Gets or sets the vertical offset of the toast (maps to --toast-y).
        /// </summary>
        public static readonly StyledProperty<double> ToastVerticalOffsetProperty =
            AvaloniaProperty.Register<DaisyToast, double>(nameof(ToastVerticalOffset), 16.0);

        public double ToastVerticalOffset
        {
            get => GetValue(ToastVerticalOffsetProperty);
            set => SetValue(ToastVerticalOffsetProperty, value);
        }
    }

    public class ToastMarginConverter : IMultiValueConverter
    {
        public static readonly ToastMarginConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 &&
                values[0] is double horizontal &&
                values[1] is double vertical)
            {
                return new Thickness(horizontal, vertical);
            }
            return new Thickness(16);
        }
    }
}
