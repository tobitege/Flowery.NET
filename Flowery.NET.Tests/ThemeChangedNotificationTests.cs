using System;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class ThemeChangedNotificationTests
{
    [AvaloniaFact]
    public void A_Throwing_Handler_Does_Not_Keep_Later_Handlers_From_Hearing_The_Theme_Change()
    {
        var original = DaisyThemeManager.CurrentThemeName ?? DaisyThemeManager.BaseThemeName;
        string? reached = null;
        EventHandler<string> throwing = (_, _) => throw new InvalidOperationException("Simulated failing subscriber");
        EventHandler<string> later = (_, name) => reached = name;
        DaisyThemeManager.ThemeChanged += throwing;
        DaisyThemeManager.ThemeChanged += later;
        try
        {
            DaisyThemeManager.SetCurrentTheme(original + "-test");
            Assert.Equal(original + "-test", reached);
        }
        finally
        {
            DaisyThemeManager.ThemeChanged -= throwing;
            DaisyThemeManager.ThemeChanged -= later;
            DaisyThemeManager.SetCurrentTheme(original);
        }
    }
}
