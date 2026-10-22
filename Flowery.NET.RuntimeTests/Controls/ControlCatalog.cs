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
        GetControls(typeof(DaisyButton).Assembly, "Flowery.Controls").Where(IsDaisyArea).ToList();

    public static IReadOnlyList<Type> GetColorPickerControls() =>
        GetControls(typeof(DaisyButton).Assembly, "Flowery.Controls.ColorPicker");

    public static IReadOnlyList<Type> GetCustomControls() =>
        GetControls(typeof(DaisyButton).Assembly, "Flowery.Controls.Custom");

    public static IReadOnlyList<Type> GetKanbanControls() =>
        GetControls(typeof(FlowKanban).Assembly, "Flowery.NET.Kanban.Controls");

    private static bool IsDaisyArea(Type type)
    {
        foreach (var area in SeparateAreas)
        {
            if (type.Namespace!.Contains(area, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static List<Type> GetControls(Assembly assembly, string rootNamespace)
    {
        var controls = new List<Type>();
        foreach (var type in assembly.GetExportedTypes())
        {
            if (type is { IsAbstract: false, IsGenericTypeDefinition: false, Namespace: { } ns }
                && (ns == rootNamespace || ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal))
                && typeof(Control).IsAssignableFrom(type))
            {
                controls.Add(type);
            }
        }

        controls.Sort(static (first, second) => StringComparer.Ordinal.Compare(first.FullName, second.FullName));
        return controls;
    }
}
