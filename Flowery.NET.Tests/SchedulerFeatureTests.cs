using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Localization;
using System.Globalization;
using Xunit;

namespace Flowery.NET.Tests;

public sealed partial class SchedulerTests
{
    [AvaloniaTheory]
    [InlineData(SchedulerDateTimeFormat.US, SchedulerClockFormat.Locale, "t", "en-US", "10/12/2026")]
    [InlineData(SchedulerDateTimeFormat.UK, SchedulerClockFormat.Locale, "t", "en-GB", "12/10/2026")]
    [InlineData(SchedulerDateTimeFormat.European, SchedulerClockFormat.Locale, "t", "de-DE", "12.10.2026")]
    [InlineData(SchedulerDateTimeFormat.US, SchedulerClockFormat.TwentyFourHour, "HH:mm", "en-US", "10/12/2026")]
    [InlineData(SchedulerDateTimeFormat.European, SchedulerClockFormat.TwelveHour, "h:mm tt", "de-DE", "12.10.2026")]
    public void FormatOverridesRefreshAttachedAppointmentAndTooltipWithoutChangingUiLanguage(
        SchedulerDateTimeFormat format, SchedulerClockFormat clock, string timePattern, string cultureName, string date)
    {
        // Locale formatting follows the runtime's culture data, including ICU/NLS spacing.
        var time = Monday.AddHours(9).ToString(timePattern, CultureInfo.GetCultureInfo(cultureName));
        using var scope = new SchedulerScope(SchedulerView.Day);
        var previousCulture = FloweryLocalization.CurrentCulture;
        scope.Scheduler.DateTimeFormat = format;
        scope.Scheduler.ClockFormat = clock;
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        var context = Assert.IsType<AppointmentContext>(frame.DataContext);
        Assert.Equal(time, context.StartTime);
        Assert.Equal(time, scope.Scheduler.FormatTime(Monday.AddHours(9)));
        Assert.Equal(date, scope.Scheduler.FormatDate(Monday));
        var tooltip = Assert.IsType<StackPanel>(Assert.IsType<ToolTip>(ToolTip.GetTip(frame)).Content);
        Assert.Contains(tooltip.Children.OfType<TextBlock>(), label => label.Text?.Contains(date, StringComparison.Ordinal) == true);
        Assert.Equal(previousCulture, FloweryLocalization.CurrentCulture);
        Assert.Equal(Monday.AddHours(9), scope.Appointment.Start);
    }

    [AvaloniaFact]
    public void EditorSlotsIncludeBothAlignedEndpointsAndAreIndependentOfGridSnapping()
    {
        var scheduler = new DaisyScheduler
        {
            TimeSlotStart = TimeSpan.FromHours(7), TimeSlotEnd = TimeSpan.FromHours(17),
            TimeSlotInterval = TimeSpan.FromMinutes(30), TimeIncrement = new TimeIncrementConfig { MinorTick = TimeSpan.FromMinutes(15) }
        };
        var slots = scheduler.GetTimeSlots();
        Assert.Equal(21, slots.Count);
        Assert.Equal(TimeSpan.FromHours(7), slots[0]);
        Assert.Equal(TimeSpan.FromHours(17), slots[^1]);
        Assert.Equal(TimeSpan.FromHours(7.5), slots[1]);
        scheduler.TimeSlotEnd = TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromHours(17), scheduler.GetTimeSlots()[^1]);
        Assert.Throws<ArgumentException>(() => scheduler.TimeSlotInterval = TimeSpan.Zero);
        scheduler.TimeSlotEnd = TimeSpan.FromHours(6);
        Assert.Throws<InvalidOperationException>(() => scheduler.GetTimeSlots());
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Day, 1)]
    [InlineData(SchedulerView.Week, 3)]
    [InlineData(SchedulerView.Month, 3)]
    [InlineData(SchedulerView.Agenda, 3)]
    [InlineData(SchedulerView.Planner, 3)]
    [InlineData(SchedulerView.Timeline, 1)]
    public void DailySpansRenderOccurrencesAndSelectionReturnsTheOriginalSeries(SchedulerView view, int count)
    {
        using var scope = new SchedulerScope(view);
        scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
        scope.Appointment.End = Monday.AddDays(2).AddHours(10);
        if (view == SchedulerView.Agenda)
            scope.Scheduler.VisibleDays = 3;
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var frames = scope.Frames(scope.Appointment).ToArray();
        Assert.Equal(count, frames.Length);
        Assert.All(frames, frame => Assert.Equal(TimeSpan.FromHours(1), Assert.IsType<AppointmentContext>(frame.DataContext).End -
            Assert.IsType<AppointmentContext>(frame.DataContext).Start));
        scope.Click(frames[0]);
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.Equal(Monday.AddDays(2).AddHours(10), scope.Appointment.End);
    }

    [AvaloniaFact]
    public void DraggingALaterDailyOccurrenceMovesTheWholeSeriesByTheSameDelta()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
        scope.Appointment.End = Monday.AddDays(2).AddHours(10);
        scope.Scheduler.SelectedDate = new DateTimeOffset(Monday.AddDays(2));
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        AppointmentMoveArgs? request = null;
        scope.Scheduler.AppointmentMoveRequested += (_, args) => request = args;
        var frame = scope.Frame(scope.Appointment);
        var origin = scope.Point(frame, frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(origin + new Vector(0, 48));
        scope.Window.MouseUp(origin + new Vector(0, 48), MouseButton.Left);
        Assert.NotNull(request);
        Assert.Same(scope.Appointment, request.Appointment);
        Assert.Equal(Monday.AddHours(10), request.NewStart);
        Assert.Equal(Monday.AddDays(2).AddHours(11), request.NewEnd);
        Assert.Equal(Monday.AddHours(9), scope.Appointment.Start);
    }

    [AvaloniaFact]
    public void PlannerKeepsOneDistinctRowPerAppointmentAndAllDailyBarsOnTheirSeriesRow()
    {
        using var scope = new SchedulerScope(SchedulerView.Planner);
        scope.Appointment.SpanMode = AppointmentSpanMode.Daily;
        scope.Appointment.End = Monday.AddDays(2).AddHours(10);
        var later = new AppointmentModel { Id = 2, Subject = "Later", Start = Monday.AddHours(14), End = Monday.AddHours(15), ResourceIds = { 1 } };
        var continuous = new AppointmentModel { Id = 3, Subject = "Continuous", Start = Monday.AddHours(7), End = Monday.AddDays(2).AddHours(17), ResourceIds = { 1 } };
        var allDay = new AppointmentModel { Id = 4, Subject = "All day", Start = Monday, End = Monday.AddDays(2), IsAllDay = true, ResourceIds = { 1 } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, later, continuous, allDay };
        scope.Layout();
        var dailyRows = scope.Frames(scope.Appointment).Select(Canvas.GetTop).Distinct().ToArray();
        Assert.Single(dailyRows);
        var rows = new[] { dailyRows[0], Canvas.GetTop(scope.Frame(later)), Canvas.GetTop(scope.Frame(continuous)), Canvas.GetTop(scope.Frame(allDay)) };
        Assert.Equal(4, rows.Distinct().Count());
        Assert.True(scope.Frame(continuous).Bounds.Width > scope.Frame(later).Bounds.Width);
    }

    [AvaloniaFact]
    public void MonthKeepsEveryAppointmentInItsOwnVisibleRow()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        var appointments = Enumerable.Range(1, 6).Select(index => new AppointmentModel { Id = index, Subject = "Event " + index,
            Start = Monday.AddHours(9), End = Monday.AddHours(10) }).ToArray();
        scope.Scheduler.Appointments = appointments;
        scope.Layout();
        foreach (var appointment in appointments)
            Assert.Single(scope.Frames(appointment));
        var positions = appointments.Select(appointment => scope.Point(scope.Frame(appointment), 0, 0).Y).ToArray();
        Assert.Equal(6, positions.Distinct().Count());
    }

    [AvaloniaFact]
    public void DayTimelineCombinesKindAndArbitraryCriteriaAndSupportsIndependentCollapsing()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var person = new ResourceModel { Id = 1, Name = "Alex", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Development" } };
        var room = new ResourceModel { Id = 2, Name = "Room", Kind = ResourceKind.Room, GroupValues = { ["Department"] = "Development" } };
        var all = new ResourceGroupModel { Id = "all", Name = "All", Kind = ResourceKind.Person };
        var department = new ResourceGroupModel { Id = "dev", Name = "Development", Kind = ResourceKind.Person, GroupType = "Department", GroupValue = "Development" };
        scope.Scheduler.SchedulerResources = new[] { person, room };
        scope.Scheduler.ResourceGroups = new[] { all, department };
        scope.Layout();
        Assert.True(department.Matches(person));
        Assert.False(department.Matches(room));
        Assert.Equal(2, scope.Frames(scope.Appointment).Count());
        var button = scope.Scheduler.GetVisualDescendants().OfType<DaisyButton>().Single(control => AutomationProperties.GetName(control) == "All");
        scope.Click(button);
        Assert.Equal(new object[] { "all" }, scope.Scheduler.CollapsedResourceGroupIds);
        Assert.Single(scope.Frames(scope.Appointment));
        Assert.Equal("All", all.Name);
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        scope.Layout();
        Assert.Empty(scope.Frames(scope.Appointment));
        scope.Scheduler.ResourceKindFilter = ResourceKind.Person;
        scope.Scheduler.CollapsedResourceGroupIds = Array.Empty<object>();
        scope.Layout();
        Assert.Equal(2, scope.Frames(scope.Appointment).Count());
        scope.Click(scope.Frames(scope.Appointment).First());
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        scope.Scheduler.Navigate(1);
        scope.Layout();
        Assert.Equal(Monday.AddDays(1), scope.Scheduler.SelectedDate.Date);
        Assert.Empty(scope.Frames(scope.Appointment));
    }

    [AvaloniaTheory]
    [InlineData(DaisySize.ExtraSmall)]
    [InlineData(DaisySize.Small)]
    [InlineData(DaisySize.Medium)]
    [InlineData(DaisySize.Large)]
    [InlineData(DaisySize.ExtraLarge)]
    public void TimelineResourceCellsAndAppointmentRowsHaveMatchingGeometry(DaisySize size)
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        scope.Scheduler.Size = size;
        var resources = Enumerable.Range(0, 3).Select(index => new ResourceModel { Id = index, Name = "P" + index, Kind = ResourceKind.Person }).ToArray();
        var appointments = Enumerable.Range(0, 6).Select(index => new AppointmentModel { Id = index, Subject = "A" + index,
            Start = Monday.AddHours(9 + index), End = Monday.AddHours(10 + index), ResourceIds = { index % 3 } }).ToArray();
        scope.Scheduler.SchedulerResources = resources;
        scope.Scheduler.ResourceGroups = new[] { new ResourceGroupModel { Id = "all", Name = "All", Kind = ResourceKind.Person } };
        scope.Scheduler.Appointments = appointments;
        scope.Layout();
        var differences = new List<string>();
        foreach (var appointment in appointments)
        {
            var resource = resources[(int)appointment.ResourceIds[0]];
            var label = scope.Scheduler.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == resource.Name + " · " + appointment.Subject);
            var row = Assert.IsType<Border>(label.Parent);
            var cell = row.GetVisualAncestors().OfType<Border>().First();
            var frame = scope.Frame(appointment);
            var canvas = Assert.IsAssignableFrom<Canvas>(frame.Parent);
            var cellTop = cell.TranslatePoint(default, scope.Scheduler)!.Value.Y;
            var canvasTop = canvas.TranslatePoint(default, scope.Scheduler)!.Value.Y;
            var rowCenter = row.TranslatePoint(new Point(0, row.Bounds.Height / 2), scope.Scheduler)!.Value.Y;
            var frameCenter = frame.TranslatePoint(new Point(0, frame.Bounds.Height / 2), scope.Scheduler)!.Value.Y;
            if (Math.Abs(cellTop - canvasTop) > 0.5 || Math.Abs(cell.Bounds.Height - canvas.Bounds.Height) > 0.5 || Math.Abs(rowCenter - frameCenter) > 0.5)
                differences.Add($"{appointment.Subject}: cell top/height {cellTop}/{cell.Bounds.Height}, canvas {canvasTop}/{canvas.Bounds.Height}, row centers {rowCenter}/{frameCenter}");
        }
        Assert.True(differences.Count == 0, size + ": " + string.Join("; ", differences));
    }

    [AvaloniaFact]
    public void SchedulerLabelsUseHostDesignTokenOverrides()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        scope.Scheduler.Size = DaisySize.Small;
        scope.Layout();
        var originalWidth = Assert.IsAssignableFrom<Canvas>(scope.Frame(scope.Appointment).Parent).Bounds.Width;
        scope.Scheduler.Resources["DaisySizeSmallSecondaryFontSize"] = 10.5d;
        scope.Scheduler.Resources["DaisySpacingSmall"] = 12d;
        scope.Scheduler.Resources["DaisySchedulerTimelineMinimumHourWidth"] = 200d;
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Token badge" });
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var label = scope.Scheduler.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Alex · Install");
        Assert.Equal(10.5, label.FontSize);
        var badge = scope.Frame(scope.Appointment).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Token badge");
        Assert.Equal(10.5, badge.FontSize);
        Assert.Equal(40, Assert.IsType<Border>(label.Parent).Height);
        Assert.True(Assert.IsAssignableFrom<Canvas>(scope.Frame(scope.Appointment).Parent).Bounds.Width > originalWidth);
    }
}
