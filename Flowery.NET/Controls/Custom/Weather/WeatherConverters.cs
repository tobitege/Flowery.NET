using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Flowery.Controls.Custom.Weather.Models;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// Converts WeatherCondition enum to the appropriate icon StreamGeometry.
    /// </summary>
    public class WeatherConditionToIconConverter : IValueConverter
    {
        public static readonly WeatherConditionToIconConverter Instance = new WeatherConditionToIconConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var key = GetIconKey(value);
            if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) == true && resource is StreamGeometry geometry)
            {
                return geometry;
            }
            return null;
        }

        private static string GetIconKey(object? value)
        {
            if (value is WeatherCondition condition)
            {
                return condition switch
                {
                    WeatherCondition.Sunny => "WeatherIconSunny",
                    WeatherCondition.Clear => "WeatherIconClear",
                    WeatherCondition.PartlyCloudy => "WeatherIconPartlyCloudy",
                    WeatherCondition.Cloudy => "WeatherIconCloudy",
                    WeatherCondition.Overcast => "WeatherIconOvercast",
                    WeatherCondition.Mist => "WeatherIconMist",
                    WeatherCondition.Fog => "WeatherIconFog",
                    WeatherCondition.LightRain => "WeatherIconLightRain",
                    WeatherCondition.Rain => "WeatherIconRain",
                    WeatherCondition.HeavyRain => "WeatherIconHeavyRain",
                    WeatherCondition.Drizzle => "WeatherIconDrizzle",
                    WeatherCondition.Showers => "WeatherIconShowers",
                    WeatherCondition.Thunderstorm => "WeatherIconThunderstorm",
                    WeatherCondition.LightSnow => "WeatherIconLightSnow",
                    WeatherCondition.Snow => "WeatherIconSnow",
                    WeatherCondition.HeavySnow => "WeatherIconHeavySnow",
                    WeatherCondition.Sleet => "WeatherIconSleet",
                    WeatherCondition.FreezingRain => "WeatherIconFreezingRain",
                    WeatherCondition.Hail => "WeatherIconHail",
                    WeatherCondition.Windy => "WeatherIconWindy",
                    _ => "WeatherIconUnknown"
                };
            }
            return "WeatherIconUnknown";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts TimeSpan to time string (HH:mm format).
    /// </summary>
    public class TimeSpanToStringConverter : IValueConverter
    {
        public static readonly TimeSpanToStringConverter Instance = new TimeSpanToStringConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TimeSpan time)
            {
                return time.ToString(@"hh\:mm");
            }
            return "--:--";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts DateTime to full date format.
    /// </summary>
    public class DateToFullStringConverter : IValueConverter
    {
        public static readonly DateToFullStringConverter Instance = new DateToFullStringConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                var format = parameter as string ?? "dddd, d MMMM";
                return date.ToString(format, culture);
            }
            return "--";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
