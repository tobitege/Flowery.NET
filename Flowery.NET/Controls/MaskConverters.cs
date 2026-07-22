using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace Flowery.Controls
{
    public static class MaskConverters
    {
        public static readonly IValueConverter VariantToGeometryConverter = new FuncValueConverter<DaisyMaskVariant, Geometry>(v =>
        {
            return v switch
            {
                DaisyMaskVariant.Squircle => Geometry.Parse("M 50,0 C 10,0 0,10 0,50 0,90 10,100 50,100 90,100 100,90 100,50 100,10 90,0 50,0 Z"),
                DaisyMaskVariant.Circle => Geometry.Parse("M 50,0 A 50,50 0 1 1 50,100 A 50,50 0 1 1 50,0 Z"),
                DaisyMaskVariant.Heart => Geometry.Parse("M50,80 L10,40 A20,20 0 0 1 50,10 A20,20 0 0 1 90,40 Z"),
                DaisyMaskVariant.Hexagon => Geometry.Parse("M50,0 L100,25 L100,75 L50,100 L0,75 L0,25 Z"),
                DaisyMaskVariant.Triangle => Geometry.Parse("M50,0 L100,100 L0,100 Z"),
                DaisyMaskVariant.Diamond => Geometry.Parse("M50,0 L100,50 L50,100 L0,50 Z"),
                _ => Geometry.Parse("M 50,0 C 10,0 0,10 0,50 0,90 10,100 50,100 90,100 100,90 100,50 100,10 90,0 50,0 Z")
            };
        });

        public static readonly IValueConverter VariantToScaledGeometryConverter = new VariantToScaledGeometryConverterImpl();
    }

    public class VariantToScaledGeometryConverterImpl : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DaisyMaskVariant variant)
                return null;

            Geometry geometry = variant switch
            {
                DaisyMaskVariant.Squircle => Geometry.Parse("M 50,0 C 10,0 0,10 0,50 0,90 10,100 50,100 90,100 100,90 100,50 100,10 90,0 50,0 Z"),
                DaisyMaskVariant.Circle => new EllipseGeometry { Rect = new Rect(0, 0, 100, 100) },
                DaisyMaskVariant.Heart => Geometry.Parse("M50,90 C50,90 10,50 10,30 A20,20 0 0 1 50,20 A20,20 0 0 1 90,30 C90,50 50,90 50,90 Z"),
                DaisyMaskVariant.Hexagon => Geometry.Parse("M50,0 L100,25 L100,75 L50,100 L0,75 L0,25 Z"),
                DaisyMaskVariant.Triangle => Geometry.Parse("M50,0 L100,100 L0,100 Z"),
                DaisyMaskVariant.Diamond => Geometry.Parse("M50,0 L100,50 L50,100 L0,50 Z"),
                DaisyMaskVariant.Square => new RectangleGeometry { Rect = new Rect(0, 0, 100, 100) },
                _ => Geometry.Parse("M 50,0 C 10,0 0,10 0,50 0,90 10,100 50,100 90,100 100,90 100,50 100,10 90,0 50,0 Z")
            };

            return geometry;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
