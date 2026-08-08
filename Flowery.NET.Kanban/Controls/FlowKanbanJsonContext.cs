using System.Text.Json.Serialization;

namespace Flowery.NET.Kanban.Controls
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(FlowKanbanData))]
    [JsonSerializable(typeof(FlowKanbanColumnData))]
    [JsonSerializable(typeof(FlowKanbanLane))]
    [JsonSerializable(typeof(FlowTask))]
    [JsonSerializable(typeof(FlowSubtask))]
    [JsonSerializable(typeof(FlowKanban.FlowKanbanSettingsState))]
    [JsonSerializable(typeof(FlowKanbanFilterCriteria))]
    [JsonSerializable(typeof(FlowKanbanFilterPreset))]
    [JsonSerializable(typeof(FlowKanbanFilterPresetCollection))]
    [JsonSerializable(typeof(FlowKanbanDateRange))]
    internal partial class FlowKanbanJsonContext : JsonSerializerContext
    {
    }
}
