using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Flowery.Controls.Scheduler.Models;
using Flowery.Localization;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    /// <summary>Raised when what the toolbars of this scheduler show has changed.</summary>
    internal event EventHandler? ToolbarChanged;

    internal static string Text(string key) => FloweryLocalization.GetStringInternal("Scheduler_" + key);

    internal double ToolbarCornerRadius => Token<CornerRadius>($"DaisySize{Size}CornerRadius").TopLeft;

    internal double ToolbarButtonHeight => Token<double>($"DaisySize{Size}Height");

    internal double ToolbarTitleFontSize => Token<double>($"DaisySize{Size}HeaderFontSize");

    internal double ToolbarIconSize => Math.Round(Token<double>("DaisySchedulerToolbarIconSize") * Scale);

    private void UpdateToolbar() => ToolbarChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>The title of the shown dates: the emphasized part, such as the month, and the rest, such as the year.</summary>
    // The month or the days lead in bold and the year follows muted. A culture that writes the year first keeps it first
    // in one text: for the month, and for days where it also writes the month before the day (such as Japanese or
    // Chinese). Days are written in the culture's order of month and day, with the month abbreviated. A single day
    // keeps the culture's long date with its weekday.
    internal (string Primary, string Secondary) ToolbarTitle()
    {
        var format = Culture.DateTimeFormat;
        var yearMonth = format.YearMonthPattern;
        var yearFirst = yearMonth.IndexOf('y') < yearMonth.IndexOf('M');
        if (CurrentView == SchedulerView.Month)
            return yearFirst
                ? (SelectedDate.ToString(yearMonth, Culture), string.Empty)
                : (SelectedDate.ToString("MMMM", Culture), SelectedDate.ToString("yyyy", Culture));
        var dates = VisibleDates();
        var (first, last) = (dates[0], dates[^1]);
        if (dates.Length == 1)
            return (first.ToString("D", Culture), string.Empty);
        var monthDay = format.MonthDayPattern.Replace("MMMM", "MMM");
        // Years as the culture's calendar counts them, which need not change on 1 January.
        var sameYear = format.Calendar.GetYear(first) == format.Calendar.GetYear(last);
        // Only a culture that writes year, month and day in this order gets the year before the days; one that writes the
        // year first but the day before the month (such as Persian) keeps the year after them.
        if (yearFirst && monthDay.StartsWith('M'))
        {
            // The year as the culture writes it before the month, such as "yyyy年".
            var withYear = yearMonth[..yearMonth.IndexOf('M')] + monthDay;
            return (first.ToString(withYear, Culture) + " – " + last.ToString(sameYear ? monthDay : withYear, Culture), string.Empty);
        }
        var years = sameYear
            ? last.ToString("yyyy", Culture)
            : first.ToString("yyyy", Culture) + " – " + last.ToString("yyyy", Culture);
        return (first.ToString(monthDay, Culture) + " – " + last.ToString(monthDay, Culture), years);
    }

    internal void GoToToday()
    {
        SetCurrentValue(SelectedDatesProperty, null);
        SetCurrentValue(SelectedDateProperty, new DateTimeOffset(DateTime.Today));
    }

    internal void ShowDatePicker(Control anchor)
    {
        var calendar = new Calendar { SelectedDate = SelectedDate.Date, DisplayDate = SelectedDate.Date, FirstDayOfWeek = FirstDayOfWeek };
        calendar.SelectedDatesChanged += OnDatePicked;
        new Flyout { Content = calendar }.ShowAt(anchor);
    }

    internal void ShowSettings(Control anchor) =>
        new Flyout { Content = BuildSettings(), FlyoutPresenterClasses = { "daisy-scheduler" } }.ShowAt(anchor);

    // Toolbar tooltips open below their button. A tooltip at the pointer keeps the scaling of the monitor it was first
    // placed on (Avalonia 12.1.4, see AvaloniaUI/Avalonia#22573), so on a second monitor with another scaling it was a
    // pixel too narrow and cut its text.
    internal void SetToolbarTooltip(Control target, string? text)
    {
        SetSchedulerTooltip(target, text);
        ToolTip.SetPlacement(target, PlacementMode.Bottom);
        ToolTip.SetVerticalOffset(target, SpacingXS);
    }

    internal MenuFlyout CreateToolbarMenu(SchedulerToolbarButton model, bool adornment)
    {
        var menu = new MenuFlyout();
        foreach (var entry in model.MenuItems ?? Array.Empty<SchedulerToolbarButton>())
            menu.Items.Add(CreateMenuItem(entry, adornment));
        return menu;
    }

    private MenuItem CreateMenuItem(SchedulerToolbarButton model, bool adornment)
    {
        var item = new MenuItem { Header = model.Label, IsEnabled = model.IsEnabled };
        if (model.MenuItems is { Count: > 0 } entries)
            foreach (var entry in entries)
                item.Items.Add(CreateMenuItem(entry, adornment));
        else
            item.Click += (_, _) => InvokeToolbarAction(model, adornment);
        return item;
    }

    internal void InvokeToolbarAction(SchedulerToolbarButton model, bool adornment)
    {
        if (model.Command is { } command && command.CanExecute(model.CommandParameter))
            command.Execute(model.CommandParameter);
        if (adornment)
            AdornmentToggleRequested?.Invoke(this, model);
        else
            ToolbarButtonClicked?.Invoke(this, model);
    }

    private void OnDatePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is Calendar { SelectedDate: { } date })
        {
            SetCurrentValue(SelectedDatesProperty, null);
            SetCurrentValue(SelectedDateProperty, new DateTimeOffset(date));
        }
    }

    private Control BuildSettings()
    {
        var settings = new StackPanel { Spacing = SpacingXS, MinWidth = Token<double>("DaisySchedulerSettingsMinimumWidth") };
        settings.Children.Add(SettingCheckBox("ShowTiles", ShowTiles));
        settings.Children.Add(SettingCheckBox("HideIdleResources", HideIdleResources));
        settings.Children.Add(SettingCheckBox("ShowWeekends", ShowWeekends));
        settings.Children.Add(SettingCheckBox("GroupByDay", GroupMode == SchedulerGroupMode.ByDay));
        AddPreferences(settings);
        var increments = new[]
        {
            string.Format(Culture, Text("Minutes"), 15),
            string.Format(Culture, Text("Minutes"), 30),
            string.Format(Culture, Text("Minutes"), 60)
        };
        var scale = new DaisySelect { Size = Size, Tag = "TimeScale", ItemsSource = increments, SelectedIndex = SnapMinutes <= 15 ? 0 : SnapMinutes <= 30 ? 1 : 2 };
        scale.SelectionChanged += OnTimeScaleChanged;
        settings.Children.Add(Label(Text("TimeScale")));
        settings.Children.Add(scale);
        foreach (var option in _tileOptions)
        {
            var check = new DaisyCheckBox { Content = option.Label, IsChecked = option.IsOn, Tag = option, Size = Size };
            check.IsCheckedChanged += OnTileOptionChanged;
            settings.Children.Add(check);
        }
        return new ScrollViewer
        {
            Content = settings, MaxHeight = Token<double>("DaisySchedulerSettingsMaximumHeight"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, AllowAutoHide = false
        };
    }

    private DaisyCheckBox SettingCheckBox(string key, bool value)
    {
        var check = new DaisyCheckBox { Content = Text(key), IsChecked = value, Tag = key, Size = Size };
        check.IsCheckedChanged += OnSettingChanged;
        return check;
    }

    private void OnSettingChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not DaisyCheckBox { Tag: string key } check)
            return;
        var value = check.IsChecked == true;
        switch (key)
        {
            case "ShowTiles": SetCurrentValue(ShowTilesProperty, value); break;
            case "HideIdleResources": SetCurrentValue(HideIdleResourcesProperty, value); break;
            case "ShowWeekends": SetCurrentValue(ShowWeekendsProperty, value); break;
            case "GroupByDay": SetCurrentValue(GroupModeProperty, value ? SchedulerGroupMode.ByDay : SchedulerGroupMode.ByResource); break;
        }
    }

    private void OnTimeScaleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DaisySelect { SelectedIndex: >= 0 } select)
            SetCurrentValue(TimeIncrementProperty, new TimeIncrementConfig
            {
                MinorTick = TimeSpan.FromMinutes(select.SelectedIndex switch { 0 => 15, 1 => 30, _ => 60 }),
                MajorTick = TimeIncrement?.MajorTick ?? TimeSpan.FromHours(1),
                PixelsPerMinute = TimeIncrement?.PixelsPerMinute ?? 0.8
            });
    }

    private void OnTileOptionChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is DaisyCheckBox { Tag: SchedulerTileOption option } check)
            TileOptionToggled?.Invoke(this, new SchedulerTileOption { Key = option.Key, Label = option.Label, IsOn = check.IsChecked == true });
    }
}
