using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>Helpers that many test files share; they take them in with <c>using static</c>.</summary>
internal static class TestControls
{
    /// <summary>Shows <paramref name="content"/> in a new window, laid out once the pending jobs ran.</summary>
    public static Window Show(Control content, double width = 400, double height = 300)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    /// <summary>The only template part of <paramref name="control"/> with this name.</summary>
    public static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    /// <summary>
    /// The first template part of <paramref name="control"/> with this name, for a control whose nested items have parts
    /// of the same name, such as the child items of a tree item: its own part comes first.
    /// </summary>
    public static T FirstPart<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    /// <summary>The resource the application finds for its current theme variant, or null.</summary>
    public static object? Resource(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) ? value : null;

    public static Color ThemeColor(string key) => Assert.IsType<Color>(Resource(key));
}
