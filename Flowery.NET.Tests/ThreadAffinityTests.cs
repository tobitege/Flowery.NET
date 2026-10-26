using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;
using Flowery.Skia;
using Xunit;

namespace Flowery.NET.Tests;

public class ThreadAffinityTests
{
    // Singletons that XAML binds to. DaisyUITheme creates them on the UI thread when an app loads the theme.
    private static readonly string[] BindableSingletons = ["Flowery.Controls.DaisyAccessibility.<Preferences>k__BackingField"];

    [AvaloniaFact]
    public void No_Static_Field_Shares_An_Avalonia_Object()
    {
        // An Avalonia object belongs to the thread that created it. Kept in a static field, it is created by
        // whichever thread first touches the class, and every window on another thread then fails to render it.
        var shared = new[] { typeof(DaisyButton).Assembly, typeof(FlowKanban).Assembly, typeof(FlowerySkia).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => !type.ContainsGenericParameters)
            .SelectMany(static type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            // Fields set once when the type initializes; the others hold state an app sets on its UI thread.
            .Where(static field => field.IsInitOnly && field.GetValue(null) is AvaloniaObject)
            .Select(static field => $"{field.DeclaringType!.FullName}.{field.Name}")
            .Except(BindableSingletons)
            .ToList();

        Assert.True(shared.Count == 0, "Static fields holding Avalonia objects: " + string.Join(", ", shared));
    }
}
