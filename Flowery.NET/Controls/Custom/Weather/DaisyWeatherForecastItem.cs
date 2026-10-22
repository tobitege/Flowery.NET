using System;
using Avalonia.Controls;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// One day tile of a <see cref="DaisyWeatherForecast"/>. Being its own control lets each tile take part
    /// in effects such as <see cref="DaisyNeumorphic"/> while the forecast items are created and replaced.
    /// </summary>
    public class DaisyWeatherForecastItem : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyWeatherForecastItem);
    }
}
