using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Flowery.Controls
{
    public static class StatConverters
    {
        public static readonly IValueConverter CenteredToAlignment =
            new FuncValueConverter<bool, HorizontalAlignment>(isCentered =>
                isCentered ? HorizontalAlignment.Center : HorizontalAlignment.Left);
    }
}
