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
            ResourceKindOption.ItemsSource = new[] { FloweryLocalization.GetString("Scheduler_AllKinds") }
                .Concat(Enum.GetValues<ResourceKind>().Select(static kind => FloweryLocalization.GetString("Scheduler_Kind" + kind))).ToArray();
            ResourceKindOption.SelectedIndex = DemoScheduler.ResourceKindFilter is { } kind ? (int)kind + 1 : 0;
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

    private void OnResourceKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFormatOptions && ResourceKindOption.SelectedIndex >= 0)
            DemoScheduler.ResourceKindFilter = ResourceKindOption.SelectedIndex == 0 ? null : (ResourceKind)(ResourceKindOption.SelectedIndex - 1);
    }

    private void OnClockFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFormatOptions && ClockFormatOption.SelectedIndex >= 0)
            DemoScheduler.ClockFormat = (SchedulerClockFormat)ClockFormatOption.SelectedIndex;
    }
}
