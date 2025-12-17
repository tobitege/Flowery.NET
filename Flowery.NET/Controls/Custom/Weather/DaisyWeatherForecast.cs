using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// Displays a horizontal strip of daily weather forecasts.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyWeatherForecast : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyWeatherForecast);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<string> TemperatureUnitProperty =
            AvaloniaProperty.Register<DaisyWeatherForecast, string>(nameof(TemperatureUnit), "C");

        /// <summary>
        /// Temperature unit (C or F).
        /// </summary>
        public string TemperatureUnit
        {
            get => GetValue(TemperatureUnitProperty);
            set => SetValue(TemperatureUnitProperty, value);
        }

        public static readonly StyledProperty<bool> ShowPrecipitationProperty =
            AvaloniaProperty.Register<DaisyWeatherForecast, bool>(nameof(ShowPrecipitation), false);

        /// <summary>
        /// Whether to show precipitation chance.
        /// </summary>
        public bool ShowPrecipitation
        {
            get => GetValue(ShowPrecipitationProperty);
            set => SetValue(ShowPrecipitationProperty, value);
        }
    }
}
