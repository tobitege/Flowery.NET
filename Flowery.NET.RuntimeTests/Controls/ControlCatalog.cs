using System.Reflection;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;

namespace Flowery.NET.RuntimeTests.Controls;

/// <summary>
/// The public, constructible controls of the Flowery.NET and Flowery.NET.Kanban assemblies, grouped like the sources.
/// </summary>
internal static class ControlCatalog
{
    private static readonly string[] SeparateAreas = [".ColorPicker", ".Custom"];

    public static IReadOnlyList<Type> GetDaisyControls() =>
        GetControls(typeof(DaisyButton).Assembly, "Flowery.Controls")
            .Where(type => !SeparateAreas.Any(area => type.Namespace!.Contains(area, StringComparison.Ordinal)))
            .ToList();

    public static IReadOnlyList<Type> GetColorPickerControls() =>
        GetControls(typeof(DaisyButton).Assembly, "Flowery.Controls.ColorPicker");

    public static IReadOnlyList<Type> GetCustomControls() =>
        GetControls(typeof(DaisyButton).Assembly, "Flowery.Controls.Custom");

    public static IReadOnlyList<Type> GetKanbanControls() =>
        GetControls(typeof(FlowKanban).Assembly, "Flowery.NET.Kanban.Controls");

    private static IReadOnlyList<Type> GetControls(Assembly assembly, string rootNamespace) =>
        assembly.GetExportedTypes()
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(type => type.Namespace is { } ns
                           && (ns == rootNamespace || ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal)))
            .Where(type => typeof(Control).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
}
