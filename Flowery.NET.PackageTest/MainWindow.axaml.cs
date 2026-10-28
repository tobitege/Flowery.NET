using System.Reflection;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.Skia;

namespace Flowery.NET.PackageTest;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Packages.Text = $"{Describe(typeof(DaisyGlass).Assembly)}\n{Describe(typeof(FlowerySkia).Assembly)}";
    }

    /// <summary>
    /// Gets the name of an assembly with its package version and the commit it was built from.
    /// </summary>
    internal static string Describe(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return $"{assembly.GetName().Name} {version}";
    }
}
