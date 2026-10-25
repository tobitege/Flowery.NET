using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Controls.Scheduler.Utilities;
using Flowery.Localization;
using System.Text.Json;
using Xunit;

namespace Flowery.NET.Tests;

[Collection("LocalizationTests")]
public sealed partial class SchedulerTests
{
    private static readonly DateTime Monday = new(2026, 10, 12);

    [AvaloniaTheory]
    [InlineData(SchedulerView.Day)]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Agenda)]
    [InlineData(SchedulerView.Planner)]
    [InlineData(SchedulerView.Timeline)]
    public void EveryViewRendersHostAppointmentsAndSupportsSelection(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var block = scope.Frame(scope.Appointment);
        Assert.True(block.Bounds.Width > 0);
        Assert.True(block.Bounds.Height > 0);
        scope.Click(block);
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.Single(scope.Scheduler.SelectedAppointments);
    }

    [AvaloniaFact]
    public void DragMoveReportsSnappedProposalWithoutMutatingHostData()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        AppointmentMoveArgs? request = null;
        scope.Scheduler.AppointmentMoveRequested += (_, args) => request = args;
        var block = scope.Frame(scope.Appointment);
        var origin = scope.Point(block, block.Bounds.Width / 2, block.Bounds.Height / 2);
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(origin + new Vector(0, 48));
        scope.Window.MouseUp(origin + new Vector(0, 48), MouseButton.Left);
        scope.Layout();
        Assert.NotNull(request);
        Assert.Same(scope.Appointment, request.Appointment);
        Assert.Equal(Monday.AddHours(10), request.NewStart);
        Assert.Equal(Monday.AddHours(11), request.NewEnd);
        Assert.Equal(Monday.AddHours(9), scope.Appointment.Start);
        Assert.Equal(Monday.AddHours(10), scope.Appointment.End);
    }

    [AvaloniaFact]
    public void SelectedEdgeResizeReportsDurationChangeWithoutMutatingHostData()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        scope.Layout();
        AppointmentResizeArgs? request = null;
        scope.Scheduler.AppointmentResizeRequested += (_, args) => request = args;
        var block = scope.Frame(scope.Appointment);
        var origin = scope.Point(block, block.Bounds.Width / 2, block.Bounds.Height - 2);
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(origin + new Vector(0, 24));
        scope.Window.MouseUp(origin + new Vector(0, 24), MouseButton.Left);
        scope.Layout();
        Assert.NotNull(request);
        Assert.Equal(Monday.AddHours(9), request.NewStart);
        Assert.Equal(Monday.AddHours(10.5), request.NewEnd);
        Assert.Equal(Monday.AddHours(10), scope.Appointment.End);
    }

    [AvaloniaFact]
    public void DraggingAnEmptyTimeRangeRequestsCreation()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        AppointmentCreateArgs? request = null;
        scope.Scheduler.AppointmentCreateRequested += (_, args) => request = args;
        var surface = scope.Scheduler.GetVisualDescendants().OfType<Canvas>()
            .Single(canvas => canvas.Height == 1152 && canvas.Width > 100);
        var origin = scope.Point(surface, surface.Width / 2, 11 * 60 * 0.8);
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(origin + new Vector(0, 48));
        scope.Window.MouseUp(origin + new Vector(0, 48), MouseButton.Left);
        scope.Layout();
        Assert.NotNull(request);
        Assert.Equal(Monday.AddHours(11), request.Start);
        Assert.Equal(Monday.AddHours(12.5), request.End);
        Assert.False(request.IsAllDay);
        Assert.Single(scope.Scheduler.Appointments!);
    }

    [AvaloniaFact]
    public void ReadOnlyPreventsMoveResizeCreateAndDeleteButKeepsSelection()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        scope.Scheduler.IsReadOnly = true;
        scope.Layout();
        var writes = 0;
        scope.Scheduler.AppointmentMoveRequested += (_, _) => writes++;
        scope.Scheduler.AppointmentResizeRequested += (_, _) => writes++;
        scope.Scheduler.AppointmentCreateRequested += (_, _) => writes++;
        scope.Scheduler.AppointmentsDeleteRequested += (_, _) => writes++;
        var block = scope.Frame(scope.Appointment);
        var origin = scope.Point(block, block.Bounds.Width / 2, block.Bounds.Height / 2);
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(origin + new Vector(0, 48));
        scope.Window.MouseUp(origin + new Vector(0, 48), MouseButton.Left);
        scope.Scheduler.DeleteSelected();
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.Equal(0, writes);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Week, DayOfWeek.Monday, 0)]
    [InlineData(SchedulerView.Week, DayOfWeek.Sunday, 0)]
    [InlineData(SchedulerView.Planner, DayOfWeek.Monday, 0)]
    [InlineData(SchedulerView.Planner, DayOfWeek.Sunday, 0)]
    [InlineData(SchedulerView.Planner, DayOfWeek.Saturday, 0)]
    [InlineData(SchedulerView.Planner, DayOfWeek.Wednesday, -7)]
    public void WithoutWeekendsWeekAndPlannerShowMondayToFriday(SchedulerView view, DayOfWeek firstDay, int mondayOffset)
    {
        using var scope = new SchedulerScope(view);
        Assert.True(scope.Scheduler.ShowWeekends);
        scope.Scheduler.FirstDayOfWeek = firstDay;
        scope.Scheduler.ShowWeekends = false;
        scope.Layout();

        // A week from Wednesday to Tuesday shares three weekdays with the Monday before it and two with the one after.
        var monday = Monday.AddDays(mondayOffset);
        var culture = scope.Scheduler.FormattingCulture;
        var texts = scope.Scheduler.GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text).ToArray();
        for (var day = 0; day < 5; day++)
            Assert.Contains(monday.AddDays(day).ToString("ddd, d MMM", culture), texts);
        Assert.DoesNotContain(monday.AddDays(-1).ToString("ddd, d MMM", culture), texts);
        Assert.DoesNotContain(monday.AddDays(5).ToString("ddd, d MMM", culture), texts);
    }

    [AvaloniaFact]
    public void WithoutWeekendsTheMonthShowsFiveColumns()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        Grid MonthGrid() => scope.Scheduler.GetVisualDescendants().OfType<Grid>().First(static grid => grid.RowDefinitions.Count == 7);
        Assert.Equal(7, MonthGrid().ColumnDefinitions.Count);

        scope.Scheduler.ShowWeekends = false;
        scope.Layout();

        var grid = MonthGrid();
        var culture = scope.Scheduler.FormattingCulture;
        var headers = grid.Children.OfType<Border>().Where(static cell => Grid.GetRow(cell) == 0)
            .Select(static cell => ((TextBlock)cell.Child!).Text).ToArray();
        Assert.Equal(Enumerable.Range(0, 5).Select(day => Monday.AddDays(day).ToString("ddd", culture)), headers);
        Assert.Equal(5, grid.ColumnDefinitions.Count);
        Assert.Equal(5 + 6 * 5, grid.Children.Count);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerClockFormat.TwelveHour, "9a")]
    [InlineData(SchedulerClockFormat.TwentyFourHour, "09:00")]
    public void SingleLineChipShowsTheCompactStart(SchedulerClockFormat clock, string expected)
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        scope.Scheduler.ClockFormat = clock;
        scope.Layout();
        var texts = scope.Frame(scope.Appointment).GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text).ToArray();
        Assert.Contains(expected, texts);
        Assert.Contains(scope.Appointment.Subject, texts);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Timeline)]
    public void SelectingAnAppointmentKeepsItsSizeAndItsContentInPlace(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var frame = scope.Frame(scope.Appointment);
        var content = frame.Child!;
        var size = frame.Bounds.Size;
        var position = content.TranslatePoint(default, scope.Scheduler);
        var thickness = frame.BorderThickness;
        var border = frame.BorderBrush;
        var background = ((ISolidColorBrush)frame.Background!).Color;

        scope.Click(frame);

        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.Same(frame, scope.Frame(scope.Appointment));
        Assert.Equal(size, frame.Bounds.Size);
        Assert.Equal(position, content.TranslatePoint(default, scope.Scheduler));
        Assert.Equal(thickness, frame.BorderThickness);
        Assert.True(scope.Scheduler.TryFindResource("DaisyBaseContentBrush", scope.Scheduler.ActualThemeVariant, out var text));
        Assert.Same(text, frame.BorderBrush);
        Assert.NotEqual(background, ((ISolidColorBrush)frame.Background!).Color);

        scope.Scheduler.SelectedAppointment = null;

        Assert.Same(border, frame.BorderBrush);
        Assert.Equal(background, ((ISolidColorBrush)frame.Background!).Color);
    }

    [AvaloniaFact]
    public void LockedAppointmentCannotBeSelectedOrDeletedButAllowsContextRequest()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        scope.Appointment.IsLocked = true;
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var block = scope.Frame(scope.Appointment);
        scope.Click(block);
        Assert.Null(scope.Scheduler.SelectedAppointment);
        AppointmentContextRequestedArgs? context = null;
        scope.Scheduler.AppointmentContextRequested += (_, args) => context = args;
        scope.Click(block, MouseButton.Right);
        Assert.NotNull(context);
        Assert.Same(scope.Appointment, context.Appointment);
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        Assert.Empty(scope.Scheduler.SelectedAppointments);
    }

    [AvaloniaFact]
    public void DeleteKeyReportsSelectionAndLeavesTheInputListIntact()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        AppointmentsDeleteArgs? request = null;
        scope.Scheduler.AppointmentsDeleteRequested += (_, args) => request = args;
        scope.Click(scope.Frame(scope.Appointment));
        scope.Window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        scope.Window.KeyRelease(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Assert.NotNull(request);
        Assert.Same(scope.Appointment, Assert.Single(request.Appointments));
        Assert.Single(scope.Scheduler.Appointments!);
        Assert.Empty(scope.Scheduler.SelectedAppointments);
    }

    [AvaloniaFact]
    public void RefreshPreservesScrollAndMatchesSelectionToReplacedHostInstance()
    {
        using var scope = new SchedulerScope(SchedulerView.Day);
        var body = scope.Scheduler.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(scroll => scroll.HorizontalScrollBarVisibility == Avalonia.Controls.Primitives.ScrollBarVisibility.Auto &&
                scroll.VerticalScrollBarVisibility == Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        body.Offset = new Vector(0, 480);
        scope.Scheduler.SelectedAppointment = scope.Appointment;
        var updated = scope.Appointment.Clone();
        updated.Subject = "Updated";
        scope.Scheduler.Appointments = new[] { updated };
        scope.Layout();
        var refreshed = scope.Scheduler.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(scroll => scroll.HorizontalScrollBarVisibility == Avalonia.Controls.Primitives.ScrollBarVisibility.Auto &&
                scroll.VerticalScrollBarVisibility == Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        Assert.Equal(480, refreshed.Offset.Y);
        Assert.Same(updated, scope.Scheduler.SelectedAppointment);
        Assert.Contains(scope.Scheduler.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Updated");
    }

    [AvaloniaFact]
    public void GlobalSizeChangesFontsAndExplicitSizeIsRespected()
    {
        var previous = FlowerySizeManager.CurrentSize;
        using var scope = new SchedulerScope(SchedulerView.Month);
        try
        {
            scope.Scheduler.ClearValue(DaisyScheduler.SizeProperty);
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            scope.Layout();
            Assert.Equal(DaisySize.ExtraLarge, scope.Scheduler.Size);
            Assert.Equal(20, scope.Scheduler.FontSize);
            Assert.All(scope.Scheduler.GetVisualDescendants().OfType<DaisyButton>(), button => Assert.Equal(DaisySize.ExtraLarge, button.Size));
            scope.Scheduler.Size = DaisySize.Small;
            FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
            scope.Layout();
            Assert.Equal(DaisySize.Small, scope.Scheduler.Size);
            Assert.Equal(12, scope.Scheduler.FontSize);
        }
        finally { FlowerySizeManager.ApplySize(previous); }
    }

    [AvaloniaFact]
    public void ThemeChangesRefreshAppointmentColorsAndCustomFillsKeepReadableText()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        using var scope = new SchedulerScope(SchedulerView.Month);
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            scope.Layout();
            // DynamicResource updates a live brush. Capture the color value before switching.
            var light = ((ISolidColorBrush)((AppointmentContext)scope.Frame(scope.Appointment).DataContext!).BackBrush).Color;
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            scope.Layout();
            var dark = ((AppointmentContext)scope.Frame(scope.Appointment).DataContext!).BackBrush;
            Assert.NotEqual(light, ((ISolidColorBrush)dark).Color);
            scope.Appointment.BackColor = Colors.Black;
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            scope.Layout();
            var context = (AppointmentContext)scope.Frame(scope.Appointment).DataContext!;
            Assert.Equal(Colors.White, ((ISolidColorBrush)context.Foreground).Color);
        }
        finally { DaisyThemeManager.ApplyTheme(previous ?? "Light"); }
    }

    [AvaloniaTheory]
    [InlineData("Coffee", false)]
    [InlineData("Light", false)]
    [InlineData("Dark", false)]
    [InlineData("Corporate", false)]
    [InlineData("Light", true)]
    public void NameTagsUseTheirOwnBackgroundForReadableContrast(string theme, bool darkAppointment)
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        using var scope = new SchedulerScope(SchedulerView.Month);
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme(theme));
            Assert.True(scope.Scheduler.TryFindResource("DaisyBase300Brush", scope.Scheduler.ActualThemeVariant, out var tagResource));
            var initialTagColor = Assert.IsAssignableFrom<ISolidColorBrush>(tagResource).Color;
            if (darkAppointment)
                scope.Appointment.BackColor = Colors.Black;
            scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Alex" });
            scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Dark tag", BackColor = Colors.Black });
            scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Light tag", BackColor = Colors.White });
            scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Translucent tag", BackColor = Color.FromArgb(128, 255, 255, 255) });
            scope.Appointment.Tiles.Add(new AppointmentDecoration { Text = "Explicit tag", BackColor = Colors.Black, ForeColor = Colors.Orange });
            scope.Appointment.Adornments.Add(new AppointmentDecoration { Text = "Overlay" });
            scope.Scheduler.ShowTiles = true;
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            scope.Layout();

            var frame = scope.Frame(scope.Appointment);
            var labels = frame.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var context = Assert.IsType<AppointmentContext>(frame.DataContext);
            var backdrop = Assert.IsAssignableFrom<ISolidColorBrush>(context.BackBrush).Color;
            foreach (var name in new[] { "Alex", "Dark tag", "Light tag", "Translucent tag" })
            {
                var label = Assert.Single(labels, text => text.Text == name);
                var badge = Assert.IsType<Border>(label.Parent);
                var fill = Assert.IsAssignableFrom<ISolidColorBrush>(badge.Background).Color;
                var textColor = Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color;
                var opacity = fill.A / 255d;
                var rendered = Color.FromRgb(
                    (byte)Math.Round(fill.R * opacity + backdrop.R * (1 - opacity)),
                    (byte)Math.Round(fill.G * opacity + backdrop.G * (1 - opacity)),
                    (byte)Math.Round(fill.B * opacity + backdrop.B * (1 - opacity)));
                var foregroundLuminance = Luminance(textColor);
                var backgroundLuminance = Luminance(rendered);
                var ratio = (Math.Max(foregroundLuminance, backgroundLuminance) + 0.05) /
                    (Math.Min(foregroundLuminance, backgroundLuminance) + 0.05);
                Assert.True(ratio >= 4.5, $"{theme} / {name}: contrast {ratio:F2}; text {textColor}, fill {fill}, initial tile {initialTagColor}, appointment {backdrop}");
            }
            var explicitTag = Assert.Single(labels, text => text.Text == "Explicit tag");
            Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(explicitTag.Foreground).Color);
            var overlay = Assert.Single(labels, text => text.Text == "Overlay");
            Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(context.Foreground).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(overlay.Foreground).Color);
        }
        finally { DaisyThemeManager.ApplyTheme(previous ?? "Light"); }

        static double Luminance(Color color)
        {
            static double Linear(byte channel) => channel <= 10 ? channel / 3294.6 : Math.Pow((channel / 255d + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }
    }

    [AvaloniaFact]
    public void PlannerConvertsDisplayedTimesButSelectionReturnsOriginalAppointment()
    {
        using var scope = new SchedulerScope(SchedulerView.Planner);
        scope.Scheduler.SchedulerResources = new[] { new ResourceModel { Id = 1, Name = "New York", TimeZoneId = "America/New_York" } };
        scope.Appointment.Start = Monday.AddHours(13);
        scope.Appointment.End = Monday.AddHours(14);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Layout();
        var frame = scope.Frame(scope.Appointment);
        var context = (AppointmentContext)frame.DataContext!;
        Assert.Equal(Monday.AddHours(9), context.Start);
        scope.Click(frame);
        Assert.Same(scope.Appointment, scope.Scheduler.SelectedAppointment);
        Assert.Equal(Monday.AddHours(13), scope.Appointment.Start);
    }

    [AvaloniaTheory]
    [InlineData("Corporate")]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("Coffee")]
    public void HoverTooltipTextInheritsTheTooltipPalette(string theme)
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        using var scope = new SchedulerScope(SchedulerView.Planner);
        try
        {
            Assert.True(DaisyThemeManager.ApplyTheme(theme));
            scope.Appointment.Description = "Appointment note";
            scope.Appointment.Memo = "Internal memo";
            scope.Appointment.Details.Add(new AppointmentDetail("Resource", "Alex"));
            scope.Scheduler.Appointments = new[] { scope.Appointment };
            scope.Layout();
            var frame = scope.Frame(scope.Appointment);
            var tooltip = Assert.IsType<ToolTip>(ToolTip.GetTip(frame));
            var content = Assert.IsType<StackPanel>(tooltip.Content);
            ToolTip.SetIsOpen(frame, true);
            scope.Layout();
            try
            {
                var popup = content.GetVisualAncestors().OfType<ToolTip>().Single();
                var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(popup.Foreground).Color;
                var background = Assert.IsAssignableFrom<ISolidColorBrush>(popup.Background).Color;
                Assert.True(scope.Scheduler.TryFindResource("DaisyBase200Brush", scope.Scheduler.ActualThemeVariant, out var backgroundResource));
                Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(backgroundResource).Color, background);
                var border = Assert.IsAssignableFrom<ISolidColorBrush>(popup.BorderBrush).Color;
                Assert.NotEqual(background, border);
                Assert.True(popup.BorderThickness.Left > 0);
                Assert.NotEqual(background, foreground);
                Assert.All(content.Children.OfType<TextBlock>(), label =>
                    Assert.Equal(foreground, Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color));
            }
            finally { ToolTip.SetIsOpen(frame, false); }
        }
        finally { DaisyThemeManager.ApplyTheme(previous ?? "Light"); }
    }

    [AvaloniaFact]
    public void MonthRespectsFirstDayOfWeekAndMidnightEndIsExclusive()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        scope.Appointment.IsAllDay = true;
        scope.Appointment.Start = Monday;
        scope.Appointment.End = Monday.AddDays(1);
        scope.Scheduler.Appointments = new[] { scope.Appointment };
        scope.Scheduler.FirstDayOfWeek = DayOfWeek.Sunday;
        scope.Layout();
        Assert.Single(scope.Frames(scope.Appointment));
        var grid = scope.Scheduler.GetVisualDescendants().OfType<Grid>().Single(control => control.RowDefinitions.Count == 7 && control.ColumnDefinitions.Count == 7);
        var header = (Border)grid.Children[0];
        Assert.Equal(new DateTime(2026, 9, 27).ToString("ddd", Flowery.Localization.FloweryLocalization.CurrentCulture), ((TextBlock)header.Child!).Text);
    }

    [AvaloniaFact]
    public void MonthFitsTheViewportAfterTheHostResizes()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        scope.Window.Width = 760;
        scope.Layout();
        var scroll = scope.Scheduler.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        Assert.True(scope.Frame(scope.Appointment).Bounds.Width > 0);
    }

    [AvaloniaFact]
    public void EveryLanguageHasSchedulerResourcesAndUpdatesAttachedControls()
    {
        var previousCulture = FloweryLocalization.CurrentCulture;
        var previousResolver = FloweryLocalization.CustomResolver;
        var assembly = typeof(DaisyScheduler).Assembly;
        using var englishStream = assembly.GetManifestResourceStream("Flowery.NET.Localization.en.json")!;
        using var english = JsonDocument.Parse(englishStream);
        var requiredKeys = english.RootElement.EnumerateObject()
            .Where(static property => property.Name.StartsWith("Scheduler_", StringComparison.Ordinal))
            .Select(static property => property.Name).OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(requiredKeys);
        using var scope = new SchedulerScope(SchedulerView.Planner);
        try
        {
            // App keys must not replace the library's view names or resource headings.
            FloweryLocalization.CustomResolver = static _ => "Host override";
            foreach (var language in FloweryLocalization.SupportedLanguages)
            {
                using var stream = assembly.GetManifestResourceStream($"Flowery.NET.Localization.{language}.json")!;
                using var document = JsonDocument.Parse(stream);
                var keys = document.RootElement.EnumerateObject()
                    .Where(static property => property.Name.StartsWith("Scheduler_", StringComparison.Ordinal))
                    .Select(static property => property.Name).OrderBy(static key => key, StringComparer.Ordinal).ToArray();
                Assert.Equal(requiredKeys, keys);
                foreach (var key in keys)
                    Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty(key).GetString()));

                FloweryLocalization.SetCulture(language);
                scope.Layout();
                foreach (var button in scope.Scheduler.GetVisualDescendants().OfType<DaisyButton>())
                {
                    if (button.Tag is SchedulerView view)
                        Assert.Equal(document.RootElement.GetProperty("Scheduler_" + view).GetString(), button.Content);
                    else if (button.Tag is string action)
                    {
                        if (action is "Today" or "Settings")
                            Assert.Equal(document.RootElement.GetProperty("Scheduler_" + action).GetString(), button.Content);
                        var key = action == "Date" ? "ChooseDate" : action;
                        Assert.Equal(document.RootElement.GetProperty("Scheduler_" + key).GetString(), AutomationProperties.GetName(button));
                    }
                }
                var resourceHeading = document.RootElement.GetProperty("Scheduler_Resources").GetString();
                Assert.Contains(resourceHeading, scope.Scheduler.GetVisualDescendants().OfType<TextBlock>().Select(static text => text.Text));
                Assert.Equal("Install", scope.Appointment.Subject);
                Assert.Equal(Monday.AddHours(9), scope.Appointment.Start);
            }
        }
        finally
        {
            FloweryLocalization.CustomResolver = previousResolver;
            FloweryLocalization.SetCulture(previousCulture);
        }
    }

    [Fact]
    public void OverlapPackingAndCloneKeepTheUpstreamDataContract()
    {
        var first = new AppointmentModel { Start = Monday.AddHours(9), End = Monday.AddHours(11), ResourceIds = { 1 }, Tiles = { new AppointmentDecoration { Text = "Tile" } } };
        var second = new AppointmentModel { Start = Monday.AddHours(10), End = Monday.AddHours(12) };
        var third = new AppointmentModel { Start = Monday.AddHours(13), End = Monday.AddHours(14) };
        var layouts = EventBlockLayout.Layout(Monday, new[] { first, second, third });
        Assert.Equal(2, layouts.Single(layout => layout.Appointment == first).ColumnCount);
        Assert.Equal(1, layouts.Single(layout => layout.Appointment == third).ColumnCount);
        var copy = first.Clone();
        copy.ResourceIds.Add(2);
        copy.Tiles.Clear();
        Assert.Single(first.ResourceIds);
        Assert.Single(first.Tiles);
    }

    [Fact]
    public void ExternalDragDataCarriesTypedSourceItem()
    {
        var item = new Droppable();
        using var data = ExternalDropHandler.CreateDataTransfer(item);
        Assert.Same(item, data.TryGetValue(ExternalDropHandler.Format));
    }

    private sealed class Droppable : ISchedulerDroppable
    {
        public string Subject => "External";
        public TimeSpan DefaultDuration => TimeSpan.FromHours(1);
        public object? Payload => null;
    }

    private sealed class SchedulerScope : IDisposable
    {
        public AppointmentModel Appointment { get; } = new() { Id = 1, Subject = "Install", Start = Monday.AddHours(9), End = Monday.AddHours(10), ResourceIds = { 1 } };
        public DaisyScheduler Scheduler { get; }
        public Window Window { get; }

        public SchedulerScope(SchedulerView view)
        {
            Scheduler = new DaisyScheduler
            {
                CurrentView = view, SelectedDate = new DateTimeOffset(Monday), Size = DaisySize.Medium,
                Appointments = new[] { Appointment },
                SchedulerResources = view is SchedulerView.Planner or SchedulerView.Timeline ? new[] { new ResourceModel { Id = 1, Name = "Alex" } } : Array.Empty<ResourceModel>()
            };
            Window = new Window { Width = 1100, Height = 720, Content = Scheduler };
            Window.Show();
            Layout();
        }

        public IEnumerable<Border> Frames(AppointmentModel appointment) => Scheduler.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Focusable && border.DataContext is AppointmentContext context && Equals(context.Appointment.Id, appointment.Id));
        public Border Frame(AppointmentModel appointment) => Frames(appointment).First();
        public Point Point(Control control, double x, double y) => control.TranslatePoint(new Point(x, y), Window)!.Value;
        public void Click(Control control, MouseButton button = MouseButton.Left)
        {
            var point = Point(control, control.Bounds.Width / 2, control.Bounds.Height / 2);
            Window.MouseDown(point, button);
            Window.MouseUp(point, button);
            Layout();
        }
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}
