// Avalonia adaptation of Xtreme.Components Scheduler (MIT).
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls.Scheduler.Contracts;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;
using Flowery.Helpers;
using Flowery.Localization;

namespace Flowery.Controls;

/// <summary>
/// A Flowery calendar and resource scheduler adapted from Xtreme.Components.
/// Data is read-only input; gestures raise requests that the host may accept.
/// Reassign the appointment list after editing a model to refresh the view.
/// </summary>
[TemplatePart("PART_ViewHost", typeof(Border))]
[TemplatePart("PART_Toolbar", typeof(DaisySchedulerToolbar))]
public partial class DaisyScheduler : TemplatedControl
{
    protected override Type StyleKeyOverride => typeof(DaisyScheduler);

    public static readonly StyledProperty<IReadOnlyList<AppointmentModel>?> AppointmentsProperty =
        AvaloniaProperty.Register<DaisyScheduler, IReadOnlyList<AppointmentModel>?>(nameof(Appointments), null);

    public IReadOnlyList<AppointmentModel>? Appointments
    {
        get => GetValue(AppointmentsProperty);
        set => SetValue(AppointmentsProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<ResourceModel>?> SchedulerResourcesProperty =
        AvaloniaProperty.Register<DaisyScheduler, IReadOnlyList<ResourceModel>?>(nameof(SchedulerResources), null);

    public IReadOnlyList<ResourceModel>? SchedulerResources
    {
        get => GetValue(SchedulerResourcesProperty);
        set => SetValue(SchedulerResourcesProperty, value);
    }

    public static readonly StyledProperty<SchedulerView> CurrentViewProperty =
        AvaloniaProperty.Register<DaisyScheduler, SchedulerView>(nameof(CurrentView), SchedulerView.Week);

    public SchedulerView CurrentView
    {
        get => GetValue(CurrentViewProperty);
        set => SetValue(CurrentViewProperty, value);
    }

    public static readonly StyledProperty<DateTimeOffset> SelectedDateProperty =
        AvaloniaProperty.Register<DaisyScheduler, DateTimeOffset>(nameof(SelectedDate), default, defaultBindingMode: BindingMode.TwoWay);

    public DateTimeOffset SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<DateTime>?> SelectedDatesProperty =
        AvaloniaProperty.Register<DaisyScheduler, IReadOnlyList<DateTime>?>(nameof(SelectedDates), null);

    public IReadOnlyList<DateTime>? SelectedDates
    {
        get => GetValue(SelectedDatesProperty);
        set => SetValue(SelectedDatesProperty, value);
    }

    public static readonly StyledProperty<int> VisibleDaysProperty =
        AvaloniaProperty.Register<DaisyScheduler, int>(nameof(VisibleDays), 1, validate: static value => value is > 0 and <= 366);

    public int VisibleDays
    {
        get => GetValue(VisibleDaysProperty);
        set => SetValue(VisibleDaysProperty, value);
    }

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<DaisyScheduler, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Monday,
            validate: static value => Enum.IsDefined(value));

    public DayOfWeek FirstDayOfWeek
    {
        get => GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    public static readonly StyledProperty<SchedulerGroupMode> GroupModeProperty =
        AvaloniaProperty.Register<DaisyScheduler, SchedulerGroupMode>(nameof(GroupMode), SchedulerGroupMode.ByResource);

    public SchedulerGroupMode GroupMode
    {
        get => GetValue(GroupModeProperty);
        set => SetValue(GroupModeProperty, value);
    }

    public static readonly StyledProperty<bool> HideIdleResourcesProperty =
        AvaloniaProperty.Register<DaisyScheduler, bool>(nameof(HideIdleResources), false);

    public bool HideIdleResources
    {
        get => GetValue(HideIdleResourcesProperty);
        set => SetValue(HideIdleResourcesProperty, value);
    }

    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<DaisyScheduler, bool>(nameof(ShowToolbar), true);

    /// <summary>Whether the scheduler shows its own <see cref="DaisySchedulerToolbar"/> (default true). Set it to false to
    /// place a toolbar elsewhere, such as in a title bar, with its <see cref="DaisySchedulerToolbar.Scheduler"/> set.</summary>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    public static readonly StyledProperty<bool> ShowWeekendsProperty =
        AvaloniaProperty.Register<DaisyScheduler, bool>(nameof(ShowWeekends), true);

    /// <summary>Whether Month, Week and Planner show Saturday and Sunday (default true). Without them, Month and Week
    /// show the other five days of each week, and the Planner shows Monday to Friday.</summary>
    public bool ShowWeekends
    {
        get => GetValue(ShowWeekendsProperty);
        set => SetValue(ShowWeekendsProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> WorkDayStartProperty =
        AvaloniaProperty.Register<DaisyScheduler, TimeSpan>(nameof(WorkDayStart), TimeSpan.FromHours(8), validate: static value => value >= TimeSpan.Zero && value < TimeSpan.FromDays(1));

    public TimeSpan WorkDayStart
    {
        get => GetValue(WorkDayStartProperty);
        set => SetValue(WorkDayStartProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> WorkDayEndProperty =
        AvaloniaProperty.Register<DaisyScheduler, TimeSpan>(nameof(WorkDayEnd), TimeSpan.FromHours(17), validate: static value => value > TimeSpan.Zero && value <= TimeSpan.FromDays(1));

    public TimeSpan WorkDayEnd
    {
        get => GetValue(WorkDayEndProperty);
        set => SetValue(WorkDayEndProperty, value);
    }

    public static readonly StyledProperty<TimeIncrementConfig?> TimeIncrementProperty =
        AvaloniaProperty.Register<DaisyScheduler, TimeIncrementConfig?>(nameof(TimeIncrement), null);

    public TimeIncrementConfig? TimeIncrement
    {
        get => GetValue(TimeIncrementProperty);
        set => SetValue(TimeIncrementProperty, value);
    }

    public static readonly StyledProperty<double?> ColumnMinWidthProperty =
        AvaloniaProperty.Register<DaisyScheduler, double?>(nameof(ColumnMinWidth), null,
            validate: static value => value is null || double.IsFinite(value.Value) && value.Value > 0);

    public double? ColumnMinWidth
    {
        get => GetValue(ColumnMinWidthProperty);
        set => SetValue(ColumnMinWidthProperty, value);
    }

    public static readonly StyledProperty<double> ColumnZoomProperty =
        AvaloniaProperty.Register<DaisyScheduler, double>(nameof(ColumnZoom), MinimumColumnZoom,
            validate: static value => double.IsFinite(value),
            coerce: static (_, value) => Math.Clamp(value, MinimumColumnZoom, MaximumColumnZoom));

    /// <summary>Widens the day columns of Month, Day and Week and the hours of Planner and Timeline by this factor of the
    /// width that fits them into the view, at least their minimum width; from 1 (default) to 4. Users change it with two
    /// fingers, Ctrl and the mouse wheel, a double tap on a column heading or the settings menu.</summary>
    public double ColumnZoom
    {
        get => GetValue(ColumnZoomProperty);
        set => SetValue(ColumnZoomProperty, value);
    }

    public static readonly StyledProperty<string?> StateKeyProperty =
        AvaloniaProperty.Register<DaisyScheduler, string?>(nameof(StateKey));

    /// <summary>Remembers the settings of the user under this key in <see cref="Flowery.Services.StateStorageProvider"/>:
    /// the scheduler loads them when it is attached and saves every change. For now this is the column zoom. Null
    /// (default) keeps nothing.</summary>
    public string? StateKey
    {
        get => GetValue(StateKeyProperty);
        set => SetValue(StateKeyProperty, value);
    }

    public static readonly StyledProperty<double?> AppointmentCornerRadiusProperty =
        AvaloniaProperty.Register<DaisyScheduler, double?>(nameof(AppointmentCornerRadius), null,
            validate: static value => value is null || double.IsFinite(value.Value) && value.Value >= 0);

    public double? AppointmentCornerRadius
    {
        get => GetValue(AppointmentCornerRadiusProperty);
        set => SetValue(AppointmentCornerRadiusProperty, value);
    }

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<DaisyScheduler, bool>(nameof(IsReadOnly), false);

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly StyledProperty<bool> SingleClickCreatesProperty =
        AvaloniaProperty.Register<DaisyScheduler, bool>(nameof(SingleClickCreates), false);

    public bool SingleClickCreates
    {
        get => GetValue(SingleClickCreatesProperty);
        set => SetValue(SingleClickCreatesProperty, value);
    }

    public static readonly StyledProperty<bool> ShowTilesProperty =
        AvaloniaProperty.Register<DaisyScheduler, bool>(nameof(ShowTiles), true);

    public bool ShowTiles
    {
        get => GetValue(ShowTilesProperty);
        set => SetValue(ShowTilesProperty, value);
    }

    public static readonly StyledProperty<DaisySize> SizeProperty =
        AvaloniaProperty.Register<DaisyScheduler, DaisySize>(nameof(Size), DaisySize.Medium);

    public DaisySize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public static readonly StyledProperty<AppointmentModel?> SelectedAppointmentProperty =
        AvaloniaProperty.Register<DaisyScheduler, AppointmentModel?>(nameof(SelectedAppointment), null, defaultBindingMode: BindingMode.TwoWay);

    public AppointmentModel? SelectedAppointment
    {
        get => GetValue(SelectedAppointmentProperty);
        set => SetValue(SelectedAppointmentProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> AppointmentTemplateProperty =
        AvaloniaProperty.Register<DaisyScheduler, IDataTemplate?>(nameof(AppointmentTemplate), null);

    public IDataTemplate? AppointmentTemplate
    {
        get => GetValue(AppointmentTemplateProperty);
        set => SetValue(AppointmentTemplateProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> AppointmentChipTemplateProperty =
        AvaloniaProperty.Register<DaisyScheduler, IDataTemplate?>(nameof(AppointmentChipTemplate), null);

    public IDataTemplate? AppointmentChipTemplate
    {
        get => GetValue(AppointmentChipTemplateProperty);
        set => SetValue(AppointmentChipTemplateProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> AppointmentBarTemplateProperty =
        AvaloniaProperty.Register<DaisyScheduler, IDataTemplate?>(nameof(AppointmentBarTemplate), null);

    public IDataTemplate? AppointmentBarTemplate
    {
        get => GetValue(AppointmentBarTemplateProperty);
        set => SetValue(AppointmentBarTemplateProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> AppointmentTooltipTemplateProperty =
        AvaloniaProperty.Register<DaisyScheduler, IDataTemplate?>(nameof(AppointmentTooltipTemplate), null);

    public IDataTemplate? AppointmentTooltipTemplate
    {
        get => GetValue(AppointmentTooltipTemplateProperty);
        set => SetValue(AppointmentTooltipTemplateProperty, value);
    }

    public static readonly StyledProperty<Func<AppointmentContext, Control?>?> AppointmentRendererProperty =
        AvaloniaProperty.Register<DaisyScheduler, Func<AppointmentContext, Control?>?>(nameof(AppointmentRenderer), null);

    public Func<AppointmentContext, Control?>? AppointmentRenderer
    {
        get => GetValue(AppointmentRendererProperty);
        set => SetValue(AppointmentRendererProperty, value);
    }

    public static readonly StyledProperty<Func<AppointmentContext, Control?>?> AppointmentTooltipRendererProperty =
        AvaloniaProperty.Register<DaisyScheduler, Func<AppointmentContext, Control?>?>(nameof(AppointmentTooltipRenderer), null);

    public Func<AppointmentContext, Control?>? AppointmentTooltipRenderer
    {
        get => GetValue(AppointmentTooltipRendererProperty);
        set => SetValue(AppointmentTooltipRendererProperty, value);
    }

    public static readonly DirectProperty<DaisyScheduler, IReadOnlyList<AppointmentModel>> SelectedAppointmentsProperty =
        AvaloniaProperty.RegisterDirect<DaisyScheduler, IReadOnlyList<AppointmentModel>>(
            nameof(SelectedAppointments), static scheduler => scheduler.SelectedAppointments);

    private IReadOnlyList<AppointmentModel> _selectedAppointments = Array.Empty<AppointmentModel>();
    public IReadOnlyList<AppointmentModel> SelectedAppointments => _selectedAppointments;
    public ObservableCollection<SchedulerToolbarButton> ToolbarButtons { get; } = new();
    public ObservableCollection<SchedulerToolbarButton> ToolbarAdornments { get; } = new();

    public event EventHandler<AppointmentCreateArgs>? AppointmentCreateRequested;
    public event EventHandler<EmptySlotSelectedArgs>? EmptySlotSelected;
    public event EventHandler<AppointmentMoveArgs>? AppointmentMoveRequested;
    public event EventHandler<AppointmentResizeArgs>? AppointmentResizeRequested;
    public event EventHandler<AppointmentsMoveArgs>? AppointmentsMoveRequested;
    public event EventHandler<AppointmentsResizeArgs>? AppointmentsResizeRequested;
    public event EventHandler<AppointmentsDeleteArgs>? AppointmentsDeleteRequested;
    public event EventHandler<ExternalDropArgs>? ExternalAppointmentDropped;
    public event EventHandler<AppointmentEditRequestedArgs>? AppointmentEditRequested;
    public event EventHandler<AppointmentContextRequestedArgs>? AppointmentContextRequested;
    public event EventHandler<IReadOnlyList<AppointmentModel>>? SelectionChanged;
    public event EventHandler<SchedulerToolbarButton>? ToolbarButtonClicked;
    public event EventHandler<SchedulerToolbarButton>? AdornmentToggleRequested;
    public event EventHandler<SchedulerTileOption>? TileOptionToggled;

    private readonly DaisyControlLifecycle _lifecycle;
    private Border? _viewHost;
    private readonly List<AppointmentModel> _selection = new();
    private readonly Dictionary<AppointmentModel, object> _selectionResourceIds = new();
    private readonly List<(Border Frame, AppointmentModel Appointment)> _frames = new();
    private bool _selectionSyncing;
    private bool _renderPending;
    private bool _attached;
    private ScrollViewer? _bodyScroll;
    private Vector? _savedOffset;
    private SchedulerView? _renderedView;
    private IReadOnlyList<SchedulerTileOption> _tileOptions = Array.Empty<SchedulerTileOption>();

    public DaisyScheduler()
    {
        SetCurrentValue(SelectedDateProperty, new DateTimeOffset(DateTime.Today));
        SetCurrentValue(TimeIncrementProperty, TimeIncrementConfig.Default);
        _lifecycle = new DaisyControlLifecycle(this, RequestRender);
        ToolbarButtons.CollectionChanged += (_, _) => RequestRender();
        ToolbarAdornments.CollectionChanged += (_, _) => RequestRender();
        Focusable = true;
        AddHandler(PointerReleasedEvent, OnPendingTapReleased, handledEventsToo: true);
        AddHandler(ScrollGestureInertiaStartingEvent, OnScrollFlingStarting, handledEventsToo: true);
        AddHandler(ScrollGestureEndedEvent, OnScrollGestureEnded, handledEventsToo: true);
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, OnPinchEnded, handledEventsToo: true);
        // Tunnel: the scroll viewer of the view would scroll on the wheel first.
        AddHandler(PointerWheelChangedEvent, OnZoomWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        FloweryLocalization.CultureChanged += OnCultureChanged;
        LoadState();
        RequestRender();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        FloweryLocalization.CultureChanged -= OnCultureChanged;
        CancelGesture();
        CancelHold();
        _pendingTap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnCultureChanged(object? sender, CultureInfo culture) => RequestRender();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CancelGesture();
        base.OnApplyTemplate(e);
        _viewHost = e.NameScope.Find<Border>("PART_ViewHost");
        if (e.NameScope.Find<DaisySchedulerToolbar>("PART_Toolbar") is { } toolbar)
            toolbar.Scheduler = this;
        RenderActiveView();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedAppointmentProperty)
        {
            if (_selectionSyncing)
                return;
            _selection.Clear();
            if (SelectedAppointment is { IsLocked: false } selected)
                _selection.Add(selected);
            CommitSelection();
            return;
        }
        if (change.Property == SelectedAppointmentsProperty)
            return;
        // A new zoom only resizes the columns of the shown view; the view is not built again.
        if (change.Property == ColumnZoomProperty)
        {
            ZoomColumns();
            SaveState();
            return;
        }
        if (change.Property == StateKeyProperty)
        {
            LoadState();
            return;
        }
        if (change.Property == CurrentViewProperty)
        {
            CancelGesture();
            if (CurrentView is SchedulerView.Week or SchedulerView.Month or SchedulerView.Planner or SchedulerView.Timeline)
                SetCurrentValue(SelectedDatesProperty, null);
        }
        if (change.Property.OwnerType == typeof(DaisyScheduler) ||
            change.Property == FontSizeProperty ||
            change.Property == ThemeVariantScope.ActualThemeVariantProperty)
            RequestRender();
    }

    private void RequestRender()
    {
        if (!_attached || _renderPending)
            return;
        _renderPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _renderPending = false;
            if (_attached)
                RenderActiveView();
        }, DispatcherPriority.Loaded);
    }

    private void RenderActiveView()
    {
        if (_viewHost is null)
            return;
        CancelGesture();
        if (_bodyScroll is not null && _renderedView == CurrentView)
            _savedOffset = _bodyScroll.Offset;
        else
            _savedOffset = null;
        _bodyScroll = null;
        _headerScroll = null;
        _gutterScroll = null;
        _monthGrid = null;
        _timeColumns.Clear();
        _frames.Clear();
        _occurrenceSources.Clear();
        _lanes.Clear();
        _minuteView = null;
        _visibleItems = GetVisibleOccurrences().ToArray();
        var appointments = Appointments ?? Array.Empty<AppointmentModel>();
        // A selected appointment that VisibleResourceIds hides leaves the selection, so Delete never removes what is not shown.
        var retained = _selection.Select(old => appointments.FirstOrDefault(a => Same(a, old)))
            .OfType<AppointmentModel>().Where(a => !a.IsLocked && ShowsResources(a.ResourceIds)).ToArray();
        if (!_selection.SequenceEqual(retained))
        {
            _selection.Clear();
            _selection.AddRange(retained);
            CommitSelection();
        }
        UpdateToolbar();
        _viewHost.Child = CurrentView switch
        {
            SchedulerView.Month => BuildMonth(),
            SchedulerView.Agenda => BuildAgenda(),
            SchedulerView.Calendar => BuildCalendar(),
            SchedulerView.Planner or SchedulerView.Timeline => BuildPlanner(),
            _ => BuildTimeGrid()
        };
        _renderedView = CurrentView;
        ApplySelection();
    }

    internal double Scale => Token<double>($"DaisySize{Size}FontSize") / Token<double>("DaisySizeMediumFontSize");
    private double ChipHeight => RoundToPixel(Token<double>($"DaisyBadge{Size}Height"));
    private CultureInfo Culture => FormattingCulture;
    private double SnapMinutes => Math.Clamp(TimeIncrement?.MinorTick.TotalMinutes ?? 30, 1, 1440);
    private double PixelsPerMinute => Math.Clamp(TimeIncrement?.PixelsPerMinute ?? 0.8, 0.1, 5) * Scale;
    private readonly Dictionary<AppointmentModel, AppointmentModel> _occurrenceSources = new();
    private IReadOnlyList<AppointmentModel> _visibleItems = Array.Empty<AppointmentModel>();
    private IEnumerable<AppointmentModel> Items => _visibleItems;
    private AppointmentModel OriginalAppointment(AppointmentModel appointment) =>
        _occurrenceSources.GetValueOrDefault(appointment, appointment);

    private IEnumerable<AppointmentModel> GetVisibleOccurrences()
    {
        var dates = VisibleDates();
        var start = CurrentView == SchedulerView.Month
            ? WeekStart(new DateTime(SelectedDate.Year, SelectedDate.Month, 1), FirstDayOfWeek) : dates[0];
        var end = CurrentView == SchedulerView.Month ? start.AddDays(42) : dates[^1].AddDays(1);
        // A zoned Planner may display UTC appointments from either adjacent date.
        if (CurrentView is SchedulerView.Planner or SchedulerView.Timeline)
        {
            start = start.AddDays(-1);
            end = end.AddDays(1);
        }
        foreach (var source in (Appointments ?? Array.Empty<AppointmentModel>()).Where(source => ShowsResources(source.ResourceIds)))
            foreach (var occurrence in Scheduler.Utilities.AppointmentOccurrences.InRange(source, start, end))
            {
                if (!ReferenceEquals(source, occurrence))
                    _occurrenceSources[occurrence] = source;
                yield return occurrence;
            }
    }
    private static bool Same(AppointmentModel a, AppointmentModel b) =>
        ReferenceEquals(a, b) || a.Id is not null && b.Id is not null && Equals(a.Id, b.Id);

    private static DateTime WeekStart(DateTime date, DayOfWeek firstDay) =>
        date.Date.AddDays(-((7 + (int)date.DayOfWeek - (int)firstDay) % 7));

    private static bool IsWeekend(DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    // Without weekends the Planner shows Monday to Friday as one stretch of time: the five days that share the most
    // days with the week. For a week that starts on Saturday, Sunday or Monday, these are its own weekdays.
    private DateTime PlannerStart()
    {
        var weekStart = WeekStart(SelectedDate.Date, FirstDayOfWeek);
        if (ShowWeekends)
            return weekStart;
        var monday = (7 + (int)DayOfWeek.Monday - (int)weekStart.DayOfWeek) % 7;
        return weekStart.AddDays(monday <= 4 ? monday : monday - 7);
    }

    private DateTime[] VisibleDates()
    {
        if (CurrentView is SchedulerView.Day or SchedulerView.Agenda && SelectedDates is { Count: > 0 })
            return SelectedDates.Select(static d => d.Date).Distinct().OrderBy(static d => d).ToArray();
        if (CurrentView == SchedulerView.Planner)
        {
            var plannerStart = PlannerStart();
            return Enumerable.Range(0, ShowWeekends ? 7 : 5).Select(day => plannerStart.AddDays(day)).ToArray();
        }
        var start = CurrentView == SchedulerView.Week ? WeekStart(SelectedDate.Date, FirstDayOfWeek) : SelectedDate.Date;
        var count = CurrentView == SchedulerView.Week ? 7 : CurrentView == SchedulerView.Calendar ? 1 : VisibleDays;
        var dates = new DateTime[count];
        for (var index = 0; index < count; index++)
            dates[index] = start.AddDays(index);
        return CurrentView == SchedulerView.Week && !ShowWeekends ? dates.Where(static date => !IsWeekend(date)).ToArray() : dates;
    }

    /// <summary>Navigate by the visible date span, week, or month.</summary>
    public void Navigate(int direction)
    {
        if (direction == 0)
            return;
        if (CurrentView is SchedulerView.Day or SchedulerView.Agenda && SelectedDates is { Count: > 0 })
        {
            var shifted = VisibleDates().Select(date => date.AddDays(7 * direction)).ToArray();
            SetCurrentValue(SelectedDatesProperty, shifted);
            SetCurrentValue(SelectedDateProperty, new DateTimeOffset(shifted[0]));
        }
        else
            SetCurrentValue(SelectedDateProperty, CurrentView == SchedulerView.Month
                ? SelectedDate.AddMonths(direction)
                : SelectedDate.AddDays(direction * (CurrentView is SchedulerView.Week or SchedulerView.Planner ? 7 :
                    CurrentView is SchedulerView.Timeline or SchedulerView.Calendar ? 1 : VisibleDays)));
    }

    /// <summary>Raise a deletion request without changing the host's appointment list.</summary>
    public void DeleteSelected(bool withShift = false)
    {
        if (IsReadOnly)
            return;
        var selected = _selection.Where(static appointment => !appointment.IsLocked).ToArray();
        if (selected.Length == 0)
            return;
        _selection.Clear();
        CommitSelection();
        AppointmentsDeleteRequested?.Invoke(this, new AppointmentsDeleteArgs { Appointments = selected, WithShift = withShift });
    }

    public void SetTileOptions(IReadOnlyList<SchedulerTileOption> options)
    {
        _tileOptions = options ?? throw new ArgumentNullException(nameof(options));
        RequestRender();
    }

    private void SelectAppointment(AppointmentModel appointment, bool multiple)
    {
        if (appointment.IsLocked)
            return;
        var previous = _selection.FirstOrDefault(selected => Same(selected, appointment));
        if (multiple && previous is not null)
            _selection.Remove(previous);
        else
        {
            if (!multiple)
                _selection.Clear();
            if (!_selection.Any(selected => Same(selected, appointment)))
                _selection.Add(appointment);
        }
        CommitSelection();
        Focus(NavigationMethod.Pointer);
    }

    private void CommitSelection()
    {
        // Retain the clicked lane across host model replacement, and release deselected models.
        foreach (var (appointment, resourceId) in _selectionResourceIds.ToArray())
        {
            var selected = _selection.FirstOrDefault(item => Same(item, appointment));
            if (ReferenceEquals(selected, appointment))
                continue;
            _selectionResourceIds.Remove(appointment);
            if (selected is not null)
                _selectionResourceIds[selected] = resourceId;
        }
        _selectionSyncing = true;
        SetAndRaise(SelectedAppointmentsProperty, ref _selectedAppointments, _selection.ToArray());
        SetCurrentValue(SelectedAppointmentProperty, _selection.Count == 1 ? _selection[0] : null);
        _selectionSyncing = false;
        ApplySelection();
        SelectionChanged?.Invoke(this, SelectedAppointments);
    }

    private void ApplySelection()
    {
        var content = Brush("DaisyBaseContentBrush");
        foreach (var (frame, appointment) in _frames)
        {
            if (frame.DataContext is not AppointmentContext context)
                continue;
            var selected = _selection.Any(item => Same(item, appointment));
            // Selection recolors the frame border and tints the background toward the theme text color; the border
            // keeps its thickness, so the appointment neither moves nor resizes, and no bar color hides the selection.
            frame.BorderBrush = selected ? content : context.BorderBrush;
            frame.Background = selected ? SelectedBackground(context.BackBrush, content) : context.BackBrush;
            frame.ZIndex = selected ? 10 : 0;
        }
    }

    private static IBrush SelectedBackground(IBrush background, IBrush content) =>
        background is ISolidColorBrush back && content is ISolidColorBrush tint
            ? new SolidColorBrush(FloweryColorHelpers.Blend(back.Color, tint.Color, 0.18))
            : background;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Typing in a host-supplied editor must not delete appointments.
        if (e.Source is TextBox)
            return;
        if (e.Key == Key.Escape)
        {
            CancelGesture();
            _selection.Clear();
            CommitSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && !IsReadOnly && _selection.Count > 0)
        {
            DeleteSelected(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SelectedAppointment is { IsLocked: false } appointment)
        {
            AppointmentEditRequested?.Invoke(this, new AppointmentEditRequestedArgs { Appointment = appointment });
            e.Handled = true;
        }
    }

    internal IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush ? brush : Brushes.Transparent;

    private TextBlock Label(string text, bool secondary = false) => new()
    {
        Text = text,
        FontSize = secondary ? Token<double>($"DaisySize{Size}SecondaryFontSize") : FontSize,
        FontFamily = FontFamily,
        Foreground = Brush(secondary ? "DaisyBaseContentMutedBrush" : "DaisyBaseContentBrush"),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    private static bool OnDate(AppointmentModel appointment, DateTime date) =>
        appointment.Start < date.AddDays(1) &&
        (appointment.End > date || appointment.End == appointment.Start && appointment.Start.Date == date);
}
