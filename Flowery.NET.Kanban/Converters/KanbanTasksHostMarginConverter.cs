using System;
using Avalonia;
using Avalonia.Data.Converters;

namespace Flowery.NET.Kanban.Converters;

/// <summary>
/// Converts a column <see cref="Thickness"/> padding into a margin that cancels the horizontal padding
/// for the tasks viewport area. This keeps the column header padded while allowing cards to sit closer
/// to the column border.
/// </summary>
public sealed class KanbanTasksHostMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
