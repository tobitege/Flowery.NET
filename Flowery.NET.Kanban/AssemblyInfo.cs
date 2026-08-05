using Avalonia.Metadata;

[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Flowery.NET.Kanban.Controls")]
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Flowery.NET.Kanban.Converters")]

internal static class FlowKanbanLocalizationRegistration
{
    internal static void EnsureRegistered() =>
        Flowery.Localization.FloweryLocalization.RegisterAssembly(
            typeof(FlowKanbanLocalizationRegistration).Assembly);
}
