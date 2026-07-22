using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flowery.Controls.Custom.Weather.Models;

namespace Flowery.Controls.Custom.Weather.Services
{
    /// <summary>
    /// Interface for weather data providers.
    /// Implement this interface to create custom weather service integrations.
    /// </summary>
    public interface IWeatherService
    {
        /// <summary>
        /// Gets current weather conditions for a location.
        /// </summary>
        /// <param name="location">Location query (city name, coordinates, etc.)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current weather data</returns>
        Task<CurrentWeather> GetCurrentWeatherAsync(string location, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets weather forecast for a location.
        /// </summary>
        /// <param name="location">Location query (city name, coordinates, etc.)</param>
        /// <param name="days">Number of days to forecast (typically 1-7)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of forecast days</returns>
        Task<IEnumerable<ForecastDay>> GetForecastAsync(string location, int days = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed weather metrics for a location.
        /// </summary>
        /// <param name="location">Location query (city name, coordinates, etc.)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Weather metrics data</returns>
        Task<WeatherMetrics> GetMetricsAsync(string location, CancellationToken cancellationToken = default);
    }
}
