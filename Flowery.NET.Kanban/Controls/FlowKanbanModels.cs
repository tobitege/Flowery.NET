using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flowery.Localization;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Represents a task/card in the Kanban board with full project management properties.
    /// </summary>
    public class FlowTask : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private int _workItemNumber;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private DaisyColor _palette = DaisyColor.Default;
        private bool _isSearchMatch = true;

        // Scheduling
        private DateTime? _plannedStartDate;
        private DateTime? _plannedEndDate;
        private DateTime? _actualStartDate;
        private DateTime? _actualEndDate;

        // Effort tracking
        private double? _estimatedHours;
        private double? _actualHours;
        private double? _estimatedDays;
        private double? _actualDays;

        // Status & Priority
        private FlowTaskPriority _priority = FlowTaskPriority.Normal;
        private int _progressPercent;
        private DateTime? _completedAt;

        // Assignment & Categorization
        private string? _assignee;
        private string? _assigneeId;
        private string? _tags;
        private string? _laneId;

        // Blocked state
        private bool _isBlocked;
        private string? _blockedReason;
        private DateTime? _blockedSince;

        // Archive status
        private bool _isArchived;
        private DateTime? _archivedAt;
        private string? _archivedFromColumnId;
        private int? _archivedFromIndex;

        // Selection state (UI-only, not persisted)
        private bool _isSelected;
        private ObservableCollection<FlowSubtask> _subtasks = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>Sequential work item number (1-based).</summary>
        [JsonPropertyName("workItemNumber")]
        public int WorkItemNumber
        {
            get => _workItemNumber;
            set => SetProperty(ref _workItemNumber, Math.Max(0, value));
        }

        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        [JsonPropertyName("description")]
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        [JsonPropertyName("palette")]
        public DaisyColor Palette
        {
            get => _palette;
            set => SetProperty(ref _palette, value);
        }

        #region Scheduling Properties

        /// <summary>Planned start date for the task.</summary>
        [JsonPropertyName("plannedStartDate")]
        public DateTime? PlannedStartDate
        {
            get => _plannedStartDate;
            set => SetProperty(ref _plannedStartDate, value);
        }

        /// <summary>Planned end/due date for the task.</summary>
        [JsonPropertyName("plannedEndDate")]
        public DateTime? PlannedEndDate
        {
            get => _plannedEndDate;
            set => SetProperty(ref _plannedEndDate, value);
        }

        /// <summary>Actual start date when work began.</summary>
        [JsonPropertyName("actualStartDate")]
        public DateTime? ActualStartDate
        {
            get => _actualStartDate;
            set => SetProperty(ref _actualStartDate, value);
        }

        /// <summary>Actual completion date.</summary>
        [JsonPropertyName("actualEndDate")]
        public DateTime? ActualEndDate
        {
            get => _actualEndDate;
            set => SetProperty(ref _actualEndDate, value);
        }

        #endregion

        #region Effort Tracking Properties

        /// <summary>Estimated effort in hours.</summary>
        [JsonPropertyName("estimatedHours")]
        public double? EstimatedHours
        {
            get => _estimatedHours;
            set => SetProperty(ref _estimatedHours, value);
        }

        /// <summary>Actual hours spent on the task.</summary>
        [JsonPropertyName("actualHours")]
        public double? ActualHours
        {
            get => _actualHours;
            set => SetProperty(ref _actualHours, value);
        }

        /// <summary>Estimated effort in days (alternative to hours for larger tasks).</summary>
        [JsonPropertyName("estimatedDays")]
        public double? EstimatedDays
        {
            get => _estimatedDays;
            set => SetProperty(ref _estimatedDays, value);
        }

        /// <summary>Actual days spent on the task.</summary>
        [JsonPropertyName("actualDays")]
        public double? ActualDays
        {
            get => _actualDays;
            set => SetProperty(ref _actualDays, value);
        }

        #endregion

        #region Status & Priority Properties

        /// <summary>Task priority level.</summary>
        [JsonPropertyName("priority")]
        public FlowTaskPriority Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        /// <summary>Progress percentage (0-100).</summary>
        [JsonPropertyName("progressPercent")]
        public int ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, Math.Clamp(value, 0, 100));
        }

        /// <summary>Timestamp when the task entered the Done column.</summary>
        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set => SetProperty(ref _completedAt, value);
        }

        #endregion

        #region Assignment & Categorization Properties

        /// <summary>Person assigned to this task.</summary>
        [JsonPropertyName("assignee")]
        public string? Assignee
        {
            get => _assignee;
            set => SetProperty(ref _assignee, value);
        }

        /// <summary>Composite user ID of the assignee (provider-prefixed when available).</summary>
        [JsonPropertyName("assigneeId")]
        public string? AssigneeId
        {
            get => _assigneeId;
            set => SetProperty(ref _assigneeId, value);
        }

        /// <summary>Comma-separated tags for categorization.</summary>
        [JsonPropertyName("tags")]
        public string? Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        /// <summary>Optional swimlane identifier.</summary>
        [JsonPropertyName("laneId")]
        public string? LaneId
        {
            get => _laneId;
            set => SetProperty(ref _laneId, value);
        }

        #endregion

        #region Blocked State

        /// <summary>Whether this task is currently blocked.</summary>
        [JsonPropertyName("isBlocked")]
        public bool IsBlocked
        {
            get => _isBlocked;
            set
            {
                if (SetProperty(ref _isBlocked, value))
                {
                    if (value && !_blockedSince.HasValue)
                        BlockedSince = DateTime.Now;
                    else if (!value)
                    {
                        BlockedSince = null;
                        BlockedReason = null;
                    }
                }
            }
        }

        /// <summary>Reason why the task is blocked.</summary>
        [JsonPropertyName("blockedReason")]
        public string? BlockedReason
        {
            get => _blockedReason;
            set => SetProperty(ref _blockedReason, value);
        }

        /// <summary>Timestamp when the task became blocked.</summary>
        [JsonPropertyName("blockedSince")]
        public DateTime? BlockedSince
        {
            get => _blockedSince;
            set => SetProperty(ref _blockedSince, value);
        }

        /// <summary>Number of days the task has been blocked.</summary>
        [JsonIgnore]
        public int? BlockedDays => BlockedSince.HasValue
            ? (int)(DateTime.Now - BlockedSince.Value).TotalDays
            : null;

        #endregion

        #region Archive Status

        /// <summary>Whether this task has been archived.</summary>
        [JsonPropertyName("isArchived")]
        public bool IsArchived
        {
            get => _isArchived;
            set
            {
                if (SetProperty(ref _isArchived, value))
                    ArchivedAt = value ? DateTime.Now : null;
            }
        }

        /// <summary>Timestamp when the task was archived.</summary>
        [JsonPropertyName("archivedAt")]
        public DateTime? ArchivedAt
        {
            get => _archivedAt;
            set => SetProperty(ref _archivedAt, value);
        }

        /// <summary>Original column ID before the task was archived.</summary>
        [JsonPropertyName("archivedFromColumnId")]
        public string? ArchivedFromColumnId
        {
            get => _archivedFromColumnId;
            set => SetProperty(ref _archivedFromColumnId, value);
        }

        /// <summary>Original index before the task was archived.</summary>
        [JsonPropertyName("archivedFromIndex")]
        public int? ArchivedFromIndex
        {
            get => _archivedFromIndex;
            set => SetProperty(ref _archivedFromIndex, value);
        }

        #endregion

        [JsonPropertyName("subtasks")]
        public ObservableCollection<FlowSubtask> Subtasks
        {
            get => _subtasks;
            set => SetProperty(ref _subtasks, value ?? new ObservableCollection<FlowSubtask>());
        }

        /// <summary>Extensible fields for custom data without schema changes.</summary>
        [JsonPropertyName("customFields")]
        public Dictionary<string, JsonElement>? CustomFields { get; set; }

        #region Computed Properties

        /// <summary>Returns true if the task is overdue (past planned end date without completion).</summary>
        [JsonIgnore]
        public bool IsOverdue => PlannedEndDate.HasValue &&
                                 PlannedEndDate.Value < DateTime.Now &&
                                 !ActualEndDate.HasValue;

        /// <summary>Calculates completion percentage based on subtasks if available.</summary>
        [JsonIgnore]
        public int SubtaskProgress => Subtasks.Count > 0
            ? (int)((double)Subtasks.Count(s => s.IsCompleted) / Subtasks.Count * 100)
            : 0;

        /// <summary>Returns the effective progress (manual or calculated from subtasks).</summary>
        [JsonIgnore]
        public int EffectiveProgress => Subtasks.Count > 0 ? SubtaskProgress : ProgressPercent;

        /// <summary>True when the task matches the current board search filter.</summary>
        [JsonIgnore]
        public bool IsSearchMatch
        {
            get => _isSearchMatch;
            set => SetProperty(ref _isSearchMatch, value);
        }

        /// <summary>Whether this task is currently selected for bulk operations.</summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        #endregion

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    /// <summary>
    /// Priority levels for Kanban tasks.
    /// </summary>
    public enum FlowTaskPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    public class FlowSubtask : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = string.Empty;
        private bool _isCompleted;

        public event PropertyChangedEventHandler? PropertyChanged;

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class FlowKanbanColumnData : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = string.Empty;
        private ObservableCollection<FlowTask> _tasks = new();
        private int? _wipLimit;
        private Dictionary<string, int?> _laneWipLimits = new(StringComparer.Ordinal);
        private string? _policyText;
        private bool _isArchiveColumn;
        private bool _isArchiveColumnVisible = true;
        private bool _isCollapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public FlowKanbanColumnData()
        {
            _tasks.CollectionChanged += OnTasksCollectionChanged;
        }

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Title) ? string.Empty : Title;
        }

        [JsonPropertyName("tasks")]
        public ObservableCollection<FlowTask> Tasks
        {
            get => _tasks;
            set
            {
                if (ReferenceEquals(_tasks, value))
                    return;

                _tasks.CollectionChanged -= OnTasksCollectionChanged;
                _tasks = value ?? new ObservableCollection<FlowTask>();
                _tasks.CollectionChanged += OnTasksCollectionChanged;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tasks)));
                RaiseWipIndicators();
            }
        }

        #region WIP Limits

        /// <summary>Maximum number of cards allowed in this column (null = unlimited).</summary>
        [JsonPropertyName("wipLimit")]
        public int? WipLimit
        {
            get => _wipLimit;
            set
            {
                if (SetProperty(ref _wipLimit, value))
                {
                    RaiseWipIndicators();
                }
            }
        }

        /// <summary>Whether the column has exceeded its WIP limit.</summary>
        [JsonIgnore]
        public bool IsWipExceeded => WipLimit.HasValue && Tasks.Count > WipLimit.Value;

        /// <summary>Display string for WIP status (e.g., "3/5" or "3").</summary>
        [JsonIgnore]
        public string WipDisplay => WipLimit.HasValue
            ? $"{Tasks.Count}/{WipLimit.Value}"
            : $"{Tasks.Count}";

        /// <summary>Per-lane WIP limits for this column (keyed by lane ID).</summary>
        [JsonPropertyName("laneWipLimits")]
        public Dictionary<string, int?> LaneWipLimits
        {
            get => _laneWipLimits;
            set => SetProperty(ref _laneWipLimits, value ?? new Dictionary<string, int?>(StringComparer.Ordinal));
        }

        public int? GetLaneWipLimit(string? laneId)
        {
            var laneKey = NormalizeLaneKey(laneId);
            if (!string.IsNullOrWhiteSpace(laneKey)
                && LaneWipLimits.TryGetValue(laneKey, out var limit))
            {
                return limit;
            }

            return WipLimit;
        }

        public int GetLaneWipCount(string? laneId)
        {
            var laneKey = NormalizeLaneKey(laneId);
            if (laneKey == null)
                return Tasks.Count(t => NormalizeLaneKey(t.LaneId) == null);

            return Tasks.Count(t => string.Equals(NormalizeLaneKey(t.LaneId), laneKey, StringComparison.Ordinal));
        }

        public string GetLaneWipDisplay(string? laneId)
        {
            var count = GetLaneWipCount(laneId);
            var limit = GetLaneWipLimit(laneId);
            return limit.HasValue ? $"{count}/{limit.Value}" : $"{count}";
        }

        public bool IsLaneWipExceeded(string? laneId)
        {
            var limit = GetLaneWipLimit(laneId);
            return limit.HasValue && GetLaneWipCount(laneId) > limit.Value;
        }

        #endregion

        #region Collapsed State

        /// <summary>Whether this column is collapsed into a compact rail.</summary>
        [JsonPropertyName("isCollapsed")]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (SetProperty(ref _isCollapsed, value))
                {
                    OnPropertyChanged(nameof(DisplayTitle));
                }
            }
        }

        [JsonIgnore]
        public string DisplayTitle => IsCollapsed ? BuildVerticalTitle(Title) : Title;

        [JsonIgnore]
        public string CompactTitle => BuildCompactTitle(Title);

        #endregion

        #region Column Policy

        /// <summary>Definition of Done or entry criteria text for this column.</summary>
        [JsonPropertyName("policyText")]
        public string? PolicyText
        {
            get => _policyText;
            set => SetProperty(ref _policyText, value);
        }

        /// <summary>Whether this column has policy text defined.</summary>
        [JsonIgnore]
        public bool HasPolicy => !string.IsNullOrWhiteSpace(PolicyText);

        #endregion

        #region Archive Column

        /// <summary>True when this column is the board archive column.</summary>
        [JsonIgnore]
        public bool IsArchiveColumn
        {
            get => _isArchiveColumn;
            internal set => SetProperty(ref _isArchiveColumn, value);
        }

        /// <summary>True when the column should be visible (hidden only for archive column).</summary>
        [JsonIgnore]
        public bool IsArchiveColumnVisible
        {
            get => _isArchiveColumnVisible;
            internal set => SetProperty(ref _isArchiveColumnVisible, value);
        }

        #endregion

        /// <summary>Extensible fields for custom column data without schema changes.</summary>
        [JsonPropertyName("customFields")]
        public Dictionary<string, JsonElement>? CustomFields { get; set; }

        private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseWipIndicators();
        }

        private void RaiseWipIndicators()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsWipExceeded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WipDisplay)));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string BuildVerticalTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var builder = new StringBuilder(title.Length * 2);
            var previousWasBreak = false;

            foreach (var ch in title.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!previousWasBreak)
                    {
                        builder.AppendLine();
                        previousWasBreak = true;
                    }
                    continue;
                }

                builder.Append(ch);
                builder.AppendLine();
                previousWasBreak = false;
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildCompactTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            const int maxLength = 18;
            var trimmed = title.Trim();
            if (trimmed.Length <= maxLength)
                return trimmed;

            var prefix = trimmed.Substring(0, maxLength - 3).TrimEnd();
            if (prefix.Length == 0)
                prefix = trimmed.Substring(0, maxLength - 3);
            return string.Concat(prefix, "...");
        }

        private static string? NormalizeLaneKey(string? laneId)
        {
            if (FlowKanban.IsUnassignedLaneId(laneId))
                return FlowKanban.UnassignedLaneId;

            return FlowKanban.NormalizeLaneId(laneId);
        }
    }

    /// <summary>
    /// Grouping modes for Kanban board views.
    /// </summary>
    public enum FlowKanbanGroupBy
    {
        None,
        Lane
    }

    /// <summary>
    /// Represents a swimlane row for grouping tasks.
    /// </summary>
    public class FlowKanbanLane : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = string.Empty;
        private string? _description;

        public event PropertyChangedEventHandler? PropertyChanged;

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        [JsonPropertyName("description")]
        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>Extensible fields for custom lane data without schema changes.</summary>
        [JsonPropertyName("customFields")]
        public Dictionary<string, JsonElement>? CustomFields { get; set; }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    /// <summary>
    /// The root container for the entire Kanban board with metadata.
    /// </summary>
    public class FlowKanbanData : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = FloweryLocalization.GetString("Kanban_Board_Untitled", "Untitled Board");
        private string _description = string.Empty;
        private DateTime _createdAt = DateTime.Now;
        private ObservableCollection<FlowKanbanColumnData> _columns = new();
        private string _createdBy = string.Empty;
        private ObservableCollection<string> _tags = new();
        private ObservableCollection<FlowKanbanLane> _lanes = new();
        private FlowKanbanGroupBy _groupBy = FlowKanbanGroupBy.None;
        private string? _archiveColumnId;
        private bool _isArchiveColumnHidden;
        private string? _doneColumnId;
        private bool _autoArchiveDoneEnabled;
        private int _autoArchiveDoneDays = 14;
        private int _nextWorkItemNumber = 1;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Unique identifier (GUID).</summary>
        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>The board title displayed at the top of the Kanban.</summary>
        [JsonPropertyName("title")]
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>Optional description of the board's purpose.</summary>
        [JsonPropertyName("description")]
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>Timestamp when the board was created.</summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        /// <summary>Timestamp of the latest successful board save.</summary>
        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Name of the user who created the board.</summary>
        [JsonPropertyName("createdBy")]
        public string CreatedBy
        {
            get => _createdBy;
            set => SetProperty(ref _createdBy, value);
        }

        /// <summary>Available tags for this board.</summary>
        [JsonPropertyName("tags")]
        public ObservableCollection<string> Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value ?? new ObservableCollection<string>());
        }

        /// <summary>Optional swimlanes for grouping tasks.</summary>
        [JsonPropertyName("lanes")]
        public ObservableCollection<FlowKanbanLane> Lanes
        {
            get => _lanes;
            set => SetProperty(ref _lanes, value ?? new ObservableCollection<FlowKanbanLane>());
        }

        /// <summary>View-only grouping mode (does not change data structure).</summary>
        [JsonPropertyName("groupBy")]
        public FlowKanbanGroupBy GroupBy
        {
            get => _groupBy;
            set => SetProperty(ref _groupBy, value);
        }

        /// <summary>Schema version for migration support. Current version: 2.</summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = FlowKanbanMigration.CurrentSchemaVersion;

        /// <summary>Next work item number to assign (1-based).</summary>
        [JsonPropertyName("nextWorkItemNumber")]
        public int NextWorkItemNumber
        {
            get => _nextWorkItemNumber;
            set => SetProperty(ref _nextWorkItemNumber, Math.Max(1, value));
        }

        /// <summary>Board-wide archive column ID.</summary>
        [JsonPropertyName("archiveColumnId")]
        public string? ArchiveColumnId
        {
            get => _archiveColumnId;
            set => SetProperty(ref _archiveColumnId, value);
        }

        /// <summary>Whether the archive column is hidden from the board view.</summary>
        [JsonPropertyName("isArchiveColumnHidden")]
        public bool IsArchiveColumnHidden
        {
            get => _isArchiveColumnHidden;
            set => SetProperty(ref _isArchiveColumnHidden, value);
        }

        /// <summary>Column ID used to identify the Done column for aging.</summary>
        [JsonPropertyName("doneColumnId")]
        public string? DoneColumnId
        {
            get => _doneColumnId;
            set => SetProperty(ref _doneColumnId, value);
        }

        /// <summary>Whether Done cards should be auto-archived after a delay.</summary>
        [JsonPropertyName("autoArchiveDoneEnabled")]
        public bool AutoArchiveDoneEnabled
        {
            get => _autoArchiveDoneEnabled;
            set => SetProperty(ref _autoArchiveDoneEnabled, value);
        }

        /// <summary>Number of days to keep Done cards before auto-archiving.</summary>
        [JsonPropertyName("autoArchiveDoneDays")]
        public int AutoArchiveDoneDays
        {
            get => _autoArchiveDoneDays;
            set => SetProperty(ref _autoArchiveDoneDays, Math.Max(1, value));
        }

        [JsonPropertyName("columns")]
        public ObservableCollection<FlowKanbanColumnData> Columns
        {
            get => _columns;
            set => SetProperty(ref _columns, value ?? new ObservableCollection<FlowKanbanColumnData>());
        }

        /// <summary>Extensible fields for custom board data without schema changes.</summary>
        [JsonPropertyName("customFields")]
        public Dictionary<string, JsonElement>? CustomFields { get; set; }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    /// <summary>
    /// Date range for filtering by date fields.
    /// </summary>
    public sealed class FlowKanbanDateRange
    {
        [JsonPropertyName("from")]
        public DateTime? From { get; set; }

        [JsonPropertyName("to")]
        public DateTime? To { get; set; }

        [JsonIgnore]
        public bool HasValue => From.HasValue || To.HasValue;

        public bool IsInRange(DateTime? date)
        {
            if (!date.HasValue)
                return false;

            if (From.HasValue && date.Value.Date < From.Value.Date)
                return false;

            if (To.HasValue && date.Value.Date > To.Value.Date)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Filter criteria for Kanban board task filtering.
    /// </summary>
    public sealed class FlowKanbanFilterCriteria : INotifyPropertyChanged
    {
        private string? _textQuery;
        private bool _matchTitle = true;
        private bool _matchDescription = true;
        private bool _matchTags = true;
        private bool _matchAssignee = true;
        private bool? _showOnlyOverdue;
        private bool? _showOnlyBlocked;
        private bool _excludeArchive = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Free-text search query.</summary>
        [JsonPropertyName("textQuery")]
        public string? TextQuery
        {
            get => _textQuery;
            set => SetProperty(ref _textQuery, value);
        }

        /// <summary>Include Title in text search.</summary>
        [JsonPropertyName("matchTitle")]
        public bool MatchTitle
        {
            get => _matchTitle;
            set => SetProperty(ref _matchTitle, value);
        }

        /// <summary>Include Description in text search.</summary>
        [JsonPropertyName("matchDescription")]
        public bool MatchDescription
        {
            get => _matchDescription;
            set => SetProperty(ref _matchDescription, value);
        }

        /// <summary>Include Tags in text search.</summary>
        [JsonPropertyName("matchTags")]
        public bool MatchTags
        {
            get => _matchTags;
            set => SetProperty(ref _matchTags, value);
        }

        /// <summary>Include Assignee in text search.</summary>
        [JsonPropertyName("matchAssignee")]
        public bool MatchAssignee
        {
            get => _matchAssignee;
            set => SetProperty(ref _matchAssignee, value);
        }

        /// <summary>Priority levels to include (null = all).</summary>
        [JsonPropertyName("priorities")]
        public List<FlowTaskPriority>? Priorities { get; set; }

        /// <summary>Show only overdue tasks. Null = don't filter by overdue status.</summary>
        [JsonPropertyName("showOnlyOverdue")]
        public bool? ShowOnlyOverdue
        {
            get => _showOnlyOverdue;
            set => SetProperty(ref _showOnlyOverdue, value);
        }

        /// <summary>Show only blocked tasks. Null = don't filter by blocked status.</summary>
        [JsonPropertyName("showOnlyBlocked")]
        public bool? ShowOnlyBlocked
        {
            get => _showOnlyBlocked;
            set => SetProperty(ref _showOnlyBlocked, value);
        }

        /// <summary>Filter by planned start date range.</summary>
        [JsonPropertyName("plannedStartRange")]
        public FlowKanbanDateRange? PlannedStartRange { get; set; }

        /// <summary>Filter by planned end date range.</summary>
        [JsonPropertyName("plannedEndRange")]
        public FlowKanbanDateRange? PlannedEndRange { get; set; }

        /// <summary>Column IDs to include (null = all non-archive).</summary>
        [JsonPropertyName("includedColumnIds")]
        public List<string>? IncludedColumnIds { get; set; }

        /// <summary>Exclude archive column from results (default true).</summary>
        [JsonPropertyName("excludeArchive")]
        public bool ExcludeArchive
        {
            get => _excludeArchive;
            set => SetProperty(ref _excludeArchive, value);
        }

        /// <summary>Assignee IDs to filter by (null = all).</summary>
        [JsonPropertyName("assigneeIds")]
        public List<string>? AssigneeIds { get; set; }

        /// <summary>Returns true if any filter criterion is active.</summary>
        [JsonIgnore]
        public bool HasAnyFilter =>
            !string.IsNullOrWhiteSpace(TextQuery) ||
            (Priorities?.Count > 0) ||
            ShowOnlyOverdue == true ||
            ShowOnlyBlocked == true ||
            PlannedStartRange?.HasValue == true ||
            PlannedEndRange?.HasValue == true ||
            (IncludedColumnIds?.Count > 0) ||
            (AssigneeIds?.Count > 0);

        /// <summary>Creates a deep copy of this criteria.</summary>
        public FlowKanbanFilterCriteria Clone()
        {
            return new FlowKanbanFilterCriteria
            {
                TextQuery = TextQuery,
                MatchTitle = MatchTitle,
                MatchDescription = MatchDescription,
                MatchTags = MatchTags,
                MatchAssignee = MatchAssignee,
                Priorities = Priorities?.ToList(),
                ShowOnlyOverdue = ShowOnlyOverdue,
                ShowOnlyBlocked = ShowOnlyBlocked,
                PlannedStartRange = PlannedStartRange != null
                    ? new FlowKanbanDateRange { From = PlannedStartRange.From, To = PlannedStartRange.To }
                    : null,
                PlannedEndRange = PlannedEndRange != null
                    ? new FlowKanbanDateRange { From = PlannedEndRange.From, To = PlannedEndRange.To }
                    : null,
                IncludedColumnIds = IncludedColumnIds?.ToList(),
                ExcludeArchive = ExcludeArchive,
                AssigneeIds = AssigneeIds?.ToList()
            };
        }

        /// <summary>Resets all criteria to defaults.</summary>
        public void Clear()
        {
            TextQuery = null;
            MatchTitle = true;
            MatchDescription = true;
            MatchTags = true;
            MatchAssignee = true;
            Priorities = null;
            ShowOnlyOverdue = null;
            ShowOnlyBlocked = null;
            PlannedStartRange = null;
            PlannedEndRange = null;
            IncludedColumnIds = null;
            ExcludeArchive = true;
            AssigneeIds = null;
            NotifyChanged();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        /// <summary>Raises PropertyChanged for all criteria to force refresh.</summary>
        public void NotifyChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAnyFilter)));
        }
    }

    /// <summary>
    /// A saved filter preset with name and criteria.
    /// </summary>
    public sealed class FlowKanbanFilterPreset
    {
        public const int MaxNameLength = 50;

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _name = string.Empty;

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set => _name = SanitizeName(value);
        }

        [JsonPropertyName("criteria")]
        public FlowKanbanFilterCriteria Criteria { get; set; } = new();

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("lastUsedAt")]
        public DateTime LastUsedAt { get; set; } = DateTime.Now;

        /// <summary>Sanitizes the preset name: trims whitespace, removes invalid chars, enforces max length.</summary>
        public static string SanitizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Trim and remove control characters
            var sanitized = new StringBuilder(Math.Min(name.Length, MaxNameLength));
            foreach (var ch in name.Trim())
            {
                if (char.IsControl(ch))
                    continue;

                sanitized.Append(ch);
                if (sanitized.Length >= MaxNameLength)
                    break;
            }

            return sanitized.ToString();
        }
    }

    /// <summary>
    /// Collection of saved filter presets for global persistence.
    /// </summary>
    public sealed class FlowKanbanFilterPresetCollection
    {
        [JsonPropertyName("presets")]
        public List<FlowKanbanFilterPreset> Presets { get; set; } = new();
    }
}
