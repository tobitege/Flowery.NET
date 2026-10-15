using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Controls.Custom.Weather.Models;
using Flowery.Controls.Custom.Weather.Services;
using Flowery.Services;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// A composite weather card that can display current weather, forecast, and metrics.
    /// Supports both manual property binding and automatic data fetching via IWeatherService.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyWeatherCard : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyWeatherCard);

        private const double BaseTextFontSize = 14.0;
        private readonly DaisyControlLifecycle _lifecycle;

        public DaisyWeatherCard()
        {
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private CancellationTokenSource? _loadingCts;
        private CancellationTokenSource? _autoRefreshCts;

        #region Current Weather Properties

        public static readonly StyledProperty<double> TemperatureProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, double>(nameof(Temperature));

        public double Temperature
        {
            get => GetValue(TemperatureProperty);
            set => SetValue(TemperatureProperty, value);
        }

        public static readonly StyledProperty<double> FeelsLikeProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, double>(nameof(FeelsLike));

        public double FeelsLike
        {
            get => GetValue(FeelsLikeProperty);
            set => SetValue(FeelsLikeProperty, value);
        }

        public static readonly StyledProperty<WeatherCondition> ConditionProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, WeatherCondition>(nameof(Condition), WeatherCondition.Unknown);

        public WeatherCondition Condition
        {
            get => GetValue(ConditionProperty);
            set => SetValue(ConditionProperty, value);
        }

        public static readonly StyledProperty<DateTime> DateProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, DateTime>(nameof(Date), DateTime.Now);

        public DateTime Date
        {
            get => GetValue(DateProperty);
            set => SetValue(DateProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> SunriseProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, TimeSpan>(nameof(Sunrise));

        public TimeSpan Sunrise
        {
            get => GetValue(SunriseProperty);
            set => SetValue(SunriseProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> SunsetProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, TimeSpan>(nameof(Sunset));

        public TimeSpan Sunset
        {
            get => GetValue(SunsetProperty);
            set => SetValue(SunsetProperty, value);
        }

        #endregion

        #region Forecast Properties

        public static readonly StyledProperty<IEnumerable<ForecastDay>> ForecastProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, IEnumerable<ForecastDay>>(nameof(Forecast));

        public IEnumerable<ForecastDay> Forecast
        {
            get => GetValue(ForecastProperty);
            set => SetValue(ForecastProperty, value);
        }

        #endregion

        #region Metrics Properties

        public static readonly StyledProperty<double> UvIndexProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, double>(nameof(UvIndex));

        public double UvIndex
        {
            get => GetValue(UvIndexProperty);
            set => SetValue(UvIndexProperty, value);
        }

        public static readonly StyledProperty<double> UvMaxProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, double>(nameof(UvMax));

        public double UvMax
        {
            get => GetValue(UvMaxProperty);
            set => SetValue(UvMaxProperty, value);
        }

        public static readonly StyledProperty<double> WindSpeedProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, double>(nameof(WindSpeed));

        public double WindSpeed
        {
            get => GetValue(WindSpeedProperty);
            set => SetValue(WindSpeedProperty, value);
        }

        public static readonly StyledProperty<double> WindMaxProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, double>(nameof(WindMax));

        public double WindMax
        {
            get => GetValue(WindMaxProperty);
            set => SetValue(WindMaxProperty, value);
        }

        public static readonly StyledProperty<string> WindUnitProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, string>(nameof(WindUnit), "km/h");

        public string WindUnit
        {
            get => GetValue(WindUnitProperty);
            set => SetValue(WindUnitProperty, value);
        }

        public static readonly StyledProperty<int> HumidityProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, int>(nameof(Humidity));

        public int Humidity
        {
            get => GetValue(HumidityProperty);
            set => SetValue(HumidityProperty, value);
        }

        public static readonly StyledProperty<int> HumidityMaxProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, int>(nameof(HumidityMax));

        public int HumidityMax
        {
            get => GetValue(HumidityMaxProperty);
            set => SetValue(HumidityMaxProperty, value);
        }

        #endregion

        #region Configuration Properties

        public static readonly StyledProperty<string> TemperatureUnitProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, string>(nameof(TemperatureUnit), "C");

        public string TemperatureUnit
        {
            get => GetValue(TemperatureUnitProperty);
            set => SetValue(TemperatureUnitProperty, value);
        }

        public static readonly StyledProperty<bool> ShowCurrentProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, bool>(nameof(ShowCurrent), true);

        public bool ShowCurrent
        {
            get => GetValue(ShowCurrentProperty);
            set => SetValue(ShowCurrentProperty, value);
        }

        public static readonly StyledProperty<bool> ShowForecastProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, bool>(nameof(ShowForecast), true);

        public bool ShowForecast
        {
            get => GetValue(ShowForecastProperty);
            set => SetValue(ShowForecastProperty, value);
        }

        public static readonly StyledProperty<bool> ShowMetricsProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, bool>(nameof(ShowMetrics), true);

        public bool ShowMetrics
        {
            get => GetValue(ShowMetricsProperty);
            set => SetValue(ShowMetricsProperty, value);
        }

        public static readonly StyledProperty<bool> ShowSunTimesProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, bool>(nameof(ShowSunTimes), true);

        public bool ShowSunTimes
        {
            get => GetValue(ShowSunTimesProperty);
            set => SetValue(ShowSunTimesProperty, value);
        }

        #endregion

        #region Service Properties

        public static readonly StyledProperty<IWeatherService> WeatherServiceProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, IWeatherService>(nameof(WeatherService));

        public IWeatherService WeatherService
        {
            get => GetValue(WeatherServiceProperty);
            set => SetValue(WeatherServiceProperty, value);
        }

        public static readonly StyledProperty<string> LocationProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, string>(nameof(Location));

        public string Location
        {
            get => GetValue(LocationProperty);
            set => SetValue(LocationProperty, value);
        }

        public static readonly StyledProperty<int> ForecastDaysProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, int>(nameof(ForecastDays), 5);

        public int ForecastDays
        {
            get => GetValue(ForecastDaysProperty);
            set => SetValue(ForecastDaysProperty, value);
        }

        public static readonly StyledProperty<bool> IsLoadingProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, bool>(nameof(IsLoading));

        public bool IsLoading
        {
            get => GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly StyledProperty<string?> ErrorMessageProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, string?>(nameof(ErrorMessage));

        public string? ErrorMessage
        {
            get => GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public static readonly StyledProperty<bool> AutoRefreshProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, bool>(nameof(AutoRefresh), false);

        /// <summary>
        /// Whether to automatically refresh weather data at the specified interval.
        /// </summary>
        public bool AutoRefresh
        {
            get => GetValue(AutoRefreshProperty);
            set => SetValue(AutoRefreshProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> RefreshIntervalProperty =
            AvaloniaProperty.Register<DaisyWeatherCard, TimeSpan>(nameof(RefreshInterval), TimeSpan.FromMinutes(30));

        /// <summary>
        /// Interval between automatic refreshes (default: 30 minutes).
        /// </summary>
        public TimeSpan RefreshInterval
        {
            get => GetValue(RefreshIntervalProperty);
            set => SetValue(RefreshIntervalProperty, value);
        }

        #endregion

        static DaisyWeatherCard()
        {
            WeatherServiceProperty.Changed.AddClassHandler<DaisyWeatherCard>((x, _) => x.OnServiceOrLocationChanged());
            LocationProperty.Changed.AddClassHandler<DaisyWeatherCard>((x, _) => x.OnServiceOrLocationChanged());
            AutoRefreshProperty.Changed.AddClassHandler<DaisyWeatherCard>((x, _) => x.OnAutoRefreshChanged());
            RefreshIntervalProperty.Changed.AddClassHandler<DaisyWeatherCard>((x, _) => x.OnAutoRefreshChanged());
        }

        private void OnServiceOrLocationChanged()
        {
            _loadingCts?.Cancel();
            _loadingCts = null;
            IsLoading = false;
            OnAutoRefreshChanged();
            if (WeatherService != null && !string.IsNullOrWhiteSpace(Location))
            {
                _ = LoadWeatherDataAsync();
            }
        }

        private void OnAutoRefreshChanged()
        {
            _autoRefreshCts?.Cancel();
            _autoRefreshCts = null;

            if (AutoRefresh && WeatherService != null && !string.IsNullOrWhiteSpace(Location)
                && TopLevel.GetTopLevel(this) != null)
            {
                _autoRefreshCts = new CancellationTokenSource();
                _ = RunAutoRefreshLoop(_autoRefreshCts);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            OnAutoRefreshChanged();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _autoRefreshCts?.Cancel();
            _autoRefreshCts = null;
            _loadingCts?.Cancel();
            _loadingCts = null;
            IsLoading = false;
            base.OnDetachedFromVisualTree(e);
        }

        private async Task RunAutoRefreshLoop(CancellationTokenSource request)
        {
            var token = request.Token;
            try
            {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RefreshInterval, token);
                    if (!token.IsCancellationRequested)
                    {
                        await LoadWeatherDataAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            }
            finally { request.Dispose(); }
        }

        /// <summary>
        /// Manually triggers a refresh of weather data from the configured service.
        /// </summary>
        public async Task RefreshAsync()
        {
            await LoadWeatherDataAsync();
        }

        private async Task LoadWeatherDataAsync()
        {
            if (WeatherService == null || string.IsNullOrWhiteSpace(Location))
                return;

            _loadingCts?.Cancel();
            var request = new CancellationTokenSource();
            _loadingCts = request;
            var token = request.Token;
            var service = WeatherService;
            var location = Location;

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var current = await service.GetCurrentWeatherAsync(location, token);
                token.ThrowIfCancellationRequested();
                IEnumerable<ForecastDay>? forecast = ShowForecast ? await service.GetForecastAsync(location, ForecastDays, token) : null;
                token.ThrowIfCancellationRequested();
                WeatherMetrics? metrics = ShowMetrics ? await service.GetMetricsAsync(location, token) : null;

                if (token.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested || !ReferenceEquals(_loadingCts, request)) return;
                    Temperature = current.Temperature;
                    FeelsLike = current.FeelsLike;
                    Condition = current.Condition;
                    Date = current.Date;
                    Sunrise = current.Sunrise;
                    Sunset = current.Sunset;

                    if (forecast != null)
                    {
                        Forecast = forecast.ToList();
                    }

                    if (metrics != null)
                    {
                        UvIndex = metrics.UvIndex;
                        UvMax = metrics.UvMax;
                        WindSpeed = metrics.WindSpeed;
                        WindMax = metrics.WindMax;
                        WindUnit = metrics.WindUnit;
                        Humidity = metrics.Humidity;
                        HumidityMax = metrics.HumidityMax;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Cancelled - ignore
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested && ReferenceEquals(_loadingCts, request)) ErrorMessage = ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_loadingCts, request))
                {
                    _loadingCts = null;
                    IsLoading = false;
                }
                request.Dispose();
            }
        }

        private void ApplyAll()
        {
            InvalidateVisual();
        }
    }
}
