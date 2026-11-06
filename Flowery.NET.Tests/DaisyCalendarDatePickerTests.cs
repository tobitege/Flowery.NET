using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyCalendarDatePickerTests
{
    [AvaloniaFact]
    public void Date_And_SelectedDate_Stay_In_Sync()
    {
        var picker = new DaisyCalendarDatePicker();

        picker.Date = new DateTimeOffset(new DateTime(2026, 10, 16, 15, 30, 0));
        Assert.Equal(new DateTime(2026, 10, 16), picker.SelectedDate);

        picker.SelectedDate = new DateTime(2026, 12, 24);
        Assert.Equal(new DateTime(2026, 12, 24), picker.Date?.Date);

        picker.Date = null;
        Assert.Null(picker.SelectedDate);
    }
}
