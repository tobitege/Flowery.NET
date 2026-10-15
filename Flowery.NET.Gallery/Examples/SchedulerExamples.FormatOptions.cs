using System;
using System.Linq;
using Avalonia.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Localization;

namespace Flowery.NET.Gallery.Examples;

public partial class SchedulerExamples
{
    private bool _updatingFormatOptions;

    private void RefreshFormatOptions()
    {
        _updatingFormatOptions = true;
        try
        {
            DateTimeFormatOption.ItemsSource = Enum.GetValues<SchedulerDateTimeFormat>()
                .Select(static format => FloweryLocalization.GetString("Scheduler_Format" + format)).ToArray();
            DateTimeFormatOption.SelectedIndex = (int)DemoScheduler.DateTimeFormat;
            ClockFormatOption.ItemsSource = Enum.GetValues<SchedulerClockFormat>()
                .Select(static format => FloweryLocalization.GetString("Scheduler_Clock" + format)).ToArray();
            ClockFormatOption.SelectedIndex = (int)DemoScheduler.ClockFormat;
        }
        finally { _updatingFormatOptions = false; }
    }

    private void OnDateTimeFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFormatOptions && DateTimeFormatOption.SelectedIndex >= 0)
            DemoScheduler.DateTimeFormat = (SchedulerDateTimeFormat)DateTimeFormatOption.SelectedIndex;
    }

    private void OnClockFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFormatOptions && ClockFormatOption.SelectedIndex >= 0)
            DemoScheduler.ClockFormat = (SchedulerClockFormat)ClockFormatOption.SelectedIndex;
    }
}
