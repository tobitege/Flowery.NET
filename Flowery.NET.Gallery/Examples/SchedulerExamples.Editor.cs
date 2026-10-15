using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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

    private CultureInfo _editorCulture = CultureInfo.InvariantCulture;
    private IReadOnlyList<AppointmentModel> _conflicts = Array.Empty<AppointmentModel>();

    private void OpenEditor(AppointmentModel appointment, bool isNew)
    {
        _editing = appointment;
        _isNew = isNew;
        SubjectInput.Text = appointment.Subject;
        _editorCulture = DemoScheduler.FormattingCulture;
        StartInput.Text = DemoScheduler.FormatDate(appointment.Start);
        EndInput.Text = DemoScheduler.FormatDate(appointment.End);
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
        EditorPanel.IsVisible = true;
    }

    private void RefreshEditorChoices()
    {
        if (_editing is null)
            return;
        var startTime = (StartTimeInput.SelectedItem as TimeChoice)?.Time ?? _editing.Start.TimeOfDay;
        var endTime = (EndTimeInput.SelectedItem as TimeChoice)?.Time ?? _editing.End.TimeOfDay;
        var color = ColorInput.SelectedItem is ColorChoice selected ? selected.Color : _editing.BackColor;
        var mode = SpanModeInput.SelectedIndex < 0 ? (int)_editing.SpanMode : SpanModeInput.SelectedIndex;
        if (DateTime.TryParseExact(StartInput.Text, "d", _editorCulture, DateTimeStyles.None, out var startDate))
            StartInput.Text = DemoScheduler.FormatDate(startDate);
        if (DateTime.TryParseExact(EndInput.Text, "d", _editorCulture, DateTimeStyles.None, out var endDate))
            EndInput.Text = DemoScheduler.FormatDate(endDate);
        _editorCulture = DemoScheduler.FormattingCulture;
        StartInput.PlaceholderText = _editorCulture.DateTimeFormat.ShortDatePattern;
        EndInput.PlaceholderText = _editorCulture.DateTimeFormat.ShortDatePattern;
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
    }

    private void OnSave(object? sender, RoutedEventArgs e) => SaveEditor(false);
    private void OnSaveAnyway(object? sender, RoutedEventArgs e) => SaveEditor(true);

    private void SaveEditor(bool allowOverlap)
    {
        if (_editing is null || DemoScheduler.IsReadOnly)
            return;
        if (string.IsNullOrWhiteSpace(SubjectInput.Text) ||
            !DateTime.TryParseExact(StartInput.Text, "d", _editorCulture, DateTimeStyles.None, out var start) ||
            !DateTime.TryParseExact(EndInput.Text, "d", _editorCulture, DateTimeStyles.None, out var end) ||
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
