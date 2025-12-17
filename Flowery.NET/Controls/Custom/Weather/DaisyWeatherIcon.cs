using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Controls.Custom.Weather.Models;
using Flowery.Services;

namespace Flowery.Controls.Custom.Weather
{
    /// <summary>
    /// Animated weather condition icon with subtle animations for each weather type.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyWeatherIcon : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyWeatherIcon);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<WeatherCondition> ConditionProperty =
            AvaloniaProperty.Register<DaisyWeatherIcon, WeatherCondition>(nameof(Condition), WeatherCondition.Unknown);

        /// <summary>
        /// Weather condition to display.
        /// </summary>
        public WeatherCondition Condition
        {
            get => GetValue(ConditionProperty);
            set => SetValue(ConditionProperty, value);
        }

        public static readonly StyledProperty<bool> IsAnimatedProperty =
            AvaloniaProperty.Register<DaisyWeatherIcon, bool>(nameof(IsAnimated), true);

        /// <summary>
        /// Whether animations are enabled. Default is true.
        /// </summary>
        public bool IsAnimated
        {
            get => GetValue(IsAnimatedProperty);
            set => SetValue(IsAnimatedProperty, value);
        }

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<DaisyWeatherIcon, double>(nameof(IconSize), 64.0);

        /// <summary>
        /// Size of the icon in pixels. Default is 64.
        /// </summary>
        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        static DaisyWeatherIcon()
        {
            ConditionProperty.Changed.AddClassHandler<DaisyWeatherIcon>((x, _) => x.UpdatePseudoClasses());
            IsAnimatedProperty.Changed.AddClassHandler<DaisyWeatherIcon>((x, _) => x.UpdatePseudoClasses());
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdatePseudoClasses();
        }

        private void UpdatePseudoClasses()
        {
            // Clear all condition classes
            PseudoClasses.Remove(":sunny");
            PseudoClasses.Remove(":cloudy");
            PseudoClasses.Remove(":rainy");
            PseudoClasses.Remove(":snowy");
            PseudoClasses.Remove(":stormy");
            PseudoClasses.Remove(":windy");
            PseudoClasses.Remove(":foggy");
            PseudoClasses.Remove(":animated");

            if (IsAnimated)
            {
                PseudoClasses.Add(":animated");
            }

            // Add appropriate condition class
            switch (Condition)
            {
                case WeatherCondition.Sunny:
                case WeatherCondition.Clear:
                    PseudoClasses.Add(":sunny");
                    break;
                case WeatherCondition.PartlyCloudy:
                case WeatherCondition.Cloudy:
                case WeatherCondition.Overcast:
                    PseudoClasses.Add(":cloudy");
                    break;
                case WeatherCondition.LightRain:
                case WeatherCondition.Rain:
                case WeatherCondition.HeavyRain:
                case WeatherCondition.Drizzle:
                case WeatherCondition.Showers:
                    PseudoClasses.Add(":rainy");
                    break;
                case WeatherCondition.LightSnow:
                case WeatherCondition.Snow:
                case WeatherCondition.HeavySnow:
                case WeatherCondition.Sleet:
                case WeatherCondition.FreezingRain:
                case WeatherCondition.Hail:
                    PseudoClasses.Add(":snowy");
                    break;
                case WeatherCondition.Thunderstorm:
                    PseudoClasses.Add(":stormy");
                    break;
                case WeatherCondition.Windy:
                    PseudoClasses.Add(":windy");
                    break;
                case WeatherCondition.Mist:
                case WeatherCondition.Fog:
                    PseudoClasses.Add(":foggy");
                    break;
            }
        }
    }
}
