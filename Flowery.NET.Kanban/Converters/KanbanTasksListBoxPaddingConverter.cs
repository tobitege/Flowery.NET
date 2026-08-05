using System;
using Avalonia;
using Avalonia.Data.Converters;

namespace Flowery.NET.Kanban.Converters;

/// <summary>
/// Returns the ListBox padding used for the tasks list.
/// The task viewport reserves room for its scrollbar, so extra right padding creates an unnecessary gap.
/// </summary>
public sealed class KanbanTasksListBoxPaddingConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new Thickness(0, 0, 20, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
