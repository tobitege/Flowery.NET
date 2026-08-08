using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Partial class containing filterbar-related properties and methods for FlowKanban.
    /// </summary>
    public partial class FlowKanban
    {
        private const string FilterPresetsStorageKey = "kanban.filter.presets.global";

        private ObservableCollection<FlowKanbanFilterPreset> _filterPresets = new();
        private DatePicker? _filterDateFromPicker;
        private DatePicker? _filterDateToPicker;
        private bool _isSyncingFilterDates;
        private bool _isSyncingSearchText;
        private bool _isSyncingAssigneeFilter;

        #region Filter DPs

        public static readonly StyledProperty<FlowKanbanFilterCriteria?> FilterCriteriaProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanFilterCriteria?>(
                                nameof(FilterCriteria),
                                default!);

        /// <summary>
        /// Current filter criteria applied to the board.
        /// </summary>
        public FlowKanbanFilterCriteria? FilterCriteria
        {
            get => (FlowKanbanFilterCriteria?)GetValue(FilterCriteriaProperty);
            set => SetValue(FilterCriteriaProperty, value);
        }

        public static readonly StyledProperty<bool> IsFilterActiveProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsFilterActive),
                false);

        /// <summary>
        /// True when any filter criterion is active.
        /// </summary>
        public bool IsFilterActive
        {
            get => (bool)GetValue(IsFilterActiveProperty);
            private set => SetValue(IsFilterActiveProperty, value);
        }

        public static readonly StyledProperty<int> ActiveFilterCountProperty =
            AvaloniaProperty.Register<FlowKanban, int>(
                nameof(ActiveFilterCount),
                0);

        /// <summary>
        /// Number of active filter criteria.
        /// </summary>
        public int ActiveFilterCount
        {
            get => (int)GetValue(ActiveFilterCountProperty);
            private set => SetValue(ActiveFilterCountProperty, value);
        }

        public static readonly StyledProperty<bool> IsFilterExpandedProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsFilterExpanded),
                false);

        /// <summary>
        /// Whether the filter panel is expanded.
        /// </summary>
        public bool IsFilterExpanded
        {
            get => (bool)GetValue(IsFilterExpandedProperty);
            set => SetValue(IsFilterExpandedProperty, value);
        }

        public static readonly StyledProperty<bool> FilterShowOverdueProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterShowOverdue),
                                false);

        /// <summary>
        /// Whether to filter to only overdue tasks.
        /// </summary>
        public bool FilterShowOverdue
        {
            get => (bool)GetValue(FilterShowOverdueProperty);
            set => SetValue(FilterShowOverdueProperty, value);
        }

        private static void OnFilterShowOverdueChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            var criteria = kanban.EnsureFilterCriteria();
            criteria.ShowOnlyOverdue = e.GetNewValue<bool>();
            criteria.NotifyChanged();
        }

        public static readonly StyledProperty<bool> FilterShowBlockedProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterShowBlocked),
                                false);

        /// <summary>
        /// Whether to filter to only blocked tasks.
        /// </summary>
        public bool FilterShowBlocked
        {
            get => (bool)GetValue(FilterShowBlockedProperty);
            set => SetValue(FilterShowBlockedProperty, value);
        }

        private static void OnFilterShowBlockedChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            var criteria = kanban.EnsureFilterCriteria();
            criteria.ShowOnlyBlocked = e.GetNewValue<bool>();
            criteria.NotifyChanged();
        }

        #region Priority Filter Bridge DPs

        public static readonly StyledProperty<bool> FilterPriorityLowProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterPriorityLow),
                                false);

        /// <summary>
        /// Whether Low priority filter is active.
        /// </summary>
        public bool FilterPriorityLow
        {
            get => (bool)GetValue(FilterPriorityLowProperty);
            set => SetValue(FilterPriorityLowProperty, value);
        }

        private static void OnFilterPriorityLowChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdatePriorityFilter(FlowTaskPriority.Low, e.GetNewValue<bool>());
        }

        public static readonly StyledProperty<bool> FilterPriorityNormalProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterPriorityNormal),
                                false);

        /// <summary>
        /// Whether Normal priority filter is active.
        /// </summary>
        public bool FilterPriorityNormal
        {
            get => (bool)GetValue(FilterPriorityNormalProperty);
            set => SetValue(FilterPriorityNormalProperty, value);
        }

        private static void OnFilterPriorityNormalChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdatePriorityFilter(FlowTaskPriority.Normal, e.GetNewValue<bool>());
        }

        public static readonly StyledProperty<bool> FilterPriorityHighProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterPriorityHigh),
                                false);

        /// <summary>
        /// Whether High priority filter is active.
        /// </summary>
        public bool FilterPriorityHigh
        {
            get => (bool)GetValue(FilterPriorityHighProperty);
            set => SetValue(FilterPriorityHighProperty, value);
        }

        private static void OnFilterPriorityHighChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdatePriorityFilter(FlowTaskPriority.High, e.GetNewValue<bool>());
        }

        public static readonly StyledProperty<bool> FilterPriorityUrgentProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterPriorityUrgent),
                                false);

        /// <summary>
        /// Whether Urgent priority filter is active.
        /// </summary>
        public bool FilterPriorityUrgent
        {
            get => (bool)GetValue(FilterPriorityUrgentProperty);
            set => SetValue(FilterPriorityUrgentProperty, value);
        }

        private static void OnFilterPriorityUrgentChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdatePriorityFilter(FlowTaskPriority.Urgent, e.GetNewValue<bool>());
        }

        private void UpdatePriorityFilter(FlowTaskPriority priority, bool isSelected)
        {
            var criteria = EnsureFilterCriteria();
            if (criteria.Priorities == null)
                criteria.Priorities = new List<FlowTaskPriority>();

            if (isSelected && !criteria.Priorities.Contains(priority))
                criteria.Priorities.Add(priority);
            else if (!isSelected && criteria.Priorities.Contains(priority))
                criteria.Priorities.Remove(priority);

            criteria.NotifyChanged();
        }

        #endregion

        #region Date Range Filter Bridge DPs

        public static readonly StyledProperty<DateTimeOffset?> FilterDateFromProperty =
            AvaloniaProperty.Register<FlowKanban, DateTimeOffset?>(
                                nameof(FilterDateFrom),
                                null);

        /// <summary>
        /// Start date for due date range filter.
        /// </summary>
        public DateTimeOffset? FilterDateFrom
        {
            get => (DateTimeOffset?)GetValue(FilterDateFromProperty);
            set => SetValue(FilterDateFromProperty, value);
        }

        private static void OnFilterDateFromChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdateDateRangeFilter();
            kanban.SyncFilterDatePickers();
        }

        public static readonly StyledProperty<DateTimeOffset?> FilterDateToProperty =
            AvaloniaProperty.Register<FlowKanban, DateTimeOffset?>(
                                nameof(FilterDateTo),
                                null);

        /// <summary>
        /// End date for due date range filter.
        /// </summary>
        public DateTimeOffset? FilterDateTo
        {
            get => (DateTimeOffset?)GetValue(FilterDateToProperty);
            set => SetValue(FilterDateToProperty, value);
        }

        private static void OnFilterDateToChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdateDateRangeFilter();
            kanban.SyncFilterDatePickers();
        }

        private void UpdateDateRangeFilter()
        {
            var criteria = EnsureFilterCriteria();
            var from = FilterDateFrom?.DateTime;
            var to = FilterDateTo?.DateTime;

            if (from.HasValue || to.HasValue)
            {
                criteria.PlannedEndRange = new FlowKanbanDateRange
                {
                    From = from,
                    To = to?.Date.AddDays(1).AddTicks(-1) // End of day
                };
            }
            else
            {
                criteria.PlannedEndRange = null;
            }

            criteria.NotifyChanged();
        }

        #endregion

        #region Quick Date Filter Bridge DPs

        public static readonly StyledProperty<bool> FilterDueTodayProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterDueToday),
                                false);

        /// <summary>
        /// Whether the "Today" quick date filter is active.
        /// </summary>
        public bool FilterDueToday
        {
            get => (bool)GetValue(FilterDueTodayProperty);
            set => SetValue(FilterDueTodayProperty, value);
        }

        private static void OnFilterDueTodayChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdateQuickDateFilters();
        }

        public static readonly StyledProperty<bool> FilterDueThisWeekProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                                nameof(FilterDueThisWeek),
                                false);

        /// <summary>
        /// Whether the "This Week" quick date filter is active.
        /// </summary>
        public bool FilterDueThisWeek
        {
            get => (bool)GetValue(FilterDueThisWeekProperty);
            set => SetValue(FilterDueThisWeekProperty, value);
        }

        private static void OnFilterDueThisWeekChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdateQuickDateFilters();
        }

        private void UpdateQuickDateFilters()
        {
            var today = DateTime.Today;
            DateTime? from = null;
            DateTime? to = null;

            if (FilterDueToday)
            {
                from = today;
                to = today;
            }

            if (FilterDueThisWeek)
            {
                var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
                if (daysUntilSunday == 0) daysUntilSunday = 7;
                var weekEnd = today.AddDays(daysUntilSunday - 1);

                // Combine with Today if both are checked
                if (from.HasValue)
                {
                    // Keep from as today, extend to to week end
                    to = weekEnd;
                }
                else
                {
                    from = today;
                    to = weekEnd;
                }
            }

            // Update the bridge DPs (which will trigger UpdateDateRangeFilter)
            if (from.HasValue)
            {
                FilterDateFrom = new DateTimeOffset(from.Value);
                FilterDateTo = new DateTimeOffset(to!.Value);
            }
            else
            {
                // Clear date filter when both unchecked
                FilterDateFrom = null;
                FilterDateTo = null;
            }
        }

        #endregion

        #region Assignee Filter DPs

        public static readonly StyledProperty<ObservableCollection<FlowTaskAssigneeOption>> AssigneeFilterOptionsProperty =
            AvaloniaProperty.Register<FlowKanban, ObservableCollection<FlowTaskAssigneeOption>>(
                nameof(AssigneeFilterOptions),
                default!);

        /// <summary>
        /// Available assignees for filtering.
        /// </summary>
        public ObservableCollection<FlowTaskAssigneeOption> AssigneeFilterOptions
        {
            get
            {
                if (GetValue(AssigneeFilterOptionsProperty) is not ObservableCollection<FlowTaskAssigneeOption> options)
                {
                    options = new ObservableCollection<FlowTaskAssigneeOption>();
                    SetValue(AssigneeFilterOptionsProperty, options);
                }
                return options;
            }
            private set => SetValue(AssigneeFilterOptionsProperty, value);
        }

        public static readonly StyledProperty<FlowTaskAssigneeOption?> SelectedAssigneeFilterProperty =
            AvaloniaProperty.Register<FlowKanban, FlowTaskAssigneeOption?>(
                                nameof(SelectedAssigneeFilter),
                                default!);

        /// <summary>
        /// Selected assignee filter option.
        /// </summary>
        public FlowTaskAssigneeOption? SelectedAssigneeFilter
        {
            get => (FlowTaskAssigneeOption?)GetValue(SelectedAssigneeFilterProperty);
            set => SetValue(SelectedAssigneeFilterProperty, value);
        }

        public static readonly StyledProperty<bool> HasAssigneeOptionsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(HasAssigneeOptions),
                false);

        /// <summary>
        /// True when assignee options are available.
        /// </summary>
        public bool HasAssigneeOptions
        {
            get => (bool)GetValue(HasAssigneeOptionsProperty);
            private set => SetValue(HasAssigneeOptionsProperty, value);
        }

        private static void OnSelectedAssigneeFilterChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var kanban = (FlowKanban)d;
            kanban.UpdateAssigneeFilterFromSelection();
        }

        #endregion

        public static readonly StyledProperty<bool> IsFilterDirtyProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsFilterDirty),
                false);

        /// <summary>
        /// True when filter criteria have been modified since last save/load.
        /// </summary>
        public bool IsFilterDirty
        {
            get => (bool)GetValue(IsFilterDirtyProperty);
            private set => SetValue(IsFilterDirtyProperty, value);
        }

        public static readonly StyledProperty<FlowKanbanFilterPreset?> ActivePresetProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanFilterPreset?>(
                nameof(ActivePreset),
                default!);

        /// <summary>
        /// Currently loaded filter preset.
        /// </summary>
        public FlowKanbanFilterPreset? ActivePreset
        {
            get => (FlowKanbanFilterPreset?)GetValue(ActivePresetProperty);
            private set => SetValue(ActivePresetProperty, value);
        }

        public static readonly StyledProperty<ObservableCollection<FlowKanbanFilterPreset>> FilterPresetsProperty =
            AvaloniaProperty.Register<FlowKanban, ObservableCollection<FlowKanbanFilterPreset>>(
                nameof(FilterPresets),
                default!);

        /// <summary>
        /// Available filter presets.
        /// </summary>
        public ObservableCollection<FlowKanbanFilterPreset> FilterPresets
        {
            get
            {
                if (GetValue(FilterPresetsProperty) is not ObservableCollection<FlowKanbanFilterPreset> presets)
                {
                    presets = new ObservableCollection<FlowKanbanFilterPreset>();
                    SetValue(FilterPresetsProperty, presets);
                }
                return presets;
            }
            private set => SetValue(FilterPresetsProperty, value);
        }

        public static readonly StyledProperty<bool> HasFilterPresetsProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(HasFilterPresets),
                false);

        /// <summary>
        /// True when saved filter presets exist.
        /// </summary>
        public bool HasFilterPresets
        {
            get => (bool)GetValue(HasFilterPresetsProperty);
            private set => SetValue(HasFilterPresetsProperty, value);
        }

        #endregion

        #region Filter Commands

        public static readonly StyledProperty<ICommand> ClearFilterCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ClearFilterCommand),
                default!);

        public ICommand ClearFilterCommand
        {
            get => (ICommand)GetValue(ClearFilterCommandProperty);
            set => SetValue(ClearFilterCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> SaveFilterPresetCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(SaveFilterPresetCommand),
                default!);

        public ICommand SaveFilterPresetCommand
        {
            get => (ICommand)GetValue(SaveFilterPresetCommandProperty);
            set => SetValue(SaveFilterPresetCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> LoadFilterPresetCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(LoadFilterPresetCommand),
                default!);

        public ICommand LoadFilterPresetCommand
        {
            get => (ICommand)GetValue(LoadFilterPresetCommandProperty);
            set => SetValue(LoadFilterPresetCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> DeleteFilterPresetCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(DeleteFilterPresetCommand),
                default!);

        public ICommand DeleteFilterPresetCommand
        {
            get => (ICommand)GetValue(DeleteFilterPresetCommandProperty);
            set => SetValue(DeleteFilterPresetCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ToggleFilterExpandedCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ToggleFilterExpandedCommand),
                default!);

        public ICommand ToggleFilterExpandedCommand
        {
            get => (ICommand)GetValue(ToggleFilterExpandedCommandProperty);
            set => SetValue(ToggleFilterExpandedCommandProperty, value);
        }

        #endregion

        #region Filter Callbacks

        private static void OnFilterCriteriaChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                if (e.OldValue is FlowKanbanFilterCriteria oldCriteria)
                {
                    oldCriteria.PropertyChanged -= kanban.OnFilterCriteriaPropertyChanged;
                }

                if (e.NewValue is FlowKanbanFilterCriteria newCriteria)
                {
                    newCriteria.PropertyChanged += kanban.OnFilterCriteriaPropertyChanged;
                }

                kanban.SyncSearchTextFromCriteria();
                kanban.SyncSelectedAssigneeFromCriteria();
                kanban.UpdateFilterState();
                kanban.ApplyFilter();
            }
        }

        private void OnFilterCriteriaPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            IsFilterDirty = true;
            if (string.Equals(e.PropertyName, nameof(FlowKanbanFilterCriteria.TextQuery), StringComparison.Ordinal))
            {
                SyncSearchTextFromCriteria();
            }
            SyncSelectedAssigneeFromCriteria();
            UpdateFilterState();
            ApplyFilter();
        }

        private void UpdateFilterState()
        {
            var criteria = FilterCriteria;
            IsFilterActive = criteria?.HasAnyFilter == true;
            ActiveFilterCount = CountActiveFilters(criteria);
        }

        private int CountActiveFilters(FlowKanbanFilterCriteria? criteria)
        {
            if (criteria == null)
                return 0;

            int count = 0;
            if (!string.IsNullOrWhiteSpace(criteria.TextQuery)) count++;
            if (criteria.Priorities?.Count > 0) count++;
            if (criteria.ShowOnlyOverdue == true) count++;
            if (criteria.ShowOnlyBlocked == true) count++;
            if (criteria.PlannedStartRange?.HasValue == true) count++;
            if (criteria.PlannedEndRange?.HasValue == true) count++;
            if (criteria.IncludedColumnIds?.Count > 0) count++;
            if (criteria.AssigneeIds?.Count > 0) count++;
            return count;
        }

        #endregion

        #region Filter Date Picker Sync

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            HookFilterDatePickers(e.NameScope);
            ResolveThemeRefreshTemplateParts(e.NameScope);
        }

        private void HookFilterDatePickers(INameScope nameScope)
        {
            DetachFilterDatePickers();

            _filterDateFromPicker = nameScope.Find<DatePicker>("PART_FilterDateFrom");
            _filterDateToPicker = nameScope.Find<DatePicker>("PART_FilterDateTo");

            if (_filterDateFromPicker != null)
            {
                _filterDateFromPicker.SelectedDateChanged += OnFilterDateFromPickerChanged;
            }

            if (_filterDateToPicker != null)
            {
                _filterDateToPicker.SelectedDateChanged += OnFilterDateToPickerChanged;
            }

            SyncFilterDatePickers();
        }

        private void DetachFilterDatePickers()
        {
            if (_filterDateFromPicker != null)
            {
                _filterDateFromPicker.SelectedDateChanged -= OnFilterDateFromPickerChanged;
            }

            if (_filterDateToPicker != null)
            {
                _filterDateToPicker.SelectedDateChanged -= OnFilterDateToPickerChanged;
            }
        }

        private void OnFilterDateFromPickerChanged(object? sender, DatePickerSelectedValueChangedEventArgs args)
        {
            if (_isSyncingFilterDates || sender is not DatePicker picker)
                return;

            _isSyncingFilterDates = true;
            try
            {
                FilterDateFrom = picker.SelectedDate;
            }
            finally
            {
                _isSyncingFilterDates = false;
            }
        }

        private void OnFilterDateToPickerChanged(object? sender, DatePickerSelectedValueChangedEventArgs args)
        {
            if (_isSyncingFilterDates || sender is not DatePicker picker)
                return;

            _isSyncingFilterDates = true;
            try
            {
                FilterDateTo = picker.SelectedDate;
            }
            finally
            {
                _isSyncingFilterDates = false;
            }
        }

        private void SyncFilterDatePickers()
        {
            if (_isSyncingFilterDates)
                return;

            _isSyncingFilterDates = true;
            try
            {
                if (_filterDateFromPicker != null && !Equals(_filterDateFromPicker.SelectedDate, FilterDateFrom))
                {
                    _filterDateFromPicker.SelectedDate = FilterDateFrom;
                }

                if (_filterDateToPicker != null && !Equals(_filterDateToPicker.SelectedDate, FilterDateTo))
                {
                    _filterDateToPicker.SelectedDate = FilterDateTo;
                }
            }
            finally
            {
                _isSyncingFilterDates = false;
            }
        }

        #endregion

        #region Filter Methods

        /// <summary>
        /// Initializes filter commands and loads saved presets.
        /// </summary>
        private void InitializeFilterbar()
        {
            ClearFilterCommand = new RelayCommand(ExecuteClearFilter);
            SaveFilterPresetCommand = new RelayCommand(ExecuteSaveFilterPreset);
            LoadFilterPresetCommand = new RelayCommand<FlowKanbanFilterPreset>(ExecuteLoadFilterPreset);
            DeleteFilterPresetCommand = new RelayCommand<FlowKanbanFilterPreset>(ExecuteDeleteFilterPreset);
            ToggleFilterExpandedCommand = new RelayCommand(ExecuteToggleFilterExpanded);

            LoadFilterPresets();
        }

        private FlowKanbanFilterCriteria EnsureFilterCriteria()
        {
            if (FilterCriteria == null)
                FilterCriteria = new FlowKanbanFilterCriteria();
            return FilterCriteria;
        }

        private void SyncSearchTextFromCriteria()
        {
            if (_isSyncingSearchText)
                return;

            _isSyncingSearchText = true;
            try
            {
                var criteriaText = FilterCriteria?.TextQuery ?? string.Empty;
                if (!string.Equals(SearchText, criteriaText, StringComparison.Ordinal))
                {
                    SearchText = criteriaText;
                }
            }
            finally
            {
                _isSyncingSearchText = false;
            }
        }

        private void SyncCriteriaTextFromSearch()
        {
            if (_isSyncingSearchText)
                return;

            if (FilterCriteria == null && string.IsNullOrWhiteSpace(SearchText))
                return;

            _isSyncingSearchText = true;
            try
            {
                var criteria = EnsureFilterCriteria();
                var normalized = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
                if (!string.Equals(criteria.TextQuery, normalized, StringComparison.Ordinal))
                {
                    criteria.TextQuery = normalized;
                }
            }
            finally
            {
                _isSyncingSearchText = false;
            }
        }

        private void UpdateAssigneeFilterFromSelection()
        {
            if (_isSyncingAssigneeFilter)
                return;

            var selected = SelectedAssigneeFilter;
            var criteria = FilterCriteria;
            if (criteria == null)
            {
                if (selected == null)
                    return;

                criteria = EnsureFilterCriteria();
            }

            _isSyncingAssigneeFilter = true;
            try
            {
                if (selected == null || string.IsNullOrWhiteSpace(selected.Id))
                {
                    criteria.AssigneeIds = null;
                }
                else
                {
                    criteria.AssigneeIds = new List<string> { selected.Id };
                }
                criteria.NotifyChanged();
            }
            finally
            {
                _isSyncingAssigneeFilter = false;
            }
        }

        private void SyncSelectedAssigneeFromCriteria()
        {
            if (_isSyncingAssigneeFilter)
                return;

            var assigneeId = FilterCriteria?.AssigneeIds?.FirstOrDefault();
            FlowTaskAssigneeOption? match = null;
            if (!string.IsNullOrWhiteSpace(assigneeId))
            {
                match = AssigneeFilterOptions.FirstOrDefault(option =>
                    string.Equals(option.Id, assigneeId, StringComparison.Ordinal));
            }

            _isSyncingAssigneeFilter = true;
            try
            {
                if (!ReferenceEquals(SelectedAssigneeFilter, match))
                {
                    SelectedAssigneeFilter = match;
                }
            }
            finally
            {
                _isSyncingAssigneeFilter = false;
            }
        }

        private void UpdateAssigneeFilterOptions(IReadOnlyList<FlowTaskAssigneeOption> resolvedOptions)
        {
            var options = BuildAssigneeFilterOptions(resolvedOptions);
            var items = AssigneeFilterOptions;
            items.Clear();

            foreach (var option in options)
            {
                items.Add(option);
            }

            HasAssigneeOptions = items.Count > 0;
            SyncSelectedAssigneeFromCriteria();
        }

        private List<FlowTaskAssigneeOption> BuildAssigneeFilterOptions(
            IEnumerable<FlowTaskAssigneeOption> resolvedOptions)
        {
            var options = new List<FlowTaskAssigneeOption>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var option in resolvedOptions ?? Array.Empty<FlowTaskAssigneeOption>())
            {
                if (option?.Id is not { } id || string.IsNullOrWhiteSpace(id))
                    continue;

                if (!seenIds.Add(id))
                    continue;

                var displayName = string.IsNullOrWhiteSpace(option.DisplayName)
                    ? id
                    : option.DisplayName.Trim();
                options.Add(new FlowTaskAssigneeOption(id, displayName));
            }

            var unresolvedLabel = FloweryLocalization.GetString("Kanban_Users_Unresolved", "Unavailable");
            foreach (var column in Board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    var id = task.AssigneeId;
                    if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                        continue;

                    var displayName = string.IsNullOrWhiteSpace(task.Assignee)
                        ? id
                        : task.Assignee.Trim();
                    options.Add(new FlowTaskAssigneeOption(
                        id,
                        $"{displayName} ({unresolvedLabel})",
                        isResolved: false));
                }
            }

            options.Sort((left, right) =>
                string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            return options;
        }

        private void ExecuteClearFilter()
        {
            // Reset all bridge DPs first (prevents re-triggering filter updates)
            FilterPriorityLow = false;
            FilterPriorityNormal = false;
            FilterPriorityHigh = false;
            FilterPriorityUrgent = false;
            FilterShowOverdue = false;
            FilterShowBlocked = false;
            FilterDueToday = false;
            FilterDueThisWeek = false;
            FilterDateFrom = null;
            FilterDateTo = null;
            SelectedAssigneeFilter = null;

            // Clear the criteria (NotifyChanged is called internally by Clear())
            FilterCriteria?.Clear();
            ActivePreset = null;
            IsFilterDirty = false;

            // Update filter state to refresh badge
            UpdateFilterState();
            ApplyFilter();
        }

        private async void ExecuteSaveFilterPreset()
        {
            if (FilterCriteria == null || TopLevel == null)
                return;

            var presetName = await ShowInputDialogAsync(
                FloweryLocalization.GetString("Kanban_Filter_SavePreset", "Save Filter Preset"),
                ActivePreset?.Name ?? string.Empty,
                FloweryLocalization.GetString("Kanban_Filter_PresetName", "Preset Name"));

            if (string.IsNullOrWhiteSpace(presetName))
                return;

            var sanitizedName = FlowKanbanFilterPreset.SanitizeName(presetName);
            if (string.IsNullOrEmpty(sanitizedName))
                return;

            FlowKanbanFilterPreset preset;
            if (ActivePreset != null)
            {
                preset = ActivePreset;
                preset.Name = sanitizedName;
                preset.Criteria = FilterCriteria.Clone();
                preset.LastUsedAt = DateTime.Now;
            }
            else
            {
                preset = new FlowKanbanFilterPreset
                {
                    Name = sanitizedName,
                    Criteria = FilterCriteria.Clone()
                };
                FilterPresets.Add(preset);
            }

            ActivePreset = preset;
            IsFilterDirty = false;
            SaveFilterPresets();
            UpdateHasFilterPresets();
        }

        private void ExecuteLoadFilterPreset(FlowKanbanFilterPreset? preset)
        {
            if (preset == null)
                return;

            FilterCriteria = preset.Criteria.Clone();
            ActivePreset = preset;
            preset.LastUsedAt = DateTime.Now;
            IsFilterDirty = false;
            SaveFilterPresets();
        }

        private void ExecuteDeleteFilterPreset(FlowKanbanFilterPreset? preset)
        {
            if (preset == null)
                return;

            FilterPresets.Remove(preset);
            if (ActivePreset?.Id == preset.Id)
            {
                ActivePreset = null;
            }
            SaveFilterPresets();
            UpdateHasFilterPresets();
        }

        private void ExecuteToggleFilterExpanded()
        {
            IsFilterExpanded = !IsFilterExpanded;
        }

        /// <summary>
        /// Applies filter criteria to all visible tasks.
        /// </summary>
        private void ApplyFilter()
        {
            ApplySearchFilter();
        }

        #endregion

        #region Preset Persistence

        private void LoadFilterPresets()
        {
            try
            {
                var lines = StateStorageProvider.Instance.LoadLines(FilterPresetsStorageKey);
                if (lines.Count == 0)
                    return;

                var json = string.Join(Environment.NewLine, lines);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var collection = JsonSerializer.Deserialize<FlowKanbanFilterPresetCollection>(
                    json,
                    FlowKanbanJsonContext.Default.FlowKanbanFilterPresetCollection);
                if (collection?.Presets != null)
                {
                    FilterPresets.Clear();
                    foreach (var preset in collection.Presets)
                    {
                        FilterPresets.Add(preset);
                    }
                }
                UpdateHasFilterPresets();
            }
            catch
            {
                // Ignore deserialization errors
            }
        }

        private void SaveFilterPresets()
        {
            try
            {
                var collection = new FlowKanbanFilterPresetCollection
                {
                    Presets = new List<FlowKanbanFilterPreset>(FilterPresets)
                };
                var json = JsonSerializer.Serialize(
                    collection,
                    FlowKanbanJsonContext.Default.FlowKanbanFilterPresetCollection);
                var lines = json.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                StateStorageProvider.Instance.SaveLines(FilterPresetsStorageKey, lines);
            }
            catch
            {
                // Ignore serialization errors
            }
        }

        private void UpdateHasFilterPresets()
        {
            HasFilterPresets = FilterPresets.Count > 0;
        }

        #endregion

        #region Enhanced Task Matching

        /// <summary>
        /// Checks if a task matches the current filter criteria.
        /// </summary>
        private bool IsTaskMatchWithCriteria(FlowTask task, FlowKanbanFilterCriteria? criteria)
        {
            if (criteria == null || !criteria.HasAnyFilter)
                return true;

            // Text search
            if (!string.IsNullOrWhiteSpace(criteria.TextQuery))
            {
                var query = criteria.TextQuery.Trim();
                var comparison = StringComparison.OrdinalIgnoreCase;
                var textMatch = false;

                if (int.TryParse(query, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var workItemNumber)
                    && workItemNumber > 0
                    && task.WorkItemNumber == workItemNumber)
                {
                    textMatch = true;
                }

                if (criteria.MatchTitle && !string.IsNullOrWhiteSpace(task.Title) &&
                    task.Title.Contains(query, comparison))
                    textMatch = true;

                if (!textMatch && criteria.MatchDescription && !string.IsNullOrWhiteSpace(task.Description) &&
                    task.Description.Contains(query, comparison))
                    textMatch = true;

                if (!textMatch && criteria.MatchTags && !string.IsNullOrWhiteSpace(task.Tags) &&
                    task.Tags.Contains(query, comparison))
                    textMatch = true;

                if (!textMatch && criteria.MatchAssignee && !string.IsNullOrWhiteSpace(task.Assignee) &&
                    task.Assignee.Contains(query, comparison))
                    textMatch = true;

                if (!textMatch)
                    return false;
            }

            // Priority filter
            if (criteria.Priorities?.Count > 0 && !criteria.Priorities.Contains(task.Priority))
                return false;

            // Overdue filter
            if (criteria.ShowOnlyOverdue == true && !task.IsOverdue)
                return false;

            // Blocked filter
            if (criteria.ShowOnlyBlocked == true && !task.IsBlocked)
                return false;

            // Planned start date range
            if (criteria.PlannedStartRange?.HasValue == true)
            {
                if (!task.PlannedStartDate.HasValue ||
                    !criteria.PlannedStartRange.IsInRange(task.PlannedStartDate))
                    return false;
            }

            // Planned end date range
            if (criteria.PlannedEndRange?.HasValue == true)
            {
                if (!task.PlannedEndDate.HasValue ||
                    !criteria.PlannedEndRange.IsInRange(task.PlannedEndDate))
                    return false;
            }

            // Assignee filter
            if (criteria.AssigneeIds?.Count > 0)
            {
                var matchesAssignee = !string.IsNullOrWhiteSpace(task.AssigneeId) &&
                                      criteria.AssigneeIds.Contains(task.AssigneeId);

                if (!matchesAssignee)
                {
                    var assigneeName = task.Assignee?.Trim();
                    if (!string.IsNullOrWhiteSpace(assigneeName))
                    {
                        foreach (var assigneeId in criteria.AssigneeIds)
                        {
                            var option = AssigneeFilterOptions.FirstOrDefault(candidate =>
                                string.Equals(candidate.Id, assigneeId, StringComparison.Ordinal));
                            if (option != null &&
                                string.Equals(option.DisplayName, assigneeName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchesAssignee = true;
                                break;
                            }
                        }
                    }
                }

                if (!matchesAssignee)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a column should be included based on filter criteria.
        /// </summary>
        private bool IsColumnIncluded(FlowKanbanColumnData column, FlowKanbanFilterCriteria? criteria)
        {
            if (criteria == null)
                return true;

            // Check archive exclusion
            if (criteria.ExcludeArchive && column.IsArchiveColumn)
                return false;

            // Check explicit column inclusion
            if (criteria.IncludedColumnIds?.Count > 0)
            {
                return criteria.IncludedColumnIds.Contains(column.Id);
            }

            return true;
        }

        #endregion
    }
}
