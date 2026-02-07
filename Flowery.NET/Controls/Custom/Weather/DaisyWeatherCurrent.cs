using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.Controls.Custom.Weather.Models;
using Flowery.Services;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// Displays current weather conditions including temperature, feels-like, condition icon, and date/time info.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyWeatherCurrent : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyWeatherCurrent);

        private const double BaseTextFontSize = 14.0;
        private readonly DaisyControlLifecycle _lifecycle;

        public DaisyWeatherCurrent()
        {
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll,
                () => DaisySize.Medium,
                _ => { },
                subscribeSizeChanges: false);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<double> TemperatureProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, double>(nameof(Temperature));

        /// <summary>
        /// Current temperature value.
        /// </summary>
        public double Temperature
        {
            get => GetValue(TemperatureProperty);
            set => SetValue(TemperatureProperty, value);
        }

        public static readonly StyledProperty<double> FeelsLikeProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, double>(nameof(FeelsLike));

        /// <summary>
        /// "Feels like" temperature value.
        /// </summary>
        public double FeelsLike
        {
            get => GetValue(FeelsLikeProperty);
            set => SetValue(FeelsLikeProperty, value);
        }

        public static readonly StyledProperty<WeatherCondition> ConditionProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, WeatherCondition>(nameof(Condition), WeatherCondition.Unknown);

        /// <summary>
        /// Current weather condition for icon display.
        /// </summary>
        public WeatherCondition Condition
        {
            get => GetValue(ConditionProperty);
            set => SetValue(ConditionProperty, value);
        }

        public static readonly StyledProperty<DateTime> DateProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, DateTime>(nameof(Date), DateTime.Now);

        /// <summary>
        /// Date and time of the weather reading.
        /// </summary>
        public DateTime Date
        {
            get => GetValue(DateProperty);
            set => SetValue(DateProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> SunriseProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, TimeSpan>(nameof(Sunrise));

        /// <summary>
        /// Sunrise time.
        /// </summary>
        public TimeSpan Sunrise
        {
            get => GetValue(SunriseProperty);
            set => SetValue(SunriseProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> SunsetProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, TimeSpan>(nameof(Sunset));

        /// <summary>
        /// Sunset time.
        /// </summary>
        public TimeSpan Sunset
        {
            get => GetValue(SunsetProperty);
            set => SetValue(SunsetProperty, value);
        }

        public static readonly StyledProperty<string> TemperatureUnitProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, string>(nameof(TemperatureUnit), "C");

        /// <summary>
        /// Temperature unit (C or F).
        /// </summary>
        public string TemperatureUnit
        {
            get => GetValue(TemperatureUnitProperty);
            set => SetValue(TemperatureUnitProperty, value);
        }

        public static readonly StyledProperty<bool> ShowSunTimesProperty =
            AvaloniaProperty.Register<DaisyWeatherCurrent, bool>(nameof(ShowSunTimes), true);

        /// <summary>
        /// Whether to show sunrise/sunset times.
        /// </summary>
        public bool ShowSunTimes
        {
            get => GetValue(ShowSunTimesProperty);
            set => SetValue(ShowSunTimesProperty, value);
        }

        private void ApplyAll()
        {
            InvalidateVisual();
        }
    }
}
