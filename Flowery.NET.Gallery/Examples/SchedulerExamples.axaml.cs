using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.NET.Gallery.Localization;

namespace Flowery.NET.Gallery.Examples;

public partial class SchedulerExamples : UserControl
{
    private readonly List<AppointmentModel> _appointments = new();
    private readonly ResourceModel[] _resources =
    [
        new() { Id = 1, Name = "Alex", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Development" } },
        new() { Id = 2, Name = "Bailey", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Development" } },
        new() { Id = 3, Name = "Casey", Kind = ResourceKind.Person, GroupValues = { ["Department"] = "Support" } },
        new() { Id = "room-a", Name = "Room A", Kind = ResourceKind.Room },
        new() { Id = "room-b", Name = "Room B", Kind = ResourceKind.Room }
    ];
    private AppointmentModel? _editing;
    private bool _isNew;
    private int _editingResourceIndex;

    public SchedulerExamples()
    {
        InitializeComponent();
        DemoScheduler.ResourceGroups = new[]
        {
            new ResourceGroupModel { Id = "all", Name = Text("AllPeople"), Kind = ResourceKind.Person },
            new ResourceGroupModel { Id = "development", Name = Text("Development"), Kind = ResourceKind.Person, GroupType = "Department", GroupValue = "Development" },
            new ResourceGroupModel { Id = "support", Name = Text("Support"), Kind = ResourceKind.Person, GroupType = "Department", GroupValue = "Support" },
            new ResourceGroupModel { Id = "rooms", Name = Text("Rooms"), Kind = ResourceKind.Room }
        };
        foreach (var option in new[] { ResourcesOption, TilesOption, IconsOption, AdornmentsOption, DetailsOption, ReadOnlyOption })
            option.IsCheckedChanged += OnOptionsChanged;
        DemoScheduler.AppointmentCreateRequested += OnCreate;
        DemoScheduler.AppointmentEditRequested += OnEdit;
        DemoScheduler.AppointmentMoveRequested += OnMove;
        DemoScheduler.AppointmentResizeRequested += OnResize;
        DemoScheduler.AppointmentsMoveRequested += OnMoveMany;
        DemoScheduler.AppointmentsResizeRequested += OnResizeMany;
        DemoScheduler.AppointmentsDeleteRequested += OnDelete;
        DemoScheduler.AppointmentContextRequested += OnContext;
        DemoScheduler.ToolbarButtonClicked += OnToolbarAction;
        DemoScheduler.SelectionChanged += OnSelectionChanged;
        DemoScheduler.ExternalAppointmentDropped += OnExternalDrop;
        DemoScheduler.PropertyChanged += OnSchedulerPropertyChanged;
        RefreshFormatOptions();
        SeedAppointments();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DaisyThemeManager.ThemeChanged += OnThemeChanged;
        GalleryLocalization.CultureChanged += OnCultureChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        GalleryLocalization.CultureChanged -= OnCultureChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, string themeName) => Refresh();
    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        RefreshFormatOptions();
        foreach (var group in DemoScheduler.ResourceGroups ?? Array.Empty<ResourceGroupModel>())
            group.Name = Text(group.Id switch { "all" => "AllPeople", "development" => "Development", "support" => "Support", _ => "Rooms" });
        DemoScheduler.ResourceGroups = DemoScheduler.ResourceGroups?.ToArray();
        Refresh();
        OnSelectionChanged(this, DemoScheduler.SelectedAppointments);
        if (EditorPanel.IsVisible)
        {
            var selectedResource = ResourceInput.SelectedIndex;
            ResourceInput.ItemsSource = new[] { Text("Unassigned") }.Concat(_resources.Select(static resource => resource.Name)).ToArray();
            ResourceInput.SelectedIndex = selectedResource;
            RefreshEditorChoices();
            if (!string.IsNullOrEmpty(EditorError.Text))
                EditorError.Text = Text("InvalidInput");
        }
    }
    private void OnOptionsChanged(object? sender, RoutedEventArgs e) => Refresh();
    private void OnSchedulerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DaisyScheduler.CurrentViewProperty)
            Refresh();
        if (e.Property == DaisyScheduler.DateTimeFormatProperty || e.Property == DaisyScheduler.ClockFormatProperty ||
            e.Property == DaisyScheduler.ResourceKindFilterProperty)
            RefreshFormatOptions();
        if (EditorPanel.IsVisible && (e.Property == DaisyScheduler.TimeSlotStartProperty ||
            e.Property == DaisyScheduler.TimeSlotEndProperty || e.Property == DaisyScheduler.TimeSlotIntervalProperty ||
            e.Property == DaisyScheduler.ClockFormatProperty || e.Property == DaisyScheduler.DateTimeFormatProperty ||
            e.Property == DaisyScheduler.AppointmentColorsProperty))
            RefreshEditorChoices();
    }
    private static string Text(string key) => GalleryLocalization.GetString("Scheduler_" + key);

    private Color? ThemeColor(string key) => this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is Color color ? color : null;

    private void SeedAppointments()
    {
        _appointments.Clear();
        var monday = DateTime.Today.AddDays(-((7 + (int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        var subjects = new[] { "Stand-up", "Site survey", "Annual service", "Installation", "Warranty visit", "Parts pickup", "Follow-up call", "Team meeting", "Office hours" };
        for (var index = 0; index < subjects.Length; index++)
        {
            var start = monday.AddDays(index % 5).AddHours(8.5 + index % 3 * 2);
            _appointments.Add(new AppointmentModel
            {
                Id = Guid.NewGuid(), Subject = subjects[index], Start = start, End = start.AddHours(index % 2 == 0 ? 1 : 2),
                ResourceIds = { index % 3 + 1 }, Description = Text("SampleDescription"),
                IsComplete = index == 2, IsTentative = index == 5, RecurrenceRule = index == 0 ? "FREQ=WEEKLY" : null
            });
        }
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Trade show", Start = monday.AddDays(3), End = monday.AddDays(5), IsAllDay = true, ResourceIds = { 1, 2 } });
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Reserved maintenance", Start = monday.AddDays(2).AddHours(14), End = monday.AddDays(2).AddHours(16), IsLocked = true, ResourceIds = { 3 } });
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Training", Start = monday.AddHours(9), End = monday.AddDays(4).AddHours(12), SpanMode = AppointmentSpanMode.Daily, ResourceIds = { 1, "room-a" } });
        _appointments.Add(new AppointmentModel { Id = Guid.NewGuid(), Subject = "Exhibition setup", Start = monday.AddDays(1).AddHours(7), End = monday.AddDays(3).AddHours(17), ResourceIds = { "room-b" } });
        Refresh();
    }

    private void Refresh()
    {
        var palette = new[] { ThemeColor("DaisyPrimaryColor"), ThemeColor("DaisySecondaryColor"), ThemeColor("DaisyAccentColor") };
        for (var index = 0; index < _resources.Length; index++)
            _resources[index].Color = palette[index % palette.Length];
        DemoScheduler.AppointmentColors = palette.OfType<Color>().ToArray();
        foreach (var appointment in _appointments)
        {
            var resource = _resources.FirstOrDefault(item => appointment.ResourceIds.Contains(item.Id));
            appointment.BarColor = resource?.Color;
            appointment.Tiles.Clear();
            appointment.Indicators.Clear();
            appointment.Adornments.Clear();
            appointment.Details.Clear();
            if (TilesOption.IsChecked == true)
                appointment.Tiles.Add(new AppointmentDecoration { Text = resource?.Name ?? Text("Unassigned"), Position = DecorationPosition.Bottom });
            if (IconsOption.IsChecked == true && appointment.IsRecurring)
                appointment.Indicators.Add(new AppointmentIndicator { Glyph = "↻", Tooltip = Text("Recurring") });
            if (AdornmentsOption.IsChecked == true && appointment.IsTentative)
                appointment.Adornments.Add(new AppointmentDecoration { Text = "?", Position = DecorationPosition.FloatRight, BackColor = ThemeColor("DaisyWarningColor"), ForeColor = ThemeColor("DaisyWarningContentColor") });
            if (DetailsOption.IsChecked == true)
            {
                appointment.Details.Add(new AppointmentDetail(Text("Resource"), resource?.Name ?? Text("Unassigned")));
                appointment.Details.Add(new AppointmentDetail(Text("Status"), Text(appointment.IsComplete ? "StatusComplete" : appointment.IsLocked ? "StatusLocked" : "StatusScheduled")));
            }
        }
        DemoScheduler.IsReadOnly = ReadOnlyOption.IsChecked == true;
        DemoScheduler.ShowTiles = TilesOption.IsChecked == true;
        DemoScheduler.SchedulerResources = ResourcesOption.IsChecked == true ? _resources.ToArray() : Array.Empty<ResourceModel>();
        // Horizontal views need resource lanes even if resource grouping is off in the other views.
        if (DemoScheduler.CurrentView is SchedulerView.Planner or SchedulerView.Timeline or SchedulerView.Calendar)
            DemoScheduler.SchedulerResources = _resources.ToArray();
        DemoScheduler.Appointments = _appointments.ToArray();
        DemoScheduler.ToolbarButtons.Clear();
        DemoScheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "new", Label = Text("New"), IsEnabled = !DemoScheduler.IsReadOnly });
        DemoScheduler.ToolbarButtons.Add(new SchedulerToolbarButton { Id = "delete", Label = Text("Delete"), IsEnabled = !DemoScheduler.IsReadOnly });
        if (DemoScheduler.IsReadOnly)
            EditorPanel.IsVisible = false;
    }

    private void OnToolbarAction(object? sender, SchedulerToolbarButton action)
    {
        if (action.Id == "delete")
            DemoScheduler.DeleteSelected();
        else if (action.Id == "new")
            OpenEditor(new AppointmentModel { Id = Guid.NewGuid(), Start = DemoScheduler.SelectedDate.Date + DemoScheduler.TimeSlotStart,
                End = DemoScheduler.SelectedDate.Date + DemoScheduler.TimeSlotStart + DemoScheduler.TimeSlotInterval }, true);
    }

    private void OnSelectionChanged(object? sender, IReadOnlyList<AppointmentModel> selected)
    {
        StatusText.Text = selected.Count == 0 ? Text("Help") : string.Join(", ", selected.Select(static appointment => appointment.Subject));
    }

    private void OnCreate(object? sender, AppointmentCreateArgs e)
    {
        var appointment = new AppointmentModel { Id = Guid.NewGuid(), Start = e.Start, End = e.End, IsAllDay = e.IsAllDay };
        if (e.ResourceId is { } resource)
            appointment.ResourceIds.Add(resource);
        OpenEditor(appointment, true);
    }

    private void OnEdit(object? sender, AppointmentEditRequestedArgs e)
    {
        if (!DemoScheduler.IsReadOnly)
        {
            OpenEditor(e.Appointment, false);
            e.Handled = true;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => EditorPanel.IsVisible = false;
    private void OnReset(object? sender, RoutedEventArgs e) { EditorPanel.IsVisible = false; SeedAppointments(); }

    private void ApplyMove(AppointmentModel appointment, DateTime start, DateTime end, object? from, object? to, bool copy)
    {
        if (copy)
        {
            appointment = appointment.Clone();
            appointment.Id = Guid.NewGuid();
            _appointments.Add(appointment);
        }
        appointment.Start = start;
        appointment.End = end;
        if (!Equals(from, to))
        {
            if (from is not null)
                appointment.ResourceIds.Remove(from);
            if (to is not null && !appointment.ResourceIds.Contains(to))
                appointment.ResourceIds.Add(to);
        }
    }

    private void OnMove(object? sender, AppointmentMoveArgs e) { ApplyMove(e.Appointment, e.NewStart, e.NewEnd, e.FromResourceId, e.ToResourceId, e.IsCopy); Refresh(); }
    private void OnResize(object? sender, AppointmentResizeArgs e) { e.Appointment.Start = e.NewStart; e.Appointment.End = e.NewEnd; Refresh(); }
    private void OnMoveMany(object? sender, AppointmentsMoveArgs e)
    {
        foreach (var item in e.Items)
            ApplyMove(item.Appointment, item.NewStart, item.NewEnd, item.FromResourceId, item.ToResourceId, e.IsCopy);
        Refresh();
    }
    private void OnResizeMany(object? sender, AppointmentsResizeArgs e)
    {
        foreach (var item in e.Items) { item.Appointment.Start = item.NewStart; item.Appointment.End = item.NewEnd; }
        Refresh();
    }
    private void OnDelete(object? sender, AppointmentsDeleteArgs e)
    {
        foreach (var appointment in e.Appointments)
            _appointments.Remove(appointment);
        EditorPanel.IsVisible = false;
        Refresh();
    }
    private void OnExternalDrop(object? sender, ExternalDropArgs e)
    {
        var appointment = new AppointmentModel { Id = Guid.NewGuid(), Subject = e.Item.Subject, Start = e.Start, End = e.End, Tag = e.Item.Payload };
        if (e.ResourceId is { } resource)
            appointment.ResourceIds.Add(resource);
        _appointments.Add(appointment);
        Refresh();
    }

    private void OnContext(object? sender, AppointmentContextRequestedArgs e)
    {
        var menu = new ContextMenu();
        foreach (var action in new[] { "Edit", "Duplicate", "Complete", "Delete" })
        {
            var item = new MenuItem { Header = Text(action), IsEnabled = !DemoScheduler.IsReadOnly && !e.Appointment.IsLocked, Tag = (action, e.Appointment) };
            item.Click += OnContextAction;
            menu.Items.Add(item);
        }
        menu.Placement = PlacementMode.Pointer;
        menu.Open(DemoScheduler);
    }

    private void OnContextAction(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ValueTuple<string, AppointmentModel> action })
            return;
        var (id, appointment) = action;
        switch (id)
        {
            case "Edit": OpenEditor(appointment, false); return;
            case "Duplicate": ApplyMove(appointment, appointment.Start.AddHours(1), appointment.End.AddHours(1), null, null, true); break;
            case "Complete": appointment.IsComplete = !appointment.IsComplete; break;
            case "Delete": _appointments.Remove(appointment); break;
        }
        Refresh();
    }
}
