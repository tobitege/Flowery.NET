using System;
using Avalonia.Data.Converters;
using System.Globalization;
using System.Collections.Generic;

namespace Flowery.Controls
{
    public static class RadialConverters
    {
        public static readonly IMultiValueConverter RangeToSweepConverter = new FuncMultiValueConverter<double, double>(values =>
        {
            if (values == null) return 0;
            // Expected: Value, Minimum, Maximum
            // We get an IEnumerable.

            var list = new List<double>(values);
            if (list.Count < 3) return 0;

            var val = list[0];
            var min = list[1];
            var max = list[2];

            if (max <= min) return 0;

            var percent = (val - min) / (max - min);
            return percent * 360.0;
        });
    }
}
