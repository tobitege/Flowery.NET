using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Flowery.Services
{
    /// <summary>
    /// Converts values based on window size using proportional scaling.
    /// Unlike FloweryResponsive which uses discrete breakpoints, this converter
    /// provides continuous real-time scaling as the window is resized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This converter scales values proportionally based on the ratio between
    /// the current window size and reference dimensions (default: 1920×1080 HD).
    /// It's ideal for scaling fonts, icons, padding, and other visual elements
    /// that should smoothly adapt to window size changes.
    /// </para>
    /// <para>
    /// The converter uses the most constraining axis (whichever is smaller relative
    /// to its reference) to calculate the scale factor, maintaining aspect ratio
    /// behavior. The scale is clamped between MinScaleFactor and 1.0.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// &lt;!-- In XAML resources --&gt;
    /// &lt;services:FloweryScaleConverter x:Key="ScaleConverter"/&gt;
    /// 
    /// &lt;!-- Scale a font size (base 24pt, min 12pt) --&gt;
    /// &lt;TextBlock FontSize="{Binding Bounds, ElementName=RootWindow, 
    ///     Converter={StaticResource ScaleConverter}, ConverterParameter='24,12'}"/&gt;
    /// 
    /// &lt;!-- Scale padding --&gt;
    /// &lt;Border Padding="{Binding Bounds, ElementName=RootWindow,
    ///     Converter={StaticResource ScaleConverter}, ConverterParameter='20'}"/&gt;
    /// </code>
    /// </example>
    public class FloweryScaleConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance for convenience when default settings are acceptable.
        /// </summary>
        public static readonly FloweryScaleConverter Instance = new FloweryScaleConverter();

        /// <summary>
        /// Reference width for 100% scaling. Default is 1920 (Full HD width).
        /// </summary>
        public double ReferenceWidth { get; set; } = 1920;

        /// <summary>
        /// Reference height for 100% scaling. Default is 1080 (Full HD height).
        /// </summary>
        public double ReferenceHeight { get; set; } = 1080;

        /// <summary>
        /// Minimum scale factor (0.0 to 1.0). Values won't scale below this ratio.
        /// Default is 0.5 (50% minimum size).
        /// </summary>
        public double MinScaleFactor { get; set; } = 0.5;

        /// <summary>
        /// Default minimum value for font sizes when not specified in parameter.
        /// Default is 9.0 points.
        /// </summary>
        public double DefaultMinFontSize { get; set; } = 9.0;

        /// <summary>
        /// Converts a window Size to a scaled value.
        /// </summary>
        /// <param name="value">The window Size (from Bounds property).</param>
        /// <param name="targetType">The target property type (used to return Thickness for padding).</param>
        /// <param name="parameter">
        /// Format: "baseValue" or "baseValue,minValue"
        /// Examples: "24" (base 24, no minimum) or "24,12" (base 24, minimum 12)
        /// </param>
        /// <param name="culture">Culture info (not used).</param>
        /// <returns>
        /// The scaled value. Returns Thickness if targetType is Thickness, otherwise returns double.
        /// </returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Handle both Size (from Bounds) and Rect (from some layout properties)
            double width, height;
            
            if (value is Size size)
            {
                width = size.Width;
                height = size.Height;
            }
            else if (value is Rect rect)
            {
                width = rect.Width;
                height = rect.Height;
            }
            else
            {
                // Can't determine size, return parameter as-is
                return ParseBaseValue(parameter);
            }

            // Ignore invalid/zero dimensions
            if (width <= 0 || height <= 0)
            {
                return ParseBaseValue(parameter);
            }

            var paramStr = parameter?.ToString() ?? "";
            var parts = paramStr.Split(',');

            if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double baseValue))
            {
                return parameter;
            }

            // Parse optional minimum value (second parameter after comma)
            double? minValue = null;
            if (parts.Length > 1 && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedMin))
            {
                minValue = parsedMin;
            }

            // Calculate scaling ratios relative to reference dimensions
            double widthScale = width / ReferenceWidth;
            double heightScale = height / ReferenceHeight;

            // Use the most constraining scale (clamped between MinScaleFactor and 1.0)
            double scale = Math.Max(MinScaleFactor, Math.Min(1.0, Math.Min(widthScale, heightScale)));
            double scaledValue = baseValue * scale;

            // Apply minimum value if specified
            if (minValue.HasValue)
            {
                scaledValue = Math.Max(minValue.Value, scaledValue);
            }

            // Return Thickness if target type requires it (for Padding, Margin bindings)
            if (targetType == typeof(Thickness))
            {
                return new Thickness(scaledValue);
            }

            return scaledValue;
        }

        /// <summary>
        /// Not implemented - one-way conversion only.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("FloweryScaleConverter is one-way only.");
        }

        private static object? ParseBaseValue(object? parameter)
        {
            var paramStr = parameter?.ToString() ?? "";
            var parts = paramStr.Split(',');
            
            if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double baseValue))
            {
                return baseValue;
            }
            
            return parameter;
        }
    }
}
