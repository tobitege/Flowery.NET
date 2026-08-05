using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flowery.Controls;

namespace Flowery.NET.Kanban.Controls;

/// <summary>
/// Shared Avalonia base for the Kanban controls.
/// </summary>
public abstract class FlowKanbanContentControl : ContentControl
{
    static FlowKanbanContentControl()
    {
        FlowKanbanLocalizationRegistration.EnsureRegistered();
    }

    protected FlowKanbanContentControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override Type StyleKeyOverride => GetType();

    protected TopLevel? TopLevel => Avalonia.Controls.TopLevel.GetTopLevel(this);

    protected virtual void OnThemeChanged(string themeName)
    {
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        DaisyThemeManager.ThemeChanged += OnThemeManagerChanged;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DaisyThemeManager.ThemeChanged -= OnThemeManagerChanged;
    }

    private void OnThemeManagerChanged(object? sender, string themeName)
    {
        OnThemeChanged(themeName);
    }
}
