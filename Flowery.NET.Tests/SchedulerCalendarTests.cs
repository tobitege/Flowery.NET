using System;
using System.Globalization;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Xunit;

namespace Flowery.NET.Tests;

public partial class SchedulerTests
{
    [AvaloniaFact]
    public void CalendarFiltersKindsWrapsTextAndScrollsWithoutChangingAppointmentOwnership()
    {
        using var scope = new SchedulerScope(SchedulerView.Calendar);
        scope.Window.Height = 500;
        scope.Scheduler.ResourceKindFilter = ResourceKind.Room;
        scope.Scheduler.SchedulerResources = new[]
        {
            new ResourceModel { Id = 1, Name = "Person", Kind = ResourceKind.Person },
            new ResourceModel { Id = 2, Name = "Room", Kind = ResourceKind.Room }
        };
        var room = new AppointmentModel { Id = 2, Subject = "Room booking", Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { 1, 2 } };
        var longer = room.Clone();
        longer.Id = 3;
        longer.Description = string.Join(" ", Enumerable.Repeat("A long description that must wrap across multiple lines.", 70));
        scope.Scheduler.Appointments = new[] { scope.Appointment, room, longer };
        scope.Layout();
        Assert.Empty(scope.Frames(scope.Appointment));
        Assert.Single(scope.Frames(room));
        Assert.True(scope.Frame(longer).Bounds.Height > scope.Frame(room).Bounds.Height);
        var description = scope.Frame(longer).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == longer.Description);
        Assert.Equal(Avalonia.Media.TextWrapping.Wrap, description.TextWrapping);
        var scroll = scope.Scheduler.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_CalendarAppointmentScroll");
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        var frame = scope.Frame(room);
        frame.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        Assert.Same(room, scope.Scheduler.SelectedAppointment);
        Assert.Equal(3, scope.Scheduler.Appointments.Count);
    }

    [AvaloniaFact]
    public void CalendarUsesConfiguredWeekRulesAndSupportsMonthAndDateNavigation()
    {
        using var scope = new SchedulerScope(SchedulerView.Calendar);
        scope.Scheduler.SelectedDate = new DateTimeOffset(new DateTime(2021, 1, 12));
        scope.Layout();
        Grid Days() => scope.Scheduler.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Name == "PART_CalendarDays");
        string? FirstWeek() => Days().Children.OfType<TextBlock>().Single(text => Grid.GetRow(text) == 1 && Grid.GetColumn(text) == 0).Text;
        Assert.Equal("53", FirstWeek());
        scope.Scheduler.FirstDayOfWeek = DayOfWeek.Sunday;
        scope.Scheduler.WeekRule = CalendarWeekRule.FirstDay;
        scope.Layout();
        Assert.Equal("1", FirstWeek());
        var down = scope.Scheduler.GetVisualDescendants().OfType<DaisyButton>().Single(button => Equals(button.Content, "↓"));
        down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        scope.Layout();
        Assert.Equal(2, scope.Scheduler.SelectedDate.Month);
        var date = Days().Children.OfType<DaisyButton>().First(button => Equals(button.Content, "15"));
        date.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        scope.Layout();
        Assert.Equal(new DateTime(2021, 2, 15), scope.Scheduler.SelectedDate.Date);
        Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(date)));
    }
}
