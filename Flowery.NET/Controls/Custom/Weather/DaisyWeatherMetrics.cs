using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.Services;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// Displays weather metrics in a table format (UV, wind, humidity).
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyWeatherMetrics : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyWeatherMetrics);

        private const double BaseTextFontSize = 12.0;
        private readonly DaisyControlLifecycle _lifecycle;

        public DaisyWeatherMetrics()
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
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<double> UvIndexProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, double>(nameof(UvIndex));

        /// <summary>
        /// Current UV index.
        /// </summary>
        public double UvIndex
        {
            get => GetValue(UvIndexProperty);
            set => SetValue(UvIndexProperty, value);
        }

        public static readonly StyledProperty<double> UvMaxProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, double>(nameof(UvMax));

        /// <summary>
        /// Maximum UV index for the day.
        /// </summary>
        public double UvMax
        {
            get => GetValue(UvMaxProperty);
            set => SetValue(UvMaxProperty, value);
        }

        public static readonly StyledProperty<double> WindSpeedProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, double>(nameof(WindSpeed));

        /// <summary>
        /// Current wind speed.
        /// </summary>
        public double WindSpeed
        {
            get => GetValue(WindSpeedProperty);
            set => SetValue(WindSpeedProperty, value);
        }

        public static readonly StyledProperty<double> WindMaxProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, double>(nameof(WindMax));

        /// <summary>
        /// Maximum wind speed for the day.
        /// </summary>
        public double WindMax
        {
            get => GetValue(WindMaxProperty);
            set => SetValue(WindMaxProperty, value);
        }

        public static readonly StyledProperty<string> WindUnitProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, string>(nameof(WindUnit), "km/h");

        /// <summary>
        /// Wind speed unit (e.g., "km/h", "mph").
        /// </summary>
        public string WindUnit
        {
            get => GetValue(WindUnitProperty);
            set => SetValue(WindUnitProperty, value);
        }

        public static readonly StyledProperty<int> HumidityProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, int>(nameof(Humidity));

        /// <summary>
        /// Current humidity percentage.
        /// </summary>
        public int Humidity
        {
            get => GetValue(HumidityProperty);
            set => SetValue(HumidityProperty, value);
        }

        public static readonly StyledProperty<int> HumidityMaxProperty =
            AvaloniaProperty.Register<DaisyWeatherMetrics, int>(nameof(HumidityMax));

        /// <summary>
        /// Maximum humidity for the day.
        /// </summary>
        public int HumidityMax
        {
            get => GetValue(HumidityMaxProperty);
            set => SetValue(HumidityMaxProperty, value);
        }

        private void ApplyAll()
        {
            InvalidateVisual();
        }
    }
}
