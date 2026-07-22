namespace Flowery.Controls.Custom.Weather.Models
{
    /// <summary>
    /// Represents additional weather metrics (UV, wind, humidity).
    /// </summary>
    public class WeatherMetrics
    {
        /// <summary>
        /// Current UV index.
        /// </summary>
        public double UvIndex { get; set; }

        /// <summary>
        /// Maximum UV index for the day.
        /// </summary>
        public double UvMax { get; set; }

        /// <summary>
        /// Current wind speed in configured unit.
        /// </summary>
        public double WindSpeed { get; set; }

        /// <summary>
        /// Maximum wind speed for the day.
        /// </summary>
        public double WindMax { get; set; }

        /// <summary>
        /// Wind speed unit (e.g., "km/h", "mph").
        /// </summary>
        public string WindUnit { get; set; } = "km/h";

        /// <summary>
        /// Current humidity percentage (0-100).
        /// </summary>
        public int Humidity { get; set; }

        /// <summary>
        /// Maximum humidity for the day.
        /// </summary>
        public int HumidityMax { get; set; }
    }
}
