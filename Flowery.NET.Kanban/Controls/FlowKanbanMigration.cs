using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Provides schema migration support for Kanban board persistence.
    /// </summary>
    public static class FlowKanbanMigration
    {
        /// <summary>
        /// Current schema version. Increment when making breaking changes to the data model.
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// Migrates a JSON document to the current schema version.
        /// </summary>
        /// <param name="json">The raw JSON string from storage.</param>
        /// <returns>A deserialized FlowKanbanData at the current schema version.</returns>
        public static FlowKanbanData? MigrateFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = JsonDocument.Parse(json);
            var version = GetSchemaVersion(doc);

            // Apply sequential migrations
            var migrated = version < CurrentSchemaVersion
                ? MigrateToLatest(doc, version)
                : json;

            return JsonSerializer.Deserialize(migrated, FlowKanbanJsonContext.Default.FlowKanbanData);
        }

        /// <summary>
        /// Gets the schema version from a JSON document.
        /// </summary>
        public static int GetSchemaVersion(JsonDocument doc)
        {
            return doc.RootElement.TryGetProperty("schemaVersion", out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : 0; // Version 0 = pre-versioning schema
        }

        /// <summary>
        /// Applies all necessary migrations to bring a document to the current version.
        /// </summary>
        private static string MigrateToLatest(JsonDocument doc, int fromVersion)
        {
            var current = doc.RootElement.GetRawText();

            // V0 -> V1: Add schemaVersion, default WIP limits, blocked flags, etc.
            if (CurrentSchemaVersion >= 1 && fromVersion < 1)
            {
                current = MigrateV0ToV1(current);
            }

            // V1 -> V2: Add work item numbers and nextWorkItemNumber
            if (CurrentSchemaVersion >= 2 && fromVersion < 2)
            {
                current = MigrateV1ToV2(current);
            }

            return current;
        }

        /// <summary>
        /// Migrates from pre-versioning (V0) to V1.
        /// V1 adds: schemaVersion field (handled by model default)
        /// </summary>
        private static string MigrateV0ToV1(string json)
        {
            // For V0 -> V1, the model defaults handle the new fields automatically.
            // We just need to ensure the schemaVersion is set on load.
            // The JsonSerializer will use the model defaults for new fields.
            // No structural changes needed for this migration.
            return json;
        }

        /// <summary>
        /// Migrates from V1 to V2.
        /// V2 adds: workItemNumber on tasks and nextWorkItemNumber on the board.
        /// </summary>
        private static string MigrateV1ToV2(string json)
        {
            JsonNode? rootNode;
            try
            {
                rootNode = JsonNode.Parse(json);
            }
            catch
            {
                return json;
            }

            if (rootNode is not JsonObject root)
                return json;

            var tasksNeedingNumber = new List<JsonObject>();
            var usedNumbers = new HashSet<int>();
            var maxNumber = 0;

            if (root["columns"] is JsonArray columns)
            {
                foreach (var columnNode in columns)
                {
                    if (columnNode is not JsonObject column)
                        continue;

                    if (column["tasks"] is not JsonArray tasks)
                        continue;

                    foreach (var taskNode in tasks)
                    {
                        if (taskNode is not JsonObject task)
                            continue;

                        if (TryGetPositiveInt(task["workItemNumber"], out var number))
                        {
                            if (usedNumbers.Add(number))
                            {
                                if (number > maxNumber)
                                {
                                    maxNumber = number;
                                }
                            }
                            else
                            {
                                tasksNeedingNumber.Add(task);
                            }
                        }
                        else
                        {
                            tasksNeedingNumber.Add(task);
                        }
                    }
                }
            }

            var nextNumber = Math.Max(1, maxNumber + 1);
            foreach (var task in tasksNeedingNumber)
            {
                task["workItemNumber"] = nextNumber;
                if (nextNumber < int.MaxValue)
                {
                    nextNumber++;
                }
            }

            root["nextWorkItemNumber"] = nextNumber;
            root["schemaVersion"] = CurrentSchemaVersion;

            return root.ToJsonString(GetJsonOptions());
        }

        private static bool TryGetPositiveInt(JsonNode? node, out int value)
        {
            value = 0;
            if (node is not JsonValue jsonValue)
                return false;

            if (!jsonValue.TryGetValue(out int number))
                return false;

            if (number <= 0)
                return false;

            value = number;
            return true;
        }

        private static JsonObject EnsureArchiveColumn(JsonArray columns, string archiveColumnId)
        {
            JsonObject? archiveColumn = null;
            var duplicates = new List<JsonObject>();

            foreach (var columnNode in columns)
            {
                if (columnNode is not JsonObject column)
                    continue;

                var columnId = column["id"]?.GetValue<string>();
                if (!string.Equals(columnId, archiveColumnId, StringComparison.Ordinal))
                    continue;

                if (archiveColumn == null)
                {
                    archiveColumn = column;
                }
                else
                {
                    duplicates.Add(column);
                }
            }

            foreach (var duplicate in duplicates)
            {
                duplicate["id"] = Guid.NewGuid().ToString();
            }

            if (archiveColumn == null)
            {
                archiveColumn = new JsonObject
                {
                    ["id"] = archiveColumnId,
                    ["title"] = "Archive",
                    ["tasks"] = new JsonArray()
                };
                columns.Add((JsonNode?)archiveColumn);
            }

            if (archiveColumn["tasks"] is not JsonArray)
            {
                archiveColumn["tasks"] = new JsonArray();
            }

            return archiveColumn;
        }

        private static void ConsolidateArchivedTasks(JsonArray columns, JsonObject archiveColumn)
        {
            if (archiveColumn["tasks"] is not JsonArray archiveTasks)
                return;

            foreach (var columnNode in columns)
            {
                if (columnNode is not JsonObject column)
                    continue;

                if (ReferenceEquals(column, archiveColumn))
                    continue;

                if (column["tasks"] is not JsonArray tasks)
                    continue;

                for (var i = tasks.Count - 1; i >= 0; i--)
                {
                    if (tasks[i] is not JsonObject task)
                        continue;

                    if (task["isArchived"]?.GetValue<bool>() != true)
                        continue;

                    if (task["archivedFromColumnId"] == null)
                    {
                        task["archivedFromColumnId"] = column["id"]?.GetValue<string>();
                    }

                    if (task["archivedFromIndex"] == null)
                    {
                        task["archivedFromIndex"] = i;
                    }

                    tasks.RemoveAt(i);
                    archiveTasks.Add((JsonNode?)task);
                }
            }

            foreach (var taskNode in archiveTasks)
            {
                if (taskNode is not JsonObject task)
                    continue;

                task["isArchived"] = true;
            }
        }

        private static bool HasArchivedTasks(JsonObject root)
        {
            if (root["columns"] is not JsonArray columns)
                return false;

            foreach (var columnNode in columns)
            {
                if (columnNode is not JsonObject column)
                    continue;

                if (column["tasks"] is not JsonArray tasks)
                    continue;

                foreach (var taskNode in tasks)
                {
                    if (taskNode is not JsonObject task)
                        continue;

                    if (task["isArchived"]?.GetValue<bool>() == true)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the standard JSON serializer options for Kanban persistence.
        /// </summary>
        public static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }
    }
}
