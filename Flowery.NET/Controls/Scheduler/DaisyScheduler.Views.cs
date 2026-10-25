// Avalonia adaptation of Xtreme.Components Scheduler (MIT).
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Controls.Scheduler.Utilities;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private ScrollViewer? _headerScroll;
    private ScrollViewer? _gutterScroll;
    private readonly List<Lane> _lanes = new();

    private sealed record Lane(Canvas Surface, DateTime Origin, ResourceModel? Resource,
        bool Horizontal, double FixedPixelsPerMinute, int DayCount = 1)
    {
        // Planner and Timeline lanes take the current scale of their surface, which follows the visible width.
        public double PixelsPerMinute => Surface is MinuteCanvas minutes ? minutes.PixelsPerMinute : FixedPixelsPerMinute;
    }

    // The Planner or Timeline view whose surfaces inherit its scale, and what that scale is computed from.
    private Control? _minuteView;
    private double _minuteMinimum;
    private double _minuteTotal;

    private double MinuteScale => _minuteView?.GetValue(MinuteCanvas.PixelsPerMinuteProperty) ?? 0;

    // Planner and Timeline hours fill the visible width. A new width only sets the scale; every surface arranges its
    // children by minutes, so appointment frames, the selection, focus and running gestures stay.
    private void RescaleHours(double visibleWidth) =>
        _minuteView?.SetValue(MinuteCanvas.PixelsPerMinuteProperty, Math.Max(_minuteMinimum, visibleWidth / _minuteTotal));

    private IReadOnlyList<ResourceModel> ActiveResources(DateTime start, DateTime end)
    {
        var resources = (SchedulerResources ?? Array.Empty<ResourceModel>())
            .Where(resource => (ResourceKindFilter is null || resource.Kind == ResourceKindFilter) && ShowsResources([resource.Id]))
            .ToArray();
        if (!HideIdleResources)
            return resources;
        if (CurrentView is SchedulerView.Planner or SchedulerView.Timeline)
        {
            var occupied = GetOccupiedResources(start, end, ResourceKindFilter);
            return resources.Where(occupied.Contains).ToArray();
        }
        // Day/Week draw stored times unchanged, including for resources with a time zone.
        var dates = VisibleDates();
        return resources.Where(resource => Items.Any(appointment => !appointment.IsCancelled &&
            MatchesResource(appointment, resource) && dates.Any(date => OnDate(appointment, date)))).ToArray();
    }

    private Border Cell(Control child, bool header = false, bool today = false) => new()
    {
        Child = child,
        Padding = new Thickness(SpacingXS),
        Background = Brush(today ? "DaisySchedulerTodayBrush" : header ? "DaisyBase200Brush" : "DaisyBase100Brush"),
        BorderBrush = Brush("DaisyBase300Brush"),
        BorderThickness = new Thickness(0, 0, LineThickness, LineThickness)
    };

    private Control BuildMonth()
    {
        var month = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        var start = WeekStart(month, FirstDayOfWeek);
        // The offsets within a week of the columns shown, in the order of the week.
        var columns = Enumerable.Range(0, 7).Where(day => ShowWeekends || !IsWeekend(start.AddDays(day))).ToArray();
        var days = Enumerable.Range(0, 42).Where(day => columns.Contains(day % 7)).ToArray();
        var maxAppointments = days.Max(day => Items.Count(appointment => OnDate(appointment, start.AddDays(day))));
        var minimumHeight = 6 * Math.Max(Token<double>("DaisySchedulerMonthMinimumCellHeight") * Scale,
            Token<double>($"DaisySize{Size}Height") + maxAppointments * (ChipHeight + SpacingXS / 2)) + ChipHeight;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", columns.Select(static _ => "*"))),
            RowDefinitions = new RowDefinitions("Auto,*,*,*,*,*,*"),
            MinWidth = Token<double>("DaisySchedulerMonthMinimumWidth") * Scale,
            Width = Math.Max(Token<double>("DaisySchedulerMonthMinimumWidth") * Scale, Bounds.Width - BorderThickness.Left - BorderThickness.Right - Padding.Left - Padding.Right - SpacingLarge),
            MinHeight = minimumHeight,
            Height = minimumHeight
        };
        _monthGrid = grid;
        for (var column = 0; column < columns.Length; column++)
        {
            var text = Label(start.AddDays(columns[column]).ToString("ddd", Culture), true);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            var header = Cell(text, header: true);
            Grid.SetColumn(header, column);
            grid.Children.Add(header);
        }
        foreach (var index in days)
        {
            var date = start.AddDays(index);
            var stack = new StackPanel { Spacing = 2, Margin = new Thickness(2) };
            var number = Label(date.Day.ToString(Culture), true);
            if (date.Month != month.Month)
                number.Foreground = Brush("DaisyBaseContentDisabledBrush");
            if (date == DateTime.Today)
            {
                number.Foreground = Brush("DaisyPrimaryForegroundBrush");
                number.FontWeight = FontWeight.Bold;
            }
            stack.Children.Add(number);
            var appointments = Items.Where(appointment => OnDate(appointment, date))
                .OrderByDescending(static appointment => appointment.IsAllDay).ThenBy(static appointment => appointment.Start).ToArray();
            foreach (var appointment in appointments)
                stack.Children.Add(CreateAppointment(appointment, AppointmentSurface.Chip));
            var cell = Cell(stack, today: date == DateTime.Today);
            cell.ClipToBounds = true;
            AttachEmptyDay(cell, date, null);
            Grid.SetColumn(cell, Array.IndexOf(columns, index % 7));
            Grid.SetRow(cell, index / 7 + 1);
            grid.Children.Add(cell);
        }
        return CreateScroll(grid);
    }

    private Control BuildAgenda()
    {
        var stack = new StackPanel { Spacing = 4 };
        foreach (var date in VisibleDates())
        {
            var title = Label(date.ToString("D", Culture));
            title.FontWeight = FontWeight.SemiBold;
            stack.Children.Add(Cell(title, header: true, today: date == DateTime.Today));
            var appointments = Items.Where(appointment => OnDate(appointment, date))
                .OrderBy(static appointment => appointment.Start).ToArray();
            if (appointments.Length == 0)
                stack.Children.Add(Cell(Label(Text("NoEvents"), true)));
            foreach (var appointment in appointments)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Margin = new Thickness(8, 4) };
                var time = Label(appointment.IsAllDay ? Text("AllDay") : FormatTime(appointment.Start) + " – " + FormatTime(appointment.End), true);
                time.MinWidth = Token<double>("DaisySchedulerAgendaTimeWidth") * Scale;
                row.Children.Add(time);
                var detail = new StackPanel { Spacing = 4 };
                detail.Children.Add(CreateAppointment(appointment, AppointmentSurface.Chip));
                var resources = SchedulerResources ?? Array.Empty<ResourceModel>();
                var names = resources.Where(resource => appointment.ResourceIds.Contains(resource.Id)).Select(static resource => resource.Name);
                detail.Children.Add(Label(string.Join(", ", names), true));
                Grid.SetColumn(detail, 1);
                row.Children.Add(detail);
                stack.Children.Add(Cell(row));
            }
        }
        return CreateScroll(stack);
    }

    private Control BuildTimeGrid()
    {
        var dates = VisibleDates();
        var resources = ActiveResources(dates[0], dates[^1].AddDays(1));
        var columns = new List<(DateTime Date, ResourceModel? Resource)>();
        if (SchedulerResources is { Count: > 0 } && resources.Count == 0)
            return Label(Text("NoEvents"));
        if (resources.Count == 0)
            columns.AddRange(dates.Select(static date => (date, (ResourceModel?)null)));
        else if (GroupMode == SchedulerGroupMode.ByResource)
        {
            foreach (var resource in resources)
                foreach (var date in dates)
                    columns.Add((date, resource));
        }
        else
        {
            foreach (var date in dates)
                foreach (var resource in resources)
                    columns.Add((date, resource));
        }
        var gutterWidth = Math.Max(Token<double>("DaisySchedulerTimeGutterMinimumWidth") * Scale, FontSize * 4);
        var width = Math.Max((ColumnMinWidth ?? Token<double>("DaisySchedulerColumnMinimumWidth")) * Scale,
            (Bounds.Width - gutterWidth - SpacingLarge - SpacingXS) / columns.Count);
        var header = new Grid();
        var body = new Grid { Height = 1440 * PixelsPerMinute };
        var gutter = new Canvas { Width = gutterWidth, Height = body.Height };
        var tick = Math.Clamp(TimeIncrement?.MajorTick.TotalMinutes ?? 60, 1, 1440);
        for (var minute = 0d; minute < 1440; minute += tick)
        {
            var label = Label(FormatTime(dates[0].AddMinutes(minute)), true);
            Canvas.SetTop(label, minute * PixelsPerMinute);
            gutter.Children.Add(label);
        }
        for (var index = 0; index < columns.Count; index++)
        {
            var (date, resource) = columns[index];
            header.ColumnDefinitions.Add(new ColumnDefinition(width, GridUnitType.Pixel));
            body.ColumnDefinitions.Add(new ColumnDefinition(width, GridUnitType.Pixel));
            var heading = new StackPanel { Spacing = 4 };
            heading.Children.Add(Label(date.ToString("ddd, d MMM", Culture)));
            if (resource is not null)
                heading.Children.Add(Label(resource.Name, true));
            var banners = new StackPanel { Spacing = 2, MinHeight = ChipHeight };
            foreach (var appointment in Items.Where(appointment => IsBanner(appointment) && OnDate(appointment, date) && MatchesResource(appointment, resource)))
                banners.Children.Add(CreateAppointment(appointment, AppointmentSurface.Chip, resourceId: resource?.Id));
            var allDay = new Border { Background = Brushes.Transparent, Child = banners };
            AttachEmptyDay(allDay, date, resource);
            heading.Children.Add(allDay);
            var headerCell = Cell(heading, header: true, today: date == DateTime.Today);
            Grid.SetColumn(headerCell, index);
            header.Children.Add(headerCell);

            var canvas = new Canvas { Width = width, Height = body.Height, Background = Brush("DaisyBase100Brush") };
            var lane = new Lane(canvas, date, resource, false, PixelsPerMinute);
            _lanes.Add(lane);
            if (WorkDayEnd > WorkDayStart)
            {
                AddRectangle(canvas, 0, 0, width, WorkDayStart.TotalMinutes * PixelsPerMinute, Brush("DaisyBase200Brush"));
                AddRectangle(canvas, 0, WorkDayEnd.TotalMinutes * PixelsPerMinute, width,
                    (1440 - WorkDayEnd.TotalMinutes) * PixelsPerMinute, Brush("DaisyBase200Brush"));
            }
            if (date == DateTime.Today)
                AddRectangle(canvas, 0, 0, width, body.Height, Brush("DaisySchedulerTodayBrush"));
            for (var minute = 0d; minute < 1440; minute += SnapMinutes)
                AddRectangle(canvas, 0, minute * PixelsPerMinute, width, 1, Brush("DaisyBase300Brush"));
            var timed = Items.Where(appointment => !IsBanner(appointment) && MatchesResource(appointment, resource));
            var layouts = EventBlockLayout.Layout(date, timed, Math.Max(15, ChipHeight / PixelsPerMinute));
            var blocks = new List<(Border Frame, EventLayout Layout)>();
            var minimumWidth = (ColumnMinWidth ?? Token<double>("DaisySchedulerColumnMinimumWidth")) * Scale;
            foreach (var layout in layouts)
            {
                minimumWidth = Math.Max(minimumWidth, layout.ColumnCount * TimedAppointmentMinimumWidth(layout.Appointment));
                var block = CreateAppointment(layout.Appointment, AppointmentSurface.Timed, lane);
                block.Width = Math.Max(1, width / layout.ColumnCount - 4);
                block.Height = Math.Max(ChipHeight, (layout.EndMinutes - layout.StartMinutes) * PixelsPerMinute - 2);
                Canvas.SetLeft(block, layout.ColumnIndex * width / layout.ColumnCount + 2);
                Canvas.SetTop(block, layout.StartMinutes * PixelsPerMinute);
                canvas.Children.Add(block);
                blocks.Add((block, layout));
            }
            if (date == DateTime.Today)
                AddRectangle(canvas, 0, DateTime.Now.TimeOfDay.TotalMinutes * PixelsPerMinute, width, 2, Brush("DaisyPrimaryBrush"));
            AttachLane(lane);
            var wrapper = new Border { Child = canvas, BorderBrush = Brush("DaisyBase300Brush"), BorderThickness = new Thickness(0, 0, 1, 0) };
            Grid.SetColumn(wrapper, index);
            body.Children.Add(wrapper);
            _timeColumns.Add(new TimeColumn(header.ColumnDefinitions[index], body.ColumnDefinitions[index], canvas,
                blocks, canvas.Children.OfType<Border>().Where(static child => !child.IsHitTestVisible).ToArray(), minimumWidth));
        }
        return FrozenGrid(header, gutter, body, gutterWidth);
    }

    private Control BuildPlanner()
    {
        var dayCount = CurrentView == SchedulerView.Timeline ? 1 : ShowWeekends ? 7 : 5;
        var start = dayCount == 1 ? SelectedDate.Date : PlannerStart();
        var end = start.AddDays(dayCount);
        var resources = ActiveResources(start, end);
        if (resources.Count == 0)
            return Label(Text("NoResources"));
        // Hours start at their minimum width; UpdateViewGeometry widens them to the visible width.
        var minimum = MinimumHourPixelsPerMinute(dayCount == 1);
        var totalMinutes = dayCount * 1440d;
        var rowHeight = RowHeight;
        var header = new MinuteCanvas(totalMinutes) { Height = ChipHeight * 2 };
        for (var day = 0; day < dayCount; day++)
        {
            var date = start.AddDays(day);
            var dayHeader = Cell(Label(date.ToString("ddd, d MMM", Culture)), header: true, today: date == DateTime.Today);
            dayHeader.Height = ChipHeight;
            MinuteCanvas.Place(dayHeader, day * 1440, 1440);
            header.Children.Add(dayHeader);
            for (var hour = 0; hour < 24; hour++)
            {
                var label = Label(FormatTime(date.AddHours(hour)), true);
                MinuteCanvas.Place(label, day * 1440 + hour * 60, offset: 4);
                Canvas.SetTop(label, ChipHeight);
                header.Children.Add(label);
            }
        }
        var body = new StackPanel();
        var gutter = new StackPanel { Width = Token<double>(dayCount == 1 ? "DaisySchedulerTimelineResourceWidth" : "DaisySchedulerPlannerResourceWidth") * Scale };
        foreach (var rowGroup in TimelineGroups(resources, start))
        {
            if (rowGroup.Name is not null)
            {
                var group = Cell(GroupHeading(rowGroup), header: true);
                group.Height = rowHeight;
                group.Padding = new Thickness(0);
                gutter.Children.Add(group);
                body.Children.Add(new Border { Height = rowHeight, Background = Brush("DaisyBase200Brush"),
                    BorderBrush = Brush("DaisyBase300Brush"), BorderThickness = new Thickness(0, 0, 0, LineThickness) });
            }
            if (rowGroup.Model is { } model && CollapsedResourceGroupIds?.Contains(model.Id) == true)
                continue;
            foreach (var resource in rowGroup.Resources)
            {
                var shown = new List<AppointmentModel>();
                var originals = new Dictionary<AppointmentModel, AppointmentModel>();
                foreach (var appointment in Items.Where(appointment => MatchesResource(appointment, resource)))
                {
                    var copy = appointment.IsAllDay ? appointment.Clone() : TimeZoneConverter.ToZone(appointment, resource.TimeZoneId);
                    if (copy.End <= start || copy.Start >= end || (rowGroup.Date is { } groupDate && !OnDate(copy, groupDate)))
                        continue;
                    shown.Add(copy);
                    originals[copy] = appointment;
                }
                // Every appointment has a row of its own (the occurrences of a series share it, a day apart), so a short
                // bar widened to its whole start time never overlaps another bar. The rows follow the start, longer first.
                shown.Sort(static (x, y) => x.Start != y.Start ? x.Start.CompareTo(y.Start) : y.End.CompareTo(x.End));
                var rows = new List<AppointmentModel>();
                foreach (var bar in shown)
                {
                    var original = OriginalAppointment(originals[bar]);
                    if (!rows.Contains(original))
                        rows.Add(original);
                }
                var height = Math.Max(1, rows.Count) * rowHeight;
                var canvas = new MinuteCanvas(totalMinutes) { Height = height, Background = Brush("DaisyBase100Brush") };
                var lane = new Lane(canvas, start, resource, true, minimum, dayCount);
                _lanes.Add(lane);
                for (var hour = 0; hour < dayCount * 24; hour++)
                    AddMinuteLine(canvas, hour * 60, 0, LineThickness, height);
                for (var row = 1; row <= Math.Max(1, rows.Count); row++)
                    AddMinuteLine(canvas, 0, row * rowHeight - LineThickness, double.NaN, LineThickness, totalMinutes);
                foreach (var bar in shown)
                {
                    var original = OriginalAppointment(originals[bar]);
                    var block = CreateAppointment(originals[bar], AppointmentSurface.Bar, original.IsAllDay ? null : lane, bar, resource.Id);
                    block.Height = ChipHeight;
                    // A bar that starts before or ends after the shown days is clipped to them.
                    var startMinutes = Math.Max(0, (bar.Start - start).TotalMinutes);
                    var endMinutes = Math.Min(totalMinutes, (bar.End - start).TotalMinutes);
                    MinuteCanvas.Place(block, startMinutes, endMinutes - startMinutes, trim: BarGap, minimumWidth: BarMinimumWidth(bar));
                    Canvas.SetTop(block, BarTop(rows.IndexOf(original)));
                    canvas.Children.Add(block);
                }
                AttachLane(lane);
                body.Children.Add(canvas);
                var names = new StackPanel();
                if (rows.Count == 0)
                    names.Children.Add(ResourceNameRow(resource.Name));
                foreach (var appointment in rows)
                    names.Children.Add(ResourceNameRow(resource.Name + " · " + appointment.Subject));
                var resourceCell = Cell(names);
                resourceCell.Padding = default;
                resourceCell.Height = height;
                resourceCell.BorderBrush = resource.Color is { } color ? new SolidColorBrush(color) : Brush("DaisyBase300Brush");
                resourceCell.BorderThickness = new Thickness(SpacingXS, 0, 0, 0);
                gutter.Children.Add(resourceCell);
            }
        }
        var view = FrozenGrid(header, gutter, body, gutter.Width);
        view.SetValue(MinuteCanvas.PixelsPerMinuteProperty, minimum);
        _minuteView = view;
        _minuteMinimum = minimum;
        _minuteTotal = totalMinutes;
        return view;
    }

    // A grid line of a Planner or Timeline lane: one hour line, or a row line across all minutes when given.
    private void AddMinuteLine(MinuteCanvas canvas, double startMinutes, double top, double width, double height, double minutes = double.NaN)
    {
        var line = new Border { Width = width, Height = height, Background = Brush("DaisyBase300Brush"), IsHitTestVisible = false };
        MinuteCanvas.Place(line, startMinutes, minutes);
        Canvas.SetTop(line, top);
        canvas.Children.Add(line);
    }

    private Border ResourceNameRow(string name) => new()
    {
        Height = RowHeight, Padding = new Thickness(SpacingXS, 0), Child = Label(name, true),
        BorderBrush = Brush("DaisyBase300Brush"), BorderThickness = new Thickness(0, 0, 0, LineThickness)
    };

    private static bool MatchesResource(AppointmentModel appointment, ResourceModel? resource) =>
        resource is null || appointment.ResourceIds.Contains(resource.Id);

    private static bool IsBanner(AppointmentModel appointment) => appointment.IsAllDay ||
        appointment.End > appointment.Start && appointment.Start.Date != appointment.End.AddTicks(-1).Date;

    private ScrollViewer CreateScroll(Control content)
    {
        _bodyScroll = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _bodyScroll.Loaded += OnBodyLoaded;
        _bodyScroll.ScrollChanged += OnBodyScrolled;
        return _bodyScroll;
    }

    private Control FrozenGrid(Control header, Control gutter, Control body, double gutterWidth)
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        _headerScroll = new ScrollViewer { Content = header, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _gutterScroll = new ScrollViewer { Content = gutter, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        var scroll = CreateScroll(body);
        var corner = Cell(Label(CurrentView is SchedulerView.Planner or SchedulerView.Timeline ? Text("Resources") : Text("AllDay"), true), header: true);
        corner.Width = gutterWidth;
        root.Children.Add(corner);
        Grid.SetColumn(_headerScroll, 1);
        root.Children.Add(_headerScroll);
        Grid.SetRow(_gutterScroll, 1);
        root.Children.Add(_gutterScroll);
        Grid.SetColumn(scroll, 1);
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        return root;
    }

    private void OnBodyScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _bodyScroll) && _bodyScroll is { } body)
        {
            if (e.ViewportDelta != default)
                UpdateViewGeometry();
            if (_headerScroll is not null)
                _headerScroll.Offset = new Vector(body.Offset.X, 0);
            if (_gutterScroll is not null)
                _gutterScroll.Offset = new Vector(0, body.Offset.Y);
        }
    }

    private void OnBodyLoaded(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _bodyScroll) || _bodyScroll is null)
            return;
        _bodyScroll.Loaded -= OnBodyLoaded;
        UpdateViewGeometry();
        var initial = CurrentView switch
        {
            SchedulerView.Day or SchedulerView.Week => new Vector(0, 7 * 60 * PixelsPerMinute),
            SchedulerView.Planner => new Vector(WorkDayStart.TotalMinutes * MinuteScale, 0),
            SchedulerView.Timeline => new Vector(7 * 60 * MinuteScale, 0),
            _ => default
        };
        _bodyScroll.Offset = _savedOffset ?? initial;
    }

    private static void AddRectangle(Canvas canvas, double x, double y, double width, double height, IBrush fill)
    {
        if (height <= 0 || width <= 0)
            return;
        var rectangle = new Border { Width = width, Height = height, Background = fill, IsHitTestVisible = false };
        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        canvas.Children.Add(rectangle);
    }
}
