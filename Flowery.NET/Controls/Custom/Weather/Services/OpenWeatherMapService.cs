using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flowery.Controls.Custom.Weather.Models;

namespace Flowery.Controls.Custom.Weather.Services
{
    /// <summary>
    /// Weather service implementation using OpenWeatherMap API.
    /// Requires a free API key from https://openweathermap.org/api
    /// </summary>
    public class OpenWeatherMapService : IWeatherService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _units;

        private const string BaseUrl = "https://api.openweathermap.org/data/2.5";

        /// <summary>
        /// Creates a new OpenWeatherMap service instance.
        /// </summary>
        /// <param name="apiKey">Your OpenWeatherMap API key</param>
        /// <param name="units">Units: "metric" (Celsius), "imperial" (Fahrenheit), or "standard" (Kelvin)</param>
        public OpenWeatherMapService(string apiKey, string units = "metric")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _units = units;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Creates a new OpenWeatherMap service instance with a custom HttpClient.
        /// </summary>
        public OpenWeatherMapService(string apiKey, HttpClient httpClient, string units = "metric")
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _units = units;
        }

        public async Task<CurrentWeather> GetCurrentWeatherAsync(string location, CancellationToken cancellationToken = default)
        {
            var url = $"{BaseUrl}/weather?q={Uri.EscapeDataString(location)}&appid={_apiKey}&units={_units}";
            var httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API returned {(int)httpResponse.StatusCode}: {responseContent}");
            }

            var json = JsonDocument.Parse(responseContent);
            var root = json.RootElement;

            var main = root.GetProperty("main");
            var weather = root.GetProperty("weather")[0];
            var sys = root.GetProperty("sys");
            var wind = root.GetProperty("wind");

            return new CurrentWeather
            {
                Temperature = main.GetProperty("temp").GetDouble(),
                FeelsLike = main.GetProperty("feels_like").GetDouble(),
                Humidity = main.GetProperty("humidity").GetInt32(),
                WindSpeed = wind.GetProperty("speed").GetDouble(),
                Condition = MapConditionCode(weather.GetProperty("id").GetInt32()),
                Description = weather.GetProperty("description").GetString() ?? string.Empty,
                Location = root.GetProperty("name").GetString() ?? string.Empty,
                Date = DateTime.Now,
                Sunrise = DateTimeOffset.FromUnixTimeSeconds(sys.GetProperty("sunrise").GetInt64()).LocalDateTime.TimeOfDay,
                Sunset = DateTimeOffset.FromUnixTimeSeconds(sys.GetProperty("sunset").GetInt64()).LocalDateTime.TimeOfDay
            };
        }

        public async Task<IEnumerable<ForecastDay>> GetForecastAsync(string location, int days = 5, CancellationToken cancellationToken = default)
        {
            var url = $"{BaseUrl}/forecast?q={Uri.EscapeDataString(location)}&appid={_apiKey}&units={_units}&cnt={days * 8}";
            var httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API returned {(int)httpResponse.StatusCode}: {responseContent}");
            }

            var json = JsonDocument.Parse(responseContent);
            var list = json.RootElement.GetProperty("list");

            var forecasts = new Dictionary<DateTime, ForecastDay>();

            foreach (var item in list.EnumerateArray())
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()).LocalDateTime;
                var date = dt.Date;
                var main = item.GetProperty("main");
                var weather = item.GetProperty("weather")[0];
                var temp = main.GetProperty("temp").GetDouble();
                var pop = item.TryGetProperty("pop", out var popProp) ? (int)(popProp.GetDouble() * 100) : 0;

                if (!forecasts.ContainsKey(date))
                {
                    forecasts[date] = new ForecastDay
                    {
                        Date = date,
                        HighTemperature = temp,
                        LowTemperature = temp,
                        Condition = MapConditionCode(weather.GetProperty("id").GetInt32()),
                        ChanceOfPrecipitation = pop
                    };
                }
                else
                {
                    var existing = forecasts[date];
                    existing.HighTemperature = Math.Max(existing.HighTemperature, temp);
                    existing.LowTemperature = Math.Min(existing.LowTemperature, temp);
                    existing.ChanceOfPrecipitation = Math.Max(existing.ChanceOfPrecipitation, pop);
                }
            }

            return forecasts.Values.Take(days).ToList();
        }

        public async Task<WeatherMetrics> GetMetricsAsync(string location, CancellationToken cancellationToken = default)
        {
            var current = await GetCurrentWeatherAsync(location, cancellationToken);
            var forecast = await GetForecastAsync(location, 1, cancellationToken);
            var todayForecast = forecast.FirstOrDefault();

            return new WeatherMetrics
            {
                UvIndex = 0, // OpenWeatherMap free tier doesn't include UV - would need OneCall API
                UvMax = 0,
                WindSpeed = current.WindSpeed,
                WindMax = todayForecast != null ? current.WindSpeed * 1.5 : current.WindSpeed, // Estimate
                WindUnit = _units == "imperial" ? "mph" : "km/h",
                Humidity = current.Humidity,
                HumidityMax = Math.Min(100, current.Humidity + 10) // Estimate
            };
        }

        /// <summary>
        /// Maps OpenWeatherMap condition codes to WeatherCondition enum.
        /// See: https://openweathermap.org/weather-conditions
        /// </summary>
        private static WeatherCondition MapConditionCode(int code)
        {
            return code switch
            {
                // Thunderstorm group (2xx)
                >= 200 and < 300 => WeatherCondition.Thunderstorm,

                // Drizzle group (3xx)
                >= 300 and < 400 => WeatherCondition.Drizzle,

                // Rain group (5xx)
                500 => WeatherCondition.LightRain,
                501 => WeatherCondition.Rain,
                502 or 503 or 504 => WeatherCondition.HeavyRain,
                511 => WeatherCondition.FreezingRain,
                >= 520 and < 600 => WeatherCondition.Showers,

                // Snow group (6xx)
                600 => WeatherCondition.LightSnow,
                601 => WeatherCondition.Snow,
                602 => WeatherCondition.HeavySnow,
                611 or 612 or 613 => WeatherCondition.Sleet,
                615 or 616 => WeatherCondition.Sleet,
                620 or 621 or 622 => WeatherCondition.Snow,

                // Atmosphere group (7xx)
                701 or 711 or 721 or 731 or 741 => WeatherCondition.Mist,
                751 or 761 or 762 => WeatherCondition.Fog,
                771 or 781 => WeatherCondition.Windy,

                // Clear (800)
                800 => WeatherCondition.Clear,

                // Clouds group (80x)
                801 => WeatherCondition.PartlyCloudy,
                802 => WeatherCondition.PartlyCloudy,
                803 => WeatherCondition.Cloudy,
                804 => WeatherCondition.Overcast,

                _ => WeatherCondition.Unknown
            };
        }
    }
}
