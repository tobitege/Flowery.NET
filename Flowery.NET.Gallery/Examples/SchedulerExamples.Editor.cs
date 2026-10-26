using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.NET.Gallery.Localization;

namespace Flowery.NET.Gallery.Examples;

public partial class SchedulerExamples
{
    private sealed record TimeChoice(TimeSpan Time, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ColorChoice(Color? Color, string Label)
    {
        public override string ToString() => Label;
    }

    // The editor fields as values, to tell an edit from fields that were only shown again.
    private sealed record EditorFields(string Subject, DateTime? Start, TimeSpan? StartTime, DateTime? End, TimeSpan? EndTime,
        int SpanMode, string Description, bool? AllDay, int Resource, Color? Color);

    private EditorFields? _openedFields;
    // The last valid start and end of the editor, whose duration a moved start or end keeps.
    private (DateTime Start, DateTime End)? _editorSpan;
    // Set while the editor fills its fields, so their change events do not move the other end.
    private bool _fillingEditor;
    private IReadOnlyList<AppointmentModel> _conflicts = Array.Empty<AppointmentModel>();

    private void OpenEditor(AppointmentModel appointment, bool isNew)
    {
        _fillingEditor = true;
        _editing = appointment;
        _isNew = isNew;
        SubjectInput.Text = appointment.Subject;
        StartInput.SelectedDate = appointment.Start.Date;
        EndInput.SelectedDate = appointment.End.Date;
        StartTimeInput.ItemsSource = null;
        EndTimeInput.ItemsSource = null;
        ColorInput.ItemsSource = null;
        SpanModeInput.ItemsSource = null;
        DescriptionInput.Text = appointment.Description;
        AllDayInput.IsChecked = appointment.IsAllDay;
        ResourceInput.ItemsSource = new[] { Text("Unassigned") }.Concat(_resources.Select(static resource => resource.Name)).ToArray();
        ResourceInput.SelectedIndex = Array.FindIndex(_resources, resource => appointment.ResourceIds.Contains(resource.Id)) + 1;
        _editingResourceIndex = ResourceInput.SelectedIndex;
        EditorError.Text = string.Empty;
        _conflicts = Array.Empty<AppointmentModel>();
        RefreshEditorChoices();
        _editorSpan = null;
        _fillingEditor = false;
        KeepSpanValid(movedStart: true);
        EditorPanel.IsVisible = true;
        // The editor needs the height more than the description and the options.
        if (FoldsIntro)
            IsIntroVisible = false;
        _openedFields = ReadEditorFields();
    }

    private EditorFields ReadEditorFields() => new(SubjectInput.Text ?? string.Empty, StartInput.SelectedDate,
        (StartTimeInput.SelectedItem as TimeChoice)?.Time, EndInput.SelectedDate, (EndTimeInput.SelectedItem as TimeChoice)?.Time,
        SpanModeInput.SelectedIndex, DescriptionInput.Text ?? string.Empty, AllDayInput.IsChecked, ResourceInput.SelectedIndex,
        (ColorInput.SelectedItem as ColorChoice)?.Color);

    // Light dismiss: a press on the scheduler or Escape closes the editor while its fields are as they were opened. With
    // changes it stays open until Save or Cancel.
    private void DismissUnchangedEditor()
    {
        if (EditorPanel.IsVisible && ReadEditorFields() == _openedFields)
            EditorPanel.IsVisible = false;
    }

    private void OnSchedulerPressed(object? sender, PointerPressedEventArgs e) => DismissUnchangedEditor();

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DismissUnchangedEditor();
    }

    private void RefreshEditorChoices()
    {
        var filling = _fillingEditor;
        _fillingEditor = true;
        try
        {
            FillEditorChoices();
        }
        finally
        {
            _fillingEditor = filling;
        }
    }

    // Moving the start onto or past the end moves the end along, keeping the duration; moving the end onto or before the
    // start moves the start. An all-day appointment may end on its start day.
    private void OnStartEdited(object? sender, SelectionChangedEventArgs e) => KeepSpanValid(movedStart: true);
    private void OnEndEdited(object? sender, SelectionChangedEventArgs e) => KeepSpanValid(movedStart: false);

    private void OnStartDateChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CalendarDatePicker.SelectedDateProperty)
            KeepSpanValid(movedStart: true);
    }

    private void OnEndDateChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CalendarDatePicker.SelectedDateProperty)
            KeepSpanValid(movedStart: false);
    }

    private void KeepSpanValid(bool movedStart)
    {
        if (_fillingEditor || EditorTime(StartInput, StartTimeInput) is not { } start || EditorTime(EndInput, EndTimeInput) is not { } end)
            return;
        var allDay = AllDayInput.IsChecked == true;
        if ((allDay ? end < start : end <= start) && _editorSpan is { } span)
        {
            var duration = span.End - span.Start;
            // An all-day range may end on its start day; with times the range lasts at least one time slot.
            if (!allDay && duration <= TimeSpan.Zero)
                duration = DemoScheduler.TimeSlotInterval;
            _fillingEditor = true;
            try
            {
                if (movedStart)
                    ShowEditorTime(EndInput, EndTimeInput, end = start + duration);
                else
                    ShowEditorTime(StartInput, StartTimeInput, start = end - duration);
            }
            finally
            {
                _fillingEditor = false;
            }
        }
        if (allDay ? end >= start : end > start)
            _editorSpan = (start, end);
    }

    private DateTime? EditorTime(DaisyCalendarDatePicker date, DaisySelect time) => date.SelectedDate is { } day
        ? day.Date + (AllDayInput.IsChecked == true ? TimeSpan.Zero : (time.SelectedItem as TimeChoice)?.Time ?? TimeSpan.Zero)
        : null;

    private void ShowEditorTime(DaisyCalendarDatePicker date, DaisySelect time, DateTime value)
    {
        date.SelectedDate = value.Date;
        if (AllDayInput.IsChecked != true)
            FillTimeChoices(time, value.TimeOfDay);
    }

    private void FillEditorChoices()
    {
        if (_editing is null)
            return;
        var startTime = (StartTimeInput.SelectedItem as TimeChoice)?.Time ?? _editing.Start.TimeOfDay;
        var endTime = (EndTimeInput.SelectedItem as TimeChoice)?.Time ?? _editing.End.TimeOfDay;
        var color = ColorInput.SelectedItem is ColorChoice selected ? selected.Color : _editing.BackColor;
        var mode = SpanModeInput.SelectedIndex < 0 ? (int)_editing.SpanMode : SpanModeInput.SelectedIndex;
        ShowDatesLikeTheScheduler(StartInput);
        ShowDatesLikeTheScheduler(EndInput);
        FillTimeChoices(StartTimeInput, startTime);
        FillTimeChoices(EndTimeInput, endTime);
        SpanModeInput.ItemsSource = new[] { Text("Continuous"), Text("Daily") };
        SpanModeInput.SelectedIndex = mode;
        ColorPanel.IsVisible = DemoScheduler.AppointmentColors is { Count: > 0 };
        var colors = new List<ColorChoice> { new(null, Text("NoColor")) };
        foreach (var choice in DemoScheduler.AppointmentColors ?? Array.Empty<Color>())
            if (!colors.Any(item => item.Color == choice))
                colors.Add(new ColorChoice(choice, choice.ToString()));
        if (color is { } existing && !colors.Any(item => item.Color == existing))
            colors.Add(new ColorChoice(existing, existing.ToString()));
        ColorInput.ItemTemplate = new FuncDataTemplate<ColorChoice>(static (choice, _) =>
        {
            if (choice is null)
                return null;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new Border { Width = 18, Height = 18, Background = choice.Color is { } fill ? new SolidColorBrush(fill) : Brushes.Transparent });
            row.Children.Add(new TextBlock { Text = choice.Label });
            return row;
        });
        ColorInput.ItemsSource = colors;
        ColorInput.SelectedIndex = colors.FindIndex(item => item.Color == color);
        ShowConflicts();
    }

    // The pickers write the date in the scheduler's regional format, which may differ from the UI culture: its pattern is
    // used with the scheduler's own date separator, quoted, since the picker formats with the current culture.
    private void ShowDatesLikeTheScheduler(DaisyCalendarDatePicker picker)
    {
        var format = DemoScheduler.FormattingCulture.DateTimeFormat;
        picker.SelectedDateFormat = CalendarDatePickerFormat.Custom;
        picker.CustomDateFormatString = format.ShortDatePattern.Replace("/", "'" + format.DateSeparator + "'");
        picker.PlaceholderText = format.ShortDatePattern;
        picker.FirstDayOfWeek = DemoScheduler.FirstDayOfWeek;
    }

    private void FillTimeChoices(DaisySelect select, TimeSpan selected)
    {
        var times = DemoScheduler.GetTimeSlots().Append(selected).Distinct().Order().ToArray();
        select.ItemsSource = times.Select(time => new TimeChoice(time, DemoScheduler.FormatTime(DateTime.Today + time))).ToArray();
        select.SelectedIndex = Array.IndexOf(times, selected);
        select.IsEnabled = AllDayInput.IsChecked != true;
    }

    private void OnAllDayChanged(object? sender, RoutedEventArgs e)
    {
        StartTimeInput.IsEnabled = AllDayInput.IsChecked != true;
        EndTimeInput.IsEnabled = AllDayInput.IsChecked != true;
        // With times, an all-day range that ended on its start day lasts one time slot.
        KeepSpanValid(movedStart: true);
    }

    private void OnSave(object? sender, RoutedEventArgs e) => SaveEditor(false);
    private void OnSaveAnyway(object? sender, RoutedEventArgs e) => SaveEditor(true);

    private void SaveEditor(bool allowOverlap)
    {
        if (_editing is null || DemoScheduler.IsReadOnly)
            return;
        if (string.IsNullOrWhiteSpace(SubjectInput.Text) ||
            StartInput.SelectedDate is not { } start || EndInput.SelectedDate is not { } end ||
            StartTimeInput.SelectedItem is not TimeChoice startTime || EndTimeInput.SelectedItem is not TimeChoice endTime)
        {
            EditorError.Text = Text("InvalidInput");
            return;
        }
        var candidate = _editing.Clone();
        candidate.Subject = SubjectInput.Text.Trim();
        candidate.IsAllDay = AllDayInput.IsChecked == true;
        candidate.SpanMode = (AppointmentSpanMode)Math.Max(0, SpanModeInput.SelectedIndex);
        candidate.Start = candidate.IsAllDay ? start : start + startTime.Time;
        candidate.End = candidate.IsAllDay ? (end > start ? end : start.AddDays(1)) : end + endTime.Time;
        if (candidate.End <= candidate.Start || candidate.SpanMode == AppointmentSpanMode.Daily &&
            !candidate.IsAllDay && endTime.Time <= startTime.Time)
        {
            EditorError.Text = Text("InvalidInput");
            return;
        }
        candidate.Description = DescriptionInput.Text;
        if (ColorPanel.IsVisible && ColorInput.SelectedItem is ColorChoice color)
            candidate.BackColor = color.Color;
        if (_isNew || ResourceInput.SelectedIndex != _editingResourceIndex)
        {
            candidate.ResourceIds.Clear();
            if (ResourceInput.SelectedIndex > 0)
                candidate.ResourceIds.Add(_resources[ResourceInput.SelectedIndex - 1].Id);
        }
        _conflicts = DemoScheduler.GetConflictingAppointments(candidate);
        ShowConflicts();
        if (!allowOverlap && _conflicts.Count > 0)
            return;
        if (_isNew)
            _appointments.Add(candidate);
        else
            _appointments[_appointments.IndexOf(_editing)] = candidate;
        EditorPanel.IsVisible = false;
        Refresh();
    }

    private void ShowConflicts()
    {
        OverlapWarning.Text = _conflicts.Count == 0 ? string.Empty : string.Format(GalleryLocalization.CurrentCulture,
            Text("Overlap"), string.Join(", ", _conflicts.Select(static appointment => appointment.Subject)));
        SaveAnywayButton.IsVisible = _conflicts.Count > 0;
    }
}
