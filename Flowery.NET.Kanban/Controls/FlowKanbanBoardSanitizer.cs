using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Flowery.NET.Kanban.Controls
{
    internal static class FlowKanbanBoardSanitizer
    {
        private const int MaxJsonLength = 2_000_000;
        private const int MaxJsonDepth = 64;
        private const int MaxColumns = 120;
        private const int MaxTasksPerColumn = 500;
        private const int MaxTotalTasks = 10_000;
        private const int MaxSubtasksPerTask = 200;
        private const int MaxLanes = 50;
        private const int MaxTags = 200;
        private const int MaxCustomFields = 100;
        private const int MaxCustomFieldKeyLength = 80;
        private const int MaxBoardTitleLength = 200;
        private const int MaxBoardDescriptionLength = 4000;
        private const int MaxBoardCreatedByLength = 120;
        private const int MaxColumnTitleLength = 160;
        private const int MaxColumnPolicyLength = 2000;
        private const int MaxLaneTitleLength = 160;
        private const int MaxLaneDescriptionLength = 2000;
        private const int MaxTaskTitleLength = 200;
        private const int MaxTaskDescriptionLength = 4000;
        private const int MaxTaskTagsLength = 300;
        private const int MaxAssigneeLength = 120;
        private const int MaxAssigneeIdLength = 200;
        private const int MaxBlockedReasonLength = 400;
        private const int MaxSubtaskTitleLength = 160;
        private const int MaxTagTextLength = 80;
        private const int MaxWipLimit = 1000;
        private const int MaxLaneWipLimitEntries = 200;
        private const int MaxAutoArchiveDoneDays = 3650;

        internal static bool TryLoadFromJson(string json, out FlowKanbanData? data)
        {
            data = null;

            if (!IsSafeJsonContent(json))
                return false;

            try
            {
                var migrated = FlowKanbanMigration.MigrateFromJson(json);
                if (migrated == null)
                    return false;

                data = SanitizeBoard(migrated);
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        internal static bool TryBuildBoardMetadata(string json, string fallbackId, out FlowBoardMetadata metadata)
        {
            metadata = BuildFallbackMetadata(fallbackId);

            if (!IsSafeJsonContent(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
                var root = doc.RootElement;

                var title = TryReadString(root, "title");

                var createdAt = TryReadDate(root, "createdAt");
                var updatedAt = TryReadDate(root, "updatedAt");
                var lastModified = updatedAt ?? createdAt ?? DateTime.MinValue;

                var resolvedId = fallbackId;
                var resolvedTitle = SanitizeRequiredText(title, MaxBoardTitleLength, allowLineBreaks: false);
                if (string.IsNullOrWhiteSpace(resolvedTitle))
                {
                    resolvedTitle = FloweryLocalization.GetString("Kanban_Board_Untitled", "Untitled Board");
                }

                metadata = new FlowBoardMetadata
                {
                    Id = resolvedId ?? fallbackId,
                    Title = resolvedTitle,
                    LastModified = lastModified
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsValidId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            if (!string.Equals(trimmed, value, StringComparison.Ordinal))
                return false;

            return Guid.TryParse(trimmed, out var guid) && guid != Guid.Empty;
        }

        internal static string? NormalizeId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            if (!Guid.TryParse(trimmed, out var guid) || guid == Guid.Empty)
                return null;

            return guid.ToString();
        }

        internal static bool IsSafeJsonContent(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            if (json.Length > MaxJsonLength)
                return false;

            foreach (var ch in json)
            {
                if (ch == '\r' || ch == '\n' || ch == '\t')
                    continue;

                if (char.IsControl(ch))
                    return false;
            }

            return true;
        }

        private static FlowKanbanData SanitizeBoard(FlowKanbanData data)
        {
            if (!IsValidId(data.Id))
            {
                data.Id = Guid.NewGuid().ToString();
            }

            data.SchemaVersion = FlowKanbanMigration.CurrentSchemaVersion;
            data.Title = SanitizeRequiredText(data.Title, MaxBoardTitleLength, allowLineBreaks: false);
            data.Description = SanitizeRequiredText(data.Description, MaxBoardDescriptionLength, allowLineBreaks: true);
            data.CreatedBy = SanitizeRequiredText(data.CreatedBy, MaxBoardCreatedByLength, allowLineBreaks: false);
            data.CustomFields = SanitizeCustomFields(data.CustomFields);

            data.Tags = SanitizeTags(data.Tags);

            var lanes = SanitizeLanes(data.Lanes, out var laneIds);
            data.Lanes = lanes;

            var columns = SanitizeColumns(data.Columns, laneIds, out var columnIds);
            data.Columns = columns;

            data.ArchiveColumnId = SanitizeColumnReference(data.ArchiveColumnId, columnIds);
            data.DoneColumnId = SanitizeColumnReference(data.DoneColumnId, columnIds);

            if (!Enum.IsDefined(data.GroupBy))
            {
                data.GroupBy = FlowKanbanGroupBy.None;
            }

            data.AutoArchiveDoneDays = Clamp(data.AutoArchiveDoneDays, 1, MaxAutoArchiveDoneDays);

            SanitizeTaskArchiveReferences(columns, columnIds);
            FlowKanbanWorkItemNumberHelper.EnsureBoardNumbers(data);

            return data;
        }

        private static ObservableCollection<string> SanitizeTags(ObservableCollection<string>? tags)
        {
            var result = new ObservableCollection<string>();
            if (tags == null)
                return result;

            foreach (var tag in tags)
            {
                if (result.Count >= MaxTags)
                    break;

                var sanitized = SanitizeOptionalText(tag, MaxTagTextLength, allowLineBreaks: false);
                if (string.IsNullOrWhiteSpace(sanitized))
                    continue;

                result.Add(sanitized);
            }

            return result;
        }

        private static ObservableCollection<FlowKanbanLane> SanitizeLanes(
            ObservableCollection<FlowKanbanLane>? lanes,
            out HashSet<string> laneIds)
        {
            var result = new ObservableCollection<FlowKanbanLane>();
            laneIds = new HashSet<string>(StringComparer.Ordinal);

            if (lanes == null)
                return result;

            foreach (var lane in lanes)
            {
                if (result.Count >= MaxLanes)
                    break;

                if (lane == null)
                    continue;

                var id = NormalizeId(lane.Id);
                if (id == null || !laneIds.Add(id))
                    continue;

                lane.Id = id;
                lane.Title = SanitizeRequiredText(lane.Title, MaxLaneTitleLength, allowLineBreaks: false);
                lane.Description = SanitizeOptionalText(lane.Description, MaxLaneDescriptionLength, allowLineBreaks: true);
                lane.CustomFields = SanitizeCustomFields(lane.CustomFields);

                result.Add(lane);
            }

            return result;
        }

        private static ObservableCollection<FlowKanbanColumnData> SanitizeColumns(
            ObservableCollection<FlowKanbanColumnData>? columns,
            HashSet<string> laneIds,
            out HashSet<string> columnIds)
        {
            var result = new ObservableCollection<FlowKanbanColumnData>();
            columnIds = new HashSet<string>(StringComparer.Ordinal);

            if (columns == null)
                return result;

            var taskIds = new HashSet<string>(StringComparer.Ordinal);
            var totalTasks = 0;

            foreach (var column in columns)
            {
                if (result.Count >= MaxColumns)
                    break;

                if (column == null)
                    continue;

                var id = NormalizeId(column.Id);
                if (id == null || !columnIds.Add(id))
                    continue;

                column.Id = id;
                column.Title = SanitizeRequiredText(column.Title, MaxColumnTitleLength, allowLineBreaks: false);
                column.PolicyText = SanitizeOptionalText(column.PolicyText, MaxColumnPolicyLength, allowLineBreaks: true);
                column.CustomFields = SanitizeCustomFields(column.CustomFields);
                column.WipLimit = SanitizeWipLimit(column.WipLimit);
                column.LaneWipLimits = SanitizeLaneWipLimits(column.LaneWipLimits, laneIds);

                var tasks = new ObservableCollection<FlowTask>();
                if (column.Tasks != null)
                {
                    foreach (var task in column.Tasks)
                    {
                        if (tasks.Count >= MaxTasksPerColumn || totalTasks >= MaxTotalTasks)
                            break;

                        var sanitizedTask = SanitizeTask(task, laneIds, taskIds);
                        if (sanitizedTask == null)
                            continue;

                        tasks.Add(sanitizedTask);
                        totalTasks++;
                    }
                }

                column.Tasks = tasks;
                result.Add(column);
            }

            return result;
        }

        private static FlowTask? SanitizeTask(
            FlowTask? task,
            HashSet<string> laneIds,
            HashSet<string> taskIds)
        {
            if (task == null)
                return null;

            var id = NormalizeId(task.Id);
            if (id == null || !taskIds.Add(id))
                return null;

            task.Id = id;
            task.Title = SanitizeRequiredText(task.Title, MaxTaskTitleLength, allowLineBreaks: false);
            task.Description = SanitizeRequiredText(task.Description, MaxTaskDescriptionLength, allowLineBreaks: true);
            task.Tags = SanitizeOptionalText(task.Tags, MaxTaskTagsLength, allowLineBreaks: false);
            task.Assignee = SanitizeOptionalText(task.Assignee, MaxAssigneeLength, allowLineBreaks: false);
            task.AssigneeId = SanitizeOpaqueId(task.AssigneeId, MaxAssigneeIdLength);
            task.BlockedReason = SanitizeOptionalText(task.BlockedReason, MaxBlockedReasonLength, allowLineBreaks: true);
            task.CustomFields = SanitizeCustomFields(task.CustomFields);

            task.LaneId = SanitizeLaneReference(task.LaneId, laneIds);

            if (task.ArchivedFromIndex.HasValue && task.ArchivedFromIndex.Value < 0)
            {
                task.ArchivedFromIndex = null;
            }

            task.Subtasks = SanitizeSubtasks(task.Subtasks);

            return task;
        }

        private static ObservableCollection<FlowSubtask> SanitizeSubtasks(ObservableCollection<FlowSubtask>? subtasks)
        {
            var result = new ObservableCollection<FlowSubtask>();
            if (subtasks == null)
                return result;

            var subtaskIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var subtask in subtasks)
            {
                if (result.Count >= MaxSubtasksPerTask)
                    break;

                if (subtask == null)
                    continue;

                var id = NormalizeId(subtask.Id);
                if (id == null || !subtaskIds.Add(id))
                    continue;

                subtask.Id = id;
                subtask.Title = SanitizeRequiredText(subtask.Title, MaxSubtaskTitleLength, allowLineBreaks: false);
                result.Add(subtask);
            }

            return result;
        }

        private static void SanitizeTaskArchiveReferences(
            ObservableCollection<FlowKanbanColumnData> columns,
            HashSet<string> columnIds)
        {
            foreach (var column in columns)
            {
                foreach (var task in column.Tasks)
                {
                    var archivedFromId = NormalizeId(task.ArchivedFromColumnId);
                    if (archivedFromId == null || !columnIds.Contains(archivedFromId))
                    {
                        task.ArchivedFromColumnId = null;
                    }
                    else
                    {
                        task.ArchivedFromColumnId = archivedFromId;
                    }

                    if (task.ArchivedFromIndex.HasValue && task.ArchivedFromIndex.Value < 0)
                    {
                        task.ArchivedFromIndex = null;
                    }
                }
            }
        }

        private static int? SanitizeWipLimit(int? limit)
        {
            if (!limit.HasValue)
                return null;

            if (limit.Value < 0)
                return null;

            return limit.Value > MaxWipLimit ? MaxWipLimit : limit.Value;
        }

        private static Dictionary<string, int?> SanitizeLaneWipLimits(
            Dictionary<string, int?>? laneWipLimits,
            HashSet<string> laneIds)
        {
            var result = new Dictionary<string, int?>(StringComparer.Ordinal);
            if (laneWipLimits == null || laneWipLimits.Count == 0)
                return result;

            foreach (var pair in laneWipLimits)
            {
                if (result.Count >= MaxLaneWipLimitEntries)
                    break;

                var key = NormalizeLaneKey(pair.Key, laneIds);
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                    continue;

                var value = pair.Value;
                if (value.HasValue)
                {
                    if (value.Value < 0)
                        value = null;
                    else if (value.Value > MaxWipLimit)
                        value = MaxWipLimit;
                }

                result.Add(key, value);
            }

            return result;
        }

        private static string? NormalizeLaneKey(string? laneId, HashSet<string> laneIds)
        {
            if (string.IsNullOrWhiteSpace(laneId))
                return null;

            if (FlowKanban.IsUnassignedLaneId(laneId))
                return FlowKanban.UnassignedLaneId;

            var normalized = NormalizeId(laneId);
            if (normalized == null)
                return null;

            return laneIds.Contains(normalized) ? normalized : null;
        }

        private static string? SanitizeLaneReference(string? laneId, HashSet<string> laneIds)
        {
            if (string.IsNullOrWhiteSpace(laneId))
                return null;

            if (FlowKanban.IsUnassignedLaneId(laneId))
                return null;

            var normalized = NormalizeId(laneId);
            if (normalized == null || !laneIds.Contains(normalized))
                return null;

            return normalized;
        }

        private static string? SanitizeColumnReference(string? columnId, HashSet<string> columnIds)
        {
            if (string.IsNullOrWhiteSpace(columnId))
                return null;

            var normalized = NormalizeId(columnId);
            if (normalized == null || !columnIds.Contains(normalized))
                return null;

            return normalized;
        }

        private static Dictionary<string, JsonElement>? SanitizeCustomFields(Dictionary<string, JsonElement>? fields)
        {
            if (fields == null || fields.Count == 0)
                return null;

            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var pair in fields)
            {
                if (result.Count >= MaxCustomFields)
                    break;

                var key = SanitizeOptionalText(pair.Key, MaxCustomFieldKeyLength, allowLineBreaks: false);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!result.ContainsKey(key))
                {
                    result.Add(key, pair.Value);
                }
            }

            return result.Count == 0 ? null : result;
        }

        private static string SanitizeRequiredText(string? text, int maxLength, bool allowLineBreaks)
        {
            var sanitized = SanitizeTextCore(text, maxLength, allowLineBreaks);
            return string.IsNullOrWhiteSpace(sanitized) ? string.Empty : sanitized;
        }

        private static string? SanitizeOpaqueId(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
                return null;

            foreach (var ch in value)
            {
                if (char.IsControl(ch))
                    return null;
            }

            return value;
        }

        private static string? SanitizeOptionalText(string? text, int maxLength, bool allowLineBreaks)
        {
            var sanitized = SanitizeTextCore(text, maxLength, allowLineBreaks);
            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        private static string? SanitizeTextCore(string? text, int maxLength, bool allowLineBreaks)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var builder = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (allowLineBreaks && (ch == '\r' || ch == '\n' || ch == '\t'))
                {
                    builder.Append(ch);
                    if (builder.Length >= maxLength)
                        break;
                    continue;
                }

                if (char.IsControl(ch))
                    continue;

                builder.Append(ch);
                if (builder.Length >= maxLength)
                    break;
            }

            var sanitized = builder.ToString().Trim();
            return sanitized;
        }

        private static string? TryReadString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element))
                return null;

            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }

        private static DateTime? TryReadDate(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element))
                return null;

            if (element.ValueKind != JsonValueKind.String)
                return null;

            var text = element.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
                ? value
                : null;
        }

        private static FlowBoardMetadata BuildFallbackMetadata(string fallbackId)
        {
            return new FlowBoardMetadata
            {
                Id = fallbackId,
                Title = FloweryLocalization.GetString("Kanban_Board_Untitled", "Untitled Board"),
                LastModified = DateTime.MinValue
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;

            return value > max ? max : value;
        }
    }
}
