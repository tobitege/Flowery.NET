using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Flowery.Helpers;

internal sealed class ContrastingBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ISolidColorBrush brush) return AvaloniaProperty.UnsetValue;
        static double Linear(byte channel) => channel <= 10 ? channel / 3294.6 : Math.Pow((channel / 255d + .055) / 1.055, 2.4);
        var color = brush.Color;
        var luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        return luminance > .179 ? Brushes.Black : Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
