using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;

namespace Flowery.Services
{
    /// <summary>
    /// Converts a nullable value to a Thickness. Returns the specified thickness when value is not null,
    /// otherwise returns zero thickness.
    /// </summary>
    /// <remarks>
    /// Use this converter to conditionally apply margins/paddings based on whether another property has a value.
    /// The ConverterParameter specifies the thickness to use when value is not null (format: "left,top,right,bottom" or "uniform").
    /// </remarks>
    /// <example>
    /// <code>
    /// &lt;ContentPresenter Margin="{Binding StartIcon, RelativeSource={RelativeSource TemplatedParent},
    ///     Converter={x:Static services:FloweryConverters.NullToThickness}, ConverterParameter='32,0,0,0'}" /&gt;
    /// </code>
    /// </example>
    public class NullToThicknessConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance for use in XAML.
        /// </summary>
        public static readonly NullToThicknessConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return new Thickness(0);

            // Parse the parameter as thickness
            var paramStr = parameter?.ToString();
            if (string.IsNullOrEmpty(paramStr))
                return new Thickness(0);

            return ParseThickness(paramStr!);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static Thickness ParseThickness(string value)
        {
            var parts = value.Split(',');

            if (parts.Length == 1 && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
                return new Thickness(uniform);

            if (parts.Length == 2 &&
                double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return new Thickness(h, v);

            if (parts.Length == 4 &&
                double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var top) &&
                double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var right) &&
                double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom))
                return new Thickness(left, top, right, bottom);

            return new Thickness(0);
        }
    }

    /// <summary>
    /// Converts a double value to a uniform Thickness.
    /// Used internally by ScaleExtension to support Thickness properties (Padding, Margin).
    /// </summary>
    public class DoubleToThicknessConverter : IValueConverter
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly DoubleToThicknessConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d)
                return new Thickness(d);

            return new Thickness(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Multiplies a double value by a parameter.
    /// </summary>
    public class MultiplyConverter : IValueConverter
    {
        public static readonly MultiplyConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d && parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
                return d * m;
            return value ?? 0.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Calculates the TranslateTransform for NumberFlow digits using the two-element approach.
    /// Values: [Offset (double), FontSize (double)]
    /// Offset is a normalized value: -1 = above (hidden), 0 = visible, 1 = below (hidden)
    /// </summary>
    public class NumberFlowTransformConverter : IMultiValueConverter
    {
        /// <summary>
        /// Line height multiplier - must match DaisyNumberFlow.LineHeightMultiplier and XAML ConverterParameter.
        /// </summary>
        private const double LineHeightMultiplier = 1.4;

        public static readonly NumberFlowTransformConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Validate we have both required values
            if (values.Count < 2)
                return new TranslateTransform(0, 0);

            // Handle various numeric types for offset
            double offset = 0;
            if (values[0] is double d)
                offset = d;
            else if (values[0] is int i)
                offset = i;
            else if (values[0] is { } v0 && v0 != AvaloniaProperty.UnsetValue)
            {
                if (double.TryParse(v0.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    offset = parsed;
            }

            // Handle FontSize - might be double or other numeric types
            double fontSize = 16; // fallback default
            if (values[1] is double fs)
                fontSize = fs;
            else if (values[1] is int fsi)
                fontSize = fsi;
            else if (values[1] is { } v1 && v1 != AvaloniaProperty.UnsetValue)
            {
                if (double.TryParse(v1.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    fontSize = parsed;
            }

            // Ensure valid values
            if (double.IsNaN(fontSize) || fontSize <= 0)
                fontSize = 16;

            double lineHeight = fontSize * LineHeightMultiplier;
            double y = offset * lineHeight;
            return new TranslateTransform(0, y);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts a boolean to a Thickness. Returns the parameter thickness when true, zero when false.
    /// </summary>
    public class BoolToThicknessConverter : IValueConverter
    {
        public static readonly BoolToThicknessConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                var paramStr = parameter?.ToString();
                if (!string.IsNullOrEmpty(paramStr))
                {
                    var parts = paramStr!.Split(',');
                    if (parts.Length == 1 && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
                        return new Thickness(uniform);
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                        double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        return new Thickness(h, v);
                    if (parts.Length == 4 &&
                        double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                        double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var top) &&
                        double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var right) &&
                        double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom))
                        return new Thickness(left, top, right, bottom);
                }
            }
            return new Thickness(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Converts a boolean to a Cursor. When true, returns the cursor specified in parameter; when false, returns default.
    /// </summary>
    public class BoolToCursorConverter : IValueConverter
    {
        public static readonly BoolToCursorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is string cursorName)
            {
                return cursorName.ToLowerInvariant() switch
                {
                    "hand" => new Cursor(StandardCursorType.Hand),
                    "pointer" => new Cursor(StandardCursorType.Hand),
                    "arrow" => new Cursor(StandardCursorType.Arrow),
                    "ibeam" => new Cursor(StandardCursorType.Ibeam),
                    "cross" => new Cursor(StandardCursorType.Cross),
                    "wait" => new Cursor(StandardCursorType.Wait),
                    _ => Cursor.Default
                };
            }
            return Cursor.Default;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Multi-value converter for NumberFlow digit cursor.
    /// Values: [AllowDigitSelection (bool), IsDigit (bool)]
    /// Returns Hand cursor when both are true, Default otherwise.
    /// </summary>
    public class NumberFlowDigitCursorConverter : IMultiValueConverter
    {
        public static readonly NumberFlowDigitCursorConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 &&
                values[0] is bool allowSelection && allowSelection &&
                values[1] is bool isDigit && isDigit)
            {
                return new Cursor(StandardCursorType.Hand);
            }
            return Cursor.Default;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Provides static converter instances for use in XAML via x:Static.
    /// </summary>
    public static class FloweryConverters
    {
        /// <summary>
        /// Converts a nullable value to Thickness. Returns ConverterParameter as thickness when not null, zero otherwise.
        /// </summary>
        public static readonly NullToThicknessConverter NullToThickness = NullToThicknessConverter.Instance;

        /// <summary>
        /// Converts a double to a uniform Thickness.
        /// </summary>
        public static readonly DoubleToThicknessConverter DoubleToThickness = DoubleToThicknessConverter.Instance;

        /// <summary>
        /// Multiplies a double value by a parameter.
        /// </summary>
        public static readonly MultiplyConverter Multiply = MultiplyConverter.Instance;

        /// <summary>
        /// Calculates the TranslateTransform for NumberFlow digits.
        /// </summary>
        public static readonly NumberFlowTransformConverter NumberFlowTransformConverter = NumberFlowTransformConverter.Instance;

        /// <summary>
        /// Converts a boolean to Thickness. Returns ConverterParameter as thickness when true, zero when false.
        /// </summary>
        public static readonly BoolToThicknessConverter BoolToThickness = BoolToThicknessConverter.Instance;

        /// <summary>
        /// Converts a boolean to a Cursor. When true, uses parameter (Hand, Arrow, etc); when false, uses default.
        /// </summary>
        public static readonly BoolToCursorConverter BoolToCursor = BoolToCursorConverter.Instance;

        /// <summary>
        /// Multi-value converter for NumberFlow digit cursor.
        /// </summary>
        public static readonly NumberFlowDigitCursorConverter NumberFlowDigitCursor = NumberFlowDigitCursorConverter.Instance;
    }
}
