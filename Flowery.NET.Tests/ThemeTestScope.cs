using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Controls;

namespace Flowery.NET.Tests;

/// <summary>
/// A window for the theme list tests: every theme without preferences, "Business" current and the applied themes counted.
/// Disposing puts all of it back and removes the themes registered or added since.
/// </summary>
internal class ThemeTestScope : IDisposable
{
    private readonly IReadOnlyCollection<string> _excluded = DaisyThemeManager.ExcludedThemes;
    private readonly IReadOnlyCollection<string> _preferred = DaisyThemeManager.PreferredThemes;
    private readonly string? _current = DaisyThemeManager.CurrentThemeName;
    private readonly Func<string, bool>? _applicator = DaisyThemeManager.CustomThemeApplicator;
    private readonly bool _suppressed = DaisyThemeManager.SuppressThemeApplication;
    private readonly HashSet<string> _registered = ThemeRegistrations.Capture();

    protected ThemeTestScope(double width, double height)
    {
        Window = new Window { Width = width, Height = height };
        ThemeRegistrations.ClearExclusions();
        DaisyThemeManager.PreferredThemes = [];
        DaisyThemeManager.CustomThemeApplicator = Apply;
        DaisyThemeManager.SuppressThemeApplication = false;
        DaisyThemeManager.SetCurrentTheme("Business");
    }

    public Window Window { get; }
    public int ApplyCalls { get; private set; }

    public void Layout()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    private bool Apply(string name) { ApplyCalls++; return true; }

    public virtual void Dispose()
    {
        Window.Close();
        // The current theme cannot be unregistered, so it is restored first.
        DaisyThemeManager.SetCurrentTheme(_current ?? string.Empty);
        ThemeRegistrations.RemoveAddedSince(_registered);
        DaisyThemeManager.ExcludedThemes = _excluded;
        DaisyThemeManager.PreferredThemes = _preferred;
        DaisyThemeManager.CustomThemeApplicator = _applicator;
        DaisyThemeManager.SuppressThemeApplication = _suppressed;
    }
}
