using Avalonia;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class PreferencesRegressionTests
{
    [AvaloniaFact]
    public async Task PlatformContrastUpdatesOnUiThreadAndRestoresSelectedPalette()
    {
        var oldTheme = DaisyThemeManager.CurrentThemeName;
        var oldOverride = DaisyAccessibility.HighContrastOverride;
        var originalColors = Application.Current!.PlatformSettings!.GetColorValues();
        var button = new DaisyButton { Content = "Bound palette" };
        button.Bind(DaisyButton.BackgroundProperty, new DynamicResourceExtension("DaisyBase100Brush"));
        var glass = new DaisyGlass { Content = "Material", Width = 150, Height = 100 };
        var card = new DaisyCard();
        var window = new Window { Content = new StackPanel { Children = { button, glass, card } } };
        window.Show();
        Flush(window);
        var settings = new Settings();
        using var observer = DaisyAccessibility.ObservePlatformSettings(settings.Source);
        try
        {
            DaisyAccessibility.HighContrastOverride = null;
            DaisyThemeManager.ApplyTheme("Light");
            var normal = button.Background;
            await Task.Run(() => settings.Set(new PlatformColorValues
            {
                ContrastPreference = ColorContrastPreference.High, ThemeVariant = PlatformThemeVariant.Dark
            }));
            Flush(window);
            Assert.True(DaisyAccessibility.IsHighContrast);
            Assert.Null(card.Effect);
            Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(button.Background).Color);
            var fallback = glass.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_ContrastSurface");
            Assert.True(fallback.IsVisible);
            Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(fallback.Background).Color);
            DaisyThemeManager.ApplyTheme("Cupcake");
            Assert.Equal("Cupcake", DaisyThemeManager.CurrentThemeName);
            Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(button.Background).Color);
            settings.Set(new PlatformColorValues { ContrastPreference = ColorContrastPreference.NoPreference });
            Flush(window);
            Assert.False(DaisyAccessibility.IsHighContrast);
            Assert.NotNull(card.Effect);
            Assert.False(fallback.IsVisible);
            Assert.NotEqual(Colors.Black, Assert.IsType<SolidColorBrush>(button.Background).Color);
            DaisyThemeManager.ApplyTheme("Light");
            Assert.Equal(Assert.IsType<SolidColorBrush>(normal).Color, Assert.IsType<SolidColorBrush>(button.Background).Color);
        }
        finally
        {
            settings.Set(originalColors);
            DaisyAccessibility.HighContrastOverride = oldOverride;
            if (oldTheme != null) DaisyThemeManager.ApplyTheme(oldTheme);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HostContrastApplicatorOwnsEnableAndRestoreWithoutReplacingThemeIdentity()
    {
        var oldContrast = DaisyAccessibility.HighContrastOverride;
        var oldApplicator = DaisyAccessibility.CustomContrastApplicator;
        var oldTheme = DaisyThemeManager.CurrentThemeName;
        var states = new List<bool>();
        try
        {
            DaisyAccessibility.HighContrastOverride = false;
            DaisyAccessibility.CustomContrastApplicator = enabled => { states.Add(enabled); return true; };
            DaisyAccessibility.HighContrastOverride = true;
            Assert.True(states[^1]);
            DaisyAccessibility.HighContrastOverride = false;
            Assert.False(states[^1]);
            Assert.Equal(oldTheme, DaisyThemeManager.CurrentThemeName);
        }
        finally
        {
            DaisyAccessibility.CustomContrastApplicator = oldApplicator;
            DaisyAccessibility.HighContrastOverride = oldContrast;
        }
    }

    [AvaloniaFact]
    public void DisposedPlatformObserverCannotChangePreferences()
    {
        var oldOverride = DaisyAccessibility.HighContrastOverride;
        var originalColors = Application.Current!.PlatformSettings!.GetColorValues();
        var settings = new Settings();
        var observer = DaisyAccessibility.ObservePlatformSettings(settings.Source);
        try
        {
            DaisyAccessibility.HighContrastOverride = null;
            observer.Dispose();
            settings.Set(new PlatformColorValues { ContrastPreference = ColorContrastPreference.High });
            Assert.False(DaisyAccessibility.IsHighContrast);
        }
        finally
        {
            observer.Dispose();
            settings.Set(originalColors);
            using var restore = DaisyAccessibility.ObservePlatformSettings(settings.Source);
            DaisyAccessibility.HighContrastOverride = oldOverride;
        }
    }

    private static void Flush(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }

    private sealed class Settings
    {
        public IPlatformSettings Source { get; } = DispatchProxy.Create<IPlatformSettings, SettingsProxy>();
        public void Set(PlatformColorValues colors) => ((SettingsProxy)(object)Source).Set(colors);
    }

    // Avalonia marks platform implementations as private API in its reference assembly.
    // A runtime proxy supplies a controllable event source while the production observer uses only IPlatformSettings.
    public class SettingsProxy : DispatchProxy
    {
        private PlatformColorValues _colors = new();
        private EventHandler<PlatformColorValues>? _changed;
        public void Set(PlatformColorValues colors) { _colors = colors; _changed?.Invoke(this, colors); }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "GetColorValues": return _colors;
                case "add_ColorValuesChanged": _changed += (EventHandler<PlatformColorValues>)args![0]!; return null;
                case "remove_ColorValuesChanged": _changed -= (EventHandler<PlatformColorValues>)args![0]!; return null;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
