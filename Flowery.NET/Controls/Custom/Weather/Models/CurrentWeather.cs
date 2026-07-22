using System;

namespace Flowery.Controls.Custom.Weather.Models
{
    /// <summary>
    /// Represents current weather conditions.
    /// </summary>
    public class CurrentWeather
    {
        /// <summary>
        /// Current temperature in the configured unit.
        /// </summary>
        public double Temperature { get; set; }

        /// <summary>
        /// "Feels like" temperature accounting for wind chill or heat index.
        /// </summary>
        public double FeelsLike { get; set; }

        /// <summary>
        /// Current weather condition for icon display.
        /// </summary>
        public WeatherCondition Condition { get; set; }

        /// <summary>
        /// Date and time of the weather reading.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Sunrise time for the location.
        /// </summary>
        public TimeSpan Sunrise { get; set; }

        /// <summary>
        /// Sunset time for the location.
        /// </summary>
        public TimeSpan Sunset { get; set; }

        /// <summary>
        /// Location name or description.
        /// </summary>
        public string? Location { get; set; }

        /// <summary>
        /// Text description of the weather condition.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Humidity percentage (0-100).
        /// </summary>
        public int Humidity { get; set; }

        /// <summary>
        /// Wind speed in configured unit.
        /// </summary>
        public double WindSpeed { get; set; }
    }
}
