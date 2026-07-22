using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using System.Linq;

namespace Flowery.Controls
{
    public static class RatingConverters
    {
        public static readonly IValueConverter RangeConverter = new FuncValueConverter<double, IEnumerable<int>>(max =>
        {
            int count = (int)max;
            return Enumerable.Range(1, count);
        });
    }
}
