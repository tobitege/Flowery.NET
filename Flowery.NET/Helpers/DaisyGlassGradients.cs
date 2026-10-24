using System;
using Avalonia;
using Avalonia.Media;
using Flowery.Enums;

namespace Flowery.Helpers
{
    /// <summary>
    /// Creates the brushes of the <see cref="DaisyGlassGradient"/> presets.
    /// The presets are plain ARGB stops with an angle, so other Flowery ports can reproduce them with their own brush types.
    /// </summary>
    public static class DaisyGlassGradients
    {
        private enum Shape
        {
            Linear,
            Conic,
            Radial
        }

        private readonly record struct Definition(Shape Shape, double Angle, (uint Argb, double Offset)[] Stops);

        /// <summary>
        /// Creates a new brush for a preset, or returns <c>null</c> for <see cref="DaisyGlassGradient.None"/>.
        /// </summary>
        public static IBrush? CreateBrush(DaisyGlassGradient gradient)
        {
            if (GetDefinition(gradient) is not { } definition)
                return null;

            var stops = new GradientStops();
            foreach (var (argb, offset) in definition.Stops)
                stops.Add(new GradientStop(Color.FromUInt32(argb), offset));

            switch (definition.Shape)
            {
                case Shape.Conic:
                    return new ConicGradientBrush
                    {
                        Center = RelativePoint.Center,
                        Angle = definition.Angle,
                        GradientStops = stops
                    };
                case Shape.Radial:
                    return new RadialGradientBrush
                    {
                        Center = new RelativePoint(0.2, 0.1, RelativeUnit.Relative),
                        GradientOrigin = new RelativePoint(0.2, 0.1, RelativeUnit.Relative),
                        RadiusX = new RelativeScalar(1.1, RelativeUnit.Relative),
                        RadiusY = new RelativeScalar(1.1, RelativeUnit.Relative),
                        GradientStops = stops
                    };
                default:
                    // The angle runs clockwise from the left edge, so 0 is left to right and 90 is top to bottom.
                    var radians = definition.Angle * Math.PI / 180;
                    var dx = Math.Cos(radians) / 2;
                    var dy = Math.Sin(radians) / 2;
                    return new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative),
                        GradientStops = stops
                    };
            }
        }

        private static Definition? GetDefinition(DaisyGlassGradient gradient) => gradient switch
        {
            DaisyGlassGradient.Aurora => new(Shape.Linear, 30, [(0xFF22D3EE, 0), (0xFF34D399, 0.5), (0xFFA78BFA, 1)]),
            DaisyGlassGradient.Sunset => new(Shape.Linear, 30, [(0xFFF97316, 0), (0xFFEC4899, 0.55), (0xFF8B5CF6, 1)]),
            DaisyGlassGradient.Ocean => new(Shape.Linear, 60, [(0xFF38BDF8, 0), (0xFF2563EB, 0.6), (0xFF1E3A8A, 1)]),
            DaisyGlassGradient.Mint => new(Shape.Linear, 20, [(0xFFD1FAE5, 0), (0xFF5EEAD4, 0.6), (0xFF14B8A6, 1)]),
            DaisyGlassGradient.Holo => new(Shape.Conic, 0,
            [
                (0xFFFF9AD5, 0), (0xFFA5B4FC, 0.2), (0xFF67E8F9, 0.4), (0xFF86EFAC, 0.6), (0xFFFDE68A, 0.8), (0xFFFF9AD5, 1)
            ]),
            DaisyGlassGradient.Frost => new(Shape.Radial, 0, [(0xEEFFFFFF, 0), (0x88E0F2FE, 0.55), (0x22BFDBFE, 1)]),
            _ => null
        };
    }
}
