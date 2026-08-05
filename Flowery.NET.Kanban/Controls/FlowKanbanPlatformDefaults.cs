namespace Flowery.NET.Kanban.Controls;

internal enum FlowKanbanRuntimePlatform
{
    Desktop,
    Browser,
    Android,
    IOS
}

internal readonly record struct FlowKanbanPlatformDefaults(
    bool EnableStaggeredTaskRendering,
    bool IsKeyboardHelpVisible,
    bool IsColumnTooltipsEnabled)
{
    internal static FlowKanbanPlatformDefaults Current => For(DetectPlatform());

    internal static FlowKanbanPlatformDefaults For(FlowKanbanRuntimePlatform platform)
    {
        return platform switch
        {
            FlowKanbanRuntimePlatform.Android => new(true, false, false),
            FlowKanbanRuntimePlatform.IOS => new(true, true, true),
            FlowKanbanRuntimePlatform.Browser => new(false, false, false),
            _ => new(false, true, true)
        };
    }

    private static FlowKanbanRuntimePlatform DetectPlatform()
    {
        if (OperatingSystem.IsAndroid())
            return FlowKanbanRuntimePlatform.Android;

        if (OperatingSystem.IsIOS())
            return FlowKanbanRuntimePlatform.IOS;

        return OperatingSystem.IsBrowser()
            ? FlowKanbanRuntimePlatform.Browser
            : FlowKanbanRuntimePlatform.Desktop;
    }
}
