using System;

namespace Flowery.Controls.Custom.Weather.Models
{
    /// <summary>
    /// Represents a single day's forecast data.
    /// </summary>
    public class ForecastDay
    {
        /// <summary>
        /// Date of the forecast.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Weather condition for the day.
        /// </summary>
        public WeatherCondition Condition { get; set; }

        /// <summary>
        /// High temperature for the day.
        /// </summary>
        public double HighTemperature { get; set; }

        /// <summary>
        /// Low temperature for the day.
        /// </summary>
        public double LowTemperature { get; set; }

        /// <summary>
        /// Chance of precipitation (0-100).
        /// </summary>
        public int ChanceOfPrecipitation { get; set; }

        /// <summary>
        /// Short day name (e.g., "Mon", "Tue").
        /// </summary>
        public string DayName => Date.ToString("ddd");
    }
}
