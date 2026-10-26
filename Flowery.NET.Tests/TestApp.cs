using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Services;

[assembly: AvaloniaTestApplication(typeof(Flowery.NET.Tests.TestAppBuilder))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]
// Plain facts run on the UI thread too. On a thread of their own, the first one to create a cached brush or resource
// would own it, and every UI test using it later would fail.
[assembly: AvaloniaTestFramework]

namespace Flowery.NET.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Styles.Add(new DaisyUITheme());
            Styles.Add(new StyleInclude(new Uri("avares://Flowery.NET.Tests/"))
            {
                Source = new Uri("avares://Flowery.NET.Kanban/Themes/Generic.axaml")
            });

            // A theme, a UI culture and a storage from the start, as in an app, so tests can put back the ones they
            // found. FloweryLocalization.SetCulture also sets the default UI culture of new threads.
            DaisyThemeManager.ApplyTheme("Light");
            CultureInfo.DefaultThreadCurrentUICulture = FloweryLocalization.CurrentCulture;
            StateStorageProvider.Configure(new InMemoryStateStorage());
        }
    }
}
