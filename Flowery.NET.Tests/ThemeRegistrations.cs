using Flowery.Controls;

namespace Flowery.NET.Tests;

/// <summary>
/// Registered themes stay with <see cref="DaisyThemeManager"/> for the whole test run, so a test that registers
/// themes, directly or by selecting a product theme, removes them again.
/// </summary>
internal static class ThemeRegistrations
{
    /// <summary>The names of all registered themes. Clears the exclusions, which hide themes from the list.</summary>
    public static HashSet<string> Capture()
    {
        DaisyThemeManager.ExcludedThemes = Array.Empty<string>();
        return DaisyThemeManager.AvailableThemes.Select(static theme => theme.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Unregisters the themes registered since <paramref name="before"/> was captured.</summary>
    public static void RemoveAddedSince(HashSet<string> before)
    {
        foreach (var name in Capture().Except(before, StringComparer.OrdinalIgnoreCase).ToList())
            DaisyThemeManager.UnregisterTheme(name);
    }
}
