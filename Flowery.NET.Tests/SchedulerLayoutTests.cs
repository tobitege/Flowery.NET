using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
        var dateButton = scope.Scheduler.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Tag, "Date"));
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
        AssertNotTrimmed(time);
        Assert.Single(frame.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "↻");
        foreach (var label in frame.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Bounds.Height > 0 &&
                     text.GetVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, frame)).OfType<Control>().All(ancestor => ancestor.Bounds.Height > 0)))
            AssertFullyInside(label, frame);
        var tallFrame = scope.Frame(longAppointment);
        var badge = tallFrame.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Alex");
        AssertFullyInside(badge, tallFrame);
        Assert.True(badge.Bounds.Height > 0);
    }

    // The laid-out text itself says whether it was cut off with an ellipsis; a width measured outside the tree can
    // differ from the width in it by layout rounding.
    private static void AssertNotTrimmed(TextBlock text) =>
        Assert.False(text.TextLayout.TextLines.Any(static line => line.HasCollapsed), $"\"{text.Text}\" is trimmed at {text.Bounds.Width}.");

    [AvaloniaTheory]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Timeline)]
    public void VisibleResourceIdsShowOnlyTheseResourcesAndTheirAppointments(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var other = new AppointmentModel { Id = 2, Subject = "Other", Start = Monday.AddHours(11), End = Monday.AddHours(12), ResourceIds = { 2 } };
        var unassigned = new AppointmentModel { Id = 3, Subject = "Unassigned", Start = Monday.AddHours(13), End = Monday.AddHours(14) };
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, Name = "Alex" }, new ResourceModel { Id = 2, Name = "Bailey" } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, other, unassigned };
        scope.Scheduler.VisibleResourceIds = new object[] { 2 };
        scope.Layout();

        Assert.Empty(scope.Frames(scope.Appointment));
        Assert.Empty(scope.Frames(unassigned));
        Assert.NotEmpty(scope.Frames(other));
        if (view == SchedulerView.Timeline)
            Assert.Single(scope.Scheduler.GetVisualDescendants().OfType<Canvas>(), DragDrop.GetAllowDrop);

        // Null shows everything again.
        scope.Scheduler.VisibleResourceIds = null;
        scope.Layout();
        Assert.NotEmpty(scope.Frames(scope.Appointment));
    }

    [AvaloniaFact]
    public void ToolbarIconsAndButtonsFollowTheSchedulerSize()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        var sizes = new Dictionary<DaisySize, (double Icon, double Button)>();
        foreach (var size in new[] { DaisySize.Small, DaisySize.Large })
        {
            scope.Scheduler.Size = size;
            scope.Layout();
            var toolbar = scope.Scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single();
            var segment = toolbar.GetVisualDescendants().OfType<Button>().First(button => button.Tag is SchedulerView);
            sizes[size] = (Assert.IsType<Viewbox>(segment.Content).Bounds.Width, segment.Bounds.Height);
        }
        Assert.True(sizes[DaisySize.Large].Icon > sizes[DaisySize.Small].Icon, $"icons {sizes[DaisySize.Small].Icon} and {sizes[DaisySize.Large].Icon}");
        Assert.True(sizes[DaisySize.Large].Button > sizes[DaisySize.Small].Button, $"buttons {sizes[DaisySize.Small].Button} and {sizes[DaisySize.Large].Button}");
    }

    [AvaloniaTheory]
    [InlineData(SchedulerClockFormat.TwelveHour)]
    [InlineData(SchedulerClockFormat.TwentyFourHour)]
    public void SingleLineBarsAlwaysShowTheWholeStartTimeAndOnlyWhatFitsBesideIt(SchedulerClockFormat clock)
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        scope.Scheduler.ClockFormat = clock;
        scope.Scheduler.ShowTiles = true;
        scope.Appointment.Subject = "Support shift";
        scope.Appointment.Start = Monday.AddHours(11);
        scope.Appointment.End = Monday.AddHours(12);
        var longOne = Decorated(new AppointmentModel { Id = 3, Subject = "Site visit", Start = Monday.AddHours(17), End = Monday.AddHours(21), ResourceIds = { 1 } });
        scope.Scheduler.Appointments = new[] { scope.Appointment, longOne };
        // Narrower than one day at the minimum hour width, so hours keep that minimum.
        scope.Window.Width = 500;
        scope.Layout();

        // A one-hour bar at the narrowest hours shows its start time whole.
        var frame = scope.Frame(scope.Appointment);
        var start = Text(frame, Assert.IsType<AppointmentContext>(frame.DataContext).CompactStart);
        AssertFullyInside(start, frame);

        // A ten-minute bar is widened to its whole start time and hides everything that does not fit beside it.
        var shortOne = Decorated(new AppointmentModel { Id = 2, Subject = "Install", Start = Monday.AddHours(15), End = Monday.AddHours(15).AddMinutes(10), ResourceIds = { 1 } });
        scope.Scheduler.Appointments = new[] { scope.Appointment, shortOne, longOne };
        scope.Layout();
        var shortFrame = scope.Frame(shortOne);
        var shortStart = Text(shortFrame, Assert.IsType<AppointmentContext>(shortFrame.DataContext).CompactStart);
        AssertFullyInside(shortStart, shortFrame);
        var lane = Assert.IsAssignableFrom<Canvas>(shortFrame.Parent);
        Assert.True(shortFrame.Bounds.Width > lane.Bounds.Width / 1440 * 10, $"frame {shortFrame.Bounds.Width}");
        foreach (var hidden in new[] { shortOne.Subject, "↻", "Alex" })
            Assert.Equal(0, PartWidth(Text(shortFrame, hidden)));

        // A long bar shows the status icon and the tile beside its start time and subject.
        var longFrame = scope.Frame(longOne);
        foreach (var shown in new[] { longOne.Subject, "↻", "Alex" })
            AssertFullyInside(Text(longFrame, shown), longFrame);
    }

    private static AppointmentModel Decorated(AppointmentModel appointment)
    {
        appointment.Indicators.Add(new AppointmentIndicator { Glyph = "↻", Tooltip = "Recurring" });
        appointment.Tiles.Add(new AppointmentDecoration { Text = "Alex", Position = DecorationPosition.Right });
        return appointment;
    }

    private static TextBlock Text(Control frame, string text) =>
        frame.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == text);

    // The arranged width of the part of a chip line that holds the text; a hidden part is arranged empty and clips it.
    private static double PartWidth(Control text) => new[] { text }.Concat(text.GetVisualAncestors().OfType<Control>())
        .First(static part => part.GetVisualParent()?.GetType().Name == "AppointmentLinePanel").Bounds.Width;

    [AvaloniaTheory]
    [InlineData(SchedulerView.Planner)]
    [InlineData(SchedulerView.Timeline)]
    public void HoursFillTheVisibleWidthAndRescaleInPlace(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        scope.Scheduler.ShowWeekends = false;
        // A narrow window keeps the minimum hour width and scrolls.
        scope.Window.Width = 500;
        scope.Layout();
        var lane = scope.Scheduler.GetVisualDescendants().OfType<Canvas>().Single(DragDrop.GetAllowDrop);
        var scroll = lane.GetVisualAncestors().OfType<ScrollViewer>().First();
        var frame = scope.Frame(scope.Appointment);
        var hours = view == SchedulerView.Timeline ? 24 : 24 * 5;
        var minimum = lane.Bounds.Width;
        Assert.True(minimum > scroll.Viewport.Width, $"lane {minimum}, viewport {scroll.Viewport.Width}");
        AssertHourBar(frame, lane, hours);

        // Wider windows widen the hours to the visible width without rendering again: the same frame grows with them.
        foreach (var extra in new[] { 1000d, 3000d })
        {
            scope.Window.Width = minimum + extra;
            scope.Layout();
            Assert.Same(frame, scope.Frame(scope.Appointment));
            Assert.Equal(scroll.Viewport.Width, lane.Bounds.Width, 3);
            // Filled hours leave nothing to scroll sideways.
            Assert.Equal(scroll.Viewport.Width, scroll.Extent.Width, 3);
            Assert.True(lane.Bounds.Width > minimum);
            AssertHourBar(frame, lane, hours);
        }

        // The header label of an hour starts at that hour of the lane, and a click there creates an appointment at it.
        var hour = Monday.AddHours(14);
        var hourX = lane.Bounds.Width / hours * 14;
        var label = scope.Scheduler.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == scope.Scheduler.FormatTime(hour));
        var labelX = label.TranslatePoint(default, lane)!.Value.X;
        Assert.InRange(labelX - 4 - hourX, -1, 1);
        scope.Scheduler.SingleClickCreates = true;
        var created = new List<DateTime>();
        scope.Scheduler.AppointmentCreateRequested += (_, args) => created.Add(args.Start);
        var point = scope.Point(lane, hourX + 1, lane.Bounds.Height / 2);
        scope.Window.MouseDown(point, MouseButton.Left);
        scope.Window.MouseUp(point, MouseButton.Left);
        Assert.Equal(hour, Assert.Single(created));
    }

    // The one-hour appointment is one hour wide less the gap between bars; layout rounds it to whole pixels.
    private static void AssertHourBar(Control frame, Control lane, int hours) =>
        Assert.InRange(frame.Bounds.Width + 2 - lane.Bounds.Width / hours, -1, 1);

    [AvaloniaFact]
    public void ShortBarsWidenedToTheirStartTimeNeverOverlapAtAnyHourWidth()
    {
        // Four ten-minute appointments two minutes apart: each bar is wider than its duration, so the bars stay apart
        // only because every appointment has a row of its own.
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var appointments = Enumerable.Range(0, 4).Select(index => new AppointmentModel
        {
            Id = index + 1, Subject = "Bar " + index, Start = Monday.AddHours(9).AddMinutes(index * 12),
            End = Monday.AddHours(9).AddMinutes(index * 12 + 10), ResourceIds = { 1 }
        }).ToArray();
        scope.Scheduler.Appointments = appointments;
        foreach (var width in new[] { 500d, 4000d })
        {
            scope.Window.Width = width;
            scope.Layout();
            var frames = appointments.Select(scope.Frame).Select(frame => new Rect(frame.TranslatePoint(default, scope.Window)!.Value, frame.Bounds.Size)).ToArray();
            for (var a = 0; a < frames.Length; a++)
                for (var b = a + 1; b < frames.Length; b++)
                    Assert.False(frames[a].Intersects(frames[b]), $"Bars {a} and {b} overlap at width {width}: {frames[a]} {frames[b]}");
        }
    }

    [AvaloniaFact]
    public void ShortBarsKeepTheirDurationAboveTheirMinimumAndStayInsideTheLane()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var tenMinutes = new AppointmentModel { Id = 2, Subject = "Short", Start = Monday.AddHours(13), End = Monday.AddHours(13).AddMinutes(10), ResourceIds = { 1 } };
        var twoHours = new AppointmentModel { Id = 3, Subject = "Long", Start = Monday.AddHours(15), End = Monday.AddHours(17), ResourceIds = { 1 } };
        var lastQuarter = new AppointmentModel { Id = 4, Subject = "Late", Start = Monday.AddHours(23).AddMinutes(30), End = Monday.AddHours(23).AddMinutes(45), ResourceIds = { 1 } };
        scope.Scheduler.Appointments = new[] { tenMinutes, twoHours, lastQuarter };
        scope.Window.Width = 500;
        scope.Layout();
        var lane = Assert.IsAssignableFrom<Canvas>(scope.Frame(twoHours).Parent);
        var perMinute = lane.Bounds.Width / 1440;

        // A long bar is as long as its appointment, less the gap between bars; layout rounds it to whole pixels.
        Assert.InRange(scope.Frame(twoHours).Bounds.Width - (120 * perMinute - 2), -1, 1);
        // A short bar is only as wide as its whole start time needs, far less than an hour.
        var shortFrame = scope.Frame(tenMinutes);
        var start = Text(shortFrame, Assert.IsType<AppointmentContext>(shortFrame.DataContext).CompactStart);
        Assert.True(shortFrame.Bounds.Width < 60 * perMinute, $"short bar {shortFrame.Bounds.Width}, hour {60 * perMinute}");
        Assert.True(start.Bounds.Width >= start.DesiredSize.Width - 0.5, $"start {start.Bounds.Width} of {start.DesiredSize.Width}");
        // A bar at the end of the day stays inside the lane with its whole start time.
        var lateFrame = scope.Frame(lastQuarter);
        Assert.True(lateFrame.Bounds.Right <= lane.Bounds.Width + 0.5, $"late bar ends at {lateFrame.Bounds.Right} of {lane.Bounds.Width}");
        AssertFullyInside(Text(lateFrame, Assert.IsType<AppointmentContext>(lateFrame.DataContext).CompactStart), lateFrame);
    }

    [AvaloniaFact]
    public void TheToolbarKeepsItsButtonsWhenTheSchedulerChanges()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var toolbar = scope.Scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single();
        var settings = ToolbarButton(toolbar, "Settings");
        var next = ToolbarButton(toolbar, "Next");
        var title = Title(toolbar);

        next.Focus();
        next.PointerClick();
        scope.Scheduler.ShowWeekends = false;
        scope.Scheduler.CurrentView = SchedulerView.Month;
        scope.Layout();

        // The same buttons stay, so an open settings menu stays open and the keyboard focus stays on Next.
        Assert.Same(settings, ToolbarButton(toolbar, "Settings"));
        Assert.Same(next, ToolbarButton(toolbar, "Next"));
        Assert.True(next.IsFocused);
        Assert.NotEqual(title, Title(toolbar));
        var selected = toolbar.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains("selected")).ToArray();
        Assert.Equal(SchedulerView.Month, Assert.Single(selected).Tag);
    }

    [AvaloniaFact]
    public void AToolbarElsewhereControlsAHiddenOneOfItsScheduler()
    {
        var scheduler = new DaisyScheduler { ShowToolbar = false, SelectedDate = new DateTimeOffset(Monday), CurrentView = SchedulerView.Week };
        var toolbar = new DaisySchedulerToolbar { Scheduler = scheduler };
        var panel = new StackPanel();
        var window = new Window { Width = 1100, Height = 720, Content = panel };
        window.Show();
        try
        {
            // The toolbar may be shown before its scheduler is; it stays empty until then.
            panel.Children.Add(toolbar);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(toolbar.Content);
            panel.Children.Add(scheduler);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            ToolbarButton(toolbar, "Next").PointerClick();
            Assert.Equal(Monday.AddDays(7), scheduler.SelectedDate.Date);
            var builtIn = scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single();
            Assert.False(builtIn.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HostButtonsRaiseTheirEventsAndShowAnImageWithItsLabel()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var clicked = new List<string?>();
        var toggled = new List<string?>();
        scope.Scheduler.ToolbarButtonClicked += (_, button) => clicked.Add(button.Id);
        scope.Scheduler.AdornmentToggleRequested += (_, button) => toggled.Add(button.Id);
        scope.Scheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "new", Label = "New", IconData = SchedulerToolbarIcons.Add });
        scope.Scheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "map", Label = "Map", Image = OnePixelPng });
        scope.Scheduler.ToolbarAdornments.Add(new SchedulerToolbarButton { Id = "flag", Label = "Flags", IsToggle = true, IsChecked = true });
        scope.Layout();
        var toolbar = scope.Scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single();
        Button Host(string id) => toolbar.GetVisualDescendants().OfType<Button>().Single(button => button.Tag is SchedulerToolbarButton model && model.Id == id);

        Host("new").PointerClick();
        Host("flag").PointerClick();
        Assert.Equal(["new"], clicked);
        Assert.Equal(["flag"], toggled);
        Assert.Contains("checked", Host("flag").Classes);
        Assert.IsType<Viewbox>(Host("new").Content);
        Assert.Contains(Host("map").GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "Map");
    }

    [AvaloniaFact]
    public void VisibleResourceIdsDropHiddenAppointmentsFromTheSelectionAndTheCalendarMarks()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var other = new AppointmentModel { Id = 2, Subject = "Other", Start = Monday.AddDays(1).AddHours(11), End = Monday.AddDays(1).AddHours(12), ResourceIds = { 2 } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, other };
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        scope.Scheduler.VisibleResourceIds = new object[] { 2 };
        scope.Layout();
        Assert.Null(scope.Scheduler.SelectedAppointment);

        // In the Calendar view, the day of the hidden appointment is not marked; the day of the shown one is.
        scope.Scheduler.CurrentView = SchedulerView.Calendar;
        scope.Layout();
        DaisyButton Day(int day) => scope.Scheduler.GetVisualDescendants().OfType<DaisyButton>()
            .Single(button => Equals(button.Content, Monday.AddDays(day).Day.ToString(CultureInfo.CurrentCulture)));
        Assert.Equal(DaisyButtonStyle.Default, Day(0).ButtonStyle);
        Assert.Equal(DaisyButtonStyle.Soft, Day(1).ButtonStyle);
    }

    [AvaloniaTheory]
    [InlineData("en", SchedulerView.Month, 0, "October", "2026")]
    [InlineData("en", SchedulerView.Week, 0, "Oct 12 – Oct 18", "2026")]
    [InlineData("en", SchedulerView.Week, 79, "Dec 28 – Jan 3", "2026 – 2027")]
    [InlineData("ja", SchedulerView.Month, 0, "2026年10月", "")]
    [InlineData("ja", SchedulerView.Week, 0, "2026年10月12日 – 10月18日", "")]
    [InlineData("ja", SchedulerView.Week, 79, "2026年12月28日 – 2027年1月3日", "")]
    public void TheToolbarTitleFollowsTheCultureOrderOfYearMonthAndDay(string language, SchedulerView view, int days, string primary, string secondary)
    {
        var previous = FloweryLocalization.CurrentCulture;
        try
        {
            FloweryLocalization.SetCulture(language);
            using var scope = new SchedulerScope(view);
            scope.Scheduler.FirstDayOfWeek = DayOfWeek.Monday;
            scope.Scheduler.SelectedDate = new DateTimeOffset(Monday.AddDays(days));
            scope.Layout();
            var texts = ToolbarButton(scope.Scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single(), "Date")
                .GetVisualDescendants().OfType<TextBlock>().ToArray();

            Assert.Equal(primary, texts[0].Text);
            Assert.Equal(secondary, texts[1].Text);
        }
        finally
        {
            FloweryLocalization.SetCulture(previous);
        }
    }

    [AvaloniaTheory]
    // Persian writes the year first but the day before the month, and its year begins at Nowruz, on 21 March 2026.
    [InlineData("fa-IR", false, -210, "1404 – 1405")]
    // Saudi Arabic with the Gregorian calendar for formatting, where the default Hijri year changes in June.
    [InlineData("ar-SA", true, 79, "2026 – 2027")]
    [InlineData("ar-SA", true, -119, "2026")]
    public void TheToolbarTitleCountsYearsInTheCalendarItFormatsWith(string name, bool gregorian, int days, string years)
    {
        var previous = FloweryLocalization.CurrentCulture;
        try
        {
            var culture = new CultureInfo(name);
            if (gregorian)
                culture.DateTimeFormat.Calendar = new GregorianCalendar();
            FloweryLocalization.SetCulture(culture);
            using var scope = new SchedulerScope(SchedulerView.Week);
            scope.Scheduler.FirstDayOfWeek = DayOfWeek.Monday;
            scope.Scheduler.SelectedDate = new DateTimeOffset(Monday.AddDays(days));
            scope.Layout();
            var texts = ToolbarButton(scope.Scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single(), "Date")
                .GetVisualDescendants().OfType<TextBlock>().ToArray();

            Assert.DoesNotContain(years[..4], texts[0].Text);
            Assert.Equal(years, texts[1].Text);
        }
        finally
        {
            FloweryLocalization.SetCulture(previous);
        }
    }

    [AvaloniaTheory]
    [InlineData(1100, false)]
    [InlineData(420, true)]
    public void TheToolbarMovesItsButtonsBelowTheTitleOnlyWhereTheyDoNotFitBesideIt(double width, bool below)
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        scope.Scheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "new", Label = "New", IconData = SchedulerToolbarIcons.Add });
        scope.Scheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "print", Label = "Print", IconData = SchedulerToolbarIcons.Add });
        scope.Window.Width = width;
        scope.Layout();
        var toolbar = scope.Scheduler.GetVisualDescendants().OfType<DaisySchedulerToolbar>().Single();
        var buttons = new List<(string? Name, Rect Bounds)>();
        foreach (var button in toolbar.GetVisualDescendants().OfType<Button>().Where(static button => button.IsEffectivelyVisible))
            buttons.Add((button.Tag?.ToString(), new Rect(button.TranslatePoint(default, toolbar)!.Value, button.Bounds.Size)));

        foreach (var (name, bounds) in buttons)
            Assert.True(bounds.X >= -0.5 && bounds.Right <= toolbar.Bounds.Width + 0.5, $"{name} at {bounds} outside {toolbar.Bounds.Width}");
        for (var a = 0; a < buttons.Count; a++)
            for (var b = a + 1; b < buttons.Count; b++)
                Assert.False(buttons[a].Bounds.Intersects(buttons[b].Bounds), $"{buttons[a].Name} {buttons[a].Bounds} overlaps {buttons[b].Name} {buttons[b].Bounds}");
        var settings = buttons.Single(static button => button.Name == "Settings").Bounds;
        var date = buttons.Single(static button => button.Name == "Date").Bounds;
        Assert.True(below == settings.Y >= date.Bottom, $"Settings at {settings}, date at {date}, expected below the title: {below}");
    }

    [AvaloniaFact]
    public void TheGhostOfAMovedShortBarShowsItsWholeTimes()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var tenMinutes = new AppointmentModel { Id = 2, Subject = "Short", Start = Monday.AddHours(9), End = Monday.AddHours(9).AddMinutes(10), ResourceIds = { 1 } };
        scope.Scheduler.Appointments = new[] { tenMinutes };
        scope.Window.Width = 500;
        scope.Layout();
        var frame = scope.Frame(tenMinutes);
        frame.BringIntoView();
        scope.Layout();
        var origin = scope.Point(frame, frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        var lane = Assert.IsAssignableFrom<Canvas>(frame.Parent);
        try
        {
            scope.Window.MouseDown(origin, MouseButton.Left);
            scope.Window.MouseMove(origin + new Point(40, 0));
            scope.Layout();
            var ghost = lane.Children.OfType<Border>().Single(static child => child.ZIndex == 100);
            var times = Assert.IsType<TextBlock>(ghost.Child);

            Assert.True(ghost.Bounds.Width >= frame.Bounds.Width - 0.5, $"ghost {ghost.Bounds.Width}, bar {frame.Bounds.Width}");
            AssertNotTrimmed(times);
        }
        finally
        {
            scope.Window.MouseUp(origin + new Point(40, 0), MouseButton.Left);
        }
    }

    [AvaloniaFact]
    public void TheGhostOfABarOverTheEndOfTheDayStaysAtItsTimeAndOnItsRow()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var morning = new AppointmentModel { Id = 2, Subject = "Morning", Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { 1 } };
        var overnight = new AppointmentModel { Id = 3, Subject = "Night", Start = Monday.AddHours(20), End = Monday.AddHours(28), ResourceIds = { 1 } };
        scope.Scheduler.Appointments = new[] { morning, overnight };
        scope.Window.Width = 500;
        scope.Layout();
        var frame = scope.Frame(overnight);
        frame.BringIntoView();
        scope.Layout();
        var origin = scope.Point(frame, 30, frame.Bounds.Height / 2);
        var lane = Assert.IsAssignableFrom<Canvas>(frame.Parent);
        try
        {
            scope.Window.MouseDown(origin, MouseButton.Left);
            scope.Window.MouseMove(origin + new Point(40, 0));
            scope.Layout();
            var ghost = lane.Children.OfType<Border>().Single(static child => child.ZIndex == 100);

            // Moved later, the ghost starts after the bar, still reaches the end of the shown day and lies on the bar's row.
            Assert.True(ghost.Bounds.X > frame.Bounds.X, $"ghost at {ghost.Bounds.X}, bar at {frame.Bounds.X}");
            Assert.InRange(ghost.Bounds.Right, lane.Bounds.Width - 1, lane.Bounds.Width + 0.5);
            Assert.InRange(ghost.Bounds.Y, frame.Bounds.Y - 0.5, frame.Bounds.Y + 0.5);
        }
        finally
        {
            scope.Window.MouseUp(origin + new Point(40, 0), MouseButton.Left);
        }
    }

    [AvaloniaFact]
    public void TheGhostOfABarMovedIntoAnotherLaneLiesOnTheRowUnderThePointer()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, Name = "Alex" }, new ResourceModel { Id = 2, Name = "Blair" } };
        var others = Enumerable.Range(0, 3).Select(static index => new AppointmentModel
        {
            Id = 10 + index, Subject = "Other " + index, Start = Monday.AddHours(9 + index), End = Monday.AddHours(10 + index), ResourceIds = { 2 }
        }).ToArray();
        scope.Scheduler.Appointments = new[] { scope.Appointment }.Concat(others).ToArray();
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        frame.BringIntoView();
        scope.Layout();
        var third = scope.Frame(others[2]);
        var target = Assert.IsAssignableFrom<Canvas>(third.Parent);
        var origin = scope.Point(frame, frame.Bounds.Width / 2, frame.Bounds.Height / 2);
        var destination = scope.Point(target, frame.Bounds.X + frame.Bounds.Width / 2, third.Bounds.Y + third.Bounds.Height / 2);
        try
        {
            scope.Window.MouseDown(origin, MouseButton.Left);
            scope.Window.MouseMove(destination);
            scope.Layout();
            var ghost = target.Children.OfType<Border>().Single(static child => child.ZIndex == 100);

            Assert.InRange(ghost.Bounds.Y, third.Bounds.Y - 0.5, third.Bounds.Y + 0.5);
        }
        finally
        {
            scope.Window.MouseUp(destination, MouseButton.Left);
        }
    }

    [AvaloniaFact]
    public void TheGhostOfAResizedBarStaysOnItsRowWhenThePointerDriftsToAnother()
    {
        using var scope = new SchedulerScope(SchedulerView.Timeline);
        var second = new AppointmentModel { Id = 2, Subject = "Second", Start = Monday.AddHours(11), End = Monday.AddHours(12), ResourceIds = { 1 } };
        scope.Scheduler.Appointments = new[] { scope.Appointment, second };
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        frame.BringIntoView();
        scope.Layout();
        var lane = Assert.IsAssignableFrom<Canvas>(frame.Parent);
        var origin = scope.Point(frame, frame.Bounds.Width - 2, frame.Bounds.Height / 2);
        var destination = origin + new Point(40, scope.Frame(second).Bounds.Y - frame.Bounds.Y);
        try
        {
            scope.Window.MouseDown(origin, MouseButton.Left);
            scope.Window.MouseMove(destination);
            scope.Layout();
            var ghost = lane.Children.OfType<Border>().Single(static child => child.ZIndex == 100);

            // Resizing never changes the row, so the ghost stays on the bar's row.
            Assert.InRange(ghost.Bounds.Y, frame.Bounds.Y - 0.5, frame.Bounds.Y + 0.5);
        }
        finally
        {
            scope.Window.MouseUp(destination, MouseButton.Left);
        }
    }

    private static string Title(DaisySchedulerToolbar toolbar) =>
        string.Join(" ", ToolbarButton(toolbar, "Date").GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text));

    private static Button ToolbarButton(DaisySchedulerToolbar toolbar, string tag) =>
        toolbar.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Tag, tag));

    // A 1x1 transparent PNG.
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

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
