using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests;

public sealed partial class SchedulerTests
{
    [AvaloniaTheory]
    [InlineData(SchedulerView.Day)]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Agenda)]
    [InlineData(SchedulerView.Planner)]
    [InlineData(SchedulerView.Timeline)]
    [InlineData(SchedulerView.Calendar)]
    public void ResizingKeepsAppointmentAndToolbarControlsAttached(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        scope.Scheduler.Size = DaisySize.ExtraLarge;
        var renders = 0;
        scope.Scheduler.AppointmentRenderer = _ => { renders++; return null; };
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        var scroll = frame.GetVisualAncestors().OfType<ScrollViewer>().First();
        var dateButton = scope.Scheduler.GetVisualDescendants().OfType<DaisyButton>().Single(button => Equals(button.Tag, "Date"));
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        var before = renders;
        var detachments = 0;
        frame.DetachedFromVisualTree += (_, _) => detachments++;
        foreach (var width in new[] { 900d, 760d, 1280d })
        {
            var offset = scroll.Offset;
            scope.Window.Width = width;
            scope.Window.Height = 680;
            scope.Layout();
            Assert.Same(frame, scope.Frame(scope.Appointment));
            Assert.Same(scroll, frame.GetVisualAncestors().OfType<ScrollViewer>().First());
            Assert.Contains(dateButton, scope.Scheduler.GetVisualDescendants());
            Assert.Equal(Math.Min(offset.X, Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width)), scroll.Offset.X, 1);
            Assert.Equal(Math.Min(offset.Y, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)), scroll.Offset.Y, 1);
        }
        Assert.Equal(before, renders);
        Assert.Equal(0, detachments);
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
    }

    [AvaloniaTheory]
    [InlineData(DaisySize.ExtraSmall, SchedulerClockFormat.TwentyFourHour)]
    [InlineData(DaisySize.Small, SchedulerClockFormat.TwentyFourHour)]
    [InlineData(DaisySize.Medium, SchedulerClockFormat.TwentyFourHour)]
    [InlineData(DaisySize.Large, SchedulerClockFormat.TwentyFourHour)]
    [InlineData(DaisySize.ExtraLarge, SchedulerClockFormat.TwentyFourHour)]
    [InlineData(DaisySize.ExtraLarge, SchedulerClockFormat.TwelveHour)]
    public void WeekOverlapsKeepSubjectAndTimeUnclippedAtEverySize(DaisySize size, SchedulerClockFormat clock)
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        scope.Scheduler.Size = size;
        scope.Scheduler.ClockFormat = clock;
        scope.Appointment.Description = "A note that must yield to the subject and time";
        scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Alex", Position = DecorationPosition.Bottom });
        scope.Appointment.RecurrenceRule = "FREQ=WEEKLY";
        scope.Appointment.Indicators.Add(new AppointmentIndicator { Glyph = "↻", Tooltip = "Recurring" });
        var overlap = scope.Appointment.Clone();
        overlap.Id = 2;
        overlap.Subject = "Overlap";
        var longAppointment = scope.Appointment.Clone();
        longAppointment.Id = 3;
        longAppointment.Start = Monday.AddDays(1).AddHours(9);
        longAppointment.End = longAppointment.Start.AddHours(3);
        scope.Scheduler.Appointments = new[] { scope.Appointment, overlap, longAppointment };
        scope.Window.Width = 900;
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        var context = Assert.IsType<AppointmentContext>(frame.DataContext);
        var title = frame.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == context.Subject);
        var time = frame.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == context.TimeRange);
        AssertFullyInside(title, frame);
        AssertFullyInside(time, frame);
        var fullTime = new TextBlock { Text = time.Text, FontSize = time.FontSize, FontFamily = time.FontFamily };
        fullTime.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(time.Bounds.Width + 1 >= fullTime.DesiredSize.Width,
            $"{size}: time width {time.Bounds.Width}, required {fullTime.DesiredSize.Width}, font {time.FontSize}, frame {frame.Bounds.Size}, lane {((Canvas)frame.Parent!).Width}");
        Assert.Single(frame.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "↻");
        foreach (var label in frame.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Bounds.Height > 0 &&
                     text.GetVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, frame)).OfType<Control>().All(ancestor => ancestor.Bounds.Height > 0)))
            AssertFullyInside(label, frame);
        var tallFrame = scope.Frame(longAppointment);
        var badge = tallFrame.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Alex");
        AssertFullyInside(badge, tallFrame);
        Assert.True(badge.Bounds.Height > 0);
    }

    private static void AssertFullyInside(Control child, Control parent)
    {
        var origin = child.TranslatePoint(default, parent)!.Value;
        Assert.True(child.Bounds.Height > 0, "Expected a visible row");
        Assert.True(origin.X >= -1 && origin.Y >= -1 &&
            origin.X + child.Bounds.Width <= parent.Bounds.Width + 1 &&
            origin.Y + child.Bounds.Height <= parent.Bounds.Height + 1,
            $"{child}: {origin}, {child.Bounds.Size} outside {parent.Bounds.Size}");
    }

    [AvaloniaFact]
    public void CalendarHighlightsAppointmentsAcrossTheMonthAndFiltersResourceKinds()
    {
        using var scope = new SchedulerScope(SchedulerView.Calendar);
        scope.Scheduler.SelectedDate = new DateTimeOffset(Monday.AddDays(1));
        scope.Scheduler.SchedulerResources = new[]
        {
            new ResourceModel { Id = 1, Kind = ResourceKind.Person },
            new ResourceModel { Id = 2, Kind = ResourceKind.Room }
        };
        var spanning = new AppointmentModel { Id = 2, Start = Monday.AddDays(3).AddHours(9), End = Monday.AddDays(5), ResourceIds = { 2 } };
        var daily = new AppointmentModel { Id = 3, Start = Monday.AddDays(7).AddHours(9), End = Monday.AddDays(9).AddHours(10),
            SpanMode = AppointmentSpanMode.Daily, ResourceIds = { 2 } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, spanning, daily };
        scope.Layout();
        Assert.Equal(DaisyButtonVariant.Primary, DayButton(scope, Monday).Variant);
        Assert.Equal(DaisyButtonStyle.Soft, DayButton(scope, Monday).ButtonStyle);
        Assert.Equal(DaisyButtonStyle.Default, DayButton(scope, Monday.AddDays(1)).ButtonStyle);
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        scope.Layout();
        Assert.Equal(DaisyButtonVariant.Ghost, DayButton(scope, Monday).Variant);
        foreach (var day in new[] { 3, 4, 7, 8, 9 })
        {
            Assert.Equal(DaisyButtonVariant.Primary, DayButton(scope, Monday.AddDays(day)).Variant);
            Assert.Equal(DaisyButtonStyle.Soft, DayButton(scope, Monday.AddDays(day)).ButtonStyle);
        }
        Assert.Equal(DaisyButtonVariant.Ghost, DayButton(scope, Monday.AddDays(5)).Variant);
        Assert.Equal(DaisyButtonVariant.Ghost, DayButton(scope, Monday.AddDays(10)).Variant);
        var occupiedColor = Assert.IsAssignableFrom<ISolidColorBrush>(DayButton(scope, Monday.AddDays(3)).Background).Color;
        var emptyColor = Assert.IsAssignableFrom<ISolidColorBrush>(DayButton(scope, Monday.AddDays(5)).Background).Color;
        Assert.NotEqual(occupiedColor, emptyColor);
        Assert.Empty(scope.Frames(spanning));
    }

    private static DaisyButton DayButton(SchedulerScope scope, DateTime date)
    {
        var days = scope.Scheduler.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "PART_CalendarDays");
        var label = date.ToString("D", scope.Scheduler.FormattingCulture);
        return days.Children.OfType<DaisyButton>().Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == label);
    }

    [AvaloniaFact]
    public void SettingsReserveTheVisibleScrollbarWidthAtExtraLarge()
    {
        var previous = FloweryLocalization.CurrentCulture;
        using var scope = new SchedulerScope(SchedulerView.Week);
        Window? popup = null;
        try
        {
            FloweryLocalization.SetCulture("de");
            scope.Scheduler.Size = DaisySize.ExtraLarge;
            scope.Layout();
            var builder = typeof(DaisyScheduler).GetMethod("BuildSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var scroll = Assert.IsType<ScrollViewer>(builder.Invoke(scope.Scheduler, null));
            popup = new Window { Width = 500, Height = 320, Content = scroll };
            popup.Show();
            scope.Layout();
            popup.UpdateLayout();
            var bar = scroll.GetVisualDescendants().OfType<ScrollBar>().Single(control => control.Orientation == Orientation.Vertical);
            Assert.True(bar.IsVisible);
            var barLeft = bar.TranslatePoint(default, scroll)!.Value.X;
            foreach (var select in scroll.GetVisualDescendants().OfType<DaisySelect>())
            {
                var left = select.TranslatePoint(default, scroll)!.Value.X;
                Assert.True(left + select.Bounds.Width <= barLeft + 1, "Settings control overlaps the vertical scrollbar");
            }
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        }
        finally
        {
            popup?.Close();
            FloweryLocalization.SetCulture(previous);
        }
    }
}
